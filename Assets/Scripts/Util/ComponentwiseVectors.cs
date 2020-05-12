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

using System;
using UnityEngine;

namespace TiltBrush {

/// Contains structs which are useful for translating glsl code to C#.
/// Unless otherwise stated, all operators act elementwise.
public static class ComponentwiseVectors {
  public struct vec3 {
    public static explicit operator Vector3(vec3 v) =>
        new Vector3((float)v.x, (float)v.y, (float)v.z);

    public static vec3 operator *(vec3 a, vec3 b) => new vec3(a.x * b.x, a.y * b.y, a.z * b.z);
    public static vec3 operator *(vec3 a, double s) => new vec3(a.x * s, a.y * s, a.z * s);
    public static vec3 operator /(vec3 a, vec3 b) => new vec3(a.x / b.x, a.y / b.y, a.z / b.z);
    public static vec3 operator +(vec3 a, vec3 b) => new vec3(a.x + b.x, a.y + b.y, a.z + b.z);
    public static vec3 operator -(vec3 a, vec3 b) => new vec3(a.x - b.x, a.y - b.y, a.z - b.z);

    public static vec3 abs(vec3 a) => new vec3(Math.Abs(a.x), Math.Abs(a.y), Math.Abs(a.z));
    public static double sum(vec3 a) => a.x + a.y + a.z;
    public static double dot(vec3 a, vec3 b) => sum(a * b);

    public static vec3 normalized(vec3 a) {
      double len = Math.Sqrt(a.x*a.x + a.y*a.y + a.z*a.z);
      return new vec3(a.x / len, a.y / len, a.z / len);
    }

    public static double dist2(vec3 a, vec3 b) {
      vec3 delta = b - a;
      return dot(delta, delta);
    }


    public double x, y, z;

    public vec3(double x, double y, double z) {
      this.x = x; this.y =  y; this.z = z;
    }

    public vec3(Vector3 v) {
      this.x = v.x; this.y = v.y; this.z = v.z;
    }

    public vec3 sign() => new vec3(Math.Sign(x), Math.Sign(y), Math.Sign(z));

    public double mag2() => sum(this*this);
  }
}  // namespace ComponentwiseVectors

}  // namespace TiltBrush
