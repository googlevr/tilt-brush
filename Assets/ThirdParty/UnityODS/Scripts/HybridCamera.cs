//Uncomment ENABLE_TIMING in order to report render times for ODS rendering.
//#define ENABLE_TIMING

using UnityEngine;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Debug = UnityEngine.Debug;
using TiltBrush;

namespace ODS {

public class HybridCamera : MonoBehaviour {
  [Serializable]
  public enum OdsRendererType {
    Slice = 0,
    StereoCubemap,
    Count
  };

  const int MaxRenders = 1000;
  const int MaxImageWidth = 6000;
                       
  public float interPupillaryDistance = 0.05f;
  public int imageWidth = 4096;

  private int lastImageWidth = 0;
  private int imageHeight;
  private int eyeImageWidth;
  private bool vr180 = false;
  private bool lastvr180 = false;
  private OdsRendererType rendererType;
  private OdsRendererType lastRendererType;

  public float particleScaleFactor = 100.0f;
  public int   bloomRadius = 16;
  private int  lastBloomRadius = 0;
        
  public bool HDR = false;
  public bool opaqueBackground = true;

  public RenderTexture stitched   = null;
  public RenderTexture bloomed    = null;
  public RenderTexture finalImage = null;
  public Texture2D returnImage    = null;

  public string outputFolder = null;
  public string basename = null;

  private int frameCount = 0;
  private bool isRendering = false;

  private OdsRenderer odsRenderer = null;

#if ENABLE_TIMING
  public System.Diagnostics.Stopwatch m_timer = new System.Diagnostics.Stopwatch();
#endif

  public bool IsRendering { get { return isRendering; } }
  public bool CollapseIpd { get; set; }
  public int FrameCount { get { return frameCount; } }

  public Texture2D Image {
    get { return returnImage; }
  }

  public RenderTexture FinalImage {
    get { return finalImage; }
  }

  public void SetOdsRendererType(OdsRendererType type) {
  //Currently StereoCubemap ODS rendering is only supported in the editor or when experimental is 
  //enabled. If/When StereoCubemap ODS rendering is fully supported, removed this #if/#else/#endif.
#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (!isExperimental()) {
      type = OdsRendererType.StereoCubemap;
    }
#else
    type = OdsRendererType.StereoCubemap;
#endif

    if (type != rendererType) {
      Debug.Assert( type < OdsRendererType.Count );
      if (odsRenderer != null) { 
        odsRenderer.Release();
      }
      
      if (type == OdsRendererType.Slice) {
        Debug.Log("ODS Mode: Slice");
        odsRenderer = new OdsSlice();
      }
      else {
        Debug.Log("ODS Mode: Stereo Cubemap");
        odsRenderer = new OdsStereoCubemap();
      }
      odsRenderer.SetVr180(vr180);
    }
    rendererType = type;
  }

  public void EnableVr180(bool enable) {
    vr180 = enable;
    odsRenderer.SetVr180(enable);
  }

  private void SetupTextures() {
    imageHeight    = imageWidth;

    int bloomPadding;
    if (vr180) {
      bloomPadding = 4 * bloomRadius;  //extra padding in the middle
    }
    else {
      bloomPadding = 2 * bloomRadius;
    }
    eyeImageWidth  = imageWidth + bloomPadding;
            
    RenderTextureFormat format = HDR ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.Default;

    stitched = new RenderTexture(eyeImageWidth, imageHeight, 0, format);
    stitched.antiAliasing = 1;

    bloomed = new RenderTexture(stitched.width, stitched.height, 0, format);
    bloomed.antiAliasing = 1;
            
    finalImage  = new RenderTexture( imageWidth, imageHeight, 0, RenderTextureFormat.ARGB32 );
    returnImage = new Texture2D( finalImage.width, finalImage.height, TextureFormat.RGB24, false );

    odsRenderer.SetWidth(imageWidth, eyeImageWidth, bloomRadius);
    odsRenderer.SetupTextures(format);
  }

