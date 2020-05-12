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

public class PointerScript : MonoBehaviour {
  // ---- public types

  public struct PreviewControlPoint {
    public float m_BirthTime;
    public TrTransform m_xf_LS;  // in the local coordinate system of the preview line
  }

  /// Designates in which space space the parametric brush-size lerp operates.
  public enum BrushLerp {
    Default,
    // These next ones are leftover tests from development
    Radius,     // Lerp the brush radius
    Area,       // Lerp the brush area (ie, radius ^ 2)
    ScaleInvariant,     // Lerp in log space (fixed dt == fixed multiple of brush size)
    SqrtRadius, // Lerp sqrt radius (ie, radius ^ 0.5). No theoretical basis, but it feels good!
  };

  const float m_BasePreviewIntensity = 4.0f;
  const float m_GlowPreviewIntensity = 8.0f;

  // ---- Private inspector data

  [SerializeField] private Light m_PreviewLight;
  [SerializeField] private float m_PreviewLightScalar = 1.0f;
  [SerializeField] private Renderer m_Mesh;
  //this is the list of meshes that make up the standard pointer look: cone + ring
  [SerializeField] private Renderer[] m_PrimaryMeshes;
  [SerializeField] private Transform m_BrushSizeIndicator;
  [SerializeField] private bool m_PreviewLineEnabled;
  [SerializeField] private float m_PreviewLineControlPointLife = 1.0f;
  [SerializeField] private float m_PreviewLineIdealLength = 1.0f;
  [SerializeField] private GvrAudioSource[] m_AudioSources;
  [SerializeField] private Vector2 m_BrushAudioPitchVelocityRange;
  [SerializeField] private AudioClip m_BrushPlaybackAudioClip;

  // ---- Private member data

  private bool m_AllowPreviewLight = true;

  private Color m_CurrentColor;
  private float m_GlowPreviewEnabled = 1.0f;

  private Vector3 m_InitialBrushSizeScale;
  private TiltBrush.BrushDescriptor m_CurrentBrush;
  private float m_CurrentBrushSize;  // In pointer aka room space
  private Vector2 m_BrushSizeRange;
  private float m_CurrentPressure;      // TODO: remove and query line instead?
  private BaseBrushScript m_CurrentLine;
  private ParametricStrokeCreator m_CurrentCreator;
  private float m_ParametricCreatorBackupStrokeSize;  // In pointer aka room space

  private float m_AudioVolumeDesired;
  private float m_CurrentTotalVolume; // Brush audio volume before being divided between layers
  private float m_BrushAudioMaxVolume;
  private float m_BrushAudioAdjustSpeedUp;
  private float m_BrushAudioAdjustSpeedDown;
  private Vector2 m_BrushAudioVolumeVelocityRange;
  private float m_BrushAudioBasePitch;
  private float m_BrushAudioMaxPitchShift;
  private float m_AudioPitchDesired;

  private bool m_AllowPreviewLine;
  private float m_AllowPreviewLineTimer;
  private BaseBrushScript m_PreviewLine;

  private List<PreviewControlPoint> m_PreviewControlPoints;  // FIFO queue
  private List<PointerManager.ControlPoint> m_ControlPoints;
  private bool m_LastControlPointIsKeeper;
  private Vector3 m_PreviousPosition;   //used for audio

  private float m_LineDepth;  // depth of stroke, only used in monoscopic mode. Room-space.
  private float m_LineLength_CS; // distance moved for the active line. Canvas-space.

  private bool m_ShowDebugControlPoints = false;
  private List<Vector3> m_DebugViewControlPoints;

  private float? m_LastUsedBrushSize_CS;

  private CanvasScript m_SubscribedCanvas;

  // ---- Public properties, accessors, events

  public event Action<TiltBrush.BrushDescriptor> OnBrushChange = delegate {};

  float _FromRadius(float x) {
    switch (DevOptions.I.BrushLerp) {
    case BrushLerp.Radius: return x;
    case BrushLerp.Area: return x * x;
    case BrushLerp.ScaleInvariant: return Mathf.Log(x);
    default:
    case BrushLerp.Default:
    case BrushLerp.SqrtRadius: return Mathf.Sqrt(x);
    }
  }
  float _ToRadius(float x) {
    switch (DevOptions.I.BrushLerp) {
    case BrushLerp.Radius: return x;
    case BrushLerp.Area: return Mathf.Sqrt(x);
    case BrushLerp.ScaleInvariant: return Mathf.Exp(x);
    default:
    case BrushLerp.Default:
    case BrushLerp.SqrtRadius: return x * x;
    }
  }

  /// The brush size, using "normalized" values in the range [0,1].
  /// On get, values are raw and may be outside [0,1].
  /// On set, values outside of the range [0,1] are clamped.
  public float BrushSize01 {
    get {
      float min = _FromRadius(m_BrushSizeRange.x);
      float max = _FromRadius(m_BrushSizeRange.y);
      return Mathf.InverseLerp(min, max, _FromRadius(BrushSizeAbsolute));
    }
    set {
      float min = _FromRadius(m_BrushSizeRange.x);
      float max = _FromRadius(m_BrushSizeRange.y);
      BrushSizeAbsolute = _ToRadius(Mathf.Lerp(min, max, Mathf.Clamp01(value)));
    }
  }

