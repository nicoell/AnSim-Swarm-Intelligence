using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ansim.Runtime;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AnSim.Runtime
{
  public class DistanceFieldVolume
  {
    private int _volumeResolution;
    public int VolumeResolution => _volumeResolution;
    private int _prefixSumThreadsPerGroup;
    private int _threadGroupCount;
    private readonly SimulationResources _simulationResources;

    private const int ViewDirectionCount = 3;

    private readonly int _pixelFragmentCounterBufferNameId = Shader.PropertyToID("FragmentCounterBuffer");
    private readonly int _distanceFieldObjectDataBufferNameId = Shader.PropertyToID("DistanceFieldObjectDataBuffer");
    private readonly int _prefixSumInputBufferNameId = Shader.PropertyToID("_Input");
    private readonly int _prefixSumResultBufferNameId = Shader.PropertyToID("_Result");
    private readonly int _prefixSumBufferNameId = Shader.PropertyToID("PrefixSumBuffer");
    private readonly int _dynamicDepthBufferNameId = Shader.PropertyToID("DynamicDepthBuffer");
    private readonly int _uintBufferToClearNameId = Shader.PropertyToID("BufferToClear");
    private readonly int _distanceFieldTextureNameId = Shader.PropertyToID("DistanceFieldTexture");

    private Bounds _volumeBounds;
    private Matrix4x4 _orthoProjectionMatrix;
    public Matrix4x4 OrthoProjectionMatrix => _orthoProjectionMatrix;
    private Matrix4x4[] _viewProjMatrices = new Matrix4x4[3];
    private Matrix4x4[] _gridModelViewProjMatrices = new Matrix4x4[3];

    private int _pixelCount;
    private int _totalPixelCount; // for all viewing directions
    private int _fragmentCounterValue;

    private CommandBuffer _cmdBuffer1;
    private CommandBuffer _cmdBuffer2;
    private RenderTexture _dummyRenderTarget;

    private ComputeBuffer _distanceFieldObjectDataBuffer;
    private ComputeBuffer _pixelFragmentCounterBuffer;
    private ComputeBuffer _prefixSumAuxBuffer;
    private ComputeBuffer _prefixSumResultBuffer;
    private ComputeBuffer _dynamicDepthBuffer;
    public RenderTexture DistanceField3DTexture { get; private set; }

    private List<DistanceFieldObjectData> _distanceFieldObjects;


    public DistanceFieldVolume(in SimulationResources simulationResources, Bounds volumeBounds)
    {
      _simulationResources = simulationResources;
      // Volume Resolution is limited by by PrefixSum ComputeShader. The prefixSum number of threads should exactly be volumeResolution * sqrt(3), since this wouldn't work we take the double. 
      _prefixSumThreadsPerGroup = (int)_simulationResources.shaders.prefixSumScanInBucketExclusive.numThreadsX;
      _volumeResolution = _prefixSumThreadsPerGroup / 2;
      _volumeBounds = volumeBounds;

      _cmdBuffer1 = new CommandBuffer { name = "FragCountAndPrefixSum" };
      _cmdBuffer2 = new CommandBuffer { name = "DynamicDepthAndDistanceFieldConstruction" };
    }


    private void SetupDistanceFieldObjects()
    {
      int objectIndex = 0;
      //Find all objects with DistanceFieldInput tag that have a mesh attached to it.
      var objects = GameObject.FindGameObjectsWithTag("DistanceFieldInput");
      foreach (var gameObject in objects)
      {
        if (gameObject.GetComponent<MeshFilter>() == null)
        {
          Debug.Log("Object " + gameObject.name + " has no Mesh attached.");
          continue; // Object has no Mesh
        }

        Debug.Log("Setup object " + gameObject.name + " for usage in Distance Field Volume.");

        var mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;

        // Add DistanceFieldObjectData if not already present
        var objectData = gameObject.GetComponent<DistanceFieldObjectData>();
        if (objectData == null)
        {
          objectData = gameObject.AddComponent<DistanceFieldObjectData>();
        }

        // Each object has it's own propertyBlock holding constant data
        var materialPropertyBlock = new MaterialPropertyBlock();
        // The object index is used to index a buffer of non-constant data
        materialPropertyBlock.SetInt("objectIndex", objectIndex);

        objectData.materialPropertyBlock = materialPropertyBlock;

        objectData.argumentBuffers = new ComputeBuffer[mesh.subMeshCount];
        objectData.sharedMesh = mesh;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
          var args = new uint[]
          {
            mesh.GetIndexCount(i),
            ViewDirectionCount,
            mesh.GetIndexStart(i),
            mesh.GetBaseVertex(i),
            0
          };

          objectData.argumentBuffers[i] = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
          objectData.argumentBuffers[i].SetData(args);
        }


        objectIndex++;
        _distanceFieldObjects.Add(objectData);
      }
      Debug.Log("Setup " + objectIndex + " objects.");
    }

    public void UpdateDistanceFieldObjectsData()
    {
      var distanceFieldObjectData = new DistanceFieldObjectMatrices[_distanceFieldObjects.Count];

      for (var i = 0; i < _distanceFieldObjects.Count; i++)
      {
        var distanceFieldObject = _distanceFieldObjects[i];

        distanceFieldObjectData[i].matrixA = _viewProjMatrices[0] * distanceFieldObject.transform.localToWorldMatrix;
        distanceFieldObjectData[i].matrixB = _viewProjMatrices[1] * distanceFieldObject.transform.localToWorldMatrix;
        distanceFieldObjectData[i].matrixC = _viewProjMatrices[2] * distanceFieldObject.transform.localToWorldMatrix;
      }

      //Debug.Log("Updated " + _distanceFieldObjects.Count + " objects.");
      _distanceFieldObjectDataBuffer.SetData(distanceFieldObjectData);
    }



    public void SetupPipeline()
    {
      /* -----------------------------------------------------------------------
       * Init basics for pipeline and matrices
       * -----------------------------------------------------------------------
       */
      Debug.Log("Setup DistanceFieldVolume Pipeline");
      _pixelCount = _volumeResolution * _volumeResolution;
      _totalPixelCount = _pixelCount * ViewDirectionCount;

      _threadGroupCount = _totalPixelCount / _prefixSumThreadsPerGroup;

      // Init render target with volume resolution
      _dummyRenderTarget = new RenderTexture(_volumeResolution, _volumeResolution, 0, RenderTextureFormat.ARGB32);
      _dummyRenderTarget.Create();

      // Set basic commands
      _cmdBuffer1.Clear();
      _cmdBuffer1.SetRenderTarget(_dummyRenderTarget);
      _cmdBuffer1.ClearRenderTarget(true, true, Color.black, 1.0f);

      _cmdBuffer1.SetGlobalInt("pixelCount", _pixelCount);
      _cmdBuffer1.SetGlobalInt("volumeResolution", _volumeResolution);


      /* -----------------------------------------------------------------------
       * Setup Matrices
       * -----------------------------------------------------------------------
       */
      {
        _orthoProjectionMatrix = Matrix4x4.Ortho(_volumeBounds.min.x, _volumeBounds.max.x, _volumeBounds.min.y, _volumeBounds.max.y, -_volumeBounds.max.z, -_volumeBounds.min.z);

        //Correct projectrion matrix so rendertexture is not upside down
        _orthoProjectionMatrix = GL.GetGPUProjectionMatrix(_orthoProjectionMatrix, true);

        var rotationAxes = new[] { Vector3.right, Vector3.up };
        var rotations = new[] { Quaternion.AngleAxis(90, rotationAxes[0]), Quaternion.AngleAxis(90, rotationAxes[1]) };

        _viewProjMatrices[0] = _orthoProjectionMatrix * Matrix4x4.Rotate(Quaternion.identity);
        _viewProjMatrices[1] = _orthoProjectionMatrix * (Matrix4x4.Translate(_volumeBounds.center) * Matrix4x4.Rotate(rotations[0]) * Matrix4x4.Translate(-_volumeBounds.center));
        _viewProjMatrices[2] = _orthoProjectionMatrix * (Matrix4x4.Translate(_volumeBounds.center) * Matrix4x4.Rotate(rotations[1]) * Matrix4x4.Translate(-_volumeBounds.center));

        var gridModelMatrix = Matrix4x4.Translate(_volumeBounds.min) * Matrix4x4.Scale(_volumeBounds.size / _volumeResolution);
        _gridModelViewProjMatrices[0] = _viewProjMatrices[0] * gridModelMatrix;
        _gridModelViewProjMatrices[1] = _viewProjMatrices[1] * gridModelMatrix;
        _gridModelViewProjMatrices[2] = _viewProjMatrices[2] * gridModelMatrix;
      }


      /* -----------------------------------------------------------------------
       * Setup Distance Field Objects
       * -----------------------------------------------------------------------
       *  - Init List of all objects we want to include in distance field
       */
      _distanceFieldObjects = new List<DistanceFieldObjectData>();
      SetupDistanceFieldObjects();

      if (_distanceFieldObjects.Count == 0)
      {
        Debug.LogError("There needs to be at least one object within the simulation area.");
      }

      _distanceFieldObjectDataBuffer = new ComputeBuffer(_distanceFieldObjects.Count, DistanceFieldObjectMatrices.GetSize(), ComputeBufferType.Structured);
      UpdateDistanceFieldObjectsData();


      /* -----------------------------------------------------------------------
       * Setup Fragment Counting Phase
       * -----------------------------------------------------------------------
       */
      Debug.Log("Setup Fragment Counting Phase");
      _pixelFragmentCounterBuffer = new ComputeBuffer(_totalPixelCount, sizeof(uint), ComputeBufferType.Structured);

      // Clear FragmentCounterBuffer
      var clearBufferUintCs = _simulationResources.shaders.clearBufferUintComputeShader;
      var clearBufferUintCsKernel = _simulationResources.shaders.clearBufferUintKernelData.index;
      _cmdBuffer1.SetComputeBufferParam(clearBufferUintCs, clearBufferUintCsKernel, _uintBufferToClearNameId, _pixelFragmentCounterBuffer);
      _cmdBuffer1.DispatchCompute(clearBufferUintCs, clearBufferUintCsKernel, _threadGroupCount, 1, 1);

      _cmdBuffer1.ClearRandomWriteTargets();
      _cmdBuffer1.SetRandomWriteTarget(2, _pixelFragmentCounterBuffer);
      _cmdBuffer1.SetGlobalBuffer(_distanceFieldObjectDataBufferNameId, _distanceFieldObjectDataBuffer);
      //_cmdBuffer1.ran

      // Draw every object 
      foreach (var distanceFieldObject in _distanceFieldObjects)
      {
        for (int i = 0; i < distanceFieldObject.sharedMesh.subMeshCount; i++)
        {
          _cmdBuffer1.DrawMeshInstancedIndirect(distanceFieldObject.sharedMesh, i, _simulationResources.materials.fragmentCountingMaterial, -1, distanceFieldObject.argumentBuffers[i], 0, distanceFieldObject.materialPropertyBlock);

        }
      }

      _cmdBuffer1.ClearRandomWriteTargets(); // !Important, otherwise changes in _pixelFragmentCounterBuffer are not visible to computebuffer 

      /* -----------------------------------------------------------------------
       * Setup Prefix Sum Phase
       * -----------------------------------------------------------------------
       */
      Debug.Log("Setup PrefixSum Phase");
      _prefixSumAuxBuffer = new ComputeBuffer(_totalPixelCount, sizeof(uint), ComputeBufferType.Structured);
      _prefixSumResultBuffer = new ComputeBuffer(_totalPixelCount, sizeof(uint), ComputeBufferType.Structured);

      var prefixSumCs = _simulationResources.shaders.prefixSumCompute;

      // ScanInBucketInclusive
      var prefixSumScanInBucketInclusiveIndex = _simulationResources.shaders.prefixSumScanInBucketInclusive.index;

      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanInBucketInclusiveIndex, _prefixSumInputBufferNameId, _pixelFragmentCounterBuffer);
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanInBucketInclusiveIndex, _prefixSumResultBufferNameId, _prefixSumResultBuffer);
      _cmdBuffer1.DispatchCompute(prefixSumCs, prefixSumScanInBucketInclusiveIndex, _threadGroupCount, 1, 1);

      // ScanBucketResult
      var prefixSumScanBucketResultIndex = _simulationResources.shaders.prefixSumScanBucketResult.index;
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanBucketResultIndex, _prefixSumInputBufferNameId, _prefixSumResultBuffer);
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanBucketResultIndex, _prefixSumResultBufferNameId, _prefixSumAuxBuffer);
      _cmdBuffer1.DispatchCompute(prefixSumCs, prefixSumScanBucketResultIndex, 1, 1, 1);

      // ScanAddBucketResult
      var prefixSumScanAddBucketResultIndex = _simulationResources.shaders.prefixSumScanAddBucketResult.index;
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanAddBucketResultIndex, _prefixSumInputBufferNameId, _prefixSumAuxBuffer);
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanAddBucketResultIndex, _prefixSumResultBufferNameId, _prefixSumResultBuffer);
      _cmdBuffer1.DispatchCompute(prefixSumCs, prefixSumScanAddBucketResultIndex, _threadGroupCount, 1, 1);

      // Final Prefix sums are are in _prefixSumResultBuffer

      /* -----------------------------------------------------------------------
       * Setup DistanceField 3D RenderTexture
       * -----------------------------------------------------------------------
       */
      if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat))
      {
        Debug.LogError("Requested Texture Format not supported by device.");
      }
      if (!SystemInfo.supports3DTextures)
      {
        Debug.LogError("3DTextures not supported by device.");
      }
      DistanceField3DTexture = new RenderTexture(new RenderTextureDescriptor
      {
        autoGenerateMips = false,
        enableRandomWrite = true,
        useMipMap = false,
        depthBufferBits = 0,
        width = _volumeResolution,
        height = _volumeResolution,
        volumeDepth = _volumeResolution,
        colorFormat = RenderTextureFormat.RFloat,
        graphicsFormat = GraphicsFormat.R32_SFloat,
        msaaSamples = 1,
        dimension = TextureDimension.Tex3D,
        bindMS = false,

      });
      DistanceField3DTexture.Create();

      /* -----------------------------------------------------------------------
       * Setup Dynamic Depth Buffer
       * -----------------------------------------------------------------------
       *  - We need to know the number of fragments, so this has to come after cmdBuffer1 has finished. We do this inside the asyncReadback callback.
       */
      _cmdBuffer1.RequestAsyncReadback(_prefixSumResultBuffer, (AsyncGPUReadbackRequest readback) =>
      {
        //Debug.Log("PrefixSumResult Readback called");
        if (!readback.done || readback.hasError)
        {
          Debug.LogError("There was an error with GPU Readback.");
        }

        if (readback.GetData<uint>().Length == 0)
        {
          Debug.LogError("There needs to be at least one object within the simulation area.");
        }

        var data = readback.GetData<uint>();
        int fragmentCount = (int)data[data.Length - 1];

        if (_fragmentCounterValue != fragmentCount || _dynamicDepthBuffer == null)
        {
          if (_dynamicDepthBuffer != null)
          {
            Debug.Log("FragmentCount has changed from " + _fragmentCounterValue + " to " + fragmentCount);
            Debug.Log("DynamicDepthBuffer and CommandBuffer needs to be reconstructed.");
            _dynamicDepthBuffer.Release();
          }

          // Create Dynamic Depth Buffer, its size dependes on _prefixSumResultBuffer so we need to create it on cpu readback
          _dynamicDepthBuffer = new ComputeBuffer(fragmentCount, sizeof(float), ComputeBufferType.Structured);

          _fragmentCounterValue = fragmentCount;

          /* -----------------------------------------------------------------------
       * Setup CommandBuffer2 to construct DynamicDepthBuffer
       * -----------------------------------------------------------------------
       */
          _cmdBuffer2.Clear();
          _cmdBuffer2.SetGlobalInt("pixelCount", _pixelCount);
          _cmdBuffer2.SetGlobalInt("volumeResolution", _volumeResolution);

          _cmdBuffer2.SetRenderTarget(_dummyRenderTarget);
          _cmdBuffer2.ClearRenderTarget(true, true, Color.black, 1.0f);

          // Clear FragmentCounterBuffer
          _cmdBuffer2.SetComputeBufferParam(clearBufferUintCs, clearBufferUintCsKernel, _uintBufferToClearNameId, _pixelFragmentCounterBuffer);
          _cmdBuffer2.DispatchCompute(clearBufferUintCs, clearBufferUintCsKernel, _threadGroupCount, 1, 1);

          // Render all objects and write their depth in dynamic depth buffer
          _cmdBuffer2.ClearRandomWriteTargets();
          _cmdBuffer2.SetRandomWriteTarget(2, _pixelFragmentCounterBuffer);
          _cmdBuffer2.SetRandomWriteTarget(3, _dynamicDepthBuffer);
          _cmdBuffer2.SetGlobalBuffer(_distanceFieldObjectDataBufferNameId, _distanceFieldObjectDataBuffer);
          _cmdBuffer2.SetGlobalBuffer(_prefixSumBufferNameId, _prefixSumResultBuffer);

          // Draw every object 
          foreach (var distanceFieldObject in _distanceFieldObjects)
          {
            for (int i = 0; i < distanceFieldObject.sharedMesh.subMeshCount; i++)
            {
              _cmdBuffer2.DrawMeshInstancedIndirect(distanceFieldObject.sharedMesh, i, _simulationResources.materials.dynamicDepthBufferConstructionMaterial, -1, distanceFieldObject.argumentBuffers[i], 0, distanceFieldObject.materialPropertyBlock);

            }
          }
          _cmdBuffer2.ClearRandomWriteTargets();

          /* -----------------------------------------------------------------------
           * Setup CommandBuffer2 to construct DistanceField
           * -----------------------------------------------------------------------
           */
          var distanceFieldConstructionCs = _simulationResources.shaders.distanceFieldConstructionComputeShader;
          var distanceFieldConstructionKernel = _simulationResources.shaders.distanceFieldConstructionKernelData.index;

          _cmdBuffer2.SetComputeIntParam(distanceFieldConstructionCs, "pixelCount", _pixelCount);
          _cmdBuffer2.SetComputeIntParam(distanceFieldConstructionCs, "volumeResolution", _volumeResolution);
          _cmdBuffer2.SetComputeMatrixArrayParam(distanceFieldConstructionCs, "gridModelViewProj", _gridModelViewProjMatrices);

          _cmdBuffer2.SetComputeBufferParam(distanceFieldConstructionCs, distanceFieldConstructionKernel, _dynamicDepthBufferNameId, _dynamicDepthBuffer);
          _cmdBuffer2.SetComputeBufferParam(distanceFieldConstructionCs, distanceFieldConstructionKernel, _prefixSumBufferNameId, _prefixSumResultBuffer);

          _cmdBuffer2.SetComputeTextureParam(distanceFieldConstructionCs, distanceFieldConstructionKernel, _distanceFieldTextureNameId, DistanceField3DTexture);

          _cmdBuffer2.DispatchCompute(distanceFieldConstructionCs, distanceFieldConstructionKernel, 1, _volumeResolution, _volumeResolution);
        }

        // Execute DynamicDepthBuffer and DistanceField construction.
        Graphics.ExecuteCommandBuffer(_cmdBuffer2);

        /*
        _cmdBuffer2.RequestAsyncReadback(_distanceField3DTexture, (AsyncGPUReadbackRequest obj) =>
        {
          Debug.Log("DistanceFieldTexture Readback called");
          for (int layer = 0; layer < obj.depth; layer++)
          {
            var distanceFieldLayerData = obj.GetData<float>(layer);
            string debugprint = "";
            for (int x = 0; x < obj.width * obj.height; x++)
            {
              if (x % obj.width == 0) debugprint += "\n";

              if (distanceFieldLayerData[x] >= 0.0f || Mathf.Approximately(distanceFieldLayerData[x], 0.0f))
              {
                debugprint += " " + distanceFieldLayerData[x].ToString("0.00") + "|";
              } else if (Mathf.Approximately(distanceFieldLayerData[x], -1.0f))
              {
                debugprint += "-----" + "|";
              } else
              {
                debugprint += distanceFieldLayerData[x].ToString("0.00") + "|";
              }
            }
            Debug.Log("Layer #"+layer);
            Debug.Log(debugprint);
          }

          Debug.Log("DistanceFieldTexture Readback finished");
        });*/



        //Test if prefixSum Result is correct
        /*var prev = 0u;
        foreach (var u in data)
        {
          if (u < prev)
          {
            Debug.Log("Not working");
          }

          prev = u;
        }*/
        //Debug.Log("PrefixSumResult Readback finished");
      });
    }

    public void ExecutePipeline()
    {
      Graphics.ExecuteCommandBuffer(_cmdBuffer1);
    }

  }
}