  public void Awake() {
    CollapseIpd = true;
    odsRenderer = new OdsSlice();
    odsRenderer.SetVr180(vr180);
    rendererType = OdsRendererType.Slice;
    lastRendererType = rendererType;

    Debug.Log("Init ODS Mode: " + rendererType.ToString());

    if ( outputFolder == null ) {
      outputFolder = System.Environment.GetFolderPath(
        System.Environment.SpecialFolder.DesktopDirectory) + "/ODS";
    }
  }

  public IEnumerator Render(Transform node) {
    if ( imageWidth != lastImageWidth || bloomRadius  != lastBloomRadius ||
        lastRendererType != rendererType || lastvr180 != vr180) {
      // Round image width to a mutiple of four to keep symmetry with the image height.
      imageWidth = Math.Min( ((imageWidth + 3) / 4) * 4, MaxImageWidth );

      SetupTextures();

      lastImageWidth   = imageWidth;
      lastBloomRadius  = bloomRadius;
      lastRendererType = rendererType;
      lastvr180        = vr180;
    }

    if ( outputFolder != null  && !Directory.Exists( outputFolder ) ) {
      Directory.CreateDirectory( outputFolder );
    }

    GameObject renderCameraObject = new GameObject();
    renderCameraObject.transform.parent = node.transform;
    Camera renderCamera = renderCameraObject.AddComponent<Camera>() as Camera;

    Camera parentCamera = GetComponent<Camera>();
    if ( Camera.main == null ) { // this may occur if the camera is disabled
      parentCamera = GetComponent<Camera>();
      Debug.Assert( parentCamera != null );
    }

    renderCamera.CopyFrom( parentCamera );
    renderCamera.cullingMask = parentCamera.cullingMask;
    renderCamera.name = "Hybrid ODS Camera";
    renderCamera.fieldOfView = 90.0f;

    if ( opaqueBackground ) {
      // TBD - Specify full alpha for exported image
      Color background = parentCamera.backgroundColor;
      background.a = 1.0f;
      renderCamera.backgroundColor = background;
    }

    ParticleSystemRenderer[] renderers = FindObjectsOfType<ParticleSystemRenderer>();
    for ( int i = 0; i < renderers.Length; ++i ) {
      renderers[i].maxParticleSize *= particleScaleFactor;
    }

    // Remove pitch/elevation and roll/bank, but preserve yaw/heading.
    Quaternion origRotation = node.rotation;
    renderCamera.rect = new Rect(0, 0, 1.0f, 1.0f);
    renderCamera.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f);
    renderCamera.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);

    float scale = renderCamera.transform.lossyScale.x;
    renderCamera.nearClipPlane = parentCamera.nearClipPlane * scale;
    renderCamera.farClipPlane = parentCamera.farClipPlane * scale;

#if ENABLE_TIMING
    timerStart();
#endif

    isRendering = true;
    //This suspends the execution of this function while running the odsRenderer.Render() function 
    //as a coroutine and will then resume execution when it is done.
    yield return StartCoroutine( 
      odsRenderer.Render(renderCamera, node, stitched, interPupillaryDistance * scale, CollapseIpd,
                         MaxRenders)
    );
    isRendering = false;

#if ENABLE_TIMING
    double deltaMs = timerStop();
    print("OdsRender - Render time: " + deltaMs.ToString() + "ms");
#endif

    node.rotation = origRotation;
    Destroy( renderCameraObject );

    for ( int i = 0; i < renderers.Length; ++i ) {
      renderers[i].maxParticleSize /= particleScaleFactor;
    }

    var oldActiveTexture = RenderTexture.active;
    RenderTexture.active = finalImage;
    GL.Clear(true, true, Color.black);
    RenderTexture.active = oldActiveTexture;

    bool useBloomedImage = false;
    MonoBehaviour[] behaviours = gameObject.GetComponents<MonoBehaviour>();
    foreach (MonoBehaviour b in behaviours) {
      MethodInfo m = b.GetType().GetMethod("OnRenderImage");
      if (m != null && m.IsPublic && b.enabled) {
        //Apply the bloom and composite.
        if (vr180) {
          SbsBloomAndComposite(stitched, finalImage, b, m);
        }
        else {
          StackedBloomAndComposite(stitched, finalImage, b, m);
        }
        useBloomedImage = true;
        break;
      }
    }

    //When bloom is not enabled, we still have to composite.
    if (!useBloomedImage) {
      if (vr180) {
        SbsComposite(stitched, finalImage);
      }
      else {
        StackedComposite(stitched, finalImage);
      }
    }

    oldActiveTexture = RenderTexture.active;
    RenderTexture.active = finalImage;
    returnImage.ReadPixels(new Rect(0, 0, finalImage.width, finalImage.height), 0, 0);
    RenderTexture.active = oldActiveTexture;

    Graphics.Blit(finalImage, (RenderTexture)null);

    byte[] image = returnImage.EncodeToPNG();

    string file = String.Format(basename + "_{0:d6}.png", frameCount);
    string path = Path.Combine(outputFolder, file);

