﻿Shader "Custom/FragmentCounting" {
  Properties {
    //_MainTex("Texture", 2D) = "white" {}
  }
  SubShader {
    Cull Off ZWrite Off Blend One One BlendOp Add

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
      uniform int pixelCount;
      uniform int volumeResolution;

      uniform StructuredBuffer<DistanceFieldObjectMatrices>
          DistanceFieldObjectDataBuffer;

      // Material Property Block of object
      uniform int objectIndex;

      struct VertIn {
        float4 vertex : POSITION;
      };

      struct VertOut {
        float4 pos : SV_POSITION;
        uint instanceID : InstanceID;
      };

      VertOut vert(VertIn vertIn, uint instanceID : SV_InstanceID) {
        float4x4 vp =
            DistanceFieldObjectDataBuffer[objectIndex].matrices[instanceID];
        // float4x4 vp = debugTestMat[instanceID];
        VertOut o;
        o.pos = mul(vp, vertIn.vertex);

        // InterlockedAdd(FragmentCounterBuffer[1], 1);
        // o.pos = verts[instanceID];//vertIn.vertex;
        o.instanceID = instanceID;

        return o;
      }

      float4 frag(VertOut vertOut) : SV_Target {
        // vertOut.xy is Screen Position with 0.5 0ffset, so lets convert it to uint2
        uint2 pixelCoord = uint2(vertOut.pos.xy);
        uint index = vertOut.instanceID * pixelCount +
                     pixelCoord.y * volumeResolution + pixelCoord.x;

        //InterlockedAdd(FragmentCounterBuffer[vertOut.instanceID], 1);
        InterlockedAdd(FragmentCounterBuffer[index], 1);

        return float4(frac(pixelCoord.x), frac(pixelCoord.y), 0, 1);

        ///*Debug Per Instance View
        if (vertOut.instanceID == 0) {
          return float4(vertOut.pos.xy / 128.0, 0, 1);
        }
        else if (vertOut.instanceID == 1) {
          return float4(0, 0, 0, 1);
        }
        else if (vertOut.instanceID == 2) {
          return float4(0, 0, 0, 1);
        }
        else {
          return float4(-1, -1, -1, 1);
        }  //*/

        // return float4(1, 1, 1, 1);
      }
      ENDCG
    }
  }
}
