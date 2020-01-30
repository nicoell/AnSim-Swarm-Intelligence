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

      _simulationBounds = new Bounds(transform.position + 0.5f * transform.localScale, transform.localScale);

      Shader.SetGlobalConstantBuffer(_swarmSimulationUniformBufferNameId, _swarmSimulationUniformBuffer, 0, _swarmSimulationUniformsSize);

      foreach (var swarm in _swarms)
      {
        swarm.SetupSimulation(simulationResources.shaders.swarmSimulationComputeShader, simulationResources.shaders.swarmSimulationMaskedResetKernelData);
      }
      #endregion
    }

    private void Update()
    {
      #region Update all Swarms

      _simulationBounds.center = transform.position + 0.5f * transform.localScale;
      _simulationBounds.size = transform.localScale;

      //Update global Swarm Simulation Uniforms
      _swarmSimulationUniforms[0].deltaTime = Time.deltaTime;
      _swarmSimulationUniforms[0].timeSinceStart = Time.time;
      _swarmSimulationUniforms[0].worldmax = transform.position + transform.localScale;
      _swarmSimulationUniforms[0].worldmin = transform.position;
      _swarmSimulationUniformBuffer.SetData(_swarmSimulationUniforms);

      Shader.SetGlobalConstantBuffer(_swarmSimulationUniformBufferNameId, _swarmSimulationUniformBuffer, 0, _swarmSimulationUniformsSize);

      _swarms.Shuffle(); //Shuffle swarms to reduce disadvantaging any swarm in reaching a target

      foreach (var swarm in _swarms)
      {
        swarm.RunSimulation(simulationResources.shaders.swarmSimulationComputeShader, simulationResources.shaders.swarmSimulationMaskedResetKernelData, simulationResources.shaders.swarmSimulationSlaveUpdateKernelData, simulationResources.shaders.swarmSimulationMasterUpdateKernelData);
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
      if (_swarms != null)
      {
        if (_swarms.All(item => item.GetInstanceID() != swarm.GetInstanceID()))
        {
          _swarms.Add(swarm);
          Debug.Log("Registered Swarm: " + swarm.name);
        } else
        {
          Debug.Log("SwarmList already contains swarm: " + swarm.name);
        }
      } else
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

    /*
     * Returns a valid position inside Simulation Boounds
     * TODO: Prevent position be to be inside environment.
     */
    public Vector3 GetValidFoodPosition()
    {
      var foodPos = new Vector3(
        Random.Range(_simulationBounds.min.x, _simulationBounds.max.x),
        Random.Range(_simulationBounds.min.y, _simulationBounds.max.y),
        Random.Range(_simulationBounds.min.z, _simulationBounds.max.z)
      );
      return foodPos;
    }

    private void OnDrawGizmos()
    {
      Gizmos.DrawWireCube(_simulationBounds.center, _simulationBounds.size);
    }
  }
}