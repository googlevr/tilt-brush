using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public sealed class GlTF_Mesh : GlTF_ReferencedObject {
  public List<GlTF_Primitive> primitives;

  public GlTF_Mesh(GlTF_Globals globals) : base(globals) {
    primitives = new List<GlTF_Primitive>();
  }

  public static string GetNameFromObject(ObjectName o) {
    return "mesh_" + o.ToGltf1Name();
  }

  public void Populate(TiltBrush.GeometryPool pool) {
    pool.EnsureGeometryResident();

    if (primitives.Count > 0) {
      // Vertex data is shared among all the primitives in the mesh, so only do [0]
      primitives[0].attributes.Populate(pool);
      primitives[0].Populate(pool);

      // I guess someone might want to map Unity submeshes -> gltf primitives.
      // - First you'd want to make sure that consuming tools won't freak out about that,
      //   since it doesn't seem to be the intended use for the mesh/primitive distinction.
      //   See https://github.com/KhronosGroup/glTF/issues/1278
      // - Then you'd want Populate() to take multiple GeometryPools, one per MeshSubset.
      // - Then you'd want those GeometryPools to indicate somehow whether their underlying
      //   vertex data is or can be shared -- maybe do this in GeometryPool.FromMesh()
      //   by having them point to the same Lists.
      // - Then you'd want to make GlTF_attributes.Populate() smart enough to understand that
      //   sharing (ie, memoizing on the List<Vector3> pointer)
      // None of that is implemented, which is okay since our current gltf generation
      // code doesn't add more than one GlTF_Primitive per GlTF_Mesh.
      if (primitives.Count > 1) {
        Debug.LogError("More than one primitive per mesh is unimplemented and unsupported");
      }
    }


    // The mesh data is only ever needed once (because it only goes into the .bin
    // file once), but ExportMeshGeomPool still uses bits of data like pool.NumTris
    // so we can't destroy it.
    //
    // We could MakeNotResident(filename) again, but that's wasteful and I'd need to
    // add an API to get the cache filename. So this "no coming back" API seems like
    // the most expedient solution.
    // TODO: replace this hack with something better? eg, a way to reload from
    // file without destroying the file?
    // pool.Destroy();
    pool.MakeGeometryPermanentlyNotResident();
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    foreach (GlTF_Primitive p in primitives) {
      foreach (var objRef in p.IterReferences(G)) {
        yield return G.Lookup(objRef);
      }
    }
  }

  public override void WriteTopLevel() {
    BeginGltfObject();
    G.CNI.WriteNamedString("name", PresentationName);
    CommaNL(); Indent(); jsonWriter.Write("\"primitives\": [\n");
    IndentIn();
    foreach (GlTF_Primitive p in primitives) {
      CommaNL();
      Indent();
      p.WriteAsUnnamedJObject(G);
    }
    jsonWriter.WriteLine();
    IndentOut();
    Indent(); jsonWriter.Write("]");

    EndGltfObject();
  }
}
