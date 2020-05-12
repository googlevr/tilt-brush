using UnityEngine;
using System.Collections;

public class GlTF_Scale : GlTF_Vector3 {
  public GlTF_Scale(GlTF_Globals globals) : base(globals) {
    items = new float[] { 1f, 1f, 1f };
  }
  public GlTF_Scale(GlTF_Globals globals, Vector3 v) : base(globals) {
    items = new float[] { v.x, v.y, v.z };
  }
  public void WriteScale() {
    Indent();
    jsonWriter.Write("\"scale\": [ ");
    WriteVals();
    jsonWriter.Write("]");
  }
}
