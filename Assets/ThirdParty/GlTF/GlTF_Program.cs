using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class GlTF_Program : GlTF_ReferencedObject {
  public List<string> attributes = new List<string>();
  public string vertexShader = "";
  public string fragmentShader = "";

  public GlTF_Program(GlTF_Globals globals) : base(globals) {}
  public static string GetNameFromObject(TiltBrush.IExportableMaterial iem) {
    return $"program_{iem.UniqueName:D}";
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield return G.Lookup<GlTF_Shader>(vertexShader);
    yield return G.Lookup<GlTF_Shader>(fragmentShader);
  }

  public override void WriteTopLevel() {
    BeginGltfObject();
    G.CNI.WriteNamedJArray("attributes", attributes, item => jsonWriter.Write("\"" + item + "\""));
    G.CNI.WriteNamedReference<GlTF_Shader>("vertexShader", vertexShader);
    G.CNI.WriteNamedReference<GlTF_Shader>("fragmentShader", fragmentShader);
    EndGltfObject();
  }
}
