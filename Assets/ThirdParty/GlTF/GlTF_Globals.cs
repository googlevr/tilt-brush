// This holds all the global state that was once in GlTF_Writer

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Debug = UnityEngine.Debug;
using RefObj = GlTF_ReferencedObject;
using RefGraph = System.Collections.Generic.Dictionary<
    GlTF_ReferencedObject,
    System.Collections.Generic.HashSet<GlTF_ReferencedObject>>;
using IExportableMaterial = TiltBrush.IExportableMaterial;
using ExportFileReference = TiltBrush.ExportFileReference;

public sealed class GlTF_Globals : IDisposable {
  private const int B3DM_HEADER_SIZE = 24;
  // 'glTF', version, total len, json len, type field: all 4 bytes each
  private const int kGlbHeaderSize = 5 * 4;

  private const UInt32 kFourCC_glTF = 0x46546c67;  // 'glTF'
  private const UInt32 kFourCC_JSON = 0x4E4F534A;  // 'JSON'
  // FourCC generally space-pads these codes, but this is NUL-padded
  private const UInt32 kFourCC_BIN_ = 0x004E4942;  // 'BIN\0'
  private const UInt32 kFourCC_b3dm = 0x6D643362;  // 'b3dm'
  // See https://github.com/KhronosGroup/glTF/blob/master/extensions/Prefixes.md
  public const string kTiltBrushMaterialExtensionName = "GOOGLE_tilt_brush_material";

  // In binary mode, both these writers reference the same stream.
  // Be sure to always flush one before starting to use the other.
  public StreamWriter jsonWriter;
  public BinaryWriter binWriter;
  // Only valid after OpenFiles has been called. This might be a .glb or a .gltf
  private string m_outputFileName;
  private string binFileName;

  private int indent = 0;
  public bool binary;
  private bool b3dm;
  private readonly int m_gltfVersion;
  public readonly GlTF_BufferView m_scalarUshortElementArrayBv;
  public readonly GlTF_BufferView m_stride4Bv;
  public readonly GlTF_BufferView m_stride8Bv;
  public readonly GlTF_BufferView m_stride12Bv;
  public readonly GlTF_BufferView m_stride16Bv;
  public readonly List<GlTF_BufferView> bufferViews = new List<GlTF_BufferView>();
  public readonly List<GlTF_Camera> cameras = new List<GlTF_Camera>();
  public readonly List<GlTF_Mesh> meshes = new List<GlTF_Mesh>();
  public readonly List<GlTF_Accessor> accessors = new List<GlTF_Accessor>();
  public readonly Dictionary<string, GlTF_Node> nodes = new Dictionary<string, GlTF_Node>();
  public readonly Dictionary<IExportableMaterial, GlTF_Material> materials =
      new Dictionary<IExportableMaterial, GlTF_Material>();
  public readonly Dictionary<string, GlTF_Sampler> samplers = new Dictionary<string, GlTF_Sampler>();
  public readonly Dictionary<string, GlTF_Texture> textures = new Dictionary<string, GlTF_Texture>();
  public readonly Dictionary<string, GlTF_Image> imagesByFileRefUri =
      new Dictionary<string, GlTF_Image>();
  public readonly Dictionary<string, GlTF_Technique> techniques = new Dictionary<string, GlTF_Technique>();
  public readonly List<GlTF_Program> programs = new List<GlTF_Program>();
  public readonly List<GlTF_Shader> shaders = new List<GlTF_Shader>();
  // Allows (key, value) string pairs to be passed along to the glTF consumer by
  // stuffing them onto the main GlTF_Scene object.
  public readonly Dictionary<string, object> extras = new Dictionary<string, object>();
  public readonly double[] RTCCenter = null;

  private List<String> m_exportedFiles = new List<string>();
  private string copyright;
  private string generator;
  private byte[] copyBuffer = new byte[16 * 1024];
  private List<ExportFileReference> m_exportedFileReferences = new List<ExportFileReference>();
  public ExportFileReference.DisambiguationContext m_disambiguationContext =
      new ExportFileReference.DisambiguationContext();
  private HashSet<string> m_materialPresentationNames = new HashSet<string>();

  public readonly string temporaryDirectory;

  // Only valid during Write(). This contains all the known objects.
  public Dictionary<string, RefObj> m_nameToObject = null;
  // Captures outgoing edges in the object graph by examining the behavior of the serializers.
  // Used for verifying the correctness of GlTF_ReferencedObject.IterReferences().
  HashSet<RefObj> m_emittedRefs = new HashSet<RefObj>();

  public bool Gltf2 => m_gltfVersion == 2;

