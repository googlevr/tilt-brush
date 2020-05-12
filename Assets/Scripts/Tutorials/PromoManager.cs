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
using System.Linq;
using System.Collections.Generic;

namespace TiltBrush {
public interface IPromoFactory {
  BasePromo CreateInstance();
  PromoType InstanceType { get; }
  string PrefsKey { get; }
}

public class PromoFactory<T> : IPromoFactory where T : BasePromo, new() {
  private PromoType m_Type;
  private string m_KeySuffix;

  public PromoFactory(PromoType type, string keySuffix) {
    m_Type = type; m_KeySuffix = keySuffix;
  }

  public BasePromo CreateInstance() { return new T(); }
  public PromoType InstanceType { get { return m_Type; } }
  public string PrefsKey { get { return PromoManager.kPromoPrefix + m_KeySuffix; } }
}

public class PromoManager : MonoBehaviour {
  public static PromoManager m_Instance;

  public const string kPromoPrefix = "Promo_";

  [Header("Button Promos")]
  [SerializeField] private GameObject m_ButtonHighlightPrefab;

  [Header("Share Sketch Promo")]
  [SerializeField] Material m_BorderMaterial;
  [SerializeField] private GameObject m_HintLinePrefab;

  [Header("Brush Size Promo")]
  [SerializeField] private float m_BrushSizeHintShowDistance;
  [SerializeField] private float m_BrushSizeHintPreventSwipeAmount = 1.0f;
  [SerializeField] private float m_BrushSizeHintCancelSwipeAmount = 0.3f;

  [Header("Floating Panel Promo")]
  [SerializeField,TextArea] private string m_GrabPanelHintText;
  [SerializeField, TextArea] private string m_TossPanelHintText;

  private Dictionary<PromoType, IPromoFactory> m_Factories;
  private HashSet<BasePromo> m_RequestedPromos;
  private BasePromo m_DisplayedPromo;
  private HintArrowLineScript m_HintLine;
  private GameObject m_ButtonHighlightMesh;
  private Vector3 m_ButtonHighlightMeshBasePos;
  private Vector3 m_ButtonHighlightMeshBaseScale;

  public Material SharePromoMaterial { get { return m_BorderMaterial; } }
  public HintArrowLineScript HintLine { get { return m_HintLine; } }
  public GameObject ButtonHighlight { get { return m_ButtonHighlightMesh; } }
  public float BrushSizeHintShowDistance { get { return m_BrushSizeHintShowDistance; } }
  public float BrushSizeHintPreventSwipeAmount { get { return m_BrushSizeHintPreventSwipeAmount; } }
  public float BrushSizeHintCancelSwipeAmount { get { return m_BrushSizeHintCancelSwipeAmount; } }
  public string GrabPanelHintText { get { return m_GrabPanelHintText; } }
  public string TossPanelHintText { get { return m_TossPanelHintText; } }

  public IEnumerable<BasePromo> RequestedPromos { get { return m_RequestedPromos; } }

  private bool m_RequestedAdvancedPanelsThisSession;
  private bool m_RequestedFloatingPanelsThisSession;

  public void ResetButtonHighlightXf() {
    m_ButtonHighlightMesh.transform.localScale = m_ButtonHighlightMeshBaseScale;
    m_ButtonHighlightMesh.transform.localPosition = m_ButtonHighlightMeshBasePos;
    m_ButtonHighlightMesh.transform.localRotation = Quaternion.identity;
  }

  public bool ShouldPausePromos {
    get {
      return (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate) &&
        !SketchSurfacePanel.m_Instance.ActiveTool.IsEatingInput) ||
        SketchControlsScript.m_Instance.IsUserInteractingWithUI() ||
        SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget() ||
        SketchControlsScript.m_Instance.IsUserGrabbingWorld() ||
        InputManager.m_Instance.ControllersAreSwapping();
    }
  }

  // Used for debugging
  string ListRequests() {
    string reqs = "Requests:\n";
    foreach (var r in m_RequestedPromos) {
      reqs += r + " " + (r.ReadyToDisplay ? "ready" : "idle") + "\n";
    }
    return reqs;
  }

