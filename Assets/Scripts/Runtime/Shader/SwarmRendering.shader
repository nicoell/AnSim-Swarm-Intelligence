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

      StructuredBuffer<SwarmParticleData> SwarmParticleBuffer;
      uniform float4 particleTint;

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
        SwarmParticleData particle = SwarmParticleBuffer[instanceID];
        VertOut o;
        o.pos =
            mul(UNITY_MATRIX_VP,
                vertIn.vertex + 0.01 * float4(instanceID, instanceID,
                                       instanceID, 1) + float4(particle.position, 1));
        o.color = /*float4(particle.velocity, 1) * */ particleTint *
                  float4((particle.position), 1);
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
