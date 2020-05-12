// MIT LICENSE
// Copyright 2013 Anton Kirczenow
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Adapted from https://www.shadertoy.com/view/ldsGWX

using System;
using UnityEngine;

using static TiltBrush.ComponentwiseVectors;

namespace TiltBrush {

public static class MathEllipsoidAnton {
  /// Uses 2D Newton's method using stereographic parametrization and fancy initial guess
  /// Pass:
  ///   abc - coefficients of the ellipse equation; aka the half-extents
  ///   p - point in ellipse coordinates
  public static Vector3 ClosestPointEllipsoid(
      Vector3 abc_, Vector3 p_, int numIters=30, double threshold=0) {
    // Doesn't like negative scales & result is the same by symmetry!
    vec3 abc = vec3.abs(new vec3(abc_));

    // WLOG move to the all-positive octant; move back to original octant when done.
    vec3 orig_p = new vec3(p_);
    vec3 p = vec3.abs(orig_p);

    Stereo uv = Stereo.FromCartesian(InitialGuessClosestPointToEllipsoid(p, abc), abc);
    for (int i = 0; i < numIters; ++i) {
      Stereo prevUv = uv;
      uv = Iterate_Stereographic(uv, p, abc);
      if (Math.Abs(prevUv.x - uv.x) < threshold ||
          Math.Abs(prevUv.y - uv.y) < threshold) {
        break;
      }
    }

    // Move back to correct octant
    vec3 wrongOctant = uv.ToCartesian(abc);
    vec3 rightOctant = wrongOctant * orig_p.sign();
    return (Vector3)rightOctant;
  }

  // Returns the real root which is closest to zero.
  // Does not handle these cases:
  // - complex roots
  // - a ~= 0 (ie, one root is at infinity)
  // Error cases return a large positive number
  static double QuadraticPositive(double a, double b, double c) {
    double discriminant = b * b - (4 * a * c);
    if (discriminant > 0 && Math.Abs(a) > 0.0001) {
      double sqrt_disc = Math.Sqrt(discriminant);
      double t = (-b + sqrt_disc) / (2 * a);
      double s = (-b  -sqrt_disc) / (2 * a);

      // Smaller of these will be nearer the query point for the same ray.
      return Math.Abs(t) < Math.Abs(s) ? t : s;
    }

    // Somebody else will have a nearer point and this will be rejected!
    return 1e10;
  }

  private static vec3 GuessProjectOnSphere(vec3 p, vec3 abc) {
    return vec3.normalized(p / abc) * abc;
  }

  private static vec3 GuessOrthogonal(vec3 p, vec3 abc) {
    const double inf = 1e10;
    vec3 n = p / abc;
    vec3 nsq = n * n;
    double x_over_a_sq = nsq.x;
    double y_over_b_sq = nsq.y;
    double z_over_c_sq = nsq.z;

    double x_term = y_over_b_sq + z_over_c_sq;
    double y_term = x_over_a_sq + z_over_c_sq;
    double z_term = x_over_a_sq + y_over_b_sq;

    // Eq of ellipse: (x/a)^2 + (y/b)^2 + (z/c)^2 = 1
    // Solve for x, y, z (keeping the other two constant) to find intercepts
    double FixSign(double _sign, double _val) =>  _sign > 0 ? _val : -_val;
    double N_x = x_term < 1 ? FixSign(p.x, abc.x * Math.Sqrt(1 - x_term)) : inf;
    double N_y = y_term < 1 ? FixSign(p.y, abc.y * Math.Sqrt(1 - y_term)) : inf;
    double N_z = z_term < 1 ? FixSign(p.z, abc.z * Math.Sqrt(1 - z_term)) : inf;

    double abs_N_x = Math.Abs(N_x - p.x);
    double abs_N_y = Math.Abs(N_y - p.y);
    double abs_N_z = Math.Abs(N_z - p.z);

    // Take the nearest of those
    vec3 guess = p;
    if (abs_N_x < abs_N_y && abs_N_x < abs_N_z) {
      guess.x = N_x;
    } else if (abs_N_y < abs_N_x && abs_N_y < abs_N_z) {
      guess.y = N_y;
    } else {
      guess.z = N_z;
    }
    return guess;
  }

