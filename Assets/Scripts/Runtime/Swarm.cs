using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using AnSim.Runtime.Utils;
using UnityEngine;

namespace AnSim.Runtime
{
  public class Swarm : MonoBehaviour
  {
    [Header("Dependencies")]
    public SwarmSimManager swarmSimManager;
    public Transform targetTransform;

    [Header("Fixed Simulation Settings")]
    [Range(1, 2048)]
    public int maxSlaveSwarmCount = 32;
    [Range(0.001f, 2.0f)]
    public float setupPositionVariance = 0.5f;
    public float setupVelocityVariance = 0.2f;

    [Header("Runtime Simulation Settings")]
    [Range(1, 2048)]
    public int activeSlaveSwarmCount = 32;
    [Range(1, 16)]
    public uint replicatesPerParticle = 5; //N
    [Range(0.001f, 2.0f)]
    public float boundaryDisturbanceConstant = 0.22f; //alpha
    [Range(0.001f, 0.02f)]
    public float globalBestDisturbanceConstant = 0.005f; //sigma_g
    [Range(0.001f, 1.0f)]
    public float mutationStrategyParameter = 0.22f; //sigma
    [Range(0.1f, 9.9f)]
    public float individualAccelerationConstant = 2.05f; //c1
    [Range(0.1f, 9.9f)]
    public float socialAccelerationConstant = 2.05f; //c2
    [Range(0.1f, 9.9f)]
    public float masterAccelerationConstant = 2.02f; //c3

    [Header("Render Settings")]
    public Mesh particleMesh;
    public int subMeshIndex = 0;
    public Color particleTint = Color.white;

    // General
    private int _slaveSwarmSize;
    private int _masterSwarmSize;

    // Compute Buffers
    private int _swarmBufferNameId;
    private ComputeBuffer _swarmBuffer;

    private int _swarmParticleBufferNameId;
    private ComputeBuffer _swarmParticleBuffer;

    private int _slaveSwarmUniformBufferNameId;
    private ComputeBuffer _slaveSwarmUniformBuffer;
    private SlaveSwarmUniforms[] _slaveSwarmUniforms;
    private int _slaveSwarmUniformsSize;

    private int _masterSwarmUniformBufferNameId;
    private ComputeBuffer _masterSwarmUniformBuffer;
    private MasterSwarmUniforms[] _masterSwarmUniforms;
    private int _masterSwarmUniformsSize;

    // Rendering
    private uint[] _renderingArgs = {0, 0, 0, 0, 0};
    private ComputeBuffer _renderingArgumentBuffer;
    private MaterialPropertyBlock _materialProperties;


