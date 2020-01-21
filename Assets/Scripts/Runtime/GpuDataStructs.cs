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
    public Vector3 maxVelocity; // Max velocity
    //------------------------------------------- 16byte Boundary
    [FieldOffset(48)]
    public uint swarmParticleBufferMasterOffset; //Index of SwarmBuffer to Master Swarm
    [FieldOffset(52)]
    public uint swarmBufferMasterIndex; //Index of SwarmBuffer to Master Swarm
    //[FieldOffset(56)]
    //public uint p1;
    //[FieldOffset(60)]
    //public uint p2;
    // +4Byte
    // +4Byte
    //------------------------------------------- 16byte Boundary
  }

  [StructLayout(LayoutKind.Explicit, Size = 48)]
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
    public Vector3 maxVelocity;
    // +4Byte
    //------------------------------------------- 16byte Boundary
  }

  [StructLayout(LayoutKind.Explicit, Size = 32)]
  public struct SwarmSimulationUniforms
  {
    [FieldOffset(0)]
    public Vector3 worldmin;
    [FieldOffset(12)]
    public float deltaTime;
    //------------------------------------------- 16byte Boundary
    [FieldOffset(16)]
    public Vector3 worldmax;
    [FieldOffset(28)]
    public float timeSinceStart;
    //------------------------------------------- 16byte Boundary
  }

  [StructLayout(LayoutKind.Explicit, Size = 48)]
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
    // +4Byte
    //------------------------------------------- 16byte Boundary
  }

  public struct SwarmParticleData
  {
    public Vector3 position;
    public Vector3 velocity;
    private float health;
    public Vector3 localBest;
    public float fitness;

    public static int GetSize() => (3 + 3 + 1 + 3 + 1) * sizeof(float);
  }

  public struct SwarmData
  {
    public Vector3 globalBest; //best solution of all particles in a swarm
    public float fitness; //rating of the globalBest
    public int particlesAlive;
    //public Vector2 rand;

    public static int GetSize() => (3 + 1 + 1) * sizeof(float);
  };

}
