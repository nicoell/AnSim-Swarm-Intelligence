using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnSim.Runtime;
using UnityEngine;

namespace Ansim.Runtime
{
  class DistanceFieldObjectData : MonoBehaviour
  {
    public MaterialPropertyBlock materialPropertyBlock;
    public ComputeBuffer[] argumentBuffers;
    public Mesh sharedMesh;
  }
}
