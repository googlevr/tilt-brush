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
using UnityEngine;

namespace TiltBrush {

public class StrokePlaybackByDistance : StrokePlayback {
  private ScenePlaybackByStrokeDistance m_parent;
  private Vector3 m_lastPosition;
  private bool m_playBackAtStrokeGranularity;

  public StrokePlaybackByDistance(ScenePlaybackByStrokeDistance parent) {
    m_parent = parent;
  }

  public void Init(Stroke stroke, PointerScript pointer, CanvasScript canvas) {
    BaseInit(stroke, pointer, canvas);
    m_lastPosition = m_stroke.m_ControlPoints[0].m_Pos;

    var desc = BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid);
    m_playBackAtStrokeGranularity = (m_parent.QuickLoad || desc.m_PlayBackAtStrokeGranularity);
  }

  protected override bool IsControlPointReady(PointerManager.ControlPoint controlPoint) {
    float meters_LS = (controlPoint.m_Pos - m_lastPosition).magnitude * App.UNITS_TO_METERS;
    float pointerToLocal = m_stroke.m_BrushScale;
    float meters_PS = meters_LS / pointerToLocal;
    m_parent.TryDecrementDistance(meters_PS);

    bool isReady = m_parent.HasMetersRemaining || m_playBackAtStrokeGranularity;
    if (isReady) {
      m_lastPosition = controlPoint.m_Pos;
    }
    return isReady;
  }
}

/// Playback using aggregate pointer-space stroke distance per frame.
/// (For discussion of pointer and stroke-local space, see BaseBrushScript.cs)
public class ScenePlaybackByStrokeDistance : IScenePlayback {
  private IEnumerator<Timeslice> m_drawer;

  // Strokes drawn out of SketchMemoryScript.m_MemoryList
  private int m_MemoryObjectsDrawn;
  private float m_metersRemaining;
  private bool m_OutOfMeters;
  private bool m_bQuickLoad;

  public bool QuickLoad {
    get { return m_bQuickLoad; }
  }

  private float MetersPerSecond {
    get {
      return m_bQuickLoad
        ? float.MaxValue
        : SketchMemoryScript.m_Instance.PlaybackMetersPerSecond;
    }
  }

  public bool HasMetersRemaining {
    get { return !m_OutOfMeters; }
  }

  /// Tries to consume some meters.
  /// If there are not enough left, doesn't consume any meters, and unsets HasMetersRemaining.
  public void TryDecrementDistance(float decrementAmount) {
    // If decrementing m_metersRemaining is going to put us at or below 0, don't
    // do it, so we retain the remainder for the next frame.
    if (m_metersRemaining <= decrementAmount) {
      m_OutOfMeters = true;
    } else {
      m_metersRemaining -= decrementAmount;
    }
  }

  public ScenePlaybackByStrokeDistance(IEnumerable<Stroke> strokes) {
    m_drawer = DrawWhileMetersRemaining(strokes, App.ActiveCanvas);
    m_metersRemaining = 0;
    m_OutOfMeters = false;
    m_MemoryObjectsDrawn = 0;
  }

  // Continue drawing stroke for this frame, returning true if more rendering is pending.
  public bool Update() {
    m_metersRemaining += MetersPerSecond * Time.deltaTime;
    m_OutOfMeters = false;

    // Unity appears to only clean up memory from mucking about with meshes on frame boundaries,
    // So Tilt Brush was using vast amounts of memory to do a quickload.
    // This causes Tilt Brush to only draw a certain distance before returning a frame.
    float maxDistancePerFrame = App.PlatformConfig.QuickLoadMaxDistancePerFrame;
    if (m_metersRemaining > maxDistancePerFrame) {
      m_metersRemaining = maxDistancePerFrame;
    }

    return m_drawer.MoveNext();
  }

  // Draws all strokes in mobjs, yielding when we're out of distance for the frame.
  private IEnumerator<Timeslice> DrawWhileMetersRemaining(
      IEnumerable<Stroke> strokes,
      CanvasScript targetCanvas) {
    StrokePlaybackByDistance playback = new StrokePlaybackByDistance(this);
    foreach (var stroke in strokes) {
      m_MemoryObjectsDrawn++;
      var pointer = PointerManager.m_Instance.GetPointer(InputManager.ControllerName.Brush);
      playback.Init(stroke, pointer, targetCanvas);
      while (! playback.IsDone()) {
        playback.Update();  // mutates m_metersRemaining and m_OutOfMeters
        if (m_OutOfMeters) {
          yield return null;
        }
      }
    }
  }

  public void QuickLoadRemaining() {
    m_bQuickLoad = true;
  }

  public void AddStroke(Stroke stroke) {}
  public void RemoveStroke(Stroke stroke) {}
  public int MaxPointerUnderrun { get { return 0; } }
  public int MemoryObjectsDrawn { get { return m_MemoryObjectsDrawn; } }
}

} // namespace TiltBrush