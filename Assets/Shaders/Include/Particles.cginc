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

// -*- c -*-
// Utilities for implementing 'genius' particles.
//
// These functions help with orienting the particle quads to face the camera, and also
// the animation of the particles out from the origin.
//
// In general, a particle's quad is made camera facing in the following way:
//
// * v.corner is valid, but the quad is arbitrarily-oriented
// * v.center is the center of the quad
// * v.vid (vertex id) is used to work out which corner of the quad the vertex is.
// * The size of the quad is inferred from (v.corner - v.center).length
// * One of the _OrientParticle variants is called
// * This is done so a "naive" geometry export still contains usable vert data
//
// Some particles also 'spread' from an origin. The spreading works as follows:
//
// * The particle vertices are stored in their final, 'resting' position.
// * v.origin is the birth position of the particle
// * The time of particle spawn is passed through in a spare texcoord channel.
// * The particle position is an exponential decay from v.origin to v.center
//
// Also note that there is a special case for shrinking the particles, as this is needed for the
// preview brush. In the case of the preview brush, the time is sent through in negative,
// and this is used as a signal to shrink the particle over time.

static const float kRecipSquareRootOfTwo = 0.70710678;

// Value of the preview lifetime *must* be greater than zero.
uniform float _GeniusParticlePreviewLifetime;

// NOTOOLKIT {
struct ParticleVertexWithSpread_t {
  uint vid : SV_VertexID;
  float4 corner : POSITION;     // pos: corner of randomly-oriented quad
  float3 center : NORMAL;       // pos: center of randomly-oriented quad
  fixed4 color : COLOR;
  float4 texcoord : TEXCOORD0;  // xy: texcoord   z: rotation   w: birth time
  float3 origin : TEXCOORD1;    // pos: location of the knot (particle "sprays" from this)
};
// } NOTOOLKIT
// TOOLKIT: #define ParticleVertexWithSpread_t ParticleVertex_t

struct ParticleVertex_t {
  uint vid : SV_VertexID;
  float4 corner : POSITION;     // pos: corner of randomly-oriented quad
  float3 center : NORMAL;       // NOTOOLKIT pos: center of randomly-oriented quad
  // TOOLKIT: float3 center : TEXCOORD1;
  fixed4 color : COLOR;
  float4 texcoord : TEXCOORD0;  // xy: texcoord   z: rotation   w: birth time
};

// Rotates the corner of a square quad centered at the origin
//   up, rt   - quad's basis axes; unit-length
//   center   - quad center
//   halfSize - ie, distance from center to edge
//   corner   - a number in [0, 3]; specifies which corner to rotate
//   rotation - in radians; rotates counterclockwise in the plane of the quad
//
// quad-space coordinate system
//
//           ^ y axis/up
//           |
//       2 . . . 3
//       .   |   .         x axis/right
//       . - o <--origin   --->
//       .   |   .
//       0 . . . 1
//
float3 _RotatedQuadCorner(float3 up, float3 rt, float3 center,
                          float halfSize, int corner, float rotation) {
  // The corner's position in the (2D) quad coordinate system
  float2 pos = halfSize * float2(
      float(corner == 1 || corner == 3) * 2 - 1,  // +1 for 1 and 3,  -1 for 0 and 2
      float(corner == 2 || corner == 3) * 2 - 1   // +1 for 2 and 3,  -1 for 0 and 1
  );

  // Perform rotation in quad coordinate system
  float c = cos(rotation);
  float s = sin(rotation);
  float2x2 mRotation = float2x2(c, -s,  s, c);
  float2 rotatedPos = mul(mRotation, pos);

  // Change-of-basis to 3D coordinate system.
  // gles requires square arrays, so do it homogeneous-style.
  return mul(float3(rotatedPos, 1), float3x3(rt, up,  center));
}

