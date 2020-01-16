using UnityEngine;

namespace AnSim.Runtime
{
  public struct SwarmRenderingUniforms {
    public Color particleTint;

    public static int GetSize() => (4) * sizeof(float);
  }

  public struct MasterSwarmUniforms
  {
    public float c1; // acceleration constant for individual interaction
    public float c2; // acceleration constant for social interaction
    public float c3; // acceleration constant for interaction with slaves
    public float alpha; // disturbance constant for boundary conditions
    public uint n; // Number of particle replications
    public Vector3 target; // Target Position
    public float inertiaWeight; // linearly descreases every iteration
    public Vector3 slaveGlobalBest; // Best global value obtained by the slave swarms
    public uint swarmBufferMasterIndex; //Index of SwarmBuffer to Master Swarm
    public uint swarmParticleBufferMasterOffset; //Index of SwarmBuffer to Master Swarm

    public static int GetSize() => (5 + 3 + 3) * sizeof(float) + (3) * sizeof(uint);
  }

  public struct SlaveSwarmUniforms
  {
    public float c1;
    public float c2;
    public float alpha;
    public uint n;
    public Vector3 target;
    public float inertiaWeight;

    public static int GetSize() => (3 + 3 + 1) * sizeof(float) + sizeof(uint);
  }

  public struct SwarmSimulationUniforms
  {
    public Vector3 worldmin;
    public Vector3 worldmax;
    public Vector3 time;

    public static int GetSize() => (3 + 3 + 3) * sizeof(float);
  }

  public struct SwarmParticleData
  {
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 localBest;
    public float fitness;

    public static int GetSize() => (3 + 3 + 3 + 1) * sizeof(float);
  }

  public struct SwarmData
  {
    public Vector3 globalBest; //best solution of all particles in a swarm
    public float fitness; //rating of the globalBest
    public Vector2 rand;

    public static int GetSize() => (3 + 1 + 2) * sizeof(float);
  };

}
