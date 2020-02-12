Shader "Custom/InstancedSwarmRendering" {
  Properties {
    //_MainTex("Texture", 2D) = "white" {}
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

      StructuredBuffer<uint> SwarmIndexBuffer;
      StructuredBuffer<SwarmParticleData> SwarmParticleBuffer;
      uniform float4 particleTint;
      uniform uint swarmSize;

      struct VertIn {
        float4 vertex : POSITION;
        // float2 uv : TEXCOORD0;
      };

      struct VertOut {
        float4 pos : SV_POSITION;
        float4 color : TEXCOORD0;
      };

      VertOut vert(VertIn vertIn, uint instanceID : SV_InstanceID) 
      {
        uint swarmIndexBufferIndex = instanceID / swarmSize;
        uint groupIndex = instanceID % swarmSize;
        uint swarmIndex =
            SwarmIndexBuffer[swarmIndexBufferIndex];
        uint particleIndex = swarmIndex * swarmSize + groupIndex;
        SwarmParticleData particle = SwarmParticleBuffer[particleIndex];

        VertOut o;
        o.pos = float4(float3(1, 1, 5) * vertIn.vertex.xyz, 1);
        o.pos = float4(mul(particle.rotationMatrix, o.pos.xyz), 1);
        o.pos = float4(o.pos.xyz + particle.position.xyz, 1);
        //o.pos.xyz = vertIn.vertex.xyz + float3(particleIndex, particleIndex, particleIndex);
        o.pos = mul(UNITY_MATRIX_VP, o.pos);
        o.color = /*float(swarmIndex)/ 32.0 * */particleTint * particle.health;

        //Debug Color Master Swarm
        if (swarmIndex == 16) {
          o.color = float4(1, 0, 0, 1);
        }
        return o;
      }

      fixed4 frag(VertOut vertOut) : SV_Target {
        fixed4 col = vertOut.color;

        return col;
      }
      ENDCG
    }
  }
}
