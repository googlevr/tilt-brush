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
using System.Linq;
using UnityEngine;

namespace TiltBrush {
  /// Class containing statistical helper utilities
  public static class Statistics {

    /// Class that calculates a set of statistical information about an array of floats passed
    /// through at construction.
    public class Summary {
      public float Min { get; private set; }
      public float Max { get; private set; }
      public float Median { get; private set; }
      public float Mean { get; private set; }
      public float InterquartileMean { get; private set; }
      public float StandardDeviation { get; private set; }
      public float StandardDeviationPcOfMedian { get; private set; }
      public float StandardDeviationPcOfMean { get; private set; }
      public float StandardDeviationPcOfInterquartileMean { get; private set; }

      public Summary(float[] data) {
        float[] sortedData = new float[data.Length];
        data.CopyTo(sortedData, 0);
        Array.Sort(sortedData);
        Min = sortedData.First();
        Max = sortedData.Last();
        int midIndex1 = sortedData.Length / 2;
        int midIndex2 = (sortedData.Length - 1) / 2;
        Median = (sortedData[midIndex1] + sortedData[midIndex2]) / 2f;
        Mean = sortedData.Average();
        InterquartileMean =
            sortedData.Skip(sortedData.Length / 4).Take(sortedData.Length / 2).Average();
        StandardDeviation = Mathf.Sqrt(
          data.Select(x => Mathf.Pow(x - Median, 2f)).Sum() / (sortedData.Length - 1));
        StandardDeviationPcOfMedian = 100f * StandardDeviation / Median;
        StandardDeviationPcOfMean = 100f * StandardDeviation / Mean;
        StandardDeviationPcOfInterquartileMean = 100f * StandardDeviation / InterquartileMean;
      }
    }
  }
} // namespace TiltBrush
