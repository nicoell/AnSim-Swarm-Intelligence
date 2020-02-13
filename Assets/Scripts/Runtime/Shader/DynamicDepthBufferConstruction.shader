Shader "Custom/DynamicDepthBufferConstruction" {
  Properties {
    //_MainTex("Texture", 2D) = "white" {}
  }
  SubShader {
    Cull Off ZWrite On Blend One One BlendOp Add

        Tags{"RenderType" = "Transparent"}

    Pass {
      CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 4.5

#include "Definitions/Structs.hlsl"
#include "UnityCG.cginc"

      // For DrawMeshInstancedIndirect register u1 is not free...
      uniform RWStructuredBuffer<uint> FragmentCounterBuffer : register(u2);
      uniform RWStructuredBuffer<float> DynamicDepthBuffer : register(u3);
      uniform int pixelCount;
      uniform int volumeResolution;

      uniform StructuredBuffer<DistanceFieldObjectMatrices> DistanceFieldObjectDataBuffer;
      uniform StructuredBuffer<uint> PrefixSumBuffer;

      // Material Property Block of object
      uniform int objectIndex;

      struct VertIn {
        float4 vertex : POSITION;
      };

      struct VertOut {
        float4 pos : SV_POSITION;
        uint instanceID : InstanceID;
        float canonicDepth : TEXCOORD0;
      };

      VertOut vert(VertIn vertIn, uint instanceID : SV_InstanceID) {
        float4x4 vp =
            DistanceFieldObjectDataBuffer[objectIndex].matrices[instanceID];

        VertOut o;
        o.pos = mul(vp, vertIn.vertex);
        o.instanceID = instanceID;
        o.canonicDepth = o.pos.z;

        return o;
      }

      float4 frag(VertOut vertOut) : SV_Target {
        // vertOut.xy is Screen Position with 0.5 0ffset, so lets convert it to
        // uint2
        uint2 pixelCoord = uint2(vertOut.pos.xy);
        uint index = vertOut.instanceID * pixelCount +
                     (pixelCoord.y * volumeResolution) + pixelCoord.x;
        
        uint dynamicIndex = 0;
        InterlockedAdd(FragmentCounterBuffer[index], 1, dynamicIndex);
        dynamicIndex += PrefixSumBuffer[index - 1];

        DynamicDepthBuffer[dynamicIndex] = vertOut.canonicDepth;

        /*Debug Per Instance View
        if (vertOut.instanceID == 0) {
          return float4(vertOut.canonicDepth, 0, 0, 1);
        } else if (vertOut.instanceID == 1) {
          return float4(0, vertOut.canonicDepth, 0, 1);
        } else if (vertOut.instanceID == 2) {
          return float4(0, 0, vertOut.canonicDepth, 1);
        }*/

        return float4(1, 1, 1, 1);
      }
      ENDCG
    }
  }
}