#if MULTI_THREADED
  Thread imageWriter = new Thread(() => {
    File.WriteAllBytes(path, image);
#if LOG_IMAGE_WRITES
    Debug.Log( "Wrote image " + path );
#endif
  });
  imageWriter.IsBackground = true;
  imageWriter.Start();
#else
    File.WriteAllBytes( path, image );
#if LOG_IMAGE_WRITES
  Debug.Log( "Wrote image " + path );
#endif
#endif

    frameCount++;
  }  // Render method

  //This render function can be called from the Editor to make testing easier.
  //This has to be called in "Play" mode - Coroutines simply don't work in normal Editor mode.
  public void RenderAll(Transform node) {
    StartCoroutine( Render(node) );
  }
#if ENABLE_TIMING
  private void timerStart()
  {
    m_timer.Start();
  }
         
  private double timerStop()
  {
    m_timer.Stop();
    long nanosecPerTick = (1000L * 1000L * 1000L) / System.Diagnostics.Stopwatch.Frequency;
    long ns = m_timer.ElapsedTicks / nanosecPerTick;
    m_timer.Reset();

    return (double)ns / 1000.0;
  }
#endif

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
  //This is a little wonky but used so ODS captures can be tested in test scenes by adding a 
  //Hybrid Camera Editor script. Since these scenes may not have a proper Tiltbrush App, we have
  //to handle the case where it is null.
  private bool isExperimental() {
    return !App.Instance || Config.IsExperimental;
  }
