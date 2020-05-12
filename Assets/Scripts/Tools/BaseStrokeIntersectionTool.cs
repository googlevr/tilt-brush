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

namespace TiltBrush {

public class BaseStrokeIntersectionTool : BaseTool {
  [System.Serializable]
  protected enum IntersectionResetBehavior {
    None,               // Don't reset anything on intersection
    ResetPosition,      // Set position of intersection object, but don't reset detection
    ResetDetection,     // Start searching strokes from the beginning (also resets position)
  }

  [SerializeField] private IntersectionResetBehavior m_IntersectionResetBehavior =
      IntersectionResetBehavior.ResetDetection;
  [SerializeField] private float m_TimeSliceInMS = 1.0f;
  [SerializeField] protected float m_PointerForwardOffset = 0f;

  // Null when no request is in flight and non-null when active.
  // If non-null, it owns m_GpuFutureResultList.
  private GpuIntersector.FutureBatchResult m_GpuFutureResult;
  // Owned by m_GpuFutureResult (if one exists).
  // The only time this holds good data is if m_GpuFutureResult.IsReady.
  private List<GpuIntersector.BatchResult> m_GpuFutureResultList =
      new List<GpuIntersector.BatchResult>();

  // The results of a previous GpuFutureResult run, processed over several frames.
  private List<GpuIntersector.BatchResult> m_GpuOldResultList =
      new List<GpuIntersector.BatchResult>();
  // Indicates the range of m_GpuOldResultList values that have yet to be processed.
  private int m_GpuConsumedResults;

  // If not null, only intersections for strokes within this particular canvas will be handled.
  // Otherwise, if null, intersections with *any* canvas' strokes will be handled.
  protected CanvasScript m_CurrentCanvas;
  protected CanvasScript m_PreviousCanvas;

  protected System.Diagnostics.Stopwatch m_DetectionStopwatch;
  protected int m_DetectionObjectIndex;
  protected int m_DetectionVertIndex;

  protected int m_BatchPoolIndex;
  protected int m_BatchObjectIndex;
  protected int m_BatchVertGroupIndex;
  protected int m_BatchTriIndexIndex;

  protected float m_TimeSlice;
  protected int m_TimeSliceInTicks;

  protected bool m_TimesUp = false;
  protected bool m_ResetDetection = false;

  override public void Init() {
    base.Init();

    m_DetectionStopwatch = new System.Diagnostics.Stopwatch();
    m_TimeSlice = m_TimeSliceInMS / (1000.0f);
    m_TimeSliceInTicks = (int)(m_TimeSlice * System.Diagnostics.Stopwatch.Frequency);
  }

  protected void ResetDetection() {
    m_DetectionObjectIndex = 0;
    m_DetectionVertIndex = 0;

    m_BatchPoolIndex = 0;
    m_BatchObjectIndex = 0;
    m_BatchVertGroupIndex = 0;
    m_BatchTriIndexIndex = 0;

    ClearGpuFutureLists();
  }

  virtual protected void SnapIntersectionObjectToController() { }

  /// Easier interface than overriding HandleIntersectionWith*
  virtual protected void HandleIntersection(Stroke stroke) { }

  /// An alternative to overriding HandleIntersection(),
  /// if subclasses need finer-grained control
  /// Returns true if an action was carried out as a result of the intersection.
  ///
  /// The subset is guaranteed to be valid (ie, not part of a deleted batch)
  /// but you should still check if its state is correct (ie, if you're erasing,
  /// that it's not already erased)
  virtual protected bool HandleIntersectionWithBatchedStroke(BatchSubset rGroup) {
    HandleIntersection(rGroup.m_Stroke);
    return true;
  }

  virtual protected bool HandleIntersectionWithWidget(GrabWidget widget) {
    return true;
  }