    private void Awake()
    {
      _slaveSwarmSize = swarmSimManager.GetSlaveSwarmSize();
      _masterSwarmSize = swarmSimManager.GetMasterSwarmSize();


      #region Init GPU Buffers
      //Init Swarm Buffer
      _swarmBufferNameId =
        Shader.PropertyToID("SwarmBuffer");
      _swarmBuffer = new ComputeBuffer(GetMaxSwarmCount(), SwarmData.GetSize(), ComputeBufferType.Structured);

      //Init SwarmParticleBuffer
      _swarmParticleBufferNameId =
        Shader.PropertyToID("SwarmParticleBuffer");
      _swarmParticleBuffer = new ComputeBuffer(GetMaxSwarmParticleCount(), SwarmParticleData.GetSize(), ComputeBufferType.Structured);

      //Init SlaveSwarmUniformBuffer
      _slaveSwarmUniformsSize = Marshal.SizeOf(typeof(SlaveSwarmUniforms));
      _slaveSwarmUniformBufferNameId =
        Shader.PropertyToID("SlaveSwarmUniforms");
      _slaveSwarmUniformBuffer = new ComputeBuffer(1, _slaveSwarmUniformsSize, ComputeBufferType.Constant);
      _slaveSwarmUniforms = new SlaveSwarmUniforms[1];

      //Init MasterSwarmUniformBuffer
      _masterSwarmUniformsSize = Marshal.SizeOf(typeof(MasterSwarmUniforms));
      _masterSwarmUniformBufferNameId =
        Shader.PropertyToID("MasterSwarmUniforms");
      _masterSwarmUniformBuffer = new ComputeBuffer(1, _masterSwarmUniformsSize, ComputeBufferType.Constant);
      _masterSwarmUniforms = new MasterSwarmUniforms[1];
      #endregion

      #region Init Rendering
      _renderingArgumentBuffer = new ComputeBuffer(1, _renderingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

      subMeshIndex = Mathf.Clamp(subMeshIndex, 0, particleMesh.subMeshCount - 1);
      _renderingArgs[0] = particleMesh.GetIndexCount(subMeshIndex);
      _renderingArgs[1] = (uint) GetMaxSwarmParticleCount();
      _renderingArgs[2] = particleMesh.GetIndexStart(subMeshIndex);
      _renderingArgs[3] = particleMesh.GetBaseVertex(subMeshIndex);
      _renderingArgumentBuffer.SetData(_renderingArgs);

      _materialProperties = new MaterialPropertyBlock();
      _materialProperties.SetBuffer(_swarmParticleBufferNameId, _swarmParticleBuffer);
      _materialProperties.SetColor("particleTint", particleTint);
      #endregion
    }

    public void SetupSimulation(ComputeShader simulationShader,
    in CsKernelData csKernelData)
    {
      #region Setup Complete Swarm Simulation (Slave + Master)
      //Set Uniforms for Setup
      simulationShader.SetFloat("positionVariance", setupPositionVariance);
      simulationShader.SetFloat("velocityVariance", setupVelocityVariance);
      simulationShader.SetVector("startPosition", transform.position);
      simulationShader.SetVector("target", targetTransform.position);

      //Set Buffers for Setup
      simulationShader.SetBuffer(csKernelData.index, _swarmBufferNameId, _swarmBuffer);
      simulationShader.SetBuffer(csKernelData.index, _swarmParticleBufferNameId, _swarmParticleBuffer);

      simulationShader.Dispatch(csKernelData.index, GetMaxSwarmCount(), 1, 1);
      #endregion
    }

    public void RunSimulation(ComputeShader simulationShader, in CsKernelData slaveSimKernelData, in CsKernelData masterSimKernelData)
    {
      #region Run SlaveSwarm Simulation
      //Update Uniform Data
      _slaveSwarmUniforms[0].alpha = boundaryDisturbanceConstant;
      _slaveSwarmUniforms[0].c1 = individualAccelerationConstant;
      _slaveSwarmUniforms[0].c2 = socialAccelerationConstant;
      _slaveSwarmUniforms[0].inertiaWeight = 1.0f; //TODO
      _slaveSwarmUniforms[0].n = replicatesPerParticle;
      _slaveSwarmUniforms[0].target = targetTransform.position;
      //Update Uniform Buffer
      _slaveSwarmUniformBuffer.SetData(_slaveSwarmUniforms);

      //Set all Buffers
      //simulationShader.SetBuffer(slaveSimKernelData.index, _slaveSwarmUniformBufferNameId, _slaveSwarmUniformBuffer);
      Shader.SetGlobalConstantBuffer(_slaveSwarmUniformBufferNameId, _slaveSwarmUniformBuffer, 0, _slaveSwarmUniformsSize);
      simulationShader.SetBuffer(slaveSimKernelData.index, _swarmBufferNameId, _swarmBuffer);
      simulationShader.SetBuffer(slaveSimKernelData.index, _swarmParticleBufferNameId, _swarmParticleBuffer);

      simulationShader.Dispatch(slaveSimKernelData.index, GetCurrentSlaveSwarmCount(), 1, 1);
      #endregion

      #region Run MasterSwarm Simulation
      //Update Uniform Data
      _masterSwarmUniforms[0].alpha = boundaryDisturbanceConstant;
      _masterSwarmUniforms[0].c1 = individualAccelerationConstant;
      _masterSwarmUniforms[0].c2 = socialAccelerationConstant;
      _masterSwarmUniforms[0].c3 = masterAccelerationConstant;
      _masterSwarmUniforms[0].inertiaWeight = 1.0f; //TODO
      _masterSwarmUniforms[0].n = replicatesPerParticle;
      _masterSwarmUniforms[0].target = targetTransform.position;
      _masterSwarmUniforms[0].slaveGlobalBest = targetTransform.position; //TODO: Use actual best result of best SlaveSwarm

      _masterSwarmUniforms[0].swarmBufferMasterIndex = (uint)maxSlaveSwarmCount;
      _masterSwarmUniforms[0].swarmParticleBufferMasterOffset = 2048;//(uint)GetMaxSlaveSwarmParticleCount();

      //Update Uniform Buffer
      _masterSwarmUniformBuffer.SetData(_masterSwarmUniforms);

      //Set all Buffers
      //simulationShader.SetBuffer(masterSimKernelData.index, _masterSwarmUniformBufferNameId, _masterSwarmUniformBuffer);
      Shader.SetGlobalConstantBuffer(_masterSwarmUniformBufferNameId, _masterSwarmUniformBuffer, 0, _masterSwarmUniformsSize);
      simulationShader.SetBuffer(masterSimKernelData.index, _swarmBufferNameId, _swarmBuffer);
      simulationShader.SetBuffer(masterSimKernelData.index, _swarmParticleBufferNameId, _swarmParticleBuffer);

      simulationShader.Dispatch(masterSimKernelData.index, 1, 1, 1);
      #endregion
    }

    public void Render(in Material material, in Bounds bounds)
    {
      // Update Arguments and Uniforms
      subMeshIndex = Mathf.Clamp(subMeshIndex, 0, particleMesh.subMeshCount - 1);
      _renderingArgs[0] = particleMesh.GetIndexCount(subMeshIndex);
      _renderingArgs[1] = (uint) GetMaxSwarmParticleCount();
      _renderingArgs[2] = particleMesh.GetIndexStart(subMeshIndex);
      _renderingArgs[3] = particleMesh.GetBaseVertex(subMeshIndex);
      _renderingArgumentBuffer.SetData(_renderingArgs);

      _materialProperties.SetBuffer(_swarmParticleBufferNameId, _swarmParticleBuffer);
      _materialProperties.SetColor("particleTint", particleTint);

      Graphics.DrawMeshInstancedIndirect(particleMesh, subMeshIndex, material, bounds, _renderingArgumentBuffer, 0, _materialProperties);
    }

    private void OnDisable() { swarmSimManager.RemoveDisabledSwarms(); }
    private void OnEnable() { swarmSimManager.RegisterSwarm(this); }
    private void OnDestroy() { swarmSimManager.RemoveDisabledSwarms(); }
    private int GetMaxSwarmCount() => maxSlaveSwarmCount + 1;
    private int GetCurrentSwarmCount() => GetCurrentSlaveSwarmCount() + 1;
    private int GetCurrentSlaveSwarmCount() => Mathf.Min(maxSlaveSwarmCount, activeSlaveSwarmCount);
    private int GetMaxSwarmParticleCount() => GetMaxSlaveSwarmParticleCount() + _masterSwarmSize;
    private int GetMaxSlaveSwarmParticleCount() => maxSlaveSwarmCount * _slaveSwarmSize;
    

  }
}
