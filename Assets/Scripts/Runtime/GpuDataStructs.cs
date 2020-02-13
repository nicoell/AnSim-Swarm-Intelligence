using System.Runtime.InteropServices;
using UnityEngine;

namespace AnSim.Runtime
{
  /*
   * For constant/uniform buffers (cbuffer) there are strict packing rules
   * https://docs.microsoft.com/en-us/windows/win32/direct3dhlsl/dx-graphics-hlsl-packing-rules
   *
   * StructuredBuffers are by definition tightly packed, but they should be:
   *  - divisible by 128bits (because of cache line)
   *  - and vectors (float4) should be naturally aligned (to not fall into different cache lines)
   */

  [StructLayout(LayoutKind.Explicit, Size = 16)]
  public struct SwarmRenderingUniforms {
    [FieldOffset(0)]
    public Color particleTint;
  }

  [StructLayout(LayoutKind.Explicit, Size = 64)]
  public struct MasterSwarmUniforms
  {
    [FieldOffset(0)]
    public float c1; // acceleration constant for individual interaction
    [FieldOffset(4)]
    public float c2; // acceleration constant for social interaction
    [FieldOffset(8)]
    public float c3; // acceleration constant for interaction with slaves
    [FieldOffset(12)]
    public float alpha; // disturbance constant for boundary conditions
    //------------------------------------------- 16byte Boundary
    [FieldOffset(16)]
    public uint n; // Number of particle replications
    [FieldOffset(20)]
    public Vector3 target; // Target Position
    //------------------------------------------- 16byte Boundary
    [FieldOffset(32)]
    public float inertiaWeight; // linearly descreases every iteration
    [FieldOffset(36)]
    public float worldDodgeBias;
    [FieldOffset(40)]
    public float unused1;
    [FieldOffset(44)]
    public float unused2;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(48)]
    public uint swarmParticleBufferMasterOffset; //Index of SwarmBuffer to Master Swarm
    [FieldOffset(52)]
    public uint swarmBufferMasterIndex; //Index of SwarmBuffer to Master Swarm
    [FieldOffset(56)]
    public float sigma; // mutation strategy parameter for inertia and acceleration mutation
    [FieldOffset(60)]
    public float sigma_g; // disturbance constant to the global best mutation
    //------------------------------------------- 16byte Boundary
  }

  [StructLayout(LayoutKind.Explicit, Size = 64)]
  public struct SlaveSwarmUniforms
  {
    [FieldOffset(0)]
    public float c1;
    [FieldOffset(4)]
    public float c2;
    [FieldOffset(8)]
    public float alpha;
    [FieldOffset(12)]
    public uint n;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(16)]
    public Vector3 target;
    [FieldOffset(28)]
    public float inertiaWeight;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(32)]
    public float worldDodgeBias;
    [FieldOffset(36)]
    public float unused1;
    [FieldOffset(40)]
    public float unused2;
    [FieldOffset(44)]
    public float sigma;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(48)]
    public float sigma_g;
    // +4Byte
    // +4Byte
    // +4Byte
    //------------------------------------------- 16byte Boundary
  }

  [StructLayout(LayoutKind.Explicit, Size = 32)]
  public struct SwarmSimulationUniforms
  {
    [FieldOffset(0)]
    public Vector3 worldmin;
    [FieldOffset(12)]
    public float timeScale;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(16)]
    public Vector3 worldmax;
    [FieldOffset(28)]
    public float timeSinceStart;
    //------------------------------------------- 16byte Boundary
  }

  [StructLayout(LayoutKind.Explicit, Size = 64)]
  public struct SwarmResetUniforms
  {
    [FieldOffset(0)]
    public Vector3 resetPosition;
    [FieldOffset(12)]
    public float positionVariance;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(16)]
    public Vector3 target;
    [FieldOffset(28)]
    public float velocityVariance;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(32)]
    public uint enablePositionReset;
    [FieldOffset(36)]
    public uint enableVelocityReset;
    [FieldOffset(40)]
    public uint reviveParticles;
    [FieldOffset(44)]
    public uint resetOnlyIfRevived; //Only reset pos and velocity if particle actually gets revived (health of particle was 0)
    //------------------------------------------- 16byte Boundary
    [FieldOffset(48)]
    public float reviveHealthAmount; //Only matters if reviveParticles is true
  }

  public struct SwarmParticleData
  {
    public Vector3 position;
    public Vector3 velocity;
    public float health;
    public Vector3 localBest;
    public float fitness;
    public Vector3[] rotationMatrix; //because there is no matrix3x3 and we just need this to represent gpu size

    public static int GetSize() => (3 + 3 + 1 + 3 + 1 + 3*3) * sizeof(float);
  }

  public struct SwarmData
  {
    public Vector3 globalBest; //best solution of all particles in a swarm
    public float fitness; //rating of the globalBest
    public uint particlesAlive;
    //public Vector2 rand;

    public static int GetSize() => (3 + 1 + 1) * sizeof(float);
  };

  public struct DistanceFieldObjectMatrices
  {
    public Matrix4x4 matrixA;
    public Matrix4x4 matrixB;
    public Matrix4x4 matrixC;

    public static int GetSize() => 3 * (4 * 4) * sizeof(float);
  }

  public struct ParticleHashData
  {
    public Vector3 particlePos;
    public uint cellId;

    public static int GetSize() => (3 + 1) * sizeof(float);
  }

}