  /// An alternative to overriding HandleIntersection(),
  /// if subclasses need finer-grained control
  /// Returns true if an action was carried out as a result of the intersection.
  virtual protected bool HandleIntersectionWithSolitaryObject(GameObject rGameObject) {
    var brush = rGameObject.GetComponent<BaseBrushScript>();
    if (brush != null) {
      HandleIntersection(brush.Stroke);
      return true;
    }
    return false;
  }

  /// This should be overriden by child classes that want a callback when an intersection occurs,
  /// max one per frame.
  virtual public void IntersectionHappenedThisFrame() { }

  /// This should be overriden by child classes that want custom control over what additional
  /// layers are checked by GPU intersection.
  virtual protected int AdditionalGpuIntersectionLayerMasks() {
    return 0;
  }

  // Helper for UpdateBatchedBrushDetection
  private void DoIntersectionResets() {
    switch (m_IntersectionResetBehavior) {
    case IntersectionResetBehavior.None:
      break;
    case IntersectionResetBehavior.ResetPosition:
      m_TimesUp = true;
      break;
    case IntersectionResetBehavior.ResetDetection:
      m_ResetDetection = true;
      m_TimesUp = true;
      break;
    }
  }

  protected void ClearGpuFutureLists() {
    m_GpuFutureResultList.Clear();
    m_GpuOldResultList.Clear();
    m_GpuFutureResult = null;
  }

  /// Side effects:
  /// - May cause HandleIntersectionWithXxx to be called
  ///
  /// Returns true if and only if any "intersection actions" were carried out (ie,
  /// if at least one HandleIntersectionWithXxx call returned true)
  ///
  private bool UpdateGpuIntersection(Vector3 vDetectionCenter_GS, float size_GS) {
    // Possible states of m_GpuFutureResult and m_GpuFutureResultlist:
    //
    //   m_GpuFutureResult = null       No outstanding GPU request. m_GpuFutureResultList
    //                                  is unused, might contain garbage, and is ready to pass
    //                                  to a new GpuFutureResult
    //   m_GpuFutureResult != null
    //      IsReady = false             Request is running; will fill in m_GpuFutureResultList
    //      IsReady = true              Request is done; m_GpuFutureResultList is filled in
    //
    // Possible states of m_GpuOldResultList and m_GpuConsumedResults:
    //
    //   m_GpuOldResultList             Always not-null, but may have been fully-processed
    //   m_GpuConsumedResults           Indices >= this have yet to be consumed
    //
    // m_GpuFutureResult may be pending for multiple frames.
    // m_GpuOldResultList may be processed over multiple frames.

    if (m_GpuFutureResult == null) {
      // Note that m_GpuFutureResultList will be cleared and populated at some future point
      // after this call.
      int intersectionLayer = (1 << m_CurrentCanvas.gameObject.layer) |
          AdditionalGpuIntersectionLayerMasks();

      // The new request will only be null when the intersector is disabled.
      // Given the logic in this function, this should be fine without any special handling.
      // TODO: use a pool of List<BatchResult> instead of being so stateful
      m_GpuFutureResult = App.Instance.GpuIntersector
          .RequestBatchIntersections(vDetectionCenter_GS,
                                size_GS,
                                m_GpuFutureResultList,
                                255,
                                intersectionLayer);
    } else if (m_GpuFutureResult.IsReady) {
      // We could go use GpuResultList, but as we're swapping the buffers here, it feels better
      // to be explicit about which buffers we're swapping.

      // TODO: use m_GpuFutureResult.GetResults() instead
      List<GpuIntersector.BatchResult> results = m_GpuFutureResultList;
      m_GpuFutureResultList = m_GpuOldResultList;
      // Note that this throws away any results that have yet to be consumed.
      m_GpuOldResultList = results;
      m_GpuConsumedResults = 0;

      // We could immediately submit another request, however we have likely already hit our budget
      // when there are intersections, so it should generally feel better to allow this to be a
      // three frame cycle.
      m_GpuFutureResult = null;
    }

    if (m_GpuConsumedResults < m_GpuOldResultList.Count) {
      int hitCount = 0;
      for (int i = m_GpuConsumedResults; i < m_GpuOldResultList.Count && !m_TimesUp; i++) {
        // Prefer to find widgets, although the results struct should never have both.
        if (m_GpuOldResultList[i].widget) {
          if (HandleIntersectionWithWidget(m_GpuOldResultList[i].widget)) {
            hitCount++;
          }
        } else {
          BatchSubset subset = m_GpuOldResultList[i].subset;
          if (subset.m_ParentBatch == null) {
            // The stroke was deleted between creating the result and processing the result. This
            // could happen due to the inherent latency in GPU intersection, although in practice,
            // this should be very rare. But this will also happen if the selection tool intersects
            // more than a single stroke in the same group. In this case, the following happens:
            //
            //   * HandleIntersectionWithBatchedStroke() is called once with one of the strokes in
            //     the group.
            //   * All the strokes in that group are moved to the selection canvas and thus, a
            //     different subset.
            //   * HandleIntersectionWithBatchedStroke() is called once with another stroke in the
            //     same group.
            continue;
          }
          if (HandleIntersectionWithBatchedStroke(subset)) {
            hitCount++;
          }
        }

        // Always process at least 1 hit. This number can be tuned to taste, but in initial
        // tests, it kept the deletion time under the frame budget while still feeling responsive.
        if (hitCount > 0) {
          m_TimesUp = m_DetectionStopwatch.ElapsedTicks > m_TimeSliceInTicks;
        }
      }
      m_GpuConsumedResults += hitCount;
      if (hitCount > 0) {
        return true;
      }
    }

    return false;
  }

