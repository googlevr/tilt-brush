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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush {
/// Base class for tabs in the reference panel. Should be derived from for each new media type.
public abstract class ReferencePanelTab : MonoBehaviour {

  // Helper class for storing information about the icons on the tab. Classes deriving from
  // ReferencePanelTab may have their own classes derived from ReferenceIcon to store per-icon data.
  public abstract class ReferenceIcon {
    public BaseButton Button { get; set; }
    public abstract void Refresh(int catalog);
  }

  [SerializeField] private string m_PanelName;
  protected ReferenceIcon[] m_Icons = new ReferenceIcon[0];
  protected int m_IndexOffset;

  public string PanelName { get { return m_PanelName; } }
  public int PageIndex { get; set; }
  public abstract IReferenceItemCatalog Catalog { get; }
  public List<BaseButton> Buttons {
    get { return m_Icons.Select(x => x.Button).ToList(); }
  }

  // This is the enum value returned by a ReferenceButton to refer to items on this tab.
  public abstract ReferenceButton.Type ReferenceButtonType { get; }
  // This is the type of the button used for items on the tab.
  protected abstract Type ButtonType { get;  }
  // This is the ReferenceIcon-derived type used to store information about the icons on the tab.
  protected abstract Type IconType { get; }

  public virtual void InitTab() {
    var items = GetComponentsInChildren(ButtonType, includeInactive: true);
    m_Icons = (ReferenceIcon[])Array.CreateInstance(IconType, items.Length);
    for (int i = 0; i < items.Length; ++i) {
      m_Icons[i] = Activator.CreateInstance(IconType) as ReferenceIcon;
      m_Icons[i].Button = items[i] as BaseButton;
    }
  }

  public virtual void UpdateButtonTransitionScale(float scale) {
    foreach (var icon in m_Icons) {
      Vector3 vScale = icon.Button.gameObject.transform.localScale;
      vScale.x = icon.Button.ScaleBase.y * scale;
      icon.Button.gameObject.transform.localScale = vScale;
    }
  }

  public virtual void RefreshTab(bool selected) {
    m_IndexOffset = PageIndex * m_Icons.Length;
    for (int i = 0; i < m_Icons.Length; ++i) {
      int index = m_IndexOffset + i;
      bool activeAndSelected = selected && (index < Catalog.ItemCount);
      var icon = m_Icons[i];
      if (activeAndSelected) {
        icon.Button.SetButtonAvailable(true);
        icon.Refresh(index);
      }
      icon.Button.gameObject.SetActive(activeAndSelected);
    }
  }

  public int PageCount {
    get { return ((Catalog.ItemCount - 1) / m_Icons.Length) + 1; }
  }

  public int IconCount {
    get { return m_Icons.Length; }
  }

  public virtual void UpdateTab() { }

  public virtual void OnTabEnable() {
    if (!App.PlatformConfig.UseFileSystemWatcher) {
      Catalog.ForceCatalogScan();
    }
  }

  public virtual void OnTabDisable() { }

  public virtual void OnUpdateGazeBehavior(Color panelColor, bool gazeActive, bool available) {}

  public virtual bool RaycastAgainstMeshCollider(Ray ray, out RaycastHit hitInfo, float dist) {
    hitInfo = new RaycastHit();
    return false;
  }
}
} // namespace TiltBrush
