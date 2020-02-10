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
        fragmentCountingShader =  
          AssetDatabase.LoadAssetAtPath<Shader>(
            anSimShaderPath + "FragmentCounting.shader"),
        dynamicDepthBufferConstructionShader =  
          AssetDatabase.LoadAssetAtPath<Shader>(
            anSimShaderPath + "DynamicDepthBufferConstruction.shader"),
        distanceFieldConstructionComputeShader =  
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "DistanceFieldConstruction.compute"),
        distanceToGradientFieldComputeShader =  
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "DistanceToGradientField.compute"),
        prefixSumComputeShader =  
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "PrefixSumCompute.compute"),
        clearBufferUintComputeShader =  
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "ClearBufferUint.compute"),
      };

      shaders.swarmSimulationMaskedResetKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "MaskedReset");
      shaders.swarmSimulationSlaveUpdateKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "SlaveUpdate");
      shaders.swarmSimulationMasterUpdateKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "MasterUpdate");
      

      shaders.distanceFieldConstructionKernelData = new CsKernelData(shaders.distanceFieldConstructionComputeShader, "CSMain");
      shaders.distanceToGradientKernelData = new CsKernelData(shaders.distanceToGradientFieldComputeShader, "CSMain");

      shaders.prefixSumScanInBucketInclusiveKernelData = new CsKernelData(shaders.prefixSumComputeShader, "ScanInBucketInclusive");
      shaders.prefixSumScanInBucketExclusiveKernelData = new CsKernelData(shaders.prefixSumComputeShader, "ScanInBucketExclusive");
      shaders.prefixSumScanBucketResultKernelData = new CsKernelData(shaders.prefixSumComputeShader, "ScanBucketResult");
      shaders.prefixSumScanAddBucketResultKernelData = new CsKernelData(shaders.prefixSumComputeShader, "ScanAddBucketResult");

      shaders.clearBufferUintKernelData = new CsKernelData(shaders.clearBufferUintComputeShader, "Clear");

      materials = new MaterialResources()
      {
        swarmRenderMaterial = new Material(shaders.swarmRenderingShader),
        fragmentCountingMaterial = new Material(shaders.fragmentCountingShader),
        dynamicDepthBufferConstructionMaterial = new Material(shaders.dynamicDepthBufferConstructionShader)
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

      public Shader fragmentCountingShader;
      public Shader dynamicDepthBufferConstructionShader;
      public ComputeShader distanceFieldConstructionComputeShader;
      public ComputeShader distanceToGradientFieldComputeShader;
      public ComputeShader prefixSumComputeShader;
      public ComputeShader clearBufferUintComputeShader;

      public CsKernelData prefixSumScanInBucketInclusiveKernelData;
      public CsKernelData prefixSumScanInBucketExclusiveKernelData;
      public CsKernelData prefixSumScanBucketResultKernelData;
      public CsKernelData prefixSumScanAddBucketResultKernelData;
      public CsKernelData distanceFieldConstructionKernelData;
      public CsKernelData distanceToGradientKernelData;
      public CsKernelData clearBufferUintKernelData;
    }

    [Serializable]
    public sealed class MaterialResources
    {
      public Material swarmRenderMaterial;
      public Material fragmentCountingMaterial;
      public Material dynamicDepthBufferConstructionMaterial;
    }
  }
}