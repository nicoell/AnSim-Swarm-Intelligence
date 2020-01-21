using System;
using System.Collections.Generic;
using AnSim.Runtime.Utils;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace AnSim.Runtime
{
  public class SimulationResources : ScriptableObject
  {
    public ShaderResources shaders;
    public MaterialResources materials;

#if UNITY_EDITOR
    public void Init()
    {
      var anSimRuntimePath = AnSimUtils.GetAnSimPath() + "Scripts/Runtime/";
      var anSimShaderPath = anSimRuntimePath + "Shader/";

      shaders = new ShaderResources()
      {
        swarmSimulationComputeShader =
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "SwarmSimulation.compute"),
        swarmRenderingShader =
          AssetDatabase.LoadAssetAtPath<Shader>(
            anSimShaderPath + "SwarmRendering.shader"),
      };

      shaders.swarmSimulationMaskedResetKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "MaskedReset");

      shaders.swarmSimulationSlaveUpdateKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "SlaveUpdate");

      shaders.swarmSimulationMasterUpdateKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "MasterUpdate");

      materials = new MaterialResources()
      {
        swarmRenderMaterial = new Material(shaders.swarmRenderingShader)
      };
    }
#endif

    [Serializable]
    public sealed class ShaderResources
    {
      public ComputeShader swarmSimulationComputeShader;
      public CsKernelData swarmSimulationMaskedResetKernelData;
      public CsKernelData swarmSimulationSlaveUpdateKernelData;
      public CsKernelData swarmSimulationMasterUpdateKernelData;
      public Shader swarmRenderingShader;
    }

    [Serializable]
    public sealed class MaterialResources
    {
      public Material swarmRenderMaterial;
    }
  }
}