using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class GlTF_Light : GlTF_Writer {
  public GlTF_ColorRGB color;
  public string type;
  public GlTF_Light(GlTF_Globals globals) : base(globals) {}
  protected abstract void WriteLight();
}