  private static vec3 GuessDiagonal2d(vec3 p, vec3 abc) {
    // Find the nearest of the intersections in the XY XZ and YZ directions
    var x = p.x;
    var y = p.y;
    var z = p.z;

    var aa = abc.x * abc.x;
    var bb = abc.y * abc.y;
    var cc = abc.z * abc.z;

    var xx = x*x;
    var yy = y*y;
    var zz = z*z;

    // XY	t^2*(aa+bb) + t*2*(x*bb+y*aa) + aa*y_sq+bb*x_sq-(aa*bb*1)+aa*bb*z_sq/cc
    var t_xy = QuadraticPositive(aa+bb, 2*(x*bb+y*aa), aa*yy+bb*xx-(aa*bb)+aa*bb*zz/cc);
    // XZ	t^2*(aa+cc) + t*2*(z*aa+x*cc) + aa*cc*(y_sq/bb)+aa*z_sq+cc*x_sq-(aa*cc)
    var t_xz = QuadraticPositive(aa+cc,2*(z*aa+x*cc),aa*cc*(yy/bb)+aa*zz+cc*xx-(aa*cc));
    // YZ t^2*(cc+bb) + t*2*(y*cc+z*bb) + (cc*y_sq+bb*z_sq+bb*cc*x_sq/aa-(bb*cc))
    var t_yz = QuadraticPositive(cc+bb,2*(y*cc+z*bb),(cc*yy+bb*zz+bb*cc*xx/aa-(bb*cc)));

    if (Math.Abs(t_xy) < Math.Abs(t_xz) && Math.Abs(t_xy) < Math.Abs(t_yz)) {
      return new vec3(x + t_xy, y + t_xy, z);
    } else if (Math.Abs(t_xz) < Math.Abs(t_xy) && Math.Abs(t_xz) < Math.Abs(t_yz)) {
      return new vec3(x + t_xz, y, z + t_xz);
    } else {
      return new vec3(x, y + t_yz, z + t_yz);
    }
  }

  private static vec3 GuessDiagonal3d(vec3 p, vec3 abc) {
    // Shoot a ray in the XYZ direction.
    // t^2 * (    bb cc  +      aa cc  +      aa bb) +
    // t   * (2 x bb cc  +  2 y aa cc  +  2 z aa bb) +
    //       bb * cc * xx +
    //       aa * cc * yy +
    //       aa * bb * zz -
    //       (aa*bb*cc)
    var aa = abc.x * abc.x;
    var bb = abc.y * abc.y;
    var cc = abc.z * abc.z;

    var pp = p * p;
    var t_xyz = QuadraticPositive(
        bb * cc +       aa * cc +       aa * bb,
        2 * (p.x * bb * cc + p.y * aa * cc + p.z * aa * bb),
        bb * cc * pp.x  +
        aa * cc * pp.y  +
        aa * bb * pp.z  -
        (aa*bb*cc));
    return p + new vec3(t_xyz, t_xyz, t_xyz);
  }

  // Tries all the guesses above and returns the best one.
  private static vec3 InitialGuessClosestPointToEllipsoid(vec3 p, vec3 abc) {
    // Returns which of (a, b) is nearest to p
    vec3 NearerToP(vec3 _a, vec3 _b) => (vec3.dist2(_a, p) < vec3.dist2(_b, p)) ? _a : _b;

    bool fully_inside; {
      vec3 n = p / abc;
      vec3 nsq = n * n;
      fully_inside = vec3.sum(nsq) < 1;
    }

    vec3 guess = NearerToP(GuessProjectOnSphere(p, abc),
                           GuessOrthogonal(p, abc));

    // If we're inside there seems to be an extra bit kind of torus shape and aligned to the
    // principal planes where the initial guess still gives a divide by 0 in Newton's method.
    // Add some more directions to try to get rid of it!
    // But still not perfect, so do not use for air traffic control software, please thanks!
    // Improvements welcomed.
    if (fully_inside) {
      guess = NearerToP(guess, GuessDiagonal2d(p, abc));
      guess = NearerToP(guess, GuessDiagonal3d(p, abc));
    }

    return guess;
  }

