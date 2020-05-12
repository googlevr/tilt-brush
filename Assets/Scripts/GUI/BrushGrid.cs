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
  public class BrushGrid : UIComponent {
    // Inspector data

    [SerializeField] private GameObject m_PrevButton;
    [SerializeField] private GameObject m_NextButton;
    [SerializeField] private float m_PadSwipeGiveExponent = 0.5f;
    [SerializeField] private float m_PadSwipeGiveScale = 1.0f;
    [SerializeField] private float m_PadSwipeGiveMaxPercent = 0.5f;
    [SerializeField] private float m_PageFlipSpeed = 2f;
    [SerializeField] private float m_PageScrollRatio = 1.25f;

    // Internal data

    private UIComponentManager m_UIComponentManager;
    private BrushTypeButton[] m_BrushButtons;
    private int m_BaseBrushIndex;

    private float m_PageIndex;
    private float m_RequestedPageIndex;
    private int m_NumPages;
    private float m_PadSwipePerPage;
    private int m_PrevDirection = 0;
    private bool m_EatPadInput = false;

    // Time (in seconds) to sleep after flipping a page. Only used by thumb stick.
    private float m_PageFlipSleepTime = 0.25f;
    private float m_PageFlipSleepValue = 0f;

    // Small threshold value to prevent constant being resetting
    // when scrolling past the page boundaries.
    private float m_BoundaryThreshold = .01f;

    private Vector2 m_SwipeRecentMotion = Vector2.zero;
    private Vector2 m_AccumulatedSwipeMotion = Vector2.zero;
    private const float m_SwipeThreshold = 0.4f;

    const float NUM_COLS = 4;
    const float ANGLE_HIGHLIGHT_THRESHOLD = 5;

    override protected void Awake() {
      base.Awake();

      m_UIComponentManager = GetComponent<UIComponentManager>();

      BrushCatalog.m_Instance.BrushCatalogChanged += OnBrushCatalogChanged;
      BrushController.m_Instance.BrushChanged += OnBrushChanged;
      BrushController.m_Instance.BrushSetToDefault += OnBrushSetToDefault;
      BrushController.m_Instance.StrokeSelected += OnStrokeSelected;
      App.Switchboard.AudioReactiveStateChanged += OnAudioReactiveStateChanged;

      // Cache brush buttons.
      m_BrushButtons = GetComponentsInChildren<BrushTypeButton>();

      // Start the brushes on Page 2
      m_PageIndex = 1;
      m_BaseBrushIndex = (int)m_PageIndex * m_BrushButtons.Length;
    }

    override public void SetColor(Color color) {
      base.SetColor(color);
      m_UIComponentManager.SetColor(color);
    }

    override public void UpdateVisuals() {
      base.UpdateVisuals();
      m_UIComponentManager.UpdateVisuals();
    }

    override public void HasFocus(RaycastHit hitInfo) {
      base.HasFocus(hitInfo);

      if (m_EatPadInput && !(InputManager.Brush.GetPadTouch()
                             || InputManager.Brush.GetThumbStickTouch())) {
        m_EatPadInput = false;
      }

      if (!m_EatPadInput) {
        int currDirection = AccumulateSwipe();
        // When using the analog stick, don't accumulate displacement, instead map the stick rotation
        // directly to the button rotation, which feels much better.
        if (App.VrSdk.AnalogIsStick(InputManager.ControllerName.Brush)) {
          currDirection = 0;
          if (m_PageFlipSleepValue > 0) {
            m_PageFlipSleepValue -= Time.deltaTime;
          } else {
            float v = InputManager.m_Instance.GetBrushScrollAmount();
            // Ignore values near zero.
            if (Mathf.Abs(v) > 0.01) {
              // currDirection is always -1,0, or 1.
              currDirection = (int)Mathf.Sign(v);
            }
          }
        }

        // Only snap the page if the finger was lifted from the trackpad.
        if (m_PrevDirection != 0 && currDirection == 0) {
          SnapPage();
        } else {
          AdvancePage(currDirection * Mathf.Abs(InputManager.m_Instance.GetBrushScrollAmount()));
        }

        m_PrevDirection = currDirection;
      }
    }

    override public bool UpdateStateWithInput(bool inputValid, Ray inputRay,
          GameObject parentActiveObject, Collider parentCollider) {
      if (base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider)) {
        if (parentActiveObject == null || parentActiveObject == gameObject) {
          if (BasePanel.DoesRayHitCollider(inputRay, GetCollider())) {
            m_UIComponentManager.UpdateUIComponents(inputRay, inputValid, parentCollider);
            return true;
          }
        }
      }
      return false;
    }

    override public void ResetState() {
      base.ResetState();
      SnapPage();
      m_UIComponentManager.Deactivate();
    }

    override public void ReceiveMessage(ComponentMessage type, int param) {
      switch (type) {
      case UIComponent.ComponentMessage.NextPage: AdvancePage(1); break;
      case UIComponent.ComponentMessage.PrevPage: AdvancePage(-1); break;
      case UIComponent.ComponentMessage.GotoPage: GotoPage(param); break;
      }
    }

    override public void AssignControllerMaterials(InputManager.ControllerName controller) {
      if (controller != InputManager.ControllerName.Brush) {
        return;
      }
      // Swipe to change pages isn't supported by logitech pen.
      if (App.VrSdk.VrControls.Brush.ControllerGeometry.Style == ControllerStyle.LogitechPen) {
        return;
      }
      InputManager.Brush.Geometry.ShowBrushPage(!m_EatPadInput);
    }

    override public float GetControllerPadShaderRatio(InputManager.ControllerName controller) {
      if (controller == InputManager.ControllerName.Brush) {
        // Swipe to change pages isn't supported by logitech pen.
        if (App.VrSdk.VrControls.Brush.ControllerGeometry.Style == ControllerStyle.LogitechPen) {
          return 0.0f;
        }

        // Return the swipe amount through all pages.
        float accumulatedSwipeRatio = GetAccumulatedSwipeRatio();
        float fSwipeRatio = GetSwipeRatio();

        // Add a little give on the boundaries
        float fAdjustedGiveScale = m_PadSwipeGiveScale / m_PadSwipePerPage;
        float fMaxAmount = m_PadSwipePerPage * m_PadSwipeGiveMaxPercent;
        float fOneMinusPageAmount = 1.0f - m_PadSwipePerPage;
        if (m_RequestedPageIndex < m_BoundaryThreshold && accumulatedSwipeRatio < 0.0f) {
          // Exponential decay give on the low end.
          float fDiff = Mathf.Min(Mathf.Abs(accumulatedSwipeRatio), fMaxAmount);
          float fDiffScale = Mathf.Pow(m_PadSwipeGiveExponent, fDiff * fAdjustedGiveScale);
          fSwipeRatio = -(fDiff * fDiffScale);
        } else if (m_RequestedPageIndex > m_NumPages - 1 - m_BoundaryThreshold && accumulatedSwipeRatio > fOneMinusPageAmount) {
          // Exponential decay give on the high end.
          float fDiff = Mathf.Min(accumulatedSwipeRatio - fOneMinusPageAmount, fMaxAmount);
          float fDiffScale = Mathf.Pow(m_PadSwipeGiveExponent, fDiff * fAdjustedGiveScale);
          fSwipeRatio = fOneMinusPageAmount + (fDiff * fDiffScale);
        }

        return fSwipeRatio;
      }
      return 0.0f;
    }

    override public bool BrushPadAnimatesOnHover() {
      return App.VrSdk.VrControls.Brush.ControllerGeometry.Style != ControllerStyle.LogitechPen;
    }

    void CountPages() {
      m_NumPages = ((BrushCatalog.m_Instance.GuiBrushList.Count - 1) / m_BrushButtons.Length) + 1;

      float pages = (float)m_NumPages;
      ControllerMaterialCatalog.m_Instance.BrushPage.SetFloat("_UsedIconCount", pages);
      ControllerMaterialCatalog.m_Instance.BrushPageActive.SetFloat("_UsedIconCount", pages);
      ControllerMaterialCatalog.m_Instance.BrushPageActive_LogitechPen.SetFloat(
          "_UsedIconCount", pages);

      m_PadSwipePerPage = 1.0f / (float)m_NumPages;
    }

    public void RefreshNavigationButtons() {
      // Refresh the navigation buttons.
      m_PrevButton.SetActive(m_PageIndex > 0 + m_BoundaryThreshold);
      m_NextButton.SetActive(m_PageIndex < m_NumPages - 1 - m_BoundaryThreshold);
    }

    void RefreshButtonSelection() {
      BrushDescriptor activeBrush = BrushController.m_Instance.ActiveBrush;
      Debug.Assert(activeBrush != null);
      for (int i = 0; i < m_BrushButtons.Length; ++i) {
        int iBrushIndex = m_BaseBrushIndex + i;

        //show icon according to availability
        if (iBrushIndex < BrushCatalog.m_Instance.GuiBrushList.Count) {
          BrushDescriptor rBrush = BrushCatalog.m_Instance.GuiBrushList[iBrushIndex];
          m_BrushButtons[i].SetButtonSelected(rBrush == activeBrush);
        }
      }
    }

    void OnStrokeSelected(Stroke stroke) {
      RefreshButtonSelection();
    }

    void OnBrushSetToDefault() {
      m_PageIndex = 1;
      m_RequestedPageIndex = 1;

      RefreshButtonSelection();
      RefreshButtonPositions();
    }

    void OnAudioReactiveStateChanged() {
      RefreshButtonProperties();
    }

    void OnBrushChanged(BrushDescriptor brush) {
      RefreshButtonSelection();
    }

    // Called when the brush catalog tells us brushes have been loaded.
    // Since we cache brush textures, we need to deal with them getting Destroy()ed etc.
    void OnBrushCatalogChanged() {
      CountPages();
      RefreshButtonPositions();
    }

    // TODO : See if we can make BrushGrid not use Update.
    void Update() {
      float prevPageIndex = m_PageIndex;
      if (m_PageIndex < m_RequestedPageIndex) {
        m_PageIndex += Time.deltaTime * m_PageFlipSpeed;
        m_PageIndex = Mathf.Clamp(m_PageIndex, 0, m_NumPages - 1);

        if (m_PageIndex > m_RequestedPageIndex) {
          m_PageIndex = m_RequestedPageIndex;
        }

        RefreshButtonPositions();
      } else if (m_PageIndex > m_RequestedPageIndex) {
        m_PageIndex -= Time.deltaTime * m_PageFlipSpeed;
        m_PageIndex = Mathf.Clamp(m_PageIndex, 0, m_NumPages - 1);

        if (m_PageIndex < m_RequestedPageIndex) {
          m_PageIndex = m_RequestedPageIndex;
        }

        RefreshButtonPositions();
      }

      // Only trigger haptics when we cross the page boundary.
      if (Mathf.RoundToInt(m_PageIndex) != Mathf.RoundToInt(prevPageIndex)) {
        InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
        m_PageFlipSleepValue = m_PageFlipSleepTime;
      }
    }

    // This takes sets the page state to m_PageIndex.
    void RefreshButtonPositions() {
      float translationAmount = Mathf.Clamp(Mathf.Abs(m_PageIndex - Mathf.Floor(m_PageIndex)), 0, 1);

      float threshold = Mathf.Abs(m_BrushButtons[0].m_OriginPosition.x);
      float buttonSizeX = m_BrushButtons[0].transform.localScale.x;
      float buttonSpacingX = Mathf.Abs(m_BrushButtons[0].m_OriginPosition.x - m_BrushButtons[1].m_OriginPosition.x) -
        buttonSizeX;
      float translationDistance = -(buttonSizeX + buttonSpacingX) * NUM_COLS;
      float rotationAngle = 90;
      for (int i = 0; i < m_BrushButtons.Length; i++) {
        Vector3 updateVector = m_BrushButtons[i].m_OriginPosition + new Vector3(translationDistance, 0, 0);
        Vector3 updatedPosition = Vector3.Lerp(m_BrushButtons[i].m_OriginPosition, updateVector, translationAmount);

        if (updatedPosition.x <= translationDistance / 2f) {
          updateVector.x = m_BrushButtons[i].m_OriginPosition.x - translationDistance;
          updatedPosition = Vector3.Lerp(updateVector, m_BrushButtons[i].m_OriginPosition, translationAmount);
        }

        Quaternion updatedRotation = Quaternion.identity;
        float rotationAmount = Mathf.Abs(Mathf.Abs(updatedPosition.x) - threshold) /
          (Mathf.Abs(translationDistance / 2f) - threshold);

        if (updatedPosition.x < (-threshold)) {
          updatedRotation =
            Quaternion.Slerp(Quaternion.Euler(0, 0, 0), Quaternion.Euler(0, rotationAngle, 0), (rotationAmount));
          updateVector =
            new Vector3(updatedPosition.x, m_BrushButtons[i].m_OriginPosition.y, m_BrushButtons[i].m_OriginPosition.z + .2f);
          updatedPosition = Vector3.Lerp(updatedPosition, updateVector, rotationAmount);
        } else if (updatedPosition.x > (threshold)) {
          updatedRotation =
            Quaternion.Slerp(Quaternion.Euler(0, -rotationAngle, 0), Quaternion.Euler(0, 0, 0), (1 - rotationAmount));
          updateVector =
            new Vector3(updatedPosition.x, m_BrushButtons[i].m_OriginPosition.y, m_BrushButtons[i].m_OriginPosition.z + .2f);
          updatedPosition = Vector3.Lerp(updateVector, updatedPosition, 1 - rotationAmount);
        }

        m_BrushButtons[i].transform.localPosition = updatedPosition;
        m_BrushButtons[i].transform.localRotation = updatedRotation;
      }
      RefreshButtonProperties();
      RefreshNavigationButtons();
    }

    // Updates the displayed brush on each button based on where it's displayed at.
    void RefreshButtonProperties() {
      for (int i = 0; i < m_BrushButtons.Length; i++) {
        int pageIndex = 0;
        if (m_BrushButtons[i].transform.localPosition.x >= m_BrushButtons[i].m_OriginPosition.x) {
          pageIndex = Mathf.CeilToInt(m_PageIndex); //using ceiling value
        } else {
          pageIndex = Mathf.FloorToInt(m_PageIndex); //using floor value
        }

        m_BaseBrushIndex = 0;
        int numBrushesInBrushList = BrushCatalog.m_Instance.GuiBrushList.Count;
        int iNumBrushesPerPage = m_BrushButtons.Length;
        int iPageWalk = 0;
        int iBrushCountWalk = numBrushesInBrushList;
        while (iBrushCountWalk > 0) {
          if (iPageWalk == pageIndex) {
            m_BaseBrushIndex = numBrushesInBrushList - iBrushCountWalk;
            break;
          }

          iBrushCountWalk -= iNumBrushesPerPage;
          ++iPageWalk;
        }

        int iBrushIndex = m_BaseBrushIndex + i;
        // Display the brush icon according to brush availability.
        if (iBrushIndex < numBrushesInBrushList) {
          if (!m_BrushButtons[i].IsHover()) {
            BrushDescriptor rBrush = BrushCatalog.m_Instance.GuiBrushList[iBrushIndex];
            m_BrushButtons[i].SetButtonProperties(rBrush);
            m_BrushButtons[i].SetButtonSelected(rBrush == BrushController.m_Instance.ActiveBrush);
            m_BrushButtons[i].gameObject.SetActive(true);
          }
        } else {
          m_BrushButtons[i].gameObject.SetActive(false);
        }
      }
    }

    // Snaps the page number to the nearest integer page number.
    void SnapPage() {
      m_RequestedPageIndex = Mathf.RoundToInt(m_PageIndex);
      m_AccumulatedSwipeMotion.x = 0f;
    }

    /// Accumulates dpad input, and returns nonzero for a discrete swipe action.
    /// Return value is 1 for "backward" swipe moving a page forward
    /// and -1 for "forward" swipe moving a page backward.
    int AccumulateSwipe() {
      int direction = 0;
      if (InputManager.m_Instance.IsBrushScrollActive() &&
          App.VrSdk.VrControls.Brush.ControllerGeometry.Style != ControllerStyle.LogitechPen) {
        // If our delta is beyond our trigger threshold, report it.
        float fDelta = InputManager.m_Instance.GetAdjustedBrushScrollAmount();
        if (IncrementMotionAndCheckForSwipe(fDelta)) {
          direction = (int)Mathf.Sign(m_SwipeRecentMotion.x) * -1;
          m_SwipeRecentMotion.x = 0.0f;
        }
      } else {
        m_SwipeRecentMotion.x = 0.0f;
      }

      return direction;
    }

    bool IncrementMotionAndCheckForSwipe(float fMotion) {
      m_SwipeRecentMotion.x += fMotion * m_PageScrollRatio *
          App.VrSdk.SwipeScaleAdjustment(InputManager.ControllerName.Brush);

      // Keep our motion clamped to values showing visible motion.
      float fClampAmount = m_PadSwipeGiveMaxPercent * m_SwipeThreshold;
      if (m_RequestedPageIndex == 0 && m_SwipeRecentMotion.x > fClampAmount) {
        m_SwipeRecentMotion.x = fClampAmount;
      } else if (m_RequestedPageIndex == m_NumPages - 1 && m_SwipeRecentMotion.x < -fClampAmount) {
        m_SwipeRecentMotion.x = -fClampAmount;
      }

      m_AccumulatedSwipeMotion.x += m_SwipeRecentMotion.x;
      // Don't report a swipe if we're beyond the low or high boundaries on the extremes.
      float fSwipeRatio = GetSwipeRatio();
      return (fSwipeRatio > 0.0f || m_RequestedPageIndex < m_NumPages - 1) &&
          (fSwipeRatio < 1.0f - m_PadSwipePerPage || m_RequestedPageIndex > 0);
    }

    float GetSwipeRatio(float motion) {
      float fIndexPercent = (float)m_RequestedPageIndex * m_PadSwipePerPage;
      float fTouchAdjust = m_PadSwipePerPage * ((motion * -1f) / m_SwipeThreshold);
      return fIndexPercent + fTouchAdjust;
    }

    float GetSwipeRatio() {
      return GetSwipeRatio(m_SwipeRecentMotion.x);
    }

    float GetAccumulatedSwipeRatio() {
      return GetSwipeRatio(m_AccumulatedSwipeMotion.x);
    }

    public void GotoPage(int page) {
      m_RequestedPageIndex = Mathf.Clamp(page, 0, m_NumPages - 1);
    }

    // Called when NavButtons are pressed to flip brush pages.
    public void AdvancePage(int iAmount) {
      if (iAmount == 0) {
        return;
      }
      if (m_EatPadInput) {
        if (iAmount == -1) {
          m_AccumulatedSwipeMotion.x -= m_PageIndex - (int)m_PageIndex;
          m_RequestedPageIndex = Mathf.Ceil(m_PageIndex);
        } else if (iAmount == 1) {
          m_AccumulatedSwipeMotion.x += (int)(m_PageIndex + 1) - m_PageIndex;
          m_RequestedPageIndex = Mathf.Floor(m_PageIndex);
        }
      }
      m_RequestedPageIndex = Mathf.Clamp(m_RequestedPageIndex + iAmount, 0, m_NumPages - 1);
    }

    // Called when joystick/pad is used to scroll brush pages.
    void AdvancePage(float amount) {
      m_RequestedPageIndex = Mathf.Clamp(m_RequestedPageIndex + amount, 0, m_NumPages - 1);
    }
  }
} // namespace TiltBrush