  // When true, generate gltf that strictly follows gltf's attribute-naming conventions.
  // eg: texcoord_N can only have two elements, cannot skip a texcoord, etc
  public bool GltfCompatibilityMode => m_gltfVersion == 2;

  // When true, put the Tilt Brush material name (ie guid) in a TILTBRUSH_Material extension.
  public bool UseTiltBrushMaterialExtension => m_gltfVersion == 2;

  // glTF metadata to write out.
  public string Copyright {
    get { return OrUnknown(copyright); }
    set { copyright = value; }
  }
  public string Generator {
    get { return OrUnknown(generator); }
    set { generator = value; }
  }
  public string GltfVersion {
    get {
      switch (m_gltfVersion) {
      case 1: return "1.1";
      case 2: return "2.0";
      default: return null;
      }
    }
  }

  // Contains full paths (or maybe paths relative to the current working directory)
  // These paths not relative to the export directory.
  public IEnumerable<string> ExportedFiles => m_exportedFiles;

  /// Almost every single thing we write is prefixed by "CommaNL(); Indent();"
  /// This "Comma, Newline, Indent" property makes that a little bit less typing.
  /// Usage:
  ///    G.CommaNL(); G.Indent(); G.WriteBlahBlah(...);  // old
  ///    G.CNI.WriteBlahBlah(...);                       // new
  public GlTF_Globals CNI {
    get {
      CommaNL();
      Indent();
      return this;
    }
  }

  private static string OrUnknown(string str) { return str != null ? str : "Unknown."; }

  private readonly bool[] firsts = new bool[100];

  // temporaryDirectory may be null
  // If non-null, ownership of the directory is transferred.
  public GlTF_Globals(string temporaryDirectory, int gltfVersion) {
    if (gltfVersion != 1 && gltfVersion != 2) {
      throw new ArgumentException("gltfVersion");
    }
    m_gltfVersion = gltfVersion;

    this.temporaryDirectory = temporaryDirectory;
    try {
      Directory.CreateDirectory(this.temporaryDirectory);
    } catch (Exception e) {
      UnityEngine.Debug.LogException(e);
      this.temporaryDirectory = null;
    }
    m_scalarUshortElementArrayBv = GlTF_BufferView.Create(this, "ushortBufferView",
                                       GlTF_BufferView.kTarget_ELEMENT_ARRAY_BUFFER);
    // These names aren't really accurate any more; but I'm keeping them for easier diffing.
    m_stride4Bv = GlTF_BufferView.Create(this, "floatBufferView");
    m_stride8Bv = GlTF_BufferView.Create(this, "vec2BufferView");
    m_stride12Bv = GlTF_BufferView.Create(this, "vec3BufferView");
    m_stride16Bv = GlTF_BufferView.Create(this, "vec4BufferView");
  }

  /// Enables use of files instead of memory when collating data for export.
  public void EnableFileStream() {
    if (this.temporaryDirectory != null) {
      foreach (var bv in bufferViews) {
        bv.EnableFileStream();
      }
    }
  }

  public void Dispose() {
    CloseFiles();
    foreach (GlTF_BufferView bv in bufferViews) { bv.Dispose(); }
    if (temporaryDirectory != null) {
      try {
        if (Directory.Exists(temporaryDirectory)) {
          Directory.Delete(temporaryDirectory, true);
        }
      } catch (IOException e) {
        UnityEngine.Debug.LogException(e);
      }
    }
  }

  public void Indent() {
    for (int i = 0; i < indent; i++)
      jsonWriter.Write("\t");
  }

  // Cursor should be indented properly.
  // Leaves cursor at the beginning of a new line.
  // =>"KEYNAME": {\n
  public void WriteKeyAndIndentIn(string key, string delimiter) {
    // There's a newline here because CommaNL (weirdly) skips writing a newline
    // for the first item in an indented block.
    jsonWriter.Write($"\"{key}\": {delimiter}\n");
    IndentIn();
  }

  // Cursor should be at the end of the line after writing an item
  // Leaves cursor and the end of a line just after a delimiter
  public void NewlineAndIndentOut(string delimiter) {
    jsonWriter.WriteLine();
    IndentOut();
    Indent();
    jsonWriter.Write(delimiter);
  }

  public void IndentIn() {
    indent++;
    firsts[indent] = true;
  }

