using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using AnSim;
using UnityEngine;

namespace AnSim.Runtime
{
  public class SwarmSimManager : MonoBehaviour
  {
    [Header("Resource Management")]
    public SimulationResources simulationResources;

    [Header("Simulation Settings")]
    [Range(1, 32)]
    public uint maxSwarmCount = 8;

    private Bounds _simulationBounds;

    private List<Swarm> _swarms;

    private int _swarmSimulationUniformBufferNameId;
    private ComputeBuffer _swarmSimulationUniformBuffer;
    private SwarmSimulationUniforms[] _swarmSimulationUniforms;
    private int _swarmSimulationUniformsSize;

    private void Awake()
    {
      _swarms = new List<Swarm>(); //Init Swarm list

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
      #region Init all Swarms
      //Update global Swarm Simulation Uniforms
      _swarmSimulationUniforms[0].deltaTime = Time.deltaTime;
      _swarmSimulationUniforms[0].timeSinceStart = Time.time;
      _swarmSimulationUniforms[0].worldmax = transform.localScale;
      _swarmSimulationUniforms[0].worldmin = transform.position;
      _swarmSimulationUniformBuffer.SetData(_swarmSimulationUniforms);

      _simulationBounds = new Bounds(transform.position + 0.5f * transform.localScale, transform.localScale - transform.position);

      Shader.SetGlobalConstantBuffer(_swarmSimulationUniformBufferNameId, _swarmSimulationUniformBuffer, 0, _swarmSimulationUniformsSize);

      foreach (var swarm in _swarms)
      {
        swarm.SetupSimulation(simulationResources.shaders.swarmSimulationComputeShader, simulationResources.shaders.swarmSimulationSetupKernelData);
      }
      #endregion
    }

    private void Update()
    {
      #region Update all Swarms
      //Update global Swarm Simulation Uniforms
      _swarmSimulationUniforms[0].deltaTime = Time.deltaTime;
      _swarmSimulationUniforms[0].timeSinceStart = Time.time;

      _swarmSimulationUniforms[0].worldmax = transform.localScale;
      _swarmSimulationUniforms[0].worldmin = transform.position;
      _swarmSimulationUniformBuffer.SetData(_swarmSimulationUniforms);

      Shader.SetGlobalConstantBuffer(_swarmSimulationUniformBufferNameId, _swarmSimulationUniformBuffer, 0, _swarmSimulationUniformsSize);

      foreach (var swarm in _swarms)
      {
        swarm.RunSimulation(simulationResources.shaders.swarmSimulationComputeShader, simulationResources.shaders.swarmSimulationSlaveUpdateKernelData, simulationResources.shaders.swarmSimulationMasterUpdateKernelData);
      }

      foreach (var swarm in _swarms)
      {
        swarm.Render(simulationResources.materials.swarmRenderMaterial, _simulationBounds);
      }

      #endregion
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
      if (_swarms != null && _swarms.All(item => item.GetInstanceID() != swarm.GetInstanceID())) _swarms.Add(swarm);
    }

    public void RemoveDisabledSwarms()
    {
      _swarms.RemoveAll(item => item == null || !item.enabled);
    }

    public int GetSlaveSwarmSize()
    {
      if (simulationResources)
      {
        return (int)simulationResources.shaders.swarmSimulationSlaveUpdateKernelData.numThreadsX;
      }

      return 0;
    }

    public int GetMasterSwarmSize()
    {
      if (simulationResources)
      {
        return (int)simulationResources.shaders.swarmSimulationMasterUpdateKernelData.numThreadsX;
      }

      return 0;
    }
  }
}