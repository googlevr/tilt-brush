using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class GlTF_Node : GlTF_ReferencedObject {
  private List<GlTF_Node> m_children = new List<GlTF_Node>();

  public string cameraName;
  public string lightNameThatDoesNothing;
  // gltf 2 only supports a single mesh per node, so enforce that with gltf1
  public GlTF_Mesh m_mesh;
  public GlTF_Matrix matrix;
  //	public GlTF_Mesh mesh;
  public GlTF_Rotation rotation;
  public GlTF_Scale scale;
  public GlTF_Translation translation;

  public readonly GlTF_Node Parent;

  public static string GetNameFromObject(ObjectName o) {
    return "node_" + o.ToGltf1Name();
  }

  /// Always creates a node, but the name may not be your desired name.
  public static GlTF_Node Create(
      GlTF_Globals globals, string desiredName, Matrix4x4 mat, GlTF_Node parent) {
    string name = desiredName;
    for (int i = 0; globals.nodes.ContainsKey(name); ++i) {
      name = $"{desiredName} {i}";
    }
    return GetOrCreate(globals, name, mat, parent, out _);
  }

  // If a node with this name is already in the scene, return that node, and created = false.
  // Otherwise, returns a newly-created node, and created = true.
  // It's considered a user error to ask for the same node twice with 2 different matrices.
  //
  // It's up to the caller to figure out if "node already created" is an error or not.
  // As best as I can tell, we might try and create nodes of the same name if (for example)
  // we create a gltf_light and gltf_mesh with the same ObjectName, because both of those
  // create a gltf_node to point to the gltf_light and _mesh objects.
  //
  // Since gltf_node has slots for both lightName and meshName, it can be used as both a light
  // and a mesh; so callers should probably not care if created=false; they should instead check
  // whether meshName == null. Or perhaps, they should first check to see if they're creating
  // a duplicate mesh.
  public static GlTF_Node GetOrCreate(
      GlTF_Globals globals, ObjectName o,
      Matrix4x4 mat, GlTF_Node parent,
      out bool created) {
    return GetOrCreate(globals, GetNameFromObject(o), mat, parent, out created);
  }

  private static GlTF_Node GetOrCreate(
      GlTF_Globals globals, string name,
      Matrix4x4 mat, GlTF_Node parent,
      out bool created) {
    if (globals.nodes.ContainsKey(name)) {
      var ret = globals.nodes[name];
      if (ret.matrix.unityMatrix != mat) {
        Debug.LogErrorFormat("Node {0} cannot have two different matrices", name);
      }
      created = false;
    } else {
      var ret = new GlTF_Node(globals, name, mat, parent);
      globals.nodes[name] = ret;
      created = true;
      return ret;
    }
    return globals.nodes[name];
  }

  private GlTF_Node(GlTF_Globals globals, string name, Matrix4x4 mat, GlTF_Node parent)
      : base(globals) {
    this.Parent = parent;
    if (parent != null) {
      parent.m_children.Add(this);
    }
    this.matrix = new GlTF_Matrix(globals, mat);
    this.name = name;
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    if (cameraName != null) {
      // No node.camera in gltf2
      if (! G.Gltf2) {
        yield return G.Lookup<GlTF_Camera>(cameraName);
      }
    } else if (m_mesh != null) {
      yield return G.Lookup(m_mesh);
    }

    foreach (var child in m_children) {
      yield return G.Lookup(child);
    }
  }

  public override void WriteTopLevel() {
    BeginGltfObject();

    G.CNI.WriteNamedString("name", PresentationName);
    if (cameraName != null) {
      // No node.camera in gltf2
      if (! G.Gltf2) {
        G.CNI.WriteNamedReference<GlTF_Camera>("camera", cameraName);
      }
    } else if (lightNameThatDoesNothing != null) {
      // This does absolutely nothing
      if (!G.Gltf2) {
        G.CNI.WriteNamedString("light", lightNameThatDoesNothing);
      }
    } else if (m_mesh != null) {
      if (G.Gltf2) {
        G.CNI.WriteNamedReference("mesh", m_mesh);
      } else {
        G.CNI.WriteNamedJArray("meshes", new[] { m_mesh },
                               item => jsonWriter.Write(G.SerializeReference(item)));
      }
    }

    if (m_children != null && m_children.Count > 0) {
      G.CNI.WriteNamedJArray("children", m_children,
                             item => jsonWriter.Write(G.SerializeReference(item)));
    }

    // Gltf2 suggests that the default matrix not get written out
    if (matrix != null) {
      bool isDefaultMatrix = G.Gltf2 && matrix.unityMatrix.isIdentity;
      if (!isDefaultMatrix) {
        CommaNL();
        matrix.WriteMatrix();
      }
    }
    if (translation != null && (translation.items[0] != 0f || translation.items[1] != 0f || translation.items[2] != 0f)) {
      CommaNL();
      translation.WriteTranslation();
    }
    if (scale != null && (scale.items[0] != 1f || scale.items[1] != 1f || scale.items[2] != 1f)) {
      CommaNL();
      scale.WriteScale();
    }
    if (rotation != null && (rotation.items[0] != 0f || rotation.items[1] != 0f || rotation.items[2] != 0f || rotation.items[3] != 0f)) {
      CommaNL();
      rotation.WriteRotation();
    }

    EndGltfObject();
  }
}