  public BrushDescriptor CurrentBrush {
    get { return m_CurrentBrush; }
    set { SetBrush(value); }
  }

  /// The brush size, in absolute room space units.
  /// On get, values are raw and may be outside the brush's desired range.
  /// On set, values outside the brush's nominal range are clamped.
  public float BrushSizeAbsolute {
    get { return m_CurrentBrushSize; }
    set { _SetBrushSizeAbsolute(Mathf.Clamp(value, m_BrushSizeRange.x, m_BrushSizeRange.y)); }
  }

  public float MonoscopicLineDepth {
    set { m_LineDepth = value; }
  }

  // This is a flaky function.  It's only valid if called after a tool has moved the pointer
  // and before UpdatePointer() is called.
  public float GetMovementDelta() {
    return (transform.position - m_PreviousPosition).magnitude;
  }

  /// Timestamp of stroke start, or error if unknown
  public uint TimestampMs {
    get { return m_ControlPoints[0].m_TimestampMs; }
  }

  /// Index of the PointerData in PointerManager
  public int ChildIndex { get; set; }

  public BaseBrushScript CurrentBrushScript { get { return m_CurrentLine; } }

  public void AllowPreviewLight(bool bAllow) {
    m_AllowPreviewLight = bAllow;
  }

  public void EnableDebugViewControlPoints(bool bEnable) {
    m_ShowDebugControlPoints = bEnable;
  }

  // ---- Unity events

  void Awake() {
    m_ControlPoints = new List<PointerManager.ControlPoint>();
    m_PreviewControlPoints = new List<PreviewControlPoint>();

    m_AllowPreviewLine = true;

    if (m_PreviewLight) {
      m_PreviewLight.enabled = false;
    }

    if (m_BrushSizeIndicator) {
      m_InitialBrushSizeScale = m_BrushSizeIndicator.localScale;
    }
    m_CurrentBrushSize = 1.0f;
    m_BrushSizeRange.x = 1.0f;
    m_BrushSizeRange.y = 2.0f;
    m_CurrentPressure = 1.0f;

    m_DebugViewControlPoints = new List<Vector3>();

    App.Scene.ActiveCanvasChanged += OnActiveCanvasChanged;
    OnActiveCanvasChanged(null, App.Scene.ActiveCanvas);
  }

  void OnDestroy() {
    if (m_SubscribedCanvas != null) {
      m_SubscribedCanvas.PoseChanged -= OnActiveCanvasPoseChanged;
    }
  }

  void OnActiveCanvasChanged(CanvasScript prev, CanvasScript current) {
    // Swap subscription
    Debug.Assert(prev == m_SubscribedCanvas);
    if (m_SubscribedCanvas != null) {
      m_SubscribedCanvas.PoseChanged -= OnActiveCanvasPoseChanged;
    }
    m_SubscribedCanvas = current;
    if (m_SubscribedCanvas != null) {
      m_SubscribedCanvas.PoseChanged += OnActiveCanvasPoseChanged;
      if (m_LastUsedBrushSize_CS != null) {
        BrushSizeAbsolute = current.Pose.scale * m_LastUsedBrushSize_CS.Value;
      }
    }
  }

  void OnActiveCanvasPoseChanged(TrTransform prev, TrTransform current) {
    if (m_LastUsedBrushSize_CS != null) {
      BrushSizeAbsolute = current.scale * m_LastUsedBrushSize_CS.Value;
    }
  }

  public void CopyInternals(PointerScript rOther) {
    SetBrush(rOther.m_CurrentBrush);
    m_BrushSizeRange = rOther.m_BrushSizeRange; // remove? handled by SetBrush
    BrushSizeAbsolute = rOther.BrushSizeAbsolute;
    m_CurrentPressure = rOther.m_CurrentPressure;
    m_AllowPreviewLineTimer = rOther.m_AllowPreviewLineTimer;
    m_AllowPreviewLine = rOther.m_AllowPreviewLine;
    m_LastUsedBrushSize_CS = rOther.m_LastUsedBrushSize_CS;
    SetColor(rOther.m_CurrentColor);
  }

  public bool IsCreatingStroke() {
    return (m_CurrentLine != null);
  }

  public Color GetCurrentColor() {
    return m_CurrentColor;
  }

  public float GetCurrentLineSpawnInterval(float pressure01) {
    if (m_CurrentLine != null) {
      return m_CurrentLine.GetSpawnInterval(pressure01);
    }
    return 1.0f;
  }

  public void MarkBrushSizeUsed() {
    m_LastUsedBrushSize_CS = (1 / App.ActiveCanvas.Pose.scale) * BrushSizeAbsolute;
  }

