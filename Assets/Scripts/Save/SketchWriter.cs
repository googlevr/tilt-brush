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

/*
  Binary file format:

  int32 sentinel
  int32 version
  int32 reserved (must be 0)
  [ uint32 size + <size> bytes of additional header data ]

  int32 num_strokes
  num_strokes * {
    int32 brush_index
    float32x4 brush_color
    float32 brush_size
    uint32 stroke_extension_mask
    uint32 controlpoint_extension_mask
    [ int32/float32              for each set bit in stroke_extension_mask &  ffff ]
    [ uint32 size + <size> bytes for each set bit in stroke_extension_mask & ~ffff ]
    int32 num_control_points
    num_control_points * {
      float32x3 position
      float32x4 orientation (quat)
      [ int32/float32 for each set bit in controlpoint_extension_mask ]
    }
  }

 */

using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StrokeFlags = TiltBrush.SketchMemoryScript.StrokeFlags;
using ControlPoint = TiltBrush.PointerManager.ControlPoint;

namespace TiltBrush {

public static class SketchWriter {
  // Extensions-- we use this for stroke and control point extensibility.
  //
  // Each bit in the enum represents an extension ID in [0, 31].
  // At save, we write out the extension ID mask and, grouped at a certain place in the
  // stream, the corresponding blocks of data in ascending order of ID.
  //
  // At load, we iterate through set bits in the mask, consuming each block of data.
  //
  // Data blocks for ControlPointExtension IDs are 4 bytes.
  // Data blocks for StrokeExtension IDs in [0,15] are 4 bytes.
  // Data blocks for StrokeExtension IDs in [16,31] are uint32 length + <length> bytes.

  [Flags]
  public enum StrokeExtension : uint {
    MaskSingleWord = 0xffff,
    None = 0,
    Flags = 1 << 0,     // uint32, bitfield
    Scale = 1 << 1,     // float, 1.0 is nominal
    Group = 1 << 2,     // uint32, a value of 0 corresponds to SketchGroupTag.None so in that case,
                        // we don't save out the group.
    Seed = 1 << 3,      // int32; if not found then you get a random int.
  }

  [Flags]
  public enum ControlPointExtension : uint {
    None = 0,
    Pressure = 1 << 0,  // float, 1.0 is nominal
    Timestamp = 1 << 1,  // uint32, milliseconds
  }

  public struct AdjustedMemoryBrushStroke {
    public StrokeData strokeData;
    public StrokeFlags adjustedStrokeFlags;
  }

  private const int REQUIRED_SKETCH_VERSION_MIN = 5;
  private const int REQUIRED_SKETCH_VERSION_MAX = 6;
  private static readonly uint SKETCH_SENTINEL = 0xc576a5cd;  // introduced at v5
  // 5: added sketch sentinel, explicit version
  // 6: reserved for when we add a length-prefixed stroke extension, or more header data
  private static readonly int SKETCH_VERSION = 5;

  static public void RuntimeSelfCheck() {
    // Sanity-check ControlPoint's self-description; ReadMemory relies on it
    // being correct.
    unsafe {
      uint sizeofCP = (uint)sizeof(PointerManager.ControlPoint);
      uint extensionBytes = 4 * CountOnes(PointerManager.ControlPoint.EXTENSIONS);
      System.Diagnostics.Debug.Assert(
          sizeofCP == sizeof(Vector3) + sizeof(Quaternion) + extensionBytes);
    }
  }

  static uint CountOnes(uint val) {
    uint n = 0;
    while (val != 0) {
      n += 1;
      val = val & (val - 1);
    }
    return n;
  }

