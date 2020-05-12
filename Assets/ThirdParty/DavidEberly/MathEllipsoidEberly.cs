// CC-BY license; see LICENSE.txt
// Copyright 2013, 2019 David Eberly
//
// Adapted from https://www.geometrictools.com/Documentation/DistancePointEllipseEllipsoid.pdf

using System;
using UnityEngine;

using static TiltBrush.ComponentwiseVectors;

namespace TiltBrush {
public static class MathEllipsoidEberly {
  /// Pass:
  ///   abc - coefficients of the ellipse equation; aka the half-extents
  ///   p - point in ellipse coordinates
  public static Vector3 ClosestPointEllipsoid(
      Vector3 abc, Vector3 point, int numIters=20) {
    // No negative constants; result is the same by symmetry
    abc = new Vector3(Mathf.Abs(abc.x), Mathf.Abs(abc.y), Mathf.Abs(abc.z));

    // DistancePointEllipsoid is fussy and requires that abc be in sorted descending order,
    // and that point be in the all-positive octant.
    Matrix4x4 shuffle = CreateShuffleMatrix(abc);
    Matrix4x4 flip = CreateFlipMatrix(point);
    Vector3 sortedAbc = shuffle * abc;
    Vector3 positiveOctantPoint = shuffle * (flip * point);

    var (d, x0, x1, x2) = DistancePointEllipsoid(
        sortedAbc.x, sortedAbc.y, sortedAbc.z,
        positiveOctantPoint.x, positiveOctantPoint.y, positiveOctantPoint.z,
        numIters);

    // Undo the shuffles and flips
    Vector3 wrongOctantShuffled = new Vector3((float)x0, (float)x1, (float)x2);
    // flip.inverse == flip, and shuffle.inverse == shuffle.transpose
    Vector3 result = flip * (shuffle.transpose * wrongOctantShuffled);
    return result;
  }

  // Returns a matrix which puts unsorted into sorted order (descending)
  static Matrix4x4 CreateShuffleMatrix(Vector3 unsorted) {
    var shuffle = Matrix4x4.identity;
    var greatest = new Vector4(1, 0, 0, 0);
    var middle = new Vector4(0, 1, 0, 0);
    var least = new Vector4(0, 0, 1, 0);
    if (unsorted.x > unsorted.y && unsorted.x > unsorted.z) {
      shuffle.SetColumn(0, greatest); // x (0) -> greatest
      if (unsorted.y > unsorted.z) {
        shuffle.SetColumn(1, middle); // y (1) -> middle
        shuffle.SetColumn(2, least ); // z (2) -> least
      } else {
        shuffle.SetColumn(2, middle); // z (2) -> middle
        shuffle.SetColumn(1, least ); // y (1) -> least
      }
    } else if (unsorted.y > unsorted.z) {
      shuffle.SetColumn(1, greatest); // y (1) -> greatest
      if (unsorted.x > unsorted.z) {
        shuffle.SetColumn(0, middle); // x (0) -> middle
        shuffle.SetColumn(2, least ); // z (2) -> least
      } else {
        shuffle.SetColumn(2, middle); // z (2) -> middle
        shuffle.SetColumn(0, least ); // x (0) -> least
      }
    } else {
      shuffle.SetColumn(2, greatest); // z (2) -> greatest
      if (unsorted.x > unsorted.y) {
        shuffle.SetColumn(0, middle); // x (0) -> middle
        shuffle.SetColumn(1, least ); // y (1) -> least
      } else {
        shuffle.SetColumn(1, middle); // y (1) -> middle
        shuffle.SetColumn(0, least ); // x (0) -> least
      }
    }
    return shuffle;
  }

  // Returns a matrix which mirrors axes to put maybeNegative into the all-positive octant.
  static Matrix4x4 CreateFlipMatrix(Vector3 maybeNegative) {
    var flip = Matrix4x4.identity;
    for (int i = 0; i < 3; ++i) {
      if (maybeNegative[i] < 0) { flip[i, i] = -1; }
    }
    return flip;
  }

  static double Sqr(double x) => x * x;

  static double MaxAbs(double v0, double v1, double v2) {
    v0 = Math.Abs(v0);
    v1 = Math.Abs(v1);
    v2 = Math.Abs(v2);
    return Math.Max(Math.Max(v0, v1), v2);
  }

  static double RobustLength(double v0, double v1) {
    return RobustLength(0, v0, v1);
  }

  static double RobustLength(double v0, double v1, double v2) {
    double max = MaxAbs(v0, v1, v2);
    if (max > 0) {
      return max * Math.Sqrt(Sqr(v0 / max) + Sqr(v1 / max) + Sqr(v2 / max));
    } else {
      return 0;
    }
  }

  static double GetRoot(double r0, double z0, double z1, double g, int numIters) {
    double n0 = r0 * z0;
    double s0 = z1 - 1, s1 = (g < 0 ? 0 : RobustLength(n0, z1) - 1);
    double s = 0;
    for (int i = 0; i < numIters; ++i) {
      s = (s0 + s1) / 2;
      if (s == s0 || s == s1) {
        break;
      }

      double ratio0 = n0 / (s + r0), ratio1 = z1 / (s + 1);
      g = Sqr(ratio0) + Sqr(ratio1) - 1;
      if (g > 0) {
        s0 = s;
      } else if (g < 0) {
        s1 = s;
      } else {
        break;
      }
    }

    return s;
  }

