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
using System.Collections.Generic;
using System.Linq;

using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TiltBrush {
namespace Test {

static class Stats {
  const int kBuckets = 50;
  const float kCorrelationBound = 0.05f;

#if false
  static void WriteFile(string name, IEnumerable<float> vals) {
    using (var writer = new System.IO.StreamWriter(@"C:\src\tb\logs\" + name)) {
      foreach (var val in vals) {
        writer.WriteLine("{0}", val);
      }
    }
  }
#endif

  /// Returns a value in [0, 1] that says how v.x is correlated with v.y
  public static float GetCorrelation(IEnumerable<Vector2> vals) {
    // https://www.mathsisfun.com/data/correlation.html
    float x = 0, y = 0, xx = 0, yy = 0, xy = 0;  // sums
    int n = 0;
    foreach (var v in vals) {
      n += 1;
      x += v.x;
      y += v.y;
      xx += v.x * v.x;
      yy += v.y * v.y;
      xy += v.x * v.y;
    }
    return          (n * xy - x * y)
        / Mathf.Sqrt(n * xx - x * x)
        / Mathf.Sqrt(n * yy - y * y);
  }

  /// Calculates mean and centered variance.
  public static void GetMeanAndVariance(
      IEnumerable<float> vals,
      out float out_mean, out float out_variance) {
    int count = 0;
    float mean = 0, m2 = 0;
    foreach (var val in vals) {
      count += 1;
      float delta = val - mean;
      mean += delta / count;
      float delta2 = val - mean;
      m2 += delta * delta2;
    }
    out_mean = mean;
    out_variance = m2 / count;
  }

  /// Verifies that values in vals are uniformly distributed in [0, 1)
  public static void CheckUniformity(string name, IEnumerable<float> vals) {
    var valsA = vals.ToArray();

    float valueMean, valueVariance;
    GetMeanAndVariance(valsA, out valueMean, out valueVariance);

    // Create a histogram of the values
    int count = 0;
    float[] histogram; {
      histogram = Enumerable.Repeat(0f, kBuckets).ToArray();
      foreach (var val in valsA) {
        Assert.GreaterOrEqual(val, 0f, "{0} {1}", name, count);
        Assert.Less(val, 1f, "value");
        histogram[(int) (val * kBuckets)] += 1;
        count += 1;
      }
      // WriteFile(name + "_buckets.txt", histogram);
      // Normalize bucket counts so sum(buckets) == 1
      float invCount = 1f / count;
      for (int i = 0; i < histogram.Length; ++i) {
        histogram[i] *= invCount;
      }
    }

    float histMean, histVariance;
    GetMeanAndVariance(histogram, out histMean, out histVariance);

    // https://stats.stackexchange.com/users/42843/user495285
    // I don't know what it is, but it seems to work okay...
    // value chosen kind of empirically based on 10k samples
    float histUniformity; {
      float d = histogram.Length;
      float n = Mathf.Sqrt(histogram.Select(v => v*v).Sum());  // euclidean norm
      histUniformity = (n * Mathf.Sqrt(d) - 1) / (Mathf.Sqrt(d) - 1);
      Assert.Less(histUniformity, 6e-4f, "{0} histogram uniformity", name);
    }

    // If values are uniform across [0,1], mean should be almost exactly .5
    // No idea about variance, so ignore.
    Assert.That(valueMean, Is.EqualTo(.5f).Within(5).Percent, "{0} mean", name);

    // histogram variance should be very low (each bucket should have roughly the same #).
    // This number was determined empirically for 10k samples.
    float histStdev = Mathf.Sqrt(histVariance);
    float coefficientOfVariationPct = (histStdev / histMean) * 100;
    Assert.Less(coefficientOfVariationPct, 10f, "{0} hist cv%", name);

    // Debug.LogFormat("{0}: Hsd {1:E2}  Hu {2:E2}  Vm {3:E2}",
    //                 name, Mathf.Sqrt(histVariance), histUniformity, valueMean);
  }

  /// Checks that 1D sub-distributions of the passed N-D distribution are uniform,
  /// and that the 1D distributions are not correlated with each other.
  /// (Correlations are only pair-wise checked)
  public static void CheckUniformity(string[] paramNames, IEnumerable<Vector2> vals) {
    int kDims = 2;
    var valsA = vals.ToArray();
    for (int j = 0; j < kDims; ++j) {
      int index = j;  // index into the Vector; make a copy for the closure
      IEnumerable<float> subvals = valsA.Select(v => v[index]);
      CheckUniformity(paramNames[index], subvals);
    }

    var correlation = GetCorrelation(valsA);
    Assert.Less(correlation, kCorrelationBound,
                "Correlation {0} vs {1}", paramNames[0], paramNames[1]);
  }

  /// Checks that 1D sub-distributions of the passed N-D distribution are uniform,
  /// and that the 1D distributions are not correlated with each other.
  /// (Correlations are only pair-wise checked)
  public static void CheckUniformity(string[] paramNames, IEnumerable<Vector3> vals) {
    int kDims = 3;
    var valsA = vals.ToArray();
    for (int j = 0; j < kDims; ++j) {
      int index = j;  // index into the Vector; make a copy for the closure
      IEnumerable<float> subvals = valsA.Select(v => v[index]);
      CheckUniformity(paramNames[index], subvals);
    }

    // Pairwise correlations
    for (int i = 0; i < kDims; ++i) {
      int idx0 = i;
      int idx1 = (i+1) % kDims;
      var correlation = GetCorrelation(valsA.Select(v3 => new Vector2(v3[idx0], v3[idx1])));
      Assert.Less(correlation, kCorrelationBound,
                  "Correlation {0} vs {1}", paramNames[idx0], paramNames[idx1]);

    }
  }
}

}  // namespace Test
}  // namespace TiltBrush
