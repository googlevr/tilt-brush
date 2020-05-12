using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GlTF_FileReference = TiltBrush.ExportFileReference;

public sealed class GlTF_Shader : GlTF_ReferencedObject {
  public enum Type {
    Vertex,
    Fragment
  }

  public Type type = Type.Vertex;
  public GlTF_FileReference uri;

  public static string GetNameFromObject(TiltBrush.IExportableMaterial iem, Type type) {
    var typeName = type == Type.Vertex ? "vertex" : "fragment";
    return $"{typeName}_{iem.UniqueName:D}";
  }

  public GlTF_Shader(GlTF_Globals globals) : base(globals) {}

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield break;
  }

  public override void WriteTopLevel() {
    BeginGltfObject();
    G.CNI.WriteNamedInt("type", TypeStr());
    if (uri != null) {
      G.CNI.WriteNamedFile("uri", uri);
    }
    EndGltfObject();
  }

  private int TypeStr() {
    if (type == Type.Vertex) {
      return 35633;
    } else if (type == Type.Fragment) {
      return 35632;
    }

    return 0;
  }
}
