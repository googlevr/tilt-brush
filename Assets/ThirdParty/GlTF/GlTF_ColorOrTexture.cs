using UnityEngine;
using System.Collections;

public abstract class GlTF_ColorOrTexture : GlTF_Writer {
  public GlTF_ColorOrTexture(GlTF_Globals globals) : base(globals) {
  }
  public GlTF_ColorOrTexture(GlTF_Globals globals, string n) : base(globals) {
    name = n;
  }
  public abstract void WriteColorOrTexture();
}
