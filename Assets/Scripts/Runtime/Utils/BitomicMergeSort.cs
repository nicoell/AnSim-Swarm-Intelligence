/*
MIT License

Copyright (c) 2019 Nakata Nobuyuki (仲田将之)

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

From nobnak - GPUMergeSortForUnity:
  https://github.com/nobnak/GPUMergeSortForUnity

*/

using UnityEngine;

namespace AnSim.Runtime.Utils
{
  public class BitomicMergeSort
  {
    private readonly int _blockNameId = Shader.PropertyToID("block");
    private readonly int _dimNameId = Shader.PropertyToID("dim");
    private readonly int _countNameId = Shader.PropertyToID("count");
    private readonly int _keysNameId = Shader.PropertyToID("Keys");
    private readonly int _valuesNameId = Shader.PropertyToID("Values");

    private readonly ComputeShader _computeShader;
    private readonly CsKernelData _kernelInit;
    private readonly CsKernelData _kernelSort;

    private int _keyBufferSize;
    private int _x, _y, _z;

    public BitomicMergeSort(ComputeShader computeShader, CsKernelData kernelInit, CsKernelData kernelSort)
    {
      _computeShader = computeShader;
      _kernelInit = kernelInit;
      _kernelSort = kernelSort;
    }

    public void Init(in ComputeBuffer keyBuffer)
    {
      _keyBufferSize = keyBuffer.count;
      CalcWorkSize(_keyBufferSize, out _x, out _y, out _z);
      _computeShader.SetInt(_countNameId, _keyBufferSize);
      _computeShader.SetBuffer(_kernelInit.index, _keysNameId, keyBuffer);
      _computeShader.Dispatch(_kernelInit.index, _x, _y, _z);
    }

    public void Sort(ComputeBuffer keys, ComputeBuffer values)
    {
      _computeShader.SetInt(_countNameId, _keyBufferSize);

      for (var dim = 2; dim <= _keyBufferSize; dim <<= 1)
      {
        _computeShader.SetInt(_dimNameId, dim);
        for (var block = dim >> 1; block > 0; block >>= 1)
        {
          _computeShader.SetInt(_blockNameId, block);
          _computeShader.SetBuffer(_kernelSort.index, _keysNameId, keys);
          _computeShader.SetBuffer(_kernelSort.index, _valuesNameId, values);
          _computeShader.Dispatch(_kernelSort.index, _x, _y, _z);
        }
      }
    }


    private const int GroupSize = 256;
    private const int MaxDimGroups = 1024;
    private const int MaxDimThreads = (GroupSize * MaxDimGroups);

    private static void CalcWorkSize(int length, out int x, out int y, out int z)
    {
      if (length <= MaxDimThreads)
      {
        x = (length - 1) / GroupSize + 1;
        y = z = 1;
      }
      else
      {
        x = MaxDimGroups;
        y = (length - 1) / MaxDimThreads + 1;
        z = 1;
      }
    }
    private static int AlignBufferSize(int length)
    {
      return ((length - 1) / GroupSize + 1) * GroupSize;
    }
  }
}
