using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using GlTF_FileReference = TiltBrush.ExportFileReference;

public sealed class GlTF_Image : GlTF_ReferencedObject {
  public static GlTF_Image LookupOrCreate(GlTF_Globals G, GlTF_FileReference fileRef,
                                          string proposedName = null) {
    if (! G.imagesByFileRefUri.ContainsKey(fileRef.m_uri)) {
      string name = "image_" + (proposedName ?? $"{G.imagesByFileRefUri.Count}");
      G.imagesByFileRefUri.Add(fileRef.m_uri, new GlTF_Image(G, name, fileRef));
    }
    return G.imagesByFileRefUri[fileRef.m_uri];
  }

  public readonly GlTF_FileReference uri;

  private GlTF_Image(GlTF_Globals globals, string name, GlTF_FileReference fileRef)
      : base(globals) {
    this.name = name;
    this.uri = fileRef;
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield break;
  }

  public override void WriteTopLevel() {
    BeginGltfObject();
    G.CNI.WriteNamedFile("uri", uri);
    EndGltfObject();
  }
}