#endif

   //Composite into the final image, no bloom is applied.
  private void SbsComposite(RenderTexture src, RenderTexture dst)
  {
    var oldActiveTexture = RenderTexture.active;
    RenderTexture.active = dst;
    GL.PushMatrix();
    GL.LoadPixelMatrix(0, dst.width, dst.height, 0);

    float offset = (float)bloomRadius / (float)src.width;
    float eyeWidth = 0.5f - 2.0f * offset;
    Graphics.DrawTexture(new Rect(0, 0, dst.width / 2, dst.height),
      src, new Rect(offset, 0.0f, eyeWidth, 1.0f),
      0, 0, 0, 0);

    Graphics.DrawTexture(new Rect(dst.width / 2, 0, dst.width / 2, dst.height),
      src, new Rect(0.5f + offset, 0.0f, eyeWidth, 1.0f),
      0, 0, 0, 0);
    GL.PopMatrix();
    RenderTexture.active = oldActiveTexture;
  }

  //Composite into the final image, no bloom is applied.
  private void StackedComposite(RenderTexture src, RenderTexture dst)
  {
    var oldActiveTexture = RenderTexture.active;
    RenderTexture.active = dst;
    GL.PushMatrix();
    GL.LoadPixelMatrix(0, dst.width, dst.height, 0);
    float offset = (float)bloomRadius / (float)src.width;

    Graphics.DrawTexture(new Rect(0, 0, dst.width, dst.height),
      src, new Rect(offset, 0.0f, 1.0f - 2*offset, 1.0f),
      0, 0, 0, 0);
    GL.PopMatrix();
    RenderTexture.active = oldActiveTexture;
  }

  //Apply bloom and composite into the final image.
  private void StackedBloomAndComposite(RenderTexture src, RenderTexture dst,
                                        MonoBehaviour behavior, MethodInfo renderImageMethod)
  {
    RenderTextureFormat fmt = src.format;
    RenderTexture rt0 = RenderTexture.GetTemporary(src.width, src.height, 0, fmt);
    float eyeWidth = (float)rt0.width;

    renderImageMethod.Invoke(behavior, new object[] { src, rt0 });

    var oldActiveTexture = RenderTexture.active;
    RenderTexture.active = dst;
    GL.PushMatrix();
    GL.LoadPixelMatrix(0, dst.width, dst.height, 0);
    float offset = (float)bloomRadius / eyeWidth;

    Graphics.DrawTexture(new Rect(0, 0, dst.width, dst.height),
      rt0, new Rect(offset, 0.0f, 1.0f - 2 * offset, 1.0f),
      0, 0, 0, 0);
    GL.PopMatrix();

    RenderTexture.ReleaseTemporary(rt0);
    RenderTexture.active = oldActiveTexture;
  }

  //Apply bloom and composite into the final image.
  private void SbsBloomAndComposite(RenderTexture src, RenderTexture dst,
                                    MonoBehaviour behavior, MethodInfo renderImageMethod)
  {
    //split the src into halves.
    int srcWidth = src.width;
    int srcHeight = src.height;
    RenderTextureFormat fmt = src.format;

    int tmpWidth = srcWidth / 2;
    RenderTexture rt0 = RenderTexture.GetTemporary(tmpWidth, srcHeight, 0, fmt);
    RenderTexture rt1 = RenderTexture.GetTemporary(tmpWidth, srcHeight, 0, fmt);
    RenderTexture rt2 = RenderTexture.GetTemporary(tmpWidth, srcHeight, 0, fmt);
    RenderTexture rt3 = RenderTexture.GetTemporary(tmpWidth, srcHeight, 0, fmt);
    rt0.Create();
    rt1.Create();
    rt2.Create();
    rt3.Create();
    dst.Create();

    //Copy each eye into seperate buffers so that bloom can be applied without leaking across eyes.
    int srcElement = 0, srcMipLevel = 0;
    int dstElement = 0, dstMipLevel = 0;
    int srcX = 0, srcY = 0;
    int dstX = 0, dstY = 0;
    //Copy the left half of the image into its own buffer.
    Graphics.CopyTexture(src, srcElement, srcMipLevel, srcX, srcY, tmpWidth, srcHeight, rt0,
      dstElement, dstMipLevel, dstX, dstY);

    //Copy the right half of the image into its own buffer.
    srcX = tmpWidth;
    Graphics.CopyTexture(src, srcElement, srcMipLevel, srcX, srcY, tmpWidth, srcHeight, rt1,
      dstElement, dstMipLevel, dstX, dstY);

    renderImageMethod.Invoke(behavior, new object[] { rt0, rt2 });
    renderImageMethod.Invoke(behavior, new object[] { rt1, rt3 });

    //CopyTexture doesn't work when changing formats, so DrawTexture must be used instead.
    //Merge both eye textures once bloom has been applied back into a single image.
    var oldActiveTexture = RenderTexture.active;
    RenderTexture.active = dst;
    GL.PushMatrix();
    GL.LoadPixelMatrix(0, dst.width, dst.height, 0);

    float offset = (float)bloomRadius / (float)tmpWidth;
    float eyeWidth = 1.0f - 2.0f * offset;
    Graphics.DrawTexture(new Rect(0, 0, dst.width / 2, dst.height),
      rt2, new Rect(offset, 0.0f, eyeWidth, 1.0f),
      0, 0, 0, 0);

    Graphics.DrawTexture(new Rect(dst.width / 2, 0, dst.width / 2, dst.height),
      rt3, new Rect(offset, 0.0f, eyeWidth, 1.0f),
      0, 0, 0, 0);
    GL.PopMatrix();

    RenderTexture.ReleaseTemporary(rt0);
    RenderTexture.ReleaseTemporary(rt1);
    RenderTexture.ReleaseTemporary(rt2);
    RenderTexture.ReleaseTemporary(rt3);
    RenderTexture.active = oldActiveTexture;
  }
} // class ODSCamera

} // namespace ODS