  // Stereographic parametrization
  // See http://mathworld.wolfram.com/Ellipsoid.html
  public struct Stereo {
    public static Stereo FromCartesian(vec3 cartesian, vec3 abc) => new Stereo {
        x = (abc.x * cartesian.y) / (abc.y * (cartesian.x + abc.x)),
        y = (abc.x * cartesian.z) / (abc.z * (cartesian.x + abc.x))
    };
    public static Stereo operator*(double s, Stereo uv) => new Stereo { x=s * uv.x, y=s * uv.y };
    public static Stereo operator-(Stereo a, Stereo b) => new Stereo { x=a.x-b.x, y=a.y-b.y };
    public double x;
    public double y;
    public vec3 ToCartesian(vec3 abc) {
      var d = x*x + y*y;
      var ood = 1 / (1 + d);
      return new vec3(1 - d, 2*x, 2*y) * ood * abc;
    }
  }

  private static Stereo Iterate_Stereographic(Stereo uv, vec3 p, vec3 abc) {
    var a = abc.x;
    var b = abc.y;
    var c = abc.z;

    var x = p.x;
    var y = p.y;
    var z = p.z;
    var a_2 = a*a;
    {
      var u = uv.x;
      var u_2 = u*u;
      var v = uv.y;
      var v_2 = v*v;

      var U = 1.0 + u_2 + v_2;
      var ooU = 1.0 / U;
      var U_2 = U * U;
      var ooU_2 = 1.0 / U_2;

      //partial derivatives via computer algebra system, not very simplified...

      var t0 = (-16.0) * u * v    * a_2 * ooU_2;
      var t1 = -x - a + 2.0 * a * ooU;
      var t2 = y - 2.0 * b * u * ooU;
      var t3 = 4.0 * c * u * v;
      var t4 = 4.0 * b * u * v;
      var t5 = z - 2.0 * c * v * ooU;
      var t6 = -x - a + 2.0 * a * ooU;
      var t7 = 2.0 * U * c - (4.0 * c * v_2);
      var t8 = (v_2 - u_2 + 1.0) * ooU_2;
      var t9 = (u_2 - v_2 + 1.0) * ooU_2;

      var F = 4.0 * a * u * t1  +  t2 * b * (2.0 * U - (4.0 * u_2))  -  (t3 * t5);
      var G = 4.0 * a * v * t1  -  t4 * t2  +  t5 * 2.0 * c * (U - (2.0 * v_2));

      var A = 4.0 * a * (-4.0 * a * u_2 * ooU_2 + t6) +
              2.0 * b * (-(2.0 * u * t2) - (2.0 * U * b - (4.0 * b * u_2)) * t8) -
              (4.0 * c * (4.0 * c * u_2 * v_2 * ooU_2 + v * t5));
      var B = t0 + 2.0 * b * (2.0 * v * t2 + t4 * t8) - (4.0 * c * u * ((-v) * t7 * ooU_2 + t5));
      var C = t0 - (4.0 * b * ((-u) * v * (2.0 * U * b - (4.0 * b * u_2)) * ooU_2 + v * t2)) +
              2.0 * c * (2.0 * u * t5 + t3 * t9);
      var D = -(4.0 * a * (4.0 * a * v_2 * ooU_2 - t6)) -
              (4.0 * b * u * (4.0 * b * u * v_2 * ooU_2 + y - 2.0 * b * u * ooU)) +
              2.0 * c * (-(2.0 * v * t5) - t7 * t9);

      var denom = (A * D - B * C);
      var det = 1.0 / denom;

      //	D  -B  mul F
      //	-C  A      G

      uv -= det * new Stereo { x = D*F-B*G, y = A*G-C*F };
    }

    return uv;
  }
}
}
