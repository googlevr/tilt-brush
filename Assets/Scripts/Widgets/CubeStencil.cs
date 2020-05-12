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
using System;
using System.Linq;

namespace TiltBrush {
public class CubeStencil : StencilWidget {
  [SerializeField] private Transform[] m_Xxfs;
  [SerializeField] private Transform[] m_Yxfs;
  [SerializeField] private Transform[] m_Zxfs;
  [SerializeField] private bool m_HardEdges;
  [SerializeField] private bool m_StayInsideEdges;
  [SerializeField] private float m_PreviewClampPreference = 1.1f;
  [SerializeField] private float m_StickyFaceHalfWidthBloat = 0.075f;

  const float cornerSnapDist = 0.15f;
  const float maxUnitBrushSize = 0.725f;

  private Vector3 m_AspectRatio;
  private Transform[][] m_FaceXfs;
  private MeshFilter[][] m_FaceMeshFilters;
  private CubeFace m_StickyFace;
  private CubeFace m_LastQueriedFace;
  private int m_ClampedFace; // -1 = none, 0 = x, 1 = y, 2 = z

  private enum CubeFace {
    Up,
    Down,
    Left,
    Right,
    Forward,
    Back,
    None
  }

  public override Vector3 Extents {
    get {
      return m_Size * m_AspectRatio;
    }
    set {
      m_Size = 1;
      m_AspectRatio = value;
      UpdateScale();
    }
  }

  public override Vector3 CustomDimension {
    get { return m_AspectRatio; }
    set {
      m_AspectRatio = value;
      UpdateScale();
    }
  }

  protected override Vector3 HomeSnapOffset {
    get { return Extents * 0.5f * App.Scene.Pose.scale; }
  }

  protected override void Awake() {
    base.Awake();
    m_AspectRatio = Vector3.one; // [0..1]
    m_Type = StencilType.Cube;
    m_FaceXfs = new[] { m_Xxfs, m_Yxfs, m_Zxfs };
    m_FaceMeshFilters = new MeshFilter[m_FaceXfs.Length][];
    for (int i = 0; i < m_FaceXfs.Length; i++) {
      m_FaceMeshFilters[i] = m_FaceXfs[i].Select(xf => xf.GetComponent<MeshFilter>()).ToArray();
    }

    if (m_BoxCollider == null) {
      m_BoxCollider = m_Collider as BoxCollider;
      Debug.Assert(m_BoxCollider != null);
    }

    m_StickyFace = CubeFace.None;
    m_LastQueriedFace = CubeFace.None;
  }

  protected override Axis GetInferredManipulationAxis(
      Vector3 primaryHand, Vector3 secondaryHand, bool secondaryHandInside) {
    if (secondaryHandInside) {
      return Axis.Invalid;
    }
    Vector3 vHandsInObjectSpace = transform.InverseTransformDirection(primaryHand - secondaryHand);
    Vector3 vAbs = vHandsInObjectSpace.Abs();
    if (vAbs.x > vAbs.y && vAbs.x > vAbs.z) {
      return Axis.X;
    } else if (vAbs.y > vAbs.z) {
      return Axis.Y;
    } else {
      return Axis.Z;
    }
  }

  public override Axis GetScaleAxis(
      Vector3 handA, Vector3 handB,
      out Vector3 axisVec, out float extent) {
    // Unexpected -- normally we're only called during a 2-handed manipulation
    Debug.Assert(m_LockedManipulationAxis != null);
    Axis axis = m_LockedManipulationAxis ?? Axis.Invalid;

    float parentScale = TrTransform.FromTransform(transform.parent).scale;

    // Fill in axisVec, extent
    switch (axis) {
    case Axis.X: case Axis.Y: case Axis.Z:
      Vector3 axisVec_LS = Vector3.zero;
      axisVec_LS[(int)axis] = 1;
      axisVec = transform.TransformDirection(axisVec_LS);
      extent = parentScale * Extents[(int)axis];
      break;
    case Axis.Invalid:
      axisVec = default(Vector3);
      extent = default(float);
      break;
    default:
      throw new NotImplementedException(axis.ToString());
    }

    return axis;
  }

