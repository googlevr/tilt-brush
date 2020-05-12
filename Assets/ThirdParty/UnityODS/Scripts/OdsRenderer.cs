using UnityEngine;
using System.Collections;

namespace ODS {

// ODS Renderer base class
// An OdsRenderer handles generating ODS Images from Unity scenes.
public class OdsRenderer {
  protected int imageWidth    = 0;
  protected int eyeImageWidth = 0;
  protected int bloomRadius   = 0;
  protected bool vr180 = false;

  /// Release resources such as RenderTextures
  virtual public void Release() { 
  }

  /// Enable Youtube VR 180 mode
  virtual public void SetVr180(bool enable) {
    vr180 = enable;
  }

  /// Set the desired ODS image width
  /// width: Image Width in pixels.
  /// eyeWidth: Image Width in pixels including post-processing border.
  /// bloomRad: Radius of the bloom filter, set to 0 if bloom is not desired.
  virtual public void SetWidth(int width, int eyeWidth, int bloomRad) {
    imageWidth    = width;
    eyeImageWidth = eyeWidth;
    bloomRadius   = bloomRad;
  }

  /// Setup and allocate any textures and RenderTextures that specific Ods
  /// Renderer requires.
  /// format: The desired render target format.
  virtual public void SetupTextures(RenderTextureFormat format) {
  }

  /// Render an Ods Image to the output RenderTexture.
  /// renderCamera: The Camera used for rendering.
  /// node: The node that defines the position and orientation from which to render.
  /// output: The output RenderTexture that will contain the ODS Image.
  /// ipd: Interpupillary distance
  /// CollapseIpd: True if the IPD should be collapsed towards the poles.
  /// MaxRenders: The maximum number of renders allowed per-frame.
  virtual public IEnumerator Render(Camera renderCamera, Transform node, RenderTexture output, 
    float ipd, bool CollapseIpd, int MaxRenders) {
    yield return null;
  }
} // class OdsRenderer

} // namespace ODS
