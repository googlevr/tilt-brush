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
using System.Collections.Generic;

namespace TiltBrush {

// A class for managing GPU intersection requests.
//
// This class accepts intersection requests, processes them on the GPU, and provides access to those
// results once they are ready. The caller is responsible for managing multiple concurrent requests
// and ensuring those requests do not degrade the user experience.
//
// Use: Request an intersection, hold the FutureResult until IsReady is true, then process the
// results.
//
// Future work: add a mechanism to pipeline requests and ensure never more than one texture is read
// back from the GPU on any given frame.
//
public class GpuIntersector : MonoBehaviour {
  public delegate bool ResultReader();

  public struct BatchResult {
    // The widget in which the triangle exists.
    // This is mutually exclusive with subset.
    public GrabWidget widget;
    // The batch in which the triangle exists.
    // This is mutually exclusive with widget.
    public BatchSubset subset;
  }

  public struct ModelResult {
    // The widget in which the intersected model exists.
    public GrabWidget widget;
  }

  // The size of the square texture used to read back results from the GPU.
  // A value of 16 here indicates that 16 x 16 pixel texture and buffers will be used.
  private const int kTexSize = 16;

  // Batch Id is 16 bits long. Given that our batches have ~10,000 triangles each, this gives us
  // enough space for ~65,000,000,000 triangles in a scene. That should be enough.
  static private ushort sm_BatchIdCounter = 1;

  // It would be possible to eventually wrap around the batch ids, and in the case where the
  // batch id is zero, one triangle on that stroke will not be able to be selected, but otherwise
  // it is fine.
  public static ushort GetNextBatchId() {
    return sm_BatchIdCounter++;
  }

  [SerializeField] private Shader m_IntersectionShader;
  [SerializeField] private Shader m_DownsampleShader;
  [SerializeField] private ComputeShader m_ComputeCopyShader;

  private Material m_DownsampleMat;
  private RenderTexture m_HighResTex;
  private Camera m_IntersectionCamera;
  private List<ResultReader> m_activeResults = new List<ResultReader>();

  private void Start() {
    App.VrSdk.GetVrCamera().gameObject
        .GetComponent<RenderWrapper>().ReadBackTextures += ReadResultsDispatchCallback;

    // Compute kernel (blit tex -> buffer) setup.
    int pixelCount = kTexSize * kTexSize;
    int kPixelBytes = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Color));
    int kernel = m_ComputeCopyShader.FindKernel("CopyToBuffer");
    FutureResult.sm_ReadbackBuffer = new ComputeBuffer(pixelCount, kPixelBytes, ComputeBufferType.Default);
    FutureResult.sm_ReadbackBufferStorage = new uint[pixelCount];
    FutureResult.sm_ReadbackBuffer.SetData(FutureResult.sm_ReadbackBufferStorage);
    FutureResult.sm_ComputeCopyShader = m_ComputeCopyShader;
    FutureResult.sm_CopyKernel = kernel;
    FutureResult.sm_ComputeCopyShader = m_ComputeCopyShader;
    m_ComputeCopyShader.SetBuffer(kernel, "OutputBuffer", FutureResult.sm_ReadbackBuffer);