  /// Returns the specified top-level gltf object.
  /// This may seem like a silly no-op, but it performs some validation
  /// to ensure Lookup(obj) === Lookup<T>(obj.name)
  public RefObj Lookup(RefObj gltfObject) {
    if (! m_nameToObject.TryGetValue(gltfObject.name, out RefObj gltfObject2)) {
      Debug.LogError($"Object {gltfObject.name} was not registered");
      throw new KeyNotFoundException(gltfObject.name);
    }
    if (ReferenceEquals(gltfObject2, gltfObject)) {
      return gltfObject;
    } else {
      Debug.LogError($"Two objects are named {gltfObject.name}: {gltfObject} and {gltfObject2}");
      throw new KeyNotFoundException(gltfObject.name);
    }
  }

  /// Returns the index of the specified top-level gltf object.
  /// Raises KeyNotFoundException if the desired object is not found, or it has no index
  /// (meaning that it is not marked to be exported).
  private int LookupIndex(RefObj gltfObject) {
    RefObj obj = Lookup(gltfObject);
    if (obj.Index == null) {
      throw new KeyNotFoundException("Index " + gltfObject.name);
    }
    return obj.Index.Value;
  }

  /// Performs a LookupIndex() and formats the result as a gltf-style reference.
  /// If gltf1, this is a string name.
  /// If gltf2, this is a small integer index into the specified type's top-level object array.
  /// Raises KeyNotFoundException if the desired object is not found.
  public string SerializeReference(RefObj gltfObject) {
    // Always look it up, so we get good sanity checking even in gltf1 mode.
    int id = LookupIndex(gltfObject);
    m_emittedRefs.Add(gltfObject);
    if (Gltf2) {
      return id.ToString();
    } else {
      return $"\"{gltfObject.name}\"";
    }
  }

  /// Like LookupIndex<T>(string name) but looks through all the known objects,
  /// as opposed to all the objects with indices.
  public T Lookup<T>(string gltfObjectName) where T : RefObj {
    if (! m_nameToObject.TryGetValue(gltfObjectName, out RefObj gltfObj)) {
      throw new KeyNotFoundException("Missing " + gltfObjectName);
    }
    if (gltfObj is T gltfObjTyped) {
      return gltfObjTyped;
    } else {
      Type a = typeof(T), b = gltfObj.GetType();
      Debug.LogError($"Two objects are named {gltfObjectName}: T={a} and T={b}");
      throw new KeyNotFoundException("Duplicate " + gltfObjectName);
    }
  }

  /// Returns the index of the specified top-level gltf object.
  /// Raises KeyNotFoundException if the desired object is not found.
  /// Sanity-checking is slightly less robust than LookupIndex(GlTF_ReferencedObject);
  /// it will not detect duplicate-named objects if the duplicate has the same type.
  /// Raises KeyNotFoundException if the desired object is not found.
  public (int, T) LookupIndex<T>(string gltfObjectName) where T : RefObj {
    var obj = Lookup<T>(gltfObjectName);
    if (obj.Index == null) {
      throw new KeyNotFoundException("Index " + gltfObjectName);
    }
    return (obj.Index.Value, obj);
  }

  /// Like SerializeReference(GlTF_Writer) but uses LookupIndex<T> so it's a little less safe.
  public string SerializeReference<T>(string gltfObjectName) where T : RefObj {
    (int id, RefObj gltfObject) = LookupIndex<T>(gltfObjectName);
    m_emittedRefs.Add(gltfObject);
    if (Gltf2) {
      return id.ToString();
    } else {
      return $"\"{gltfObjectName}\"";
    }
  }

  public void IndentOut() {
    indent--;
  }

  public void CommaStart() {
    firsts[indent] = false;
  }

  public void CommaNL() {
    if (!firsts[indent])
      jsonWriter.Write(",\n");
    //		else
    //			jsonWriter.Write ("\n");
    firsts[indent] = false;
  }

  public void OpenFiles(string filepath) {
    Debug.Assert(m_outputFileName == null);
    m_outputFileName = filepath;
    m_exportedFiles.Add(filepath);
    jsonWriter = new StreamWriter(File.Open(filepath, FileMode.Create));
    jsonWriter.NewLine = "\n";

    if (binary) {
      var encoding = new System.Text.UTF8Encoding(false, true);
      // jsonWriter and binWriter both use the same underlying file stream.
      // This probably only works because we write to a single one at a time.
      // This should really be refactored to not share the stream.
#if UNITY_2018_4_OR_NEWER || NET_4_6
      // In 4.6 it's not okay for fs to be owned by two streams. In .net 2.0 it was okay,
      // which is good because the leaveOpen: api didn't exist then.
      binWriter = new BinaryWriter(jsonWriter.BaseStream, encoding, leaveOpen: true);
#else
      binWriter = new BinaryWriter(jsonWriter.BaseStream, encoding);
#endif
      long jsonContentStart = kGlbHeaderSize + (b3dm ? B3DM_HEADER_SIZE : 0);
      jsonWriter.BaseStream.Seek(jsonContentStart, SeekOrigin.Begin); // header skip
    } else {
      // separate bin file
      binFileName = Path.GetFileNameWithoutExtension(filepath) + ".bin";
      var binPath = Path.Combine(Path.GetDirectoryName(filepath), binFileName);
      binWriter = new BinaryWriter(File.Open(binPath, FileMode.Create));
      binWriter.Seek(0, SeekOrigin.Begin);
      m_exportedFiles.Add(binPath);
    }
  }

