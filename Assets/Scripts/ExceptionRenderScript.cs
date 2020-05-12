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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;

namespace TiltBrush {

public class ExceptionRenderScript : MonoBehaviour {
  [SerializeField] private TextMeshPro m_TextMesh;
  [SerializeField] private int m_MaxNumLines;

  public class ExceptionLine {
    public string m_Condition;
    public string m_StackTrace;
    public float m_Lifetime;
  }
  private List<ExceptionLine> m_Lines;
  private float m_LineLifetime = 8.0f;

  void Awake() {
    m_Lines = new List<ExceptionLine>();
    m_TextMesh.parseCtrlCharacters = false;

    // Turn off if we're not in a debug build.
    if (!Debug.isDebugBuild) {
      gameObject.SetActive(false);
    } else {
      Application.logMessageReceived += HandleException;
    }
  }

  void Update() {
    // Tick down timers and remove dead lines.
    for (int i = 0; i < m_Lines.Count; ++i) {
      m_Lines[i].m_Lifetime -= Time.deltaTime;
    }
    bool bSomethingChanged = false;
    while (m_Lines.Count > 0 && m_Lines[0].m_Lifetime <= 0.0f) {
      m_Lines.RemoveAt(0);
      bSomethingChanged = true;
    }
    if (bSomethingChanged) {
      UpdateMeshText();
    }
  }

  void LateUpdate() {
    var parentFwd = transform.parent.forward;
    parentFwd.y = 0;
    parentFwd = parentFwd.normalized;

    transform.rotation = Quaternion.LookRotation(parentFwd, Vector3.up);
  }

  /// Returns a smaller, easier-to-read-in-VR stack trace
  public static string StreamlineStackTrace(string stackTrace) {
    Regex rgx = new Regex(
        @"^
          (?<namespace> \w+ \. )* (?<class> \w+) : (?<method> \w+)
          (?<params> \( [^)]* \) )
          (  # The ' (at blah:345)' part is optional
            \s+ \(
              at \s (?<filename> [^:] +) : (?<linenum> \d*)
            \)
          ) ?",
        RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);
    return rgx.Replace(stackTrace, @"${class}.${method} ${linenum}");
  }

  // namespace.class:function(param, param) (at path/to/file.cs:NNN)
  void HandleException(string condition, string stackTrace, LogType type) {
    // Update our render mesh to reflect this exception.
    if (type == LogType.Exception || type == LogType.Assert) {
      // This comes from a Unity bug
      if (condition == "IsFinite(outDistanceForSort)") {
        return;
      }

      // Add new line and make sure we're not over our total amount.
      ExceptionLine newLine = new ExceptionLine();
      newLine.m_Condition = condition;
      newLine.m_StackTrace = StreamlineStackTrace(stackTrace);
      newLine.m_Lifetime = m_LineLifetime;

      m_Lines.Add(newLine);
      while (m_Lines.Count > m_MaxNumLines) {
        m_Lines.RemoveAt(0);
      }

      // Update text mesh to render all lines.
      UpdateMeshText();
    }
  }

  void UpdateMeshText() {
    m_TextMesh.text = "";
    for (int i = 0; i < m_Lines.Count; ++i) {
      m_TextMesh.text += "<color=#ff8080>";
      m_TextMesh.text += m_Lines[i].m_Condition;
      m_TextMesh.text += "</color>\n";
      m_TextMesh.text += m_Lines[i].m_StackTrace;
    }
  }
}
}  // namespace TiltBrush
