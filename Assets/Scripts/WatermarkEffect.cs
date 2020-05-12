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

using UnityEngine;

namespace TiltBrush {

[ExecuteInEditMode]
[AddComponentMenu("Tilt/Watermark")]
public class WatermarkEffect : MonoBehaviour {
  /// If you use the default shader, this should have pre-multiplied alpha
  public Texture m_overlayTexture;

  /// Destination UV of the bottom-left corner of the watermark.
  /// Keep at (0,0) to pin to the bottom-left of the target RenderTexture.
  public Vector2 m_uvDestination = Vector2.zero;

  /// Height of watermark, as a %age of the shorter axis of the destination.
  /// This makes the watermark visually the same size whether the dest is
  /// in portrait or landscape.
  public float m_Size = 0.2f;
  
  public Shader   shader;
  private Material m_Material;
  
  protected virtual void Start () {
    Reset();
    if (m_Material == null) {
      m_Material = new Material (shader);
    }
  }

  void Reset() {
#if UNITY_EDITOR
    if (shader == null) {
      string path = UnityEditor.AssetDatabase.GUIDToAssetPath("35c384d563e634c38bb88d16cf788ab5");
      shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(path);
    }
#endif
  }

  // Called by camera to apply image effect
  void OnRenderImage(RenderTexture source, RenderTexture destination) {
    if (CameraConfig.Watermark) {
      float pixelHeight = m_Size * Mathf.Min(source.width, source.height);
      float pixelWidth = pixelHeight / m_overlayTexture.height * m_overlayTexture.width;
      var uvSize = new Vector2(pixelWidth  / source.width,
                               pixelHeight / source.height);

      Vector4 uvMax = m_uvDestination + uvSize;
      Vector4 range = new Vector4(
          m_uvDestination[0], m_uvDestination[1],
          uvMax[0], uvMax[1]);

      m_Material.SetVector("_OverlayUvRange", range);
      m_Material.SetTexture("_OverlayTex", m_overlayTexture);
      Graphics.Blit(source, destination, m_Material);
    } else {
      Graphics.Blit(source, destination);
    }
  }
}
}  // namespace TiltBrush
