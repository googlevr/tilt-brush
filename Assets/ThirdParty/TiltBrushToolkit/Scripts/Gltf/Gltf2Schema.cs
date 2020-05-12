// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;

using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrushToolkit {
[Serializable]
public class GOOGLE_tilt_brush_material {
  public string guid;
}

[Serializable]
public sealed class Gltf2Root : GltfRootBase {
  public List<Gltf2Buffer> buffers;
  public List<Gltf2Accessor> accessors;
  public List<Gltf2BufferView> bufferViews;
  public List<Gltf2Mesh> meshes;
  public List<Gltf2Material> materials;
  public List<Gltf2Node> nodes;
  public List<Gltf2Scene> scenes;
  public List<Gltf2Texture> textures;
  public List<Gltf2Image> images;
  public int scene;
  private bool disposed;

  [JsonIgnore] public Gltf2Scene scenePtr;

  public override GltfSceneBase ScenePtr { get { return scenePtr; } }

  public override IEnumerable<GltfImageBase> Images {
    get {
      if (images == null) { return new GltfImageBase[0]; }
      return images.Cast<GltfImageBase>();
    }
  }

  public override IEnumerable<GltfTextureBase> Textures {
    get {
      if (textures == null) { return new GltfTextureBase[0]; }
      return textures.Cast<GltfTextureBase>();
    }
  }

  public override IEnumerable<GltfMaterialBase> Materials {
    get {
      if (materials == null) { return new GltfMaterialBase[0]; }
      return materials.Cast<GltfMaterialBase>();
    }
  }

  public override IEnumerable<GltfMeshBase> Meshes {
    get {
      if (meshes == null) { return new GltfMeshBase[0]; }
      return meshes.Cast<GltfMeshBase>();
    }
  }

  // Disposable pattern, with Dispose(void) and Dispose(bool), as recommended by:
  // https://docs.microsoft.com/en-us/dotnet/api/system.idisposable
  protected override void Dispose(bool disposing) {
    if (disposed) return;  // Already disposed.
    if (disposing && buffers != null) {
      foreach (var buffer in buffers) {
        if (buffer != null && buffer.data != null) {
          buffer.data.Dispose();
        }
      }
    }
    disposed = true;
    base.Dispose(disposing);
  }

  /// Map gltfIndex values (ie, int indices) names to the objects they refer to
  public override void Dereference(bool isGlb, IUriLoader uriLoader = null) {
    // "dereference" all the indices
    scenePtr = scenes[scene];
    for (int i = 0; i < buffers.Count; i++) {
      Gltf2Buffer buffer = buffers[i];
      buffer.gltfIndex = i;
      if (buffer.uri == null && !(i == 0 && isGlb)) {
        Debug.LogErrorFormat("Buffer {0} isGlb {1} has null uri", i, isGlb);
        // leave the data buffer null
        return;
      }

      if (uriLoader != null) {
        buffer.data = uriLoader.Load(buffer.uri);
      }
    }
    for (int i = 0; i < accessors.Count; i++) {
      accessors[i].gltfIndex = i;
      accessors[i].bufferViewPtr = bufferViews[accessors[i].bufferView];
    }
    for (int i = 0; i < bufferViews.Count; i++) {
      bufferViews[i].gltfIndex = i;
      bufferViews[i].bufferPtr = buffers[bufferViews[i].buffer];
    }
    for (int i = 0; i < meshes.Count; i++) {
      meshes[i].gltfIndex = i;
      foreach (var prim in meshes[i].primitives) {
        prim.attributePtrs = prim.attributes.ToDictionary(
            elt => elt.Key,
            elt => accessors[elt.Value]);
        prim.indicesPtr = accessors[prim.indices];
        prim.materialPtr = materials[prim.material];
      }
    }
    if (images != null) {
      for (int i = 0; i < images.Count; i++) {
        images[i].gltfIndex = i;
      }
    }
    if (textures != null) {
      for (int i = 0; i < textures.Count; i++) {
        textures[i].gltfIndex = i;
        textures[i].sourcePtr = images[textures[i].source];
      }
    }
    for (int i = 0; i < materials.Count; i++) {
      Gltf2Material mat = materials[i];
      mat.gltfIndex = i;
      DereferenceTextureInfo(mat.emissiveTexture);
      DereferenceTextureInfo(mat.normalTexture);
      if (mat.pbrMetallicRoughness != null) {
        DereferenceTextureInfo(mat.pbrMetallicRoughness.baseColorTexture);
        DereferenceTextureInfo(mat.pbrMetallicRoughness.metallicRoughnessTexture);
      }
      DereferenceTextureInfo(mat.extensions?.KHR_materials_pbrSpecularGlossiness?.diffuseTexture);
    }
    for (int i = 0; i < nodes.Count; i++) {
      nodes[i].gltfIndex = i;
      Gltf2Node node = nodes[i];
      if (node.mesh >= 0) {
        node.meshPtr = meshes[node.mesh];
      }
      if (node.children != null) {
        node.childPtrs = node.children.Select(id => nodes[id]).ToList();
      }
    }
    for (int i = 0; i < scenes.Count; i++) {
      scenes[i].gltfIndex = i;
      var thisScene = scenes[i];
      if (thisScene.nodes != null) {
        thisScene.nodePtrs = thisScene.nodes.Select(index => nodes[index]).ToList();
      }
    }
  }

  private void DereferenceTextureInfo(Gltf2Material.TextureInfo texInfo) {
    if (texInfo == null) return;
    texInfo.texture = textures[texInfo.index];
  }
}

[Serializable]
public class Gltf2Buffer : GltfBufferBase {
  [JsonIgnore] public int gltfIndex;
}

[Serializable]
public class Gltf2Accessor : GltfAccessorBase {
  public int bufferView;

  [JsonIgnore] public int gltfIndex;
  [JsonIgnore] public Gltf2BufferView bufferViewPtr;

  public override GltfBufferViewBase BufferViewPtr { get { return bufferViewPtr; } }
}

[Serializable]
public class Gltf2BufferView : GltfBufferViewBase {
  public int buffer;
  [JsonIgnore] public int gltfIndex;
  [JsonIgnore] public Gltf2Buffer bufferPtr;

  public override GltfBufferBase BufferPtr { get { return bufferPtr; } }
}

[Serializable]
public class Gltf2Primitive : GltfPrimitiveBase {
  public Dictionary<string, int> attributes;  // value is an index into accessors[]
  public int indices;
  public int material;
  
  [JsonIgnore] public Dictionary<string, Gltf2Accessor> attributePtrs;
  [JsonIgnore] public Gltf2Accessor indicesPtr;
  [JsonIgnore] public Gltf2Material materialPtr;

  public override GltfMaterialBase MaterialPtr { get { return materialPtr; } }
  public override GltfAccessorBase IndicesPtr { get { return indicesPtr; } }
  public override GltfAccessorBase GetAttributePtr(string attributeName) {
    return attributePtrs[attributeName];
  }
  public override void ReplaceAttribute(string original, string replacement) {
    attributes[replacement] = attributes[original];
    attributePtrs[replacement] = attributePtrs[original];
    attributes.Remove(original);
    attributePtrs.Remove(original);
  }
  public override HashSet<string> GetAttributeNames() {
    return new HashSet<string>(attributePtrs.Keys);
  }
}


[Serializable]
public class Gltf2Material : GltfMaterialBase {
  // Enum values for alphaMode
  public const string kAlphaModeOpaque = "OPAQUE";
  public const string kAlphaModeMask = "MASK";
  public const string kAlphaModeBlend = "BLEND";

  public Dictionary<string,string> extras;
  [JsonIgnore] public int gltfIndex;

  public override Dictionary<string,string> TechniqueExtras { get { return extras; } }

  private IEnumerable<TextureInfo> TextureInfos {
    get {
      yield return normalTexture;
      yield return emissiveTexture;
      if (pbrMetallicRoughness != null) {
        yield return pbrMetallicRoughness.baseColorTexture;
        yield return pbrMetallicRoughness.metallicRoughnessTexture;
      } else if (extensions?.KHR_materials_pbrSpecularGlossiness != null) {
        var specGloss = extensions.KHR_materials_pbrSpecularGlossiness;
        yield return specGloss.diffuseTexture;
      }
    }
  }

  public override IEnumerable<GltfTextureBase> ReferencedTextures {
    get {
      foreach (var ti in TextureInfos) {
        if (ti != null && ti.texture != null) {
          yield return ti.texture;
        }
      }
    }
  }

  public PbrMetallicRoughness pbrMetallicRoughness;
  public TextureInfo normalTexture;
  public TextureInfo emissiveTexture;
  public Vector3 emissiveFactor;
  public string alphaMode;
  public bool doubleSided;
  public Extensions extensions;

  [Serializable]
  public class Extensions {
    public GOOGLE_tilt_brush_material GOOGLE_tilt_brush_material;
    public KHR_materials_pbrSpecularGlossiness KHR_materials_pbrSpecularGlossiness;
  }

  [Serializable]
  public class PbrMetallicRoughness {
    public Color baseColorFactor = Color.white;
    public float metallicFactor = 1.0f;
    public float roughnessFactor = 1.0f;
    public TextureInfo baseColorTexture;
    public TextureInfo metallicRoughnessTexture;
  }

  [Serializable]
  public class KHR_materials_pbrSpecularGlossiness {
    public Color diffuseFactor = Color.white;
    public TextureInfo diffuseTexture;
    // public Vector3 specularFactor; not supported
    public float glossinessFactor = 1.0f;
  }

  [Serializable]
  public class TextureInfo {
    public int index;  // indexes into root.textures[]
    public int texCoord = 0;
    [JsonIgnore] public GltfTextureBase texture;
  }
}

[Serializable]
public class Gltf2Texture : GltfTextureBase {
  [JsonIgnore] public int gltfIndex;
  public int source;  // indexes into images[]
  [JsonIgnore] public Gltf2Image sourcePtr;

  public override object GltfId { get { return gltfIndex; } }
  public override GltfImageBase SourcePtr { get { return sourcePtr; } }
}

[Serializable]
public class Gltf2Image : GltfImageBase {
  [JsonIgnore] public int gltfIndex;
  public string name;
  public string mimeType;
}

[Serializable]
public class Gltf2Mesh : GltfMeshBase {
  public List<Gltf2Primitive> primitives;
  [JsonIgnore] public int gltfIndex;

  public override IEnumerable<GltfPrimitiveBase> Primitives {
    get {
      foreach (Gltf2Primitive prim in primitives) {
        yield return prim;
      }
    }
  }

  public override int PrimitiveCount { get { return primitives.Count(); } }

  public override GltfPrimitiveBase GetPrimitiveAt(int i) {
    return primitives[i];
  }
}

[Serializable]
public class Gltf2Node : GltfNodeBase {
  public List<int> children;
  public int mesh = -1;

  [JsonIgnore] public int gltfIndex;
  [JsonIgnore] public Gltf2Mesh meshPtr;
  [JsonIgnore] public List<Gltf2Node> childPtrs;

  public override GltfMeshBase Mesh {
    get {
      return meshPtr;
    }
  }

  public override IEnumerable<GltfNodeBase> Children {
    get {
      if (childPtrs == null) yield break;
      foreach (Gltf2Node node in childPtrs) {
        yield return node;
      }
    }
  }
}

[Serializable]
public class Gltf2Scene : GltfSceneBase {
  public List<int> nodes;

  [JsonIgnore] public int gltfIndex;
  [JsonIgnore] public List<Gltf2Node> nodePtrs;

  public override IEnumerable<GltfNodeBase> Nodes {
    get {
      foreach (Gltf2Node node in nodePtrs) {
        yield return node;
      }
    }
  }
}

}
