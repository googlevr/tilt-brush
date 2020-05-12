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
using UnityEngine;

/// This class is used to store the transform and state of all advanced panels in
/// between play sessions.
/// On load, we read a string from the Unity.PlayerPrefs that contains a list of panel
/// layouts.  We store this data in a list structure and use it for reference when
/// creating and initializing panels.
namespace TiltBrush {
  public struct AdvancedPanelLayouts {
    private const string kPlayerPrefAdvancedLayout = "AdvancedLayout";

    // This is the data structure for an individual panel layout.
    private class AdvancedPanelDiskLayout {
      public BasePanel.PanelType type;
      public bool fixedToPane;
      public float yOffset;
      public float attachAngle;
      public Vector3 pos;
      public Quaternion rot;
    }
    // This list is empty until PopulateFromDisk() is called.  It is not used when
    // writing values to disk.
    private List<AdvancedPanelDiskLayout> layouts;

    static public void ClearPlayerPrefs() {
      PlayerPrefs.DeleteKey(kPlayerPrefAdvancedLayout);
    }

    public void PopulateFromPlayerPrefs() {
      // Lazy init of our list.
      if (layouts == null) {
        layouts = new List<AdvancedPanelDiskLayout>();
      }
      layouts.Clear();

      // Early out if we've got a blank string.
      string advLayout = PlayerPrefs.GetString(kPlayerPrefAdvancedLayout, "");
      if (advLayout == "") {
        return;
      }

      string[] advPanelLayouts = advLayout.Split('|');
      for (int i = 0; i < advPanelLayouts.Length; ++i) {
        string[] layoutStr = advPanelLayouts[i].Split(',');
        if (layoutStr.Length == 11) {
          AdvancedPanelDiskLayout layout = new AdvancedPanelDiskLayout();
          try {
            layout.type = (BasePanel.PanelType)Enum.Parse(typeof(BasePanel.PanelType), layoutStr[0]);
            layout.fixedToPane = bool.Parse(layoutStr[1]);
            layout.yOffset = float.Parse(layoutStr[2]);
            layout.attachAngle = float.Parse(layoutStr[3]);
            layout.pos = new Vector3(float.Parse(layoutStr[4]), float.Parse(layoutStr[5]),
                float.Parse(layoutStr[6]));
            layout.rot = new Quaternion(float.Parse(layoutStr[7]), float.Parse(layoutStr[8]),
                float.Parse(layoutStr[9]), float.Parse(layoutStr[10]));
            layouts.Add(layout);
          } catch (ArgumentNullException e) {
            Debug.LogWarning("Bad cached panel data. " + e);
          } catch (ArgumentException e) {
            Debug.LogWarning("Bad cached panel data. " + e);
          } catch (FormatException e) {
            Debug.LogWarning("Bad cached panel data. " + e);
          } catch (OverflowException e) {
            Debug.LogWarning("Bad cached panel data. " + e);
          }
        } else {
          Debug.LogWarning("Bad cached panel data.");
        }
      }
    }

    public void WriteToDisk(List<PanelManager.PanelData> panels) {
      string layout = "";
      bool first = true;
      for (int i = 0; i < panels.Count; ++i) {
        PanelManager.PanelData p = panels[i];

        // Only record the positions of non-unique, advanced panels that are showing.
        bool advancedNonUniquePanelShowing = p.m_Panel.AdvancedModePanel &&
            !PanelManager.m_Instance.IsPanelUnique(p.m_Panel.Type) && p.m_Widget.Showing;
        // But don't record the positions of those that are not core and floating.
        bool coreOrFixed = PanelManager.m_Instance.IsPanelCore(p.m_Panel.Type) ||
            p.m_Panel.m_Fixed;
        if (advancedNonUniquePanelShowing && coreOrFixed) {
          if (!first) {
            layout = string.Concat(layout, "|");
          }
          first = false;

          string s = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
              p.m_Panel.Type.ToString(),
              p.m_Panel.m_Fixed.ToString(),
              p.m_Panel.m_WandAttachYOffset_Stable,
              p.m_Panel.m_WandAttachAngle,
              p.m_Panel.transform.position.x,
              p.m_Panel.transform.position.y,
              p.m_Panel.transform.position.z,
              p.m_Panel.transform.rotation.x,
              p.m_Panel.transform.rotation.y,
              p.m_Panel.transform.rotation.z,
              p.m_Panel.transform.rotation.w);
          layout = string.Concat(layout, s);
        }
      }
      PlayerPrefs.SetString(kPlayerPrefAdvancedLayout, layout);
    }

    // Returns true if a layout was applied to this panel.
    public bool ApplyLayoutToPanel(BasePanel p) {
      for (int i = 0; i < layouts.Count; ++i) {
        if (layouts[i].type == p.Type) {
          p.m_Fixed = layouts[i].fixedToPane;
          if (p.m_Fixed) {
            p.m_WandAttachYOffset = layouts[i].yOffset;
            p.m_WandAttachYOffset_Target = layouts[i].yOffset;
            p.m_WandAttachYOffset_Stable = layouts[i].yOffset;
            p.m_WandAttachAngle = layouts[i].attachAngle;
          } else {
            p.transform.position = layouts[i].pos;
            p.transform.rotation = layouts[i].rot;
          }
          return true;
        }
      }
      return false;
    }

    public void ReviveFloatingPanelWithLayout(BasePanel p) {
      for (int i = 0; i < layouts.Count; ++i) {
        if (!layouts[i].fixedToPane && p.Type == layouts[i].type) {
          p.SetScale(1.0f);
          p.gameObject.SetActive(true);
          return;
        }
      }
    }
  }
} // namespace TiltBrush