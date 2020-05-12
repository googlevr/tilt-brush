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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TiltBrush {

  /// Contains helpers that allow brush geometry to be generated at edit-time.
  /// Also serves as a good record of the global state touched by our geometry
  /// generation process (and therefore as a record of places to fix if we want
  /// to run geometry generation on some other thread)
  public class TestBrush {
    // true if we've set up any singletons
    private bool m_needSingletonTeardown;

    // Contains all the components we need in order to run geometry generation
    private GameObject m_container;

    // Some stroke data for testing
    private List<Stroke> m_testStrokes;

    /// Creates and returns a temporary Canvas, with proper clean-up.
    class TempCanvas : IDisposable {
      // Hack this to true if you want to examine the results.
      public static bool kPersistent = false;

      public CanvasScript m_canvas;
      public TempCanvas(string name="Some unit test") {
        m_canvas = CanvasScript.UnitTestSetUp(new GameObject(name+" canvas"));
      }
      void IDisposable.Dispose() {
        if (kPersistent) {
          m_canvas.BatchManager.FlushMeshUpdates();
        } else {
          CanvasScript.UnitTestTearDown(m_canvas.gameObject);
        }
      }
    }

    [OneTimeSetUp]
    public void RunBeforeAnyTests() {
      m_container = new GameObject("Singletons for TestBrush");
      Coords.AsLocal[m_container.transform] = TrTransform.identity;

      var path = Path.Combine(Application.dataPath, "../Support/Sketches/PerfTest/Simple.tilt");
      m_testStrokes = GetStrokesFromTilt(path);

      if (DevOptions.I == null) {
        m_needSingletonTeardown = true;
        App.Instance = GameObject.Find("/App").GetComponent<App>();
        Config.m_SingletonState = GameObject.Find("/App/Config").GetComponent<Config>();
        DevOptions.I = App.Instance.GetComponent<DevOptions>();
        // A lot of code needs access to BrushCatalog.Instance.m_guidToBrush.
        // We could avoid having to depend on this global state, if only Tilt Brush
        // directly referenced BrushDescriptor instead of indirecting through Guid.
        BrushCatalog.UnitTestSetUp(m_container);
      }
    }

    [OneTimeTearDown]
    public void RunAfterAllTests() {
      Assert.IsTrue(m_container != null);

      if (m_needSingletonTeardown) {
        BrushCatalog.UnitTestTearDown(m_container);
        DevOptions.I = null;
        Config.m_SingletonState = null;
        App.Instance = null;
        m_needSingletonTeardown = false;
      }

      UnityEngine.Object.DestroyImmediate(m_container);
    }

    /// Returns strokes read from the passed .tilt file
    public static List<Stroke> GetStrokesFromTilt(string path) {
      var file = new DiskSceneFileInfo(path, readOnly: true);
      SketchMetadata metadata;
      using (var jsonReader = new JsonTextReader(
                 new StreamReader(
                     SaveLoadScript.GetMetadataReadStream(file)))) {
        // TODO: should cache this?
        var serializer = new JsonSerializer();
        serializer.ContractResolver = new CustomJsonContractResolver();
        serializer.Error += (sender, args) => {
          throw new Exception(args.ErrorContext.Error.Message);
        };
        metadata = serializer.Deserialize<SketchMetadata>(jsonReader);
      }

      using (var stream = file.GetReadStream(TiltFile.FN_SKETCH)) {
        var bufferedStream = new BufferedStream(stream, 4096);
        return SketchWriter.GetStrokes(
            bufferedStream, metadata.BrushIndex, BitConverter.IsLittleEndian);
      }
    }

    /// Creates and returns a BatchSubset in the passed Canvas.
    /// If stroke contains too many CPs to fit, it will be cut short.
    /// This differs from what TB does, which is to create multiple subsets.
    public static BatchSubset CreateSubsetFromStroke(CanvasScript canvas, Stroke stroke) {
      // See PointerScript.RecreateLineFromMemory

      BrushDescriptor desc = BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid);

      var cp0 = stroke.m_ControlPoints[0];
      var xf0 = TrTransform.TRS(cp0.m_Pos, cp0.m_Orient, stroke.m_BrushScale);
      BaseBrushScript bbs = BaseBrushScript.Create(
          canvas.transform, xf0, desc, stroke.m_Color, stroke.m_BrushSize);
      try {
        bbs.SetIsLoading();
        Assert.True(bbs.Canvas != null);

        foreach (var cp in stroke.m_ControlPoints) {
          bbs.UpdatePosition_LS(
              TrTransform.TRS(cp.m_Pos, cp.m_Orient, stroke.m_BrushScale), cp.m_Pressure);
        }
        return bbs.FinalizeBatchedBrush();
      } finally {
        UnityEngine.Object.DestroyImmediate(bbs.gameObject);
      }
    }

    //
    // Tests
    //

    // Unity thinks it's bad to create MeshFilter-owned Meshes at edit time, and
    // will spam an Error log message if you do. This causes the test to fail.
    // Every geometry generation unit test should call this.
    private void HackIgnoreMeshErrorLog() {
      // The number and type of these messages changes from version to version, so be broad
      UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
      // UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "Instantiating mesh due to calling MeshFilter.mesh during edit mode. This will leak meshes. Please use MeshFilter.sharedMesh instead.");
    }

    // This test doesn't actually do anything useful except exercise the scaffolding
    [Test]
    public void CreateSubsetFromStrokeWorksAtEditTime() {
      HackIgnoreMeshErrorLog();

      using (var tempCanvas = new TempCanvas()) {
        var subset = CreateSubsetFromStroke(tempCanvas.m_canvas, m_testStrokes[0]);
        Assert.True(subset != null);
      }
    }

#if false
    // Example code showing the hoops you need to jump through in order to generate
    // and examine geometry at edit-time.

    // Unit tests run inside an ephemeral scene, so if you want to examine
    // the results you need to manually run the test
    static public class TestBrushHelper {
      [MenuItem("Tilt/Run Tests In Scene")]
      public static void RunTestsInScene() {
        var tb = new TestBrush();
        tb.RunBeforeAnyTests();
        try {
          tb.TimeGeometryGeneration(true);
          tb.TimeGeometryGeneration(false);
        } finally {
          tb.RunAfterAllTests();
        }
      }
    }

    class TempEnableReduction : IDisposable {
      bool m_prev;
      public TempEnableReduction(bool val) {
        m_prev = App.Config.m_WeldQuadStripVertices;
        App.Config.m_WeldQuadStripVertices = val;
      }
      void IDisposable.Dispose() { App.Config.m_WeldQuadStripVertices = m_prev; }
    }

    public void TimeGeometryGeneration(bool withReduction) {
      var path = "c:/Users/pld/Documents/Tilt Brush/Sketches/Rescue.tilt";
      var strokes = GetStrokesFromTilt(path);

      using (var tempCanvas = new TempCanvas(string.Format("reduce {0}", withReduction)))
      using (var dummy = new TempEnableReduction(withReduction)) {
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        foreach (var stroke in strokes) {
          CreateSubsetFromStroke(tempCanvas.m_canvas, stroke, m_master);
        }
        sw.Stop();
        Debug.LogFormat("Iter {0} elapsed {1}", withReduction, sw.ElapsedMilliseconds * .001f);
      }
    }
#endif
  }
}