  // This is robust and may be called at any time.
  public void CloseFiles() {
    if (binWriter != null) {
      binWriter.Close();
      // Protect against buglet in BinaryWriter; a second Close() is not always a no-op.
      binWriter = null;
    }

    if (jsonWriter != null) {
      jsonWriter.Close();
      jsonWriter = null;
    }
  }

  /// For writing gltf objects at the top-level.
  ///
  /// Unlike the other Write* routines, this
  /// - Handles null collections by not writing anything.
  /// - Wants the cursor to be at the end of the previous line, rather than
  ///   indented to the new write position.
  ///
  /// The callback should use GlTF_Writer's Begin/EndGltfObject to write the contents
  /// either as a JArray or JObject depending on gltf version.
  ///
  /// Start with cursor at end of previous item; comma and newline will be added if necessary.
  /// Callback gets a CommaNL() and Indent().
  /// Leaves cursor at the end of the line, right after a close delimiter.
  private void WriteTopLevel(
      RefGraph refGraph, HashSet<RefObj> used,
      string name, IReadOnlyCollection<RefObj> collection) {
    if (collection == null) { return; }
    var filtered = collection.Where(elt => used.Contains(elt)).ToArray();
    WriteTopLevelImpl(refGraph, name, filtered);
  }

  private void WriteTopLevelImpl(
      RefGraph refGraph,
      string key, IReadOnlyCollection<RefObj> objs) {
    // gltf2 spec doesn't like empty top-level arrays
    if (objs.Count == 0) { return; }
    string open, close;
    if (Gltf2) { open = "["; close = "]"; }
    else { open = "{"; close = "}"; }

    CNI.WriteKeyAndIndentIn(key, open);

    int currentIndex = 0;
    foreach (RefObj obj in objs) {
      // Sanity-check current array position vs where the item expects to be written
      try {
        int expectedIndex = LookupIndex(obj);
        if (expectedIndex != currentIndex) {
          Debug.LogError(
              $"Object {obj.name} is being written at {currentIndex} instead of {expectedIndex}");
        }
      } catch (KeyNotFoundException e) {
        Debug.LogError($"Writing unknown {obj.GetType()} {obj.name}: {e.Message}");
      }

      CommaNL(); Indent();
      if (!Gltf2) {
        jsonWriter.Write($"\"{obj.name}\": ");
      }

      // Serialize, and check that the refs reported during GC mark phase (IterReferences)
      // are exactly the ones emitted during serialization
      m_emittedRefs.Clear();
      obj.WriteTopLevel();
      if (refGraph != null) {
        HashSet<RefObj> expectedRefs = refGraph[obj];
        if (!m_emittedRefs.SetEquals(expectedRefs)) {
          string missing = string.Join(", ", SetSub(expectedRefs, m_emittedRefs));
          string extra = string.Join(", ", SetSub(m_emittedRefs, expectedRefs));
          // If any are in "missing", that means we over-reported in IterReferences and left
          // in some gltf2 data that is maybe unused. If any are in "extra", that means we
          // under-reported in IterReferences and serialization will probably fail
          // since the serializer will detect attemts to write out dangling references.
          Debug.LogError($"IterReferences error detected in {obj.name}:\n" +
                         $"Over-reported: {missing}\n" +
                         $"Under-reported: {extra}");
        }
      }

      currentIndex += 1;
    }

    NewlineAndIndentOut(close);
  }

  private static IEnumerable<string> SetSub(HashSet<RefObj> lhs,
                                            HashSet<RefObj> rhs) {
    var ret = new HashSet<RefObj>(lhs);
    ret.ExceptWith(rhs);
    return ret.Select(obj => obj.name);
  }

  /// Cursor should be indented to the desired location.
  /// The callback gets an indented cursor.
  /// Upon return, the cursor is ready for a comma and newline.
  public void WriteNamedJArray<T>(string name, IEnumerable<T> col, Action<T> writer) {
    WriteKeyAndIndentIn(name, "[");
    foreach (T item in col) {
      CommaNL(); Indent(); writer(item);
    }
    NewlineAndIndentOut("]");
  }

