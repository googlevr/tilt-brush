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

// Requires a global constant "float dispAmount"
// TODO: turn it into a parameter!

// ---------------------------------------------------------------------------------------------- //
// Tangent-less normal maps (derivative maps)
// ---------------------------------------------------------------------------------------------- //
#ifndef GL_OES_standard_derivatives
vec3 PerturbNormal(vec3 position, vec3 normal, vec2 uv) {
	return normal;
}
#else
uniform sampler2D u_BumpMap;
uniform vec4 u_BumpMap_TexelSize;

// HACK: Workaround for GPUs which struggle with vec3/vec2 derivatives.
vec3 xxx_dFdx3(vec3 v) {
  return vec3(dFdx(v.x), dFdx(v.y), dFdx(v.z));
}
vec3 xxx_dFdy3(vec3 v) {
  return vec3(dFdy(v.x), dFdy(v.y), dFdy(v.z));
}
vec2 xxx_dFdx2(vec2 v) {
  return vec2(dFdx(v.x), dFdx(v.y));
}
vec2 xxx_dFdy2(vec2 v) {
  return vec2(dFdy(v.x), dFdy(v.y));
}
// </HACK>

vec3 PerturbNormal(vec3 position, vec3 normal, vec2 uv)
{
  // Bump Mapping Unparametrized Surfaces on the GPU
  // by Morten S. Mikkelsen
  // https://goo.gl/O3JiVq

  highp vec3 vSigmaS = xxx_dFdx3(position);
  highp vec3 vSigmaT = xxx_dFdy3(position);
  highp vec3 vN = normal;
  highp vec3 vR1 = cross(vSigmaT, vN);
  highp vec3 vR2 = cross(vN, vSigmaS);
  float fDet = dot(vSigmaS, vR1);

  vec2 texDx = xxx_dFdx2(uv);
  vec2 texDy = xxx_dFdy2(uv);

  float resolution = max(u_BumpMap_TexelSize.z, u_BumpMap_TexelSize.w);
  highp float d = min(1., (0.5 / resolution) / max(length(texDx), length(texDy)));

  vec2 STll = uv;
  vec2 STlr = uv + d * texDx;
  vec2 STul = uv + d * texDy;

  highp float Hll = texture2D(u_BumpMap, STll).x;
  highp float Hlr = texture2D(u_BumpMap, STlr).x;
  highp float Hul = texture2D(u_BumpMap, STul).x;

  Hll = mix(Hll, 1. - Hll, float(!gl_FrontFacing)) * dispAmount;
  Hlr = mix(Hlr, 1. - Hlr, float(!gl_FrontFacing)) * dispAmount;
  Hul = mix(Hul, 1. - Hul, float(!gl_FrontFacing)) * dispAmount;

  highp float dBs = (Hlr - Hll) / d;
  highp float dBt = (Hul - Hll) / d;

  highp vec3 vSurfGrad = sign(fDet) * (dBs * vR1 + dBt * vR2);
  return normalize(abs(fDet) * vN - vSurfGrad);
}
#endif
