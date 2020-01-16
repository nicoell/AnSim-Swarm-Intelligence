using System;
using UnityEngine;

namespace AnSim.Runtime.Utils
{
  [Serializable]
  public class CsKernelData
  {
    public string name;
    public int index;
    public uint numThreadsX;
    public uint numThreadsY;
    public uint numThreadsZ;

    public CsKernelData(ComputeShader cs, string kernelName)
    {
      name = kernelName;
      index = cs.FindKernel(name);
      cs.GetKernelThreadGroupSizes(index, out numThreadsX, out numThreadsY, out numThreadsZ);
    }

  }
}