  /// Detection Center should be in Global Space.
  protected void UpdateBatchedBrushDetection(Vector3 vDetectionCenter_GS) {
    // The CPU intersection code still needs to be updated to iterate over multiple canvases.
    //
    // TODO: Update CPU intersection checking to work on multiple canvases,
    //       then get rid of automatic defaulting to ActiveCanvas.
    //       Possibly let m_CurrentCanvas be null to represent a desire to intersect
    //       with all canvases.
    if (m_CurrentCanvas == null) {
      m_CurrentCanvas = App.ActiveCanvas;
    }

    // If we changed canvases, abandon any progress we made on checking for
    // intersections in the previous canvas.
    if (m_CurrentCanvas != m_PreviousCanvas) {
      ResetDetection();
      m_PreviousCanvas = m_CurrentCanvas;
    }

    TrTransform canvasPose = m_CurrentCanvas.Pose;
    Vector3 vDetectionCenter_CS = canvasPose.inverse * vDetectionCenter_GS;
    m_TimesUp = false;

    // Reset detection if we've moved or adjusted our size
    float fDetectionRadius_CS = GetSize() / canvasPose.scale;
    float fDetectionRadiusSq_CS = fDetectionRadius_CS * fDetectionRadius_CS;

    // Start the timer!
    m_DetectionStopwatch.Reset();
    m_DetectionStopwatch.Start();

    int iSanityCheck = 10000;
    bool bNothingChecked = true;

    if (App.Config.m_GpuIntersectionEnabled) {
      // Run GPU intersection if enabled; will update m_TimesUp.
      if (UpdateGpuIntersection(vDetectionCenter_GS, GetSize())) {
        IntersectionHappenedThisFrame();
        m_DetectionStopwatch.Stop();
        DoIntersectionResets();
        return;
      }
    }

    m_TimesUp = m_DetectionStopwatch.ElapsedTicks > m_TimeSliceInTicks;

    //check batch pools first
    int iNumBatchPools = m_CurrentCanvas.BatchManager.GetNumBatchPools();

    if (!App.Config.m_GpuIntersectionEnabled
        && iNumBatchPools > 0
        && m_BatchPoolIndex < iNumBatchPools) {
      bNothingChecked = false;
      m_ResetDetection = false;
      Plane rTestPlane = new Plane();
      BatchPool rPool = m_CurrentCanvas.BatchManager.GetBatchPool(m_BatchPoolIndex);

      //spin until we've taken up too much time
      while (!m_TimesUp) {
        --iSanityCheck;
        if (iSanityCheck == 0) {
          Batch tmpBatch = rPool.m_Batches[m_BatchObjectIndex];
          Debug.LogErrorFormat("Stroke while loop error.  NumPools({0}) BatchPoolIndex({1}) NumBatchStrokes({2}) BatchStrokeIndex({3}) NumStrokeGroups({4})",
            iNumBatchPools, m_BatchPoolIndex, rPool.m_Batches.Count, m_BatchObjectIndex, tmpBatch.m_Groups.Count);
        }

        Batch batch = rPool.m_Batches[m_BatchObjectIndex];
        if (m_BatchVertGroupIndex < batch.m_Groups.Count) {
          var subset = batch.m_Groups[m_BatchVertGroupIndex];
          Bounds rMeshBounds = subset.m_Bounds;
          rMeshBounds.Expand(2.0f * fDetectionRadius_CS);

          if (subset.m_Active && rMeshBounds.Contains(vDetectionCenter_CS)) {
            //bounds valid, check triangle intersections with sphere
            int nTriIndices = subset.m_nTriIndex;
            Vector3[] aVerts; int nVerts;
            int[] aTris; int nTris;
            batch.GetTriangles(out aVerts, out nVerts, out aTris, out nTris);
            while (m_BatchTriIndexIndex < nTriIndices - 2) {
              //check to see if we're within the brush size (plus some) radius to this triangle
              int iTriIndex = subset.m_iTriIndex + m_BatchTriIndexIndex;
              Vector3 v0 = aVerts[aTris[iTriIndex]];
              Vector3 v1 = aVerts[aTris[iTriIndex + 1]];
              Vector3 v2 = aVerts[aTris[iTriIndex + 2]];
              Vector3 vTriCenter = (v0 + v1 + v2) * 0.33333f;
              Vector3 vToTestCenter = vDetectionCenter_CS - vTriCenter;
              float fTestSphereRadius_CS = Vector3.Distance(v1, v2) + fDetectionRadius_CS;
              if (vToTestCenter.sqrMagnitude < fTestSphereRadius_CS * fTestSphereRadius_CS) {
                //check to see if we're within the sphere radius to the plane of this triangle
                Vector3 vNorm = Vector3.Cross(v1 - v0, v2 - v0).normalized;
                rTestPlane.SetNormalAndPosition(vNorm, v0);
                float fDistToPlane = rTestPlane.GetDistanceToPoint(vDetectionCenter_CS);
                if (Mathf.Abs(fDistToPlane) < fDetectionRadius_CS) {
                  //we're within the radius to this triangle's plane, find the point projected on to the plane
                  fDistToPlane *= -1.0f;
                  Vector3 vPlaneOffsetVector = vNorm * fDistToPlane;
                  Vector3 vPlaneIntersection = vDetectionCenter_CS - vPlaneOffsetVector;

                  //walk the projected point toward the triangle center to find the triangle test position
                  bool bIntersecting = false;
                  Vector3 vPointToTriCenter = vTriCenter - vDetectionCenter_CS;
                  if (vPointToTriCenter.sqrMagnitude < fDetectionRadiusSq_CS) {
                    //if the triangle center is within the detection distance, we're definitely intersecting
                    bIntersecting = true;
                  } //check against triangle segments
                  else if (SegmentSphereIntersection(v0, v1, vDetectionCenter_CS, fDetectionRadiusSq_CS)) {
                    bIntersecting = true;
                  } else if (SegmentSphereIntersection(v1, v2, vDetectionCenter_CS, fDetectionRadiusSq_CS)) {
                    bIntersecting = true;
                  } else if (SegmentSphereIntersection(v2, v0, vDetectionCenter_CS, fDetectionRadiusSq_CS)) {
                    bIntersecting = true;
                  } else {
                    //figure out how far we have left to move toward the tri-center
                    float fNormAngle = Mathf.Acos(Mathf.Abs(fDistToPlane) / fDetectionRadius_CS);
                    float fDistLeft = Mathf.Sin(fNormAngle) * fDetectionRadius_CS;

                    Vector3 vToTriCenter = vTriCenter - vPlaneIntersection;
                    vToTriCenter.Normalize();
                    vToTriCenter *= fDistLeft;
                    vPlaneIntersection += vToTriCenter;

                    //see if this projected point is in the triangle
                    if (PointInTriangle(ref vPlaneIntersection, ref v0, ref v1, ref v2)) {
                      bIntersecting = true;
                    }
                  }

                  if (bIntersecting) {
                    if (HandleIntersectionWithBatchedStroke(subset)) {
                      DoIntersectionResets();
                      break;
                    }
                  }
                }
              }

              //after each triangle, check our time
              m_BatchTriIndexIndex += 3;
              m_TimesUp = m_DetectionStopwatch.ElapsedTicks > m_TimeSliceInTicks;
              if (m_TimesUp) { break; }
            }
          }
        }

        //if we're not flagged as done, we just finished this group, so move on to the next group
        if (!m_TimesUp) {
          m_BatchTriIndexIndex = 0;
          ++m_BatchVertGroupIndex;

          //if we're done with groups, go to the next object
          if (m_BatchVertGroupIndex >= batch.m_Groups.Count) {
            m_BatchVertGroupIndex = 0;
            ++m_BatchObjectIndex;

            //aaaand if we're done with objects, go on to the next pool
            if (m_BatchObjectIndex >= rPool.m_Batches.Count) {
              m_BatchObjectIndex = 0;
              ++m_BatchPoolIndex;
              if (m_BatchPoolIndex >= iNumBatchPools) {
                //get out if we've traversed the last pool
                break;
              }
              rPool = m_CurrentCanvas.BatchManager.GetBatchPool(m_BatchPoolIndex);
            }
          }

          //we check again here in case the early checks fail
          m_TimesUp = m_DetectionStopwatch.ElapsedTicks > m_TimeSliceInTicks;
        }
      }
    }

    if (App.Config.m_GpuIntersectionEnabled && m_GpuFutureResult != null) {
      // We have an intersection test in flight, make sure we don't reset detection.
      // This ensures consistency between collision tests and avoids strobing of the result (e.g.
      // without this, one test will return "no result" and the next may return some result and this
      // oscillation will continue).
      bNothingChecked = false;
      m_ResetDetection = false;
      return;
    }

    // If our scene doesn't have anything in it, reset our detection.
    if (bNothingChecked) {
      m_ResetDetection = true;
    }

    m_DetectionStopwatch.Stop();
  }

