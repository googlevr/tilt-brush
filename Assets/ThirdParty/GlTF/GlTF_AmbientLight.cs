using UnityEngine;
using System.Collections;

public class GlTF_AmbientLight : GlTF_Light {
  public GlTF_AmbientLight(GlTF_Globals globals) : base(globals) {}
  protected override void WriteLight() {
    color.Write();
  }
}
