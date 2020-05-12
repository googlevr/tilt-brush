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
using System.Collections.Generic;
using System.Linq;

namespace TiltBrush {
/// Can display visualizations of Vectors etc for a single frame, triggered by a single code line.
/// Usage example:
/// DebugVisualization.Instance.ShowDirection(vDirection, vStartPosition, transform)
public class DebugVisualization : MonoBehaviour {

  public enum VisType {
    Position,
    Direction,
    Plane,
    Text,
  }

  private static DebugVisualization m_Instance;

  [SerializeField] private GameObject m_PositionPrefab;
  [SerializeField] private GameObject m_DirectionPrefab;
  [SerializeField] private GameObject m_PlanePrefab;
  [SerializeField] private GameObject m_TextPrefab;

  private Dictionary<VisType, Stack<GameObject>> m_VisObjects;

  private Dictionary<VisType, Stack<GameObject>> m_UnusedVisObjects;

  private bool m_Reset;

  void Awake() {
    Debug.Assert(m_Instance == null);
    m_Instance = this;
    m_VisObjects = new Dictionary<VisType, Stack<GameObject>>();
    m_UnusedVisObjects = new Dictionary<VisType, Stack<GameObject>>();

    foreach (var type in System.Enum.GetValues(typeof(VisType)).Cast<VisType>()) {
      m_VisObjects[type] = new Stack<GameObject>();
      m_UnusedVisObjects[type] = new Stack<GameObject>();
    }
  }

  private void ResetObjects() {
    if (!m_Reset) {
      return;
    }
    foreach (var group in m_VisObjects) {
      var stack = group.Value;
      var type = group.Key;
      while (stack.Count != 0) {
        GameObject pos = stack.Pop();
        pos.SetActive(false);
        m_UnusedVisObjects[type].Push(pos);
      }
    }
    m_Reset = false;
  }

  private void LateUpdate() {
    if (m_Reset) {
      ResetObjects();
    }
    m_Reset = true;
  }

  private GameObject CreateObject(VisType type, Transform parent, Vector3 pos, Quaternion rot,
                                  Vector3 scale) {
    GameObject prefab;
    switch (type) {
        case VisType.Position:
          prefab = m_PositionPrefab;
          break;
        case VisType.Direction:
          prefab = m_DirectionPrefab;
          break;
        case VisType.Plane:
          prefab = m_PlanePrefab;
          break;
        case VisType.Text:
          prefab = m_TextPrefab;
          break;
        default:
          Debug.Assert(true, "Unsupported visualization!");
          return null;
    }
    if (m_Reset) {
      ResetObjects();
    }
    GameObject visObject;
    if (m_UnusedVisObjects[type].Count == 0) {
      visObject = GameObject.Instantiate(prefab);
    } else {
      visObject = m_UnusedVisObjects[type].Pop();
      visObject.SetActive(true);
    }
    if (parent != null) {
      visObject.transform.parent = parent;
    }
    visObject.transform.localPosition = pos;
    visObject.transform.localRotation = rot;
    visObject.transform.localScale = scale;
    visObject.transform.SetParent(null, true);
    m_VisObjects[type].Push(visObject);
    return visObject;
  }

  /// Will show a position marker. Uses the local transform space, if provided.
  public static void ShowPosition(Vector3 position, Transform parent = null) {
    m_Instance.CreateObject(VisType.Position, parent, position, Quaternion.identity, Vector3.one);
  }

  /// Show a vector with a given starting position. Uses the local transform space, if provided.
  public static void ShowDirection(Vector3 direction, Vector3 startPosition,
                                   Transform parent = null) {
    m_Instance.CreateObject(VisType.Direction, parent, startPosition,
                            Quaternion.LookRotation(direction), Vector3.one * direction.magnitude);
  }

  /// Show a plane. Visualization in centered on its closest point to the supplied vector.
  /// Uses the local transform space, if provided.
  public static void ShowPlane(Plane plane, Vector3 closePoint, Transform parent = null) {
    float distance = plane.GetDistanceToPoint(closePoint);
    Vector3 onPlane = closePoint - distance * plane.normal;
    m_Instance.CreateObject(VisType.Plane, parent, onPlane, Quaternion.LookRotation(plane.normal),
                            Vector3.one);
  }

  /// Show some text at the given position.
  public static void ShowText(string text, Vector3 position, Quaternion rotation, Vector3 scale,
                              Transform parent = null) {
    GameObject gobj = m_Instance.CreateObject(VisType.Text, parent, position, rotation, scale);
    TextMesh textMesh = gobj.GetComponentInChildren<TextMesh>();
    if (text != null) {
      textMesh.text = text;
    }
  }

}
} // namespace TiltBrush
