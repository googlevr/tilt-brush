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
public abstract class Media2dWidget : MediaWidget {
  [SerializeField] protected Texture2D m_NoImageTexture;
  [SerializeField] protected float m_StartScale;
  [SerializeField] protected float m_ColliderBloat;
  [SerializeField] protected float m_MeshScalar = 1.0f;
  [SerializeField] protected Transform m_Background;
  [SerializeField] protected Transform m_Missing;
  [SerializeField] protected TextMesh m_MissingText;
  [SerializeField] protected GameObject m_MissingQuestionMark;
  [SerializeField] protected float m_QuestionMarkScalar = 1.5f;
  [SerializeField] protected float m_MinTextSize;
  [SerializeField] protected int m_TiltMeterCost = 1;

  public Renderer m_ImageQuad;
  protected Renderer m_TextRenderer;
  protected Vector3 m_BaseScale;
  protected float m_TransitionScale;
  protected float m_BGThickness;
  protected float m_BGDist;
  protected const float m_MaxHomeSnapDistance = 0.001f;
  protected float m_HomeZFightingOffset;
  protected int m_NumVertsTrackedByWidgetManager;
  protected (string fileName, float aspectRatio)? m_MissingInfo;

  /// width / height
  public abstract float? AspectRatio { get; }

  public Texture ImageTexture {
    get { return m_ImageQuad.material.mainTexture; }
    set { m_ImageQuad.material.mainTexture = value; }
  }

  public TrTransform SaveTransform {
    get {
      TrTransform imageXf = TrTransform.FromLocalTransform(transform);
      imageXf.scale = GetSignedWidgetSize();
      return imageXf;
    }
  }

  protected override Vector3 HomeSnapOffset {
    get {
      Vector3 vSize = m_BoxCollider.size * 0.5f * App.Scene.Pose.scale;
      vSize.Scale(m_BoxCollider.transform.localScale - m_ContainerBloat);
      return vSize;
    }
  }

  public override float MaxAxisScale {
    get {
      return Mathf.Max(m_Mesh.localScale.x,
        Mathf.Max(m_Mesh.localScale.y, m_Mesh.localScale.z));
    }
  }

  public int NumVertsTrackedByWidgetManager {
    get { return m_NumVertsTrackedByWidgetManager; }
  }

  override public int GetTiltMeterCost() {
    return m_TiltMeterCost;
  }

  override protected void Awake() {
    base.Awake();
    this.transform.localScale = Vector3.one * Coords.CanvasPose.scale;
    m_Size = m_StartScale / Coords.CanvasPose.scale;
    m_ContainerBloat /= App.ActiveCanvas.Pose.scale;
    m_BaseScale = m_Mesh.localScale;
    m_TransitionScale = 0.0f;
    m_BGThickness = m_Background.localScale.z;
    m_BGDist = m_Background.localPosition.z;
    UpdateScale();
    m_HomeZFightingOffset = Random.value * m_MaxHomeSnapDistance + .0001f; // ensure non-zero

    // Set a new batchId on this image so it can be picked up in GPU intersections.
    m_BatchId = GpuIntersector.GetNextBatchId();
    WidgetManager.m_Instance.AddWidgetToBatchMap(this, m_BatchId);
  }

  override public void Activate(bool bActive) {
    base.Activate(bActive);
    if (!WidgetManager.m_Instance.WidgetsDormant) {
      if (m_TextRenderer != null) { m_TextRenderer.enabled = bActive; }
    }
  }

  protected override Transform GetOriginHomeXf() {
    Transform xf = base.GetOriginHomeXf();
    if (transform.forward == Vector3.down || transform.forward == Vector3.up) {
      xf.position += m_HomeZFightingOffset * Vector3.up;
    }
    return xf;
  }

  override protected void InitPin() {
    base.InitPin();
    // Images are flat, so don't bloat out our pin target position.
    m_Pin.SetPenetrationScalar(0f);
  }

  override protected void SetWidgetSizeInternal(float fScale) {
    base.SetWidgetSizeInternal(fScale);
    transform.localScale = Vector3.one;
  }

