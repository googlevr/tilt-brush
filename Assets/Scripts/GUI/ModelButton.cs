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
using System.Collections;

namespace TiltBrush {

public abstract class ModelButton : BaseButton {
  [SerializeField] protected Transform m_PreviewParent;
  [SerializeField] private float m_RotationSeconds;
  [SerializeField] private Texture2D m_UnloadedTexture;
  [SerializeField] private Texture2D m_LoadedTexture;
  [SerializeField] private Texture2D m_ErrorTexture;
  [SerializeField] private string m_LoadHelpText;
  protected Model m_Model;
  protected Model m_ModelPreviewModel;
  protected int m_ModelIndex;
  protected Transform m_ModelPreview;
  protected Quaternion m_PreviewBaseRotation;
  private float m_AnimateValue;

  protected virtual Texture2D UnloadedTexture { get { return m_UnloadedTexture; } }
  protected virtual string ModelName { get { return m_Model.HumanName; } }

  public void CreatePreviewModel() {
    // Is there a model, is it valid, and does it need a new preview object?
    if (m_Model == null || !m_Model.m_Valid || m_Model == m_ModelPreviewModel) {
      return;
    }

    // We know the model has changed, so destroy the preview.
    if (m_ModelPreview != null) {
      Destroy(m_ModelPreview.gameObject);
    }

    // Remember the model for which this preview was created.
    m_ModelPreviewModel = m_Model;

    // Build the actual preview.
    m_ModelPreview = Instantiate(m_Model.m_ModelParent);
    HierarchyUtils.RecursivelySetLayer(m_ModelPreview, LayerMask.NameToLayer("Panels"));
    m_ModelPreview.gameObject.SetActive(true);
    m_ModelPreview.parent = m_PreviewParent;
    float maxSide = Mathf.Max(m_Model.m_MeshBounds.size.x,
      Mathf.Max(m_Model.m_MeshBounds.size.y, m_Model.m_MeshBounds.size.z));
    TrTransform xf = TrTransform.S(1 / maxSide) * TrTransform.T(-m_Model.m_MeshBounds.center);
    Coords.AsLocal[m_ModelPreview] = xf;
    HierarchyUtils.RecursivelyDisableShadows(m_ModelPreview);
  }

  protected virtual void RefreshModelButton() {
    // If we have a model and it failed to load, show the error texture and message.
    if (m_Model != null && m_Model.Error is Model.LoadError error) {
      SetDescriptionText(App.ShortenForDescriptionText(ModelName), error.message);
      SetButtonTexture(m_ErrorTexture, 1);
      return;
    } else if (m_Model != null && m_Model.m_Valid) {
      // If we have a loaded model, show the loaded texture (transparent) and the
      // appropriate sub text (null for local models, author for Poly models).
      SetButtonTexture(m_LoadedTexture, 1);
      SetDescriptionText(App.ShortenForDescriptionText(ModelName), LoadedExtraDescription());
      return;
    }

    // If we don't have a model or we do, but it's not loaded, show the unloaded texture
    // (download button for local models, thumbnail for Poly models) and the appropriate
    // sub text (m_LoadHelpText for local models, author for Poly models).
    SetButtonTexture(UnloadedTexture, 1);
    SetDescriptionText(App.ShortenForDescriptionText(ModelName), UnloadedExtraDescription());
  }

  /// Begins loading the Model.
  /// This is a no-op if the Model loaded successfully or with an error.
  /// If you really want to try it again, clear the error before calling this method.
  public void RequestModelPreload(int pageIndex) {
    if (m_Model != null) {
      if (m_Model.m_Valid || m_Model.Error != null) {
        // Already tried to load it once.
        return;
      }
    }

    // Let the subclass handle this request.
    RequestModelPreloadInternal($"preload page {pageIndex}");
  }

  /// Requests that the model associated with the button be preloaded into memory.
  /// It's okay if this request is ignored.
  /// Pass:
  ///   reason - the reason the model is being requested (it'll always be a preload)
  abstract protected void RequestModelPreloadInternal(string reason);

  /// We don't want the preload any more; try to tear it down.
  abstract protected void CancelRequestModelPreload();

  override protected void OnButtonPressed() {
    // nb: Poly models override this even further and do something fancier.
    // This implementation is used only(?) for local media models.
    if (m_Model.m_Valid) {
      SpawnValidModel(m_Model);
    } else {
      StartCoroutine(LoadModelAndRefreshButton());
    }
  }

  private IEnumerator LoadModelAndRefreshButton() {
    yield return m_Model.LoadFullyCoroutine("buttonpress");
    if (m_Model.Error.HasValue) {
      // Not sure why we only refresh the button on error, but... that's what the code used to do
      RefreshModelButton();
    }
  }

  /// Spawn a model.
  /// Precondition: Model must have m_Valid == true.
  protected void SpawnValidModel(Model model) {
    if (! model.m_Valid) {
      throw new InvalidOperationException("model must be valid");
    }
    // Button forward is into the panel, not out of the panel; so flip it around
    TrTransform xfSpawn = Coords.AsGlobal[transform]
                          * TrTransform.R(Quaternion.AngleAxis(180, Vector3.up));
    CreateWidgetCommand createCommand = new CreateWidgetCommand(
        WidgetManager.m_Instance.ModelWidgetPrefab, xfSpawn, m_PreviewBaseRotation);
    SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
    ModelWidget modelWidget = createCommand.Widget as ModelWidget;
    modelWidget.Model = model;
    modelWidget.Show(true);
    createCommand.SetWidgetCost(modelWidget.GetTiltMeterCost());

    WidgetManager.m_Instance.WidgetsDormant = false;
    SketchControlsScript.m_Instance.EatGazeObjectInput();
    SelectionManager.m_Instance.RemoveFromSelection(false);
  }

  public void SetPreset(Model model, int index) {
    m_Model = model;
    RefreshModelButton();
    m_ModelIndex = index;
    m_PreviewBaseRotation = Quaternion.Euler(0, 180, 0);
  }

  // This should be overridden by buttons that have special default settings for
  // the extra description.
  virtual public string UnloadedExtraDescription() {
    return m_LoadHelpText;
  }
  virtual public string LoadedExtraDescription() {
    return null;
  }

  /// An indication that this button's model is changing.
  /// Overloaded to also mean "please stop any preloads"
  public void DestroyModelPreview() {
    if (m_ModelPreview != null) {
      Destroy(m_ModelPreview.gameObject);
      m_ModelPreview = null;
    }
    m_ModelPreviewModel = null;
    CancelRequestModelPreload();
  }

  override public void UpdateVisuals() {
    base.UpdateVisuals();
    if (m_PreviewParent == null) {
      throw new ArgumentNullException("m_PreviewParent null in OnUpdate");
    } else if (m_Model != null) {
      if (m_CurrentButtonState == ButtonState.Hover) {
        m_AnimateValue += Time.deltaTime / m_RotationSeconds;
        Vector3 vLocalRot = m_PreviewBaseRotation.eulerAngles;
        vLocalRot.y += m_AnimateValue * 360;
        m_PreviewParent.localRotation = Quaternion.Euler(vLocalRot);
      } else {
        m_AnimateValue = 0;
        m_PreviewParent.localRotation = m_PreviewBaseRotation;
      }

      // Both of the following methods protect against invalid models.
      // Lazily make a copy of the model game objects.
      CreatePreviewModel();
      // Update textures, given that it may have transitioned to having a preview.
      RefreshModelButton();
    }
  }
}
}  // namespace TiltBrush
