using UnityEngine;
using System.Collections;

public sealed class GlTF_ColorRGBA : GlTF_Writer {
  private Color color;
  public GlTF_ColorRGBA(GlTF_Globals globals, string n) : base(globals) {
    name = n;
  }
  public GlTF_ColorRGBA(GlTF_Globals globals, Color c) : base(globals) {
    color = c;
  }
  public GlTF_ColorRGBA(GlTF_Globals globals, string n, Color c) : base(globals) {
    name = n; color = c;
  }
  public void Write() {
    Indent();
    if (name.Length > 0)
      jsonWriter.Write("\"" + name + "\": [");
    else
      jsonWriter.Write("\"color\": [");
    jsonWriter.Write(color.r.ToString() + ", " + color.g.ToString() + ", " + color.b.ToString() + ", " + color.a.ToString() + "]");
  }
}
