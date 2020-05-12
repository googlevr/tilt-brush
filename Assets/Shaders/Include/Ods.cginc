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

// Support for Omni-Directional Stereo (ODS) rendering
//
// Note that the calling shader must have the following pragma defined:
// #pragma multi_compile __ ODS_RENDER ODS_RENDER_CM

uniform float4 ODS_EyeOffset;
uniform float4 ODS_CameraPos;
uniform float ODS_PoleCollapseAmount;

float collapseIpd(float3 camOffset)
{
  const float PI = 3.14159265359;
  const float PI_2 = PI / 2;

  float3 vcam = float3(camOffset.x, 0, camOffset.z);
  float d = dot(normalize(camOffset), normalize(vcam));
  float ang = acos(clamp(d, -1, 1));

  const float minAng = 0.0;
  const float maxAng = PI_2 * 0.8;

  float t = clamp((ang - minAng) / (maxAng - minAng), 0.0, 1.0);

  // Create a continuous falloff for IPD attenuation at the poles.
  return sin(t / (2.0 * PI)) * ODS_PoleCollapseAmount;
}

void PrepForOdsWorldSpace_CM(inout float4 vertex)
{
#ifdef ODS_RENDER_CM
  float3 worldUp = float3(0.0, 1.0, 0.0);
  float3 camOffset = vertex.xyz - _WorldSpaceCameraPos.xyz;

  //Direction
  float4 D = float4(camOffset.xyz, dot(camOffset.xyz, camOffset.xyz));
  if (dot(D.xz,D.xz) < 0.00001) return;
  D *= rsqrt(D.w);

  //Tangent (note this is not the sphere tangent)
  float3 T = normalize(cross(D.xyz, worldUp.xyz));

  //reduce the IPD towards the poles (Tilt Brush specific)
  float t = collapseIpd(camOffset);
  float ipd = lerp(ODS_EyeOffset.x, 0.0, t);

  float a = ipd * ipd / D.w;
  float b = ipd / D.w * sqrt(D.w*D.w - ipd*ipd);

  float3 offset = -a*D + b*T;
  //odsRayDir = normalize( v+offset )
  //odsOrigin = cameraPos - offset

  vertex.xyz = vertex.xyz + offset;
#endif
}

void PrepForOdsWorldSpace(inout float4 vertex)
{
#if defined(ODS_RENDER_CM)
  PrepForOdsWorldSpace_CM(vertex);
#elif defined(ODS_RENDER)
  float3 vcamVert = vertex - ODS_CameraPos;
    if (dot(vcamVert.xz, vcamVert.xz) < 0.00001) return;
  float t = collapseIpd(vcamVert);

  vertex.xyz += lerp(float3(0, 0, 0), ODS_EyeOffset.xyz, t);
#endif
}

void PrepForOds(inout float4 vertex)
{
  vertex = mul(unity_ObjectToWorld, vertex);
  PrepForOdsWorldSpace(vertex);
  vertex = mul(unity_WorldToObject, vertex);
}
