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

using UnityEngine;
using ControlPoint = TiltBrush.PointerManager.ControlPoint;

namespace TiltBrush {
  /// Simplifies a set of control points using the Ramer-Douglas-Peucker alogorithm.
  /// (See http://karthaus.nl/rdp/ )
  public class RdpStrokeSimplifier {

    public float Level { get; private set; }

    // If start and end points of a line segment are closer than this, they are counted as
    // coincident.
    private const float kEpsilon = 0.00001f;

    public RdpStrokeSimplifier(float level) {
      Level = level;
    }

    public void CalculatePointsToDrop(Stroke stroke, BaseBrushScript brushScript) {
      if (!brushScript.Descriptor.m_SupportsSimplification) {
        return;
      }
      float sqrMaxError = Mathf.Pow(stroke.m_BrushScale * Level * 0.001f, 2f);
      if (stroke.m_ControlPoints.Length >= 4) {
        FlagPointsToDrop(stroke.m_ControlPoints, stroke.m_ControlPointsToDrop, 0,
          stroke.m_ControlPoints.Length - 1, sqrMaxError);
        FlagPointsToKeep(stroke, stroke.m_ControlPointsToDrop, brushScript);
      }
    }

    /// Work out what simplification level is needed to get a specified level of reduction.
    public static float CalculateLevelForReduction(float reduction) {
      return Mathf.Pow(2f, 10f * (0.9f - reduction));
    }

    private void FlagPointsToKeep(Stroke stroke, bool[] toDrop,
                                  BaseBrushScript brushScript) {
      if (stroke.m_ControlPoints.Length < 2) { return; }
      Vector3 lastDiff = stroke.m_ControlPoints[1].m_Pos - stroke.m_ControlPoints[0].m_Pos;
      for (int i = 1; i < stroke.m_ControlPoints.Length - 2; ++i) {
        Vector3 nextDiff = stroke.m_ControlPoints[i + 1].m_Pos - stroke.m_ControlPoints[i].m_Pos;
        if (Vector3.Dot(lastDiff, nextDiff) < 0) {
          SavePointSequence(stroke, toDrop, i, -1, brushScript);
          SavePointSequence(stroke, toDrop, i, 1, brushScript);
        }
        lastDiff = nextDiff;
      }

      if (brushScript.Descriptor.m_MiddlePointStep != 0) {
        for (int i = 0; i < stroke.m_ControlPointsToDrop.Length;
             i += brushScript.Descriptor.m_MiddlePointStep) {
          stroke.m_ControlPointsToDrop[i] = false;
        }
      }

      SavePointSequence(stroke, toDrop, 0, 1, brushScript);
      SavePointSequence(stroke, toDrop, stroke.m_ControlPoints.Length - 1, -1, brushScript);
    }

    /// Will flag a control point to keep, and a control point in the specified direction that is
    /// greater than the 'spawn interval' away.
    private void SavePointSequence(Stroke stroke, bool[] toDrop, int point,
                                   int dir, BaseBrushScript brushScript) {
      int count = (dir == 1) ? brushScript.Descriptor.m_HeadMinPoints
                             : brushScript.Descriptor.m_TailMinPoints;
      int step = (dir == 1)
        ? brushScript.Descriptor.m_HeadPointStep
        : brushScript.Descriptor.m_TailPointStep;
      int lastPoint = point;
      toDrop[point] = false;
      float spawnInterval = brushScript.GetSpawnInterval(stroke.m_ControlPoints[point].m_Pressure);
      float sqrMinDist = spawnInterval * spawnInterval;
      for (int i = point + dir; i < stroke.m_ControlPoints.Length && i >= 0; i += dir) {
        Vector3 diff = stroke.m_ControlPoints[i].m_Pos - stroke.m_ControlPoints[lastPoint].m_Pos;
        if (count % step == 0) {
          toDrop[i] = false;
        }
        if (diff.sqrMagnitude >= sqrMinDist) {
          if (--count <= 0) {
            return;
          }
          lastPoint = i;
        }
      }
      toDrop[point + dir] = false;
    }


    /// Uses the RDP algorithm to flat control points to drop when simplifying.
    private void FlagPointsToDrop(ControlPoint[] points, bool[] toDrop, int first, int last,
                                       float sqrMaxError) {
      Vector3 start = points[first].m_Pos;
      Vector3 end = points[last].m_Pos;
      Vector3 line = end - start;
      float lineLength = line.magnitude;
      Vector3 lineDir = line.normalized;

      int farthestIndex = -1;
      float farthestDistance = 0;
      for (int i = first + 1; i < last; ++i) {
        Vector3 diff = points[i].m_Pos - start;
        float sqrDistance;

        // When checking the closest distance, we can't just take the closest point to the line,
        // as the line is infinite - we need to get the closest point to the line *segment*.
        //
        // Closest point to P of a line that goes through A and B is at C.
        //                                 P
        //                                 |
        //                                 |
        // --------A-------------B---------C----
        //
        // Closest point to P of a line sement that goes from A to B is at B.
        //                                 P
        //                             _-'
        //                         _-'
        //         A-------------B

        float segmentDistance = Vector3.Dot(diff, lineDir);
        if (segmentDistance < kEpsilon) {
          sqrDistance = diff.sqrMagnitude;
        } else if (segmentDistance > lineLength) {
          sqrDistance = (points[i].m_Pos - end).sqrMagnitude;
        } else {
          sqrDistance = Vector3.Cross(diff, lineDir).sqrMagnitude;
        }
        if (sqrDistance > sqrMaxError) {
          if (sqrDistance > farthestDistance) {
            farthestIndex = i;
            farthestDistance = sqrDistance;
          }
        }
      }
      if (farthestIndex != -1) {
        FlagPointsToDrop(points, toDrop, first, farthestIndex, sqrMaxError);
        FlagPointsToDrop(points, toDrop, farthestIndex, last, sqrMaxError);
        return;
      }

      for (int i = first + 1; i < last; ++i) {
        toDrop[i] = true;
      }

    }
  }
} // namespace TiltBrush