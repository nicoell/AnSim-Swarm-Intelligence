#ifndef STRUCTS_INCLUDED
#define STRUCTS_INCLUDED

struct SwarmParticleData
{
	float3 position;
	float3 velocity;
  float3 localBest; //best solution of this particle
  float fitness; //rating of the localBest
};

struct SwarmData
{
  float3 globalBest; //best solution of all particles in a swarm
  float fitness; //rating of the globalBest
  //float2 rand;
};


#endif  // STRUCTS_INCLUDED