  void Awake() {
    m_Instance = this;

    // create factories
    m_Factories =
      new IPromoFactory[] {
        new PromoFactory<BrushSizePromo>(PromoType.BrushSize, "BrushSize"),
        new PromoFactory<ShareSketchPromo>(PromoType.ShareSketch, "ShareSketch"),
        new PromoFactory<FloatingPanelPromo>(PromoType.FloatingPanel, "FloatingPanel"),
        new PromoFactory<SelectionPromo>(PromoType.Selection, "Selection"),
        new PromoFactory<DuplicatePromo>(PromoType.Duplicate, "Duplicate"),
        new PromoFactory<InteractIntroPanelPromo>(PromoType.InteractIntroPanel, "InteractIntroPanel"),
        new PromoFactory<SaveIconPromo>(PromoType.SaveIcon, "SaveIcon"),
        new PromoFactory<DeselectionPromo>(PromoType.Deselection, "Deselection"),
        new PromoFactory<AdvancedPanelsPromo>(PromoType.AdvancedPanels, "AdvancedPanels"),
      }.ToDictionary(f => f.InstanceType);

    m_RequestedPromos = new HashSet<BasePromo>();

    GameObject obj = (GameObject)GameObject.Instantiate(m_HintLinePrefab);
    m_HintLine = obj.GetComponent<HintArrowLineScript>();
    obj.SetActive(false);

    m_ButtonHighlightMesh = (GameObject)Instantiate(m_ButtonHighlightPrefab);
    m_ButtonHighlightMeshBasePos = m_ButtonHighlightMesh.transform.localPosition;
    m_ButtonHighlightMeshBaseScale = m_ButtonHighlightMesh.transform.localScale;
    m_ButtonHighlightMesh.SetActive(false);

    if (App.UserConfig.Testing.ResetPromos) {
      foreach (var factory in m_Factories.Values) {
        PlayerPrefs.DeleteKey(factory.PrefsKey);
      }
    }
  }

  void Update() {
    if (m_DisplayedPromo == null) {
      foreach (BasePromo p in m_RequestedPromos) {
        p.OnIdle();
      }
      m_DisplayedPromo = m_RequestedPromos.FirstOrDefault(p => p.ReadyToDisplay);
      if (m_DisplayedPromo != null) {
        m_DisplayedPromo.Display();
      }
    }
    if (m_DisplayedPromo != null) {
      m_DisplayedPromo.OnActive();
      if (m_DisplayedPromo != null && m_DisplayedPromo.ShouldBeHidden) {
        m_DisplayedPromo.Hide();
        m_DisplayedPromo = null;
      }
    }
  }

  public void RequestPromo(PromoType promo) {
    // Don't request if promo has previously been completed or if controllers are disabled.
    if (!PlayerPrefs.HasKey(m_Factories[promo].PrefsKey) &&
        App.UserConfig.Flags.ShowControllers) {
      m_RequestedPromos.Add(m_Factories[promo].CreateInstance());
    }
  }

  // Record the number of times a promo has been completed.
  public void RecordCompletion(PromoType promo) {
    BasePromo[] removed =
      m_RequestedPromos.Where(p => p.PrefsKey == m_Factories[promo].PrefsKey).ToArray();
    foreach (BasePromo p in removed) {
      p.OnComplete();
      m_RequestedPromos.Remove(p);
    }

    string key = m_Factories[promo].PrefsKey;
    if (PlayerPrefs.HasKey(key)) {
      PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key) + 1);
    } else {
      PlayerPrefs.SetInt(key, 1);
    }
  }

  public bool HasPromoBeenCompleted(PromoType type) {
    return PlayerPrefs.HasKey(m_Factories[type].PrefsKey);
  }

  // Specific method for Advanced Panels so we can track calls over the session.
  public void RequestAdvancedPanelsPromo() {
    if (App.Config.m_SdkMode != SdkMode.Ods &&
        !App.Instance.IsFirstRunExperience &&
        !m_RequestedAdvancedPanelsThisSession) {
      RequestPromo(PromoType.AdvancedPanels);
      m_RequestedAdvancedPanelsThisSession = true;
    }
  }

  // Specific method for Floating Panels so we can track calls over the session.
  public void RequestFloatingPanelsPromo() {
    if (!m_RequestedFloatingPanelsThisSession) {
      RequestPromo(PromoType.FloatingPanel);
      m_RequestedFloatingPanelsThisSession = true;
    }
  }
}
} // namespace TiltBrush