  /// Cursor should be indented to the desired location.
  /// The callback gets an indented cursor.
  /// Upon return, the cursor is ready for a comma and newline.
  public void WriteNamedJObject<T>(string name, IEnumerable<T> col, Action<T> writer) {
    WriteKeyAndIndentIn(name, "{");
    foreach (T item in col) {
      CommaNL(); Indent(); writer(item);
    }
    NewlineAndIndentOut("}");
  }

  /// Specialization for (string, string).
  /// Omits any pairs with null keys or values.
  public void WriteNamedJObject(string name, IEnumerable<(string, string)> col) {
    WriteKeyAndIndentIn(name, "{");
    foreach (var pair in col) {
      if (pair.Item1 != null && pair.Item2 != null) {
        CommaNL(); Indent(); WriteNamedString(pair.Item1, pair.Item2);
      }
    }
    NewlineAndIndentOut("}");
  }

  /// Cursor should be indented to the desired location.
  public void WriteNamedObject(string name, object value) {
    if (value == null) { throw new ArgumentException("value"); }
    if (value is long longValue) {
      WriteNamedInt(name, longValue);
    } else if (value is string stringValue) {
      WriteNamedString(name, stringValue);
    } else if (value is ExportFileReference fileValue) {
      WriteNamedFile(name, fileValue);
    } else {
      throw new ArgumentException($"Unhandled: {value.GetType()}");
    }
  }

  /// Cursor should be indented to the desired location.
  /// If URI is local, an entry will be automatically added to this.exportedFiles
  public void WriteNamedFile(string name, ExportFileReference value) {
    if (value == null) { throw new ArgumentException("value"); }
    AddExportedFile(value);
    jsonWriter.Write($"\"{name}\": {value.AsJson()}");
  }

  /// Cursor should be indented to the desired location.
  public void WriteNamedString(string name, string value) {
    if (value == null) { throw new ArgumentException("value"); }
    jsonWriter.Write($"\"{name}\": \"{value}\"");
  }

  /// Cursor should be indented to the desired location.
  public void WriteNamedInt(string name, long value) {
    jsonWriter.Write($"\"{name}\": {value}");
  }

  /// Cursor should be indented to the desired location.
  public void WriteNamedBool(string name, bool value) {
    string stringValue = value ? "true" : "false";
    jsonWriter.Write($"\"{name}\": {stringValue}");
  }

  /// Cursor should be indented to the desired location.
  public void WriteNamedFloat(string name, float value) {
    jsonWriter.Write($"\"{name}\": {value:G9}");
  }

  /// Writes a gltf object reference, which looks different in gltf 1 and 2.
  /// Cursor should be indented to the desired location.
  /// This is the preferred version.
  public void WriteNamedReference(string key, RefObj gltfObject) {
    if (gltfObject == null) { throw new ArgumentException("gltfObject"); }
    jsonWriter.Write($"\"{key}\": {SerializeReference(gltfObject)}");
  }

  /// Writes a gltf object reference, which looks different in gltf 1 and 2.
  /// Cursor should be indented to the desired location.
  /// This is the less-preferred verison, for when you only have the name and not
  /// the object pointer itself.
  public void WriteNamedReference<T>(string key, string objectName)
      where T : RefObj {
    if (objectName == null) { throw new ArgumentException("objectName"); }
    jsonWriter.Write($"\"{key}\": {SerializeReference<T>(objectName)}");
  }

  private void SanityCheckDictionary<T>(Dictionary<string, T> collection)
      where T: RefObj {
    foreach (var kvp in collection) {
      Debug.Assert(kvp.Key == kvp.Value.name, $"Mismatch {kvp.Key} {kvp.Value.name}");
    }
  }

  /// Adds all objects in *collection* to m_nameToObject.
  /// Complains (but does not throw) if multiple objects have the same name.
  private void UpdateNameToObject(IEnumerable<RefObj> collection) {
    foreach (RefObj obj in collection) {
      if (m_nameToObject.TryGetValue(obj.name, out var obj2)) {
        Debug.LogWarning($"Dupe {obj.name} -> {obj.GetType().Name} {obj2.GetType().Name}");
      } else {
        m_nameToObject[obj.name] = obj;
      }
    }
  }

