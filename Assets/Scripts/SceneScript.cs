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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush {

// TODO: Allow light count to be reduced.  Having to deal with inactive
// lights is a burden for the rest of the codebase, and can be error prone
// (e.g. component enabled vs. game object active).
public class SceneScript : MonoBehaviour {
  public delegate void PoseChangedEventHandler(
      TrTransform prev, TrTransform current);
  public delegate void ActiveCanvasChangedEventHandler(
      CanvasScript prev, CanvasScript current);

  [SerializeField] private CanvasScript m_MainCanvas;
  [SerializeField] private CanvasScript m_SelectionCanvas;

  private bool m_bInitialized;
  private Light [] m_Lights;

  private CanvasScript m_ActiveCanvas;
  private List<CanvasScript> m_LayerCanvases;

  public event PoseChangedEventHandler PoseChanged;
  public event ActiveCanvasChangedEventHandler ActiveCanvasChanged;

  /// Helper for getting and setting transforms on Transform components.
  /// Transform natively allows you to access parent-relative ("local")
  /// and root-relative ("global") views of position, rotation, and scale.
  ///
  /// This helper gives you a scene-relative view of the transform.
  /// The syntax is a slight abuse of C#:
  ///
  ///   TrTranform xf_SS = App.Scene.AsScene[gameobj.transform];
  ///   App.Scene.AsScene[gameobj.transform] = xf_SS;
  ///
  /// Safe to use during Awake()
  ///
  public TransformExtensions.RelativeAccessor AsScene;

  /// The global pose of this scene. All scene modifications must go through this.
  /// On assignment, range of local scale is limited (log10) to +/-4.
  /// Emits SceneScript.PoseChanged, CanvasScript.PoseChanged.
  public TrTransform Pose {
    get {
      return Coords.AsGlobal[transform];
    }
    set {
      var prevScene = Coords.AsGlobal[transform];

      value = SketchControlsScript.MakeValidScenePose(value,
          SceneSettings.m_Instance.HardBoundsRadiusMeters_SS);

      // Clamp scale, and prevent tilt. These are last-ditch sanity checks
      // and are not the proper way to impose UX constraints.
      {
        value.scale = Mathf.Clamp(Mathf.Abs(value.scale), 1e-4f, 1e4f);
        var qRestoreUp = Quaternion.FromToRotation(
            value.rotation * Vector3.up, Vector3.up);
        value = TrTransform.R(qRestoreUp) * value;
      }

      Coords.AsGlobal[transform] = value;

      // hasChanged is used in development builds to detect unsanctioned
      // changes to the transform. Set to false so we don't trip the detection!
      transform.hasChanged = false;
      if (PoseChanged != null) {
        PoseChanged(prevScene, value);
      }
      using (var canvases = AllCanvases.GetEnumerator()) {
        while (canvases.MoveNext()) {
          canvases.Current.OnScenePoseChanged(prevScene, value);
        }
      }
    }
  }

  /// Safe to use any time after initialization
  public CanvasScript ActiveCanvas {
    get {
      Debug.Assert(m_bInitialized);
      return m_ActiveCanvas;
    }
    set {
      Debug.Assert(m_bInitialized);
      if (value != m_ActiveCanvas) {
        var prev = m_ActiveCanvas;
        m_ActiveCanvas = value;
        if (ActiveCanvasChanged != null) {
          ActiveCanvasChanged(prev, m_ActiveCanvas);
          // This will be incredibly irritating, but until we have some other feedback...
          OutputWindowScript.m_Instance.CreateInfoCardAtController(
              InputManager.ControllerName.Brush,
              string.Format("Canvas is now {0}", ActiveCanvas.gameObject.name),
              fPopScalar: 0.5f, false);
        }
      }
    }
  }

  /// The initial start-up canvas; guaranteed to always exist
  public CanvasScript MainCanvas { get { return m_MainCanvas; } }
  public CanvasScript SelectionCanvas { get { return m_SelectionCanvas; } }

  public IEnumerable<CanvasScript> AllCanvases {
    get {
      yield return MainCanvas;
      if (SelectionCanvas != null) {
        yield return SelectionCanvas;
      }

      if (m_LayerCanvases != null) {
        for (int i = 0; i < m_LayerCanvases.Count; ++i) {
          yield return m_LayerCanvases[i];
        }
      }
    }
  }

  // Init unless already initialized. Safe to call zero or multiple times.
  public void Init() {
    if (m_bInitialized) {
      return;
    }
    m_bInitialized = true;
    m_LayerCanvases = new List<CanvasScript>();
    AsScene = new TransformExtensions.RelativeAccessor(transform);
    m_ActiveCanvas = m_MainCanvas;
    foreach (var c in AllCanvases) {
      c.Init();
    }
  }

  void Awake() {
    Init();
    m_Lights = new Light[(int)LightMode.NumLights];
    for (int i = 0; i < m_Lights.Length; ++i) {
      GameObject go = new GameObject(string.Format("SceneLight {0}", i));
      Transform t = go.transform;
      t.parent = App.Instance.m_EnvironmentTransform;
      t.localPosition = Vector3.zero;
      t.localRotation = Quaternion.identity;
      t.localScale = Vector3.one;
      Light newLight = go.AddComponent<Light>();
      m_Lights[i] = newLight;
    }

    m_Lights[(int)LightMode.Shadow].shadows = LightShadows.Hard;
    m_Lights[(int)LightMode.Shadow].renderMode = LightRenderMode.ForcePixel;
    m_Lights[(int)LightMode.NoShadow].shadows = LightShadows.None;
    m_Lights[(int)LightMode.NoShadow].renderMode = LightRenderMode.ForceVertex;
  }

  public CanvasScript Test_AddLayer() {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      var go = new GameObject(string.Format("Layer {0}", m_LayerCanvases.Count));
      go.transform.parent = transform;
      Coords.AsLocal[go.transform] = TrTransform.identity;
      go.transform.hasChanged = false;
      var layer = go.AddComponent<CanvasScript>();
      m_LayerCanvases.Add(layer);
      App.Scene.ActiveCanvas = layer;
      return layer;
    }
#endif
    return null;
  }

  public void Test_SquashCurrentLayer() {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      var layer = ActiveCanvas;
      if (layer == m_MainCanvas) {
        return;
      }
      // TODO: this should defer updates to the batches until the end
      foreach (var stroke in SketchMemoryScript.AllStrokes()) {
        if (stroke.Canvas == layer) {
          stroke.SetParentKeepWorldPosition(m_MainCanvas);
        }
      }
      // Hm. remove after squashing?
      OutputWindowScript.m_Instance.CreateInfoCardAtController(
          InputManager.ControllerName.Brush,
          string.Format("Squashed {0}", layer.gameObject.name));
      ActiveCanvas = m_MainCanvas;
    }
#endif
  }

  public void Test_CycleCanvas() {
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      // Klunky! Find the next canvas in the list (assumes AllCanvases has deterministic order)
      // Skip over the selection canvas; it's internal.
      var all = AllCanvases.ToList();
      int next = (all.IndexOf(ActiveCanvas) + 1) % all.Count;
      if (all[next] == m_SelectionCanvas) {
        next = (next + 1) % all.Count;
      }
      ActiveCanvas = all[next];
    }
#endif
  }

  public int GetNumLights() {
    return m_Lights.Length;
  }

  public Light GetLight(int index) {
    return m_Lights[index];
  }
}

}  // namespace TiltBrush
