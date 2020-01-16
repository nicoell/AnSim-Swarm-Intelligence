#ifndef RANDOM_INCLUDED
#define RANDOM_INCLUDED

//https://www.shadertoy.com/view/4djSRW
// *** Change these to suit your range of random numbers..

// *** Use this for integer stepped ranges, ie Value-Noise/Perlin noise functions.
//#define HASHSCALE1 .1031
//#define HASHSCALE3 float3(.1031, .1030, .0973)
//#define HASHSCALE4 float4(.1031, .1030, .0973, .1099)

// For smaller input rangers like audio tick or 0-1 UVs use these...
#define HASHSCALE1 443.8975
#define HASHSCALE3 float3(443.897, 441.423, 437.195)
#define HASHSCALE4 float4(443.897, 441.423, 437.195, 444.129)

#define MOD3 float3(.1031,.11369,.13787)

float hash11(float n)
{
  return frac(sin(n) * 43758.5453123);
}

float hash12(float2 p)
{
  float3 p3 = frac(float3(p.xyx) * MOD3);
  p3 += dot(p3, p3.yzx + 19.19);
  return frac((p3.x + p3.y) * p3.z);
}

float hash13(float3 p3)
{
  p3 = frac(p3 * HASHSCALE1);
  p3 += dot(p3, p3.yzx + 19.19);
  return frac((p3.x + p3.y) * p3.z);
}

float2 hash21(float p)
{
  float3 p3 = frac(float3(p, p, p) * HASHSCALE3);
  p3 += dot(p3, p3.yzx + 19.19);
  return frac((p3.xx + p3.yz) * p3.zy);

}

float2 hash22(float2 p)
{
  p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
  return frac(sin(p) * 43758.5453);
}


float3 hash31(float n)
{
  return frac(sin(n + float3(0.0, 13.1, 31.3)) * 158.5453123);
}

float4 hash42(float2 p)
{
  float4 p4 = frac(float4(p.xyxy) * HASHSCALE4);
  p4 += dot(p4, p4.wzxy + 19.19);
  return frac((p4.xxyz + p4.yzzw) * p4.zywx);

}

#endif  // RANDOM_INCLUDED