  // Enumerate the active memory list strokes, and return snapshots of the strokes.
  // The snapshots include adjusted stroke flags which take into account the effect
  // of inactive items on grouping.
  public static IEnumerable<AdjustedMemoryBrushStroke> EnumerateAdjustedSnapshots(
      IEnumerable<Stroke> strokes) {
    // Example grouping adjustment cases (n = ID, "C"=ContinueGroup, "x" = erased object):
    //     |0  |1C |2C |  =>  |0  |1C |2C |
    //     |0 x|1C |2C |  =>  |1  |2C |
    //     |0  |1Cx|2C |  =>  |0  |2C |
    //     |0  |1Cx|2Cx|  =>  |0  |
    //     |0 x|1Cx|2C |  =>  |2  |
    bool resetGroupContinue = false;
    foreach (var stroke in strokes) {
      AdjustedMemoryBrushStroke snapshot = new AdjustedMemoryBrushStroke();
      snapshot.strokeData = stroke.GetCopyForSaveThread();
      snapshot.adjustedStrokeFlags = stroke.m_Flags;
      if (resetGroupContinue) {
        snapshot.adjustedStrokeFlags &= ~StrokeFlags.IsGroupContinue;
        resetGroupContinue = false;
      }
      if (stroke.IsGeometryEnabled) {
        yield return snapshot;
      } else {
        // Effectively, if the lead stroke of group is inactive (erased), we promote
        // subsequent strokes to lead until one such stroke is active.
        resetGroupContinue = !snapshot.adjustedStrokeFlags.HasFlag(StrokeFlags.IsGroupContinue);
      }
    }
  }

  /// Write out sketch memory strokes ordered by initial control point timestamp.
  /// Leaves stream in indeterminate state; caller should Close() upon return.
  /// Output brushList provides mapping from .sketch brush index to GUID.
  /// While writing out the strokes we adjust the stroke flags to take into account the effect
  /// of inactive items on grouping.
  public static void WriteMemory(Stream stream, IList<AdjustedMemoryBrushStroke> strokeCopies,
                                 GroupIdMapping groupIdMapping, out List<Guid> brushList){
    bool allowFastPath = BitConverter.IsLittleEndian;
    var writer = new TiltBrush.SketchBinaryWriter(stream);

    writer.UInt32(SKETCH_SENTINEL);
    writer.Int32(SKETCH_VERSION);
    writer.Int32(0);  // reserved for header: must be 0
    // Bump SKETCH_VERSION to >= 6 and remove this comment if non-zero data is written here
    writer.UInt32(0);  // additional data size

    var brushMap = new Dictionary<Guid, int>();  // map from GUID to index
    brushList = new List<Guid>();  // GUID's by index

    // strokes
    writer.Int32(strokeCopies.Count);
    foreach (var copy in strokeCopies) {
      var stroke = copy.strokeData;
      int brushIndex;
      Guid brushGuid = stroke.m_BrushGuid;
      if (!brushMap.TryGetValue(brushGuid, out brushIndex)) {
        brushIndex = brushList.Count;
        brushMap[brushGuid] = brushIndex;
        brushList.Add(brushGuid);
      }

      writer.Int32(brushIndex);
      writer.Color(stroke.m_Color);
      writer.Float(stroke.m_BrushSize);
      // Bump SKETCH_VERSION to >= 6 and remove this comment if any
      // length-prefixed stroke extensions are added
      StrokeExtension strokeExtensionMask = StrokeExtension.Flags | StrokeExtension.Seed;
      if (stroke.m_BrushScale != 1)              { strokeExtensionMask |= StrokeExtension.Scale; }
      if (stroke.m_Group != SketchGroupTag.None) { strokeExtensionMask |= StrokeExtension.Group; }

      writer.UInt32((uint)strokeExtensionMask);
      uint controlPointExtensionMask =
          (uint)(ControlPointExtension.Pressure | ControlPointExtension.Timestamp);
      writer.UInt32(controlPointExtensionMask);

      // Stroke extension fields, in order of appearance in the mask
      writer.UInt32((uint)copy.adjustedStrokeFlags);
      if ((uint)(strokeExtensionMask & StrokeExtension.Scale) != 0) {
        writer.Float(stroke.m_BrushScale);
      }
      if ((uint)(strokeExtensionMask & StrokeExtension.Group) != 0) {
        writer.UInt32(groupIdMapping.GetId(stroke.m_Group));
      }
      if ((uint)(strokeExtensionMask & StrokeExtension.Seed) != 0) {
        writer.Int32(stroke.m_Seed);
      }

      // Control points
      writer.Int32(stroke.m_ControlPoints.Length);
      if (allowFastPath && controlPointExtensionMask == ControlPoint.EXTENSIONS) {
        // Fast path: write ControlPoint[] (semi-)directly into the file
        unsafe {
          int size = sizeof(ControlPoint) * stroke.m_ControlPoints.Length;
          fixed (ControlPoint* aPoints = stroke.m_ControlPoints) {
            writer.Write((IntPtr)aPoints, size);
          }
        }
      } else {
        for (int j = 0; j < stroke.m_ControlPoints.Length; ++j) {
          var rControlPoint = stroke.m_ControlPoints[j];
          writer.Vec3(rControlPoint.m_Pos);
          writer.Quaternion(rControlPoint.m_Orient);
          // Control point extension fields, in order of appearance in the mask
          writer.Float(rControlPoint.m_Pressure);
          writer.UInt32(rControlPoint.m_TimestampMs);
        }
      }
    }
  }

