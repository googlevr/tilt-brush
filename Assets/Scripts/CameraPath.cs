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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush {

public class KnotDescriptor {
  public CameraPathKnot knot;
  // Refers to a part of the knot in reference, respective to the
  // type of knot.  This is usually cast to an enum defined by the knot class.
  // For example, CameraPathPositionKnot has 3 controls in enum ControlType:
  // Knot == 0, TangentControlForward == 1, and TangentControlBack == 2.
  public int control = CameraPathKnot.kDefaultControl;
  // For PositionKnots, this value refers to the index into the path.  0 is the head
  // knot, and CameraPath.m_PositionKnots.Count - 1 is the tail.  This is null for
  // path constrained knots.
  public int? positionKnotIndex;
  // For path constrained knots (SpeedKnot, FovKnot, etc.), this value refers to
  // the placement of the knot on the path as a [0:n-1] parametric value where n
  // is the number of PositionKnots.  This is null for position knots.
  public PathT? pathT;

  public void Set(KnotDescriptor k) {
    Set(k.knot, k.control, k.positionKnotIndex, k.pathT);
  }

  public void Set(CameraPathKnot _k, int _c, int? _ki, PathT? _t) {
    knot = _k;
    control = _c;
    positionKnotIndex = _ki;
    pathT = _t;
  }
}

/// This struct wraps a t value which is only used for finding a world space position
/// on a camera path.
public struct PathT : IEquatable<PathT> {
  public static int Cmp(PathT lhs, PathT rhs) {
    { int cmp = lhs.t.CompareTo(rhs.t); return cmp; }
  }

  private float t;  // in [0, CameraPath.PositionKnots.Count-1)

  public float T => t;

  public PathT(float _t) {
    t = _t;
  }
 
  public PathT(PathT pt) {
    t = pt.T;
  }
  public void Zero() {
    t = 0.0f;
  }

  public void Clamp(int positionCount) {
    t = Mathf.Clamp(t, 0.0f, Mathf.Max((positionCount - 1) - 0.0001f, 0.0f));
  }

  public int Floor() {
    return Mathf.FloorToInt(t);
  }

  public static bool operator <(PathT lhs, PathT rhs)  { return Cmp(lhs, rhs) < 0; }
  public static bool operator >(PathT lhs, PathT rhs)  { return Cmp(lhs, rhs) > 0; }
  public static bool operator <=(PathT lhs, PathT rhs) { return Cmp(lhs, rhs) <= 0; }
  public static bool operator >=(PathT lhs, PathT rhs) { return Cmp(lhs, rhs) >= 0; }
  public static bool operator ==(PathT lhs, PathT rhs) { return Cmp(lhs, rhs) == 0; }
  public static bool operator !=(PathT lhs, PathT rhs) { return Cmp(lhs, rhs) != 0; }
  public bool Equals(PathT other) { return Cmp(this, other) == 0; }
  public override bool Equals(object obj) {
    return obj is PathT other && Equals(other);
  }
  public override int GetHashCode() {
    unchecked {
      return t.GetHashCode();
    }
  }
}

public class KnotSegmentStep {
  public Vector3 start_RS;
  public Vector3 end_RS;
  public float length_CS;

  public float RefreshLength() {
    length_CS = Vector3.Distance(start_RS, end_RS) / Coords.CanvasPose.scale;
    return length_CS;
  }
}

public class KnotSegment {
  public LineRenderer renderer;
  public Bounds extents;
  public KnotSegmentStep[] steps;
  public float length;
}

public class CameraPath {
  private const int kNumSegmentPoints = 30;
  private const float kEpsilon = 0.0001f;

  public enum EndType {
    None,
    Head,
    Tail
  }

  // Unsorted list used for parsing all types of knots.
  public List<CameraPathKnot> AllKnots;
  // Sorted list of positions which define path.
  public List<CameraPathPositionKnot> PositionKnots;
  // List of rotation points sorted relative to the m_PositionKnots.
  public List<CameraPathRotationKnot> RotationKnots;
  // List of speed points sorted relative to the m_PositionKnots.
  public List<CameraPathSpeedKnot> SpeedKnots;
  // List of fov points sorted relative to the m_PositionKnots.
  public List<CameraPathFovKnot> FovKnots;
  public List<KnotSegment> Segments;

  private Transform m_Widget;
  private float m_SegmentRadius;
  private float m_SegmentStepInterval;
  private float m_EndRadius;
  private float m_DefaultSpeed;
  private float m_DefaultFov;
  private bool m_PathLoops;

  private KnotDescriptor m_LastPlacedKnotInfo;

  // Debug drawing.
  private Vector3 m_GizmoBasePos;
  private Vector3 m_GizmoProjectedPosition;

  public bool PathLoops {
    get => m_PathLoops;
    set {
      m_PathLoops = value;
      // If the Path is looping, hide the head.
      // Show the head if we're not looping, unless camera paths are invisible.
      if (NumPositionKnots > 0) {
        bool headHidden = PathLoops || !WidgetManager.m_Instance.CameraPathsVisible;
        PositionKnots[0].gameObject.SetActive(!headHidden);
        PositionKnots[NumPositionKnots - 1].SetVisuallySpecial(headHidden);
      }
    }
  }

  public int NumPositionKnots {
    get { return PositionKnots.Count; }
  }

  public KnotDescriptor LastPlacedKnotInfo {
    get { return m_LastPlacedKnotInfo; }
  }

  public CameraPath(Transform widget, float segmentRad, float endRad, float speed, float fov) {
    AllKnots = new List<CameraPathKnot>();
    PositionKnots = new List<CameraPathPositionKnot>();
    RotationKnots = new List<CameraPathRotationKnot>();
    SpeedKnots = new List<CameraPathSpeedKnot>();
    FovKnots = new List<CameraPathFovKnot>();
    Segments = new List<KnotSegment>();
    m_LastPlacedKnotInfo = new KnotDescriptor();

    m_Widget = widget;
    m_SegmentRadius = segmentRad;
    m_EndRadius = endRad;
    m_DefaultSpeed = speed;
    m_DefaultFov = fov;
    m_SegmentStepInterval = 1.0f / (kNumSegmentPoints - 1);
  }

  public void Reset() {
    for (int i = 0; i < AllKnots.Count; ++i) {
      UnityEngine.Object.Destroy(AllKnots[i].gameObject);
    }
    AllKnots.Clear();
    PositionKnots.Clear();
    RotationKnots.Clear();
    SpeedKnots.Clear();
    FovKnots.Clear();

    for (int i = 0; i < Segments.Count; ++i) {
      // On application shutdown, this was throwing an exception.
      if (Segments[i].renderer != null) {
        UnityEngine.Object.Destroy(Segments[i].renderer.gameObject);
      }
    }
    Segments.Clear();
  }

  public void SetKnotsActive(bool active) {
    for (int i = 0; i < AllKnots.Count; ++i) {
      AllKnots[i].gameObject.SetActive(active);
    }

    for (int i = 0; i < Segments.Count; ++i) {
      Segments[i].renderer.gameObject.SetActive(active);
    }

    if (active) {
      for (int i = 0; i < Segments.Count; ++i) {
        RefreshSegment(i);
      }
    }

    // We validate here because the path looping state affects head knot visibility.
    ValidatePathLooping();
  }

  public bool IsPositionNearHead(Vector3 pos) {
    if (NumPositionKnots == 0) { return false; }
    CameraPathPositionKnot pk0 = PositionKnots[0];
    return Vector3.Distance(pk0.transform.position, pos) < kEpsilon;
  }

  public bool ShouldPathLoop() {
    if (NumPositionKnots > 2) {
      return IsPositionNearHead(PositionKnots[NumPositionKnots - 1].transform.position);
    }
    return false;
  }

  public void ValidatePathLooping() {
    bool pathShouldLoop = false;
    if (NumPositionKnots > 2) {
      CameraPathPositionKnot pk0 = PositionKnots[0];
      CameraPathPositionKnot pkn1 = PositionKnots[NumPositionKnots - 1];
      if (Mathf.Abs(pk0.TangentMagnitude - pkn1.TangentMagnitude) < kEpsilon) {
        if (Vector3.Distance(pk0.transform.position, pkn1.transform.position) < kEpsilon) {
          if (LightsControlScript.IsLightRotationCloseEnough(
              pk0.transform.rotation, pkn1.transform.rotation)) {
            pathShouldLoop = true;
          }
        }
      }
    }
    PathLoops = pathShouldLoop;
  }

  static public KnotSegment CreateSegment(Transform parent) {
    KnotSegment segment = new KnotSegment();
    GameObject go =
          UnityEngine.Object.Instantiate(WidgetManager.m_Instance.CameraPathKnotSegmentPrefab);
    go.transform.parent = parent;
    segment.renderer = go.GetComponent<LineRenderer>();
    segment.renderer.positionCount = kNumSegmentPoints;
    segment.renderer.material.color = GrabWidget.InactiveGrey;
    segment.steps = new KnotSegmentStep[kNumSegmentPoints - 1];
    for (int j = 0; j < kNumSegmentPoints - 1; ++j) {
      segment.steps[j] = new KnotSegmentStep();
    }
    return segment;
  }

  /// The CameraPath class will not clean up this knot gameObject.  It is the duty of the caller
  /// to clean up their mess.
  public CameraPathSpeedKnot CreateSpeedKnot(PathT pathT) {
    GameObject go = UnityEngine.Object.Instantiate(
        WidgetManager.m_Instance.CameraPathSpeedKnotPrefab,
        GetPosition(pathT), Quaternion.identity);
    return go.GetComponent<CameraPathSpeedKnot>();
  }

  public void AddSpeedKnot(CameraPathSpeedKnot knot, PathT pathT) {
    knot.transform.parent = m_Widget;
    knot.PathT = pathT;
    knot.DistanceAlongSegment = GetSegmentDistanceToT(pathT);
    knot.RefreshVisuals();

    AllKnots.Add(knot);
    AudioManager.m_Instance.ShowHideWidget(true, knot.transform.position);

    // Insert the speed knot in to our sorted list.
    int insertIndex = SpeedKnots.Count;
    for (int i = 0; i < SpeedKnots.Count; ++i) {
      if (SpeedKnots[i].PathT < pathT) {
        continue;
      }
      insertIndex = i;
      break;
    }
    SpeedKnots.Insert(insertIndex, knot);
    m_LastPlacedKnotInfo.Set(knot,
        (int)CameraPathSpeedKnot.ControlType.SpeedControl, null, pathT);
  }

  /// The CameraPath class will not clean up this knot gameObject.  It is the duty of the caller
  /// to clean up their mess.
  public CameraPathFovKnot CreateFovKnot(PathT pathT) {
    GameObject go = UnityEngine.Object.Instantiate(
        WidgetManager.m_Instance.CameraPathFovKnotPrefab,
        GetPosition(pathT), Quaternion.identity);
    return go.GetComponent<CameraPathFovKnot>();
  }

  public void AddFovKnot(CameraPathFovKnot knot, PathT pathT) {
    knot.transform.parent = m_Widget;
    knot.PathT = pathT;
    knot.DistanceAlongSegment = GetSegmentDistanceToT(pathT);
    knot.RefreshVisuals();

    AllKnots.Add(knot);
    AudioManager.m_Instance.ShowHideWidget(true, knot.transform.position);

    // Insert the fov knot in to our sorted list.
    int insertIndex = FovKnots.Count;
    for (int i = 0; i < FovKnots.Count; ++i) {
      if (FovKnots[i].PathT < pathT) {
        continue;
      }
      insertIndex = i;
      break;
    }
    FovKnots.Insert(insertIndex, knot);
    m_LastPlacedKnotInfo.Set(knot,
        (int)CameraPathFovKnot.ControlType.FovControl, null, pathT);
  }

  /// The CameraPath class will not clean up this knot gameObject.  It is the duty of the caller
  /// to clean up their mess.
  public CameraPathRotationKnot CreateRotationKnot(PathT pathT, Quaternion rot) {
    GameObject go = UnityEngine.Object.Instantiate(
        WidgetManager.m_Instance.CameraPathRotationKnotPrefab,
        GetPosition(pathT), rot);
    return go.GetComponent<CameraPathRotationKnot>();
  }

  public void AddRotationKnot(CameraPathRotationKnot knot, PathT pathT) {
    knot.transform.parent = m_Widget;
    knot.PathT = pathT;
    knot.DistanceAlongSegment = GetSegmentDistanceToT(pathT);

    AllKnots.Add(knot);
    AudioManager.m_Instance.ShowHideWidget(true, knot.transform.position);

    // Insert the rotation knot in to our sorted list.
    int insertIndex = RotationKnots.Count;
    for (int i = 0; i < RotationKnots.Count; ++i) {
      if (RotationKnots[i].PathT < pathT) {
        continue;
      }
      insertIndex = i;
      break;
    }
    RotationKnots.Insert(insertIndex, knot);
    // Align quaternions on all rotation knots so we don't have unexpected camera flips
    // when calculating rotation as we walk the path.
    RefreshRotationKnotPolarities();
    m_LastPlacedKnotInfo.Set(knot, CameraPathKnot.kDefaultControl, null, pathT);
  }

  public void RefreshRotationKnotPolarities() {
    for (int i = 0; i < RotationKnots.Count - 1; ++i) {
      Quaternion r0 = RotationKnots[i].transform.rotation;
      Quaternion r1 = RotationKnots[i + 1].transform.rotation;
      float dot = Quaternion.Dot(r0, r1);
      if (dot < 0.0f) {
        RotationKnots[i + 1].transform.rotation = r1.Negated();
      }
    }
  }

  /// The CameraPath class will not clean up this knot gameObject.  It is the duty of the caller
  /// to clean up their mess.
  public CameraPathPositionKnot CreatePositionKnot(Vector3 pos) {
    GameObject go = UnityEngine.Object.Instantiate(
        WidgetManager.m_Instance.CameraPathPositionKnotPrefab, pos, Quaternion.identity);
    return go.GetComponent<CameraPathPositionKnot>();
  }

  public void InsertPositionKnot(CameraPathPositionKnot knot, int index) {
    knot.transform.parent = m_Widget;
    knot.RefreshVisuals();

    AllKnots.Add(knot);
    PositionKnots.Insert(index, knot);
    ValidatePathLooping();
    RefreshPathAfterPositionKnotAdded(index);
    AudioManager.m_Instance.ShowHideWidget(true, knot.transform.position);

    // The ControlType of m_LastPlacedKnotInfo is dependent on our index.  When we add to the
    // head, we want the user manipulating the back tangent.  In other cases, the forward tangent.
    // The exception to the head is when there's only one knot.
    CameraPathPositionKnot.ControlType controlType = (index == 0 && PositionKnots.Count > 1) ?
      CameraPathPositionKnot.ControlType.TangentControlBack :
      CameraPathPositionKnot.ControlType.TangentControlForward;
    m_LastPlacedKnotInfo.Set(knot, (int)controlType, index, null);
  }

  public void SortKnotList(CameraPathKnot.Type type) {
    switch(type) {
    case CameraPathKnot.Type.Fov: FovKnots.Sort(CompareKnotsByPathT); break;
    case CameraPathKnot.Type.Rotation: RotationKnots.Sort(CompareKnotsByPathT); break;
    case CameraPathKnot.Type.Speed: SpeedKnots.Sort(CompareKnotsByPathT); break;
    default:
      Debug.Log("Unsupported type passed to SortKnotList " + type);
      break;
    }
  }

  private static int CompareKnotsByPathT(CameraPathKnot a, CameraPathKnot b) {
    return PathT.Cmp(a.PathT, b.PathT);
  }

  void RefreshPathAfterPositionKnotAdded(int insertIndex) {
    // Gather up all the segment lengths in a nicely digestible list.
    float[] prevSegmentLengths = new float[Segments.Count];
    for (int i = 0; i < prevSegmentLengths.Length; ++i) {
      prevSegmentLengths[i] = Segments[i].length;
    }

    if (PositionKnots.Count > 1) {
      // In the event we're inserting at index 0, we want a new segment at index 0 as well.
      int segmentInsert = Mathf.Max(0, insertIndex - 1);
      Segments.Insert(segmentInsert, CreateSegment(m_Widget.transform));

      // Note that it's ok if the values passed to RefreshSegment are out of bounds.
      RefreshSegment(segmentInsert - 1);
      RefreshSegment(segmentInsert);
      RefreshSegment(segmentInsert + 1);
    }

    // Make another nicely digestible list of the current segment lengths.
    float[] newSegmentLengths = new float[Segments.Count];
    for (int i = 0; i < newSegmentLengths.Length; ++i) {
      newSegmentLengths[i] = Segments[i].length;
    }

    // Modify PathT for all rotation knots on or beyond the new segment.
    for (int i = 0; i < RotationKnots.Count; ++i) {
      RecomputeKnotPlacementAfterPositionAdded(
          RotationKnots[i], insertIndex, prevSegmentLengths, newSegmentLengths);
      RefreshRotationKnot(i);
    }

    // Modify PathT for all speed knots on or beyond the new segment.
    for (int i = 0; i < SpeedKnots.Count; ++i) {
      RecomputeKnotPlacementAfterPositionAdded(
          SpeedKnots[i], insertIndex, prevSegmentLengths, newSegmentLengths);
      RefreshSpeedKnot(i);
    }

    // Modify PathT for all fov knots on or beyond the new segment.
    for (int i = 0; i < FovKnots.Count; ++i) {
      RecomputeKnotPlacementAfterPositionAdded(
          FovKnots[i], insertIndex, prevSegmentLengths, newSegmentLengths);
      RefreshFovKnot(i);
    }
  }

  public CameraPathKnot GetKnotAtPosition(Vector3 pos) {
    for (int i = 0; i < RotationKnots.Count; ++i) {
      if (RotationKnots[i].KnotCollisionWithPoint(pos)) {
        return RotationKnots[i];
      }
    }

    for (int i = 0; i < SpeedKnots.Count; ++i) {
      if (SpeedKnots[i].KnotCollisionWithPoint(pos)) {
        return SpeedKnots[i];
      }
    }

    for (int i = 0; i < FovKnots.Count; ++i) {
      if (FovKnots[i].KnotCollisionWithPoint(pos)) {
        return FovKnots[i];
      }
    }

    for (int i = 0; i < PositionKnots.Count; ++i) {
      if (PositionKnots[i].KnotCollisionWithPoint(pos)) {
        return PositionKnots[i];
      }
    }
    return null;
  }

  public void RemoveKnot(CameraPathKnot knot) {
    CameraPathRotationKnot rotationKnot = knot as CameraPathRotationKnot;
    if (rotationKnot != null) {
      AllKnots.Remove(knot);
      RotationKnots.Remove(rotationKnot);
      AudioManager.m_Instance.ShowHideWidget(false, knot.transform.position);
      return;
    }

    CameraPathSpeedKnot speedKnot = knot as CameraPathSpeedKnot;
    if (speedKnot != null) {
      AllKnots.Remove(knot);
      SpeedKnots.Remove(speedKnot);
      AudioManager.m_Instance.ShowHideWidget(false, knot.transform.position);
      return;
    }

    CameraPathFovKnot fovKnot = knot as CameraPathFovKnot;
    if (fovKnot != null) {
      AllKnots.Remove(knot);
      FovKnots.Remove(fovKnot);
      AudioManager.m_Instance.ShowHideWidget(false, knot.transform.position);
      return;
    }

    CameraPathPositionKnot positionKnot = knot as CameraPathPositionKnot;
    if (positionKnot != null) {
      AllKnots.Remove(knot);
      int positionIndex = PositionKnots.IndexOf(positionKnot);
      PositionKnots.RemoveAt(positionIndex);
      ValidatePathLooping();
      RefreshPathAfterPositionRemoval(positionIndex);
      AudioManager.m_Instance.ShowHideWidget(false, knot.transform.position);
      return;
    }
  }

  public List<CameraPathKnot> GetKnotsOrphanedByKnotRemoval(CameraPathKnot knot) {
    List<CameraPathKnot> orphanedKnots = new List<CameraPathKnot>();
    // Position knots are the only type whose removal affects other knots.
    if (knot.KnotType != CameraPathKnot.Type.Position) {
      return orphanedKnots;
    }

    int positionIndex = PositionKnots.IndexOf(knot as CameraPathPositionKnot);

    // If we're removing an internal position knot, nothing will be orphaned.  
    if (positionIndex > 0 && positionIndex < PositionKnots.Count - 1) {
      return orphanedKnots;
    }

    // If the head is being removed (positionIndex == 0), any knots orphaned from its
    // removal will have PathT < 1.  If the tail is being removed, any knots orphaned will
    // have PathT > m_PositionKnots.Count - 2;
    int orphanTarget = (positionIndex == 0) ? 0 : positionIndex - 1;
    for (int i = 0; i < RotationKnots.Count; ++i) {
      if (RotationKnots[i].PathT.Floor() == orphanTarget) {
        orphanedKnots.Add(RotationKnots[i]);
      }
    }

    for (int i = 0; i < SpeedKnots.Count; ++i) {
      if (SpeedKnots[i].PathT.Floor() == orphanTarget) {
        orphanedKnots.Add(SpeedKnots[i]);
      }
    }

    for (int i = 0; i < FovKnots.Count; ++i) {
      if (FovKnots[i].PathT.Floor() == orphanTarget) {
        orphanedKnots.Add(FovKnots[i]);
      }
    }

    return orphanedKnots;
  }

  void RefreshPathAfterPositionRemoval(int removalIndex) {
    // Gather up all the segment lengths in a nicely digestible list.
    float[] prevSegmentLengths = new float[Segments.Count];
    for (int i = 0; i < prevSegmentLengths.Length; ++i) {
      prevSegmentLengths[i] = Segments[i].length;
    }

    // Delete the first segment and refresh all.
    if (Segments.Count > 0) {
      UnityEngine.Object.Destroy(Segments[Segments.Count - 1].renderer.gameObject);
      Segments.RemoveAt(Segments.Count - 1);
      for (int i = 0; i < Segments.Count; ++i) {
        RefreshSegment(i);
      }
    }

    // Make another nicely digestible list of the current segment lengths.
    float[] newSegmentLengths = new float[Segments.Count];
    for (int i = 0; i < newSegmentLengths.Length; ++i) {
      newSegmentLengths[i] = Segments[i].length;
    }

    // Update all rotation, speed, and fov knots.
    for (int i = 0; i < RotationKnots.Count; ++i) {
      RecomputeKnotPlacementAfterPositionRemoved(
          RotationKnots[i], removalIndex, prevSegmentLengths, newSegmentLengths);
      RefreshRotationKnot(i);
    }
    for (int i = 0; i < SpeedKnots.Count; ++i) {
      RecomputeKnotPlacementAfterPositionRemoved(
          SpeedKnots[i], removalIndex, prevSegmentLengths, newSegmentLengths);
      RefreshSpeedKnot(i);
    }
    for (int i = 0; i < FovKnots.Count; ++i) {
      RecomputeKnotPlacementAfterPositionRemoved(
          FovKnots[i], removalIndex, prevSegmentLengths, newSegmentLengths);
      RefreshFovKnot(i);
    }
  }

  public IEnumerator DelayRefresh() {
    yield return null;
    for (int i = 0; i < Segments.Count; ++i) {
      RefreshSegment(i);
    }
    for (int i = 0; i < PositionKnots.Count; ++i) {
      PositionKnots[i].RefreshVisuals();
    }
    for (int i = 0; i < SpeedKnots.Count; ++i) {
      SpeedKnots[i].RefreshVisuals();
    }
    for (int i = 0; i < FovKnots.Count; ++i) {
      FovKnots[i].RefreshVisuals();
    }
  }

  public void RefreshFromPathKnotMovement(int pathKnotIndex) {
    // Segments need to be updated before knots.
    RefreshSegment(pathKnotIndex - 1);
    RefreshSegment(pathKnotIndex);

    // Update all rotation knots on this segment.
    for (int i = 0; i < RotationKnots.Count; ++i) {
      int segment = RotationKnots[i].PathT.Floor();
      if (segment >= pathKnotIndex - 1 && segment <= pathKnotIndex) {
        RefreshRotationKnot(i);
      }
    }

    // Update all speed knots on this segment.
    for (int i = 0; i < SpeedKnots.Count; ++i) {
      int segment = SpeedKnots[i].PathT.Floor();
      if (segment >= pathKnotIndex - 1 && segment <= pathKnotIndex) {
        RefreshSpeedKnot(i);
      }
    }

    // Update all fov knots on this segment.
    for (int i = 0; i < FovKnots.Count; ++i) {
      int segment = FovKnots[i].PathT.Floor();
      if (segment >= pathKnotIndex - 1 && segment <= pathKnotIndex) {
        RefreshFovKnot(i);
      }
    }
  }

  void RefreshRotationKnot(int index) {
    CameraPathRotationKnot knot = RotationKnots[index];
    knot.SetPosition(GetPosition(knot.PathT));
    knot.DistanceAlongSegment = GetSegmentDistanceToT(knot.PathT);
  }

  void RefreshSpeedKnot(int index) {
    CameraPathSpeedKnot knot = SpeedKnots[index];
    knot.SetPosition(GetPosition(knot.PathT));
    knot.DistanceAlongSegment = GetSegmentDistanceToT(knot.PathT);
    knot.RefreshVisuals();
  }

  void RefreshFovKnot(int index) {
    CameraPathFovKnot knot = FovKnots[index];
    knot.SetPosition(GetPosition(knot.PathT));
    knot.DistanceAlongSegment = GetSegmentDistanceToT(knot.PathT);
    knot.RefreshVisuals();
  }

  public void RefreshSegment(int knot) {
    if (knot < 0 || knot >= PositionKnots.Count - 1) {
      return;
    }

    KnotSegment seg = Segments[knot];
    seg.length = 0.0f;
    float interval = 1.0f / (kNumSegmentPoints - 1);
    for (int i = 0; i < kNumSegmentPoints - 1; ++i) {
      float t = interval * i;
      Vector3 pos = GetPosition(new PathT(knot + t));
      seg.steps[i].start_RS = pos;
      seg.renderer.SetPosition(i, pos);
      if (i == 0) {
        seg.extents = new Bounds(pos, Vector3.zero);
      } else {
        seg.steps[i - 1].end_RS = pos;
        seg.length += seg.steps[i - 1].RefreshLength();
        seg.extents.Encapsulate(pos);
      }
    }

    Vector3 lastPos = PositionKnots[knot + 1].KnotXf.position;
    seg.steps[kNumSegmentPoints - 2].end_RS = lastPos;
    seg.length += seg.steps[kNumSegmentPoints - 2].RefreshLength();
    seg.renderer.SetPosition(kNumSegmentPoints - 1, lastPos);
    seg.extents.Encapsulate(lastPos);
    seg.extents.Expand(m_SegmentRadius * 2.0f);
  }

  public void RefreshSegmentVisuals(Vector3 segPos, KnotSegment seg,
      CameraPathTool.ExtendPathType extendType) {
    // If we don't have position knots, just keep the segment quiet.
    if (PositionKnots.Count <= 0) {
      for (int i = 0; i < kNumSegmentPoints; ++i) {
        seg.renderer.SetPosition(i, segPos);
      }
      return;
    }

    Debug.Assert(extendType != CameraPathTool.ExtendPathType.None);
    Vector3 pos0, pos1, tangent0, tangent1;
    switch (extendType) {
      case CameraPathTool.ExtendPathType.ExtendAtHead:
        pos0 = segPos;
        pos1 = PositionKnots[0].KnotXf.position;
        tangent0 = Vector3.zero;
        tangent1 = PositionKnots[0].ScaledTangent;
        break;
      case CameraPathTool.ExtendPathType.ExtendAtTail:
        pos0 = PositionKnots[PositionKnots.Count - 1].KnotXf.position;
        pos1 = segPos;
        tangent0 = PositionKnots[PositionKnots.Count - 1].ScaledTangent;
        tangent1 = Vector3.zero;
        break;
      case CameraPathTool.ExtendPathType.Loop:
      default:
        pos0 = PositionKnots[PositionKnots.Count - 1].KnotXf.position;
        pos1 = PositionKnots[0].KnotXf.position;
        tangent0 = PositionKnots[PositionKnots.Count - 1].ScaledTangent;
        tangent1 = PositionKnots[0].ScaledTangent;
        break;
    }

    // Solve for each midpoint.
    seg.renderer.SetPosition(0, pos0);
    for (int i = 1; i < kNumSegmentPoints - 1; ++i) {
      float t = (1.0f / (kNumSegmentPoints - 1)) * i;
      Vector3 pos = CalculateHermite(t, pos0, tangent0, pos1, tangent1);
      seg.renderer.SetPosition(i, pos);
    }
    seg.renderer.SetPosition(kNumSegmentPoints - 1, pos1);
  }

  public EndType IsPositionNearEnd(Vector3 pos) {
    float endRadSquared = m_EndRadius * m_EndRadius;
    if (PositionKnots.Count < 2) {
      return EndType.Tail;
    }
    float distSqToHead = (PositionKnots[0].KnotXf.position - pos).sqrMagnitude;
    float distSqToTail = (PositionKnots[PositionKnots.Count - 1].KnotXf.position - pos).sqrMagnitude;
    if (distSqToHead < endRadSquared && distSqToTail < endRadSquared) {
      return (distSqToHead < distSqToTail) ? EndType.Head : EndType.Tail;
    }
    if (distSqToHead < endRadSquared) {
      return EndType.Head;
    }
    if (distSqToTail < endRadSquared) {
      return EndType.Tail;
    }
    return EndType.None;
  }

  void RecomputeKnotPlacementAfterPositionAdded(CameraPathKnot knot, int addedKnotIndex,
      float[] prevSegmentLengths, float[] newSegmentLengths) {
    if (addedKnotIndex > Mathf.CeilToInt(knot.PathT.T)) {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3
      //    |__________________|_______[]________|_________+_________|______...
      //                              knot                (3)
      //                       knot.t is in [1, 2]
      //
      // Knot added beyond our segment.  Nothing to do here.
      return;
    }

    if (addedKnotIndex <= knot.PathT.Floor()) {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3
      //    |________+_________|_______[]________|___________________|______...
      //            (1)               knot
      //                       knot.t is in [1, 2]
      //
      // Knot added before our segment.  Scoot our knot t forward a full segment.
      knot.PathT = new PathT(knot.PathT.T + 1.0f);
      return;
    }

    // Knot added to our segment.  Or, (addedKnotIndex == Mathf.CeilToInt(knot.PathT))
    int segmentIndex = knot.PathT.Floor();
    Debug.Assert(segmentIndex < Segments.Count, "PathT was not Clamped");
    float prevKnotDistance = knot.DistanceAlongSegment;
    float prevRatio = prevKnotDistance / prevSegmentLengths[segmentIndex];

    float newSegmentOneDistance = newSegmentLengths[segmentIndex];
    float newSegmentTwoDistance = newSegmentLengths[segmentIndex + 1];
    float newKnotDistance = (newSegmentOneDistance + newSegmentTwoDistance) * prevRatio;

    if (newKnotDistance < newSegmentOneDistance) {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3
      //    |__________________|_______[]____+___|___________________|______...
      //                              knot  (2)
      //                       knot.t is in [1, 2]
      //
      // Knot added beyond us on our segment.
      knot.PathT = GetPathTFromDistance(segmentIndex, newKnotDistance);
      knot.DistanceAlongSegment = newKnotDistance;
    } else {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3
      //    |__________________|___+_____[]______|___________________|______...
      //                          (2)   knot
      //                       knot.t is in [1, 2]
      //
      // Knot added before us on our segment.
      newKnotDistance -= newSegmentOneDistance;
      knot.PathT = GetPathTFromDistance(segmentIndex + 1, newKnotDistance);
      knot.DistanceAlongSegment = newKnotDistance;
    }
  }

  void RecomputeKnotPlacementAfterPositionRemoved(CameraPathKnot knot, int removedKnotIndex,
      float[] prevSegmentLengths, float[] newSegmentLengths) {
    if (removedKnotIndex > Mathf.CeilToInt(knot.PathT.T)) {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3             Knot 4
      //    |__________________|_________________|_______[]_________|__________________X_____...
      //                                                knot
      //                                         knot.t is in [2, 3]
      //
      // Too far behind the removed position not, so no change to us.
      return;
    }

    if (removedKnotIndex < knot.PathT.Floor()) {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3             Knot 4
      //    |__________________X_________________|_______[]_________|__________________|_____...
      //                                                knot
      //                                         knot.t is in [2, 3]
      //
      // The removed position knot is a knot before our segment.  Step us back a full t.
      knot.PathT = new PathT(knot.PathT.T - 1.0f);
      return;
    }

    int segmentIndex = knot.PathT.Floor();
    Debug.Assert(segmentIndex < prevSegmentLengths.Length, "PathT was not Clamped");
    if (removedKnotIndex == Mathf.CeilToInt(knot.PathT.T)) {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3             Knot 4
      //    |__________________|_________________|_______[]_________X__________________|_____...
      //                                                knot
      //                                         knot.t is in [2, 3]
      //
      // The removed position knot is the end of our segment.  Our segment
      // won't change, but we'll need to update t.
      float prevKnotDistance = knot.DistanceAlongSegment;
      float prevTwoSegDistances = prevSegmentLengths[segmentIndex] +
          prevSegmentLengths[segmentIndex + 1];
      float prevRatio = prevKnotDistance / prevTwoSegDistances;

      float currentSegDistance = newSegmentLengths[segmentIndex];
      float newKnotDistance = currentSegDistance * prevRatio;

      knot.PathT = GetPathTFromDistance(segmentIndex, newKnotDistance);
      knot.DistanceAlongSegment = newKnotDistance;
    } else {
      //
      //  Knot 0             Knot 1            Knot 2             Knot 3             Knot 4
      //    |__________________|_________________X_______[]_________|__________________|_____...
      //                                                knot
      //                                         knot.t is in [2, 3]
      //
      // The removed position knot is our knot.  We need to update our position knot
      // and recalculte our t.
      float prevKnotDistance = knot.DistanceAlongSegment +
          prevSegmentLengths[segmentIndex - 1];
      float prevTwoSegDistances = prevSegmentLengths[segmentIndex - 1] +
          prevSegmentLengths[segmentIndex];
      float prevRatio = prevKnotDistance / prevTwoSegDistances;

      float currentSegDistance = newSegmentLengths[segmentIndex - 1];
      float newKnotDistance = currentSegDistance * prevRatio;

      knot.PathT = GetPathTFromDistance(segmentIndex - 1, newKnotDistance);
      knot.DistanceAlongSegment = newKnotDistance;
    }
  }

  public Vector3? ProjectPositionOntoPath(Vector3 pos) {
    float bestDistance = float.MaxValue;
    Vector3? validProjectedPos = null;

    // Run through all the segments and do gross checks inside the bounds.
    for (int i = 0; i < Segments.Count; ++i) {
      if (Segments[i].extents.Contains(pos)) {
        // Check all the steps in this segment.
        for (int j = 0; j < Segments[i].steps.Length; ++j) {
          // Project this point on to the line segment.
          KnotSegmentStep step = Segments[i].steps[j];
          Vector3 projected = Vector3.Project((pos - step.start_RS), (step.end_RS - step.start_RS));
          projected += step.start_RS;

          // Discard if we're too far away.
          if ((projected - pos).sqrMagnitude > m_SegmentRadius * m_SegmentRadius) {
            continue;
          }

          // Discard if we're too far off the end.
          float segmentT = CalculateTFromPointOnSegment(step.start_RS, step.end_RS, projected);
          if (segmentT < 0.0f || segmentT > 1.0f) {
            continue;
          }

          // We'll use distance to segment as our tie breaker.
          float distToSegment = (projected - pos).magnitude;
          if (distToSegment < bestDistance) {
            bestDistance = distToSegment;
            validProjectedPos = projected;
          }
        }
      }
    }

    return validProjectedPos;
  }

  /// This function estimates the current PathT value of a spline by checking against a
  /// quantized version of line segments.  The difference between projected position
  /// and computed t value position is returned as a Vector3 error out parameter.
  /// Parameters t and error have default values if this function fails.
  public bool ProjectPositionOnToPath(Vector3 pos, out PathT pathT, out Vector3 error) {
    m_GizmoBasePos = pos;

    error = Vector3.zero;
    pathT = new PathT(0.0f);
    float bestDistance = float.MaxValue;
    Vector3? bestProjectedPos = null;

    // Run through all the segments and do gross checks inside the bounds.
    for (int i = 0; i < Segments.Count; ++i) {
      if (Segments[i].extents.Contains(pos)) {
        // Check all the steps in this segment.
        for (int j = 0; j < Segments[i].steps.Length; ++j) {
          // Project this point on to the line segment.
          KnotSegmentStep step = Segments[i].steps[j];
          Vector3 projected = Vector3.Project((pos - step.start_RS), (step.end_RS - step.start_RS));
          projected += step.start_RS;

          // Discard if we're too far away.
          if ((projected - pos).sqrMagnitude > m_SegmentRadius * m_SegmentRadius) {
            continue;
          }

          // Discard if we're too far off the end.
          float segmentT = CalculateTFromPointOnSegment(step.start_RS, step.end_RS, projected);
          if (segmentT < 0.0f || segmentT > 1.0f) {
            continue;
          }

          // We'll use distance to segment as our tie breaker.
          float distToSegment = (projected - pos).magnitude;
          if (distToSegment < bestDistance) {
            bestDistance = distToSegment;
            bestProjectedPos = projected;

            float segmentStartT = m_SegmentStepInterval * j;
            float segmentEndT = segmentStartT + m_SegmentStepInterval;
            pathT = new PathT(i + Mathf.Lerp(segmentStartT, segmentEndT, segmentT));
            m_GizmoProjectedPosition = projected;
          }
        }
      }
    }

    if (bestProjectedPos.HasValue) {
      error = bestProjectedPos.Value - GetPosition(pathT);
      return true;
    }
    return false;
  }

  /// Calculates the parametric t value of a position on a line segment.
  /// posOnSegment is 3d position known to be on the line segment.
  float CalculateTFromPointOnSegment(
      Vector3 segmentStart, Vector3 segmentEnd, Vector3 posOnSegment) {
    Vector3 absSegment = segmentEnd - segmentStart;
    if (absSegment.x < 0.0f) { absSegment.x *= -1.0f; }
    if (absSegment.y < 0.0f) { absSegment.y *= -1.0f; }
    if (absSegment.z < 0.0f) { absSegment.z *= -1.0f; }

    // Degenerate case.
    if (absSegment.sqrMagnitude < 0.00001f) {
      return 0.0f;
    }

    // Inverse interpolate on the largest extent for the best precision.
    if (absSegment.x > absSegment.y && absSegment.x > absSegment.z) {
      return (posOnSegment.x - segmentStart.x) / (segmentEnd.x - segmentStart.x);
    } else if (absSegment.y > absSegment.x && absSegment.y > absSegment.z) {
      return (posOnSegment.y - segmentStart.y) / (segmentEnd.y - segmentStart.y);
    }
    return (posOnSegment.z - segmentStart.z) / (segmentEnd.z - segmentStart.z);
  }

  // PathT is set to 0 if within snapDistance distance from the head, or
  // Position.Count - 1 if within snapDistance distance from the tail.
  public PathT MaybeSnapPathTToEnd(PathT pathT, float snapDistance) {
    float distanceToT = GetSegmentDistanceToT(pathT);
    for (int i = 0; i < pathT.Floor(); ++i) {
      distanceToT += Segments[i].length;
    }

    if (distanceToT < snapDistance) {
      pathT.Zero();
    } else {
      // Get length of path.
      float pathLength = 0.0f;
      for (int i = 0; i < Segments.Count; ++i) {
        pathLength += Segments[i].length;
      }
      if (pathLength - distanceToT < snapDistance) {
        pathT = new PathT(PositionKnots.Count);
      }
    }

    pathT.Clamp(PositionKnots.Count);
    return pathT;
  }

  public float GetSegmentDistanceToT(PathT pathT) {
    // Will happen on newly minted paths.
    if (Segments == null || Segments.Count == 0) {
      return 0.0f;
    }

    pathT.Clamp(PositionKnots.Count);
    int knot = pathT.Floor();
    float segmentT = pathT.T % 1.0f;

    float distance = 0.0f;
    KnotSegmentStep[] steps = Segments[knot].steps;
    int targetStep = Mathf.FloorToInt(segmentT / m_SegmentStepInterval);

    for (int i = 0; i < targetStep; ++i) {
      KnotSegmentStep step = steps[i];
      distance += step.length_CS;
    }

    float targetStepStartT = targetStep * m_SegmentStepInterval;
    distance += ((segmentT - targetStepStartT) / m_SegmentStepInterval) * steps[targetStep].length_CS;

    return distance;
  }

  public float GetRatioToPathDistance(PathT pathT) {
    // Get length of entire path.
    float entirePath = 0.0f;
    for (int i = 0; i < Segments.Count; ++i) {
      entirePath += Segments[i].length;
    }
    if (entirePath == 0.0f) {
      return 1.0f;
    }

    // Get pathT length.
    float distanceToPathT = GetSegmentDistanceToT(pathT);
    int pathTKnot = pathT.Floor();
    for (int i = 0; i < pathTKnot; ++i) {
      distanceToPathT += Segments[i].length;
    }

    return distanceToPathT / entirePath;
  }

  PathT GetPathTFromDistance(int segment, float distance) {
    if (segment < 0 || segment >= Segments.Count) {
      throw new ArgumentException("Out of bounds segment: " + segment);
    }

    KnotSegmentStep[] steps = Segments[segment].steps;
    for (int i = 0; i < steps.Length; ++i) {
      KnotSegmentStep step = steps[i];
      if (distance > step.length_CS) {
        distance -= step.length_CS;
      } else {
        float segmentStartT = m_SegmentStepInterval * i;
        float segmentEndT = segmentStartT + m_SegmentStepInterval;
        float lerpT = distance / step.length_CS;
        return new PathT(segment + Mathf.Lerp(segmentStartT, segmentEndT, lerpT));
      }
    }

    // This will only be hit if distance > Segments[segment].length.
    return new PathT();
  }

  // 
  //  Knot 0             Knot 1             Knot 2     
  // pathT=0.0          pathT=1.0          pathT=2.0
  //    |__________________|__________________|_________...
  //
  // Negative t values and values beyond the path length are valid and will be clamped.
  public Vector3 GetPosition(PathT pathT) {
    int numKnots = PositionKnots.Count;
    Debug.Assert(numKnots >= 2);

    // Solve hermite.
    pathT.Clamp(numKnots);
    int baseKnot = Mathf.FloorToInt(pathT.T);
    int nextKnot = PathLoops ? (baseKnot + 1) % numKnots : Mathf.Min(baseKnot + 1, numKnots - 1);
    Vector3 pos = CalculateHermite(pathT.T % 1.0f,
      PositionKnots[baseKnot].KnotXf.position,
      PositionKnots[baseKnot].ScaledTangent,
      PositionKnots[nextKnot].KnotXf.position,
      PositionKnots[nextKnot].ScaledTangent);
    return pos;
  }

  float GetUnsignedDistanceFromTtoT(PathT t1, PathT t2) {
    int numKnots = PositionKnots.Count;
    t1.Clamp(numKnots);
    t2.Clamp(numKnots);

    int t1Segment = t1.Floor();
    int t2Segment = t2.Floor();
    float t1SegmentDistance = GetSegmentDistanceToT(t1);
    float t2SegmentDistance = GetSegmentDistanceToT(t2);
    // Same segment, just return the difference in the distances to the start of the segment.
    if (t1Segment == t2Segment && t1 <= t2) {
      return t2SegmentDistance - t1SegmentDistance;
    }

    // Our distance difference is t1 to the end of the segment, plus t2SegmentDistance, plus
    // the length of any segments between us.
    // Keep in mind that, if t2 is less than t1, we need to wrap around.
    float distance = Segments[t1Segment].length - t1SegmentDistance;
    if (t1Segment < t2Segment) {
      for (int i = t1Segment + 1; i < t2Segment; ++i) {
        distance += Segments[i].length;
      }
    } else {
      for (int i = t1Segment + 1; i < Segments.Count; ++i) {
        distance += Segments[i].length;
      }
      for (int i = 0; i < t2Segment; ++i) {
        distance += Segments[i].length;
      }
    }

    distance += t2SegmentDistance;
    return distance;
  }

  Vector4 CalculateHermite(
      float t, Vector4 pos0, Vector4 tangent0, Vector4 pos1, Vector4 tangent1) {
    // (2t^3 - 3t^2 + 1)p0 + (t^3 - 2t^2 + t)m0 + (-2t^3 + 3t^2)p1 + (t^3 - t^2)m1
    float t2 = t * t;
    float t3 = t2 * t;
    Vector4 h00 = (2.0f * t3 - 3.0f * t2 + 1.0f) * pos0;
    Vector4 h10 = (t3 - 2.0f * t2 + t) * tangent0;
    Vector4 h01 = (-2.0f * t3 + 3.0f * t2) * pos1;
    Vector4 h11 = (t3 - t2) * tangent1;
    return h00 + h10 + h01 + h11;
  }

  Vector3 CalculateHermite(
      float t, Vector3 pos0, Vector3 tangent0, Vector3 pos1, Vector3 tangent1) {
    // (2t^3 - 3t^2 + 1)p0 + (t^3 - 2t^2 + t)m0 + (-2t^3 + 3t^2)p1 + (t^3 - t^2)m1
    float t2 = t * t;
    float t3 = t2 * t;
    Vector3 h00 = (2.0f * t3 - 3.0f * t2 + 1.0f) * pos0;
    Vector3 h10 = (t3 - 2.0f * t2 + t) * tangent0;
    Vector3 h01 = (-2.0f * t3 + 3.0f * t2) * pos1;
    Vector3 h11 = (t3 - t2) * tangent1;
    return h00 + h10 + h01 + h11;
  }

  float CalculateHermite(float t, float pos0, float tangent0, float pos1, float tangent1) {
    // (2t^3 - 3t^2 + 1)p0 + (t^3 - 2t^2 + t)m0 + (-2t^3 + 3t^2)p1 + (t^3 - t^2)m1
    float t2 = t * t;
    float t3 = t2 * t;
    float h00 = (2.0f * t3 - 3.0f * t2 + 1.0f) * pos0;
    float h10 = (t3 - 2.0f * t2 + t) * tangent0;
    float h01 = (-2.0f * t3 + 3.0f * t2) * pos1;
    float h11 = (t3 - t2) * tangent1;
    return h00 + h10 + h01 + h11;
  }

  public Quaternion GetRotation(PathT pathT) {
    int numRotKnots = RotationKnots.Count;

    Quaternion rot = Coords.CanvasPose.rotation;
    if (numRotKnots == 0) {
      int numPathKnots = PositionKnots.Count;
      if (numPathKnots > 1) {
        Vector3 forward = PositionKnots[1].transform.position -
            PositionKnots[0].transform.position;
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
      }
      return rot;
    }
    if (numRotKnots == 1) {
      return RotationKnots[0].transform.rotation;
    }

    pathT.Clamp(PositionKnots.Count);

    // Gather up 4 values and 2 pathTs to solve a cubic hermite spline.
    Quaternion? v2 = null;
    Quaternion v0 = Quaternion.identity, v1 = Quaternion.identity, v3 = Quaternion.identity;
    PathT t1 = new PathT(), t2 = new PathT();
    for (int i = 0; i < numRotKnots; ++i) {
      CameraPathRotationKnot thisKnot = RotationKnots[i];
      if (pathT <= thisKnot.PathT) {
        v0 = RotationKnots[ClampIndex(i - 2, 0, numRotKnots - 1)].transform.rotation;
        v1 = RotationKnots[ClampIndex(i - 1, 0, numRotKnots - 1)].transform.rotation;
        v2 = RotationKnots[i].transform.rotation;
        v3 = RotationKnots[ClampIndex(i + 1, 0, numRotKnots - 1)].transform.rotation;

        if (i - 1 >= 0) {
          t1 = RotationKnots[i - 1].PathT;
        } else {
          t1 = PathLoops ? RotationKnots[i - 1 + numRotKnots].PathT : new PathT(0.0f);
        }
        t2 = RotationKnots[i].PathT;
        break;
      }
    }

    // If k2 hasn't been initialized, it means our pathT is beyond all rotation knots.
    if (v2 == null) {
      v0 = RotationKnots[numRotKnots - 2].transform.rotation;
      v1 = RotationKnots[numRotKnots - 1].transform.rotation;
      v2 = RotationKnots[ClampIndex(numRotKnots, 0, numRotKnots - 1)].transform.rotation;
      v3 = RotationKnots[ClampIndex(numRotKnots + 1, 0, numRotKnots - 1)].transform.rotation;

      t1 = RotationKnots[numRotKnots - 1].PathT;
      t2 = PathLoops ? RotationKnots[0].PathT : new PathT(PositionKnots.Count - 1);
    }

    float t1t2Distance = GetUnsignedDistanceFromTtoT(t1, t2);
    // Knots are on top of eachother, just return the value of the knot in front of us.
    if (t1t2Distance == 0.0f) {
      return v2.Value;
    }
    float t1pathTDistance = GetUnsignedDistanceFromTtoT(t1, pathT);

    // Special case code for dealing with quaternion polarity differences at the path loop point.
    // RefreshRotationKnotPolarities ensures that the rotation points on the path stay in the
    // same hemisphere (and thus don't contain unwanted 180 degree spins), but there's no
    // general solution for this problem with a looping path.
    // In that case, recondition just in time.
    // Note, an alternate solution is to decompose the quaternion into an up/right vector
    // pair, solve Hermite for each of those, normalize, and construct a quaterion.  This
    // works, but yielded different results in extreme cases and I prefer the results of
    // this method in those cases.
    if (PathLoops) {
      if (Quaternion.Dot(v0, v1) < 0.0f) { v1 = v1.Negated(); }
      if (Quaternion.Dot(v1, v2.Value) < 0.0f) { v2 = v2.Value.Negated(); }
      if (Quaternion.Dot(v2.Value, v3) < 0.0f) { v3 = v3.Negated(); }
    }

    Vector4 k0_vRot = new Vector4(v0.x, v0.y, v0.z, v0.w);
    Vector4 k1_vRot = new Vector4(v1.x, v1.y, v1.z, v1.w);
    Vector4 k2_vRot = new Vector4(v2.Value.x, v2.Value.y, v2.Value.z, v2.Value.w);
    Vector4 k3_vRot = new Vector4(v3.x, v3.y, v3.z, v3.w);

    float rotation_t = t1pathTDistance / t1t2Distance;
    Vector4 k1Tangent = 0.5f * (k2_vRot - k0_vRot);
    Vector4 k2Tangent = 0.5f * (k3_vRot - k1_vRot);

    Vector4 v4Rot = CalculateHermite(rotation_t, k1_vRot, k1Tangent, k2_vRot, k2Tangent);
    Quaternion qRot = new Quaternion(v4Rot.x, v4Rot.y, v4Rot.z, v4Rot.w);
    return qRot.normalized;
  }

  public float GetSpeed(PathT pathT) {
    int numSpeedKnots = SpeedKnots.Count;

    if (numSpeedKnots == 0) {
      return m_DefaultSpeed;
    }
    if (numSpeedKnots == 1) {
      return SpeedKnots[0].CameraSpeed;
    }

    pathT.Clamp(PositionKnots.Count);

    // Gather up 4 values and 2 pathTs to solve a cubic hermite spline.
    float? v2 = null;
    float v0 = 0.0f, v1 = 0.0f, v3 = 0.0f;
    PathT t1 = new PathT(), t2 = new PathT();
    for (int i = 0; i < numSpeedKnots; ++i) {
      CameraPathSpeedKnot thisKnot = SpeedKnots[i];
      if (pathT <= thisKnot.PathT) {
        v0 = SpeedKnots[ClampIndex(i - 2, 0, numSpeedKnots - 1)].CameraSpeed;
        v1 = SpeedKnots[ClampIndex(i - 1, 0, numSpeedKnots - 1)].CameraSpeed;
        v2 = SpeedKnots[i].CameraSpeed;
        v3 = SpeedKnots[ClampIndex(i + 1, 0, numSpeedKnots - 1)].CameraSpeed;

        if (i - 1 >= 0) {
          t1 = SpeedKnots[i - 1].PathT;
        } else {
          t1 = PathLoops ? SpeedKnots[i - 1 + numSpeedKnots].PathT : new PathT(0.0f);
        }
        t2 = SpeedKnots[i].PathT;
        break;
      }
    }

    // If k2 hasn't been initialized, it means our pathT is beyond all speed knots.
    if (v2 == null) {
      v0 = SpeedKnots[numSpeedKnots - 2].CameraSpeed;
      v1 = SpeedKnots[numSpeedKnots - 1].CameraSpeed;
      v2 = SpeedKnots[ClampIndex(numSpeedKnots, 0, numSpeedKnots - 1)].CameraSpeed;
      v3 = SpeedKnots[ClampIndex(numSpeedKnots + 1, 0, numSpeedKnots - 1)].CameraSpeed;

      t1 = SpeedKnots[numSpeedKnots - 1].PathT;
      t2 = PathLoops ? SpeedKnots[0].PathT : new PathT(PositionKnots.Count - 1);
    }

    float t1t2Distance = GetUnsignedDistanceFromTtoT(t1, t2);
    // Knots are on top of eachother, just return the value of the knot in front of us.
    if (t1t2Distance == 0.0f) {
      return v2.Value;
    }
    float t1pathTDistance = GetUnsignedDistanceFromTtoT(t1, pathT);

    float speed_t = t1pathTDistance / t1t2Distance;
    float k1Tangent = 0.5f * (v2.Value - v0);
    float k2Tangent = 0.5f * (v3 - v1);

    return CalculateHermite(speed_t, v1, k1Tangent, v2.Value, k2Tangent);
  }

  public float GetFov(PathT pathT) {
    int numFovKnots = FovKnots.Count;

    if (numFovKnots == 0) {
      return m_DefaultFov;
    }
    if (numFovKnots == 1) {
      return FovKnots[0].CameraFov;
    }

    pathT.Clamp(PositionKnots.Count);

    // Gather up 4 values and 2 pathTs to solve a cubic hermite spline.
    float? v2 = null;
    float v0 = 0.0f, v1 = 0.0f, v3 = 0.0f;
    PathT t1 = new PathT(), t2 = new PathT();
    for (int i = 0; i < numFovKnots; ++i) {
      CameraPathFovKnot thisKnot = FovKnots[i];
      if (pathT <= thisKnot.PathT) {
        v0 = FovKnots[ClampIndex(i - 2, 0, numFovKnots - 1)].CameraFov;
        v1 = FovKnots[ClampIndex(i - 1, 0, numFovKnots - 1)].CameraFov;
        v2 = FovKnots[i].CameraFov;
        v3 = FovKnots[ClampIndex(i + 1, 0, numFovKnots - 1)].CameraFov;

        if (i - 1 >= 0) {
          t1 = FovKnots[i - 1].PathT;
        } else {
          t1 = PathLoops ? FovKnots[i - 1 + numFovKnots].PathT : new PathT(0.0f);
        }
        t2 = FovKnots[i].PathT;
        break;
      }
    }

    // If k2 hasn't been initialized, it means our pathT is beyond all fov knots.
    if (v2 == null) {
      v0 = FovKnots[numFovKnots - 2].CameraFov;
      v1 = FovKnots[numFovKnots - 1].CameraFov;
      v2 = FovKnots[ClampIndex(numFovKnots, 0, numFovKnots - 1)].CameraFov;
      v3 = FovKnots[ClampIndex(numFovKnots + 1, 0, numFovKnots - 1)].CameraFov;

      t1 = FovKnots[numFovKnots - 1].PathT;
      t2 = PathLoops ? FovKnots[0].PathT : new PathT(PositionKnots.Count - 1);
    }

    float t1t2Distance = GetUnsignedDistanceFromTtoT(t1, t2);
    // Knots are on top of eachother, just return the value of the knot in front of us.
    if (t1t2Distance == 0.0f) {
      return v2.Value;
    }
    float t1pathTDistance = GetUnsignedDistanceFromTtoT(t1, pathT);

    float fov_t = t1pathTDistance / t1t2Distance;
    float k1Tangent = 0.5f * (v2.Value - v0);
    float k2Tangent = 0.5f * (v3 - v1);

    return CalculateHermite(fov_t, v1, k1Tangent, v2.Value, k2Tangent);
  }

  int ClampIndex(int index, int min, int max) {
    if (PathLoops) {
      if (index < 0) {
        index += max + 1;
      } else if (index > max) {
        index -= max + 1;
      }
      return index;
    }
    return Mathf.Clamp(index, min, max);
  }

  public bool MoveAlongPath(float movementAmount, PathT startT, out PathT endT) {
    bool rolled = false;
    int numPathKnots = PositionKnots.Count;

    // Early out if our path can't support movement.
    if (numPathKnots < 2) {
      endT = startT;
      return rolled;
    }

    startT.Clamp(numPathKnots);
    int walkingKnot = startT.Floor();
    float walkingT = startT.T % 1.0f;

    // Find the current segment step in our segment.
    int currentStep = Mathf.FloorToInt((startT.T - walkingKnot) / m_SegmentStepInterval);

    // Walk segments until we run out of movement.
    while (movementAmount > 0.0f) {
      KnotSegment walkingSegment = Segments[walkingKnot];
      KnotSegmentStep walkingStep = walkingSegment.steps[currentStep];
      float walkingStepStartT = currentStep * m_SegmentStepInterval;
      float walkingTNormalized = (walkingT - walkingStepStartT) / m_SegmentStepInterval;
      float distToEndOfWalkingStep = walkingStep.length_CS - (walkingTNormalized * walkingStep.length_CS);
      if (distToEndOfWalkingStep > movementAmount) {
        // We can't make it to the end of the step, so calculate our final t value.
        float tNormalizedMovement = movementAmount / walkingStep.length_CS;
        walkingT += tNormalizedMovement * m_SegmentStepInterval;
        break;
      } else {
        movementAmount -= distToEndOfWalkingStep;

        // Advance our step and see if we reached the end of the segment.
        ++currentStep;
        if (currentStep >= walkingSegment.steps.Length) {
          currentStep = 0;

          // Advance our segment and see if we reached the end of the path.
          ++walkingKnot;
          if (walkingKnot >= Segments.Count) {
            walkingKnot = 0;
            rolled = true;
          }
        }

        // Prime t value for next round.
        walkingT = currentStep * m_SegmentStepInterval;
      }
    }

    endT = new PathT(walkingKnot + walkingT);
    return rolled;
  }

  public CameraPathMetadata SerializeToCameraPathMetadata() {
    return new CameraPathMetadata() {
      PathKnots = PositionKnots.Select(k => k.AsSerializable()).ToArray(),
      RotationKnots = RotationKnots.Select(k => k.AsSerializable()).ToArray(),
      SpeedKnots = SpeedKnots.Select(k => k.AsSerializable()).ToArray(),
      FovKnots = FovKnots.Select(k => k.AsSerializable()).ToArray(),
    };
  }

  public void DrawGizmos() {
    Gizmos.color = Color.yellow;
    Gizmos.DrawSphere(m_GizmoBasePos, 0.025f);
    Gizmos.color = Color.red;
    Gizmos.DrawSphere(m_GizmoProjectedPosition, 0.04f);

    if (Segments != null) {
      for (int i = 0; i < Segments.Count; ++i) {
        GizmoDrawBox(Segments[i].extents);
        for (int j = 0; j < Segments[i].steps.Length; ++j) {
          GizmoDrawBox(Segments[i].steps[j].start_RS, Segments[i].steps[j].end_RS);
        }
      }
    }
  }

  void GizmoDrawBox(Bounds bounds) {
    Gizmos.color = Color.cyan;

    Vector3 e = bounds.extents;
    Vector3 a00 = bounds.center + new Vector3(-e.x, -e.y, -e.z);
    Vector3 a10 = bounds.center + new Vector3(e.x, -e.y, -e.z);
    Vector3 a01 = bounds.center + new Vector3(-e.x, e.y, -e.z);
    Vector3 a11 = bounds.center + new Vector3(e.x, e.y, -e.z);

    Vector3 b00 = bounds.center + new Vector3(-e.x, -e.y, e.z);
    Vector3 b10 = bounds.center + new Vector3(e.x, -e.y, e.z);
    Vector3 b01 = bounds.center + new Vector3(-e.x, e.y, e.z);
    Vector3 b11 = bounds.center + new Vector3(e.x, e.y, e.z);

    Gizmos.DrawLine(a00, a10);
    Gizmos.DrawLine(a10, a11);
    Gizmos.DrawLine(a11, a01);
    Gizmos.DrawLine(a01, a00);

    Gizmos.DrawLine(b00, b10);
    Gizmos.DrawLine(b10, b11);
    Gizmos.DrawLine(b11, b01);
    Gizmos.DrawLine(b01, b00);

    Gizmos.DrawLine(a00, b00);
    Gizmos.DrawLine(a10, b10);
    Gizmos.DrawLine(a01, b01);
    Gizmos.DrawLine(a11, b11);
  }

  void GizmoDrawBox(Vector3 posA, Vector3 posB) {
    Gizmos.color = Color.grey;
    Gizmos.DrawLine(posA, posB);

    Quaternion orient = Quaternion.LookRotation((posB - posA).normalized);
    Vector3 right = orient * Vector3.right;
    Vector3 up = orient * Vector3.up;

    Vector3 a00 = posA + (-right * m_SegmentRadius) + (-up * m_SegmentRadius);
    Vector3 a10 = posA + (right * m_SegmentRadius) + (-up * m_SegmentRadius);
    Vector3 a01 = posA + (-right * m_SegmentRadius) + (up * m_SegmentRadius);
    Vector3 a11 = posA + (right * m_SegmentRadius) + (up * m_SegmentRadius);

    Vector3 b00 = posB + (-right * m_SegmentRadius) + (-up * m_SegmentRadius);
    Vector3 b10 = posB + (right * m_SegmentRadius) + (-up * m_SegmentRadius);
    Vector3 b01 = posB + (-right * m_SegmentRadius) + (up * m_SegmentRadius);
    Vector3 b11 = posB + (right * m_SegmentRadius) + (up * m_SegmentRadius);

    Gizmos.color = Color.green;
    Gizmos.DrawLine(a00, a10);
    Gizmos.DrawLine(a10, a11);
    Gizmos.DrawLine(a11, a01);
    Gizmos.DrawLine(a01, a00);

    Gizmos.DrawLine(b00, b10);
    Gizmos.DrawLine(b10, b11);
    Gizmos.DrawLine(b11, b01);
    Gizmos.DrawLine(b01, b00);

    Gizmos.DrawLine(a00, b00);
    Gizmos.DrawLine(a10, b10);
    Gizmos.DrawLine(a01, b01);
    Gizmos.DrawLine(a11, b11);
  }
}

} // namespace TiltBrush