  void Update() {
    //update brush audio
    if (m_AudioSources.Length > 0) {
      //smooth volume and pitch out a bit from frame to frame
      float fFadeStepUp = m_BrushAudioAdjustSpeedUp * Time.deltaTime;
      float fFadeStepDown = m_BrushAudioAdjustSpeedDown * Time.deltaTime;

      float fVolumeDistToDesired = m_AudioVolumeDesired - m_CurrentTotalVolume;
      float fVolumeAdjust = Mathf.Clamp(fVolumeDistToDesired, -fFadeStepDown, fFadeStepUp);
      m_CurrentTotalVolume = m_CurrentTotalVolume + fVolumeAdjust;
      float fPitchDistToDesired = m_AudioPitchDesired - m_AudioSources[0].pitch;
      float fPitchAdjust = Mathf.Clamp(fPitchDistToDesired, -fFadeStepDown, fFadeStepUp);

      for (int i=0; i<m_AudioSources.Length; i++) {
        // Adjust volume of each layer based on brush speed
        m_AudioSources[i].volume = LayerVolume(i, m_CurrentTotalVolume);
        m_AudioSources[i].pitch += fPitchAdjust;
      }
    }
  }

  // Defines volume of a specific layer, given the total volume of the brush;
  float LayerVolume(int iLayer, float fTotalVolume) {
    float fResult = 0f;
    int iNumberOfLayers = m_CurrentBrush.m_BrushAudioLayers.Length;

    float fLayerBeginning;
    if (iLayer == 0) {
      fLayerBeginning = 0f;
    } else if (iLayer == 1) {
      fLayerBeginning = 1f / 3f;
    } else if (iLayer == 2) {
      fLayerBeginning = .5f;
    } else if (iLayer == 3) {
      fLayerBeginning = 2f / 3f;
    } else {
      fLayerBeginning = 5f / 6f;
    }
    float fLayerLength = 1f - fLayerBeginning;
    float fLayerVolume = (fTotalVolume - fLayerBeginning) / fLayerLength;
    fLayerVolume *= m_BrushAudioMaxVolume;
    fResult = Mathf.Clamp01(fLayerVolume);

    return fResult;
  }

  public List<PointerManager.ControlPoint> GetControlPoints() {
    return m_ControlPoints.ToList();
  }

  /// Returns this.transform, relative to the passed line transform.
  /// The resulting transform may have scale in it, representing how "big"
  /// the pointer is.
  ///
  /// NB: This suffers from confusion between the coordinate space of the line and the coordinate
  /// space of the line's parent (ie, the canvas). In practice they're the same, since all line's
  /// localTransforms are identity -- that's why we don't see bugs. But it's confusing.
  ///
  /// TODO: change to calculate relative to line.parent (ie Canvas) + rename + fix docs.
  TrTransform GetTransformForLine(Transform line) {
    return GetTransformForLine(line, Coords.AsRoom[transform]);
  }

  /// Returns xf_RS, relative to the passed line transform.
  /// Applies m_LineDepth and ignores xf_RS.scale
  /// TODO: see above.
  TrTransform GetTransformForLine(Transform line, TrTransform xf_RS) {
    var xfRoomFromLine = Coords.AsRoom[line];
    xf_RS.translation += xf_RS.forward * m_LineDepth;
    xf_RS.scale = 1;
    return TrTransform.InvMul(xfRoomFromLine, xf_RS);
  }

  // Regenerates the preview line's geometry from scratch.
  private void RebuildPreviewLine() {
    // Update head preview control point.
    {
      PreviewControlPoint point = new PreviewControlPoint();
      point.m_BirthTime = Time.realtimeSinceStartup;
      point.m_xf_LS = GetTransformForLine(m_PreviewLine.transform);
      m_PreviewControlPoints.Add(point);
    }

    // Trim old points from the start.
    {
      int i;
      // "-2" because we can't generate geometry without at least 2 points
      for (i = 0; i < m_PreviewControlPoints.Count-2; ++i) {
        float now = Time.realtimeSinceStartup;
        if (now - m_PreviewControlPoints[i].m_BirthTime < m_PreviewLineControlPointLife) {
          break;
        }
      }
      m_PreviewControlPoints.RemoveRange(0, i);
    }

    // Calculate length in room space.
    float previewLineLength_RS = 0.0f;
    {
      float previewLineLength_CS = 0.0f;
      for (int i = 1; i < m_PreviewControlPoints.Count; ++i) {
        previewLineLength_CS += Vector3.Distance(
            m_PreviewControlPoints[i-1].m_xf_LS.translation, m_PreviewControlPoints[i].m_xf_LS.translation);
      }
      TrTransform xfRoomFromCanvas = Coords.AsRoom[m_PreviewLine.transform.parent];
      previewLineLength_RS = xfRoomFromCanvas.scale * previewLineLength_CS;
    }

    Debug.Assert(m_PreviewControlPoints.Count > 0, "Invariant");
    m_PreviewLine.ResetBrushForPreview(m_PreviewControlPoints[0].m_xf_LS);

    // Walk control points and draw preview brush.
    // We use the num segments and length to determine width.
    {
      float lengthScale01 = Mathf.Min(1f, previewLineLength_RS / m_PreviewLineIdealLength);
      // Adjust for for emphasis on the front.
      int iFullWidthSegment = Mathf.Max(1, m_PreviewControlPoints.Count - 1 - 2);
      for (int i = 1; i < m_PreviewControlPoints.Count; ++i) {
        int iSegment = i - 1;
        float segmentScale01 = Mathf.Min(1f, (float)iSegment / iFullWidthSegment);
        // Brush size is: original size * "distance" to front * ratio to ideal length.
        // "distance" is approximated here by "segment index".
        m_PreviewLine.UpdatePosition_LS(
            m_PreviewControlPoints[i].m_xf_LS, segmentScale01 * lengthScale01);
      }
    }
  }

