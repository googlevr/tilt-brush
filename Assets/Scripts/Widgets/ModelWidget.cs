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
using System.Linq;

namespace TiltBrush {

public class ModelWidget : MediaWidget {
  // Do not change otherwise will break backward compatibility with 7.5 save format.
  private const float kInitialSizeMeters_RS = 0.25f;

  [SerializeField] private float m_MinContainerRatio; // [0, 1]
  [SerializeField] private float m_MaxBloat;

  private Model m_Model;
  private Transform m_ModelInstance;
  private ObjModelScript m_ObjModelScript;
  private float m_InitSize_CS;
  private float m_HideSize_CS;
  private bool m_PolyCallbackActive;

  private int m_NumVertsTrackedByWidgetManager;

  /// Returns null if there's no model.
  /// Note that it's an error to ask for a model's AssetId unless location type is PolyAssetId.
  public string AssetId => m_Model?.AssetId;

  // Returns all leaf meshes which are part of the model, not including those created for auxillary
  // purposes, such as ghosting or snapping.
  // Analagous to Model.GetMeshes().
  // Do not mutate the return value.
  public MeshFilter[] GetMeshes() {
    return m_ObjModelScript.m_MeshChildren;
  }

  public Model Model {
    get { return m_Model; }
    set {
      // Reduce usage count on old model.
      if (m_Model != null) {
        m_Model.m_UsageCount--;
      }
      m_Model = value;
      // Increment usage count on new model.
      if (m_Model != null) {
        m_Model.m_UsageCount++;
      }
      LoadModel();
    }
  }

  protected override Vector3 HomeSnapOffset {
    get {
      Vector3 box = m_BoxCollider.size - m_ContainerBloat * App.Scene.Pose.scale;
      return m_Size * box * 0.5f * App.Scene.Pose.scale;
    }
  }

  protected override Vector3 GetHomeSnapLocation(Quaternion snapOrient) {
    return base.GetHomeSnapLocation(snapOrient) -
      snapOrient * (App.Scene.Pose.scale * m_Size * m_Model.m_MeshBounds.center);
  }

  public override float MaxAxisScale {
    get {
      return Mathf.Max(transform.localScale.x * m_BoxCollider.size.x,
        Mathf.Max(transform.localScale.y * m_BoxCollider.size.y,
                  transform.localScale.z * m_BoxCollider.size.z));
    }
  }

  public Bounds WorldSpaceBounds {
    get { return m_BoxCollider.bounds; }
  }

  public int NumVertsTrackedByWidgetManager {
    get { return m_NumVertsTrackedByWidgetManager; }
  }

  override public int GetTiltMeterCost() {
    return (m_ObjModelScript != null) ? m_ObjModelScript.NumMeshes : 0;
  }

  public int GetNumVertsInModel() {
    return (m_ObjModelScript != null) ? m_ObjModelScript.GetNumVertsInMeshes() : 0;
  }

  override protected void Awake() {
    base.Awake();
    transform.parent = App.Instance.m_CanvasTransform;
    transform.localScale = Vector3.one;

    // Custom pin scalar for models.
    m_PinScalar = 0.5f;
  }

  override public void OnPreDestroy() {
    base.OnPreDestroy();

    if (m_PolyCallbackActive) {
      App.PolyAssetCatalog.CatalogChanged -= OnPacCatalogChanged;
      m_PolyCallbackActive = false;
    }
    // Set our model to null so its usage count is decremented.
    Model = null;
  }

  override public GrabWidget Clone() {
    ModelWidget clone = Instantiate(WidgetManager.m_Instance.ModelWidgetPrefab) as ModelWidget;
    clone.transform.position = transform.position;
    clone.transform.rotation = transform.rotation;
    clone.Model = this.Model;
    // We're obviously not loading from a sketch.  This is to prevent the intro animation.
    // TODO: Change variable name to something more explicit of what this flag does.
    clone.m_LoadingFromSketch = true;
    clone.Show(true, false);
    clone.transform.parent = transform.parent;
    clone.SetSignedWidgetSize(this.m_Size);
    HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
    TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);

