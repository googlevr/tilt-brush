//**NOTE** Using Coroutines seems to increase the cost of Stereo Cubemap rendering a bit - 
//but it is still just over 1ms on a Quadro K600 in the Pedastal scene (vs 0.7 - 0.8ms)
using UnityEngine;
using System.Collections;
using Debug = UnityEngine.Debug;

namespace ODS {

// ODS Stereo Cubemap Renderer
// Renders 360 degree "ODS" Equirectangular images by first rendering two Cubemaps and 
// using them as input into a final conversion step.
// Stereo rendering is accomplished by correctly offseting the vertex positions while
// rendering each Cubemap.
// This requires 6 renders per cubemap plus 1 post processing pass to apply the final
// conversion for a total of 12 scene renders + 1 blit.
public class OdsStereoCubemap : OdsRenderer {
  // Sample using mipmaps, but slighly bias to be more crisp.
  const float c_cubemapMipBias = -.5f;

  public Material cubeToODS_material = null;
  private RenderTexture cubemapLt = null;
  private RenderTexture cubemapRt = null;

  public override void Release() {
    if (cubemapLt != null) {  cubemapLt.Release(); }
    if (cubemapRt != null) {  cubemapRt.Release(); }
    cubemapLt = null;
    cubemapRt = null;
  }

  public override void SetupTextures(RenderTextureFormat format) {
    int cubemapWidth = imageWidth;

    Release();
    cubemapLt = new RenderTexture(cubemapWidth, cubemapWidth, 24, format);
    cubemapRt = new RenderTexture(cubemapWidth, cubemapWidth, 24, format);
    cubemapLt.dimension = UnityEngine.Rendering.TextureDimension.Cube;
    cubemapRt.dimension = UnityEngine.Rendering.TextureDimension.Cube;
    cubemapLt.hideFlags = HideFlags.HideAndDontSave;
    cubemapRt.hideFlags = HideFlags.HideAndDontSave;
    cubemapLt.useMipMap = true;
    cubemapRt.useMipMap = true;
    cubemapLt.antiAliasing = 1;
    cubemapRt.antiAliasing = 1;

    if (cubemapLt == null || cubemapRt == null) {
      Debug.LogError("HybridCamera - failed to create cubemaps.");
    }
  }

  public override IEnumerator Render(Camera renderCamera, Transform node, RenderTexture output, 
    float ipd, bool CollapseIpd, int MaxRenders)
  {
    if (cubeToODS_material == null) {
      cubeToODS_material = new Material(Shader.Find("Hidden/cubemapToODS"));
    }

    //We need to modify the global shader parameters in order to handle ODS...
    Shader.EnableKeyword("ODS_RENDER_CM");

    int renderCount = 0;
    float[] ipdScale = { 0.5f, -0.5f };
    RenderTexture[] outputs = { cubemapLt, cubemapRt };
    Vector4 shaderEyeOffset = new Vector4(0, 0, 0, 0);

    for (int eye = 0; eye < 2; eye++) {
      // Explicitly clear output texture.
      var oldActiveTexture = RenderTexture.active;
      RenderTexture.active = outputs[eye];
      GL.Clear(true, true, Color.black);
      RenderTexture.active = oldActiveTexture;

      shaderEyeOffset.x = ipdScale[eye] * ipd;
      Shader.SetGlobalVector("ODS_EyeOffset", shaderEyeOffset);

      for (int face = 0; face < 6; face++, renderCount++) {
        renderToEye(face, renderCamera, outputs[eye]);

        if (renderCount >= MaxRenders) {
          yield return new WaitForEndOfFrame();
          renderCount = 0;
        }
      }
    }

    Shader.DisableKeyword("ODS_RENDER_CM");

    //Use the Stereo Cubemaps to generate the final ODS image.
    //Note the the cubemap is rendered from the same orientation but the direction vectors are 
    //rotated to match the camera orientation when generating the final ODS stereo image.
    convertCubemapToODS(output, renderCamera.transform.rotation);
  }

  private void renderToEye(int face, Camera camera, RenderTexture cubemap)
  {
    //render a single face of the cubemap, 
    int faceMask = 1 << face;
    if (!camera.RenderToCubemap(cubemap, faceMask)) {
      Debug.LogError("OdsRenderer - Render To cubemap failed!");
    }
  }

  private void convertCubemapToODS(RenderTexture odsImage, Quaternion orientation)
  {
    //calculate the scale and offset to account for border used for bloom
    Vector2 scaleOffset;
    scaleOffset.x = 1.0f / (1.0f - 2.0f * bloomRadius / (float)eyeImageWidth);
    scaleOffset.y = -(float)bloomRadius / (float)eyeImageWidth * scaleOffset.x;

    if (vr180) {
      cubeToODS_material.EnableKeyword("VR_180");
    }
    else {
      cubeToODS_material.DisableKeyword("VR_180");
    }

    //Do the cubemap to ODS conversion on the GPU and download the result to the CPU for encoding.
    odsImage.DiscardContents();

    var old = RenderTexture.active;
    RenderTexture.active = odsImage;
    GL.Clear(true, true, Color.black);
    RenderTexture.active = old;

    cubemapLt.mipMapBias = c_cubemapMipBias;
    cubemapRt.mipMapBias = c_cubemapMipBias;
    cubemapLt.wrapMode = TextureWrapMode.Clamp;
    cubemapRt.wrapMode = TextureWrapMode.Clamp;

    //ignore pitch and roll and make sure the orientation matches the slice implementation.
    Vector3 euler = orientation.eulerAngles;
    Quaternion cubeAdj = Quaternion.Euler(0.0f, -180.0f, 0.0f);
    Quaternion projectedOrientation = Quaternion.Euler(0, euler.y, 0) * cubeAdj;

    Vector4 orient = new Vector4(projectedOrientation.x, projectedOrientation.y, 
                                 projectedOrientation.z, projectedOrientation.w);

    cubeToODS_material.SetTexture("_LeftTex", cubemapRt);
    cubeToODS_material.SetVector("_orientation", orient);
    cubeToODS_material.SetVector("_scaleOffset", scaleOffset);
    Graphics.Blit(cubemapLt, odsImage, cubeToODS_material, 0);
  }
} // class OdsStereoCubemap

} // namespace ODS
