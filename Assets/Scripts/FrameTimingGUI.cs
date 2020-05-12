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

public class FrameTimingGUI : MonoBehaviour {
  public Transform m_DroppedFramesBar;
  private Vector3 m_DroppedFramesBarBasePos;
  private Vector3 m_DroppedFramesBarBaseScale;
  public FrameTimingInfo m_TimingInfo;
  public Transform m_Message;

  void Awake() {
    m_DroppedFramesBarBasePos = m_DroppedFramesBar.localPosition;
    m_DroppedFramesBarBaseScale = m_DroppedFramesBar.localScale;
    m_DroppedFramesBar.parent.gameObject.SetActive(false);
  }

  void Update() {
    if (App.UserConfig.Flags.ShowDroppedFrames) {
      var rollingDroppedFrames = m_TimingInfo.RollingDroppedFrameCount;
      if (rollingDroppedFrames > 0) {
        //scale dropped frames bar according to rolling frame count
        float fFrameCount = (float)(Mathf.Min(rollingDroppedFrames, 9));
        float fFrameRatio = fFrameCount / 9.0f;
        Vector3 vScale = m_DroppedFramesBarBaseScale;
        vScale.x *= fFrameRatio;

        m_DroppedFramesBar.localScale = vScale;

        Vector3 vLocalPos = m_DroppedFramesBarBasePos;
        vLocalPos.x = -(m_DroppedFramesBarBaseScale.x * 0.5f) + (vScale.x * 0.5f);
        m_DroppedFramesBar.localPosition = vLocalPos;

        m_DroppedFramesBar.GetComponent<Renderer>().material.mainTextureScale =
          new Vector2(fFrameCount, 1);

        m_DroppedFramesBar.parent.gameObject.SetActive(true);
      } else {
        m_DroppedFramesBar.parent.gameObject.SetActive(false);
      }
    }
  }

  private Color SetAlpha(Color c, float a) {
    c.a = a;
    return c;
  }

  IEnumerator FadeMessage(string message) {
    // display for .5 second
    m_Message.GetComponent<TextMesh>().text = message;
    var material = m_Message.GetComponent<Renderer>().material;
    material.color = SetAlpha(material.color, 1f);
    yield return new WaitForSeconds(.5f);
    // fade out over .5 second
    while (material.color.a > 0) {
      material.color = SetAlpha(material.color,
                                Mathf.Clamp01(material.color.a - Time.deltaTime * 2));
      yield return null;
    }
  }
}
}  // namespace TiltBrush
