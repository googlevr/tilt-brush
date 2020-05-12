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

using System.IO;
using UnityEngine;

namespace TiltBrush {

/// In this class, "Landscape" is the term used for cameras at 0 and 180 degree rotation.
/// "Portrait" means 90 or 270 degree camera rotation.
///
/// General Requirements:
/// - Must have a sibling Camera component.
///
/// Requirements for m_AutoAlignRig:
/// - parent must start with identity rotation
/// - it must be OK to modify parent's rotation
///
/// Requirements for m_UseStereoRig:
/// - this object's local transform must be identity
///
public class ScreenshotManager : MonoBehaviour {
  class CameraInfo {
    // Material is mutated to display renderTexture
    public MeshRenderer renderer;
    // Camera is mutated to write to renderTexture
    public Camera camera;
    public RenderTexture renderTexture;
  }

  const float MM_TO_UNITS = .001f * App.METERS_TO_UNITS;
  const float HYSTERESIS_DEGREES = 10;

  // Cached off or created on Start(); otherwise read-only
  private CameraInfo m_LeftInfo;
  private CameraInfo m_RightInfo;
  private Vector2[] m_RendererUVs;
  private float m_LandscapeFov;
  private float m_PortraitFov;

  public bool IsPortrait {
    get { return m_bIsPortraitMode; }
    set {
      if (m_bIsPortraitModeLocked) {
        return;
      }
      m_bIsPortraitMode = value;
      CreateDisplayRenderTextures();
      UpdateCameraAspect();
    }
  }
  private bool m_bIsPortraitMode = false;

  // Enable/disable set-access to IsPortrait
  public bool IsPortraitModeLocked {
    get { return m_bIsPortraitModeLocked; }
    set { m_bIsPortraitModeLocked = value; }
  }
  private bool m_bIsPortraitModeLocked = false;

  /// Where the camera's output should be fed.
  public MeshRenderer m_Display;
  /// On startup, use config file for width and set height to match the aspect ratio.
  public bool m_UseDisplayWidthFromConfigFile;
  // Width of the live render target
  public int m_DisplayWidth;
  // Height of the live render target
  public int m_DisplayHeight;

  /// When set, align camera rig with head; automatically switches
  /// orientations to portrait mode.
  public bool m_AutoAlignRig = false;
  /// When set, use a stereo camera rig
  public bool m_UseStereoRig = false;
  /// Distance from centerline to camera, when using stereo rig. In millimeters.
  public float m_InterAxialOffset = 22f;
  /// Convergence distance, as a multiple of IAO.
  public float m_ConvergenceFactor = 10f;

  /// The left-eye camera is also the main camera when m_UseStereoRig=false.
  public Camera LeftEye { get => LeftInfo.camera; }
  public Material LeftEyeMaterial { get => LeftInfo.renderer.material; }
  public bool LeftEyeMaterialRenderTextureExists { get => LeftInfo.renderTexture != null; }
  
  private CameraInfo LeftInfo { 
    get {
      // Need to lazy-init this; others might try to call our public API before
      // our Awake() and Start() have been called (because object starts inactive)
      if (m_LeftInfo == null) {
        m_LeftInfo = new CameraInfo();
        m_LeftInfo.camera = GetComponent<Camera>();
        m_LeftInfo.renderer = m_Display;
      }
      return m_LeftInfo;
    }
  }

