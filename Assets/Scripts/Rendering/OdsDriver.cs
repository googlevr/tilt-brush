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
public class OdsDriver : MonoBehaviour {

  [System.NonSerialized] public float m_fps = 30;
  [SerializeField] private ODS.HybridCamera m_odsCamera;
  [SerializeField] private int m_framesToCapture = 0;
  [SerializeField] private float m_turnTableRotation = 0;
  private string m_outputFolder;
  private string m_outputBasename;

  private bool m_frameTick = false;
  private System.Diagnostics.Stopwatch m_frameTimer = new System.Diagnostics.Stopwatch();
  private System.Diagnostics.Stopwatch m_renderTimer = new System.Diagnostics.Stopwatch();
  private string m_videoPath;
  private string m_imagesPath;
#if USD_SUPPORTED
  private UsdPathSerializer m_PathSerializer;
#endif

  private bool HaveCameraPath {
    get {
#if USD_SUPPORTED
      return m_PathSerializer != null;
#else
      return false;
#endif
    }
  }

  public ODS.HybridCamera OdsCamera {
    get { return m_odsCamera; }
  }

  public int FramesToCapture {
    get { return m_framesToCapture; }
    set {
      m_framesToCapture = value;
      App.Instance.FrameCountDisplay.SetFramesTotal(m_framesToCapture);
    }
  }

  public float TurnTableRotation {
    get { return m_turnTableRotation; }
    set { m_turnTableRotation = value; }
  }

  public string CameraPath { get; set; }

  public string OutputFolder {
    get { return m_outputFolder; }
    set { m_outputFolder = value; }
  }

  public string OutputBasename {
    get { return m_outputBasename; }
    set { m_outputBasename = value; }
  }

  public bool IsRendering { get; private set; }

  public void BeginRender() {
    IsRendering = true;

    // Turn off shadows, until depth disparity can be fixed.
    // See http://b/68952256 for details
    UnityEngine.QualitySettings.shadows = ShadowQuality.Disable;
    for (int i = 0; i < App.Scene.GetNumLights(); i++) {
      App.Scene.GetLight(i).shadows = LightShadows.None;
    }

    for (int i = 0; ; ++i) {
      string o = string.Format("{0}_{1:00}", m_outputBasename, i);
      string outVid = Path.Combine(m_outputFolder, o + ".mp4");
      string outDir = Path.Combine(m_outputFolder, o + "/");

      if (!File.Exists(outVid) && !File.Exists(outDir)) {
        m_videoPath = outVid;
        m_imagesPath = outDir + m_outputBasename;

        m_odsCamera.outputFolder = outDir;
        m_odsCamera.basename = m_outputBasename;

        // Touch the destination folder.
        Directory.CreateDirectory(outDir);

        // Touch the destination file.
        FileStream fs = File.Open(m_videoPath, FileMode.OpenOrCreate,
                                FileAccess.Write, FileShare.ReadWrite);
        fs.Close();
        fs.Dispose();
        File.SetLastWriteTimeUtc(m_videoPath, System.DateTime.UtcNow);
        break;
      }
    }

    if (!string.IsNullOrEmpty(CameraPath) && File.Exists(CameraPath)) {
#if USD_SUPPORTED
      m_PathSerializer = gameObject.AddComponent<UsdPathSerializer>();
      if (m_PathSerializer.Load(CameraPath)) {
        m_PathSerializer.StartPlayback("/Sketch", "/VideoCamera");
        FramesToCapture = Mathf.FloorToInt((float)(m_PathSerializer.Duration) * m_fps);
        m_PathSerializer.Time = 0;
        m_PathSerializer.Deserialize();
      } else {
        Destroy(m_PathSerializer);
        m_PathSerializer = null;
      }
#else
      throw new System.NotImplementedException("CameraPath requires USD support");
#endif
    }

    Debug.LogFormat("ODS Output Video: {0}" + System.Environment.NewLine +
                    "ODS Output Path: {1}" + System.Environment.NewLine +
                    "ODS Basename: {2}"
                    , m_videoPath, m_odsCamera.outputFolder, m_odsCamera.basename);

    Shader.SetGlobalFloat("ODS_PoleCollapseAmount", App.UserConfig.Video.OdsPoleCollapsing);


    gameObject.SetActive(true);

    App.Instance.FrameCountDisplay.gameObject.SetActive(true);
  }

