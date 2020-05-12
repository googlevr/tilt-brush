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
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

namespace TiltBrush {

public class BaseChatScript : MonoBehaviour {
  [SerializeField] protected TextMeshPro m_ChatText;
  [SerializeField] protected ScrollRect m_Scroll;
  [SerializeField] protected int m_NumLines;
  [SerializeField] protected int m_CharacterLineLimit = 70;

  protected List<string> m_Lines;

  readonly protected string m_Blue = "<color=#aaaaffff>";
  readonly protected string m_DarkGrey = "<color=#666666ff>";
  readonly protected string m_OffWhite = "<color=#ccccccff>";
  protected bool m_UseOffWhite;

  void Awake() {
    m_Lines = new List<string>();
    m_ChatText.parseCtrlCharacters = false;
  }

  protected void ClearChatLines() {
    m_Lines.Clear();
  }

  protected void AddLine(string s, string sRichFront = "", string sRichBack = "") {
    bool bAddedFront = false;

    //measure and chop up in to lines without mercy
    while (s.Length > m_CharacterLineLimit) {
      if (!bAddedFront) {
        m_Lines.Add(sRichFront + s.Substring(0, m_CharacterLineLimit));
        bAddedFront = true;
      } else {
        m_Lines.Add(s.Substring(0, m_CharacterLineLimit));
      }

      s = " " + s.Substring(m_CharacterLineLimit);
    }

    //store the final part of the string with the back rich text
    if (!bAddedFront) {
      m_Lines.Add(string.Concat(sRichFront, s, sRichBack));
    } else {
      m_Lines.Add(string.Concat(s, sRichBack));
    }
  }

  protected void RefreshChatText() {
    m_ChatText.text = "";
    while (m_Lines.Count > m_NumLines) {
      m_Lines.RemoveAt(0);
    }
    for (int i = 0; i < m_Lines.Count; ++i) {
      m_ChatText.text = string.Concat(m_ChatText.text, m_Lines[i], "\n");
    }
  }
}
}  // namespace TiltBrush