  /// Detection Center should be in Global Space.
  protected void UpdateSolitaryBrushDetection(Vector3 vDetectionCenter_GS) {
    if (m_CurrentCanvas == null) {
      m_CurrentCanvas = App.ActiveCanvas;
    }
    TrTransform canvasPose = m_CurrentCanvas.Pose;
    Vector3 vDetectionCenter_CS = canvasPose.inverse * vDetectionCenter_GS;
    Transform rCanvas = m_CurrentCanvas.transform;
    int iNumCanvasChildren = rCanvas.childCount;

    m_TimesUp = false;

    //reset detection if we've moved or adjusted our size
    float fDetectionRadius = GetSize();
    float fDetectionRadiusSq = fDetectionRadius * fDetectionRadius;

    m_DetectionStopwatch.Reset();
    m_DetectionStopwatch.Start();

    //early out if there's nothing to look at
    if (iNumCanvasChildren > 0 && m_DetectionObjectIndex < iNumCanvasChildren) {
      Plane rTestPlane = new Plane();
      m_ResetDetection = false;

      //spin until we've taken up too much time
      while (!m_TimesUp) {
        //check child bounds
        Transform rChild = rCanvas.GetChild(m_DetectionObjectIndex);
        if (rChild.gameObject.activeSelf) {
          MeshFilter rMeshFilter = rChild.GetComponent<MeshFilter>();
          if (rMeshFilter) {
            Bounds rMeshBounds = rMeshFilter.mesh.bounds;
            rMeshBounds.Expand(fDetectionRadius);
            Vector3 vTransformedCenter = rChild.InverseTransformPoint(vDetectionCenter_CS);

            if (rMeshBounds.Contains(vTransformedCenter)) {
              //bounds valid, check triangle intersections with sphere
              int iMeshVertCount = rMeshFilter.mesh.vertexCount;
              Vector3[] aVerts = rMeshFilter.mesh.vertices;
              Vector3[] aNorms = rMeshFilter.mesh.normals;
              while (m_DetectionVertIndex < iMeshVertCount - 2) {
                //check to see if we're within the sphere radius to the plane of this triangle
                Vector3 vVert = aVerts[m_DetectionVertIndex];
                Vector3 vNorm = aNorms[m_DetectionVertIndex];
                rTestPlane.SetNormalAndPosition(vNorm, vVert);
                float fDistToPlane = rTestPlane.GetDistanceToPoint(vTransformedCenter);
                if (Mathf.Abs(fDistToPlane) < fDetectionRadius) {
                  //we're within the radius to this triangle's plane, find the point projected on to the plane
                  fDistToPlane *= -1.0f;
                  Vector3 vPlaneOffsetVector = vNorm * fDistToPlane;
                  Vector3 vPlaneIntersection = vTransformedCenter - vPlaneOffsetVector;

                  Vector3 vVert2 = aVerts[m_DetectionVertIndex + 1];
                  Vector3 vVert3 = aVerts[m_DetectionVertIndex + 2];

                  //walk the projected point toward the triangle center to find the triangle test position
                  Vector3 vTriCenter = (vVert + vVert2 + vVert3) * 0.33333f;

                  bool bIntersecting = false;
                  Vector3 vPointToTriCenter = vTriCenter - vTransformedCenter;
                  if (vPointToTriCenter.sqrMagnitude < fDetectionRadiusSq) {
                    //if the triangle center is within the detection distance, we're definitely intersecting
                    bIntersecting = true;
                  } else {
                    //figure out how far we have left to move toward the tri-center
                    float fNormAngle = Mathf.Acos(Mathf.Abs(fDistToPlane) / fDetectionRadius);
                    float fDistLeft = Mathf.Sin(fNormAngle) * fDetectionRadius;

                    Vector3 vToTriCenter = vTriCenter - vPlaneIntersection;
                    vToTriCenter.Normalize();
                    vToTriCenter *= fDistLeft;
                    vPlaneIntersection += vToTriCenter;

                    //see if this projected point is in the triangle
                    if (PointInTriangle(ref vPlaneIntersection, ref vVert, ref vVert2, ref vVert3)) {
                      bIntersecting = true;
                    }
                  }

                  if (bIntersecting) {
                    if (HandleIntersectionWithSolitaryObject(rChild.gameObject)) {
                      DoIntersectionResets();
                      break;
                    }
                  }
                }

                //after each triangle, check our time
                m_DetectionVertIndex += 3;
                m_TimesUp = m_DetectionStopwatch.ElapsedTicks > m_TimeSliceInTicks;
                if (m_TimesUp) {
                  break;
                }
              }
            }
          }
        }

        //if we're not flagged as done, we just finished this object, so move on to the next
        if (!m_TimesUp) {
          //move to the next object
          ++m_DetectionObjectIndex;

          m_DetectionVertIndex = 0;
          if (m_DetectionObjectIndex >= iNumCanvasChildren) {
            //if we reached the end of the line, we're done
            break;
          }

          //might as well check our clock per object
          m_TimesUp = m_DetectionStopwatch.ElapsedTicks > m_TimeSliceInTicks;
        }
      }
    }
    m_DetectionStopwatch.Stop();
  }

