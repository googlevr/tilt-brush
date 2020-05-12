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

// These tools are used for monoscopic mode only
public class BaseSelectionTool : BaseTool {
  public Renderer m_Border;
  public Transform m_SelectionRing;
  public Vector2 m_SelectionRange;
  public Transform m_SelectionProgressBar;
  private Renderer m_SelectionProgressBarRenderer;
  private Vector3 m_SelectionProgressBarBaseScale;
  private Vector3 m_SelectionProgressBarBasePosition;
  public TextMesh m_SelectionText;
  public string m_DescriptionText;
  public Renderer m_ConfirmationTextRenderer;
  public Color m_ConfirmationTextColor;
  private float m_SelectionCurrentSize;

  private int m_SelectionObjectIndex;
  private int m_SelectionObjectChildIndex;
  private int m_SelectionVertIndex;
  private System.Diagnostics.Stopwatch m_SelectionStopwatch;

  public class SelectionObject {
    public GameObject m_Object;
    public bool m_ComplexObject;
  }
  protected List<SelectionObject> m_CurrentSelection;
  private Vector3 m_SelectionPositionPrev;
  private float m_SelectionRadiusPrev;
  protected bool m_SelectionInfoQueryComplete;
  protected bool m_SelectionInfoQueryWasComplete;
  protected bool m_SelectionInfoValid;
  protected Color m_SelectionColor;
  protected BrushDescriptor m_SelectionBrush;

  override public void Init() {
    base.Init();

    m_SelectionStopwatch = new System.Diagnostics.Stopwatch();
    m_CurrentSelection = new List<SelectionObject>();

    m_SelectionCurrentSize = (m_SelectionRange.x + m_SelectionRange.y) * 0.5f;
    m_SelectionCurrentSize = Mathf.Clamp(m_SelectionCurrentSize, m_SelectionRange.x, m_SelectionRange.y);
    m_SelectionRing.localScale = Vector3.one * m_SelectionCurrentSize;

    m_SelectionProgressBarRenderer = m_SelectionProgressBar.GetComponent<Renderer>();
    m_SelectionProgressBarBaseScale = m_SelectionProgressBar.localScale;
    m_SelectionProgressBarBasePosition = m_SelectionProgressBar.localPosition;

    m_SelectionText.text = m_DescriptionText;
    if (m_ConfirmationTextRenderer != null) {
      m_ConfirmationTextRenderer.material.color = m_ConfirmationTextColor;
    }
  }

  override public void EnableTool(bool bEnable) {
    base.EnableTool(bEnable);
    ResetSelection();
  }

  override public void UpdateTool() {
    base.UpdateTool();
    UpdateSelection();
    GetInfoFromSelection();

    if (m_ConfirmationTextRenderer != null) {
      m_ConfirmationTextRenderer.enabled = m_SelectionInfoValid;
    }
  }

  override public void UpdateSize(float fAdjustAmount) {
    float newT = Mathf.Clamp01(GetSize01() + fAdjustAmount);
    m_SelectionCurrentSize = Mathf.Lerp(m_SelectionRange.x, m_SelectionRange.y, newT);
    m_SelectionRing.localScale = Vector3.one * m_SelectionCurrentSize;
  }

  override public float GetSize01() {
    return Mathf.Clamp01(Mathf.InverseLerp(
        m_SelectionRange.x, m_SelectionRange.y, m_SelectionCurrentSize));
  }

  override public float GetSize() {
    return m_SelectionCurrentSize * 0.5f;
  }

  override public void SetColor(Color rColor) {
    m_Border.material.color = rColor;
    m_SelectionRing.GetComponent<Renderer>().material.color = rColor;
    m_SelectionText.GetComponent<Renderer>().material.color = rColor;
    m_SelectionProgressBarRenderer.material.color = rColor;
  }

  override public void SetToolProgress(float fProgress) {
    if (fProgress >= 1.0f) {
      m_SelectionProgressBarRenderer.enabled = false;
    } else {
      m_SelectionProgressBarRenderer.enabled = true;

      Vector3 vScale = m_SelectionProgressBarBaseScale;
      vScale.x *= fProgress;
      m_SelectionProgressBar.localScale = vScale;

      Vector3 vPosition = m_SelectionProgressBarBasePosition;
      vPosition.x -= (m_SelectionProgressBarBaseScale.x - vScale.x) * 0.5f;
      m_SelectionProgressBar.localPosition = vPosition;
    }
  }