    CanvasScript canvas = transform.parent.GetComponent<CanvasScript>();
    if (canvas != null) {
      var materials = clone.GetComponentsInChildren<Renderer>().SelectMany(x => x.materials);
      foreach (var material in materials) {
        foreach (string keyword in canvas.BatchManager.MaterialKeywords) {
          material.EnableKeyword(keyword);
        }
      }
    }

    if (!clone.Model.m_Valid) {
      App.PolyAssetCatalog.CatalogChanged += clone.OnPacCatalogChanged;
      clone.m_PolyCallbackActive = true;
    }
    clone.CloneInitialMaterials(this);
    clone.TrySetCanvasKeywordsFromObject(transform);
    return clone;
  }

  protected override void OnHideStart() {
    m_HideSize_CS = m_Size;
  }

  public void OnPacCatalogChanged() {
    Model model = App.PolyAssetCatalog.GetModel(AssetId);
    if (model != null && model.m_Valid) {
      Model = model;
      SetSignedWidgetSize(m_Size);

      // TODO: We may not want to do this, eventually.  Perhaps we continue to receive messages,
      // get our asset each time, and do a diff to see if we should reload it.
      App.PolyAssetCatalog.CatalogChanged -= OnPacCatalogChanged;
      m_PolyCallbackActive = false;
    }
  }

  public override string GetExportName() {
    return Model.GetExportName();
  }

  void LoadModel() {
    // Clean up existing model
    if (m_ModelInstance != null) {
      GameObject.Destroy(m_ModelInstance.gameObject);
    }

    // Early out if we don't have a model to clone.
    // This can happen if model loading is deferred.
    if (m_Model == null || m_Model.m_ModelParent == null) {
      return;
    }

    m_ModelInstance = Instantiate(m_Model.m_ModelParent);
    m_ModelInstance.gameObject.SetActive(true);
    m_ModelInstance.parent = this.transform;

    Coords.AsLocal[m_ModelInstance] = TrTransform.identity;
    float maxExtent = 2 * Mathf.Max(m_Model.m_MeshBounds.extents.x,
        Mathf.Max(m_Model.m_MeshBounds.extents.y, m_Model.m_MeshBounds.extents.z));
    float size;
    if (maxExtent == 0.0f) {
      // If we created a widget with a model that doesn't have geo, we won't have calculated a
      // bounds worth much.  In that case, give us a default size.
      size = 1.0f;
    } else {
      size = kInitialSizeMeters_RS * App.METERS_TO_UNITS / maxExtent;
    }

    m_InitSize_CS = size / Coords.CanvasPose.scale;

    // Models are created in the main canvas.  Cache model layer in case it's overridden later.
    HierarchyUtils.RecursivelySetLayer(transform, App.Scene.MainCanvas.gameObject.layer);
    m_BackupLayer = m_ModelInstance.gameObject.layer;

    // Set a new batchId on this model so it can be picked up in GPU intersections.
    m_BatchId = GpuIntersector.GetNextBatchId();
    HierarchyUtils.RecursivelySetMaterialBatchID(m_ModelInstance, m_BatchId);
    WidgetManager.m_Instance.AddWidgetToBatchMap(this, m_BatchId);

    Vector3 ratios = GetBoundsRatios(m_Model.m_MeshBounds);
    m_ContainerBloat.x = Mathf.Max(0, m_MinContainerRatio - ratios.x);
    m_ContainerBloat.y = Mathf.Max(0, m_MinContainerRatio - ratios.y);
    m_ContainerBloat.z = Mathf.Max(0, m_MinContainerRatio - ratios.z);
    m_ContainerBloat /= m_MinContainerRatio; // Normalize for the min ratio.
    m_ContainerBloat *= m_MaxBloat / App.Scene.Pose.scale; // Apply bloat to appropriate axes.

    m_BoxCollider.size = m_Model.m_MeshBounds.size + m_ContainerBloat;
    m_BoxCollider.transform.localPosition = m_Model.m_MeshBounds.center;

    InitSnapGhost(m_Model.m_ModelParent, m_ModelInstance);

    // Remove previous model vertex recording.
    WidgetManager.m_Instance.AdjustModelVertCount(-m_NumVertsTrackedByWidgetManager);
    m_NumVertsTrackedByWidgetManager = 0;

    m_ObjModelScript = GetComponentInChildren<ObjModelScript>();
    m_ObjModelScript.Init();
    if (m_ObjModelScript.NumMeshes == 0) {
      OutputWindowScript.Error("No usable geometry in model");
    } else {
      m_NumVertsTrackedByWidgetManager = m_ObjModelScript.GetNumVertsInMeshes();
      WidgetManager.m_Instance.AdjustModelVertCount(m_NumVertsTrackedByWidgetManager);
    }

    if (m_Model.IsCached()) {
      m_Model.RefreshCache();
    }
  }