  // Like Unity's Update(), but only called on active pointers,
  // and called at a well-defined time.
  // Called from our manager.
  public void UpdatePointer() {
    UnityEngine.Profiling.Profiler.BeginSample("PointerScript.UpdatePointer");
    if (m_CurrentLine != null) {
      // Non-preview mode: Update line with new pointer position
      UpdateLineFromObject();
    } else if (m_PreviewLineEnabled && m_CurrentBrush != null) {
      // Preview mode: Create a preview line if we need one but don't have one
      if (m_AllowPreviewLine && m_PreviewLine == null) {
        m_AllowPreviewLineTimer -= Time.deltaTime;
        if (m_AllowPreviewLineTimer <= 0.0f) {
          CreatePreviewLine();
        }
      }

      if (m_PreviewLine != null) {
        // For most brushes, we control the rebuilding of the preview brush,
        // since we have the necessary timing information and the brush doesn't.
        if (m_PreviewLine.AlwaysRebuildPreviewBrush()) {
          RebuildPreviewLine();
        } else {
          m_PreviewLine.DecayBrush();
          m_PreviewLine.UpdatePosition_LS(GetTransformForLine(m_PreviewLine.transform), 1f);
        }

        // Always update preview brush after each frame
        m_PreviewLine.ApplyChangesToVisuals();
      }
    }

    m_PreviousPosition = transform.position;
    UnityEngine.Profiling.Profiler.EndSample();
  }

  /// Non-playback case:
  /// - Update the stroke based on the object's position.
  /// - Save off control points
  /// - Play audio.
  public void UpdateLineFromObject() {
    var xf_LS = GetTransformForLine(m_CurrentLine.transform, Coords.AsRoom[transform]);

    if (!PointerManager.m_Instance.IsMainPointerProcessingLine() && m_CurrentCreator != null) {
      var straightEdgeGuide = PointerManager.m_Instance.StraightEdgeGuide;

      if (straightEdgeGuide.SnapEnabled) {
        // Snapping should be applied before symmetry, and lift is applied
        // after symmetry, so redo both.

        TrTransform xfMain_RS = Coords.AsRoom[PointerManager.m_Instance.MainPointer.transform];
        xfMain_RS.translation = Coords.CanvasPose * straightEdgeGuide.GetTargetPos();
        TrTransform xfSymmetry_RS = PointerManager.m_Instance.GetSymmetryTransformFor(
            this, xfMain_RS);
        xf_LS = GetTransformForLine(m_CurrentLine.transform, xfSymmetry_RS);
      }

      m_ControlPoints.Clear();
      m_ControlPoints.AddRange(m_CurrentCreator.GetPoints(xf_LS));
      float scale = xf_LS.scale;
      m_CurrentLine.ResetBrushForPreview(
          TrTransform.TRS(m_ControlPoints[0].m_Pos, m_ControlPoints[0].m_Orient, scale));
      for (int i = 0; i < m_ControlPoints.Count; ++i) {
        if (m_CurrentLine.IsOutOfVerts()) {
          m_ControlPoints.RemoveRange(i, m_ControlPoints.Count - i);
          break;
        }
        m_CurrentLine.UpdatePosition_LS(
            TrTransform.TRS(m_ControlPoints[i].m_Pos, m_ControlPoints[i].m_Orient, scale),
            m_ControlPoints[i].m_Pressure);
      }
      UpdateLineVisuals();
      return;
    }

    bool bQuadCreated = m_CurrentLine.UpdatePosition_LS(xf_LS, m_CurrentPressure);

    // TODO: let brush take care of storing control points, not us
    SetControlPoint(xf_LS, isKeeper: bQuadCreated);

    // TODO: Pointers should hold a reference to the stencil they're painting on.  This
    // is a hacky temporary check to ensure mirrored pointers don't add to the lift of
    // the active stencil.
    if (PointerManager.m_Instance.MainPointer == this) {
      // Increase stencil lift if we're painting on one.
      StencilWidget stencil = WidgetManager.m_Instance.ActiveStencil;
      if (stencil != null && m_CurrentCreator == null) {
        float fPointerMovement_CS = GetMovementDelta() / Coords.CanvasPose.scale;
        stencil.AdjustLift(fPointerMovement_CS);
        m_LineLength_CS += fPointerMovement_CS;
      }
    }

    UpdateLineVisuals();

    // Update desired brush audio
    if (m_AudioSources.Length > 0) {
      float fMovementSpeed = Vector3.Distance(m_PreviousPosition, transform.position) /
        Time.deltaTime;

      float fVelRangeRange = m_BrushAudioVolumeVelocityRange.y - m_BrushAudioVolumeVelocityRange.x;
      float fVolumeRatio = Mathf.Clamp01((fMovementSpeed - m_BrushAudioVolumeVelocityRange.x) / fVelRangeRange);
      m_AudioVolumeDesired = fVolumeRatio;

      float fPitchRangeRange = m_BrushAudioPitchVelocityRange.y - m_BrushAudioPitchVelocityRange.x;
      float fPitchRatio = Mathf.Clamp01((fMovementSpeed - m_BrushAudioPitchVelocityRange.x) / fPitchRangeRange);
      m_AudioPitchDesired = m_BrushAudioBasePitch + (fPitchRatio * m_BrushAudioMaxPitchShift);
    }
  }