  /// Given a directed graph and a set of root objects, returns the transitive closure
  /// of those objects.
  private static HashSet<RefObj> TransitiveClosure(
      IEnumerable<RefObj> roots,
      RefGraph refGraph) {
    var seen = new HashSet<RefObj>();  // all objects that have been put on the horizon
    var horizon = new Queue<RefObj>();  // objects to be expanded (FIFO)
    foreach (var root in roots) {
      if (seen.Add(root)) {
        horizon.Enqueue(root);
      }
    }
    while (horizon.Count > 0) {
      var parent = horizon.Dequeue();
      foreach (var child in refGraph[parent]) {
        if (seen.Add(child)) {
          horizon.Enqueue(child);
        }
      }
    }
    return seen;
  }

  /// Gives an index to all marked objects in *collection*.
  /// These will be the only objects written out to gltf.
  private void AssignIndicesToMarkedObjects(
      IEnumerable<RefObj> collection,
      HashSet<RefObj> marked) {
    int nextUnused = 0;
    foreach (RefObj obj in collection) {
      if (marked.Contains(obj)) {
        obj.Index = nextUnused;
        nextUnused += 1;
      }
    }
  }

  public void Write() {
    // Calculate length of binary portion
    // Memory streams are written to the binary file at the end

    long binaryBufferByteLength = 0;
    foreach (var bufferView in this.bufferViews) {
      bufferView.byteOffset = binaryBufferByteLength;
      binaryBufferByteLength += bufferView.byteLength;
    }

    //
    // Create any lists of objects that haven't already been created
    //

    // We only have a single scene, and it contains all the top-level nodes
    var scenesToWrite = new List<GlTF_Scene>() {
      new GlTF_Scene(
          this, "defaultScene",
          nodes.Select(kvp => kvp.Value).Where(n => n.Parent == null),
          extras)
    };

    var buffersToWrite = new List<GlTF_Buffer>();
    string uri = binary ? null : binFileName;
    buffersToWrite.Add(new GlTF_Buffer(this, uri) {
        m_byteLength = binaryBufferByteLength
    });
    // Patch up the bufferviews so they know how to serialize themselves
    foreach (GlTF_BufferView bv in bufferViews) {
      bv.m_buffer = buffersToWrite[0];
    }

    // For some reason, the old code didn't want to write out 0-length bufferviews.
    // TODO: I think this is bogus; if a 0-length bufferview might still be referenced.
    // _And_ I think it's redundant; the GC step will remove unreferenced objects.
    var bufferViewsToWrite = bufferViews
        .Where(bv => bv.byteLength > 0)
        .ToList();

    // Light sanity checking; more will come later

    SanityCheckDictionary(techniques);
    SanityCheckDictionary(samplers);
    SanityCheckDictionary(textures);
    SanityCheckDictionary(nodes);

    //
    // Map out the object graph, then find the transitive closure of our root object set
    // so we know what objects we don't have to write out.
    //

    m_nameToObject = new Dictionary<string, RefObj>();
    var allObjectsGroupedByType = new IReadOnlyCollection<RefObj>[] {
        buffersToWrite, cameras, accessors, bufferViewsToWrite, meshes,
        shaders, programs, techniques.Values,
        samplers.Values, textures.Values, imagesByFileRefUri.Values, materials.Values, nodes.Values,
        scenesToWrite
    };
    foreach (var col in allObjectsGroupedByType) {
      UpdateNameToObject(col);
    }

    // Map out the object graph and flood-fill to find used objects.
    // We don't really need the graph any more after this, except to do some
    // sanity checking further down.
    RefGraph refGraph; {
      var prev = jsonWriter;
      try {
        jsonWriter = null;  // helps catch bugs in IterReferences() implementations
        refGraph = m_nameToObject.Values
            .ToDictionary(refobj => refobj,
                          refobj => new HashSet<RefObj>(refobj.IterReferences()));
      } finally {
        jsonWriter = prev;
      }
    }
    HashSet<RefObj> marked = TransitiveClosure(scenesToWrite, refGraph);

    foreach (var col in allObjectsGroupedByType) {
      AssignIndicesToMarkedObjects(col, marked);
    }

    //
    // Write json
    //

    jsonWriter.Write("{\n");
    IndentIn();

    CNI.WriteNamedJObject("asset", new[] {
        ("generator", Generator),
        ("version", GltfVersion),
        ("copyright", Copyright)
    });

    WriteTopLevel(refGraph, marked, "buffers", buffersToWrite);
    WriteTopLevel(refGraph, marked, "cameras", cameras);
    WriteTopLevel(refGraph, marked, "accessors", accessors);
    WriteTopLevel(refGraph, marked, "bufferViews", bufferViewsToWrite);
    WriteTopLevel(refGraph, marked, "meshes", meshes);

    if (! Gltf2) {
      WriteTopLevel(refGraph, marked, "shaders", shaders);
      WriteTopLevel(refGraph, marked, "programs", programs);
      WriteTopLevel(refGraph, marked, "techniques", techniques?.Values);
    }

    WriteTopLevel(refGraph, marked, "samplers", samplers?.Values);
    WriteTopLevel(refGraph, marked, "textures", textures?.Values);
    WriteTopLevel(refGraph, marked, "images", imagesByFileRefUri.Values);
    WriteTopLevel(refGraph, marked, "materials", materials?.Values);
    WriteTopLevel(refGraph, marked, "nodes", nodes?.Values);

    CNI.WriteNamedReference("scene", scenesToWrite[0]);
    WriteTopLevel(refGraph, marked, "scenes", scenesToWrite);

    var rtc = RTCCenter != null && RTCCenter.Length == 3;

    {
      List<string> extUsed = new List<string>();
      if (binary && !Gltf2) {
        // This extension doesn't exist in gltf2
        extUsed.Add("KHR_binary_glTF");
      }
      if (rtc) {
        extUsed.Add("CESIUM_RTC");
      }
      if (UseTiltBrushMaterialExtension) {
        extUsed.Add(kTiltBrushMaterialExtensionName);
      }

      if (extUsed.Count > 0) {
        CNI.WriteNamedJArray("extensionsUsed", extUsed,
                             item => jsonWriter.Write($"\"{item}\""));
      }
    }

    if (rtc) {
      CommaNL();
      Indent(); jsonWriter.Write("\"extensions\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"CESIUM_RTC\": {\n");
      IndentIn();
      Indent(); jsonWriter.Write("\"center\": [\n");
      IndentIn();
      for (var i = 0; i < 3; ++i) {
        CommaNL();
        Indent(); jsonWriter.Write(RTCCenter[i]);
      }
      jsonWriter.Write("\n");
      IndentOut();
      Indent(); jsonWriter.Write("]\n");
      IndentOut();
      Indent(); jsonWriter.Write("}\n");
      IndentOut();
      Indent(); jsonWriter.Write("}");
    }

    jsonWriter.Write("\n");
    IndentOut();
    Indent(); jsonWriter.Write("}");

    uint? jsonContentLength = null;
    if (binary) {
      // The spec wants the json content length to be aligned to a multiple of 4, but here
      // we're aligning the json end position. But it doesn't matter because any headers
      // we might have are all multiples of 4 bytes, by design.
      jsonWriter.Flush();  // Good rule of thumb: flush before getting BaseStream
      const long kAlign = 4;
      long remainder = jsonWriter.BaseStream.Position % kAlign;
      long pad = (remainder == 0) ? 0 : kAlign - remainder;
      for (long i = 0; i < pad; ++i) {
        jsonWriter.Write(" ");
      }

      int jsonContentStart = kGlbHeaderSize + (b3dm ? B3DM_HEADER_SIZE : 0);
      jsonWriter.Flush();
      jsonContentLength = (uint) (jsonWriter.BaseStream.Position - jsonContentStart);
    }

    //
    // Write binary
    //

    jsonWriter.Flush();
    if (binary && Gltf2) {
      binWriter.Write((UInt32)buffersToWrite[0].m_byteLength.Value);
      binWriter.Write(kFourCC_BIN_);
    }
    foreach (var bufferView in bufferViews) {
      CopyToAndDispose(bufferView, binWriter);
    }

    if (binary) {
      uint fileLength = (uint) binWriter.BaseStream.Length;

      // write header
      binWriter.Seek(0, SeekOrigin.Begin);

      if (b3dm) {
        binWriter.Write(kFourCC_b3dm); // magic
        binWriter.Write(1); // version
        binWriter.Write(fileLength);
        binWriter.Write(0); // batchTableJSONByteLength
        binWriter.Write(0); // batchTableBinaryByteLength
        binWriter.Write(0); // batchLength
        binWriter.Flush();
      }

      binWriter.Write(kFourCC_glTF);
      binWriter.Write(Gltf2 ? 2u : 1u);  // version
      uint glbLength = (uint) (fileLength - (b3dm ? B3DM_HEADER_SIZE : 0)); // min b3dm header
      binWriter.Write(glbLength);
      binWriter.Write(jsonContentLength.Value);
      binWriter.Write(Gltf2 ? kFourCC_JSON : 0u); // format
      binWriter.Flush();
    }

    //
    // Write any subsidiary files
    //
    string gltfDir = Path.GetDirectoryName(m_outputFileName);
    foreach (ExportFileReference fileReference in m_exportedFileReferences) {
      if (fileReference.m_local) {
        if (Path.IsPathRooted(fileReference.m_uri)) {
          Debug.LogError($"Rooted path got into a local FileRef {fileReference.m_uri}");
          continue;
        }
        string destination = Path.Combine(gltfDir, fileReference.m_uri);
        if (File.Exists(destination)) {
          Debug.LogError($"Not overwriting {destination}");
          continue;
        }
        File.Copy(fileReference.m_originalLocation, destination);
        m_exportedFiles.Add(destination);
      }
    }
  }

