using UnityEngine;
using System.Collections;

namespace ODS {

// ODS Slice Renderer
// Renders 360 degree "ODS" Equirectangular images by rendering one slice per pixel.
// Each slice requires two renders ("hemispheres") and two blits to convert from
// a perspective projection to the correct ODS projection.
public class OdsSlice : OdsRenderer {
  public RenderTexture cameraTexture = null;
  public Material warp = null;

  private enum Eye {
    Right = 0,
    Left = 1
  };

  private enum Hemisphere {
    Top = 0,
    Bottom = 1
  };

  public override void Release() {
    if (cameraTexture != null) { cameraTexture.Release(); }
    cameraTexture = null;
  }

  override public void SetupTextures(RenderTextureFormat format) {
    int imageHeight    = imageWidth;
    int viewportHeight = imageHeight / 4;

    Release();
    cameraTexture = new RenderTexture(8, viewportHeight * 8, 24, format);
    cameraTexture.antiAliasing = 1;
  }

  public override IEnumerator Render(Camera renderCamera, Transform node, RenderTexture output, 
    float ipd, bool CollapseIpd, int MaxRenders) {
    if (warp == null) {
      warp = new Material(Shader.Find("Hidden/Warp2"));
    }

    float dTheta;
    Vector4 rectXform;
    if (vr180) {
      dTheta = 180.0f / (imageWidth/2);
      rectXform = new Vector4(0.0f, 0.0f, 1.0f / eyeImageWidth, 0.5f);
    }
    else {
      dTheta = 360.0f / imageWidth;
      rectXform = new Vector4(0.0f, 0.0f, 1.0f / eyeImageWidth, 0.25f);
    }
    int renderCount = 0;
    renderCamera.targetTexture = cameraTexture;

    Quaternion rotation = Quaternion.Euler(0, node.rotation.eulerAngles.y, 0);
    Vector3 odsCameraPos = renderCamera.transform.position;
    Vector4 shaderEyeOffset = new Vector4(0, 0, 0, 1);

    Shader.EnableKeyword("ODS_RENDER");
    int rangeEnd;
    if (vr180) {
        rangeEnd = imageWidth / 2 + bloomRadius;
    }
    else {
        rangeEnd = imageWidth + bloomRadius;
    }

    float angleOffset = -180.0f;
    if (vr180) { //the range is -PI/2 to +PI/2 when rendering in vr180 mode.
      angleOffset += 90.0f;
    }

    for (int eye = 0; eye < 2; ++eye) {
      float eyeOffset = (eye == (int)Eye.Left ? -0.5f : 0.5f) * ipd;

      for (int hemisphere = 0; hemisphere < 2; ++hemisphere) {
        float phi = (hemisphere == (int)Hemisphere.Top ? 45.0f : -45.0f);
        if (vr180) {
          rectXform.y = 0.5f * (hemisphere);
        }
        else {
          rectXform.y = 0.25f * (2 * eye + hemisphere);
        }
        renderCamera.transform.localPosition = new Vector3(eyeOffset, 0, 0);

        for (int i = -bloomRadius; i < rangeEnd; ++i) {
          float theta = i * dTheta + angleOffset;  // traverse angles from -Pi to Pi

          node.rotation = rotation * Quaternion.Euler(0.0f, theta, 0.0f);
          renderCamera.transform.localRotation = Quaternion.Euler(phi, 0.0f, 0.0f);

          if (CollapseIpd) {
            shaderEyeOffset = new Vector4(
              renderCamera.transform.position.x - node.transform.position.x,
              renderCamera.transform.position.y - node.transform.position.y,
              renderCamera.transform.position.z - node.transform.position.z,
              1);
          }

          Shader.SetGlobalVector("ODS_EyeOffset", shaderEyeOffset);
          Shader.SetGlobalVector("ODS_CameraPos", odsCameraPos);

          RenderTexture oldActiveTexture = RenderTexture.active;
          RenderTexture.active = renderCamera.targetTexture;
          GL.Clear(true, true, Color.black);
          RenderTexture.active = oldActiveTexture;

          renderCamera.Render();
          renderCount++;
          if (renderCount >= MaxRenders) {
            yield return new WaitForEndOfFrame();
            renderCount = 0;
          }

          if (vr180 && eye == (int)Eye.Right) { //Right Eye goes to the right side when doing SBS
            rectXform.x = (float)(i + imageWidth/2 + bloomRadius*3) / eyeImageWidth;
          }
          else {
            rectXform.x = (float)(i + bloomRadius) / eyeImageWidth;
          }
          warp.SetVector("_Rect", rectXform);
          Graphics.Blit(cameraTexture, output, warp);
        } // for each angular slice
      } // for each elevation
    } // for each eye image
    Shader.DisableKeyword("ODS_RENDER");
  }
} // class OdsSlice

} // namespace ODS
