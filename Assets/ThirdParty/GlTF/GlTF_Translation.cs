using UnityEngine;
using System.Collections;

public class GlTF_Translation : GlTF_Vector3 {
  public GlTF_Translation(GlTF_Globals globals, Vector3 v) : base(globals) {
    items = new float[] { v.x, v.y, v.z };
  }
  public void WriteTranslation() {
    Indent(); jsonWriter.Write("\"translation\": [ ");
    WriteVals();
    jsonWriter.Write("]");
  }
}
