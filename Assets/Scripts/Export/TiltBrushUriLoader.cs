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

using System.IO;

using TiltBrushToolkit;
using UnityEngine;

using ToolkitRawImage = TiltBrushToolkit.RawImage;

namespace TiltBrush {

public class TiltBrushUriLoader : IUriLoader {
  private string m_uriBase;
  private IUriLoader m_delegate;
  private bool m_loadImages;

  /// If loadImages=true, use a C# image loader which doesn't need to be run on
  /// the main thread (helps avoid hitching) but is much slower than Texture2D.LoadImageData.
  public TiltBrushUriLoader(string glbPath, string uriBase, bool loadImages) {
    m_loadImages = loadImages;
    m_uriBase = uriBase;
    m_delegate = new BufferedStreamLoader(glbPath, uriBase);
  }

  public IBufferReader Load(string uri) {
    // null uri means the binary chunk of a .glb
    uri = (uri == null) ? null : PolyRawAsset.GetPolySanitizedFilePath(uri);
    return m_delegate.Load(uri);
  }

  public bool CanLoadImages() { return m_loadImages; }

  public ToolkitRawImage LoadAsImage(string uri) {
    uri = PolyRawAsset.GetPolySanitizedFilePath(uri);
    string path = Path.Combine(m_uriBase, uri);
    RawImage rawImage = ImageUtils.FromImageData(File.ReadAllBytes(path), path);
    return new ToolkitRawImage {
        format = TextureFormat.RGBA32,
        colorData = rawImage.ColorData,
        colorWidth = rawImage.ColorWidth,
        colorHeight = rawImage.ColorHeight
    };
  }

#if UNITY_EDITOR
  public UnityEngine.Texture2D LoadAsAsset(string uri) {
    return m_delegate.LoadAsAsset(uri);
  }
#endif
}

} // namespace TiltBrush
