using UnityEngine;
using System.Collections;

public class GlTF_SpotLight : GlTF_Light {
  public float constantAttenuation = 1f;
  public float fallOffAngle = 3.1415927f;
  public float fallOffExponent = 0f;
  public float linearAttenuation = 0f;
  public float quadraticAttenuation = 0f;

  public GlTF_SpotLight(GlTF_Globals globals) : base(globals) {
    type = "spot";
  }

  protected override void WriteLight() {
    color.Write();
    Indent(); jsonWriter.Write("\"constantAttentuation\": " + constantAttenuation);
    Indent(); jsonWriter.Write("\"fallOffAngle\": " + fallOffAngle);
    Indent(); jsonWriter.Write("\"fallOffExponent\": " + fallOffExponent);
    Indent(); jsonWriter.Write("\"linearAttenuation\": " + linearAttenuation);
    Indent(); jsonWriter.Write("\"quadraticAttenuation\": " + quadraticAttenuation);
    jsonWriter.Write("}");
  }
}
