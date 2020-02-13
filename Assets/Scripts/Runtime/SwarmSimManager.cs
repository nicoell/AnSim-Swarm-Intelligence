using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AnSim;
using AnSim.Runtime.Utils;
using JetBrains.Annotations;
using UnityEngine;

namespace AnSim.Runtime
{
  public class SwarmSimManager : MonoBehaviour
  {
    [Header("Simulation Settings")]
    [Range(0.01f, 2.00f)]
    public float timeScale = 0.1f;
    private float _timer = 0.0f;
    [Range(0.1f, 20.0f)]
    public float particleSightRadius = 10.0f;
    [Range(0.01f, 1.0f)]
    public float separationWeight = 0.6f;

    [Header("Resource Management")]
    public SimulationResources simulationResources;

    [Header("DistanceFieldVolume")]
    public bool updateEveryFrame = false;
    public bool enableDistanceFieldGizmos = false;
    public bool showInstancedDepthTexture = false;
    public bool drawTransformedMeshes = false;
    public bool showDistanceFieldInformation = false;
    public bool drawViewingPlanes = false;
    public bool toggleVectorDistanceGizmo = false;
    [Range(0, 2)]
    public int debugGizmoInstanceId = 0;
    public Vector3Int highlightGridPoint = Vector3Int.zero;

    private DistanceFieldVolume _distanceFieldVolume;
    private readonly int _distanceGradientFieldTextureNameId = Shader.PropertyToID("DistanceGradientFieldTexture");
    

    private UniformGrid _uniformGrid;

    private List<Swarm> _swarms;
    private Bounds _simulationBounds;

    private int _swarmSimulationUniformBufferNameId;
    private ComputeBuffer _swarmSimulationUniformBuffer;
    private SwarmSimulationUniforms[] _swarmSimulationUniforms;
    private int _swarmSimulationUniformsSize;

    List<int> _offsetsSwarms;// Contains the offset of each swarm, so we can acces each particle in a linear buffer in uniform grid

    private void Awake()
    {
      _swarms = new List<Swarm>(); //Init Swarm list

      _simulationBounds = new Bounds(transform.position + 0.5f * transform.localScale, transform.localScale);

      _distanceFieldVolume = new DistanceFieldVolume(simulationResources, _simulationBounds);

      _uniformGrid = new UniformGrid(simulationResources, _simulationBounds);

      _offsetsSwarms = new List<int>(); 

      // Init SwarmSimulationUniform Buffer and data
      _swarmSimulationUniformsSize =
        Marshal.SizeOf(typeof(SwarmSimulationUniforms));
      _swarmSimulationUniformBufferNameId =
        Shader.PropertyToID("SwarmSimulationUniforms");
      _swarmSimulationUniformBuffer = new ComputeBuffer(1, _swarmSimulationUniformsSize, ComputeBufferType.Constant);
      _swarmSimulationUniforms = new SwarmSimulationUniforms[1];
    }

    private void Start()
    {
      int totalNumberParticles = 0;

      _distanceFieldVolume.SetupPipeline();
      // Set necessary resources to simulation compute shader + kernels
      simulationResources.shaders.swarmSimulationComputeShader.SetMatrix("OrthoProjMatrix", _distanceFieldVolume.OrthoProjectionMatrix);
      simulationResources.shaders.swarmSimulationComputeShader.SetInt("VolumeResolution", _distanceFieldVolume.VolumeResolution);
      simulationResources.shaders.swarmSimulationComputeShader.SetTexture(simulationResources.shaders.swarmSimulationMaskedResetKernelData.index, _distanceGradientFieldTextureNameId, _distanceFieldVolume.DistanceGradientField3DTexture);
      simulationResources.shaders.swarmSimulationComputeShader.SetTexture(simulationResources.shaders.swarmSimulationSlaveUpdateKernelData.index, _distanceGradientFieldTextureNameId, _distanceFieldVolume.DistanceGradientField3DTexture);
      simulationResources.shaders.swarmSimulationComputeShader.SetTexture(simulationResources.shaders.swarmSimulationMasterUpdateKernelData.index, _distanceGradientFieldTextureNameId, _distanceFieldVolume.DistanceGradientField3DTexture);

      _distanceFieldVolume.ExecutePipeline();      
      
      #region Init all Swarms
      //Update global Swarm Simulation Uniforms
      _swarmSimulationUniforms[0].timeScale = timeScale;
      _swarmSimulationUniforms[0].timeSinceStart = Time.time;
      _swarmSimulationUniforms[0].worldmax = transform.localScale;
      _swarmSimulationUniforms[0].worldmin = transform.position;
      _swarmSimulationUniformBuffer.SetData(_swarmSimulationUniforms);

      Shader.SetGlobalConstantBuffer(_swarmSimulationUniformBufferNameId, _swarmSimulationUniformBuffer, 0, _swarmSimulationUniformsSize);


      foreach (var swarm in _swarms)
      {
        swarm.SetupSimulation(simulationResources.shaders.swarmSimulationComputeShader, simulationResources.shaders.swarmSimulationMaskedResetKernelData);

        _offsetsSwarms.Add(totalNumberParticles);
        totalNumberParticles += swarm.GetMaxSwarmParticleCount();
      }
      #endregion

      // Init Uniform Grid
      _uniformGrid.Init(totalNumberParticles, new Vector3Int(64, 64, 64));

      //Set global variables in swarmSimulation.compute needed by uniform grid 1st Pass and final usage
      simulationResources.shaders.swarmSimulationComputeShader.SetFloat("SeparationWeight", separationWeight);
      simulationResources.shaders.swarmSimulationComputeShader.SetFloat("ParticleSightRadius", particleSightRadius);
      simulationResources.shaders.swarmSimulationComputeShader.SetVector("GridSize", _uniformGrid.GridSize);// uint3
      simulationResources.shaders.swarmSimulationComputeShader.SetVector("CellSize", _uniformGrid.CellSize);// float3
      simulationResources.shaders.swarmSimulationComputeShader.SetVector("WorldOrigin", _uniformGrid.WorldOrigin);// float3
      simulationResources.shaders.swarmSimulationComputeShader.SetBuffer(simulationResources.shaders.swarmSimulationSlaveUpdateKernelData.index, _uniformGrid.TempParticleHashBufferNameId, _uniformGrid.TempParticleHashBuffer);
      simulationResources.shaders.swarmSimulationComputeShader.SetBuffer(simulationResources.shaders.swarmSimulationMasterUpdateKernelData.index, _uniformGrid.TempParticleHashBufferNameId, _uniformGrid.TempParticleHashBuffer);
      simulationResources.shaders.swarmSimulationComputeShader.SetBuffer(simulationResources.shaders.swarmSimulationSlaveUpdateKernelData.index, _uniformGrid.ParticleHashBufferNameId, _uniformGrid.ParticleHashBuffer);
      simulationResources.shaders.swarmSimulationComputeShader.SetBuffer(simulationResources.shaders.swarmSimulationMasterUpdateKernelData.index, _uniformGrid.ParticleHashBufferNameId, _uniformGrid.ParticleHashBuffer);
      simulationResources.shaders.swarmSimulationComputeShader.SetBuffer(simulationResources.shaders.swarmSimulationSlaveUpdateKernelData.index, _uniformGrid.CellStartBufferNameId, _uniformGrid.CellStartBuffer);
      simulationResources.shaders.swarmSimulationComputeShader.SetBuffer(simulationResources.shaders.swarmSimulationMasterUpdateKernelData.index, _uniformGrid.CellStartBufferNameId, _uniformGrid.CellStartBuffer);
    }