  /// Leaves stream in indeterminate state; caller should Close() upon return.
  public static bool ReadMemory(Stream stream, Guid[] brushList, bool bAdditive, out bool isLegacy) {
    bool allowFastPath = BitConverter.IsLittleEndian;
    // Buffering speeds up fast path ~1.4x, slow path ~2.3x
    var bufferedStream = new BufferedStream(stream, 4096);

    // var stopwatch = new System.Diagnostics.Stopwatch();
    // stopwatch.Start();

    isLegacy = false;
    SketchMemoryScript.m_Instance.ClearRedo();
    if (!bAdditive) {
      //clean up old draw'ring
      SketchMemoryScript.m_Instance.ClearMemory();
    }

#if (UNITY_EDITOR || EXPERIMENTAL_ENABLED)
    if (Config.IsExperimental) {
      if (App.Config.m_ReplaceBrushesOnLoad) {
        brushList = brushList.Select(guid => App.Config.GetReplacementBrush(guid)).ToArray();
      }
    }
#endif

    var strokes = GetStrokes(bufferedStream, brushList, allowFastPath);
    if (strokes == null) { return false; }

    // Check that the strokes are in timestamp order.
    uint headMs = uint.MinValue;
    foreach (var stroke in strokes) {
      if (stroke.HeadTimestampMs < headMs) {
        strokes.Sort((a,b) => a.HeadTimestampMs.CompareTo(b.HeadTimestampMs));
        ControllerConsoleScript.m_Instance.AddNewLine("Bad timing data detected. Please re-save.");
        Debug.LogAssertion("Unsorted timing data in sketch detected. Strokes re-sorted.");
        break;
      }
      headMs = stroke.HeadTimestampMs;
    }

    QualityControls.m_Instance.AutoAdjustSimplifierLevel(strokes, brushList);
    foreach (var stroke in strokes) {
      // Deserialized strokes are expected in timestamp order, yielding aggregate complexity
      // of O(N) to populate the by-time linked list.
      SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
    }

    // stopwatch.Stop();
    // Debug.LogFormat("Reading took {0}", stopwatch.Elapsed);
    return true;
  }