  public override float GetActivationScore(Vector3 vControllerPos, InputManager.ControllerName name) {
    Vector3 vInvTransformedPos = m_BoxCollider.transform.InverseTransformPoint(vControllerPos);
    Vector3 vSize = m_BoxCollider.size * 0.5f;
    float xDiff = vSize.x - Mathf.Abs(vInvTransformedPos.x);
    float yDiff = vSize.y - Mathf.Abs(vInvTransformedPos.y);
    float zDiff = vSize.z - Mathf.Abs(vInvTransformedPos.z);
    if (xDiff > 0.0f && yDiff > 0.0f && zDiff > 0.0f) {
      float minSize = Mathf.Abs(m_Size) *
        Mathf.Min(m_BoxCollider.size.x, Mathf.Min(m_BoxCollider.size.y, m_BoxCollider.size.z));
      return (xDiff / vSize.x + yDiff / vSize.y + zDiff / vSize.z) / 3 / (minSize + 1);
    }
    return -1.0f;
  }

  private static Vector3 GetBoundsRatios(Bounds bounds) {
    float maxExtent = 0.001f; // epsilon to avoid division by zero
    maxExtent = Mathf.Max(maxExtent, bounds.extents.x);
    maxExtent = Mathf.Max(maxExtent, bounds.extents.y);
    maxExtent = Mathf.Max(maxExtent, bounds.extents.z);

    return new Vector3(
      bounds.extents.x / maxExtent,
      bounds.extents.y / maxExtent,
      bounds.extents.z / maxExtent);
  }

