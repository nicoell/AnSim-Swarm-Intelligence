#ifndef SIMULATION_INPUTS
#define SIMULATION_INPUTS

#ifdef SETUP_ONLY
cbuffer SwarmResetUniforms
{
  float3 resetPosition;
  float positionVariance;
  //------------------------------------------- 16byte Boundary
  float3 target;
  float velocityVariance;
  //------------------------------------------- 16byte Boundary
  bool enablePositionReset;
  bool enableVelocityReset;
  bool reviveParticles;
  // +4Byte
  //------------------------------------------- 16byte Boundary
}
#endif

#ifdef SLAVE_SIM
cbuffer SlaveSwarmUniforms
{
  float c1; // acceleration constant for individual interaction
  float c2; // acceleration constant for social interaction
  float alpha; // disturbance constant for boundary conditions
  uint n; // Number of particle replications
  //------------------------------------------- 16byte Boundary
  float3 target; // Target Position
  float inertiaWeight; // linearly descreases every iteration
  //------------------------------------------- 16byte Boundary
  float3 maxVelocity;
  float sigma; // [mutationStrategyParameter] for inertia and acceleration mutation
  //------------------------------------------- 16byte Boundary
  float sigma_g; // [globalBestDisturbanceConstant] disturbance constant to the global best
  // +4Byte
  // +4Byte
  // +4Byte
  //------------------------------------------- 16byte Boundary
};
#endif

#ifdef MASTER_SIM
cbuffer MasterSwarmUniforms
{
  float c1; // acceleration constant for individual interaction
  float c2; // acceleration constant for social interaction
  float c3; // acceleration constant for interaction with slaves
  float alpha; // disturbance constant for boundary conditions
  //------------------------------------------- 16byte Boundary
  uint n; // Number of particle replications
  float3 target; // Target Position
  //------------------------------------------- 16byte Boundary
  float inertiaWeight; // linearly descreases every iteration
  float3 maxVelocity; // Best global value obtained by the slave swarms
  //------------------------------------------- 16byte Boundary
  uint swarmParticleBufferMasterOffset; //Index to first Master Swarm Particle in SwarmParticleBuffer
  uint swarmBufferMasterIndex; //Index to Master Swarm in SwarmBuffer   
  float sigma; // [mutationStrategyParameter] for inertia and acceleration mutation
  float sigma_g; // [globalBestDisturbanceConstant] disturbance constant to the global best
  //------------------------------------------- 16byte Boundary
};
#endif

#endif  // SIMULATION_INPUTS
