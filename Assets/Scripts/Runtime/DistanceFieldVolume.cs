using System.Collections.Generic;
using Ansim.Runtime;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

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
    private readonly int _distanceGradientFieldTextureNameId = Shader.PropertyToID("DistanceGradientFieldTexture");

    private Bounds _volumeBounds;
    private Matrix4x4 _orthoProjectionMatrix;
    public Matrix4x4 OrthoProjectionMatrix => _orthoProjectionMatrix;
    private Matrix4x4[] _viewMatrices = new Matrix4x4[3];
    private Matrix4x4[] _viewProjMatrices = new Matrix4x4[3];
    private Matrix4x4[] _gridModelViewProjMatrices = new Matrix4x4[3];
    private Matrix4x4 _gridModelMatrix;

    private Vector4[,,] _distanceGradientFieldData;

    private int _pixelCount;
    private int _totalPixelCount; // for all viewing directions
    private int _fragmentCounterValue;

    private CommandBuffer _cmdBuffer1;
    private CommandBuffer _cmdBuffer2;
    private bool _shouldRunCmdBuffer1 = true;
    private RenderTexture _dummyRenderTarget;

    private ComputeBuffer _distanceFieldObjectDataBuffer;
    private ComputeBuffer _pixelFragmentCounterBuffer;
    private ComputeBuffer _prefixSumAuxBuffer;
    private ComputeBuffer _prefixSumResultBuffer;
    private ComputeBuffer _dynamicDepthBuffer;
    public RenderTexture DistanceField3DTexture { get; private set; }
    public RenderTexture DistanceGradientField3DTexture { get; private set; }

    private List<DistanceFieldObjectData> _distanceFieldObjects;
    private DistanceFieldObjectMatrices[] _distanceFieldObjectData;


    public DistanceFieldVolume(in SimulationResources simulationResources, Bounds volumeBounds)
    {
      _simulationResources = simulationResources;
      // Volume Resolution is limited by by PrefixSum ComputeShader. The prefixSum number of threads should exactly be volumeResolution * sqrt(3), since this wouldn't work we take the double. 
      _prefixSumThreadsPerGroup = (int)_simulationResources.shaders.prefixSumScanInBucketExclusiveKernelData.numThreadsX;
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
      _distanceFieldObjectData = new DistanceFieldObjectMatrices[_distanceFieldObjects.Count];

      for (var i = 0; i < _distanceFieldObjects.Count; i++)
      {
        var distanceFieldObject = _distanceFieldObjects[i];

        _distanceFieldObjectData[i].matrixA = _viewProjMatrices[0] * distanceFieldObject.transform.localToWorldMatrix;
        _distanceFieldObjectData[i].matrixB = _viewProjMatrices[1] * distanceFieldObject.transform.localToWorldMatrix;
        _distanceFieldObjectData[i].matrixC = _viewProjMatrices[2] * distanceFieldObject.transform.localToWorldMatrix;
      }
      _distanceFieldObjectDataBuffer.SetData(_distanceFieldObjectData);
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

        //Correct projection matrix
        _orthoProjectionMatrix = GL.GetGPUProjectionMatrix(_orthoProjectionMatrix, false);

        var rotationAxes = new[] { Vector3.forward, Vector3.right, Vector3.up };
        var rotations = new[] { Quaternion.AngleAxis(0, rotationAxes[0]), Quaternion.AngleAxis(90, rotationAxes[1]), Quaternion.AngleAxis(90, rotationAxes[2]) };

        _viewMatrices[0] = Matrix4x4.identity;
        _viewMatrices[1] = (Matrix4x4.Translate(_volumeBounds.center) * Matrix4x4.Rotate(rotations[1]) * Matrix4x4.Translate(-_volumeBounds.center));
        _viewMatrices[2] = (Matrix4x4.Translate(_volumeBounds.center) * Matrix4x4.Rotate(rotations[2]) * Matrix4x4.Translate(-_volumeBounds.center));

        _viewProjMatrices[0] = _orthoProjectionMatrix * _viewMatrices[0];
        _viewProjMatrices[1] = _orthoProjectionMatrix * _viewMatrices[1];
        _viewProjMatrices[2] = _orthoProjectionMatrix * _viewMatrices[2];

        _gridModelMatrix = Matrix4x4.Translate(_volumeBounds.min) * Matrix4x4.Scale(_volumeBounds.size / _volumeResolution);
        _gridModelViewProjMatrices[0] = _viewProjMatrices[0] * _gridModelMatrix;
        _gridModelViewProjMatrices[1] = _viewProjMatrices[1] * _gridModelMatrix;
        _gridModelViewProjMatrices[2] = _viewProjMatrices[2] * _gridModelMatrix;
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

      var prefixSumCs = _simulationResources.shaders.prefixSumComputeShader;

      // ScanInBucketInclusive
      var prefixSumScanInBucketInclusiveIndex = _simulationResources.shaders.prefixSumScanInBucketInclusiveKernelData.index;

      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanInBucketInclusiveIndex, _prefixSumInputBufferNameId, _pixelFragmentCounterBuffer);
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanInBucketInclusiveIndex, _prefixSumResultBufferNameId, _prefixSumResultBuffer);
      _cmdBuffer1.DispatchCompute(prefixSumCs, prefixSumScanInBucketInclusiveIndex, _threadGroupCount, 1, 1);

      // ScanBucketResult
      var prefixSumScanBucketResultIndex = _simulationResources.shaders.prefixSumScanBucketResultKernelData.index;
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanBucketResultIndex, _prefixSumInputBufferNameId, _prefixSumResultBuffer);
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanBucketResultIndex, _prefixSumResultBufferNameId, _prefixSumAuxBuffer);
      _cmdBuffer1.DispatchCompute(prefixSumCs, prefixSumScanBucketResultIndex, 1, 1, 1);

      // ScanAddBucketResult
      var prefixSumScanAddBucketResultIndex = _simulationResources.shaders.prefixSumScanAddBucketResultKernelData.index;
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanAddBucketResultIndex, _prefixSumInputBufferNameId, _prefixSumAuxBuffer);
      _cmdBuffer1.SetComputeBufferParam(prefixSumCs, prefixSumScanAddBucketResultIndex, _prefixSumResultBufferNameId, _prefixSumResultBuffer);
      _cmdBuffer1.DispatchCompute(prefixSumCs, prefixSumScanAddBucketResultIndex, _threadGroupCount, 1, 1);

      // Final Prefix sums are are in _prefixSumResultBuffer

      /* -----------------------------------------------------------------------
       * Setup DistanceField 3D RenderTexture
       * -----------------------------------------------------------------------
       */
      if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
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
      DistanceField3DTexture.wrapMode = TextureWrapMode.Clamp;
      DistanceField3DTexture.Create();

      DistanceGradientField3DTexture = new RenderTexture(new RenderTextureDescriptor
      {
        autoGenerateMips = false,
        enableRandomWrite = true,
        useMipMap = false,
        depthBufferBits = 0,
        width = _volumeResolution,
        height = _volumeResolution,
        volumeDepth = _volumeResolution,
        colorFormat = RenderTextureFormat.ARGBFloat,
        graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
        msaaSamples = 1,
        dimension = TextureDimension.Tex3D,
        bindMS = false,

      });
      DistanceGradientField3DTexture.wrapMode = TextureWrapMode.Clamp;
      DistanceGradientField3DTexture.Create();

      /* -----------------------------------------------------------------------
       * Setup Dynamic Depth Buffer
       * -----------------------------------------------------------------------
       *  - We need to know the number of fragments, so this has to come after cmdBuffer1 has finished. We do this inside the asyncReadback callback.
       */
      _cmdBuffer1.RequestAsyncReadback(_prefixSumResultBuffer, (AsyncGPUReadbackRequest readback) =>
      {
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

          // Create Gradients
          var distanceToGradientFieldCs = _simulationResources.shaders.distanceToGradientFieldComputeShader;
          var distanceToGradientFieldKernelIndex = _simulationResources.shaders.distanceToGradientKernelData.index;

          _cmdBuffer2.SetComputeIntParam(distanceToGradientFieldCs, "volumeResolution", _volumeResolution);

          _cmdBuffer2.SetComputeTextureParam(distanceToGradientFieldCs, distanceToGradientFieldKernelIndex, _distanceFieldTextureNameId, DistanceField3DTexture);
          _cmdBuffer2.SetComputeTextureParam(distanceToGradientFieldCs, distanceToGradientFieldKernelIndex, _distanceGradientFieldTextureNameId, DistanceGradientField3DTexture);

          int threadGroupCountX = (int)(_volumeResolution / _simulationResources.shaders.distanceToGradientKernelData.numThreadsX);
          int threadGroupCountY = (int)(_volumeResolution / _simulationResources.shaders.distanceToGradientKernelData.numThreadsY);
          int threadGroupCountZ = (int)(_volumeResolution / _simulationResources.shaders.distanceToGradientKernelData.numThreadsZ);

          _cmdBuffer2.DispatchCompute(distanceToGradientFieldCs, distanceToGradientFieldKernelIndex, threadGroupCountX, threadGroupCountY, threadGroupCountZ);

#if UNITY_EDITOR
          _cmdBuffer2.RequestAsyncReadback(DistanceGradientField3DTexture, (AsyncGPUReadbackRequest obj) =>
          {
            _distanceGradientFieldData = new Vector4[obj.width, obj.height, obj.depth];
            for (int z = 0; z < obj.depth; z++)
            {
              var distanceFieldLayerData = obj.GetData<Vector4>(z);
              int flatIndex = 0;
              for (int x = 0; x < obj.width; x++)
              {
                for (int y = 0; y < obj.height; y++)
                {
                  _distanceGradientFieldData[x, y, z] = distanceFieldLayerData[flatIndex];
                  flatIndex++;
                }
              }
            }
          });
        }
#endif
        // Execute DynamicDepthBuffer and DistanceField construction.
        Graphics.ExecuteCommandBuffer(_cmdBuffer2);
        // Now cmdbuffer1 may be rerun
        _shouldRunCmdBuffer1 = true;
      });
    }

    public void ExecutePipeline()
    {
      if (_shouldRunCmdBuffer1) Graphics.ExecuteCommandBuffer(_cmdBuffer1);
      _shouldRunCmdBuffer1 = false;
    }

    public void DrawGizmos(int instanceId, Vector3Int point, bool showInstancedDepthTexture, bool drawViewingPlanes, bool drawTransformedMeshes, bool showDistanceFieldInformation, bool toggleVectorDistanceGizmo)
    {
#if UNITY_EDITOR
      instanceId = Mathf.Clamp(instanceId, 0, ViewDirectionCount - 1);
      if (drawTransformedMeshes)
      {
        foreach (var fieldObject in _distanceFieldObjects)
        {
          for (int i = 0; i < fieldObject.sharedMesh.subMeshCount; i++)
          {
            Gizmos.matrix = _viewMatrices[instanceId] * fieldObject.transform.localToWorldMatrix;
            Gizmos.DrawMesh(fieldObject.sharedMesh, i, Vector3.zero);
          }
        }
      }

      if (drawViewingPlanes)
      {
        Gizmos.matrix = _viewMatrices[instanceId];
        Gizmos.color = instanceId == 0 ? Color.red : (instanceId == 1) ? Color.green : Color.blue;

        Gizmos.DrawWireCube(_volumeBounds.center - new Vector3(0, 0, _volumeBounds.extents.z), new Vector3(_volumeBounds.size.x, _volumeBounds.size.y, 1));
      }

      if (_volumeResolution >= 64)
      {
        Debug.LogWarning("Volume Resolution is too high to show distance Field Information.");
        showDistanceFieldInformation = false;
      }

      if (showDistanceFieldInformation)
      {
        Gizmos.matrix = _gridModelMatrix;
        for (int x = 0; x < _volumeResolution; x++)
        {
          float colorx = 1.0f * x / _volumeResolution;
          for (int y = 0; y < _volumeResolution; y++)
          {
            float colory = 1.0f * y / _volumeResolution;
            for (int z = 0; z < _volumeResolution; z++)
            {
              float depth = _distanceGradientFieldData[x, y, z].w;
              if (Mathf.Approximately(depth, -1.0f))
              {
                continue;
              }

              if (toggleVectorDistanceGizmo)
              {
                var color = new Color(_distanceGradientFieldData[x, y, z].x / 2 + 0.5f, _distanceGradientFieldData[x, y, z].y / 2 + 0.5f, _distanceGradientFieldData[x, y, z].z / 2 + 0.5f, 1);
                Gizmos.color = color;
                Gizmos.DrawSphere(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), 0.1f);
                Gizmos.DrawRay(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), _distanceGradientFieldData[x, y, z]);
              }
              else
              {
                Gizmos.color = new Color(colorx, colory, 1.0f * z / _volumeResolution);
                Gizmos.DrawSphere(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), depth);
              }

            }
          }
        }
      }

      if (showInstancedDepthTexture)
      {
        Gizmos.DrawGUITexture(new Rect(Vector2.zero, Vector2.one * _volumeBounds.size.x), _dummyRenderTarget);
      }
#endif
    }
  }
}
