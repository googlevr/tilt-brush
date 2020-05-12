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
public sealed class Gltf1Root : GltfRootBase {
  public Dictionary<string, Gltf1Buffer> buffers;
  public Dictionary<string, Gltf1Accessor> accessors;
  public Dictionary<string, Gltf1BufferView> bufferViews;
  public Dictionary<string, Gltf1Mesh> meshes;
  public Dictionary<string, Gltf1Shader> shaders;
  public Dictionary<string, Gltf1Program> programs;
  public Dictionary<string, Gltf1Technique> techniques;
  public Dictionary<string, Gltf1Image> images;
  public Dictionary<string, Gltf1Texture> textures;
  public Dictionary<string, Gltf1Material> materials;
  public Dictionary<string, Gltf1Node> nodes;
  public Dictionary<string, Gltf1Scene> scenes;
  public string scene;
  private bool disposed;

  [JsonIgnore] public Gltf1Scene scenePtr;

  public override GltfSceneBase ScenePtr { get { return scenePtr; } }

  public override IEnumerable<GltfImageBase> Images {
    get {
      if (images == null) { return new GltfImageBase[0]; }
      return images.Values.Cast<GltfImageBase>();
    }
  }

  public override IEnumerable<GltfTextureBase> Textures {
    get {
      if (textures == null) { return new GltfTextureBase[0]; }
      return textures.Values.Cast<GltfTextureBase>();
    }
  }

  public override IEnumerable<GltfMaterialBase> Materials {
    get {
      if (materials == null) { return new GltfMaterialBase[0]; }
      return materials.Values.Cast<GltfMaterialBase>();
    }
  }

  public override IEnumerable<GltfMeshBase> Meshes {
    get {
      if (meshes == null) { return new GltfMeshBase[0]; }
      return meshes.Values.Cast<GltfMeshBase>();
    }
  }

  // Disposable pattern, with Dispose(void) and Dispose(bool), as recommended by:
  // https://docs.microsoft.com/en-us/dotnet/api/system.idisposable
  protected override void Dispose(bool disposing) {
    if (disposed) return;  // Already disposed.
    if (disposing && buffers != null) {
      foreach (var buffer in buffers.Values) {
        if (buffer != null && buffer.data != null) {
          buffer.data.Dispose();
        }
      }
    }
    disposed = true;
    base.Dispose(disposing);
  }

  /// Map glTFid values (ie, string names) names to the objects they refer to
  public override void Dereference(bool isGlb, IUriLoader uriLoader = null) {
    // "dereference" all the names
    scenePtr = scenes[scene];
    foreach (var pair in buffers) {
      pair.Value.gltfId = pair.Key;
      Gltf1Buffer buffer = pair.Value;
      if (uriLoader != null) {
        Debug.Assert(buffer.type == "arraybuffer");
        // The .glb binary chunk is indicated by the id "binary_glTF", which must lack a uri
        // It's an error for other buffers to lack URIs.
        if (pair.Key == "binary_glTF") {
          Debug.Assert(buffer.uri == null);
        } else {
          Debug.Assert(buffer.uri != null);
        }
        buffer.data = uriLoader.Load(buffer.uri);
      }
    }
    foreach (var pair in accessors) {
      pair.Value.gltfId = pair.Key;
      pair.Value.bufferViewPtr = bufferViews[pair.Value.bufferView];
    }
    foreach (var pair in bufferViews) {
      pair.Value.gltfId = pair.Key;
      pair.Value.bufferPtr = buffers[pair.Value.buffer];
    }
    foreach (var pair in meshes) {
      pair.Value.gltfId = pair.Key;
      foreach (var prim in pair.Value.primitives) {
        prim.attributePtrs = prim.attributes.ToDictionary(
            elt => elt.Key,
            elt => accessors[elt.Value]);
        prim.indicesPtr = accessors[prim.indices];
        prim.materialPtr = materials[prim.material];
      }
    }
    if (shaders != null) {
      foreach (var pair in shaders) {
        pair.Value.gltfId = pair.Key;
      }
    }
    if (programs != null) {
      foreach (var pair in programs) {
        pair.Value.gltfId = pair.Key;
        var program = pair.Value;
        if (program.vertexShader != null) {
          program.vertexShaderPtr = shaders[program.vertexShader];
        }
        if (program.fragmentShader != null) {
          program.fragmentShaderPtr = shaders[program.fragmentShader];
        }
      }
    }
    if (techniques != null) {
      foreach (var pair in techniques) {
        pair.Value.gltfId = pair.Key;
        var technique = pair.Value;
        if (technique.program != null) {
          technique.programPtr = programs[technique.program];
        }
      }
    }
    if (images != null) {
      foreach (var pair in images) {
        pair.Value.gltfId = pair.Key;
      }
      foreach (var pair in textures) {
        pair.Value.gltfId = pair.Key;
        var texture = pair.Value;
        if (texture.source != null) {
          texture.sourcePtr = images[texture.source];
        }
      }
    }
    if (materials != null) {
      foreach (var pair in materials) {
        pair.Value.gltfId = pair.Key;
        var material = pair.Value;
        if (material.technique != null) {
          material.techniquePtr = techniques[material.technique];
        }
        if (material.values != null) {
          if (material.values.BaseColorTex != null) {
            material.values.BaseColorTexPtr = textures[material.values.BaseColorTex];
          }
        }
      }
    }
    foreach (var pair in nodes) {
      pair.Value.gltfId = pair.Key;
      var node = pair.Value;
      if (node.meshes != null) {
        node.meshPtrs = node.meshes.Select(id => meshes[id]).ToList();
      }
      if (node.children != null) {
        node.childPtrs = node.children.Select(id => nodes[id]).ToList();
      }
    }
    foreach (var pair in scenes) {
      pair.Value.gltfId = pair.Key;
      var scene2 = pair.Value;
      if (scene2.nodes != null) {
        scene2.nodePtrs = scene2.nodes.Select(name => nodes[name]).ToList();
      }
    }
  }
}

