Shader "Custom/InstancedSwarmRendering" {
  Properties {
    _MainTex("Texture", 2D) = "white" {}
    _Tint("Tint", Color) = (1.0, 1.0, 1.0, 1.0)
    _NormalTex("Normal", 2D) = "white" {}
    _Scale("Scale", Vector) = (1.0, 1.0, 1.0)
  }
  SubShader {
    Tags{"RenderType" = "Opaque"}

    Pass {
      CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 4.5

#include "Definitions/Structs.hlsl"
#include "UnityCG.cginc"

      sampler2D _MainTex;
      sampler2D _NormalTex;
      float3 _Scale;
      float4 _Tint;

      StructuredBuffer<uint> SwarmIndexBuffer;
      StructuredBuffer<SwarmParticleData> SwarmParticleBuffer;
      uniform float4 particleTint;
      uniform uint swarmSize;

      struct VertIn {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };

      struct VertOut {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 color : TEXCOORD1;
      };

      /*
      struct SwarmParticleData
      {
	      float3 position;
	      float3 velocity;
        float health;
        float3 localBest; //best solution of this particle
        float fitness; //rating of the localBest
        float3x3 rotationMatrix;
      };
*/

      VertOut vert(VertIn vertIn, uint instanceID : SV_InstanceID) 
      {
        uint swarmIndexBufferIndex = instanceID / swarmSize;
        uint groupIndex = instanceID % swarmSize;
        uint swarmIndex =
            SwarmIndexBuffer[swarmIndexBufferIndex];
        uint particleIndex = swarmIndex * swarmSize + groupIndex;

        SwarmParticleData particle = SwarmParticleBuffer[particleIndex];

        VertOut o;
        o.pos = float4(_Scale * vertIn.vertex.xyz, 1);
        o.pos = float4(mul(particle.rotationMatrix, o.pos.xyz), 1);
        o.pos = float4(o.pos.xyz + particle.position.xyz, 1);
        
        o.pos = mul(UNITY_MATRIX_VP, o.pos);

        o.color = _Tint * particle.health;
        o.uv = vertIn.uv;
        
        return o;
      }

      float4 frag(VertOut vertOut) : SV_Target 
      {
        float ambientLight = 0.0f;
        float3 normal = tex2D(_NormalTex, vertOut.uv);
        const float3 lightDir = -normalize(float3(0.2, 1.0, 0.2));

        float diffuse = saturate(dot(-normal, lightDir));
        float3 col = tex2D(_MainTex, vertOut.uv) * vertOut.color.xyz;

        col *= ambientLight + diffuse;

        return float4(col, 1.0);
      }
      ENDCG
    }
  }
}