  protected void DebugDrawBounds() {
    CanvasScript canvas = App.ActiveCanvas;
    float fSelectionRadius = GetSize();
    if (App.Config.m_UseBatchedBrushes) {
      int iNumBatchPools = canvas.BatchManager.GetNumBatchPools();
      for (int i = 0; i < iNumBatchPools; ++i) {
        BatchPool rPool = canvas.BatchManager.GetBatchPool(i);
        for (int j = 0; j < rPool.m_Batches.Count; ++j) {
          for (int k = 0; k < rPool.m_Batches[j].m_Groups.Count; ++k) {
            Bounds rMeshBounds = rPool.m_Batches[j].m_Groups[k].m_Bounds;
            rMeshBounds.Expand(fSelectionRadius);
            Color rDrawColor = rPool.m_Batches[j].m_Groups[k].m_Active ? Color.white : Color.red;
            DebugDrawBox(rMeshBounds, Vector3.zero, rDrawColor);
          }
        }
      }
    } else {
      Transform rCanvas = canvas.transform;
      for (int i = 0; i < rCanvas.childCount; ++i) {
        Transform rChild = rCanvas.GetChild(i);
        if (rChild.gameObject.activeSelf) {
          MeshFilter rMeshFilter = rChild.GetComponent<MeshFilter>();
          if (rMeshFilter) {
            Bounds rMeshBounds = rMeshFilter.mesh.bounds;
            rMeshBounds.Expand(fSelectionRadius);
            DebugDrawBox(rMeshBounds, rChild.position, Color.white);
          }
        }
      }
    }
  }