  void Start() {
    // Check that we never get here if we're a clone (because we remove
    // the ScreenshotManager from the clone)
    Debug.Assert(transform.parent != null);

    if (m_AutoAlignRig) {
      // Because we need to override parent.localRotation
      Debug.Assert(transform.parent.localRotation == Quaternion.identity);
    }
    if (m_UseStereoRig) {
      // Because we need to override position (for stereo offset)
      // and rotation (for convergence)
      Debug.Assert(transform.localRotation == Quaternion.identity);
      Debug.Assert(transform.localPosition == Vector3.zero);
      Debug.Assert(transform.localScale == Vector3.one);
    }

    m_RendererUVs = m_Display.GetComponent<MeshFilter>().mesh.uv;

    // Lazy init.
    m_LeftInfo = LeftInfo;

    // If requested, create a stereo camera rig
    if (m_UseStereoRig && App.VrSdk.GetHmdDof() == VrSdk.DoF.Six) {
      Debug.Assert(LayerMask.NameToLayer("SteamVRLeftEye") != 0);

      // Duplicate the camera
      {
        var src = m_LeftInfo.camera.gameObject;
        var dst = Instantiate(src);
        dst.name = src.name + "_Right";
        DestroyImmediate(dst.GetComponent<ScreenshotManager>());
        dst.transform.parent = src.transform.parent;
        dst.transform.localPosition = Vector3.zero;
        dst.transform.localRotation = Quaternion.identity;
        m_RightInfo = new CameraInfo();
        m_RightInfo.camera = dst.GetComponent<Camera>();
      }

      // Duplicate the renderer
      {
        var src = m_Display.gameObject;
        var dst = Instantiate(src);
        dst.name = src.name + "_Right";
        dst.transform.parent = src.transform.parent;
        dst.transform.localPosition = src.transform.localPosition;
        dst.transform.localRotation = src.transform.localRotation;
        dst.transform.localScale = src.transform.localScale; // ugh
        dst.layer = LayerMask.NameToLayer("SteamVRRightEye");
        src.layer = LayerMask.NameToLayer("SteamVRLeftEye");
        m_RightInfo.renderer = dst.GetComponent<MeshRenderer>();
      }
    }

    SceneSettings.m_Instance.RegisterCamera(m_LeftInfo.camera);
    if (m_RightInfo != null) {
      SceneSettings.m_Instance.RegisterCamera(m_RightInfo.camera);
    }

    if (!App.Config.PlatformConfig.EnableMulticamPreview) {
      // If we're looking through the viewfinder, we need to make some changes to this camera
      SetScreenshotResolution(App.UserConfig.Flags.SnapshotWidth > 0
          ? App.UserConfig.Flags.SnapshotWidth : 1920);
      IsPortraitModeLocked = true;
    }
    if (App.Config.IsMobileHardware) {
      // Force no HDR on mobile
      if (m_LeftInfo == null) {
        Debug.LogAssertion("ScreenshotManager m_LeftInfo is null in ScreenshotManager.Start.");
      } else if (m_LeftInfo.camera == null) {
        Debug.LogAssertion("ScreenshotManager m_LeftInfo.camera  is null in ScreenshotManager.Start.");
      } else {
        m_LeftInfo.camera.allowHDR = false;
      }
      var mobileBloom = GetComponent<MobileBloom>();
      if (mobileBloom != null) {
        mobileBloom.enabled = true;
      } else {
        Debug.LogAssertion("No MobileBloom on the Screenshot Manager.");
      }
      var pcBloom = GetComponent<SENaturalBloomAndDirtyLens>();
      if (pcBloom != null) {
        pcBloom.enabled = false;
      } else {
        Debug.LogAssertion("No SENaturalBloomAndDirtyLens on the Screenshot Manager.");
      }
    }
    if (m_UseDisplayWidthFromConfigFile) {
      SetScreenshotResolution(App.UserConfig.Video.Resolution);
    }
    CreateDisplayRenderTextures();

    CameraConfig.FovChanged += RefreshFovs;
    RefreshFovs();
  }

  void RefreshFovs() {
    m_LandscapeFov = CameraConfig.Fov;
    // Given:
    //  tan(fovY/2) = h / d;
    //  tan(fovX/2) = w / d;
    // Solve for fovX as a function of fovY:
    //  fovX = 2 atan( w/h * tan(fovY/2) )
    {
      float invAspect = (float)m_DisplayWidth / m_DisplayHeight;
      float fovY = m_LandscapeFov * Mathf.Deg2Rad;
      float fovX = 2 * Mathf.Atan(invAspect * Mathf.Tan(fovY/2));
      m_PortraitFov = fovX * Mathf.Rad2Deg;
    }
  }