  /// Playback case:
  /// - Update stroke based on the passed transform (in local coordinates)
  /// - Do _not_ apply any normal adjustment; it's baked into the control point
  /// - Do not update the mesh
  /// TODO: replace with a bulk-ControlPoint API
  public void UpdateLineFromControlPoint(PointerManager.ControlPoint cp) {
    float scale = m_CurrentLine.StrokeScale;
    m_CurrentLine.UpdatePosition_LS(
        TrTransform.TRS(cp.m_Pos, cp.m_Orient, scale), cp.m_Pressure);
  }

  /// Bulk control point addition
  public void UpdateLineFromStroke(Stroke stroke) {
    RdpStrokeSimplifier simplifier = App.Instance.IsLoading()
        ? QualityControls.m_Instance.StrokeSimplifier
        : QualityControls.m_Instance.UserStrokeSimplifier;
    if (simplifier.Level > 0.0f) {
      simplifier.CalculatePointsToDrop(stroke, CurrentBrushScript);
    }
    float scale = m_CurrentLine.StrokeScale;
    foreach (var cp in stroke.m_ControlPoints.Where((x, i) => !stroke.m_ControlPointsToDrop[i])) {
      m_CurrentLine.UpdatePosition_LS(TrTransform.TRS(cp.m_Pos, cp.m_Orient, scale), cp.m_Pressure);
    }
  }

  public void UpdateLineVisuals() {
    m_CurrentLine.ApplyChangesToVisuals();
  }

  public void SetPreviewLineDelayTimer() {
    m_AllowPreviewLineTimer = 0.25f;
  }

  public void AllowPreviewLine(bool bAllow) {
    if (m_AllowPreviewLine != bAllow) {
      SetPreviewLineDelayTimer();
      if (!bAllow) {
        DisablePreviewLine();
      }
    }
    m_AllowPreviewLine = bAllow;
  }

  void CreatePreviewLine() {
    UnityEngine.Profiling.Profiler.BeginSample("PointerScript.CreatePreviewLine");
    if (m_PreviewLine == null && m_CurrentBrush.m_BrushPrefab != null) {
      // We don't have the transform for the line because it hasn't been created
      // yet, but we can assume that the line transform == the canvas transform,
      // since the line is parented to the canvas with an identity local transform.
      // See also the TODO in GetTransformForLine; fixing that will resolve this wart.
      Transform notReallyTheLineTransformButCloseEnough = App.Instance.m_CanvasTransform;
      TrTransform xf_LS = GetTransformForLine(notReallyTheLineTransformButCloseEnough);
      BaseBrushScript line = BaseBrushScript.Create(
          App.Instance.m_CanvasTransform,
          xf_LS,
          m_CurrentBrush, m_CurrentColor, m_CurrentBrushSize);

      line.gameObject.name = string.Format("Preview {0}", m_CurrentBrush.m_Description);
      line.SetPreviewMode();

      m_PreviewLine = line;
      ResetPreviewProperties();

      m_PreviewControlPoints.Clear();
    }
    UnityEngine.Profiling.Profiler.EndSample();
  }

  public void DisablePreviewLine() {
    if (m_PreviewLine) {
      // TODO: Remove this (and other) calls to DestroyMesh() and do it inside OnDestroy() instead?
      m_PreviewLine.DestroyMesh();
      Destroy(m_PreviewLine.gameObject);
      m_PreviewLine = null;
    }
  }

  void ResetPreviewProperties() {
    if (m_PreviewLine) {
      m_PreviewLine.SetPreviewProperties(m_CurrentColor, m_CurrentBrushSize);
    }
    if (m_PreviewLight) {
      m_PreviewLight.range = m_CurrentBrushSize * m_PreviewLightScalar;
      m_PreviewLight.color = m_CurrentColor;
    }
  }

  public void SetPressure(float fPressure) {
    m_CurrentPressure = fPressure;
  }

  public void SetColor(Color rColor) {
    m_CurrentColor = rColor;
    ResetPreviewProperties();
    UpdateTintableRenderers();
    Shader.SetGlobalColor("_BrushColor", m_CurrentColor);
  }

  void UpdateTintableRenderers() {
    float fGlowIntensity = m_GlowPreviewEnabled * m_GlowPreviewIntensity;
    if (m_BrushSizeIndicator) {
      m_BrushSizeIndicator.GetComponent<Renderer>().material.color = m_CurrentColor * (1.0f + fGlowIntensity);
    }
    if (m_Mesh) {
      m_Mesh.material.color = m_CurrentColor * (1.0f + fGlowIntensity);
      InputManager.m_Instance.TintControllersAndHMD(m_CurrentColor, m_BasePreviewIntensity, fGlowIntensity);
    }
  }

