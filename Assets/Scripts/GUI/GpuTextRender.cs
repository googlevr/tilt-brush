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
// Text rendering that requires very low CPU overhead.
// Text is encoded into a texture, with one pixel per character. The shader uses the texture as a
// lookup into a font texture for rendering. There is an option to include up to 16 numerical
// values, which can be written to via Material.SetFloatArray(...). The pixel shader interprets
// the encoding in the texture and the array of floats to render numbers.
public class GpuTextRender : MonoBehaviour {
  [SerializeField] private Material m_Material;
  [SerializeField] private int m_Width;
  [SerializeField] private int m_Height;
  [SerializeField] [Multiline(16)] private string m_Text;

  private RenderTexture m_TargetTexture;
  private Texture2D m_SourceTexture;
  private float[] m_Data;

  public RenderTexture RenderedTexture {
    get { return m_TargetTexture; }
  }

  public void SetData(int index, float value) {
    m_Data[index] = value;
  }

  // Simple system for generating the GPU text texture, given a string.
  // Encoding is thus:
  //   For each pixel:
  //     Red byte   - ASCII encoding of character (forced uppercase)
  //     Green byte - Encoding of data index
  //     Blue byte  - digit of data index:
  //                - 128 is ones, 129 is 100s, 130 is 1000s etc
  //                - 127 is 0.1s, 126 is 0.01s etc
  //     Alpha      - nothing for now.
  // In the input string, a series of '~'s, optionally with a '.' in the
  // middle is interpreted as a data value.
  // Example:
  //   Position #~: ~~.~~~, ~~.~~~, ~~.~~~
  // contains four data sections. Currently up to 16 are supported.
  private void Awake() {
    Texture fontTexture = m_Material.GetTexture("_FontTex");
    int charWidth = fontTexture.width / m_Material.GetInt("_FontWidth");
    m_TargetTexture = new RenderTexture(m_Width * charWidth, m_Height * charWidth, 0);
    m_SourceTexture = new Texture2D(m_Width, m_Height, TextureFormat.RGBA32, false);
    m_SourceTexture.filterMode = FilterMode.Point;
    Color32[] pixels = new Color32[m_Width * m_Height];
    string[] lines = m_Text.Split('\n');
    byte dataIndex = 1;
    int? digit = null;
    for (int y = 0; y < lines.Length; ++y) {
      string line = lines[y];
      Color32 color;
      for (int x = 0; x < line.Length; ++x) {
        char c = char.ToUpper(line[x]);
        if (digit.HasValue) {
          if (c != '~' && c != '.') {
            digit = null;
            dataIndex++;
          }
        } else {
          if (c == '~') {
            for (digit = 1; digit + x < line.Length && line[digit.Value + x] == '~'; ++digit) {
              // loop
            }

            digit -= 1;
          }
        }
        if (digit.HasValue && c != '.') {
          color = new Color32(0, dataIndex, (byte) (128 + digit), 0);
          digit--;
        } else {
          color = new Color32((byte) c, 0, 0, 0);
        }

        pixels[x + (m_Height - y - 1) * m_Width] = color;
      }
    }
    m_SourceTexture.SetPixels32(pixels);
    m_SourceTexture.Apply(updateMipmaps: false);
    m_Material.mainTexture = m_SourceTexture;

    m_Data = new float[16];

  }

  private void LateUpdate() {
    m_Material.SetFloatArray("_Data", m_Data);
    Graphics.Blit(m_SourceTexture, m_TargetTexture, m_Material);
  }
}
} // namespace TiltBrush