  CubeFace NormalToFaceMap(Vector3 norm) {
    Vector3 absNorm =
        new Vector3(Mathf.Abs(norm.x), Mathf.Abs(norm.y), Mathf.Abs(norm.z));
    if (absNorm.x > absNorm.y && absNorm.x > absNorm.z) {
      if (norm.x > 0.0f) {
        return CubeFace.Right;
      }
      return CubeFace.Left;
    } else if (absNorm.y > absNorm.z) {
      if (norm.y > 0.0f) {
        return CubeFace.Up;
      }
      return CubeFace.Down;
    }
    if (norm.z > 0.0f) {
      return CubeFace.Forward;
    }
    return CubeFace.Back;
  }

  public override void RecordAndApplyScaleToAxis(float deltaScale, Axis axis) {
    if (m_RecordMovements) {
      Vector3 newDimensions = CustomDimension;
      newDimensions[(int)axis] *= deltaScale;
      SketchMemoryScript.m_Instance.PerformAndRecordCommand(
        new MoveWidgetCommand(this, LocalTransform, newDimensions));
    } else {
      m_AspectRatio[(int)axis] *= deltaScale;
      UpdateScale();
    }
  }

  protected override void RegisterHighlightForSpecificAxis(Axis highlightAxis) {
    switch (highlightAxis) {
    case Axis.X:
    case Axis.Y:
    case Axis.Z: {
      int axis = (int)highlightAxis;
      for (int i = 0; i < m_FaceXfs[axis].Length; i++) {
        App.Instance.SelectionEffect.RegisterMesh(m_FaceMeshFilters[axis][i]);
      }
      break;
    }
    default:
      throw new InvalidOperationException(highlightAxis.ToString());
    }
  }

  override public void SetInUse(bool bInUse) {
    base.SetInUse(bInUse);
    if (!bInUse) {
      m_StickyFace = m_LastQueriedFace;
      m_LastQueriedFace = CubeFace.None;
    }
  }

  protected override void UpdateScale() {
    float maxAspect = m_AspectRatio.Max();
    m_AspectRatio /= maxAspect;
    m_Size *= maxAspect;
    transform.localScale = m_Size * m_AspectRatio;
    UpdateMaterialScale();
  }

  override protected void SpoofScaleForShowAnim(float showRatio) {
    transform.localScale = m_Size * showRatio * m_AspectRatio;
  }

  public override void FindClosestPointOnSurface(Vector3 pos,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    // Convert world space position to local space.
    Vector3 localPos = transform.InverseTransformPoint(pos);

    Vector3 closestPos;
    Vector3 normal;
    Vector3 halfWidth = (m_Collider as BoxCollider).size * 0.5f;
    if (m_HardEdges) {
      if (m_LastQueriedFace != CubeFace.None) {
        if (m_StayInsideEdges) {
          FindClosestPointWithStrokeInFace(localPos, halfWidth, m_LastQueriedFace,
            PointerManager.m_Instance.StraightEdgeModeEnabled, m_Size, m_AspectRatio,
            out closestPos, out normal, ref m_ClampedFace, m_PreviewClampPreference);
        } else {
          FindClosestPointOnBoxFace(localPos, halfWidth, m_LastQueriedFace,
            out closestPos, out normal);
        }
      } else {
        FindClosestPointOnBoxSurfaceHardEdges(
            localPos, halfWidth, out closestPos, out normal);
        m_LastQueriedFace = NormalToFaceMap(normal);
        if (m_StayInsideEdges) {
          FindClosestPointWithStrokeInFace(localPos, halfWidth, m_LastQueriedFace, true, m_Size,
            m_AspectRatio, out closestPos, out normal, ref m_ClampedFace, m_PreviewClampPreference);
        }
      }
    } else {
      FindClosestPointOnBoxSurface(
          localPos, halfWidth, out closestPos, out normal);
    }

    surfaceNorm = transform.TransformDirection(normal);
    surfacePos = transform.TransformPoint(closestPos);
  }

