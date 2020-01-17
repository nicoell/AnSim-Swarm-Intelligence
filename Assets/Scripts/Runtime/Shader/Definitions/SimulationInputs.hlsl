﻿#ifndef SIMULATION_INPUTS
#define SIMULATION_INPUTS

#if defined(__INTELLISENSE__)
#define SETUP_ONLY
#define SLAVE_SIM 
#define MASTER_SIM
#define SLAVE_OR_MASTER
#endif


#ifdef SETUP_ONLY
float3 startPosition;
float3 target;
float positionVariance;
float velocityVariance;
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
  float3 slaveGlobalBest; // Best global value obtained by the slave swarms
  //------------------------------------------- 16byte Boundary
  uint swarmBufferMasterIndex; //Index to Master Swarm in SwarmBuffer 
  uint swarmParticleBufferMasterOffset; //Index to first Master Swarm Particle in SwarmParticleBuffer
  // +4Byte
  // +4Byte
  //------------------------------------------- 16byte Boundary
};
#endif

#endif  // SIMULATION_INPUTS
