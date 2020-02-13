#ifndef SHARED_INPUTS
#define SHARED_INPUTS

cbuffer SwarmSimulationUniforms
{
  float3 worldmin; // World Boundaries Min 
  float timeScale;
  //------------------------------------------- 16byte Boundary
  float3 worldmax; // World Boundaries Max
  float timeSinceStart;
};

#endif  // SHARED_INPUTS