  void SetTintableIntensity(TiltBrush.BrushDescriptor rBrush) {
    if (rBrush.Material.HasProperty("_EmissionGain")) {
      float emission = rBrush.Material.GetFloat("_EmissionGain");
      m_GlowPreviewEnabled = (emission > .25f) ? 1.0f : 0.0f;
      if (m_PreviewLight) {
        m_PreviewLight.enabled = m_AllowPreviewLight;
      }
    } else {
      m_GlowPreviewEnabled = 0.0f;
      if (m_PreviewLight) {
        m_PreviewLight.enabled = false;
      }
    }
  }

  public void SetBrush(BrushDescriptor rBrush) {
    if (rBrush != null && rBrush != m_CurrentBrush) {
      m_BrushSizeRange = rBrush.m_BrushSizeRange;

      if (m_LastUsedBrushSize_CS != null) {
        BrushSizeAbsolute = Coords.CanvasPose.scale * m_LastUsedBrushSize_CS.Value;
      } else {
        BrushSize01 = 0.5f;
      }
      MarkBrushSizeUsed();

      DisablePreviewLine();
      SetTintableIntensity(rBrush);
      UpdateTintableRenderers();
      OnBrushChange(rBrush);
    }

    m_CurrentBrush = rBrush;
    ResetAudio();
  }

  public void ShowSizeIndicator(bool show) {
    m_BrushSizeIndicator.gameObject.SetActive(show);
  }

  void _SetBrushSizeAbsolute(float value) {
    m_CurrentBrushSize = value;
    if (m_BrushSizeIndicator) {
      Vector3 vLocalScale = m_InitialBrushSizeScale * m_CurrentBrushSize;
      m_BrushSizeIndicator.localScale = vLocalScale;
    }
    ResetPreviewProperties();
  }

  public void EnableRendering(bool bEnable) {
    for (int i = 0; i < m_PrimaryMeshes.Length; ++i) {
      m_PrimaryMeshes[i].enabled = bEnable;
    }
  }

  /// Record the tranform as a control point.
  /// If the most-recent point is a keeper, append a new control point.
  /// Otherwise, the most-recent point is a keeper, and will be overwritten.
  ///
  /// The parameter "keep" specifies whether the newly-written point is a keeper.
  ///
  /// The current pointer is /not/ queried to get the transform of the new
  /// control point. Instead, caller is responsible for passing in the same
  /// xf that was passed to line.UpdatePosition_LS()
  public void SetControlPoint(TrTransform lastSpawnXf_LS, bool isKeeper) {
    PointerManager.ControlPoint rControlPoint;
    rControlPoint.m_Pos = lastSpawnXf_LS.translation;
    rControlPoint.m_Orient = lastSpawnXf_LS.rotation;
    rControlPoint.m_Pressure = m_CurrentPressure;
    rControlPoint.m_TimestampMs = (uint)(App.Instance.CurrentSketchTime * 1000);

    if (m_ControlPoints.Count == 0 || m_LastControlPointIsKeeper) {
      m_ControlPoints.Add(rControlPoint);
      if (m_ShowDebugControlPoints) {
        m_DebugViewControlPoints.Add(rControlPoint.m_Pos);
      }
    } else {
      m_ControlPoints[m_ControlPoints.Count - 1] = rControlPoint;
      if (m_ShowDebugControlPoints) {
        m_DebugViewControlPoints[m_DebugViewControlPoints.Count - 1] = rControlPoint.m_Pos;
      }
    }

    m_LastControlPointIsKeeper = isKeeper;
  }

  /// Pass a Canvas parent, and a transform in that canvas's space.
  /// If overrideDesc passed, use that for the visuals -- m_CurrentBrush does not change.
  public void CreateNewLine(CanvasScript canvas, TrTransform xf_CS,
                            ParametricStrokeCreator creator, BrushDescriptor overrideDesc = null) {
    // If straightedge is enabled, we may have a minimum size requirement.
    // Initialize parametric stroke creator for our type of straightedge.
    // Maybe change the brush to a proxy brush.
    BrushDescriptor desc = overrideDesc != null ? overrideDesc : m_CurrentBrush;
    m_CurrentCreator = creator;

    // Parametric creators want control over the brush size.
    if (m_CurrentCreator != null) {
      m_ParametricCreatorBackupStrokeSize = m_CurrentBrushSize;
      m_CurrentBrushSize = m_CurrentCreator.ProcessBrushSize(m_CurrentBrushSize);
    }

    m_LastUsedBrushSize_CS = (1/Coords.CanvasPose.scale) * BrushSizeAbsolute;
    m_LineLength_CS = 0.0f;

    m_CurrentLine = BaseBrushScript.Create(
        canvas.transform, xf_CS,
        desc, m_CurrentColor, m_CurrentBrushSize);
  }