  /// Returns the interpolated scene pose based on the app primary and secondary ODS tranforms.
  /// Progress the interpolation parameter which is expected to be in the range [0, 1].
  /// Interpolation is done via TrTransform, which gives correct rotation and scale interpoalation.
  public static TrTransform GetScenePose(float progress) {
    // Interpolate in worldspace
    TrTransform cameraStart = App.Instance.OdsScenePrimary.inverse;
    TrTransform cameraEnd = App.Instance.OdsSceneSecondary.inverse;
    TrTransform cameraCur = TrTransform.Lerp(cameraStart * App.Instance.OdsHeadPrimary,
                                             cameraEnd * App.Instance.OdsHeadSecondary,
                                             progress);
    return cameraCur.inverse;
  }

  void Update() {
    if (m_odsCamera.FrameCount >= m_framesToCapture) {
      if (m_framesToCapture > 0) {
        // We rendered everything.
        if ((Application.platform == RuntimePlatform.WindowsPlayer) ||
            (Application.platform == RuntimePlatform.WindowsEditor)) {
              System.Diagnostics.Process.Start("explorer.exe", "/open," + m_outputFolder);
        }

        System.Diagnostics.Process proc = new System.Diagnostics.Process();
        proc.StartInfo.FileName = Path.GetFullPath(TiltBrush.FfmpegPipe.GetFfmpegExe());
        proc.StartInfo.Arguments = System.String.Format(
          @"-y -framerate {0} -f image2 -i ""{1}_%06d.png"" " +
          @"-c:v " + FfmpegPipe.GetVideoEncoder() + @" -r {0} -pix_fmt yuv420p ""{2}""",
          m_fps,
          m_imagesPath,
          m_videoPath);
        Debug.LogFormat("{0} {1}", proc.StartInfo.FileName, proc.StartInfo.Arguments);
        proc.StartInfo.CreateNoWindow = false;
        proc.StartInfo.ErrorDialog = true;
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardError = true;
        proc.Start();

        UnityEngine.Debug.Log(proc.StandardError.ReadToEnd());

#if USD_SUPPORTED
        if (m_PathSerializer != null) {
          m_PathSerializer.Stop();
        }
#endif

        proc.Close();
        Application.Quit();
        Debug.Break();
      }
      return;
    }

    if (m_odsCamera.IsRendering) {
      return;
    }

    if (m_frameTick) {
      if (m_frameTimer.ElapsedMilliseconds < 1000.0f / m_fps) {
        return;
      }
      m_frameTimer.Stop();
    }

    if (m_renderTimer.IsRunning) {
      m_renderTimer.Stop();

      Debug.LogFormat("ODS Frame Time: {0}", m_renderTimer.ElapsedMilliseconds / 1000.0f);
    }

    m_frameTick = !m_frameTick;

    if (m_frameTick) {
      Time.timeScale = 1.0f;
      m_frameTimer.Reset();
      m_frameTimer.Start();
      return;
    }

    Time.timeScale = 0.0f;

    if (! HaveCameraPath) {
      float progress = m_odsCamera.FrameCount / (float) m_framesToCapture;
      App.Scene.Pose = GetScenePose(progress);

      if (m_turnTableRotation > 0) {
        TiltBrush.TrTransform sc = App.Scene.Pose;
        sc.rotation = Quaternion.AngleAxis(progress * m_turnTableRotation, Vector3.up);
        App.Scene.Pose = sc;
      }
    } else {
#if USD_SUPPORTED
      m_PathSerializer.Time = m_odsCamera.FrameCount / m_fps;
      m_PathSerializer.Deserialize();
#endif
    }

    Camera cam = OdsCamera.GetComponent<Camera>();
    Camera parentCam = TiltBrush.App.VrSdk.GetVrCamera();
    cam.clearFlags = parentCam.clearFlags;
    cam.backgroundColor = parentCam.backgroundColor;

    // Copy back the culling mask so the preview window looks like the final render.
    parentCam.cullingMask = cam.cullingMask;

    if (m_odsCamera.FrameCount == 0 && m_framesToCapture > 0) {
      if (QualitySettings.GetQualityLevel() != 3) {
        QualitySettings.SetQualityLevel(3);
      }
    }
    App.Instance.FrameCountDisplay.SetCurrentFrame(m_odsCamera.FrameCount);

    // Move the viewer camera, so the user can see what's going on.
    Transform viewerXform = App.VrSdk.GetVrCamera().transform;
    viewerXform.position = transform.position;
    viewerXform.rotation = transform.rotation;
    viewerXform.localScale = transform.localScale;

    m_renderTimer.Reset();
    m_renderTimer.Start();
    StartCoroutine(m_odsCamera.Render(transform));
  }

}
}  // namespace TiltBrush
