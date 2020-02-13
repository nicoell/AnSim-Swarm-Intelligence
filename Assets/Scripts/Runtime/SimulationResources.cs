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
    public UtilityResources utilities;

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
        bitonicMergeSortComputeShader =  
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "BitonicMergeSort.compute"),
        clearBufferParticleHashComputeShader =
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "ClearBufferParticleHash.compute"),
        uniformGridConstructionComputeShader =
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "UniformGridConstruction.compute"),
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
      shaders.clearBufferUintUniformGridKernelData = new CsKernelData(shaders.clearBufferUintComputeShader, "ClearForGrid");
      shaders.clearBufferParticleHashKernelData = new CsKernelData(shaders.clearBufferParticleHashComputeShader, "Clear");

      shaders.uniformGridFindCellStartKernelData = new CsKernelData(shaders.uniformGridConstructionComputeShader, "FindCellStart");

      shaders.bitonicMergeSortInitKeysKernelData = new CsKernelData(shaders.bitonicMergeSortComputeShader, "InitKeys");
      shaders.bitonicMergeSortBitonicSortKernelData = new CsKernelData(shaders.bitonicMergeSortComputeShader, "BitonicSort");

      materials = new MaterialResources()
      {
        swarmRenderMaterial = new Material(shaders.swarmRenderingShader),
        fragmentCountingMaterial = new Material(shaders.fragmentCountingShader),
        dynamicDepthBufferConstructionMaterial = new Material(shaders.dynamicDepthBufferConstructionShader)
      };

      utilities = new UtilityResources()
      {
        bitomicMergeSort = new BitomicMergeSort(shaders.bitonicMergeSortComputeShader, shaders.bitonicMergeSortInitKeysKernelData, shaders.bitonicMergeSortBitonicSortKernelData)
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
      public ComputeShader bitonicMergeSortComputeShader;
      public ComputeShader clearBufferParticleHashComputeShader;

      public CsKernelData prefixSumScanInBucketInclusiveKernelData;
      public CsKernelData prefixSumScanInBucketExclusiveKernelData;
      public CsKernelData prefixSumScanBucketResultKernelData;
      public CsKernelData prefixSumScanAddBucketResultKernelData;
      public CsKernelData distanceFieldConstructionKernelData;
      public CsKernelData distanceToGradientKernelData;
      public CsKernelData clearBufferUintKernelData;
      public CsKernelData bitonicMergeSortInitKeysKernelData;
      public CsKernelData bitonicMergeSortBitonicSortKernelData;
      public CsKernelData clearBufferUintUniformGridKernelData;
      public CsKernelData clearBufferParticleHashKernelData;

      public ComputeShader uniformGridConstructionComputeShader;
      public CsKernelData uniformGridFindCellStartKernelData;
    }

    [Serializable]
    public sealed class MaterialResources
    {
      public Material swarmRenderMaterial;
      public Material fragmentCountingMaterial;
      public Material dynamicDepthBufferConstructionMaterial;
    }

    [Serializable]
    public sealed class UtilityResources
    {
      public BitomicMergeSort bitomicMergeSort;
    }
  }
}