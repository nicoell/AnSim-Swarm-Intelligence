#ifndef DISTANCE_FIELD_VOLUME_SIZE
#define DISTANCE_FIELD_VOLUME_SIZE

#define THREADS_PER_GROUP 128 // used for prefix sum, needs to be even power of two (2^x ; x = EVEN) 8 32 128 512
#define VOLUME_RESOLUTION 64 // !!! must always be half of THREADS_PER_GROUP

#endif // DISTANCE_FIELD_VOLUME_SIZE
