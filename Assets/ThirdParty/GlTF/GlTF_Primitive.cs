using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Note: not a top-level object, so not a GlTF_Writer
public sealed class GlTF_Primitive {
  const int kPrimitiveMode_Triangles = 4;
  public readonly GlTF_Attributes attributes;
  public GlTF_Accessor indices;
  public string materialName;

  public GlTF_Primitive(GlTF_Attributes attributes) {
    this.attributes = attributes;
  }

  public void Populate(TiltBrush.GeometryPool pool) {
    indices.PopulateUshort(pool.m_Tris.ToArray());
  }

  public IEnumerable<GlTF_ReferencedObject> IterReferences(GlTF_Globals G) {
    if (attributes != null) {
      foreach (var objRef in attributes.IterReferences(G)) {
        yield return G.Lookup(objRef);
      }
    }
    yield return G.Lookup(indices);
    yield return G.Lookup<GlTF_Material>(materialName);
  }

  public void WriteAsUnnamedJObject(GlTF_Globals G) {
    G.jsonWriter.Write("{\n"); G.IndentIn();

    if (attributes != null) {
      G.CommaNL(); G.Indent(); attributes.WriteAsNamedJObject(G, "attributes");
    }
    G.CNI.WriteNamedReference("indices", indices);
    G.CNI.WriteNamedReference<GlTF_Material>("material", materialName);
    G.CNI.WriteNamedInt("mode", kPrimitiveMode_Triangles);
    G.NewlineAndIndentOut("}");
  }
}
