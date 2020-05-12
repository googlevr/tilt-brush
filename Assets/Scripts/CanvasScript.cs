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

public class CanvasScript : MonoBehaviour {
  public delegate void PoseChangedEventHandler(TrTransform prev, TrTransform current);

  [SerializeField] private string[] m_BatchKeywords;

#if UNITY_EDITOR
  /// Pass a GameObject to receive the newly-created CanvasScript.
  /// Useful for unit tests that need some access to a Canvas but don't
  /// want to set up all of Tilt Brush.
  public static CanvasScript UnitTestSetUp(GameObject container) {
    var ret = container.AddComponent<CanvasScript>();
    // There are instances of CanvasScript in the scene, and I don't want to
    // willy-nilly add [ExecuteInEditMode] without verifying it won't cause
    // issues at edit time. Instead, initialize canvas by hand.
    ret.Awake();
    return ret;
  }

  /// The inverse of UnitTestSetUp
  public static void UnitTestTearDown(GameObject container) {
    UnityEngine.Object.DestroyImmediate(container.GetComponent<CanvasScript>());
  }
#endif

  // These bounds are for keeping values sane; they're not intended to be user-facing
  private const float kScaleMin = 1e-4f;
  private const float kScaleMax = 1e4f;

  private bool m_bInitialized;
  private BatchManager m_BatchManager;

  public event PoseChangedEventHandler PoseChanged;

  public BatchManager BatchManager {
    get { return m_BatchManager; }
  }

  /// Helper for getting and setting transforms on Transform components.
  /// Transform natively allows you to access parent-relative ("local")
  /// and root-relative ("global") views of position, rotation, and scale.
  ///
  /// This helper gives you a canvas-relative view of the transform.
  /// The syntax is a slight abuse of C#:
  ///
  ///   TrTranform xf_CS = myCanvas.AsCanvas[gameobj.transform];
  ///   myCanvas.AsCanvas[gameobj.transform] = xf_CS;
  ///
  /// Safe to use during Awake()
  ///
  public TransformExtensions.RelativeAccessor AsCanvas;

  /// The global pose of the canvas.
  /// All pose modifications must go through this property.
  /// On assignment, range of local scale is limited (log10) to +/-4
  /// Emits PoseChanged.
  public TrTransform Pose {
    get {
      return Coords.AsGlobal[transform];
    }
    set {
      Transform transform = this.transform;
      float parentScale = transform.parent.GetUniformScale();
      value.scale = Mathf.Clamp(Mathf.Abs(value.scale),
                                parentScale * kScaleMin, parentScale * kScaleMax);
      TrTransform prevValue = Coords.AsGlobal[transform];
      Coords.AsGlobal[transform] = value;
      // hasChanged is used in development builds to detect unsanctioned
      // changes to the transform. Set to false so we don't trip the detection!
      transform.hasChanged = false;
      if (PoseChanged != null) {
        PoseChanged(prevValue, value);
      }
    }
  }

  /// The local version of Pose (qv)
  /// All canvas modifications must go through this.
  /// Emits PoseChanged.
  public TrTransform LocalPose {
    get {
      return Coords.AsLocal[transform];
    }
    set {
      var transform = this.transform;
      value.scale = Mathf.Clamp(Mathf.Abs(value.scale), kScaleMin, kScaleMax);
      var prevCanvas = Coords.AsGlobal[transform];
      Coords.AsLocal[transform] = value;
      transform.hasChanged = false;
      if (PoseChanged != null) {
        PoseChanged(prevCanvas, Coords.AsGlobal[transform]);
      }
    }
  }

  // Init unless already initialized. Safe to call zero or multiple times.
  public void Init() {
    if (m_bInitialized) {
      return;
    }
    m_bInitialized = true;

    AsCanvas = new TransformExtensions.RelativeAccessor(transform);
  }

  void Awake() {
    // Canvases might be dynamically created, so we can't rely on them all
    // being initialized at App-init time.
    Init();
    m_BatchManager = new BatchManager();
    if (m_BatchKeywords != null) {
      m_BatchManager.MaterialKeywords.AddRange(m_BatchKeywords);
    }
    m_BatchManager.Init(this);
  }

  void Update() {
#if UNITY_EDITOR
    // All changes must go through .Pose accessor
    if (transform.hasChanged) {
      Debug.LogError("Detected unsanctioned change to transform");
      transform.hasChanged = false;
    }
#endif
    m_BatchManager.Update();
  }

  public void RegisterHighlight() {
    m_BatchManager.RegisterHighlight();
  }

  // Returns a bounds object that encompasses all strokes on the canvas.
  public Bounds GetCanvasBoundingBox(bool onlyActive = false) {
    return m_BatchManager.GetBoundsOfAllStrokes(onlyActive);
  }

  // Should only be called by friend classes (Coords, SceneScript)
  public void OnScenePoseChanged(TrTransform previousScene, TrTransform currentScene) {
    // hasChanged is used in development builds to detect unsanctioned
    // changes to the transform. Set to false so we don't trip the detection!
    transform.hasChanged = false;
    TrTransform local = Coords.AsLocal[transform];
    if (PoseChanged != null) {
      PoseChanged(previousScene * local, currentScene * local);
    }
  }
}

}  // namespace TiltBrush