  void ResetSelection() {
    m_SelectionObjectIndex = 0;
    m_SelectionObjectChildIndex = 0;
    m_SelectionVertIndex = 0;
    m_CurrentSelection.Clear();
    m_SelectionInfoQueryComplete = false;
    m_SelectionInfoQueryWasComplete = false;
    m_SelectionInfoValid = false;
    SetToolProgress(0.0f);
  }

  void UpdateSelection() {
    //0.1 ms
    float fTimeSlice = 1.0f / (10000.0f);
    int iTimeSliceInTicks = (int)(fTimeSlice * System.Diagnostics.Stopwatch.Frequency);
    Transform rCanvas = App.ActiveCanvas.transform;
    int iNumCanvasChildren = rCanvas.childCount;

    //reset selection if we've moved or adjusted our size
    float fSelectionRadius = GetSize();
    Vector3 vSelectionCenter = transform.position;
    Vector3 vSelectionCenterMovement = vSelectionCenter - m_SelectionPositionPrev;
    float fSelectionRadiusDiff = m_SelectionRadiusPrev - fSelectionRadius;
    if (vSelectionCenterMovement.sqrMagnitude > 0.0001f || Mathf.Abs(fSelectionRadiusDiff) > 0.001f) {
      ResetSelection();
      m_SelectionPositionPrev = vSelectionCenter;
      m_SelectionRadiusPrev = fSelectionRadius;
    }
    m_SelectionInfoQueryWasComplete = m_SelectionInfoQueryComplete;
    float fObjectProgressPercent = 0.0f;

    //DebugDrawBounds();

    //early out if there's nothing to look at
    if (iNumCanvasChildren > 0 && m_SelectionObjectIndex < iNumCanvasChildren) {
      m_SelectionStopwatch.Reset();
      m_SelectionStopwatch.Start();
      bool bDone = false;
      bool bComplexModel = false;

      //spin until we've taken up too much time
      while (!bDone) {
        //check child bounds
        Transform rChild = rCanvas.GetChild(m_SelectionObjectIndex);
        if (rChild.gameObject.activeSelf) {
          bComplexModel = false;
          MeshFilter rMeshFilter = rChild.GetComponent<MeshFilter>();
          if (rMeshFilter == null) {
            //look for a complex model
            ObjModelScript rModelScript = rChild.GetComponent<ObjModelScript>();
            if (rModelScript) {
              if (m_SelectionObjectChildIndex < rModelScript.m_MeshChildren.Length) {
                rMeshFilter = rModelScript.m_MeshChildren[m_SelectionObjectChildIndex];
                bComplexModel = true;
              }
            }
          }

          if (rMeshFilter) {
            Bounds rMeshBounds = rMeshFilter.mesh.bounds;
            rMeshBounds.Expand(fSelectionRadius);
            Vector3 vTransformedCenter = rChild.InverseTransformPoint(vSelectionCenter);

            if (rMeshBounds.Contains(vTransformedCenter)) {
              //bounds valid, check vert intersection with sphere
              int iMeshVertCount = rMeshFilter.mesh.vertexCount;
              Vector3[] aVerts = rMeshFilter.mesh.vertices;
              Vector3[] aNorms = rMeshFilter.mesh.normals;
              while (m_SelectionVertIndex < iMeshVertCount - 2) {
                //check to see if we're within the sphere radius to the plane of this triangle
                Vector3 vVert = aVerts[m_SelectionVertIndex];
                Vector3 vNorm = aNorms[m_SelectionVertIndex];
                float fDistToPlane = SignedDistancePlanePoint(ref vNorm, ref vVert, ref vTransformedCenter);
                if (Mathf.Abs(fDistToPlane) < fSelectionRadius) {
                  //we're within the radius to this triangle's plane, find the projected point
                  fDistToPlane *= -1.0f;
                  Vector3 vPlaneOffsetVector = vNorm * fDistToPlane;
                  Vector3 vPlaneIntersection = vTransformedCenter + vPlaneOffsetVector;

                  Vector3 vVert2 = aVerts[m_SelectionVertIndex + 1];
                  Vector3 vVert3 = aVerts[m_SelectionVertIndex + 2];

                  //walk the projected point toward the triangle center
                  Vector3 vTriCenter = (vVert + vVert2 + vVert3) * 0.33333f;
                  Vector3 vToTriCenter = vTriCenter - vPlaneIntersection;
                  float fWalkDistance = Mathf.Min(vToTriCenter.magnitude, fSelectionRadius);
                  vToTriCenter.Normalize();
                  vToTriCenter *= fWalkDistance;
                  vPlaneIntersection += vToTriCenter;

                  //see if this projected point is in the triangle
                  if (PointInTriangle(ref vPlaneIntersection, ref vVert, ref vVert2, ref vVert3)) {
                    //selected!
                    SelectionObject rNewSelectedObject = new SelectionObject();
                    rNewSelectedObject.m_Object = rChild.gameObject;
                    rNewSelectedObject.m_ComplexObject = bComplexModel;
                    m_CurrentSelection.Add(rNewSelectedObject);

                    //this will guarantee we'll move on from this model, complex or not
                    bComplexModel = false;
                    break;
                  }
                }

                //after each triangle, check our time
                m_SelectionVertIndex += 3;
                bDone = m_SelectionStopwatch.ElapsedTicks > iTimeSliceInTicks;
                if (bDone) {
                  fObjectProgressPercent = (float)m_SelectionVertIndex / (float)iMeshVertCount;
                  break;
                }
              }
            }
          }
        }

        //if we're not flagged as done, we just finished this object, so move on to the next
        if (!bDone) {
          //if we're looking at a complex model, look at the next piece of the model
          if (bComplexModel) {
            ++m_SelectionObjectChildIndex;
          } else {
            //move to the next object
            ++m_SelectionObjectIndex;
            m_SelectionObjectChildIndex = 0;
          }

          m_SelectionVertIndex = 0;
          if (m_SelectionObjectIndex >= iNumCanvasChildren) {
            //if we reached the end of the line, we're done
            break;
          }

          //might as well check per object
          bDone = m_SelectionStopwatch.ElapsedTicks > iTimeSliceInTicks;
        }
      }

      m_SelectionStopwatch.Stop();
    }

    //set progress
    float fProgressInterval = 1.0f;
    if (iNumCanvasChildren > 0) {
      fProgressInterval /= (float)iNumCanvasChildren;
    }
    float fCanvasProgress = fProgressInterval * (float)m_SelectionObjectIndex;
    float fBrushProgress = fObjectProgressPercent * fProgressInterval;
    SetToolProgress(fCanvasProgress + fBrushProgress + 0.001f);
  }

