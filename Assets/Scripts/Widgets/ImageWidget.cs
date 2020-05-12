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

using System.IO;

namespace TiltBrush {
public class ImageWidget : Media2dWidget {
  private static T Unused<T>(T value) {
    Debug.LogError("Supposedly-unused value is being used");
    return value;
  }

  [SerializeField] private float m_VertCountScalar = 1;

  private bool m_UseLegacyTint;
  private ReferenceImage m_ReferenceImage;
  private bool m_TextureAcquired;

  /// A string which can be passed to ReferenceImageCatalog.FileNameToIndex.
  /// Currently, this is a file _name_.
  public string FileName =>
      m_ReferenceImage?.FileName ?? m_MissingInfo?.fileName ?? Unused("Error");

  /// width / height
  public override float? AspectRatio =>
      m_ReferenceImage?.ImageAspect ?? m_MissingInfo?.aspectRatio;

  /// Prior to M15, images were incorrectly tinted.
  public bool UseLegacyTint {
    get { return m_UseLegacyTint; }
    set {
      m_UseLegacyTint = value;
      float tintValue = m_UseLegacyTint ? 1.0f : 0.0f;
      m_ImageQuad.material.SetFloat("_LegacyReferenceImageTint", tintValue);
    }
  }

  override protected void OnDestroy() {
    base.OnDestroy();
    ReleaseTexture();
  }

  override public GrabWidget Clone() {
    ImageWidget clone = Instantiate(WidgetManager.m_Instance.ImageWidgetPrefab);
    clone.transform.position = transform.position;
    clone.transform.rotation = transform.rotation;
    // We're obviously not loading from a sketch.  This is to prevent the intro animation.
    // TODO: Change variable name to something more explicit of what this flag does.
    clone.m_LoadingFromSketch = true;
    // We also want to lie about our intro transition amount.
    // TODO: I think this is an old concept and redundant with other intro anim members.
    clone.m_TransitionScale = 1.0f;
    if (m_ReferenceImage != null) {
      m_ReferenceImage.SynchronousLoad();
    }
    clone.ReferenceImage = m_ReferenceImage;
    clone.Show(true, false);
    clone.transform.parent = transform.parent;
    clone.SetSignedWidgetSize(this.m_Size);
    clone.UseLegacyTint = this.m_UseLegacyTint;
    HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
    TiltMeterScript.m_Instance.AdjustMeterWithWidget(clone.GetTiltMeterCost(), up: true);
    clone.CloneInitialMaterials(this);
    clone.TrySetCanvasKeywordsFromObject(transform);
    return clone;
  }

  override public void RestoreFromToss() {
    base.RestoreFromToss();
    AcquireTexture();
  }

  override protected void OnHide() {
    base.OnHide();
    ReleaseTexture();
  }

  public override string GetExportName() {
    if (m_ReferenceImage != null) {
      return m_ReferenceImage.GetExportName();
    } else {
      return Path.GetFileNameWithoutExtension(FileName);
    }
  }

  private void AcquireTexture() {
    if (m_ReferenceImage != null) {
      if (!m_TextureAcquired) {
        m_ReferenceImage.AcquireImageFullsize(m_LoadingFromSketch);
        ImageTexture = m_ReferenceImage.FullSize;
        m_TextureAcquired = true;
      }
    } else {
      ImageTexture = m_NoImageTexture;
      m_TextureAcquired = false;
    }
  }

  private void ReleaseTexture() {
    if (m_ReferenceImage != null) {
      m_ReferenceImage.ReleaseImageFullsize();
      m_TextureAcquired = false;
    }
    ImageTexture = m_NoImageTexture;
  }

  public ReferenceImage ReferenceImage {
    set {
      //dump old texture
      ReleaseTexture();

      m_ReferenceImage = value;

      AcquireTexture();

      // Remove previous image vertex recording.
      WidgetManager.m_Instance.AdjustImageVertCount(-m_NumVertsTrackedByWidgetManager);
      m_NumVertsTrackedByWidgetManager = 0;

      if (m_ReferenceImage != null) {
        //update the aspect ratio of our mesh to match the image
        m_Mesh.transform.localScale = Vector3.one * 0.5f;
        var sizeRange = GetWidgetSizeRange();
        if (m_ReferenceImage.ImageAspect > 1) {
          m_Size = Mathf.Clamp(2 / m_ReferenceImage.ImageAspect / Coords.CanvasPose.scale,
            sizeRange.x, sizeRange.y);
        } else {
          m_Size = Mathf.Clamp(2 * m_ReferenceImage.ImageAspect / Coords.CanvasPose.scale,
            sizeRange.x, sizeRange.y);
        }
        UpdateScale();

        m_NumVertsTrackedByWidgetManager = (int)((m_ReferenceImage.FullSize.width *
            m_ReferenceImage.FullSize.height) * m_VertCountScalar);
        WidgetManager.m_Instance.AdjustImageVertCount(m_NumVertsTrackedByWidgetManager);
      }

      // Images are created in the main canvas.
      HierarchyUtils.RecursivelySetLayer(transform, App.Scene.MainCanvas.gameObject.layer);
      HierarchyUtils.RecursivelySetMaterialBatchID(transform, m_BatchId);

      InitSnapGhost(m_ImageQuad.transform, transform);
    }
    get { return m_ReferenceImage; }
  }

  public bool IsImageValid() {
    return m_ReferenceImage != null && m_ReferenceImage.Valid;
  }

  // Get the color of the active texture at the specified (u, v) coordinate.
  public bool GetPixel(float u, float v, out Color pixelColor) {
    if (!IsImageValid()) {
      pixelColor = Color.magenta;
      return false;
    }

    pixelColor = m_ReferenceImage.FullSize.GetPixelBilinear(u, v);
    return true;
  }

  public static void FromTiltImage(TiltImages75 tiltImage) {
    var refImage = ReferenceImageCatalog.m_Instance.FileNameToImage(tiltImage.FileName);
    var groupIds = tiltImage.GroupIds;
    for (int i = 0; i < tiltImage.Transforms.Length; ++i) {
      ImageWidget image = Instantiate(WidgetManager.m_Instance.ImageWidgetPrefab);
      image.m_LoadingFromSketch = true;
      image.transform.parent = App.Instance.m_CanvasTransform;
      image.transform.localScale = Vector3.one;
      if (refImage != null) {
        refImage.SynchronousLoad();
        image.ReferenceImage = refImage;
      } else {
        image.SetMissing(tiltImage.AspectRatio, tiltImage.FileName);
      }
      image.SetSignedWidgetSize(tiltImage.Transforms[i].scale);
      image.Show(bShow: true, bPlayAudio: false);
      image.transform.localPosition = tiltImage.Transforms[i].translation;
      image.transform.localRotation = tiltImage.Transforms[i].rotation;
      if (tiltImage.PinStates[i]) {
        image.PinFromSave();
      }
      if (tiltImage.TintStates[i]) {
        image.UseLegacyTint = true;
      }
      uint groupId = (groupIds != null && i < groupIds.Length) ? groupIds[i] : 0;
      image.Group = App.GroupManager.GetGroupFromId(groupId);
      TiltMeterScript.m_Instance.AdjustMeterWithWidget(image.GetTiltMeterCost(), up: true);
    }
  }

}
} // namespace TiltBrush
