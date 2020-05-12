Shader "Sonic Ether/Emissive/Textured" {
Properties {
	_EmissionColor ("Emission Color", Color) = (1,1,1,1)
	_DiffuseColor ("Diffuse Color", Color) = (1, 1, 1, 1)
	_MainTex ("Diffuse Texture", 2D) = "White" {}
	_Illum ("Emission Texture", 2D) = "white" {}
	_EmissionGain ("Emission Gain", Range(0, 1)) = 0.5
	_EmissionTextureContrast ("Emission Texture Contrast", Range(1, 3)) = 1.0
}

SubShader {
	Tags { "RenderType"="Opaque" }
	LOD 200
	
CGPROGRAM
#pragma surface surf Lambert

sampler2D _MainTex;
sampler2D _Illum;
fixed4 _DiffuseColor;
fixed4 _EmissionColor;
float _EmissionGain;
float _EmissionTextureContrast;

struct Input {
	float2 uv_MainTex;
	float2 uv_Illum;
};

void surf (Input IN, inout SurfaceOutput o) {
	fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
	fixed4 c = tex * _DiffuseColor;
	o.Albedo = c.rgb;
	fixed3 emissTex = tex2D(_Illum, IN.uv_Illum).rgb;
	float emissL = max(max(emissTex.r, emissTex.g), emissTex.b);
	fixed3 emissN = emissTex / (emissL + 0.0001);
	emissL = pow(emissL, _EmissionTextureContrast);
	emissTex = emissN * emissL;
	
	o.Emission = _EmissionColor * emissTex * (exp(_EmissionGain * 10.0f));
	o.Alpha = c.a;
}
ENDCG
} 
FallBack "Self-Illumin/VertexLit"
}
