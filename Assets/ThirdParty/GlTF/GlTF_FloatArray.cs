using UnityEngine;
using System.Collections;

public abstract class GlTF_FloatArray : GlTF_Writer {
  public float[] items;
  public int minItems = 0;
  public int maxItems = 0; // TODO: rename to numItems?

  public GlTF_FloatArray(GlTF_Globals globals) : base(globals) {
  }
  public GlTF_FloatArray(GlTF_Globals globals, string n) : base(globals) {
    name = n;
  }

  protected void WriteValsAsNamedArray() {
    if (name.Length > 0) {
      Indent(); jsonWriter.Write("\"" + name + "\": [");
    }
    WriteVals();
    if (name.Length > 0) {
      jsonWriter.Write("]");
    }
  }

  protected void WriteVals() {
    for (int i = 0; i < maxItems; i++) {
      if (i > 0)
        jsonWriter.Write(", ");
      jsonWriter.Write(items[i].ToString());
    }
  }
}
