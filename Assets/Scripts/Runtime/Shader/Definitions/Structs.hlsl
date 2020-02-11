#ifndef STRUCTS_INCLUDED
#define STRUCTS_INCLUDED

struct SwarmParticleData
{
	float3 position;
	float3 velocity;
  float health;
  float3 localBest; //best solution of this particle
  float fitness; //rating of the localBest
  float3x3 rotationMatrix;
};

struct SwarmData
{
  float3 globalBest; //best solution of all particles in a swarm
  float fitness; //rating of the globalBest
  uint particlesAlive;
  //float2 rand;
};

struct DistanceFieldObjectMatrices
{
  float4x4 matrices[3];
};

#endif  // STRUCTS_INCLUDED
