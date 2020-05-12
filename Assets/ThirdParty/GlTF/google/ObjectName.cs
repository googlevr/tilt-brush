/// Doesn't do much anymore.
public class ObjectName {
  // A string intended to be used for a gltf1 object name; unique.
  private string m_name;

  public ObjectName(string name) {
    this.m_name = name;
  }

  public string Name {
    get { return m_name; }
    set { m_name = value; }
  }

  // Returns a string suitable for use within a gltf file as a name.
  public string ToGltf1Name() {
    // nb: none of this is necessary; the gltf name ("id" in the doc below) can be any string
    // ref: https://github.com/KhronosGroup/glTF/tree/master/specification/1.0#ids-and-names
    string ret = Name;
    ret = ret.Replace(" ", "_");
    ret = ret.Replace("/", "_");
    ret = ret.Replace("\\", "_");
    return ret;
  }
}