    // Shutdown hook to fix a warning in-editor.
    App.Instance.AppExit += OnGuaranteedAppQuit;
  }

  private void OnGuaranteedAppQuit() {
    // Technically we never need to do this, since it only happens before App quit, however it's
    // needed to keep the in-editor state clean; without this warnings will be spewed.
    FutureResult.sm_ReadbackBuffer.Dispose();
  }

  // This method only exists to avoid adding and removing callbacks for every intersection request,
  // which generates garbage. In practice this saves ~0.7k of garbage for every request.
  private void ReadResultsDispatchCallback() {
    for (int i = 0; i < m_activeResults.Count; i++) {
      if (m_activeResults[i]()) {
        m_activeResults.RemoveAt(i);
        i--;
      }
    }
  }

  // -------------------------------------------------------------------------------------------- //
  // Intersection Public API
  // -------------------------------------------------------------------------------------------- //

  // Detects intersections, but does not return the exact batches or triangles which were
  // intersected.
  public FutureBatchResult RequestBatchIntersection(Vector3 vDetectionCenter_GS,
                                                     float radius_GS,
                                                     int renderCullingMask) {
    if (!enabled) {
      return null;
    }
    UnityEngine.Profiling.Profiler.BeginSample("RenderIntersection");
    RenderTexture tex = RenderIntersection(vDetectionCenter_GS, radius_GS, renderCullingMask);
    UnityEngine.Profiling.Profiler.EndSample();
    ResultReader resultReader;
    var ret = new FutureBatchResult(tex, null, maxResults: 1, resultReader: out resultReader);
    m_activeResults.Add(resultReader);
    return ret;
  }

  // Detects intersections and outputs the exact batches and triangles intersected into the given
  // output buffer.
  public FutureBatchResult RequestBatchIntersections(Vector3 vDetectionCenter_GS,
                                                     float radius_GS,
                                                     List<BatchResult> resultsOut,
                                                     byte maxResults,
                                                     int renderCullingMask) {
    if (!enabled) {
      return null;
    }
    UnityEngine.Profiling.Profiler.BeginSample("RenderIntersection");
    RenderTexture tex = RenderIntersection(vDetectionCenter_GS, radius_GS, renderCullingMask);
    UnityEngine.Profiling.Profiler.EndSample();
    ResultReader resultReader;
    var ret = new FutureBatchResult(tex, resultsOut, maxResults, out resultReader);
    m_activeResults.Add(resultReader);
    return ret;
  }

  // Detects intersections, but does not return the exact models or triangles which were
  // intersected.
  public FutureModelResult RequestModelIntersections(Vector3 vDetectionCenter_GS,
                                                     float radius_GS,
                                                     List<ModelResult> resultsOut,
                                                     byte maxResults,
                                                     int renderCullingMask) {
    if (!enabled) {
      return null;
    }
    UnityEngine.Profiling.Profiler.BeginSample("RenderIntersection");
    RenderTexture tex = RenderIntersection(vDetectionCenter_GS, radius_GS, renderCullingMask);
    UnityEngine.Profiling.Profiler.EndSample();
    ResultReader resultReader;
    var ret = new FutureModelResult(tex, resultsOut, maxResults, out resultReader);
    m_activeResults.Add(resultReader);
    return ret;
  }

  // TODO: Implement RequestModelIntersections() that returns the exact model and triangles
  // intersected.

  // -------------------------------------------------------------------------------------------- //
  // Private lifecycle methods
  // -------------------------------------------------------------------------------------------- //
  void Awake() {
    m_IntersectionCamera = GetComponent<Camera>();

    // WARNING: All textures must be LINEAR and sample filter modes should be POINT, to avoid
    //          interpolating or remapping the output batch and triangle IDs.
    m_DownsampleMat = new Material(m_DownsampleShader);

    // Note: This was previously 512, but I could not detect any difference between
    // 512 and 64. The 64x64 texture uses 1/64th of the memory bandwidth, which is important on
    // mobile.
    int size = 64;
    m_HighResTex = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32,
                                                    RenderTextureReadWrite.Linear);
    m_HighResTex.filterMode = FilterMode.Point;
    m_HighResTex.Create();
  }

  void OnDisable() {
    m_HighResTex.Release();
  }

  // -------------------------------------------------------------------------------------------- //
  // Private Internals
  // -------------------------------------------------------------------------------------------- //

  // Performs stroke intersection on the GPU, but does not immediately read back the result.
  private RenderTexture RenderIntersection(
      Vector3 vDetectionCenter_GS, float radius_GS, int renderCullingMask) {

    // Future work: we could use an even smaller texture here, when we know the user doesn't care
    // about the exact triangle intersections.
    //
    // WARNING: All textures must be LINEAR and sample filter modes should be POINT, to avoid
    //          interpolating or remapping the output batch and triangle IDs.
    RenderTexture curRenderSmall = RenderTexture.GetTemporary(16, 16, 0,
        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    curRenderSmall.filterMode = FilterMode.Point;

    // Unfortunately, RenderWithShader() has no RenderWithMaterial() equivalent, so we must use
    // global uniforms since that is the mechanism used to launch the render.
    Shader.SetGlobalVector("vSphereCenter", vDetectionCenter_GS);
    Shader.SetGlobalFloat("fSphereRad", radius_GS);

    // Clear to black, which is an invalid result.
    m_IntersectionCamera.backgroundColor = Color.clear;
    m_IntersectionCamera.clearFlags = CameraClearFlags.Color;

    // Assign the texture to capture the output.
    m_IntersectionCamera.targetTexture = m_HighResTex;

    // Frame the camera to the detection sphere.
    m_IntersectionCamera.transform.position = vDetectionCenter_GS;
    m_IntersectionCamera.nearClipPlane = -radius_GS;
    m_IntersectionCamera.farClipPlane = radius_GS;
    m_IntersectionCamera.orthographicSize = radius_GS;

    // This is a heuristic to generally orient the camera in the same way as the user's head, such
    // that if any object is visible to the user, it will also be visible for intersection. A better
    // way to do this is to implement conservative rasterization, though that has additional
    // challenges
    m_IntersectionCamera.transform.rotation = ViewpointScript.Head.rotation;

    // Intersect with strokes just the specified layer.
    m_IntersectionCamera.cullingMask = renderCullingMask;

    // Render all gemoetry tagged as "intersection" with the intersection shader.
    m_IntersectionCamera.RenderWithShader(m_IntersectionShader, string.Empty);

    // Downsample to a tiny texture using a point filter, since we don't have an ordering to
    // selected strokes.
    Graphics.Blit(m_HighResTex, curRenderSmall, m_DownsampleMat);

    // Ensure we mark the contents of the high res texture as discardable to ensure that
    // Unity isn't tempted to resolve in unnecessarily.
    m_HighResTex.DiscardContents();

    return curRenderSmall;
  }

  // ------------------------------------------------------------------------------------------ //
  // FutureResult Classes
  // ------------------------------------------------------------------------------------------ //

  // A handle which clients hold while the intersection request is being processed.
  public abstract class FutureResult {
    protected RenderTexture m_ResultTex;
    protected int m_ResultCount;
    protected byte m_MaxResults;
    protected int m_StartFrame;

    // Not thread safe, but we can never have multiple threads reading from the GPU anyway.
    internal static int sm_CopyKernel;
    internal static uint[] sm_ReadbackBufferStorage;
    internal static ComputeBuffer sm_ReadbackBuffer;
    internal static ComputeShader sm_ComputeCopyShader;

    // A pool of size one
    internal static HashSet<object> sm_pool = null;

    protected static HashSet<object> AllocateHashSet() {
      var ret = System.Threading.Interlocked.Exchange(ref sm_pool, null);
      if (ret == null) {
        ret = new HashSet<object>(new ReferenceComparer<object>());
      }
      return ret;
    }

    protected static void DeallocateHashSet(HashSet<object> obj) {
      obj.Clear();
      sm_pool = obj;
    }


    public FutureResult(RenderTexture texture, byte maxResults, out ResultReader resultReader) {
      m_ResultTex = texture;
      m_MaxResults = maxResults;
      m_ResultCount = -1;
      m_StartFrame = Time.frameCount;

      int pixelCount = m_ResultTex.width * m_ResultTex.height;
      Debug.Assert(sm_ReadbackBufferStorage.Length == pixelCount);

      // Schedule the texture to be read back at an optimal point that causes the least GPU
      // disruption. In particulary, we don't want to read the texture while the GPU is full, which
      // requires the CPU to wait while the GPU drains.
      resultReader = ReadResultsCallback;
    }

    // Returns true when results are ready to be queried, indicating that it is safe to call
    // GetResults() and HasAnyIntersections().
    public bool IsReady {
      get {
        return m_ResultCount >= 0;
      }
    }

    public int StartFrame {
      get {
        return m_StartFrame;
      }
    }

    // Returns true if any intersections occured.
    // This method may not be called until IsReady == true.
    public bool HasAnyIntersections() {
      if (!IsReady) {
        throw new System.Exception("This function may not be called while IsReady == false");
      }
      return m_ResultCount > 0;
    }

    private bool ReadResultsCallback() {
      // Add a one frame delay to avoid thrashing the GPU.
      if (m_StartFrame == Time.frameCount) {
        return false;
      }

      // Readback the actual results.
      UnityEngine.Profiling.Profiler.BeginSample("Intersection: ReadResults");
      OnReadResults();
      UnityEngine.Profiling.Profiler.EndSample();

      // returning true indicates the result has been read, removing this callback from the queue.
      return true;
    }

    //
    // Copy the texture back to the CPU.
    // WARNING: The array returned from this method is shared & not thread safe.
    //
    protected uint[] GetTextureColors() {
      UnityEngine.Profiling.Profiler.BeginSample("Intersection: Readback Texture");

      // Only set the InputTexture, since the OutputBuffer never changes.
      sm_ComputeCopyShader.SetTexture(sm_CopyKernel, "InputTexture", m_ResultTex);
      sm_ComputeCopyShader.Dispatch(sm_CopyKernel, 4, 4, 1);

      RenderTexture.ReleaseTemporary(m_ResultTex);
      m_ResultTex = null;

      // Read data without allocating new memory.
      sm_ReadbackBuffer.GetData(sm_ReadbackBufferStorage);
      uint[] resultColors = sm_ReadbackBufferStorage;

      UnityEngine.Profiling.Profiler.EndSample();

      return resultColors;
    }

    abstract protected void OnReadResults();
  }

  // ------------------------------------------------------------------------------------------ //
  // FutureBatchResult
  // ------------------------------------------------------------------------------------------ //

  // Handle specifically for returning stroke batch intersections.
  public class FutureBatchResult : FutureResult {
    private List<BatchResult> m_ResultList;

    // Sets up a FutureBatchResult to be read from texture with maxResults stored in results.
    // Note that when results is null or maxResults < 1, exact triangle intersections will not be
    // captured, as a performance optimization.
    // Pass:
    //   results - if non-null, will be cleared and refilled by the time IsReady=true
    public FutureBatchResult(RenderTexture texture, List<BatchResult> results, byte maxResults,
                             out ResultReader resultReader) :
        base(texture, maxResults, out resultReader) {
      m_ResultList = results;
    }

    // Returns the exact batches and triangles intersected.
    // This method may not be called until IsReady == true.
    public List<BatchResult> GetResults() {
      if (!IsReady) {
        throw new System.Exception("This function may not be called while IsReady == false");
      }
      if (m_ResultList == null) {
        throw new System.Exception("Results were not included in this request");
      }
      return m_ResultList;
    }

    // ------------------------------------------------------------------------------------------ //
    // Private Internals
    // ------------------------------------------------------------------------------------------ //

    override protected void OnReadResults() {
      // Mark results as ready.
      m_ResultCount = 0;
      if (m_ResultList != null) {
        m_ResultList.Clear();
      }

      uint[] resultColors = GetTextureColors();

      //
      // Process the color buffer into result objects, if requested.
      //
      UnityEngine.Profiling.Profiler.BeginSample("Intersection: Process Results");
      HashSet<object> seen = AllocateHashSet();

      uint c;

      for (int i = 0; i < resultColors.Length; i++) {
        if (m_ResultCount == m_MaxResults) {
          break;
        }

        c = resultColors[i];

        if (c > 0) {
          // Don't bother looking up the batch if the exact result set wasn't requested.
          if (m_ResultList == null) {
            m_ResultCount++;
            continue;
          }

          // TODO: the Color32 -> object lookup is deterministic and O(n), so 'seen'
          // should store the Color32, not the object.

          int triIndex = (int)(c & 0xffff) * 3;
          ushort batchId = (ushort)((c & 0xffff0000) >> 16);
          GrabWidget widget = null;
          Batch batch = null;
          BatchSubset subset = null;

          // See if this batch refers to a brush stroke.
          batch = App.ActiveCanvas.BatchManager.GetBatch(batchId);
          if (batch != null) {
            // TODO: move this into Batch, so can do binary search if necessary
            for (int j = 0; j < batch.m_Groups.Count; j++) {
              BatchSubset bs = batch.m_Groups[j];
              if (triIndex >= bs.m_iTriIndex && triIndex < bs.m_iTriIndex + bs.m_nTriIndex) {
                subset = bs;
                break;
              }
            }

            // A stroke may be deleted by the time this executes. This is due to the delay between
            // sending an intersection request and processing the results.
            if (subset == null) {
              // This actually happens in practice!
              // TODO: investigate if it's okay
              // Debug.LogWarningFormat(
              //     "Unexpected: Nonexistent subset for c = {0:x} {1:x}",
              //     (ushort)(c >> 16),
              //     (ushort)(c & 0xffff));
              continue;
            }

            if (!seen.Add(subset)) {
              continue;
            }
          } else {
            // Not a brush stroke?  See if this is a widget.
            widget = WidgetManager.m_Instance.GetBatch(batchId);

            // A widget may be deleted by the time this executes. This is due to the delay between
            // sending an intersection request and processing the results.
            if (widget == null) {
              continue;
            }

            if (!seen.Add(widget)) {
              continue;
            }
          }

          // A batch should never be null, but in the future that may change. This is possible due
          // to the delay between sending an intersection request and processing the results.
          if (batch == null && widget == null) {
            Debug.LogWarningFormat(
                "Unexpected: Null batch {0} and widget {1}",
                ReferenceEquals(batch, null), ReferenceEquals(widget, null));
            continue;
          }

          // These cannot both be valid.
          Debug.Assert(subset == null || widget == null);
          m_ResultList.Add(
            new BatchResult { widget = widget, subset = subset });
          m_ResultCount++;
        }
      }

      DeallocateHashSet(seen);
      UnityEngine.Profiling.Profiler.EndSample();
    }
  }

  // ------------------------------------------------------------------------------------------ //
  // FutureModelResult
  // ------------------------------------------------------------------------------------------ //

  // Handle specifically for returning model intersections.
  public class FutureModelResult : FutureResult {
    private List<ModelResult> m_ResultList;

    // Sets up a FutureModelResult to be read from texture with maxResults stored in results.
    // Note that when results is null or maxResults < 1, exact triangle intersections will not be
    // captured, as a performance optimization.
    public FutureModelResult(RenderTexture texture, List<ModelResult> results, byte maxResults,
                             out ResultReader resultReader) :
        base(texture, maxResults, out resultReader) {
      m_ResultList = results;
    }

    // This method may not be called until IsReady == true.
    // TODO: Implement triangle lookup in ReadResults() to populate this data correctly.
    public List<ModelResult> GetResults() {
      throw new System.NotImplementedException();
    }

    // ------------------------------------------------------------------------------------------ //
    // Private Internals
    // ------------------------------------------------------------------------------------------ //

    override protected void OnReadResults() {
      // Mark results as ready.
      m_ResultCount = 0;
      if (m_ResultList != null) {
        m_ResultList.Clear();
      }

      uint[] resultColors = GetTextureColors();

      //
      // Process the color buffer into result objects, if requested.
      //
      UnityEngine.Profiling.Profiler.BeginSample("Intersection: Process Model Results");
      HashSet<object> seen = AllocateHashSet();
      uint c;

      for (int i = 0; i < resultColors.Length; i++) {
        if (m_ResultCount == m_MaxResults) {
          break;
        }

        c = resultColors[i];

        if (c > 0) {
          // Don't bother looking up the batch if the exact result set wasn't requested.
          if (m_ResultList == null) {
            m_ResultCount++;
            continue;
          }

          ushort batchId = (ushort)((c & 0xffff0000) >> 16);
          GrabWidget widget = WidgetManager.m_Instance.GetBatch(batchId);
          if (widget == null) {
            continue;
          }

          if (!seen.Add(widget)) {
            continue;
          }

          m_ResultList.Add(new ModelResult { widget = widget });
          m_ResultCount++;
        }
      }

      DeallocateHashSet(seen);
      UnityEngine.Profiling.Profiler.EndSample();
    }
  }

}
} // namespace TiltBrush
