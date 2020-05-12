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

// There's no longer any reason to look for salt issues, because fiddling with the salts
// breaks determism (and we're now guaranteeing deterministic load)
#if UNITY_EDITOR && false
#  define ENABLE_SALT_CHECK
#endif

using UnityEngine;
using UInt32 = System.UInt32;

namespace TiltBrush {

/// A random number generator whose methods don't mutate internal state.
/// Or put another way, most generators stream out numbers one by one,
/// like an IEnumerator<int>. This generator gives you O(1) random access
/// to any element of the stream.
///
/// The values of Random*(salt) are deterministic, given the same values
/// of Seed and salt. "salt" is named after https://en.wikipedia.org/wiki/Salt_(cryptography)
/// but you can also think of it as an index into the stream.
///
/// How to use this in brushes:
///
/// The parameter "salt" is arbitrary, but it should be:
/// - Deterministic: otherwise the stroke will generate differently when
///   it is recreated (eg, when moved or recolored)
/// - Unique: don't use the same salt multiple times; you'll get the same
///   result, and your geometry will look non-random or weirdly biased.
///
/// Generally the value should be based on the knot index; and possibly a
/// vertex offset within that knot's geometry, if knots need variable quantites
/// of random numbers. Don't base it on position (because position of a stroke
/// can change) or time (since a stroke can be generated multiple times).
public struct StatelessRng {
  private const float kTwoToNegative24 = (
      0.5f * 0.5f * 0.5f * 0.5f  *  0.5f * 0.5f * 0.5f * 0.5f *
      0.5f * 0.5f * 0.5f * 0.5f  *  0.5f * 0.5f * 0.5f * 0.5f *
      0.5f * 0.5f * 0.5f * 0.5f  *  0.5f * 0.5f * 0.5f * 0.5f);

  private const float kTwoToNegative32 = (
      0.5f * 0.5f * 0.5f * 0.5f  *  0.5f * 0.5f * 0.5f * 0.5f *
      0.5f * 0.5f * 0.5f * 0.5f  *  0.5f * 0.5f * 0.5f * 0.5f *
      0.5f * 0.5f * 0.5f * 0.5f  *  0.5f * 0.5f * 0.5f * 0.5f *
      0.5f * 0.5f * 0.5f * 0.5f  *  0.5f * 0.5f * 0.5f * 0.5f);

  // 1 minus ulp/2
  public const float kLargestFloatLessThanOne = 1f - kTwoToNegative24;

#if ENABLE_SALT_CHECK
  // For testing only
  public static System.Action<int> SaltUsed = null;
  private static bool sm_aggressiveSaltCheck = false;
  private static Dictionary<int, string> sm_used = new Dictionary<int, string>();
#endif

  // Causes rng to assert that salt values are not used more than once.
  // This is a no-op outside of the editor.
  public static void BeginSaltReuseCheck() {
#if ENABLE_SALT_CHECK
    sm_used.Clear();
    SaltUsed = i => {
      if (sm_used.TryGetValue(i, out string where)) {
        Debug.LogError($"StatelessRng: Used salt {i} twice, last time at {where}");
      } else {
        sm_used[i] = sm_aggressiveSaltCheck ? System.Environment.StackTrace : null;
      }
    };
#endif
  }

  public static void EndSaltReuseCheck() {
#if ENABLE_SALT_CHECK
    sm_used.Clear();
    SaltUsed = null;
#endif
  }

  // These constants come from https://github.com/skeeto/hash-prospector
  // This is a low-bias integer hash function.
  static UInt32 lowbias32(UInt32 x) {
    x ^= x >> 16;
    x *= (UInt32)0x7feb352d;
    x ^= x >> 15;
    x *= (UInt32)0x846ca68b;
    x ^= x >> 16;
    return x;
  }

