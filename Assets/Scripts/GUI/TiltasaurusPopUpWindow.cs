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

using System.Collections;
using UnityEngine;

namespace TiltBrush {

public class TiltasaurusPopUpWindow : PopUpWindow {
  const string kNoPeeking = @"        Tiltasaurus says...
                              No Peeking!
     Category: {0}";

  [SerializeField] private TextMesh m_DrawingPrompt;
  [SerializeField] private TextMesh m_Category;
  [SerializeField] private float m_DrawingPromptMaxWidth; // meters
  [SerializeField] private GameObject m_NoPeekingCameraPrefab;
  private GameObject m_NoPeekingCamera;

  override public void Init(GameObject rParent, string sText) {
    base.Init(rParent, sText);
    if (App.VrSdk.GetHmdDof() == VrSdk.DoF.Six) {
      m_NoPeekingCamera = Instantiate(m_NoPeekingCameraPrefab);
    }

    RefreshDrawingPrompt();
  }

  override protected void DestroyPopUpWindow() {
    if (m_NoPeekingCamera != null) {
      Destroy(m_NoPeekingCamera);
    }
    base.DestroyPopUpWindow();
  }

  public void RefreshWord() {
    StartCoroutine(Refresh());
  }

  private IEnumerator Refresh() {
    if (SketchControlsScript.m_Instance.SketchHasChanges()) {
      if (SaveLoadScript.m_Instance.IsSavingAllowed() &&
          SketchMemoryScript.m_Instance.IsMemoryDirty()) {
        // Save sketch in Tiltasaurus mode.
        SketchControlsScript.m_Instance.IssueGlobalCommand(
          SketchControlsScript.GlobalCommands.Save, iParam2: 1);

        // We need to wait for the app to stop saving before we clear the sketch. App state will not
        // reflect that it's in the saving state until the next frame, which is the reason for the
        // do/while rather than the more common while/do.
        do {
          yield return null;
        } while (App.CurrentState == App.AppState.Saving);
      }
      SketchControlsScript.m_Instance.IssueGlobalCommand(
        SketchControlsScript.GlobalCommands.NewSketch);
    }
    Tiltasaurus.m_Instance.ChooseNewPrompt();
    RefreshDrawingPrompt();
  }

  // maxWidth is in meters
  static void SetTextResize(string text, TextMesh dest, float maxWidth) {
    maxWidth = maxWidth * App.METERS_TO_UNITS;
    dest.text = text;
    float fTextWidth = TextMeasureScript.GetTextWidth(dest);
    float ratio = Mathf.Min(1, maxWidth / fTextWidth);
    dest.transform.localScale = Vector3.one * ratio;
  }

  void RefreshDrawingPrompt() {
    string sPrompt = Tiltasaurus.m_Instance.Prompt;
    if (sPrompt == null) { sPrompt = "nothing! (Out of ideas)"; }
    SetTextResize(sPrompt, m_DrawingPrompt, m_DrawingPromptMaxWidth);

    string sCategory = Tiltasaurus.m_Instance.Category;
    if (sCategory == null) {
      sCategory = "";
    } else {
      sCategory = string.Format("({0})", sCategory);
    }
    SetTextResize(sCategory, m_Category, m_DrawingPromptMaxWidth);

    if (m_NoPeekingCamera != null) {
      var categoryTrans = m_NoPeekingCamera.transform.Find("Category");
      var categoryMesh = categoryTrans.GetComponent<TextMesh>();
      categoryMesh.text = sCategory;
    }
  }
}
}  // namespace TiltBrush
