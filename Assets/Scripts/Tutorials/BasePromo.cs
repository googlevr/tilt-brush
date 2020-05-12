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

namespace TiltBrush {

public enum PromoType {
  BrushSize,
  TriggerToPin,
  FloatingPanel,
  ShareSketch,
  Selection,
  Duplicate,
  InteractIntroPanel,
  TriggerToUnpin,
  SaveIcon,
  Deselection,
  AdvancedPanels,
}

/// Subclasses of BasePromo represent a tutorialized series of actions for the user to take.
/// - At most one promo runs at any given moment. This is guaranteed by PromoManager.
/// - Promos are requested from the PromoManager when certain actions are taken, which then
///   instantiates and monitors them.
/// - Requesting a promo adds it to a set of promos whose requests are monitored by PromoManager.
///   It is not guaranteed to show at any specific moment if there is a competing promo.
/// - Once requested, promos handle their own internal state and have no ties to the requester.
///
/// Lifecyle of a Promo:
/// 1. A type of promo is requested from PromoManager, which instantiates the appriate one.
/// 2. Promo is idle until until it determines that it is ready to be displayed, at which point
///    the promo sets its request to display to notify PromoManager.
/// 3. When PromoManager is not displaying any promo, it picks one that has the is requesting to
///    display and displays it.
/// 4. If an active promo decides it shouldn't be shown anymore, it requests to hide to notify
///    the PromoManager to hide it. Hidden promos are hidden because of a temporary state that
///    conflicts with the visuals of the promo being shown.
/// 5. Steps 3 and 4 are repeated until a completion condition is met, at which point the promo
///    is hidden and removed from PromoManager's set of managed promos. Promos that are completed
///    are never shown again unless their keys in PlayerPrefs are erased.
public abstract class BasePromo {

  protected enum RequestingState {
    None,
    ToDisplay,
    ToHide
  }

  private PromoType m_PromoType;
  protected HintObjectScript m_HintObject;
  protected RequestingState m_Request;

  public PromoType PromoType { get { return m_PromoType; } }
  public abstract string PrefsKey { get; }
  public bool ReadyToDisplay { get { return m_Request == RequestingState.ToDisplay; } }
  public bool ShouldBeHidden { get { return m_Request == RequestingState.ToHide; } }

  public BasePromo(PromoType type) {
    m_PromoType = type;
  }

  /// Initiates all visuals for this promo.
  /// Only by PromoManager should call this method to ensure only one shows at a time.
  public void Display() {
    m_Request = RequestingState.None;
    if (m_HintObject) { m_HintObject.Activate(true); }
    OnDisplay();
  }

  /// Override in subclasses to show any specialized visuals.
  protected virtual void OnDisplay() { }

  /// Returns this promo to a dormant state. Should disable all visuals.
  /// Only called by PromoManager.
  public void Hide() {
    m_Request = RequestingState.None;
    if (m_HintObject) { m_HintObject.Activate(false); }
    OnHide();
  }

  /// Overrid in subclasses to hide any specialized visuals.
  protected virtual void OnHide() { }

  /// Called every frame by PromoManager when dormant.
  public virtual void OnIdle() { }

  /// Called every frame by PromoManager when displayed.
  public virtual void OnActive() { }

  /// Called exactly once, when the promo is permanently removed.
  public virtual void OnComplete() { }
}
} // namespace TiltBrush