[Serializable]
public class Gltf1Buffer : GltfBufferBase {
  public string type;
  [JsonIgnore] public string gltfId;
}

[Serializable]
public class Gltf1Accessor : GltfAccessorBase {
  public string bufferView;

  [JsonIgnore] public string gltfId;
  [JsonIgnore] public Gltf1BufferView bufferViewPtr;

  public override GltfBufferViewBase BufferViewPtr { get { return bufferViewPtr; } }
}

[Serializable]
public class Gltf1BufferView : GltfBufferViewBase {
  public string buffer;
  [JsonIgnore] public string gltfId;
  [JsonIgnore] public Gltf1Buffer bufferPtr;

  public override GltfBufferBase BufferPtr { get { return bufferPtr; } }
}

[Serializable]
public class Gltf1Primitive : GltfPrimitiveBase {
  
  public Dictionary<string, string> attributes;
  public string indices;
  public string material;

  [JsonIgnore] public Dictionary<string, Gltf1Accessor> attributePtrs;
  [JsonIgnore] public Gltf1Accessor indicesPtr;
  [JsonIgnore] public Gltf1Material materialPtr;

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
public class Gltf1Shader {
  public const int kFragmentShader = 35632;
  public const int kVertexShader = 35633;
  [JsonIgnore] public string gltfId;
  public string uri;
  public int type;
}

[Serializable]
public class Gltf1Program {
  public string vertexShader;  // index into shaders[]
  public string fragmentShader;  // index into shaders[]

  [JsonIgnore] public string gltfId;
  [JsonIgnore] public Gltf1Shader vertexShaderPtr;
  [JsonIgnore] public Gltf1Shader fragmentShaderPtr;
}

[Serializable]
public class Gltf1Technique {
  public string program;  // index into programs[]
  public Dictionary<string, string> extras;

  [JsonIgnore] public string gltfId;
  [JsonIgnore] public Gltf1Program programPtr;
}

[Serializable]
public class Gltf1Image : GltfImageBase {
  [JsonIgnore] public string gltfId;
}

[Serializable]
public class Gltf1Texture : GltfTextureBase {
  [JsonIgnore] public string gltfId;
  public string source;  // index into images[]
  [JsonIgnore] public Gltf1Image sourcePtr;

  public override object GltfId { get { return gltfId; } }
  public override GltfImageBase SourcePtr { get { return sourcePtr; } }
}

[Serializable]
public class TiltBrushGltf1PbrValues {
  public Color? BaseColorFactor;
  public float? MetallicFactor;
  public float? RoughnessFactor;
  public string BaseColorTex;  // index into textures[]
  // If you add more texture references here, update Gltf1Material.ReferencedTextures
  [JsonIgnore] public Gltf1Texture BaseColorTexPtr;
}

[Serializable]
public class Gltf1Material : GltfMaterialBase {
  public string technique;
  // Since glTF1 is dying, there is no real reason to try and be generic about "values".
  // Instead, parse only those values we need in order to duplicate glTF2-pbr-like
  // functionality. Even this is not generic, as it assumes the pbr-like material
  // was created by Tilt Brush, using its Materials' names.
  // public Dict<string, JObject> values;
  public TiltBrushGltf1PbrValues values;

  [JsonIgnore] public string gltfId;
  [JsonIgnore] public Gltf1Technique techniquePtr;

  public override Dictionary<string,string> TechniqueExtras {
    get {
      return techniquePtr != null ? techniquePtr.extras : null;
    }
  }

  public override IEnumerable<GltfTextureBase> ReferencedTextures {
    get {
      if (values != null) {
        if (values.BaseColorTexPtr != null) {
          yield return values.BaseColorTexPtr;
        }
      }
    }
  }
}

[Serializable]
public class Gltf1Mesh : GltfMeshBase {
  public List<Gltf1Primitive> primitives;
  [JsonIgnore] public string gltfId;

  public override IEnumerable<GltfPrimitiveBase> Primitives {
    get {
      foreach (Gltf1Primitive prim in primitives) {
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
public class Gltf1Node : GltfNodeBase {
  public List<string> children;
  public List<string> meshes;

  [JsonIgnore] public string gltfId;
  [JsonIgnore] public List<Gltf1Mesh> meshPtrs;
  [JsonIgnore] public List<Gltf1Node> childPtrs;

  // Returns the mesh if there is only one mesh associated with this node.
  // Raises InvalidOperationException if there are multiple meshes.
  // glTF 1 allows this; glTF 2 does not.
  //
  // Safe to use if the source is glTF 2, or if you know a priori that the
  // generator doesn't use multiple nodes per mesh.
  public override GltfMeshBase Mesh {
    get {
      if (meshPtrs == null) {
        return null;
      } else if (meshPtrs.Count == 0) {
        return null;
      } else if (meshPtrs.Count == 1) {
        return meshPtrs[0];
      } else {
        throw new InvalidOperationException("Multiple meshes");
      }
    }
  }

  public override IEnumerable<GltfNodeBase> Children {
    get {
      if (childPtrs == null) yield break;
      foreach (Gltf1Node node in childPtrs) {
        yield return node;
      }
    }
  }
}

[Serializable]
public class Gltf1Scene : GltfSceneBase {
  public List<string> nodes;

  [JsonIgnore] public string gltfId;
  [JsonIgnore] public List<Gltf1Node> nodePtrs;

  public override IEnumerable<GltfNodeBase> Nodes {
    get {
      foreach (Gltf1Node node in nodePtrs) {
        yield return node;
      }
    }
  }
}

}
