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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace TiltBrushToolkit {

public enum GltfSchemaVersion { GLTF1, GLTF2 }

public class BadJson : Exception {
  public BadJson(string message) : base(message) {}
  public BadJson(string fmt, params System.Object[] args)
    : base(string.Format(fmt, args)) {}
  public BadJson(Exception inner, string fmt, params System.Object[] args)
    : base(string.Format(fmt, args), inner) {}
}


//
// JSON.net magic for reading in a Matrix4x4 from a json float-array
//

public class JsonVectorConverter : JsonConverter {
  public override bool CanConvert(Type objectType) {
    return (objectType == typeof(Vector3)
            || objectType == typeof(Matrix4x4?)
            || objectType == typeof(Matrix4x4)
            || objectType == typeof(Color));
  }

  private static float ReadFloat(JsonReader reader) {
    reader.Read();
    if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer) {
      return Convert.ToSingle(reader.Value);
    }
    throw new BadJson("Expected numeric value");
  }

  public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
                                  JsonSerializer serializer) {
    if (reader.TokenType != JsonToken.StartArray) {
      throw new BadJson("Expected array");
    }
    object result;
    if (objectType == typeof(Vector3)) {
      result = new Vector3(
          ReadFloat(reader),
          ReadFloat(reader),
          ReadFloat(reader));
    } else if (objectType == typeof(Matrix4x4) ||
               objectType == typeof(Matrix4x4?)) {
      // Matrix members are m<row><col>
      // Gltf stores matrices in column-major order
      result = new Matrix4x4 {
        m00=ReadFloat(reader), m10=ReadFloat(reader), m20=ReadFloat(reader), m30=ReadFloat(reader),
        m01=ReadFloat(reader), m11=ReadFloat(reader), m21=ReadFloat(reader), m31=ReadFloat(reader),
        m02=ReadFloat(reader), m12=ReadFloat(reader), m22=ReadFloat(reader), m32=ReadFloat(reader),
        m03=ReadFloat(reader), m13=ReadFloat(reader), m23=ReadFloat(reader), m33=ReadFloat(reader)
      };
    } else if (objectType == typeof(Color) || objectType == typeof(Color?)) {
      result = new Color(
          ReadFloat(reader),
          ReadFloat(reader),
          ReadFloat(reader),
          ReadFloat(reader));
    } else {
      Debug.Assert(false, "Converter registered with bad type");
      throw new BadJson("Internal error");
    }
    reader.Read();
    if (reader.TokenType != JsonToken.EndArray) {
      throw new BadJson("Expected array end");
    }
    return result;
  }

  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
    throw new NotImplementedException();
  }
}

// Specify converter for types that we can't annotate
public class GltfJsonContractResolver : DefaultContractResolver {
  protected override JsonContract CreateContract(Type objectType) {
    JsonContract contract = base.CreateContract(objectType);
    if (objectType == typeof(Vector3)
        || objectType == typeof(Matrix4x4)
        || objectType == typeof(Matrix4x4?)
        || objectType == typeof(Color?)
        || objectType == typeof(Color)) {
      contract.Converter = new JsonVectorConverter();
    }
    return contract;
  }
}

//
// C# classes corresponding to the gltf json schema
//



[Serializable]
public class GltfAsset {
  public Dictionary<string, string> extensions;
  public Dictionary<string, string> extras;
  public string copyright;
  public string generator;
  public bool premultipliedAlpha;
  // public GltfProfile profile;   not currently needed
  public string version;
}

/// A bucket of mesh data in the format that Unity wants it
/// (or as close to it as possible)
public class MeshPrecursor {
  public Vector3[] vertices;
  public Vector3[] normals;
  public Color[] colors;
  public Color32[] colors32;
  public Vector4[] tangents;
  public Array[] uvSets = new Array[4];
  public int[] triangles;
}

// Note about the *Base classes: these are the base classes for the GLTF 1 and GLTF 2 versions
// of each entity. The fields that are common to both versions of the format go in the base
// class. The fields that differ (in name or type) between versions go in the subclasses.
// The version-specific subclasses are in Gltf{1,2}Schema.cs.
// For more info on the spec, see:
//   https://github.com/KhronosGroup/glTF/

