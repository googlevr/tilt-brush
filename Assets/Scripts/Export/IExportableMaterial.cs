// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {
[Serializable]
public enum ExportableMaterialBlendMode { None, AlphaMask, AdditiveBlend, AlphaBlend };

// This interface is the information needed by the export process to create a material.
public interface IExportableMaterial {
  /// This guid is guaranteed to be unique among IExportableMaterial instances in the export.
  ///
  /// If the IExportableMaterial refers to a material with an independent identity
  /// (ie, "Tilt Brush Light as of M14", "Blocks Glass"), this guid is additionally
  /// guaranteed to be durable: references to the "same" material will always have
  /// the same UniqueName.
  ///
  /// Do not call BrushCatalog.GetBrush() using this guid. Instead, if you want
  /// to know if the IEM is a BrushDescritor, try to cast it to BrushDescriptor.
  Guid UniqueName { get; }

  /// Durable, human-readable, but not unique.
  string DurableName { get; }

  ExportableMaterialBlendMode BlendMode { get; }

  float EmissiveFactor { get; }
  GeometryPool.VertexLayout VertexLayout { get; }

  // For fbx: true if this is a Brush and has a simplified albedo texture useful for
  // non-Toolkit DCC imports of the fbx. Thix texture overrides the ones in TextureUris.
  bool HasExportTexture();

  // For fbx: The filename of the albedo texture to copy into the fbx export
  string GetExportTextureFilename();

  // If UriBase is null, all Uris must be http(s?)://
  // If UriBase is non-null, it is a directory, and Uris are allowed to be non-http.
  // For dynamic exportable materials, this is the directory that holds all the textures.
  // For brushes, it's always null since brush textures are global http: uris.
  // This is a slightly misleading name since it's not a Uri itself.
  string UriBase { get; }

  // In practice, this is true for production brushes, false otherwise.
  // It requires exportManifest.json, which contains material information that we
  // can only introspect and extract at edit time.
  bool SupportsDetailedMaterialInfo { get; }

  // The following methods will fail with InvalidOperationException
  // if SupportsDetailedMaterialInfo == false.

  // https:// URIs to glsl shaders used by poly.google.com's renderer.
  // Probably only relevant to poly gltf1 exports
  string VertShaderUri { get; }
  string FragShaderUri { get; }
  bool EnableCull { get; }
  // For BrushDescriptor, these are all http://
  // For DynamicExportableMaterial, these are tiltbrush:// or files in UriBase.
  Dictionary<string, string> TextureUris { get; }

  // Probably only relevant to poly gltf1 exports
  Dictionary<string, Vector2> TextureSizes { get; }

  // Probably only relevant to poly gltf1 exports
  Dictionary<string, float> FloatParams { get; }
  Dictionary<string, Vector3> VectorParams { get; }
  Dictionary<string, Color> ColorParams { get; }
}

}  // namespace TiltBrush
