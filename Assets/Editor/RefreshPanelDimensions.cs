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

using UnityEditor;
using UnityEngine;
using System.Collections;

namespace TiltBrush {
public class RefreshPanelDimensions : MonoBehaviour {

  [MenuItem("Tilt/Refresh Panel Dimensions")]

  // Using a guide object titled, "_Bounds(inactive)", set a variety of bounding values.
  static public void Execute() {
    PanelManager pMgr = GameObject.Find("/SketchControls").GetComponent<PanelManager>();
    if (!pMgr) {
      Debug.LogFormat("Error!  Panel Manager couldn't be found, aborting.");
      return;
    }

    GameObject[] selection = Selection.gameObjects;
    for (int i = 0; i < selection.Length; ++i) {
      BasePanel panel = selection[i].GetComponent<BasePanel>();
      if (panel) {
        // Get defining bounds child.
        Transform definingBounds = null;
        for (int j = 0; j < panel.transform.childCount; ++j) {
          Transform child = panel.transform.GetChild(j);
          if (child != null && child.name == "_Bounds(inactive)") {
            definingBounds = child;
            break;
          }
        }

        // Using this object, set a bunch of dimensions on various objects.
        if (definingBounds != null) {
          // Tell the user this gameObject should be deactivated.
          if (definingBounds.gameObject.activeSelf) {
            Debug.LogFormat("Error!  _Bounds(inactive) should be deactive.");
          }

          float width = definingBounds.localScale.x;
          float height = definingBounds.localScale.y;
          float rawHeight = height;
          float doubleSnapStep = pMgr.GetSnapStepDistance() * 2.0f;
          int stepCount = Mathf.RoundToInt(height / doubleSnapStep);
          height = stepCount * doubleSnapStep;
          Debug.LogFormat("Rounding height to " + height);

          // Set panel half height for attaching to wand panes.
          panel.m_WandAttachHalfHeight = height * 0.5f;

          // Set panel collider.
          bool colliderSet = false;
          for (int j = 0; j < panel.transform.childCount; ++j) {
            Transform child = panel.transform.GetChild(j);
            if (child != null && child.name == "Collider") {
              colliderSet = true;
              BoxCollider collider = child.GetComponent<BoxCollider>();
              if (collider) {
                collider.transform.localScale = Vector3.one;
                collider.size = new Vector3(width + 0.4f, height + 0.4f, 0.5f);
              } else {
                Debug.LogFormat("Error!  Collider does not contain BoxCollider component.");
              }
              break;
            }
          }
          if (!colliderSet) {
            Debug.LogFormat("Error!  No Collider found as a child of panel.");
          }

          // Find Mesh.
          for (int j = 0; j < panel.transform.childCount; ++j) {
            Transform child = panel.transform.GetChild(j);
            if (child != null && child.name == "Mesh") {
              // Set mesh collider.
              bool meshColliderSet = false;
              for (int k = 0; k < child.transform.childCount; ++k) {
                Transform grandChild = child.transform.GetChild(k);
                if (grandChild != null && grandChild.name == "MeshCollider") {
                  meshColliderSet = true;
                  BoxCollider collider = grandChild.GetComponent<BoxCollider>();
                  if (collider) {
                    collider.transform.localScale = Vector3.one;
                    collider.size = new Vector3(width - 0.1f, height - 0.1f, 0.02f);
                  } else {
                    Debug.LogFormat("Error!  MeshCollider does not contain BoxCollider component.");
                  }
                  break;
                }
              }
              if (!meshColliderSet) {
                Debug.LogFormat("Error!  No MeshCollider found as a child of Mesh.");
              }

              // Set highlight mesh.
              bool highlightMeshSet = false;
              for (int k = 0; k < child.transform.childCount; ++k) {
                Transform grandChild = child.transform.GetChild(k);
                if (grandChild != null && grandChild.name == "HighlightMesh") {
                  highlightMeshSet = true;
                  grandChild.localPosition = new Vector3(0.0f, 0.0f, 0.1f);
                  grandChild.localScale = new Vector3(width * 0.5f - 0.07f,
                      rawHeight * 0.5f - 0.07f, 0.01f);
                  break;
                }
              }
              if (!highlightMeshSet) {
                Debug.LogFormat("Error!  No HighlightMesh found as a child of Mesh.");
              }
              break;
            }
          }
        }
      }
    }
  }
}
}