  public override float GetActivationScore(
      Vector3 vControllerPos, InputManager.ControllerName name) {
    float baseScore = base.GetActivationScore(vControllerPos, name);
    // don't try to scale if invalid; scaling by zero will make it look valid
    if (baseScore < 0) { return baseScore; }
    return baseScore * Mathf.Pow(1 - m_Size / m_MaxSize_CS, 2);
  }

  static void FindClosestPointWithStrokeInFace(Vector3 pos, Vector3 halfWidth, CubeFace face, bool preview,
      float size, Vector3 aspectRatio, out Vector3 surfacePos, out Vector3 surfaceNorm,
      ref int clampedSide, float previewClampPreference) {
    float strokeSize_RS = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;

    // Maximum absolute brush size that looks good on a 1x1 surface.
    Vector3 maxStrokeSize = aspectRatio * maxUnitBrushSize * size * Coords.CanvasPose.scale;
    maxStrokeSize.Scale(halfWidth);

    Vector3 reduceWidth = Vector3.zero;
    bool strokeTooLarge = false;
    switch (face) {
    case CubeFace.Up:
    case CubeFace.Down:
      reduceWidth.y = 0;
      reduceWidth.z = 1 / aspectRatio.z;
      reduceWidth.x = 1 / aspectRatio.x;
      strokeTooLarge = strokeSize_RS > maxStrokeSize.x || strokeSize_RS > maxStrokeSize.z;
      break;
    case CubeFace.Left:
    case CubeFace.Right:
      reduceWidth.x = 0;
      reduceWidth.y = 1 / aspectRatio.y;
      reduceWidth.z = 1 / aspectRatio.z;
      strokeTooLarge = strokeSize_RS > maxStrokeSize.y || strokeSize_RS > maxStrokeSize.z;
      break;
    case CubeFace.Forward:
    case CubeFace.Back:
      reduceWidth.z = 0;
      reduceWidth.x = 1 / aspectRatio.x;
      reduceWidth.y = 1 / aspectRatio.y;
      strokeTooLarge = strokeSize_RS > maxStrokeSize.x || strokeSize_RS > maxStrokeSize.y;
      break;
    }

    Vector3 strokeHalfSize_LS = (strokeSize_RS * 0.5f * reduceWidth) / (size * Coords.CanvasPose.scale);
    Vector3 insideBorder = halfWidth - strokeHalfSize_LS;
    if (!strokeTooLarge) {
      FindClosestPointOnBoxFace(pos, halfWidth, face, out surfacePos, out surfaceNorm);
      if (Mathf.Abs(surfacePos.x) > insideBorder.x &&
          Mathf.Abs(surfacePos.z) > insideBorder.z) {
        // snap to the closer one when there's a strong preference if in preview mode
        // stay on the clamped side if in drawing mode
        if (preview) {
          if (Mathf.Abs(surfacePos.x) - insideBorder.x >
              Mathf.Abs(surfacePos.z) - insideBorder.z * previewClampPreference) {
            clampedSide = 2;
          } else if (Mathf.Abs(surfacePos.z) - insideBorder.z >
              Mathf.Abs(surfacePos.x) - insideBorder.x * previewClampPreference) {
            clampedSide = 0;
          }
        }
        if (clampedSide == 2) {
          surfacePos.z = Mathf.Clamp(pos.z, -insideBorder.z, insideBorder.z);
        } else if (clampedSide == 0) {
          surfacePos.x = Mathf.Clamp(pos.x, -insideBorder.x, insideBorder.x);
        }
      } else if (Mathf.Abs(surfacePos.x) > insideBorder.x &&
                 Mathf.Abs(surfacePos.y) > insideBorder.y) {
        if (preview) {
          if (Mathf.Abs(surfacePos.x) - insideBorder.x >
              Mathf.Abs(surfacePos.y) - insideBorder.y * previewClampPreference) {
            clampedSide = 1;
          } else if (Mathf.Abs(surfacePos.y) - insideBorder.y >
              Mathf.Abs(surfacePos.x) - insideBorder.x * previewClampPreference) {
            clampedSide = 0;
          }
        }
        if (clampedSide == 1) {
          surfacePos.y = Mathf.Clamp(pos.y, -insideBorder.y, insideBorder.y);
        } else if (clampedSide == 0) {
          surfacePos.x = Mathf.Clamp(pos.x, -insideBorder.x, insideBorder.x);
        }
      } else if (Mathf.Abs(surfacePos.y) > insideBorder.y &&
                 Mathf.Abs(surfacePos.z) > insideBorder.z) {
        if (preview) {
          if (Mathf.Abs(surfacePos.y) - insideBorder.y >
              Mathf.Abs(surfacePos.z) - insideBorder.z * previewClampPreference) {
            clampedSide = 2;
          } else if (Mathf.Abs(surfacePos.z) - insideBorder.z >
              Mathf.Abs(surfacePos.y) - insideBorder.y * previewClampPreference) {
            clampedSide = 1;
          }
        }
        if (clampedSide == 2) {
          surfacePos.z = Mathf.Clamp(pos.z, -insideBorder.z, insideBorder.z);
        } else if (clampedSide == 1) {
          surfacePos.y = Mathf.Clamp(pos.y, -insideBorder.y, insideBorder.y);
        }
      } else if (Mathf.Abs(surfacePos.x) > insideBorder.x) {
        surfacePos.x = Mathf.Clamp(pos.x, -insideBorder.x, insideBorder.x);
        clampedSide = 0;
      } else if (Mathf.Abs(surfacePos.z) > insideBorder.z) {
        surfacePos.z = Mathf.Clamp(pos.z, -insideBorder.z, insideBorder.z);
        clampedSide = 2;
      } else if (Mathf.Abs(surfacePos.y) > insideBorder.y) {
        surfacePos.y = Mathf.Clamp(pos.y, -insideBorder.y, insideBorder.y);
        clampedSide = 1;
      } else {
        clampedSide = -1;
      }
    } else {
      FindClosestPointOnBoxFace(pos, halfWidth, face, out surfacePos, out surfaceNorm);
      clampedSide = -1;
    }
  }