  /// Returns a float32 in [0, 1)
  public static float UInt32ToFloat01(UInt32 a) {
    // Beware of the float rounding upwards to exactly 1.0.
    // It's incorrect to do this:
    //   int a24 = (int)(a >> 8);
    //   return ((float)a24) * kTwoToNegative24;
    // because although float32 only has 24 bits mantissa, those 24 bits
    // start from the first 1 bit. So that loses info if a has high 0 bits.
    float ret = ((float)a) * kTwoToNegative32;
    return Mathf.Min(ret, kLargestFloatLessThanOne);
  }

  private readonly int m_seed;

  public int Seed { get { return m_seed; } }

  public StatelessRng(int seed) {
    m_seed = seed;
  }

  /// Returns a number uniformly distributed in [0, 1).
  public float In01(int salt) {
#if ENABLE_SALT_CHECK
    if (SaltUsed != null) { SaltUsed(salt); }
#endif

    // We could try to mix 32 bits -> 24 bits:
    //   // shift away bottom 8 bits and drop them into the remaining high bits
    //   hash = (hash >> 8) ^ ((hash & 0xff) << 16);
    // but if the low-8 and high-8 bits are correlated then it might just increase bias.
    // So let's keep things simple and trust in Mr Hash Prospector.
    UInt32 hash = lowbias32(unchecked((UInt32)(m_seed ^ salt)));
    return UInt32ToFloat01(hash);
  }

  /// Returns a number uniformly distributed in [min, max).
  public float InRange(int salt, float min, float max) {
    float delta = (max - min);
    return min + delta * In01(salt);
  }

  /// Returns an integer uniformly distributed in [min, max).
  public int InIntRange(int salt, int min, int max) {
    int delta = (max - min);
    return min + (int)(delta * In01(salt));
  }

  /// Returns a unit Vector2 uniformly distributed over the unit circle's surface.
  public Vector2 OnUnitCircle(int salt) {
    float angle = InRange(salt, 0f, 2 * Mathf.PI);
    return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
  }

  /// Returns a Vector2 uniformly distributed over the unit circle's area.
  public Vector2 InUnitCircle(int salt) {
    float k = Mathf.Sqrt(In01(salt + 1));
    return k * OnUnitCircle(salt);
  }

  /// Returns a unit Vector3 uniformly distributed over the unit sphere's surface.
  public Vector3 OnUnitSphere(int salt) {
    // See Mathworld Sphere point picking eqs 6-8
    float u = InRange(salt, -1, 1);
    float theta = InRange(salt + 1, 0, 2 * Mathf.PI);
    float k = Mathf.Sqrt(1 - u*u);
    return new Vector3(k * Mathf.Cos(theta), k * Mathf.Sin(theta), u);
  }

  /// Returns a Vector3 uniformly distributed over the unit sphere's volume.
  public Vector3 InUnitSphere(int salt) {
    // Cube root gives us the proper distribution over the volume.
    float k = Mathf.Pow(In01(salt + 2), 1f/3f);
    return k * OnUnitSphere(salt);
  }

  /// Returns a quaternion uniformly distributed over the unit quaternions.
  /// This can return quaternions with rotations > 180.
  public Quaternion Rotation(int salt) {
    // George Marsaglia 1972 "Choosing a point from the surface of a sphere"
    // https://projecteuclid.org/download/pdf_1/euclid.aoms/1177692644
    // Except: instead of choosing v34 as a point _in_ unit disc and calculating v34/|v34|,
    // we directly calculate a point _on_ unit disc. It's easier and avoids a singularity.
    Vector2 v12 = InUnitCircle(salt);
    float s1 = v12.sqrMagnitude;
    // "salt + 2" because InUnitCircle uses 2 salt values
    Vector2 v34 = Mathf.Sqrt(Mathf.Max(1-s1, 0)) * OnUnitCircle(salt + 2);
    return new Quaternion(v12.x, v12.y, v34.x, v34.y);
  }
}
} // namespace TiltBrush