  /// Parses a binary file into List of MemoryBrushStroke.
  /// Returns null on parse error.
  public static List<Stroke> GetStrokes(
      Stream stream, Guid[] brushList, bool allowFastPath) {
    var reader = new TiltBrush.SketchBinaryReader(stream);

    uint sentinel = reader.UInt32();
    if (sentinel != SKETCH_SENTINEL) {
      Debug.LogFormat("Invalid .tilt: bad sentinel");
      return null;
    }

    if (brushList == null) {
      Debug.Log("Invalid .tilt: no brush list");
      return null;
    }

    int version = reader.Int32();
    if (version < REQUIRED_SKETCH_VERSION_MIN ||
        version > REQUIRED_SKETCH_VERSION_MAX) {
      Debug.LogFormat("Invalid .tilt: unsupported version {0}", version);
      return null;
    }

    reader.Int32();  // reserved for header: must be 0
    uint moreHeader = reader.UInt32();  // additional data size
    if (!reader.Skip(moreHeader)) { return null; }

    // strokes
    int iNumMemories = reader.Int32();
    var result = new List<Stroke>();
    for (int i = 0; i < iNumMemories; ++i) {
      var stroke = new Stroke();

      var brushIndex = reader.Int32();
      stroke.m_BrushGuid = (brushIndex < brushList.Length) ?
        brushList[brushIndex] : Guid.Empty;
      stroke.m_Color = reader.Color();
      stroke.m_BrushSize = reader.Float();
      stroke.m_BrushScale = 1f;
      stroke.m_Seed = 0;

      uint strokeExtensionMask = reader.UInt32();
      uint controlPointExtensionMask = reader.UInt32();

      if ((strokeExtensionMask & (int)StrokeExtension.Seed) == 0) {
        // Backfill for old files saved without seeds.
        // This is arbitrary but should be determinstic.
        unchecked {
          int seed = i;
          seed = (seed * 397) ^ stroke.m_BrushGuid.GetHashCode();
          seed = (seed * 397) ^ stroke.m_Color.GetHashCode();
          seed = (seed * 397) ^ stroke.m_BrushSize.GetHashCode();
          stroke.m_Seed = seed;
        }
      }

      // stroke extension fields
      // Iterate through set bits of mask starting from LSB via bit tricks:
      //    isolate lowest set bit: x & ~(x-1)
      //    clear lowest set bit: x & (x-1)
      for (var fields = strokeExtensionMask; fields != 0; fields &= (fields - 1)) {
        uint bit = (fields & ~(fields - 1));
        switch ((StrokeExtension)bit) {
          case StrokeExtension.None:
            // cannot happen
            Debug.Assert(false);
            break;
          case StrokeExtension.Flags:
            stroke.m_Flags = (StrokeFlags)reader.UInt32();
            break;
          case StrokeExtension.Scale:
            stroke.m_BrushScale = reader.Float();
            break;
          case StrokeExtension.Group: {
            UInt32 groupId = reader.UInt32();
            stroke.Group = App.GroupManager.GetGroupFromId(groupId);
            break;
          }
          case StrokeExtension.Seed:
            stroke.m_Seed = reader.Int32();
            break;
          default: {
            // Skip unknown extension.
            if ((bit & (uint)StrokeExtension.MaskSingleWord) != 0) {
              reader.UInt32();
            } else {
              uint size = reader.UInt32();
              if (!reader.Skip(size)) { return null; }
            }
            break;
          }
        }
      }

      // control points
      int nControlPoints = reader.Int32();
      stroke.m_ControlPoints = new PointerManager.ControlPoint[nControlPoints];
      stroke.m_ControlPointsToDrop = new bool[nControlPoints];

      if (allowFastPath && controlPointExtensionMask == PointerManager.ControlPoint.EXTENSIONS) {
        // Fast path: read (semi-)directly into the ControlPoint[]
        unsafe {
          int size = sizeof(PointerManager.ControlPoint) * stroke.m_ControlPoints.Length;
          fixed (PointerManager.ControlPoint* aPoints = stroke.m_ControlPoints) {
            if (!reader.ReadInto((IntPtr)aPoints, size)) {
              return null;
            }
          }
        }
      } else {
        // Slow path: deserialize field-by-field.
        for (int j = 0; j < nControlPoints; ++j) {
          PointerManager.ControlPoint rControlPoint;

          rControlPoint.m_Pos = reader.Vec3();
          rControlPoint.m_Orient = reader.Quaternion();

          // known extension field defaults
          rControlPoint.m_Pressure = 1.0f;
          rControlPoint.m_TimestampMs = 0;

          // control point extension fields
          for (var fields = controlPointExtensionMask; fields != 0; fields &= (fields - 1)) {
            switch ((ControlPointExtension)(fields & ~(fields - 1))) {
            case ControlPointExtension.None:
              // cannot happen
              Debug.Assert(false);
              break;
            case ControlPointExtension.Pressure:
              rControlPoint.m_Pressure = reader.Float();
              break;
            case ControlPointExtension.Timestamp:
              rControlPoint.m_TimestampMs = reader.UInt32();
              break;
            default:
              // skip unknown extension
              reader.Int32();
              break;
            }
          }
          stroke.m_ControlPoints[j] = rControlPoint;
        }
      }

      // Deserialized strokes are expected in timestamp order, yielding aggregate complexity
      // of O(N) to populate the by-time linked list.
      result.Add(stroke);
    }

    return result;
  }
}
} // namespace TiltBrush
