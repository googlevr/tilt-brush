using UnityEngine;
using System.Collections;

public class GlTF_DirectionalLight : GlTF_Light {
  public GlTF_DirectionalLight(GlTF_Globals globals) : base(globals) {}
  protected override void WriteLight() {
    color.Write();
  }
}
