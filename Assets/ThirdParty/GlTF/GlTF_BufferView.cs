using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public sealed class GlTF_BufferView : GlTF_ReferencedObject, IDisposable {
  public const int kTarget_ARRAY_BUFFER = 34962;
  public const int kTarget_ELEMENT_ARRAY_BUFFER = 34963;

  // Static API

  /// Creates a view and adds it to the array of views
  public static GlTF_BufferView Create(
      GlTF_Globals G, string name, int target=kTarget_ARRAY_BUFFER) {
    var ret = new GlTF_BufferView(G, name, target);
    G.bufferViews.Add(ret);
    return ret;
  }

  // Instance API

  public long byteLength;
  public long currentOffset => byteLength;  // For backwards-compatibility only
  // Has no value until quite late, during serialization.
  public long? byteOffset;
  public readonly int target;
  //	public string target = "ARRAY_BUFFER";
  public Stream stream = new MemoryStream();
  public string streamFileName = null;

  // Only for gltf2.
  //
  // Accessors will try to write the stride they want. If they see non-null and the value
  // contradicts what they want, they'll blow up.
  // null means "no accessors have tried to tell us what stride to use".
  // 0 (special case) means "accessors want this tightly packed".
  // A non-0 multiple of 4 means all accessors want that byte stride.
  // Non-multiples of 4 are invalid.
  //
  // If this value is 0, m_packedSize will also be set; this is to detect cases where
  // one Accessor writes 0 intending "stride of 2, tightly packed" and another writes 4
  // meaning "stride of 4, tightly packed".
  public int? m_byteStride;
  public int? m_packedSize;
  public GlTF_Buffer m_buffer;

  private TiltBrush.SketchBinaryWriter m_binaryWriter = new TiltBrush.SketchBinaryWriter(null);

  private GlTF_BufferView(GlTF_Globals globals, string n, int t) : base(globals) {
    name = n; target = t;
  }

  /// Enables use of files instead of memory when collating data for export.
  /// Can only be called before anything has been written.
  /// May fail, eg if no temporary directory has been defined.
  public void EnableFileStream() {
    if (streamFileName != null) {
      // It's already a file
      return;
    }
    Debug.Assert(stream.Position == 0, "Not safe to throw away stream");
    if (stream != null) { stream.Dispose(); }
    stream = null;

    streamFileName = Path.Combine(G.temporaryDirectory,
                                  string.Format("bufferview_{0}.tmp", name));
    stream = new FileStream(streamFileName, FileMode.Create, FileAccess.ReadWrite);
  }

  public void Dispose() {
    if (stream != null) {
      stream.Dispose();
      stream = null;
    }
    if (streamFileName != null) {
      try {
        if (File.Exists(streamFileName)) {
          File.Delete(streamFileName);
        }
      } catch (Exception e) {
        // Ugh. There are so many possible exceptions that could be raised, and
        // we have to prevent them all from propagating.
        Debug.LogException(e);
      }
      streamFileName = null;
    }
  }

  public void FastPopulate<T>(T[] data, int count) where T : unmanaged {
    // this.stream can change, so always assign it just-in-time.
    m_binaryWriter.BaseStream = this.stream;
    byteLength += m_binaryWriter.WriteRaw(data, count);
    // Flushes data if necessary (which it currently isn't)
    m_binaryWriter.BaseStream = null;
  }

  public void FastPopulate<T>(List<T> data) where T : unmanaged {
    m_binaryWriter.BaseStream = this.stream;
    byteLength += m_binaryWriter.WriteRaw(data);
    m_binaryWriter.BaseStream = null;
  }

  public void PopulateUshort(int[] vs) {
    FastPopulate(vs.Select(i32 => (ushort)i32).ToList());
  }

  public override IEnumerable<GlTF_ReferencedObject> IterReferences() {
    yield return G.Lookup(m_buffer);
  }

  public override void WriteTopLevel() {
    /*
		"bufferView_4642": {
            "buffer": "vc.bin",
            "byteLength": 630080,
            "byteOffset": 0,
            "target": "ARRAY_BUFFER"
        },
	*/
    BeginGltfObject();

    G.CNI.WriteNamedReference("buffer", m_buffer);
    G.CNI.WriteNamedInt("byteLength", byteLength);
    G.CNI.WriteNamedInt("byteOffset", byteOffset.Value);
    // 0 means "pack as tightly as possible"
    if (G.Gltf2 && m_byteStride != null && m_byteStride.Value != 0) {
      G.CNI.WriteNamedInt("byteStride", m_byteStride.Value);
    }
    G.CNI.WriteNamedInt("target", target);

    EndGltfObject();
  }
}