  void GetInfoFromSelection() {
    //don't start looking for a color until we're done with our selection
    Transform rCanvas = App.ActiveCanvas.transform;
    if (!m_SelectionInfoQueryComplete && (m_SelectionObjectIndex >= rCanvas.childCount)) {
      int iBestObject = -1;
      float fBestDistance = 999999.0f;
      Vector3 vSelectionCenter = transform.position;
      float fSelectionRadius = GetSize();
      for (int i = 0; i < m_CurrentSelection.Count; ++i) {
        //don't try to get info from complex objects
        SelectionObject rSelectedObject = m_CurrentSelection[i];
        if (rSelectedObject.m_ComplexObject) {
          continue;
        }

        MeshFilter rMeshFilter = rSelectedObject.m_Object.GetComponent<MeshFilter>();
        Vector3 vTransformedCenter = rSelectedObject.m_Object.transform.InverseTransformPoint(vSelectionCenter);

        //check each triangle for intersection
        int iMeshVertCount = rMeshFilter.mesh.vertexCount;
        Vector3[] aVerts = rMeshFilter.mesh.vertices;
        Vector3[] aNorms = rMeshFilter.mesh.normals;
        for (int j = 0; j < iMeshVertCount; j += 3) {
          //check to see if we're within the sphere radius to the plane of this triangle
          Vector3 vVert = aVerts[j];
          Vector3 vNorm = aNorms[j];
          float fDistToPlane = SignedDistancePlanePoint(ref vNorm, ref vVert, ref vTransformedCenter);
          float fAbsDistToPlane = Mathf.Abs(fDistToPlane);
          if (fAbsDistToPlane < fSelectionRadius && fAbsDistToPlane < fBestDistance) {
            //we're within the radius to this triangle's plane, find the projected point
            fDistToPlane *= -1.0f;
            Vector3 vPlaneOffsetVector = vNorm * fDistToPlane;
            Vector3 vPlaneIntersection = vTransformedCenter + vPlaneOffsetVector;

            Vector3 vVert2 = aVerts[i + 1];
            Vector3 vVert3 = aVerts[i + 2];

            //walk the projected point toward the triangle center
            Vector3 vTriCenter = (vVert + vVert2 + vVert3) * 0.33333f;
            Vector3 vToTriCenter = vTriCenter - vPlaneIntersection;
            float fWalkDistance = Mathf.Min(vToTriCenter.magnitude, fSelectionRadius);
            vToTriCenter.Normalize();
            vToTriCenter *= fWalkDistance;
            vPlaneIntersection += vToTriCenter;

            //see if this projected point is in the triangle
            if (PointInTriangle(ref vPlaneIntersection, ref vVert, ref vVert2, ref vVert3)) {
              //our projected point is in this triangle, store this distance as the best
              iBestObject = i;
              fBestDistance = fDistToPlane;
              break;
            }
          }
        }
      }

      //this can happen if we don't have a selection
      if (iBestObject != -1) {
        BaseBrushScript rBrushScript = m_CurrentSelection[iBestObject].m_Object.GetComponent<BaseBrushScript>();
        if (rBrushScript) {
          m_SelectionColor = rBrushScript.CurrentColor;
          m_SelectionBrush = rBrushScript.Descriptor;
          m_SelectionInfoValid = true;
        }
      } else {
        m_SelectionColor = Color.white;
        m_SelectionBrush = null;
      }

      m_SelectionInfoQueryComplete = true;
    }
  }

