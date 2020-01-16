#ifndef SHARED_INPUTS
#define SHARED_INPUTS

cbuffer SwarmSimulationUniforms
{
  float3 worldmin; // World Boundaries Min 
  float3 worldmax; // World Boundaries Max
  float3 time; //x: DeltaTime y:Time since start
}

#endif  // SHARED_INPUTS
