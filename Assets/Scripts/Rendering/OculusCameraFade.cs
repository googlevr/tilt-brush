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
using System.Collections;

namespace TiltBrush {

/// Controls Fading in and out the view on Oculus VR
public class OculusCameraFade : MonoBehaviour {
  public static OculusCameraFade m_Instance;

  [SerializeField] private Material m_FadeMaterial;
  private bool m_IsFading = false;

  // Use this for initialization
  void Awake() {
    m_Instance = this;

    m_FadeMaterial = new Material(Shader.Find("Oculus/Unlit Transparent Color"));
  }

  public void Fade(bool fadeIn, float fadeTime) {
    if (!m_IsFading) {
      StartCoroutine(FadeColor(fadeIn, fadeTime));
    }
  }

  private IEnumerator FadeColor(bool fadeIn, float fadeTime) {
    m_IsFading = true;

    Color finalFadeColor;
    Color initialFadeColor;
    if (fadeIn)  {
      finalFadeColor = new Color(0f, 0f, 0f, 0f);
    } else {
      finalFadeColor = Color.black;
    }

    float timer = 0f;

    initialFadeColor = m_FadeMaterial.color;

    while (timer < fadeTime) {
      m_FadeMaterial.color = Color.Lerp(initialFadeColor, finalFadeColor, timer / fadeTime);
      timer += Time.deltaTime;
      yield return null;
    }
    m_FadeMaterial.color = finalFadeColor;

    m_IsFading = false;
  }

  void OnPostRender() {
    if (m_IsFading) {
      m_FadeMaterial.SetPass(0);
      GL.PushMatrix();
      GL.LoadOrtho();
      GL.Color(m_FadeMaterial.color);
      GL.Begin(GL.QUADS);
      GL.Vertex3(0f, 0f, -12f);
      GL.Vertex3(0f, 1f, -12f);
      GL.Vertex3(1f, 1f, -12f);
      GL.Vertex3(1f, 0f, -12f);
      GL.End();
      GL.PopMatrix();
    }
  }
}
} // namespace TiltBrush