  // Records that the passed fileReference was used in an export.
  // This causes the contents to be copied into the export directory,
  // and affects the ExportedFiles property.
  private void AddExportedFile(ExportFileReference fileReference) {
    foreach (var file2 in m_exportedFileReferences) {
      if (fileReference.m_uri == file2.m_uri) {
        if (fileReference.m_originalLocation != file2.m_originalLocation) {
          Debug.LogError(
              $"Collision: {fileReference.m_originalLocation} and {file2.m_originalLocation} " +
              $"-> {fileReference.m_uri}");
          throw new InvalidOperationException("file: output collision");
        }
        return;
      } else if (fileReference.m_local &&
                 fileReference.m_originalLocation == file2.m_originalLocation) {
        // same original location being copied to two different output locations
        Debug.LogWarning(
            $"Redundant: {fileReference.m_originalLocation} " +
            $"-> {fileReference.m_uri} and {file2.m_uri}");
      }
    }

    m_exportedFileReferences.Add(fileReference);
  }

  // Reset source.Position, copy it to destination, and close source
  // (since we've now borked its Position)
  // Flushes destination to its BaseStream.
  void CopyToAndDispose(GlTF_BufferView source, BinaryWriter destination) {
    source.stream.Position = 0;
    CopyTo(source.stream, destination, source.byteLength, copyBuffer);
    source.stream.Dispose();
    destination.Flush();
  }

