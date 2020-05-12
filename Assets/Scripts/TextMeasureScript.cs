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
using System;
using System.Collections.Generic;

namespace TiltBrush {

//This script is written to be attached to an unused, global TextMesh object for the
//  purposes of measuring the real-world length of a rendered string.  In order to
//  measure, we first need to render the string.  After a unique length query has
//  been requested, the result is stored in a dictionary for easy, repeatable access.
public class TextMeasureScript : MonoBehaviour {
  static public TextMeasureScript m_Instance;
  private TextMesh m_TextMesh;
  private Renderer m_TextRenderer;

  //dictionary key-- override IEquatable to cut down on GC and increase speed
  private struct TextParams : IEquatable<TextParams> {
    public float m_CharSize;
    public int m_FontSize;
    public Font m_Font;
    public string m_Text;

    public bool Equals(TextParams other) {
      return (m_CharSize == other.m_CharSize) && (m_FontSize == other.m_FontSize) &&
        m_Text.Equals(other.m_Text) && m_Font.name.Equals(other.m_Font.name);
    }
    public override bool Equals(object other) {
      if (!(other is TextParams)) {
        return false;
      }
      return Equals((TextParams)other);
    }
    public override int GetHashCode() {
      return m_CharSize.GetHashCode() ^ m_FontSize.GetHashCode() ^ m_Text.GetHashCode();
    }
    public static bool operator ==(TextParams a, TextParams b) {
      return a.m_CharSize == b.m_CharSize && a.m_FontSize == b.m_FontSize &&
        a.m_Text.Equals(b.m_Text) && a.m_Font.name.Equals(b.m_Font.name);
    }
    public static bool operator !=(TextParams a, TextParams b) {
      return a.m_CharSize == b.m_CharSize && a.m_FontSize == b.m_FontSize &&
        a.m_Text.Equals(b.m_Text) && a.m_Font.name.Equals(b.m_Font.name);
    }
  }
  private Dictionary<TextParams, Vector2> m_StringSizeMap;

  void Awake() {
    m_Instance = this;
    m_TextMesh = GetComponent<TextMesh>();
    m_TextRenderer = GetComponent<Renderer>();
    m_StringSizeMap = new Dictionary<TextParams, Vector2>();
  }

  static public float GetTextWidth(TextMesh text) {
    return m_Instance.GetTextWidth(text.characterSize, text.fontSize, text.font, text.text);
  }

  static public float GetTextHeight(TextMesh text) {
    return m_Instance.GetTextHeight(text.characterSize, text.fontSize, text.font, text.text);
  }

  public float GetTextWidth(float fCharSize, int iFontSize, Font rFont, string sText) {
    //look for this string in the dictionary first
    TextParams rParams = new TextParams {
      m_CharSize = fCharSize,
      m_FontSize = iFontSize,
      m_Font = rFont,
      m_Text = sText
    };

    if (m_StringSizeMap.ContainsKey(rParams)) {
      return m_StringSizeMap[rParams].x;
    }

    //add new string to our map
    Vector2 vSize = AddNewString(rParams, fCharSize, iFontSize, rFont, sText);
    return vSize.x;
  }

  public float GetTextHeight(float fCharSize, int iFontSize, Font rFont, string sText) {
    //look for this string in the dictionary first
    TextParams rParams = new TextParams {
      m_CharSize = fCharSize,
      m_FontSize = iFontSize,
      m_Font = rFont,
      m_Text = sText
    };

    if (m_StringSizeMap.ContainsKey(rParams)) {
      return m_StringSizeMap[rParams].y;
    }

    //add new string to our map
    Vector2 vSize = AddNewString(rParams, fCharSize, iFontSize, rFont, sText);
    return vSize.y;
  }

  Vector2 AddNewString(TextParams rParams, float fCharSize, int iFontSize, Font rFont, string sText) {
    m_TextMesh.characterSize = fCharSize;
    m_TextMesh.fontSize = iFontSize;
    m_TextMesh.font = rFont;
    m_TextMesh.text = sText;
    Vector2 vSize = new Vector2(m_TextRenderer.bounds.size.x, m_TextRenderer.bounds.size.y);
    m_StringSizeMap.Add(rParams, vSize);
    return vSize;
  }
}
}  // namespace TiltBrush