  static void FindClosestPointOnBoxFace(Vector3 pos, Vector3 halfWidth, CubeFace face,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    surfacePos.x = Mathf.Clamp(pos.x, -halfWidth.x, halfWidth.x);
    surfacePos.y = Mathf.Clamp(pos.y, -halfWidth.y, halfWidth.y);
    surfacePos.z = Mathf.Clamp(pos.z, -halfWidth.z, halfWidth.z);

    switch(face) {
    case CubeFace.Up:
      surfacePos.y = halfWidth.y;
      surfaceNorm = Vector3.up;
      break;
    case CubeFace.Down:
      surfacePos.y = -halfWidth.y;
      surfaceNorm = Vector3.down;
      break;
    case CubeFace.Left:
      surfacePos.x = -halfWidth.x;
      surfaceNorm = Vector3.left;
      break;
    case CubeFace.Right:
      surfacePos.x = halfWidth.x;
      surfaceNorm = Vector3.right;
      break;
    case CubeFace.Forward:
      surfacePos.z = halfWidth.z;
      surfaceNorm = Vector3.forward;
      break;
    default:
    case CubeFace.Back:
      surfacePos.z = -halfWidth.z;
      surfaceNorm = Vector3.back;
      break;
    }
  }

  private void FindClosestPointOnBoxSurfaceHardEdges(Vector3 pos, Vector3 halfWidth,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    // If were have a sticky face assigned, bloat the other two axes.
    switch (m_StickyFace) {
    case CubeFace.Up:
    case CubeFace.Down:
      halfWidth.x += m_StickyFaceHalfWidthBloat;
      halfWidth.z += m_StickyFaceHalfWidthBloat;
      break;
    case CubeFace.Left:
    case CubeFace.Right:
      halfWidth.y += m_StickyFaceHalfWidthBloat;
      halfWidth.z += m_StickyFaceHalfWidthBloat;
      break;
    case CubeFace.Forward:
    case CubeFace.Back:
      halfWidth.x += m_StickyFaceHalfWidthBloat;
      halfWidth.y += m_StickyFaceHalfWidthBloat;
      break;
    }

    // Clamp to boundaries of cube.
    Vector3 absPos =
        new Vector3(Mathf.Abs(pos.x), Mathf.Abs(pos.y), Mathf.Abs(pos.z));
    surfacePos.x = Mathf.Clamp(pos.x, -halfWidth.x, halfWidth.x);
    surfacePos.y = Mathf.Clamp(pos.y, -halfWidth.y, halfWidth.y);
    surfacePos.z = Mathf.Clamp(pos.z, -halfWidth.z, halfWidth.z);
    Vector3 distance = new Vector3(
        halfWidth.x - absPos.x,
        halfWidth.y - absPos.y,
        halfWidth.z - absPos.z);

    // Fully-embedded; push out by the shortest route.
    if (distance.x < distance.y && distance.x < distance.z) {
      surfacePos.x = halfWidth.x * Mathf.Sign(pos.x);
      surfaceNorm = Vector3.right * Mathf.Sign(pos.x);
    } else if (distance.y < distance.z) {
      surfacePos.y = halfWidth.y * Mathf.Sign(pos.y);
      surfaceNorm = Vector3.up * Mathf.Sign(pos.y);
    } else {
      surfacePos.z = halfWidth.z * Mathf.Sign(pos.z);
      surfaceNorm = Vector3.forward * Mathf.Sign(pos.z);
    }
  }