  private static void CopyTo(
      Stream source, BinaryWriter destination, long numBytes, byte[] buffer) {
    long remaining = numBytes;
    while (remaining > 0) {
      int chunk = TiltBrush.MathUtils.Min(remaining, buffer.Length);
      int numRead = source.Read(buffer, 0, chunk);
      if (numRead != chunk) {
        throw new IOException("Short read");
      }
      destination.Write(buffer, 0, chunk);
      remaining -= chunk;
    }
  }

  public GlTF_BufferView GetBufferView(
      GlTF_Accessor.Type type, GlTF_Accessor.ComponentType c) {
    // The original code split these out by type, but I think all that matters
    // is the stride and whether it's an "element array" or not (ie: whether it's
    // an index or attribute buffer).
    if (c == GlTF_Accessor.ComponentType.FLOAT) {
      switch (type) {
        case GlTF_Accessor.Type.SCALAR:
          return this.m_stride4Bv;
        case GlTF_Accessor.Type.VEC2:
          return this.m_stride8Bv;
        case GlTF_Accessor.Type.VEC3:
          return this.m_stride12Bv;
        case GlTF_Accessor.Type.VEC4:
          return this.m_stride16Bv;
      }
    } else if (c == GlTF_Accessor.ComponentType.USHORT &&
               type == GlTF_Accessor.Type.SCALAR) {
      return this.m_scalarUshortElementArrayBv;
    } else if (c == GlTF_Accessor.ComponentType.UNSIGNED_BYTE &&
               type == GlTF_Accessor.Type.VEC4) {
      return m_stride4Bv;
    }
    throw new ArgumentException(
        String.Format("Unsupported accessor type {0} {1}", type, c));
  }

  // See GlTF_Accessor for the meaning of these parameters
  public GlTF_Accessor CreateAccessor(
      string n, GlTF_Accessor.Type t,
      GlTF_Accessor.ComponentType c,
      bool isNonVertexAttributeAccessor = false,
      bool normalized = false) {
    var accessor = new GlTF_Accessor(
        this, n, t, c, GetBufferView(t, c),
        isNonVertexAttributeAccessor: isNonVertexAttributeAccessor,
        normalized: normalized);
    this.accessors.Add(accessor);
    return accessor;
  }

  public GlTF_Material CreateMaterial(
      string meshNamespace, IExportableMaterial exportableMaterial) {
    Debug.Assert(! this.materials.ContainsKey(exportableMaterial));
    var mtl = new GlTF_Material(this, exportableMaterial);
    this.materials.Add(exportableMaterial, mtl);
    // Because of the namespace, we will likely not even need CreateUniqueName;
    // but it's there as a backstop.
    mtl.PresentationNameOverride = TiltBrush.ExportUtils.CreateUniqueName(
        $"{meshNamespace}_{exportableMaterial.DurableName}",
        m_materialPresentationNames);
    return mtl;
  }
}