  /// Like BeginLineFromMemory + EndLineFromMemory
  /// To help catch bugs in higher-level stroke code, it is considered
  /// an error unless the stroke is in state NotCreated.
  public void RecreateLineFromMemory(Stroke stroke) {
    if (stroke.m_Type != Stroke.Type.NotCreated) {
      throw new InvalidOperationException();
    }
    if (BeginLineFromMemory(stroke, stroke.Canvas) == null) {
      // Unclear why it would have failed, but okay.
      // I guess we keep the old version?
      Debug.LogError("Unexpected error recreating line");
      return;
    }

    UpdateLineFromStroke(stroke);

    // It's kind of warty that this needs to happen; brushes should probably track
    // the mesh-dirty state and flush it in Finalize().
    // TODO: Check if this is still necessary now that QuadStripBrushStretchUV
    // flushes pending geometry changes in Finalize*Brush()
    m_CurrentLine.ApplyChangesToVisuals();

    // Copy in new contents

    if (App.Config.m_UseBatchedBrushes && m_CurrentLine.m_bCanBatch) {
      var subset = m_CurrentLine.FinalizeBatchedBrush();

      stroke.m_Type = Stroke.Type.BatchedBrushStroke;
      stroke.m_IntendedCanvas = null;
      Debug.Assert(stroke.m_Object == null);
      stroke.m_BatchSubset = subset;
      stroke.m_BatchSubset.m_Stroke = stroke;

      m_CurrentLine.DestroyMesh();
      Destroy(m_CurrentLine.gameObject);
    } else {
      m_CurrentLine.FinalizeSolitaryBrush();

      stroke.m_Type = Stroke.Type.BrushStroke;
      stroke.m_IntendedCanvas = null;
      Debug.Assert(stroke.m_BatchSubset == null);
      stroke.m_Object = m_CurrentLine.gameObject;
      stroke.m_Object.GetComponent<BaseBrushScript>().Stroke = stroke;
    }

    m_CurrentLine = null;
  }

  // During playback, rMemoryObjectForPlayback is non-null, and strokeFlags should not be passed.
  // otherwise, rMemoryObjectForPlayback is null, and strokeFlags should be valid.
  // When non-null, rMemoryObjectForPlayback corresponds to the current line.
  public void DetachLine(
      bool bDiscard,
      Stroke rMemoryObjectForPlayback,
      SketchMemoryScript.StrokeFlags strokeFlags=SketchMemoryScript.StrokeFlags.None) {
    if (rMemoryObjectForPlayback != null) {
      Debug.Assert(strokeFlags == SketchMemoryScript.StrokeFlags.None);
    }

    if (bDiscard) {
      m_CurrentLine.DestroyMesh();
      Destroy(m_CurrentLine.gameObject);
    } else if (App.Config.m_UseBatchedBrushes && m_CurrentLine.m_bCanBatch) {
      // Save line: batched case

      // ClearRedo() is called by MemorizeXxx(), but also do it earlier as a
      // fragmentation optimization; this allows GenerateBatchSubset() to reuse the
      // reclaimed space.
      // NB: batching currently handles fragmentation poorly, so consider this
      // optimization necessary.
      if (rMemoryObjectForPlayback == null) {
        SketchMemoryScript.m_Instance.ClearRedo();
      }

      var subset = m_CurrentLine.FinalizeBatchedBrush();
      if (rMemoryObjectForPlayback == null) {
        SketchMemoryScript.m_Instance.MemorizeBatchedBrushStroke(
            subset, m_CurrentColor,
            m_CurrentBrush.m_Guid,
            m_CurrentBrushSize,
            m_CurrentLine.StrokeScale,
            m_ControlPoints, strokeFlags,
            WidgetManager.m_Instance.ActiveStencil, m_LineLength_CS, m_CurrentLine.RandomSeed);
      } else {
        //if we're in playback, patch up the in-memory stroke with its position in the batch
        rMemoryObjectForPlayback.m_Type = Stroke.Type.BatchedBrushStroke;
        rMemoryObjectForPlayback.m_Object = null;
        rMemoryObjectForPlayback.m_BatchSubset = subset;
        subset.m_Stroke = rMemoryObjectForPlayback;
      }

      //destroy original stroke, as it's now part of the batch stroke
      m_CurrentLine.DestroyMesh();
      Destroy(m_CurrentLine.gameObject);

      // recreate the stroke if it's just been drawn by the user, so we can run the simplifier on it.
      if (!App.Instance.IsLoading() &&
          QualityControls.m_Instance.UserStrokeSimplifier.Level > 0.0f &&
          m_CurrentLine.Descriptor.m_SupportsSimplification) {
        var stroke = subset.m_Stroke;
        stroke.InvalidateCopy();
        stroke.Uncreate();
        stroke.Recreate();
      }
    } else {
      // Save line: non-batched case

      if (rMemoryObjectForPlayback == null) {
        SketchMemoryScript.m_Instance.MemorizeBrushStroke(
            m_CurrentLine, m_CurrentColor,
            m_CurrentBrush.m_Guid,
            m_CurrentBrushSize,
            m_CurrentLine.StrokeScale,
            m_ControlPoints, strokeFlags,
            WidgetManager.m_Instance.ActiveStencil, m_LineLength_CS);
      } else {
        rMemoryObjectForPlayback.m_Type = Stroke.Type.BrushStroke;
        rMemoryObjectForPlayback.m_BatchSubset = null;
        m_CurrentLine.Stroke = rMemoryObjectForPlayback;
      }

      //copy master brush over to current line
      m_CurrentLine.FinalizeSolitaryBrush();
    }

    if (rMemoryObjectForPlayback == null) {
      SilenceAudio();
    }

    m_CurrentLine = null;
    // Restore brush size if our parametric creator had to modify it.
    if (m_CurrentCreator != null) {
      m_CurrentBrushSize = m_ParametricCreatorBackupStrokeSize;
    }
    m_CurrentCreator = null;
    m_ControlPoints.Clear();
  }

