using System.Collections.Generic;
using System.Runtime.InteropServices;
using AnSim.Runtime.Utils;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnSim.Runtime
{
  public class Swarm : MonoBehaviour
  {
    [Header("Dependencies")]
    public SwarmSimManager swarmSimManager;
    public FoodSource foodSource;

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
    [Range(0.001f, 1.0f)]
    public float globalBestDisturbanceConstant = 0.005f; //sigma_g
    [Range(0.001f, 1.0f)]
    public float mutationStrategyParameter = 0.22f; //sigma
    [Range(0.1f, 9.9f)]
    public float individualAccelerationConstant = 2.05f; //c1
    [Range(0.1f, 9.9f)]
    public float socialAccelerationConstant = 2.05f; //c2
    [Range(0.1f, 9.9f)]
    public float masterAccelerationConstant = 2.02f; //c3
    [Range(0.001f, 1.0f)]
    public float inertiaWeightModifier = 0.9f; //Particle momentum, weighs contribution of previous velocity
    [Range(0.1f, 2.0f)]
    public float inertiaWeightMax = 1.5f;
    [Range(0.1f, 2.0f)]
    public float inertiaWeightMin = .1f;
    [Range(0.1f, 2.0f)]
    public float maxVelocity = .1f; //Per component

    [Header("Swarm AI Settings")]
    public float asyncGpuRequestInterval = 2.0f; //Per component
    public uint maxGpuRequestQueueLength = 2; //Per component

    [Header("Swarm Collision Settings")]
    [Range(0f, 200f)]
    public float worldDodgeBias = 10f;

    [Header("Render Settings")]
    public Mesh particleMesh;
    public int subMeshIndex = 0;
    public Color particleTint = Color.white;

    public bool IsResetRequired
    {
      get => _resetRequired;
      set => _resetRequired = value;
    }

    // General
    private int _swarmSize;
    public bool IsActive { get; private set; } = true;

    // Target Update
    private Vector3 _cachedTarget;
    private bool _resetRequired;

    // Compute Buffers
    private PingPongIndex _pingPongIndex;
    private int[] _swarmIndexBufferIds;
    private ComputeBuffer[] _swarmIndexBuffers;
    private uint[] _swarmIndexData;

    private int _swarmCounterBufferNameId;
    private ComputeBuffer _swarmCounterBuffer;

    private int _indirectDispatchWriteBufferNameId;
    private uint[] _indirectDispatchArgs;
    private ComputeBuffer _indirectDispatchArgBuffer;

    private int _swarmBufferNameId;
    private ComputeBuffer _swarmBuffer;

    private int _swarmParticleBufferNameId;
    private ComputeBuffer _swarmParticleBuffer;

    //private int _swarmIndexMaskBufferId;
    private ComputeBuffer _swarmIndexMaskBuffer;
    //private uint[] _swarmIndexMaskData;

    private int _swarmResetUniformBufferNameId;
    private ComputeBuffer _swarmResetUniformBuffer;
    private int _swarmResetUniformsSize;

    private int _slaveSwarmUniformBufferNameId;
    private ComputeBuffer _slaveSwarmUniformBuffer;
    private SlaveSwarmUniforms[] _slaveSwarmUniforms;
    private int _slaveSwarmUniformsSize;

    private int _masterSwarmUniformBufferNameId;
    private ComputeBuffer _masterSwarmUniformBuffer;
    private MasterSwarmUniforms[] _masterSwarmUniforms;
    private int _masterSwarmUniformsSize;

    // Swarm Logic Stuff
    private Queue<AsyncGPUReadbackRequest> _swarmDataRequests = new Queue<AsyncGPUReadbackRequest>();
    private float _asyncGpuRequestTimer = 0;
    private int _numberOfSwarmsToRevive = 0;

    // Rendering
    private uint[] _renderingArgs = { 0, 0, 0, 0, 0 };
    private ComputeBuffer _renderingArgBuffer;
    private MaterialPropertyBlock _materialProperties;

    private void Awake()
    {
      _cachedTarget = foodSource.transform.position;
      _swarmSize = swarmSimManager.GetSwarmSize();
      _pingPongIndex = new PingPongIndex();

      #region Init GPU Buffers
      //Init Swarm Index Buffers
      _swarmIndexBufferIds = new[]
      {
        Shader.PropertyToID("SwarmIndexBuffer"),
        Shader.PropertyToID("RWSwarmIndexBuffer")
      };
      _swarmIndexBuffers = new[]
      {
        new ComputeBuffer(GetMaxSwarmCount(), sizeof(uint),
          ComputeBufferType.Structured),
        new ComputeBuffer(GetMaxSwarmCount(), sizeof(uint),
          ComputeBufferType.Structured)
      };
      _swarmIndexData = new uint[GetMaxSwarmCount()];
      for (uint i = 0; i < GetMaxSwarmCount(); i++)
      {
        _swarmIndexData[i] = i;
      }
      _swarmIndexBuffers[0].SetData(_swarmIndexData);
      _swarmIndexBuffers[1].SetData(_swarmIndexData);

      _swarmCounterBufferNameId = Shader.PropertyToID("SwarmCounterBuffer");
      _swarmCounterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Counter);
      _swarmCounterBuffer.SetCounterValue(0u);

      _indirectDispatchWriteBufferNameId = Shader.PropertyToID("RWIndirectDispatchBuffer");
      _indirectDispatchArgs = new[] { (uint)GetCurrentSwarmCount(), 1u, 1u };
      _indirectDispatchArgBuffer = new ComputeBuffer(1, _indirectDispatchArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
      _indirectDispatchArgBuffer.SetData(_indirectDispatchArgs);

      //Init Index Mask Buffer
      //_swarmIndexMaskBufferId = Shader.PropertyToID("SwarmIndexMaskBuffer");
      _swarmIndexMaskBuffer = new ComputeBuffer(GetMaxSwarmCount(),
        sizeof(uint),
        ComputeBufferType.Structured);
      //_swarmIndexMaskData = new uint[GetMaxSwarmCount()];

      //Init Swarm Reset Uniform Buffer
      _swarmResetUniformsSize = Marshal.SizeOf(typeof(SwarmResetUniforms));
      _swarmResetUniformBufferNameId = Shader.PropertyToID("SwarmResetUniforms");
      _swarmResetUniformBuffer = new ComputeBuffer(1, _swarmResetUniformsSize, ComputeBufferType.Constant);

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
      _slaveSwarmUniforms[0].inertiaWeight = inertiaWeightMax;

      //Init MasterSwarmUniformBuffer
      _masterSwarmUniformsSize = Marshal.SizeOf(typeof(MasterSwarmUniforms));
      _masterSwarmUniformBufferNameId =
        Shader.PropertyToID("MasterSwarmUniforms");
      _masterSwarmUniformBuffer = new ComputeBuffer(1, _masterSwarmUniformsSize, ComputeBufferType.Constant);
      _masterSwarmUniforms = new MasterSwarmUniforms[1];
      _masterSwarmUniforms[0].inertiaWeight = inertiaWeightMax;
      #endregion

      #region Init Rendering
      _renderingArgBuffer = new ComputeBuffer(1, _renderingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

      subMeshIndex = Mathf.Clamp(subMeshIndex, 0, particleMesh.subMeshCount - 1);
      _renderingArgs[0] = particleMesh.GetIndexCount(subMeshIndex);
      _renderingArgs[1] = (uint)GetMaxSwarmParticleCount();
      _renderingArgs[2] = particleMesh.GetIndexStart(subMeshIndex);
      _renderingArgs[3] = particleMesh.GetBaseVertex(subMeshIndex);
      _renderingArgBuffer.SetData(_renderingArgs);

      _materialProperties = new MaterialPropertyBlock();
      _materialProperties.SetBuffer(_swarmParticleBufferNameId, _swarmParticleBuffer);
      _materialProperties.SetColor("particleTint", particleTint);
      #endregion
    }

    public void SetupSimulation(ComputeShader simulationShader,
    in CsKernelData csKernelData)
    {
      /*
       *
       */
      //Set Uniforms for Setup
      var swarmSetupUniforms = new SwarmResetUniforms()
      {
        resetPosition = transform.position,
        target = foodSource.transform.position,
        positionVariance = setupPositionVariance,
        velocityVariance = setupVelocityVariance,
        enablePositionReset = 1,
        enableVelocityReset = 1,
        reviveParticles = 1
      };
      //Set Mask Buffer covering all swarms
      var swarmIndexMaskData = new uint[GetMaxSwarmCount()];
      for (uint i = 0; i < GetMaxSwarmCount(); i++)
      {
        swarmIndexMaskData[i] = i;
      }
      ResetSwarmWithMask(simulationShader, csKernelData, GetMaxSwarmCount(), swarmIndexMaskData, swarmSetupUniforms);
    }

    public void RunSimulation(ComputeShader simulationShader, in CsKernelData maskedResetKernelData, in CsKernelData slaveSimKernelData, in CsKernelData masterSimKernelData)
    {
      ProcessSwarmLogicAsync();

      #region Target Update with Swarm Reset if target changed
      //Check if Swarm needs to be reset in some way
      if (!foodSource.transform.position.Equals(_cachedTarget))
      {
        //Set Uniforms for Setup
        var swarmSetupUniforms = new SwarmResetUniforms()
        {
          resetPosition = Vector3.zero,
          target = foodSource.transform.position,
          positionVariance = 0,
          velocityVariance = 0,
          enablePositionReset = 0,
          enableVelocityReset = 0,
          reviveParticles = 0,
          resetOnlyIfRevived = 0
        };
        //Set Mask Buffer covering all swarms
        var swarmIndexMaskData = new uint[GetMaxSwarmCount()];
        for (uint i = 0; i < GetMaxSwarmCount(); i++)
        {
          swarmIndexMaskData[i] = i;
        }
        ResetSwarmWithMask(simulationShader, maskedResetKernelData, GetMaxSwarmCount(), swarmIndexMaskData, swarmSetupUniforms);

        // Reset InertiaWeight
        // TODO: Implement better control
        //_slaveSwarmUniforms[0].inertiaWeight = inertiaWeightMax;
        //_masterSwarmUniforms[0].inertiaWeight = inertiaWeightMax;

        _cachedTarget = foodSource.transform.position;
      }
      #endregion

      #region Run SlaveSwarm Simulation
      //Update Uniform Data
      _slaveSwarmUniforms[0].alpha = boundaryDisturbanceConstant;
      _slaveSwarmUniforms[0].c1 = individualAccelerationConstant;
      _slaveSwarmUniforms[0].c2 = socialAccelerationConstant;
      //Updated after simulation
      //_slaveSwarmUniforms[0].inertiaWeight *= inertiaWeightModifier;
      _slaveSwarmUniforms[0].n = replicatesPerParticle;
      _slaveSwarmUniforms[0].target = foodSource.transform.position;
      _slaveSwarmUniforms[0].worldDodgeBias = worldDodgeBias;
      _slaveSwarmUniforms[0].sigma = mutationStrategyParameter;
      _slaveSwarmUniforms[0].sigma_g = globalBestDisturbanceConstant;
      //Update Uniform Buffer
      _slaveSwarmUniformBuffer.SetData(_slaveSwarmUniforms);

      //Set all Buffers
      Shader.SetGlobalConstantBuffer(_slaveSwarmUniformBufferNameId, _slaveSwarmUniformBuffer, 0, _slaveSwarmUniformsSize);
      simulationShader.SetBuffer(slaveSimKernelData.index, _swarmIndexBufferIds[0], _swarmIndexBuffers[_pingPongIndex.Ping]);
      simulationShader.SetBuffer(slaveSimKernelData.index, _swarmIndexBufferIds[1], _swarmIndexBuffers[_pingPongIndex.Pong]);
      simulationShader.SetBuffer(slaveSimKernelData.index, _swarmCounterBufferNameId, _swarmCounterBuffer);
      simulationShader.SetBuffer(slaveSimKernelData.index, _swarmBufferNameId, _swarmBuffer);
      simulationShader.SetBuffer(slaveSimKernelData.index, _swarmParticleBufferNameId, _swarmParticleBuffer);

      simulationShader.DispatchIndirect(slaveSimKernelData.index, _indirectDispatchArgBuffer);
      #endregion

      //DEBUG
      //_numberOfSwarmsToRevive = 1;

      #region Run MasterSwarm Simulation
      //Update Uniform Data
      _masterSwarmUniforms[0].alpha = boundaryDisturbanceConstant;
      _masterSwarmUniforms[0].c1 = individualAccelerationConstant;
      _masterSwarmUniforms[0].c2 = socialAccelerationConstant;
      _masterSwarmUniforms[0].c3 = masterAccelerationConstant;
      //Updated after simulation
      //_masterSwarmUniforms[0].inertiaWeight *= inertiaWeightModifier;
      _masterSwarmUniforms[0].n = replicatesPerParticle;
      _masterSwarmUniforms[0].target = foodSource.transform.position;
      _masterSwarmUniforms[0].worldDodgeBias = worldDodgeBias;

      _masterSwarmUniforms[0].swarmBufferMasterIndex = (uint)maxSlaveSwarmCount;
      _masterSwarmUniforms[0].swarmParticleBufferMasterOffset = (uint)GetMaxSlaveSwarmParticleCount();

      _masterSwarmUniforms[0].sigma = mutationStrategyParameter;
      _masterSwarmUniforms[0].sigma_g = globalBestDisturbanceConstant;

      //Update Uniform Buffer
      _masterSwarmUniformBuffer.SetData(_masterSwarmUniforms);

      //Set all Buffers
      Shader.SetGlobalConstantBuffer(_masterSwarmUniformBufferNameId, _masterSwarmUniformBuffer, 0, _masterSwarmUniformsSize);
      simulationShader.SetBuffer(masterSimKernelData.index, _swarmIndexBufferIds[0], _swarmIndexBuffers[_pingPongIndex.Ping]);
      simulationShader.SetBuffer(masterSimKernelData.index, _swarmIndexBufferIds[1], _swarmIndexBuffers[_pingPongIndex.Pong]);
      simulationShader.SetBuffer(masterSimKernelData.index, _swarmCounterBufferNameId, _swarmCounterBuffer);
      simulationShader.SetBuffer(masterSimKernelData.index, _swarmBufferNameId, _swarmBuffer);
      simulationShader.SetBuffer(masterSimKernelData.index, _swarmParticleBufferNameId, _swarmParticleBuffer);

      simulationShader.SetInt("numberOfSwarmsToRevive", _numberOfSwarmsToRevive);

      simulationShader.Dispatch(masterSimKernelData.index, 1, 1, 1);
      #endregion

      //Update DispatchArgs with SwarmCounterBuffer Data
      ComputeBuffer.CopyCount(_swarmCounterBuffer, _indirectDispatchArgBuffer, 0);
      //Reset Counter Value
      _swarmCounterBuffer.SetCounterValue(0u);

      #region AI Masked Update

      //TODO: Maybe add more conditions to update swarm here
      if (_numberOfSwarmsToRevive > 0)
      {
        //Set Uniforms for Setup
        var swarmSetupUniforms = new SwarmResetUniforms()
        {
          resetPosition = Vector3.zero,
          target = foodSource.transform.position,
          positionVariance = 0,
          velocityVariance = 1,
          enablePositionReset = 1,
          enableVelocityReset = 1,
          reviveParticles = 1,
          resetOnlyIfRevived = 1
        };
        ResetActiveSwarm(simulationShader, maskedResetKernelData, swarmSetupUniforms);

      }

      #endregion

      // Update InertiaWeight only after simulations has run
      _slaveSwarmUniforms[0].inertiaWeight = Mathf.Max(inertiaWeightMin, _slaveSwarmUniforms[0].inertiaWeight * inertiaWeightModifier); ;
      _masterSwarmUniforms[0].inertiaWeight = Mathf.Max(inertiaWeightMin, _slaveSwarmUniforms[0].inertiaWeight * inertiaWeightModifier); ;

      //Reset AI and other values
      _numberOfSwarmsToRevive = 0;
    }

    public void Render(in Material material, in Bounds bounds)
    {
      // Update Arguments and Uniforms
      subMeshIndex = Mathf.Clamp(subMeshIndex, 0, particleMesh.subMeshCount - 1);
      _renderingArgs[0] = particleMesh.GetIndexCount(subMeshIndex);
      _renderingArgs[1] = (uint)GetCurrentSwarmParticleCount();
      _renderingArgs[2] = particleMesh.GetIndexStart(subMeshIndex);
      _renderingArgs[3] = particleMesh.GetBaseVertex(subMeshIndex);
      _renderingArgBuffer.SetData(_renderingArgs);

      _materialProperties.SetBuffer(_swarmParticleBufferNameId, _swarmParticleBuffer);

      _materialProperties.SetBuffer(_swarmIndexBufferIds[0], _swarmIndexBuffers[_pingPongIndex.Ping]);
      _materialProperties.SetColor("particleTint", particleTint);
      _materialProperties.SetInt("swarmSize", _swarmSize);

      Graphics.DrawMeshInstancedIndirect(particleMesh, subMeshIndex, material, bounds, _renderingArgBuffer, 0, _materialProperties);


      //Swap PingPong Indices
      _pingPongIndex.Advance();
    }


    private void ResetSwarmWithMask(ComputeShader simulationShader,
      in CsKernelData csKernelData, int swarmResetCount, uint[] swarmIndexMaskData, SwarmResetUniforms swarmResetUniforms)
    {
      //Add overload with gpu index buffer instead of raw data
      if (swarmIndexMaskData.Length != GetMaxSwarmCount())
      {
        Debug.Log("Swarm Reset called with Mask of incorrect length.");
      }

      //Update and Set Uniform Buffer
      SwarmResetUniforms[] uniformsAsArray = { swarmResetUniforms };
      _swarmResetUniformBuffer.SetData(uniformsAsArray);
      Shader.SetGlobalConstantBuffer(_swarmResetUniformBufferNameId, _swarmResetUniformBuffer, 0, _swarmResetUniformsSize);

      //Update and Set IndexMaskBuffer
      _swarmIndexMaskBuffer.SetData(swarmIndexMaskData);
      simulationShader.SetBuffer(csKernelData.index, _swarmIndexBufferIds[0], _swarmIndexMaskBuffer);

      //Set Swarm Buffers
      simulationShader.SetBuffer(csKernelData.index, _swarmBufferNameId, _swarmBuffer);
      simulationShader.SetBuffer(csKernelData.index, _swarmParticleBufferNameId, _swarmParticleBuffer);


      simulationShader.Dispatch(csKernelData.index, swarmResetCount, 1, 1);
    }

    private void ResetActiveSwarm(ComputeShader simulationShader,
      in CsKernelData csKernelData, SwarmResetUniforms swarmResetUniforms)
    {
      //Update and Set Uniform Buffer
      SwarmResetUniforms[] uniformsAsArray = { swarmResetUniforms };
      _swarmResetUniformBuffer.SetData(uniformsAsArray);
      Shader.SetGlobalConstantBuffer(_swarmResetUniformBufferNameId, _swarmResetUniformBuffer, 0, _swarmResetUniformsSize);

      //Update and Set IndexMaskBuffer
      simulationShader.SetBuffer(csKernelData.index, _swarmIndexBufferIds[0], _swarmIndexBuffers[_pingPongIndex.Pong]); //Important: Uses Pong Buffer, which was updated during this frame!

      //Set Swarm Buffers
      simulationShader.SetBuffer(csKernelData.index, _swarmBufferNameId, _swarmBuffer);
      simulationShader.SetBuffer(csKernelData.index, _swarmParticleBufferNameId, _swarmParticleBuffer);


      simulationShader.DispatchIndirect(csKernelData.index, _indirectDispatchArgBuffer);
    }

    private void ProcessSwarmLogicAsync()
    {
      while (_swarmDataRequests.Count > 0)
      {
        var request = _swarmDataRequests.Peek(); //Peek at head of queue

        if (request.hasError)
        {
          Debug.Log("Error in AsyncGPUReadbackRequest");
          _swarmDataRequests.Dequeue();
        }
        else if (request.done)
        {
          NativeArray<SwarmData> swarmDataArray = request.GetData<SwarmData>();
          /* ---------------------------------------------------------------
           * Implement Swarm Logic here:
           * ---------------------------------------------------------------
           */
          uint swarmsAlive = 0;
          for (int i = 0; i < swarmDataArray.Length; i++)
          {
            var swarm = swarmDataArray[i];
            if ((swarm.particlesAlive == 0)) continue;

            swarmsAlive++;

            if (swarm.fitness < 5.5f)
            {
              var foodAmount = foodSource.EatFood();
              _numberOfSwarmsToRevive += foodAmount;

              if (foodAmount > 0) Debug.Log("Swarm ate " + foodAmount + " food.");

            }

          }

          //Debug.Log(swarmsAlive + " swarms alive.");

          /*
           *  ---------------------------------------------------------------
           */
          _swarmDataRequests.Dequeue();
        }
        else
        {
          break;
        }
      }

      _asyncGpuRequestTimer += Time.deltaTime;
      if (_asyncGpuRequestTimer > asyncGpuRequestInterval && _swarmDataRequests.Count < maxGpuRequestQueueLength - 1)
      {
        _asyncGpuRequestTimer = 0;
        _swarmDataRequests.Enqueue(AsyncGPUReadback.Request(_swarmBuffer));
      }

    }

    private void OnDisable()
    {
      IsActive = false;
      swarmSimManager.RemoveDisabledSwarms();
    }

    private void OnEnable()
    {
      IsActive = true;
      swarmSimManager.RegisterSwarm(this);
    }

    private void OnDestroy()
    {
      IsActive = false;
      swarmSimManager.RemoveDisabledSwarms();
    }
    private int GetMaxSwarmCount() => maxSlaveSwarmCount + 1;
    private int GetMaxSlaveSwarmCount() => maxSlaveSwarmCount;
    private int GetMasterSwarmCount() => 1;

    private int GetCurrentSwarmCount() => GetCurrentSlaveSwarmCount() + 1;
    private int GetCurrentSlaveSwarmCount() => Mathf.Min(maxSlaveSwarmCount, activeSlaveSwarmCount);

    private int GetMaxSwarmParticleCount() => GetMaxSlaveSwarmParticleCount() + _swarmSize;
    private int GetMaxSlaveSwarmParticleCount() => maxSlaveSwarmCount * _swarmSize;
    private int GetCurrentSwarmParticleCount() => GetCurrentSwarmCount() * _swarmSize;

    private void OnDrawGizmos()
    {
      Gizmos.color = particleTint;
      Gizmos.DrawWireSphere(foodSource.transform.position, 1f);
    }

    private void OnValidate()
    {
      activeSlaveSwarmCount =
        Mathf.Min(maxSlaveSwarmCount, activeSlaveSwarmCount);
    }
  }
}