  protected void DebugDrawBox(Bounds rBounds, Vector3 vPos, Color rColor) {
    Vector3 vMinMinNeg = new Vector3(rBounds.min.x, rBounds.min.y, rBounds.min.z);
    Vector3 vMinMaxNeg = new Vector3(rBounds.min.x, rBounds.max.y, rBounds.min.z);
    Vector3 vMaxMinNeg = new Vector3(rBounds.max.x, rBounds.min.y, rBounds.min.z);
    Vector3 vMaxMaxNeg = new Vector3(rBounds.max.x, rBounds.max.y, rBounds.min.z);

    Vector3 vMinMinPos = new Vector3(rBounds.min.x, rBounds.min.y, rBounds.max.z);
    Vector3 vMinMaxPos = new Vector3(rBounds.min.x, rBounds.max.y, rBounds.max.z);
    Vector3 vMaxMinPos = new Vector3(rBounds.max.x, rBounds.min.y, rBounds.max.z);
    Vector3 vMaxMaxPos = new Vector3(rBounds.max.x, rBounds.max.y, rBounds.max.z);

    Debug.DrawLine(vPos + vMinMinNeg, vPos + vMinMaxNeg, rColor);
    Debug.DrawLine(vPos + vMaxMinNeg, vPos + vMaxMaxNeg, rColor);
    Debug.DrawLine(vPos + vMinMinNeg, vPos + vMaxMinNeg, rColor);
    Debug.DrawLine(vPos + vMinMaxNeg, vPos + vMaxMaxNeg, rColor);

    Debug.DrawLine(vPos + vMinMinPos, vPos + vMinMaxPos, rColor);
    Debug.DrawLine(vPos + vMaxMinPos, vPos + vMaxMaxPos, rColor);
    Debug.DrawLine(vPos + vMinMinPos, vPos + vMaxMinPos, rColor);
    Debug.DrawLine(vPos + vMinMaxPos, vPos + vMaxMaxPos, rColor);

    Debug.DrawLine(vPos + vMinMinNeg, vPos + vMinMinPos, rColor);
    Debug.DrawLine(vPos + vMinMaxNeg, vPos + vMinMaxPos, rColor);
    Debug.DrawLine(vPos + vMaxMinNeg, vPos + vMaxMinPos, rColor);
    Debug.DrawLine(vPos + vMaxMaxNeg, vPos + vMaxMaxPos, rColor);
  }

}
}  // namespace TiltBrush
