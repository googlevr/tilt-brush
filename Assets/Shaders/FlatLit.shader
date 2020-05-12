// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// Shader calculates normals per triangle using a geometry shader.
// Uses Blinn-Phong lighting model for the main directional light and SH
// for all additional lighting.
Shader "Brush/FlatLit" {

Properties {
  _MainTex("Texture", 2D) = "white" {}
  _Smoothness("Smoothness", Range(0, 1)) = 0.5
  _Metallic("Metallic", Range(0, 1)) = 0
}

SubShader {
  Pass {
    Tags { "LightMode" = "ForwardBase" }
    Blend SrcAlpha OneMinusSrcAlpha

    CGPROGRAM

    #pragma vertex vert
    #pragma geometry geom
    #pragma fragment frag
    #pragma multi_compile _ SHADOWS_SCREEN
    #pragma target 4.0

    #include "AutoLight.cginc"
    #include "UnityPBSLighting.cginc"

    float _Smoothness;
    float _Metallic;
    sampler2D _MainTex;
    float4 _MainTex_ST;

    struct appdata {
      float4 vertex : POSITION;
      float2 uv : TEXCOORD0;
      float4 color : Color;
    };

    struct v2f {
      float4 pos : SV_POSITION;
      float2 uv : TEXCOORD0;
      float3 normal : TEXCOORD1;
      float3 worldPos : TEXCOORD2;
      float4 color : TEXCOORD3;
      SHADOW_COORDS(5)
    };

    v2f vert(appdata v) {
      v2f o;
      o.uv = v.uv  * _MainTex_ST.xy + _MainTex_ST.zw;
      o.pos = UnityObjectToClipPos(v.vertex);
      o.worldPos = mul(unity_ObjectToWorld, v.vertex);
      o.color = v.color;
      TRANSFER_SHADOW(o);

      // normal is set in geom method

      return o;
    }

    // Called once per triangle primitive, values outputted to triangle's
    // pixels' frag methods.
    [maxvertexcount(3)]
    void geom(triangle v2f i[3], inout TriangleStream<v2f> stream) {
      float3 p0 = i[0].worldPos.xyz;
      float3 p1 = i[1].worldPos.xyz;
      float3 p2 = i[2].worldPos.xyz;

      float3 v0 = p1 - p0;
      float3 v1 = p2 - p0;

      float3 triangleNormal = normalize(cross(v0, v1));

      i[0].normal = triangleNormal;
      i[1].normal = triangleNormal;
      i[2].normal = triangleNormal;

      stream.Append(i[0]);
      stream.Append(i[1]);
      stream.Append(i[2]);
    }

    float4 frag(v2f i) : SV_TARGET {
      // Apply shadows
      UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
      float3 lightColor = _LightColor0.rgb * attenuation;

      // Add main directional light's effect.

      // Calculate vectors to be used in lighting model.
      float3 normal = i.normal;
      float3 lightDir = _WorldSpaceLightPos0.xyz;
      float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
      float3 halfDir = normalize(lightDir + viewDir);
      float nDotl = DotClamped(normal, normalize(lightDir));

      float3 albedo = tex2D(_MainTex, i.uv).rgb * (1 - _Metallic);
      // This is an oversimplification, even pure dielectrics can have some specular
      // reflection, but its good enough for this purpose (and can be toggled in inspector).
      float3 specularTint = albedo * (_Metallic);

      // Blinn-Phong model
      float3 diffuse = albedo * lightColor * nDotl;
      float3 specular = specularTint * lightColor *
                        pow(DotClamped(halfDir, i.normal), _Smoothness * 100);
      float3 lighting = diffuse + specular;

      // Add all other lights in scene.

      // Reduce this component to minimize double counting of main directional light.
      lighting += float3(ShadeSH9(half4(normal, 1.0))) * 0.5;

      return float4(lighting * i.color.rgb, i.color.a);
    }

    ENDCG
  }

  // Cast shadows
  Pass {
    Tags { "LightMode" = "ShadowCaster"}

    CGPROGRAM

    #pragma target 4.0
    #pragma vertex vert
    #pragma fragment frag

    #include "UnityCG.cginc"

    struct appdata {
      float4 position : POSITION;
    };

    float4 vert(appdata v) : SV_POSITION {
      float4 position = UnityObjectToClipPos(v.position);
      return UnityApplyLinearShadowBias(position);
    }

    half4 frag() : SV_TARGET {
      return 0;
    }

    ENDCG
  }
}
}