  override protected void UpdateScale() {
    //scale texture mesh
    Vector3 vScale = m_BaseScale;
    vScale *= m_Size * m_TransitionScale;
    // This is expected; UpdateScale can be called before the widget's been fully initialized.
    var aspectRatio = AspectRatio ?? 1;
    vScale.x *= aspectRatio;
    m_Mesh.localScale = vScale;
    m_Missing.localScale = new Vector3(vScale.x, vScale.y * aspectRatio, vScale.z);
    m_Background.localScale = new Vector3(
      m_Background.localScale.x,
      m_Background.localScale.y,
      m_BGThickness / m_Size);
    m_Background.localPosition = new Vector3(
      m_Background.localPosition.x,
      m_Background.localPosition.y,
      m_BGDist / m_Size);

    //set collider bounds
    Vector3 vColliderBounds = Vector3.zero;
    vColliderBounds.x = (vScale.x * m_MeshScalar) + m_ColliderBloat;
    vColliderBounds.y = (vScale.y * m_MeshScalar) + m_ColliderBloat;
    m_BoxCollider.transform.localScale = vColliderBounds / 2 + m_ContainerBloat;
    m_BoxCollider.size = Vector3.one * 2;

    // Update snap ghost.  This is different from models because the snap ghost is not a child
    // of the image (because the image does not have uniform scale).  Instead, we need to push
    // scale changes directly to the ghost.
    if (m_SnapGhost) {
      m_SnapGhost.localScale = vScale;
    }
  }

  override protected void OnShow() {
    base.OnShow();

    if (!m_LoadingFromSketch) {
      m_IntroAnimState = IntroAnimState.In;
      Debug.Assert(!IsMoving(), "Shouldn't have velocity!");
      ClearVelocities();
      m_IntroAnimValue = 0.0f;
      UpdateIntroAnim();
    } else {
      m_IntroAnimState = IntroAnimState.On;
    }
  }

  override protected void OnUpdate() {
    //if our transform changed, update the beams
    float fShowRatio = GetShowRatio();
    if (m_TransitionScale != fShowRatio) {
      m_TransitionScale = fShowRatio;
      UpdateScale();
    }
  }

  // Pass a reference image lookup string -- currently, a file name (not a path!)
  // TODO: this is only used by ImageWidget; move it and m_MissingInfo there?
  public void SetMissing(float aspectRatio, string fileName) {
    m_MissingText.gameObject.SetActive(true);
    m_TextRenderer = m_MissingText.GetComponent<Renderer>();
    m_MissingText.text = "Missing Image:\n" + fileName;
    m_MissingInfo = (fileName, aspectRatio);
    m_MissingQuestionMark.SetActive(true);
    m_MissingQuestionMark.transform.localScale =
      Mathf.Min(aspectRatio, 0.5f / aspectRatio) * m_QuestionMarkScalar * Vector3.one;
    if (aspectRatio < 1) {
      m_MissingText.transform.localScale = 2 * Vector3.one;
    } else {
      m_MissingText.transform.localScale = Mathf.Max(2 / aspectRatio, m_MinTextSize) * Vector3.one;
    }
    ImageTexture = m_NoImageTexture;
    UpdateScale();

    InitSnapGhost(m_ImageQuad.transform, transform);
  }

  public override float GetActivationScore(
    Vector3 vControllerPos, InputManager.ControllerName name) {
    float baseScore = base.GetActivationScore(vControllerPos, name);
    if (baseScore >= 0) {
      if (m_UngrabbableFromInside) {
        if (PointInCollider(ViewpointScript.Head.position) &&
            PointInCollider(InputManager.m_Instance.GetBrushControllerAttachPoint().position) &&
            PointInCollider(InputManager.m_Instance.GetWandControllerAttachPoint().position)) {
          return -1;
        }
      }
      return (1 - Mathf.Abs(m_Size) / GetWidgetSizeRange().y) * baseScore;
    }
    return baseScore;
  }
}
} // namespace TiltBrush
