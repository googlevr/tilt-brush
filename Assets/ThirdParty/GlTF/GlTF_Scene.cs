using System.Collections.Generic;

public sealed class GlTF_Scene : GlTF_ReferencedObject {
  private List<GlTF_Node> m_nodes = new List<GlTF_Node>();
  public Dictionary<string, object> m_extras = new Dictionary<string, object>();

  public GlTF_Scene(GlTF_Globals globals, string name,
                    IEnumerable<GlTF_Node> nodes,
                    Dictionary<string, object> extras)
      : base(globals) {
    this.name = name;
    if (nodes != null) {
      m_nodes.AddRange(nodes);
    }
    if (extras != null) {
      foreach (var kvp in extras) {
        m_extras[kvp.Key] = kvp.Value;
      }
    }
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    foreach (var node in m_nodes) {
      yield return G.Lookup(node);
    }
  }

  public override void WriteTopLevel() {
    BeginGltfObject();

    G.CNI.WriteNamedJArray("nodes", m_nodes, item => {
      jsonWriter.Write(G.SerializeReference(item));
    });
    G.CNI.WriteNamedJObject("extras", m_extras, kvp => {
      G.WriteNamedObject(kvp.Key, kvp.Value);
    });

    EndGltfObject();
  }
}