  static (double dist, double x0, double x1) DistancePointEllipse(
      double e0, double e1, double y0, double y1, int numIters) {
    double x0, x1;
    double distance;
    if (y1 > 0) {
      if (y0 > 0) {
        double z0 = y0 / e0, z1 = y1 / e1, g = Sqr(z0) + Sqr(z1) - 1;
        if (g != 0) {
          double r0 = Sqr(e0 / e1), sbar = GetRoot(r0, z0, z1, g, numIters);
          x0 = r0 * y0 / (sbar + r0);
          x1 = y1 / (sbar + 1);
          distance = Math.Sqrt(Sqr(x0 - y0) + Sqr(x1 - y1));
        } else {
          x0 = y0;
          x1 = y1;
          distance = 0;
        }
      } else // y0 == 0
      {
        x0 = 0;
        x1 = e1;
        distance = Math.Abs(y1 - e1);
      }
    } else // y1 == 0
    {
      double numer0 = e0 * y0, denom0 = Sqr(e0) - Sqr(e1);
      if (numer0 < denom0) {
        double xde0 = numer0 / denom0;
        x0 = e0 * xde0;
        x1 = e1 * Math.Sqrt(1 - xde0 * xde0);
        distance = Math.Sqrt(Sqr(x0 - y0) + Sqr(x1));
      } else {
        x0 = e0;
        x1 = 0;
        distance = Math.Abs(y0 - e0);
      }
    }

    return (distance, x0, x1);
  }


  static double GetRoot(double r0, double r1, double z0, double z1, double z2, double g,
                        int numIters) {
    double n0 = r0 * z0;
    double n1 = r1 * z1;
    double s0 = z2 - 1;
    double s1 = (g < 0 ? 0 : RobustLength(n0, n1, z2) - 1);
    double s = 0;
    for (int i = 0; i < numIters; ++i) {
      s = (s0 + s1) / 2;
      if (s == s0 || s == s1) {
        break;
      }

      double ratio0 = n0 / (s + r0);
      double ratio1 = n1 / (s + r1);
      double ratio2 = z2 / (s + 1);
      g = Sqr(ratio0) + Sqr(ratio1) + Sqr(ratio2) - 1;
      if (g > 0) {
        s0 = s;
      } else if (g < 0) {
        s1 = s;
      } else {
        break;
      }
    }

    return s;
  }

  static (double distance, double cp0, double cp1, double cp2) DistancePointEllipsoid(
      double e0, double e1, double e2, double y0, double y1, double y2, int numIters) {
    if (e0 < e1 || e1 < e2 || e2 < 0) {
      throw new ArgumentException("Ellipse constants must be non-negative and in decreasing order");
    }
    if (y0 < 0 || y1 < 0 || y2 < 0) {
      throw new ArgumentException("Point must be in the first octant");
    }

    if (y2 > 0) {
      if (y1 > 0) {
        if (y0 > 0) {  // y0 > 0, y1 > 0, y2 > 0
          var z0 = y0 / e0;
          var z1 = y1 / e1;
          var z2 = y2 / e2;
          var g = Sqr(z0) + Sqr(z1) + Sqr(z2) - 1;
          if (g != 0) {
            var r0 = Sqr(e0 / e2);
            var r1 = Sqr(e1 / e2);
            var sbar = GetRoot(r0, r1, z0, z1, z2, g, numIters);
            var x0 = r0 * y0 / (sbar + r0);
            var x1 = r1 * y1 / (sbar + r1 );
            var x2 = y2 / (sbar + 1);
            var distance = Math.Sqrt(Sqr(x0 - y0) + Sqr(x1 - y1) + Sqr(x2 - y2));
            return (distance, x0, x1, x2);
          } else {
            return (0, y0, y1, y2);
          }
        } else {       // y0 = 0, y1 > 0, y2 > 0
          var (distance, x1, x2) = DistancePointEllipse(e1, e2, y1, y2, numIters);
          return (distance, 0, x1, x2);
        }
      } else {         //         y1 = 0, y2 > 0
        if (y0 > 0) {  // y0 > 0, y1 = 0, y2 > 0
          var (distance, x0, x2) = DistancePointEllipse(e0, e2, y0, y2, numIters);
          return (distance, x0, 0, x2);
        } else {       // y0 = 0, y1 = 0, y2 > 0
          var distance = Math.Abs(y2 - e2);
          return (distance, 0, 0, e2);
        }
      }
    } else {  // y2 == 0
      var denom0 = e0 * 0 - e2 * e2;
      var denom1 = e1 * e1 - e2 * e2;
      var numer0 = e0 * y0;
      var numer1 = e1 * y1;
      if (numer0 < denom0 && numer1 < denom1) {
        var xde0 = numer0 / denom0;
        var xde1 = numer1 / denom1;
        var xde0sqr = xde0 * xde0;
        var xde1sqr = xde1 * xde1;
        var discr = 1 - xde0sqr - xde1sqr;
        if (discr > 0) {
          var x0 = e0 * xde0;
          var x1 = e1 * xde1;
          var x2 = e2 * Math.Sqrt(discr);
          var distance = Math.Sqrt((x0 - y0) * (x0 - y0) + (x1 - y1) * (x1 - y1) + x2 * x2);
          return (distance, x0, x1, x2);
        }
      }

      {
        var (distance, x0, x1) = DistancePointEllipse(e0, e1, y0, y1, numIters);
        return (distance, x0, x1, 0);
      }
    }
  }
}
}