  public void SetScreenshotResolution(int width) {
    int oldWidth = m_DisplayWidth;
    int oldHeight = m_DisplayHeight;
    m_DisplayWidth = width;
    // Preserve the aspect ratio using exact math (most likely 16 x 9)
    m_DisplayHeight = (oldHeight * width) / oldWidth;
    // Don't allow odd widths and heights.
    if ((m_DisplayWidth % 2 == 1) || (m_DisplayHeight % 2 == 1)) {
      m_DisplayHeight = Mathf.FloorToInt(m_DisplayHeight / 2) * 2;
      m_DisplayWidth = Mathf.FloorToInt(m_DisplayWidth / 2) * 2;
      OutputWindowScript.Error("Odd-numbered capture dimensions not supported.",
        string.Format("Capture dimensions capped to {0}x{1}.", m_DisplayWidth, m_DisplayHeight));
    }
    CreateDisplayRenderTextures();
  }

  void Update() {
    if (m_RightInfo != null) {
      Transform tL = m_LeftInfo.camera.transform;
      Transform tR = m_RightInfo.camera.transform;
      Vector3 offset = new Vector3(m_InterAxialOffset * MM_TO_UNITS, 0,0);
      tL.localPosition = -offset;
      tR.localPosition =  offset;

      float theta = Mathf.Atan2(1, m_ConvergenceFactor) * Mathf.Rad2Deg;
      tL.localRotation = Quaternion.AngleAxis( theta, Vector3.up);
      tR.localRotation = Quaternion.AngleAxis(-theta, Vector3.up);
    }

    if (m_AutoAlignRig) {
      var headUp = ViewpointScript.Head.up;
      AlignRigTo(headUp);
    }
  }

  // Helper for AlignRigTo()
  // Set rotation of camera rig relative to its parent.
  // Behavior is undefined if degrees is not a multiple of 90.
  void SetRigRotation(float degrees) {
    Debug.Assert(degrees % 90 == 0);

    Transform root = m_LeftInfo.camera.transform.parent;
    Quaternion desiredRotation = Quaternion.AngleAxis(degrees, Vector3.forward);

    root.localRotation = desiredRotation;
    IsPortrait = ((degrees % 180) != 0);

    // Counter-rotate the UVs to compensate
    Vector2[] altUVs = new Vector2[m_RendererUVs.Length];
    Vector2 centerOfRotation = new Vector2(0.5f, 0.5f);
    float compensation = -degrees * Mathf.Deg2Rad;
    for (int i = 0; i < m_RendererUVs.Length; ++i) {
      Vector2 uv = m_RendererUVs[i];
      uv = (uv - centerOfRotation).Rotate(compensation) + centerOfRotation;
      altUVs[i] = uv;
    }
    m_LeftInfo.renderer.GetComponent<MeshFilter>().mesh.uv = altUVs;
    if (m_RightInfo != null) {
      m_RightInfo.renderer.GetComponent<MeshFilter>().mesh.uv = altUVs;
    }
  }

  float GetRigRotation() {
    Transform root = m_LeftInfo.camera.transform.parent;
    Vector3 ea = root.localRotation.eulerAngles;
    Debug.Assert(ea.x == 0 && ea.y == 0);
    return ea.z;
  }

  // Rotate camera rig about its z axis until its up direction
  // aligns as closely as possible to desiredUp
  void AlignRigTo(Vector3 desiredUp) {
    if (IsPortraitModeLocked) {
      return;
    }
    Transform rig = m_LeftInfo.camera.transform.parent;
    Transform parent = rig.parent;
    float stability;
    float desiredAngle = MathUtils.GetAngleBetween(
        parent.up, desiredUp, parent.forward, out stability);
    // Could alternatively use head-forward to infer the desired orientation
    if (stability < .1f) { return; }

    // Add hysteresis
    float delta = MathUtils.PeriodicDifference(GetRigRotation(), desiredAngle, 360);
    if (Mathf.Abs(delta) < (90/2) + HYSTERESIS_DEGREES) {
      return;
    }

    // Make multiple of 90
    desiredAngle = 90 * (int)Mathf.Round(desiredAngle/90);
    SetRigRotation(desiredAngle);
  }

  RenderTextureFormat CameraFormat() {
    return GetComponent<Camera>().allowHDR
      ? RenderTextureFormat.ARGBFloat
      : RenderTextureFormat.ARGB32;
  }

