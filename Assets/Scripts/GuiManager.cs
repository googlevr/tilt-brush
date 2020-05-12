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

namespace TiltBrush {
public class GuiManager : MonoBehaviour {
  public static GuiManager m_Instance;

  [SerializeField] private RectTransform m_Sidebar;
  [SerializeField] private GameObject m_Login;
  [SerializeField] private GameObject m_Profile;
  [SerializeField] private Image m_Image;
  [SerializeField] private Text m_Text;
  [SerializeField] private float m_SlideDuration = .5f;

  private enum SlideState {
    Hidden,
    MovingIn,
    Showing,
    MovingOut
  }

  private float m_SlideTimer;
  private SlideState m_SlideState;
  private float m_SidebarPosX;

  void Awake() {
    m_Instance = this;
    m_SidebarPosX = m_Sidebar.position.x;
    m_SlideState = SlideState.Hidden;
    m_Sidebar.position = new Vector2(-m_SidebarPosX, m_Sidebar.position.y);
    OAuth2Identity.ProfileUpdated += OnProfileUpdated;
    RefreshObjects();
  }

  void OnDestroy() {
    OAuth2Identity.ProfileUpdated -= OnProfileUpdated;
  }

  void Start() {
    m_Login.GetComponent<Button>().onClick.AddListener(App.GoogleIdentity.LoginAsync);
  }

  public void Show(bool show) {
    switch (m_SlideState) {
    case SlideState.Hidden:
    case SlideState.MovingOut:
      if (show) {
        m_SlideState = SlideState.MovingIn;
      }
      break;
    case SlideState.Showing:
    case SlideState.MovingIn:
      if (!show) {
        m_SlideState = SlideState.MovingOut;
      }
      break;
    }
  }

  void Update() {
    switch (m_SlideState) {
    case SlideState.MovingIn:
      m_SlideTimer += Time.deltaTime;
      if (m_SlideTimer >= m_SlideDuration) {
        m_SlideState = SlideState.Showing;
        m_SlideTimer = m_SlideDuration;
      }
      m_Sidebar.position = new Vector2(
        2 * m_SidebarPosX * m_SlideTimer / m_SlideDuration - m_SidebarPosX, m_Sidebar.position.y);
      break;
    case SlideState.MovingOut:
      m_SlideTimer -= Time.deltaTime;
      if (m_SlideTimer <= 0) {
        m_SlideState = SlideState.Hidden;
        m_SlideTimer = 0;
      }
      m_Sidebar.position = new Vector2(
        2 * m_SidebarPosX * m_SlideTimer / m_SlideDuration - m_SidebarPosX, m_Sidebar.position.y);
      break;
    }
  }

  void RefreshObjects() {
    OAuth2Identity.UserInfo userInfo = App.GoogleIdentity.Profile;
    if (userInfo != null) {
      Show(true);
      m_Login.SetActive(false);
      m_Profile.SetActive(true);
      m_Text.text = userInfo.name;
      Texture tex = userInfo.icon;
      if (tex != null) {
        m_Image.sprite = Sprite.Create((Texture2D)tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
        m_Image.material.SetTexture("_MainTex", tex);
      }
    } else {
      m_Profile.SetActive(false);
      m_Login.SetActive(true);
    }
  }

  void OnProfileUpdated(OAuth2Identity _) {
    RefreshObjects();
  }
}
} // namespace TiltBrush
