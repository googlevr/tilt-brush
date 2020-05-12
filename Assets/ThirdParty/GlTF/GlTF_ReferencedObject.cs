using System.Collections.Generic;

/// An object which can be referenced in a .gltf file,
/// either by index (gltf2) or by name (gltf1)
public abstract class GlTF_ReferencedObject : GlTF_Writer {
  /// Only valid at Write() time.
  /// If this is null, the object isn't marked for serialization.
  public int? Index { get; set; }

  public GlTF_ReferencedObject(GlTF_Globals g) : base(g) {}

  // If null, PresentationName falls back to using the object-graph name.
  // Set this if you have something more human-readable to use.
  public string PresentationNameOverride {get; set;}

  // GlTF_Writer.name is the string used for inter-object references in the gltf1 object graph.
  // PresentationName is the string to be used by Mesh, Material, and Node for their
  // human-facing "name" property. It defaults to the gltf1 name but can be overridden.
  // Out of an abundance of caution, this is gltf2-only for now -- nobody imports the gltf1
  // into a DCC tool anyway.
  public string PresentationName => (G.Gltf2 ? PresentationNameOverride ?? name : name);

  /// This must return only and exactly those references that WriteTopLevel serializes.
  public abstract IEnumerable<GlTF_ReferencedObject> IterReferences();
  public abstract void WriteTopLevel();

  public void BeginGltfObject() {
    G.jsonWriter.Write("{");
    G.jsonWriter.WriteLine();
    IndentIn();
  }

  public void EndGltfObject() {
    jsonWriter.WriteLine();
    IndentOut();
    Indent();
    jsonWriter.Write("}");
  }
}
