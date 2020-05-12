using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

public sealed class GlTF_Buffer : GlTF_ReferencedObject {
  public string m_uri;
  public long? m_byteLength;

  public GlTF_Buffer(GlTF_Globals globals, [CanBeNull] string uri)
      : base(globals) {
    if (uri == null) {
      // The built-in buffer for a .glb in gltf1 needs to be called "binary_glTF"
      this.name = "binary_glTF";
    } else {
      this.name = "buffer_" + Path.GetFileNameWithoutExtension(uri);
    }
    m_uri = uri;
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield break;
  }

  public override void WriteTopLevel() {
    BeginGltfObject();

    if (m_byteLength == null) {
      throw new System.Exception("You must fill in m_byteLength before writing gltf");
    }
    G.CNI.WriteNamedInt("byteLength", m_byteLength.Value);
    if (! G.Gltf2) {
      // Could get rid of this even for gltf1, since arraybuffer is the default
      G.CNI.WriteNamedString("type", "arraybuffer");
    }
    if (m_uri != null) {
      G.CNI.WriteNamedString("uri", m_uri);
    }

    EndGltfObject();
  }
}