  public bool ShouldCurrentLineEnd() {
    if (m_CurrentLine) {
      return m_CurrentLine.ShouldCurrentLineEnd();
    }
    return false;
  }

  // Returns false if line should be kept or has a stamping implementation
  // Otherwise returns true
  public bool ShouldDiscardCurrentLine() {
    return (m_ControlPoints.Count <= 1) || (m_CurrentLine == null) || m_CurrentLine.ShouldDiscard();
  }

  public GameObject BeginLineFromMemory(Stroke stroke, CanvasScript canvas) {
    BrushDescriptor rBrush = BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid);
    if (rBrush == null) {
      // Ignore stroke
      return null;
    }

    if (m_PreviewLight) {
      m_PreviewLight.enabled = false;
    }

    var cp0 = stroke.m_ControlPoints[0];
    var xf_CS = TrTransform.TRS(cp0.m_Pos, cp0.m_Orient, stroke.m_BrushScale);
    var xf_RS = canvas.Pose * xf_CS;

    // This transform used to be incorrect, but we didn't notice.
    // That implies this isn't necessary?
    transform.position = xf_RS.translation;
    transform.rotation = xf_RS.rotation;

    m_CurrentBrush = rBrush;
    m_CurrentBrushSize = stroke.m_BrushSize;
    m_CurrentPressure = cp0.m_Pressure;
    m_CurrentColor = stroke.m_Color;
    CreateNewLine(canvas, xf_CS, null);
    m_CurrentLine.SetIsLoading();
    m_CurrentLine.RandomSeed = stroke.m_Seed;

    return m_CurrentLine.gameObject;
  }

  public void EndLineFromMemory(Stroke stroke, bool discard=false) {
    DetachLine(discard, stroke);

    if (!discard) {
      TiltMeterScript.m_Instance.AdjustMeter(stroke, true);
    }

    if (m_PreviewLight) {
      m_PreviewLight.enabled = false;
    }
  }

  public void ResetAudio() {
    for (int i = 0; i < m_AudioSources.Length; i++) {
      m_AudioSources[i].Stop();
      if (m_CurrentBrush != null) {
        if (i < m_CurrentBrush.m_BrushAudioLayers.Length) {
          m_AudioSources[i].clip = m_CurrentBrush.m_BrushAudioLayers[i];
        } else {
          m_AudioSources[i].clip = null;
        }
      }
    }

    m_CurrentTotalVolume = 0f;

    m_BrushAudioBasePitch = 1f;
    m_BrushAudioMaxPitchShift = m_CurrentBrush.m_BrushAudioMaxPitchShift;
    m_BrushAudioMaxVolume = m_CurrentBrush.m_BrushAudioMaxVolume;
    m_BrushAudioAdjustSpeedUp = CurrentBrush.m_BrushVolumeUpSpeed;
    m_BrushAudioAdjustSpeedDown = CurrentBrush.m_BrushVolumeDownSpeed;

    m_BrushAudioVolumeVelocityRange = new Vector2(.5f, 10f * m_CurrentBrush.m_VolumeVelocityRangeMultiplier);

    SilenceAudio();

    for (int i = 0; i < m_AudioSources.Length; i++) {
      //if our pointer object isn't active, calling play will spit a harmless error at us
      if (gameObject.activeSelf && m_AudioSources[i].gameObject.activeSelf
          && AudioManager.Enabled) {
        m_AudioSources[i].Play();
      }
    }
  }

  void SilenceAudio() {
    if (m_AudioSources.Length > 0){
      m_AudioVolumeDesired = 0.0f;
      m_AudioPitchDesired = 1.0f;
    }
  }

  public void SetAudioClipForPlayback() {
    for (int i=0; i<m_AudioSources.Length; i++) {
      m_AudioSources[i].Stop();
    }
    if (m_AudioSources.Length > 0  && m_AudioSources[0] != null) {
      m_AudioSources[0].clip = m_BrushPlaybackAudioClip;
      m_AudioVolumeDesired = 1.0f;
      m_AudioPitchDesired = 1.0f;
      m_BrushAudioAdjustSpeedUp = 4f;
      m_BrushAudioAdjustSpeedDown = 4f;
      if (AudioManager.Enabled) {
        m_AudioSources[0].Play();
      }
    }
  }

  void OnDrawGizmos() {
    if (m_ShowDebugControlPoints) {
      Gizmos.color = Color.yellow;
      for (int i = 0; i < m_DebugViewControlPoints.Count; ++i) {
        Gizmos.DrawSphere(m_DebugViewControlPoints[i], 0.05f);
      }
    }
  }
}
}  // namespace TiltBrush
