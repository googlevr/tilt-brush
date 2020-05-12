using UnityEngine;
using System;
using System.Collections;

public class BoundsDouble {
  private double[] min = new double[] { 0, 0, 0 };
  private double[] max = new double[] { 0, 0, 0 };
  private bool inited = false;

  public BoundsDouble() {
  }

  public BoundsDouble(BoundsDouble b) {
    for (int i = 0; i < 3; ++i) {
      min[i] = b.min[i];
      max[i] = b.max[i];
    }
    inited = true;
  }

  public BoundsDouble(double[] min, double[] max) {
    this.min[0] = min[0];
    this.min[1] = min[1];
    this.min[2] = min[2];

    this.max[0] = max[0];
    this.max[1] = max[1];
    this.max[2] = max[2];
    inited = true;
  }

  public BoundsDouble(Vector3 min, Vector3 max) {
    this.min[0] = min.x;
    this.min[1] = min.y;
    this.min[2] = min.z;

    this.max[0] = max.x;
    this.max[1] = max.y;
    this.max[2] = max.z;
    inited = true;
  }

  public void Encapsulate(BoundsDouble b) {
    if (inited) {
      min[0] = Math.Min(min[0], b.min[0]);
      min[1] = Math.Min(min[1], b.min[1]);
      min[2] = Math.Min(min[2], b.min[2]);

      max[0] = Math.Max(max[0], b.max[0]);
      max[1] = Math.Max(max[1], b.max[1]);
      max[2] = Math.Max(max[2], b.max[2]);
    } else {
      min[0] = b.min[0];
      min[1] = b.min[1];
      min[2] = b.min[2];

      max[0] = b.max[0];
      max[1] = b.max[1];
      max[2] = b.max[2];
      inited = true;
    }
  }

  public void Rotate(Matrix4x4 m) {
    if (inited) {
      double minx = m.m00 * min[0] + m.m01 * min[1] + m.m02 * min[2];
      double miny = m.m10 * min[0] + m.m11 * min[1] + m.m12 * min[2];
      double minz = m.m20 * min[0] + m.m21 * min[1] + m.m22 * min[2];

      double maxx = m.m00 * max[0] + m.m01 * max[1] + m.m02 * max[2];
      double maxy = m.m10 * max[0] + m.m11 * max[1] + m.m12 * max[2];
      double maxz = m.m20 * max[0] + m.m21 * max[1] + m.m22 * max[2];

      min[0] = Math.Min(minx, maxx);
      min[1] = Math.Min(miny, maxy);
      min[2] = Math.Min(minz, maxz);

      max[0] = Math.Max(minx, maxx);
      max[1] = Math.Max(miny, maxy);
      max[2] = Math.Max(minz, maxz);
    }
  }

  public void Translate(double x, double y, double z) {
    if (inited) {
      min[0] += x;
      min[1] += y;
      min[2] += z;

      max[0] += x;
      max[1] += y;
      max[2] += z;
    }
  }

  public bool Empty {
    get {
      return !inited;
    }
  }

  public double[] Min {
    get {
      return min;
    }
  }

  public double[] Max {
    get {
      return max;
    }
  }

}
