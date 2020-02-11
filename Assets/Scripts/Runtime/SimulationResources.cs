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
        prefixSumCompute =  
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "PrefixSumCompute.compute"),
        clearBufferUintComputeShader =  
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "ClearBufferUint.compute"),
        uniformGridConstructionComputeShader =
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "UniformGridConstruction.compute"),
        radixSortComputeShader =
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "RadixSort.compute"),
        clearBufferParticleHashComputeShader =
          AssetDatabase.LoadAssetAtPath<ComputeShader>(
            anSimShaderPath + "ClearBufferParticleHash.compute"),
      };

      shaders.swarmSimulationMaskedResetKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "MaskedReset");
      shaders.swarmSimulationSlaveUpdateKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "SlaveUpdate");
      shaders.swarmSimulationMasterUpdateKernelData = new CsKernelData(shaders.swarmSimulationComputeShader, "MasterUpdate");
      

      shaders.distanceFieldConstructionKernelData = new CsKernelData(shaders.distanceFieldConstructionComputeShader, "CSMain");

      shaders.prefixSumScanInBucketInclusive = new CsKernelData(shaders.prefixSumCompute, "ScanInBucketInclusive");
      shaders.prefixSumScanInBucketExclusive = new CsKernelData(shaders.prefixSumCompute, "ScanInBucketExclusive");
      shaders.prefixSumScanBucketResult = new CsKernelData(shaders.prefixSumCompute, "ScanBucketResult");
      shaders.prefixSumScanAddBucketResult = new CsKernelData(shaders.prefixSumCompute, "ScanAddBucketResult");

      shaders.clearBufferUintKernelData = new CsKernelData(shaders.clearBufferUintComputeShader, "Clear");
      shaders.clearBufferUintUniformGridKernelData = new CsKernelData(shaders.clearBufferUintComputeShader, "ClearForGrid");
      shaders.clearBufferParticleHashKernelData = new CsKernelData(shaders.clearBufferParticleHashComputeShader, "Clear");

      shaders.uniformGridFindCellStartKernelData = new CsKernelData(shaders.uniformGridConstructionComputeShader, "FindCellStart");

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
      public ComputeShader prefixSumCompute;
      public ComputeShader clearBufferUintComputeShader;
      public ComputeShader clearBufferParticleHashComputeShader;

      public CsKernelData prefixSumScanInBucketInclusive;
      public CsKernelData prefixSumScanInBucketExclusive;
      public CsKernelData prefixSumScanBucketResult;
      public CsKernelData prefixSumScanAddBucketResult;
      public CsKernelData distanceFieldConstructionKernelData;
      public CsKernelData clearBufferUintKernelData;
      public CsKernelData clearBufferUintUniformGridKernelData;
      public CsKernelData clearBufferParticleHashKernelData;

      public ComputeShader uniformGridConstructionComputeShader;
      public CsKernelData uniformGridFindCellStartKernelData;

      public ComputeShader radixSortComputeShader;
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