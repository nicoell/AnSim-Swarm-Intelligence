using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace AnSim.Runtime
{
  public class UniformGrid
  {
    // ------- MEMBER ---------
    private int _totalNumParticles;

    private uint _threadsPerGroup; // in X dimension
    private int _threadGroupCount; // in X dimension
    private readonly SimulationResources _simulationResources;

    private Bounds _simulationBounds; // BB of simulation is the same as our grids bb

    private Vector3 _gridSize; // In X-,Y-,Z-Dimension.
    private Vector3 _cellSize; // In X-,Y-,Z-Dimension
    private Vector3 _worldOrigin;
    private int _numGridCells; // total number of cells in the grid

    private int _tempParticleHashBufferNameId;
    private ComputeBuffer _tempParticleHashBuffer;
    private int _particleHashBufferNameId;
    private ComputeBuffer _particleHashBuffer;
    private int _cellStartBufferNameId;
    private ComputeBuffer _cellStartBuffer;

    // ------- METHODS --------
    public UniformGrid(in SimulationResources simulationResources, Bounds simulationBounds)
    {
      _simulationResources = simulationResources;
      _simulationBounds = simulationBounds;
    }

    public void Init(int totalNumParticles, Vector3Int gridSize)
    {
      // Init Member variables
      _totalNumParticles = totalNumParticles;
      _gridSize = gridSize;
      _threadsPerGroup = _simulationResources.shaders.uniformGridFindCellStartKernelData.numThreadsX;// get the number of work items (threads) per work group
      _threadGroupCount = (int)Math.Ceiling(_totalNumParticles / (float)_threadsPerGroup);// calc number of work groups for dispatch
      _worldOrigin = _simulationBounds.min;
      _cellSize.x = _simulationBounds.size.x / _gridSize.x; // worldSize / gridSize
      _cellSize.y = _simulationBounds.size.y / _gridSize.y;
      _cellSize.z = _simulationBounds.size.z / _gridSize.z;
      _numGridCells = (int)Math.Floor(_gridSize.x * _gridSize.y * _gridSize.z);

      // Init TempParticleHashBuffer
      _tempParticleHashBufferNameId = Shader.PropertyToID("TempParticleHashBuffer");
      _tempParticleHashBuffer = new ComputeBuffer(_totalNumParticles, ParticleHashData.GetSize(), ComputeBufferType.Structured);
      resetParticleHashBuffer(_tempParticleHashBuffer);
      // Init ParticleHashBuffer
      _particleHashBufferNameId = Shader.PropertyToID("ParticleHashBuffer");
      _particleHashBuffer = new ComputeBuffer(_totalNumParticles, ParticleHashData.GetSize(), ComputeBufferType.Structured);
      resetParticleHashBuffer(_particleHashBuffer);
      // Init CellStartBuffer
      _cellStartBufferNameId = Shader.PropertyToID("CellStartBuffer");
      _cellStartBuffer = new ComputeBuffer(_numGridCells, sizeof(uint), ComputeBufferType.Structured);
      resetUIntBuffer(_cellStartBuffer);

      // Set Input Buffer for 3rd Pass
      _simulationResources.shaders.uniformGridConstructionComputeShader.SetBuffer(_simulationResources.shaders.uniformGridFindCellStartKernelData.index, _particleHashBufferNameId, _particleHashBuffer);
      _simulationResources.shaders.uniformGridConstructionComputeShader.SetBuffer(_simulationResources.shaders.uniformGridFindCellStartKernelData.index, _cellStartBufferNameId, _cellStartBuffer);
           
    }

    public void Update()
    {
      /*********************
       ** 1.PASS CalcHash: Evaluates and stores each (cell id, element id) pair in a temporary buffer (TempParticleHashBuffer)
       ** -> Calc first Pass in SwarmSimulation-Shader in Simulate().
       ********************/

      // reset Buffer for 2 + 3 Pass
      resetUIntBuffer(_cellStartBuffer);
      resetParticleHashBuffer(_particleHashBuffer);

      /*********************
       ** 2.PASS: sorts these pairs on the basis of their cell id value so that elements of the same cell are stored sequentially in memory
       ********************/
      //TODO: Implement Sorting algorithm for 2.pass on gpu

      SortParticleBuffers();

      /*********************
       **  3.PASS FindCellStart: finds the start index and end index of every cell in the sorted array
       ********************/
      _simulationResources.shaders.uniformGridConstructionComputeShader.Dispatch(_simulationResources.shaders.uniformGridFindCellStartKernelData.index, _threadGroupCount, 1, 1);

      // reset Buffer for next iterations 1.Pass
      resetParticleHashBuffer(_tempParticleHashBuffer);
    }

    /// <summary>
    /// Sorts the data from TempParticleHashBuffer after the cellID and stores it in the ParticleHashBuffer
    /// </summary>
    private void SortParticleBuffers()
    {
      ParticleHashData[] particleHashes = new ParticleHashData[_totalNumParticles];
      // Get unsorted data from gpu buffer
      _tempParticleHashBuffer.GetData(particleHashes);

      Array.Sort(particleHashes, (x, y) => x.cellId.CompareTo(y.cellId));

      // Put sorted array back to gpu buffer
      _particleHashBuffer.SetData(particleHashes); 
    }

    private void resetUIntBuffer(ComputeBuffer bufferToReset)
    {
      //Clear cellStartBuffer
      var clearBufferUIntCompute = _simulationResources.shaders.clearBufferUintComputeShader;
      var clearBufferUintCsKernel = _simulationResources.shaders.clearBufferUintUniformGridKernelData;
      uint threadsUintCsKernel = clearBufferUintCsKernel.numThreadsX;// get the number of work items (threads) per work group
      int groupsUintCsKernel = (int)Math.Ceiling(_numGridCells / (float)threadsUintCsKernel);// calc number of work groups for dispatch

      clearBufferUIntCompute.SetInt("NumGridCells", _numGridCells);
      clearBufferUIntCompute.SetBuffer(clearBufferUintCsKernel.index, "BufferToClear", bufferToReset);
      clearBufferUIntCompute.Dispatch(clearBufferUintCsKernel.index, groupsUintCsKernel, 1, 1);

    }
    private void resetParticleHashBuffer(ComputeBuffer bufferToReset)
    {
      //Clear particleHashBuffer
      var clearBufferParticleCompute = _simulationResources.shaders.clearBufferParticleHashComputeShader;
      var clearBufferParticleCsKernel = _simulationResources.shaders.clearBufferParticleHashKernelData;
      uint threadsParticleCsKernel = clearBufferParticleCsKernel.numThreadsX;// get the number of work items (threads) per work group
      int groupsParticleCsKernel = (int)Math.Ceiling(_totalNumParticles / (float)threadsParticleCsKernel);// calc number of work groups for dispatch

      clearBufferParticleCompute.SetInt("NumGridCells", _numGridCells);
      clearBufferParticleCompute.SetBuffer(clearBufferParticleCsKernel.index, "BufferToClear", bufferToReset);
      clearBufferParticleCompute.Dispatch(clearBufferParticleCsKernel.index, groupsParticleCsKernel, 1, 1);
    }

    public int ParticleHashBufferNameId
    {
      get => _particleHashBufferNameId;
    }
    public ComputeBuffer ParticleHashBuffer
    {
      get => _particleHashBuffer;
    }
    public int CellStartBufferNameId
    {
      get => _cellStartBufferNameId;
    }
    public ComputeBuffer CellStartBuffer
    {
      get => _cellStartBuffer;
    }
    public int TempParticleHashBufferNameId
    {
      get => _tempParticleHashBufferNameId;
    }
    public ComputeBuffer TempParticleHashBuffer
    {
      get => _tempParticleHashBuffer;
      set => _tempParticleHashBuffer = value;
    }

    public Vector3 GridSize
    {
      get => _gridSize;
    }
    public Vector3 CellSize
    {
      get => _cellSize;
    }
    public Vector3 WorldOrigin
    {
      get => _worldOrigin;
    }


  }
}