// Returns the position of a camera-oriented quad corner.
//
//   center   - object-space center of quad
//   halfSize - distance from center to an edge
//   corner   - A non-negative number whose value mod 4 indicates the corner.
//              In CCW order as seen from front, bottom-left is 0.
//   rotation - in radians
//
float4 OrientParticle(float3 center, float halfSize, int corner, float rotation) {
  corner = corner & 3;
  float3 up, rt; {
    float4x4 cameraToObject = mul(unity_WorldToObject, unity_CameraToWorld);
    float3 upIsh = mul(cameraToObject, float4(0, 1, 0, 0)).xyz;
    float3 objSpaceCameraPos = mul(cameraToObject, float4(0, 0, 0, 1));
    float3 fwd = (center - objSpaceCameraPos);
    rt = normalize(cross(upIsh, fwd));
    up = normalize(cross(fwd, rt));
  }

  return float4(_RotatedQuadCorner(up, rt, center, halfSize, corner, rotation).xyz, 1);
}

// Like OrientParticle, but takes the center in WS
// Slightly fewer matrix multiplies than the other version,
// but susceptible to FP inaccuracy when far away from the origin
float4 OrientParticle_WS(float3 center_WS, float halfSize_OS, int corner, float rotation) {
  corner = corner & 3;
  float3 up_WS, rt_WS; {
    // Trying to write this without using unity_CameraToWorld because some renderers
    // don't keep around the inverse view matrix. upIsh_WS won't be unit-length, but that's fine.
    float3 upIsh_WS = UNITY_MATRIX_V[1].xyz;
    float3 cameraPos_WS = _WorldSpaceCameraPos;
    float3 fwd_WS = (center_WS - cameraPos_WS);
    rt_WS = normalize(cross(upIsh_WS, fwd_WS));
    up_WS = normalize(cross(fwd_WS, rt_WS));
  }

  float halfSize_WS = halfSize_OS * length(unity_ObjectToWorld[0].xyz);
  return float4(_RotatedQuadCorner(up_WS, rt_WS, center_WS, halfSize_WS, corner, rotation), 1);
}

// Sign bit of time is used to determine if this is a preview brush or not.
// Unpack that into a positive time value, and a size adjustment.
float _ParticleUnpackTime(inout float time) {
  float sizeAdjust;
  if (time < 0) {
    time = -time;
    float life01 = clamp((_Time.y - time) / _GeniusParticlePreviewLifetime, 0, 1);
    sizeAdjust = 1 - (life01 * life01);
  } else {
    sizeAdjust = 1;
  }
  return sizeAdjust;
}

// Works out the size of a particle from its corner and center positions,
// and the amount of time since it was spawned.
//
//   corner     - Object-space position of this corner; only used to compute particle size
//   center     - Object-space position of the center after fully-born
//   birthTime  - Particle birth time; sign bit indicates preview-ness
//
float GetParticleHalfSize(float3 corner, float3 center, float birthTime) {
  float sizeAdjust = _ParticleUnpackTime(/* inout */ birthTime);
  float halfSize = length(corner - center) * kRecipSquareRootOfTwo * sizeAdjust;
  return halfSize;
}

// Determines how far a particle halfSize moved from its origin to its resting position.
// Result its between 0 .. 1
//
//   birthTime  - Particle birth time; sign bit indicates preview-ness
//   spreadRate - How fast quad moves from origin to center. Units of periods-per-second,
//                where one period is about 63% (ie, a decay to 1/e)
//
float SpreadProgress(float birthTime, float spreadRate) {
// NOTOOLKIT {
  float age = max(0, abs(_Time.y) - abs(birthTime));
  return 1 - exp(-spreadRate * age);
// } NOTOOLKIT
// TOOLKIT:   return 1;
}

// Animates a particle position from its origin to its resting position
//
//   center     - Object-space position of the center after fully-born
//   origin     - Object-space position of center at birth
//   progress   - How far between the origin and center to place the particle
//
float4 SpreadParticle(ParticleVertexWithSpread_t particle, float progress) {
// NOTOOLKIT {
  return float4(lerp(particle.origin, particle.center, progress).xyz, 1);
// } NOTOOLKIT
// TOOLKIT:   return float4(particle.center.xyz, 1);
}