    private void Update()
    {
      if (updateEveryFrame)
      {
        //_distanceFieldVolume.SetupPipeline();
        _distanceFieldVolume.UpdateDistanceFieldObjectsData();
        _distanceFieldVolume.ExecutePipeline();
      }

      #region Update all Swarms

      _simulationBounds.center = transform.position + 0.5f * transform.localScale;
      _simulationBounds.size = transform.localScale;

      //Update global Swarm Simulation Uniforms
      _swarmSimulationUniforms[0].timeScale = timeScale;
      //Manage slow mode. Only update timeSinceStart after 1 time unit has passed to get same results in slow mode.
      //timeSinceStart is used for random number generation
      _timer += 1;
      if (_timer * timeScale >= 1.0f)
      {
        _timer = 0;
        _swarmSimulationUniforms[0].timeSinceStart = Time.time;
      }
      _swarmSimulationUniforms[0].worldmax = transform.position + transform.localScale;
      _swarmSimulationUniforms[0].worldmin = transform.position;
      _swarmSimulationUniformBuffer.SetData(_swarmSimulationUniforms);

      _swarms.Shuffle(); //Shuffle swarms to reduce disadvantaging any swarm in reaching a target

      for (int i = 0; i < _swarms.Count; i++)
      {
        simulationResources.shaders.swarmSimulationComputeShader.SetInt("SwarmOffset", _offsetsSwarms[i]);
        _swarms[i].RunSimulation(simulationResources.shaders.swarmSimulationComputeShader, simulationResources.shaders.swarmSimulationMaskedResetKernelData, simulationResources.shaders.swarmSimulationSlaveUpdateKernelData, simulationResources.shaders.swarmSimulationMasterUpdateKernelData);
      }

      foreach (var swarm in _swarms)
      {
        swarm.Render(simulationResources.materials.swarmRenderMaterial, _simulationBounds);
      }
      #endregion

      // Update UniformGrid Pass 2+3
      _uniformGrid.Update();
    }

#if UNITY_EDITOR
    private void Reset()
    {
      if (!simulationResources)
      {
        simulationResources =
          ScriptableObject.CreateInstance<SimulationResources>();
      }

      simulationResources.Init();
    }
#endif

    public void RegisterSwarm(Swarm swarm)
    {
      if (_swarms != null)
      {
        if (_swarms.All(item => item.GetInstanceID() != swarm.GetInstanceID()))
        {
          _swarms.Add(swarm);
          Debug.Log("Registered Swarm: " + swarm.name);
        }
        else
        {
          Debug.Log("SwarmList already contains swarm: " + swarm.name);
        }
      }
      else
      {
        Debug.Log("Swarm tried to register itself before Swarms List was created.");
        Debug.Log("Please change the Script Execution Order, so SwarmSimManager comes before Swarm.");
      }

    }

    public void RemoveDisabledSwarms()
    {
      _swarms.RemoveAll(item => item == null || !item.IsActive);
    }

    public int GetSwarmSize()
    {
      if (simulationResources)
      {
        return (int)simulationResources.shaders.swarmSimulationSlaveUpdateKernelData.numThreadsX;
      }

      return 0;
    }

    private void OnDrawGizmos()
    {
      Gizmos.DrawWireCube(_simulationBounds.center, _simulationBounds.size);

      if (enableDistanceFieldGizmos && _distanceFieldVolume != null) _distanceFieldVolume.DrawGizmos(debugGizmoInstanceId, highlightGridPoint, showInstancedDepthTexture, drawViewingPlanes, drawTransformedMeshes, showDistanceFieldInformation, toggleVectorDistanceGizmo);
    }
  }
}