[Serializable]
public abstract class GltfRootBase : IDisposable {
  public GltfAsset asset;
  // Tilt Brush/Blocks version that generated the gltf; null if not generated by that program
  [JsonIgnore] public Version? tiltBrushVersion;
  [JsonIgnore] public Version? blocksVersion;

  public abstract GltfSceneBase ScenePtr { get; }
  public abstract IEnumerable<GltfImageBase> Images { get; }
  public abstract IEnumerable<GltfTextureBase> Textures { get; }
  public abstract IEnumerable<GltfMaterialBase> Materials { get; }
  public abstract IEnumerable<GltfMeshBase> Meshes { get; }

  public abstract void Dereference(bool isGlb, IUriLoader uriLoader = null);

  // Disposable pattern, with Dispose(void) and Dispose(bool), as recommended by:
  // https://docs.microsoft.com/en-us/dotnet/api/system.idisposable
  public void Dispose() {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  // Subclasses should override Dispose(bool) and call base class implementation.
  protected virtual void Dispose(bool disposing) {}
}

[Serializable]
public abstract class GltfBufferBase {
  public long byteLength;
  public string uri;
  [JsonIgnore] public IBufferReader data;
}

[Serializable]
public abstract class GltfAccessorBase {
  [Serializable] public enum ComponentType {
    BYTE = 5120, UNSIGNED_BYTE = 5121, SHORT = 5122, UNSIGNED_SHORT = 5123,
    UNSIGNED_INT = 5125,
    FLOAT = 5126
  }
  public int byteOffset;
  public int byteStride;
  public ComponentType componentType;
  public int count;
  public List<float> max;
  public List<float> min;
  public string type;

  public abstract GltfBufferViewBase BufferViewPtr { get; }
}

[Serializable]
public abstract class GltfBufferViewBase {
  public int byteLength;
  public int byteOffset;
  public int target;

  public abstract GltfBufferBase BufferPtr { get; }
}

[Serializable]
public abstract class GltfPrimitiveBase {
  [Serializable] public enum Mode { TRIANGLES = 4 }
  public Mode mode = Mode.TRIANGLES;

  // Not part of the schema; this is for lazy-creation convenience
  // There may be more than one if the gltf primitive is too big for Unity

  [JsonIgnore] public List<MeshPrecursor> precursorMeshes;
  [JsonIgnore] public List<Mesh> unityMeshes;

  public abstract GltfMaterialBase MaterialPtr { get; }
  public abstract GltfAccessorBase IndicesPtr { get; }
  public abstract GltfAccessorBase GetAttributePtr(string attributeName);
  // Rename attribute from original -> replacement.
  // ie, attrs[replacement] = attrs.pop(original)
  public abstract void ReplaceAttribute(string original, string replacement);
  public abstract HashSet<string> GetAttributeNames();
}

[Serializable]
public abstract class GltfImageBase {
  public string uri;
  [JsonIgnore] public RawImage data;
}

[Serializable]
public abstract class GltfTextureBase {
  [JsonIgnore] public Texture2D unityTexture;

  public abstract object GltfId { get; }
  public abstract GltfImageBase SourcePtr { get; }
}

[Serializable]
public abstract class GltfMaterialBase {
  public string name;

  public abstract Dictionary<string,string> TechniqueExtras { get; }
  public abstract IEnumerable<GltfTextureBase> ReferencedTextures { get; }
}

[Serializable]
public abstract class GltfMeshBase {
  public string name;

  public abstract IEnumerable<GltfPrimitiveBase> Primitives { get ; }
  public abstract int PrimitiveCount { get; }
  public abstract GltfPrimitiveBase GetPrimitiveAt(int i);
}

[Serializable]
public abstract class GltfNodeBase {
  public string name;
  public Matrix4x4? matrix;

  // May return null
  public abstract GltfMeshBase Mesh { get; }
  public abstract IEnumerable<GltfNodeBase> Children { get; }
}

[Serializable]
public abstract class GltfSceneBase {
  public Dictionary<string, string> extras;

  public abstract IEnumerable<GltfNodeBase> Nodes { get; }
}

}