  float SignedDistancePlanePoint(ref Vector3 rPlaneNormal, ref Vector3 rPlanePoint, ref Vector3 rPoint) {
    return Vector3.Dot(rPlaneNormal, (rPoint - rPlanePoint));
  }

  void DebugDrawBounds() {
    float fSelectionRadius = GetSize();
    Transform rCanvas = App.ActiveCanvas.transform;
    for (int i = 0; i < rCanvas.childCount; ++i) {
      Transform rChild = rCanvas.GetChild(i);
      if (rChild.gameObject.activeSelf) {
        MeshFilter rMeshFilter = rChild.GetComponent<MeshFilter>();
        if (rMeshFilter) {
          Bounds rMeshBounds = rMeshFilter.mesh.bounds;
          rMeshBounds.Expand(fSelectionRadius);
          DebugDrawBox(rMeshBounds, rChild.position);
        }
      }
    }
  }

  void DebugDrawBox(Bounds rBounds, Vector3 vPos) {
    Vector3 vMinMinNeg = new Vector3(rBounds.min.x, rBounds.min.y, rBounds.min.z);
    Vector3 vMinMaxNeg = new Vector3(rBounds.min.x, rBounds.max.y, rBounds.min.z);
    Vector3 vMaxMinNeg = new Vector3(rBounds.max.x, rBounds.min.y, rBounds.min.z);
    Vector3 vMaxMaxNeg = new Vector3(rBounds.max.x, rBounds.max.y, rBounds.min.z);

    Vector3 vMinMinPos = new Vector3(rBounds.min.x, rBounds.min.y, rBounds.max.z);
    Vector3 vMinMaxPos = new Vector3(rBounds.min.x, rBounds.max.y, rBounds.max.z);
    Vector3 vMaxMinPos = new Vector3(rBounds.max.x, rBounds.min.y, rBounds.max.z);
    Vector3 vMaxMaxPos = new Vector3(rBounds.max.x, rBounds.max.y, rBounds.max.z);

    Debug.DrawLine(vPos + vMinMinNeg, vPos + vMinMaxNeg);
    Debug.DrawLine(vPos + vMaxMinNeg, vPos + vMaxMaxNeg);
    Debug.DrawLine(vPos + vMinMinNeg, vPos + vMaxMinNeg);
    Debug.DrawLine(vPos + vMinMaxNeg, vPos + vMaxMaxNeg);

    Debug.DrawLine(vPos + vMinMinPos, vPos + vMinMaxPos);
    Debug.DrawLine(vPos + vMaxMinPos, vPos + vMaxMaxPos);
    Debug.DrawLine(vPos + vMinMinPos, vPos + vMaxMinPos);
    Debug.DrawLine(vPos + vMinMaxPos, vPos + vMaxMaxPos);

    Debug.DrawLine(vPos + vMinMinNeg, vPos + vMinMinPos);
    Debug.DrawLine(vPos + vMinMaxNeg, vPos + vMinMaxPos);
    Debug.DrawLine(vPos + vMaxMinNeg, vPos + vMaxMinPos);
    Debug.DrawLine(vPos + vMaxMaxNeg, vPos + vMaxMaxPos);
  }
}
}  // namespace TiltBrush