  protected override void OnShow() {
    base.OnShow();

    if (m_Model != null && m_Model.m_Valid) {
      SetSignedWidgetSize(0.0f);
    }

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

  protected override void OnUpdate() {
    // During transitions, scale up and down.
    if (m_CurrentState == State.Hiding) {
      SetWidgetSizeAboutCenterOfMass(m_HideSize_CS * GetShowRatio());
    }
  }

  protected override void UpdateIntroAnim() {
    base.UpdateIntroAnim();
    if (!m_LoadingFromSketch) {
      SetWidgetSizeAboutCenterOfMass(m_InitSize_CS * m_IntroAnimValue);
    }
  }

  protected override void UpdateScale() {
    transform.localScale = Vector3.one * m_Size;
    if (m_Model != null && m_Model.m_Valid) {
      m_BoxCollider.size = m_Model.m_MeshBounds.size + m_ContainerBloat;
    }
  }

  override protected void InitPin() {
    base.InitPin();
    // Move the pin closer to the center since the bounds around the mesh might not be very tight.
    m_Pin.SetPenetrationScalar(.25f);
  }

  public override void RegisterHighlight() {
#if !UNITY_ANDROID
    if (m_ObjModelScript != null) {
      m_ObjModelScript.RegisterHighlight();
      return;
    }
#endif
    base.RegisterHighlight();
  }

  protected override void UnregisterHighlight() {
#if !UNITY_ANDROID
    if (m_ObjModelScript != null) {
      m_ObjModelScript.UnregisterHighlight();
      return;
    }
#endif
    base.UnregisterHighlight();
  }

  public TrTransform GetSaveTransform() {
    var xf = TrTransform.FromLocalTransform(transform);
    xf.scale = GetSignedWidgetSize();
    return xf;
  }

  public override Vector2 GetWidgetSizeRange() {
    // m_Model can be null in the event we're pulling the model from Poly.
    if (m_Model == null || !m_Model.m_Valid) {
      // Don't enforce a size range if we don't know the extents yet.
      // It will be contrained when the model loads in and the bounds are known.
      return new Vector2(float.MinValue, float.MaxValue);
    }
    float maxExtent = 2 * Mathf.Max(m_Model.m_MeshBounds.extents.x,
        Mathf.Max(m_Model.m_MeshBounds.extents.y, m_Model.m_MeshBounds.extents.z));
    // If we created a widget with a model that doesn't have geo, we won't have calculated a
    // bounds with great data.  Protect against divide by zero.
    if (maxExtent == 0.0f) {
      maxExtent = 1.0f;
    }
    return new Vector2(m_MinSize_CS / maxExtent, m_MaxSize_CS / maxExtent);
  }

  /// This method is for use when loading widgets that use the pre-M13 file format,
  /// which is normalized with respect to min and max size (ie: the transform is not "raw")
  public void SetWidgetSizeNonRaw(float fScale) {
    Vector3 extents = m_Model.m_MeshBounds.extents;
    float maxExtent = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
    // If we created a widget with a model that doesn't have geo, we won't have calculated a
    // bounds with great data.  Protect against divide by zero.
    if (maxExtent == 0.0f) {
      maxExtent = 1.0f;
    }
    float sizeRatio = kInitialSizeMeters_RS / 2 / maxExtent;
    SetSignedWidgetSize(fScale * sizeRatio * 10);
  }


  /// Updates the scale of the object, but with the center-of-scale
  /// being the center of mass's origin, as opposed to the widget's origin.
  /// Postconditions:
  /// - GetWidgetSize() == size
  /// - CenterOfMassPose_LS is unchanged (modulo precision issues)
  protected void SetWidgetSizeAboutCenterOfMass(float size) {
    if (m_Size == size) { return; }

    // Use WithUnitScale because we want only the pos/rot difference
    // Find delta such that delta * new = old
    var oldCm_LS = WithUnitScale(CenterOfMassPose_LS);
    m_Size = size;
    UpdateScale();
    var newCm_LS = WithUnitScale(CenterOfMassPose_LS);
    var delta_LS = oldCm_LS * newCm_LS.inverse;
    if (CenterOfMassTransform == transform) {
      // Edge case when they are equal: delta_LS will only be approximately identity.
      // Make it exactly identity for a smidge more accuracy
      delta_LS = TrTransform.identity;
    }

    LocalTransform = delta_LS * LocalTransform;
  }

  /// I believe (but am not sure) that Media Library content loads synchronously,
  /// and PAC content loads asynchronously.
  public static void CreateFromSaveData(TiltModels75 modelDatas) {
    Debug.AssertFormat(modelDatas.AssetId == null || modelDatas.FilePath == null,
        "Model Data should not have an AssetID *and* a File Path");
    Debug.AssertFormat(!modelDatas.InSet_deprecated,
        "InSet should have been removed at load time");

    bool ok;
    if (modelDatas.FilePath != null) {
      ok = CreateModelsFromRelativePath(
          modelDatas.FilePath,
          modelDatas.Transforms, modelDatas.RawTransforms, modelDatas.PinStates,
          modelDatas.GroupIds);
    } else if (modelDatas.AssetId != null) {
      CreateModelsFromAssetId(
          modelDatas.AssetId,
          modelDatas.RawTransforms, modelDatas.PinStates, modelDatas.GroupIds);
      ok = true;
    } else {
      Debug.LogError("Model Data doesn't contain an AssetID or File Path.");
      ok = false;
    }

    if (!ok) {
      ModelCatalog.m_Instance.AddMissingModel(
          modelDatas.FilePath, modelDatas.Transforms, modelDatas.RawTransforms);
    }
  }

  /// I believe (but am not sure) that this is synchronous.
  /// Returns false if the model can't be loaded -- in this case, caller is responsible
  /// for creating the missing-model placeholder.
  public static bool CreateModelsFromRelativePath(
      string relativePath,
      TrTransform[] xfs, TrTransform[] rawXfs, bool[] pinStates, uint[] groupIds) {
    // Verify model is loaded.  Or, at least, has been tried to be loaded.
    Model model = ModelCatalog.m_Instance.GetModel(relativePath);
    if (model == null) {
      return false;
    }
    if (!model.m_Valid) { model.LoadModel(); }
    if (!model.m_Valid) {
      return false;
    }

    if (xfs != null) {
      // Pre M13 format
      for (int i = 0; i < xfs.Length; ++i) {
        bool pin = (pinStates != null && i < pinStates.Length) ? pinStates[i] : true;
        uint groupId = (groupIds != null && i < groupIds.Length) ? groupIds[i] : 0;
        CreateModel(model, xfs[i], pin, isNonRawTransform: true, groupId);
      }
    }
    if (rawXfs != null) {
      // Post M13 format
      for (int i = 0; i < rawXfs.Length; ++i) {
        bool pin = (pinStates != null && i < pinStates.Length) ? pinStates[i] : true;
        uint groupId = (groupIds != null && i < groupIds.Length) ? groupIds[i] : 0;
        CreateModel(model, rawXfs[i], pin, isNonRawTransform: false, groupId);
      }
    }
    return true;
  }

  /// isNonRawTransform - true if the transform uses the pre-M13 meaning of transform.scale.
  static void CreateModel(Model model, TrTransform xf, bool pin,
      bool isNonRawTransform, uint groupId, string assetId = null) {
    var modelWidget = Instantiate(WidgetManager.m_Instance.ModelWidgetPrefab) as ModelWidget;
    modelWidget.transform.localPosition = xf.translation;
    modelWidget.transform.localRotation = xf.rotation;
    modelWidget.Model = model;
    modelWidget.m_LoadingFromSketch = true;
    modelWidget.Show(true, false);
    if (isNonRawTransform) {
      modelWidget.SetWidgetSizeNonRaw(xf.scale);
      modelWidget.transform.localPosition -=
          xf.rotation * modelWidget.Model.m_MeshBounds.center * modelWidget.GetSignedWidgetSize();
    } else {
      modelWidget.SetSignedWidgetSize(xf.scale);
    }
    TiltMeterScript.m_Instance.AdjustMeterWithWidget(modelWidget.GetTiltMeterCost(), up: true);
    if (pin) {
      modelWidget.PinFromSave();
    }

    if (assetId != null && !model.m_Valid) {
      App.PolyAssetCatalog.CatalogChanged += modelWidget.OnPacCatalogChanged;
      modelWidget.m_PolyCallbackActive = true;
    }
    modelWidget.Group = App.GroupManager.GetGroupFromId(groupId);
  }

  // Used when loading model assetIds from a serialized format (e.g. Tilt file).
  static void CreateModelsFromAssetId(
      string assetId, TrTransform[] rawXfs,
      bool[] pinStates, uint[] groupIds) {
    // Request model from Poly and if it doesn't exist, ask to load it.
    Model model = App.PolyAssetCatalog.GetModel(assetId);
    if (model == null) {
      // This Model is transient; the Widget will replace it with a good Model from the PAC
      // as soon as the PAC loads it.
      model = new Model(Model.Location.PolyAsset(assetId, null));
    }
    if (!model.m_Valid) {
      App.PolyAssetCatalog.RequestModelLoad(assetId, "widget");
    }

    // Create a widget for each transform.
    for (int i = 0; i < rawXfs.Length; ++i) {
      bool pin = (i < pinStates.Length) ? pinStates[i] : true;
      uint groupId = (groupIds != null && i < groupIds.Length) ? groupIds[i] : 0;
      CreateModel(model, rawXfs[i], pin, isNonRawTransform: false, groupId, assetId: assetId);
    }
  }

  override public bool HasGPUIntersectionObject() {
    return m_ModelInstance != null;
  }

  override public void SetGPUIntersectionObjectLayer(int layer) {
    HierarchyUtils.RecursivelySetLayer(m_ModelInstance, layer);
  }

  override public void RestoreGPUIntersectionObjectLayer() {
    HierarchyUtils.RecursivelySetLayer(m_ModelInstance, m_BackupLayer);
  }

  override public bool CanSnapToHome() {
    return m_Model.m_MeshBounds.center == Vector3.zero;
  }
}
}  // namespace TiltBrush