  static public void FindClosestPointOnBoxSurface(Vector3 pos, Vector3 halfWidth,
      out Vector3 surfacePos, out Vector3 surfaceNorm) {
    // Clamp to boundaries of cube.
    Vector3 absPos =
        new Vector3(Mathf.Abs(pos.x), Mathf.Abs(pos.y), Mathf.Abs(pos.z));
    surfacePos.x = Mathf.Clamp(pos.x, -halfWidth.x, halfWidth.x);
    surfacePos.y = Mathf.Clamp(pos.y, -halfWidth.y, halfWidth.y);
    surfacePos.z = Mathf.Clamp(pos.z, -halfWidth.z, halfWidth.z);

    if (absPos.x <= halfWidth.x &&
        absPos.y <= halfWidth.y &&
        absPos.z <= halfWidth.z) {
      // Fully-embedded; push out by the shortest route.
      Vector3 distance = new Vector3(
          halfWidth.x - absPos.x,
          halfWidth.y - absPos.y,
          halfWidth.z - absPos.z);
      if (distance.x < distance.y && distance.x < distance.z) {
        surfacePos.x = halfWidth.x * Mathf.Sign(pos.x);
        surfaceNorm = Vector3.right * Mathf.Sign(pos.x);
      } else if (distance.y < distance.z) {
        surfacePos.y = halfWidth.y * Mathf.Sign(pos.y);
        surfaceNorm = Vector3.up * Mathf.Sign(pos.y);
      } else {
        surfacePos.z = halfWidth.z * Mathf.Sign(pos.z);
        surfaceNorm = Vector3.forward * Mathf.Sign(pos.z);
      }
    } else {
      // pos-closest should never be zero because of the <= checks above.
      surfaceNorm = (pos - surfacePos).normalized;
    }
  }

  protected override void InitiateSnapping() {
    base.InitiateSnapping();
    if (m_SnapGhost) {
      m_SnapGhost.transform.parent = transform.parent;
    }
  }

  protected override void FinishSnapping() {
    base.FinishSnapping();
    if (m_SnapGhost) {
      m_SnapGhost.transform.parent = transform;
    }
  }

  protected override TrTransform GetSnappedTransform(TrTransform xf_GS) {
    TrTransform outXf_GS = base.GetSnappedTransform(xf_GS);
    if (m_SnapGhost) {
      m_SnapGhost.localScale = transform.localScale;
    }
    return outXf_GS;
  }
}
} // namespace TiltBrush