  void UpdateCameraAspect() {
    float fieldOfView = IsPortrait ? m_PortraitFov : m_LandscapeFov;
    m_LeftInfo.camera.fieldOfView = fieldOfView;
    if (m_RightInfo != null) {
      m_RightInfo.camera.fieldOfView = fieldOfView;
    }
  }

  void CreateDisplayRenderTextures() {
    RenderTextureFormat format = CameraFormat();
    CreateDisplayRenderTexture(m_LeftInfo, format, "L");
    if (m_RightInfo != null) {
      CreateDisplayRenderTexture(m_RightInfo, format, "R");
    }
  }

  void CreateDisplayRenderTexture(CameraInfo info, RenderTextureFormat format, string tag) {
    int width, height;
    width  = IsPortrait ? m_DisplayHeight : m_DisplayWidth;
    height = IsPortrait ? m_DisplayWidth : m_DisplayHeight;
    if (info.renderTexture != null
        && info.renderTexture.format == format
        && info.renderTexture.width == width
        && info.renderTexture.height == height) {
      return;
    }

    info.camera.targetTexture = null;
    Destroy(info.renderTexture);

    info.renderTexture = new RenderTexture(width, height, 0, format);
    info.renderTexture.name = "SshotTex" + tag;
    info.renderTexture.depth = 24;
    Debug.Assert(info.renderer != null);
    Debug.Assert(info.renderer.material != null);
    info.renderer.material.SetTexture("_MainTex", info.renderTexture);
    info.renderer.material.name = "SshotMat" + tag;
    info.camera.targetTexture = info.renderTexture;
  }

  /// Creates an ARGB32 save target. May transpose width and height if camera
  /// is in portrait orientation.
  /// Caller should release with RenderTexture.ReleaseTemporary() when done.
  public RenderTexture CreateTemporaryTargetForSave(int width, int height) {
    if (IsPortrait) {
      int tmp = width; width = height; height = tmp;
    }
    return RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
  }

  /// If m_AutoAlignRig is set, you should pass in a RenderTexture created
  /// with CreateTemporaryTargetForSave().
  public void RenderToTexture(RenderTexture rTexture) {
    RenderTextureFormat format = CameraFormat();
    int depth = 24;

    // Use a temporary rather than rendering to rTexture because we don't know
    // what format rTexture is... it may not be the correct format.
    RenderTexture targetA = RenderTexture.GetTemporary(
        rTexture.width, rTexture.height, depthBuffer: depth, format: format);

    {
      // Instead of doing a new Render(), it might seem tempting to copy from
      // the camera target.  That would be wrong, because the camera target's
      // resolution might be much lower than rTexture.
      var camera = LeftInfo.camera;
      var prev = camera.targetTexture;
      camera.targetTexture = targetA;
      camera.Render();
      camera.targetTexture = prev;
    }

    if (targetA != rTexture) {
      Graphics.Blit(targetA, rTexture);
      RenderTexture.ReleaseTemporary(targetA);
    }
  }

  static public void Save(Stream outf, RenderTexture rTextureToSave, bool bSaveAsPng) {
    var buffer = SaveToMemory(rTextureToSave, bSaveAsPng);
    outf.Write(buffer, 0, buffer.Length);
  }

  static public byte[] SaveToMemory(RenderTexture rTextureToSave, bool bSaveAsPng) {
    Debug.Assert(rTextureToSave.format == RenderTextureFormat.ARGB32);

    // Copy out of the RenderTexture
    Texture2D rNoAlphaTexture;
    {
      RenderTexture prev = RenderTexture.active;
      RenderTexture.active = rTextureToSave;
      rNoAlphaTexture = new Texture2D(rTextureToSave.width, rTextureToSave.height, TextureFormat.RGB24, false);
      rNoAlphaTexture.ReadPixels(new Rect(0, 0, rTextureToSave.width, rTextureToSave.height), 0, 0);
      RenderTexture.active = prev;
    }

    byte[] bytes = null;
    if (bSaveAsPng) {
      bytes = rNoAlphaTexture.EncodeToPNG();
    } else {
      bytes = rNoAlphaTexture.EncodeToJPG();
    }
    Destroy(rNoAlphaTexture);

    return bytes;
  }
}
}  // namespace TiltBrush
