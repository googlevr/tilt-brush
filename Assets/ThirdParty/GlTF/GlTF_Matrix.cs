using UnityEngine;
using System.Collections;

public class GlTF_Matrix : GlTF_FloatArray {
  public readonly Matrix4x4 unityMatrix;
  public GlTF_Matrix(GlTF_Globals globals, Matrix4x4 m)
      : base(globals) {
    unityMatrix = m;
    name = "matrix";
    minItems = 16;
    maxItems = 16;
    // unity: m[row][col]
    // gltf: column major
    items = new float[] {
      m.m00, m.m10, m.m20, m.m30,
      m.m01, m.m11, m.m21, m.m31,
      m.m02, m.m12, m.m22, m.m32,
      m.m03, m.m13, m.m23, m.m33
    };
  }

  public void WriteMatrix() {
    WriteValsAsNamedArray();
  }
}
