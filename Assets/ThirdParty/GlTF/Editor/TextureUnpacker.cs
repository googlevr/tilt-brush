#if false
// TODO: This code is currently unreferenced. We need to remove dependencies on UnityEditor
// before we can use it, or else we should discard this code.
using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

// NOTES:
// - only checks first texture and first uv

public class TextureUnpacker {

  private class Entry {
    public int left;
    public int right;
    public int top;
    public int bottom;
    public int texWidth;
    public int texHeight;
    public Dictionary<string, List<int>> subMeshMap = new Dictionary<string, List<int>>();
  }

  private class UVTransform {
    public Vector2 offset;
    public Vector2 scale;
  }

  private static Dictionary<string, Entry> entries = new Dictionary<string, Entry>(); // texture id->entry
  private static Dictionary<string, Dictionary<int, UVTransform>> meshMap = new Dictionary<string, Dictionary<int, UVTransform>>();

  private static Renderer GetRenderer(Transform tr) {
    Renderer mr = tr.GetComponent<MeshRenderer>();
    if (mr == null) {
      mr = tr.GetComponent<SkinnedMeshRenderer>();
    }
    return mr;
  }

  private static Mesh GetMesh(Transform tr) {
    var mr = GetRenderer(tr);
    Mesh m = null;
    if (mr != null) {
      var t = mr.GetType();
      if (t == typeof(MeshRenderer)) {
        MeshFilter mf = tr.GetComponent<MeshFilter>();
        m = mf.sharedMesh;
      } else if (t == typeof(SkinnedMeshRenderer)) {
        SkinnedMeshRenderer smr = mr as SkinnedMeshRenderer;
        m = smr.sharedMesh;
      }
    }
    return m;
  }

  private static List<Texture> GetTexturesFromRenderer(Renderer r) {
    var ret = new List<Texture>();

    var mats = r.sharedMaterials;
    foreach (var mat in mats) {
      if (mat == null) {
        continue;
      }
      var s = mat.shader;

      int spCount = ShaderUtil.GetPropertyCount(s);
      for (var i = 0; i < spCount; ++i) {
        var pName = ShaderUtil.GetPropertyName(s, i);
        var pType = ShaderUtil.GetPropertyType(s, i);

        if (pType == ShaderUtil.ShaderPropertyType.TexEnv) {
          var td = ShaderUtil.GetTexDim(s, i);
          if (td == UnityEngine.Rendering.TextureDimension.Tex2D) {
            var t = mat.GetTexture(pName);
            if (t != null) {
              ret.Add(t);
            }
          }
        }
      }
    }

    return ret;
  }

  private static List<Texture> GetTexturesFromMaterial(Material mat) {
    var ret = new List<Texture>();
    if (mat == null) {
      return ret;
    }

    var s = mat.shader;

    int spCount = ShaderUtil.GetPropertyCount(s);
    for (var i = 0; i < spCount; ++i) {
      var pName = ShaderUtil.GetPropertyName(s, i);
      var pType = ShaderUtil.GetPropertyType(s, i);

      if (pType == ShaderUtil.ShaderPropertyType.TexEnv) {
        var td = ShaderUtil.GetTexDim(s, i);
        if (td == UnityEngine.Rendering.TextureDimension.Tex2D) {
          var t = mat.GetTexture(pName);
          if (t != null) {
            ret.Add(t);
          }
        }
      }
    }

    return ret;
  }

#if false
  public static void CheckPackedTexture(Transform t, Preset preset) {
    var m = GetMesh(t);
    var r = GetRenderer(t);

    if (m != null && r != null) {

      for (int i = 0; i < m.subMeshCount; ++i) {
        if (m.GetTopology(i) != MeshTopology.Triangles ||
          i >= r.sharedMaterials.Length) {
          continue;
        }
        var mat = r.sharedMaterials[i];
        if (mat == null) {
          Debug.LogWarning("Failed getting shader name from material on mesh " + m.name);
          continue;
        }
        List<Preset.UnpackUV> unpackUVList = null;
        preset.unpackUV.TryGetValue(mat.shader.name, out unpackUVList);
        if (unpackUVList == null) {
          unpackUVList = preset.GetDefaultUnpackUVList();
        }

        var tris = m.GetTriangles(i);

        var uvsArr = new Vector2[][] { m.uv, m.uv2, m.uv3, m.uv4 };
        var mins = new Vector2[4];
        var maxs = new Vector2[4];
        var dxs = new float[4];
        var dys = new float[4];
        for (int k = 0; k < 4; ++k) {
          Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
          Vector2 max = new Vector2(float.MinValue, float.MinValue);

          var uvs = uvsArr[k];
          if (uvs != null && uvs.Length > 0) {
            for (int j = 0; j < tris.Length; ++j) {
              Vector2 uv = uvs[tris[j]];
              float y = 1.0f - uv.y; // flipped y
              min.x = Mathf.Min(min.x, uv.x);
              min.y = Mathf.Min(min.y, y);
              max.x = Mathf.Max(max.x, uv.x);
              max.y = Mathf.Max(max.y, y);
            }

            var dx = max.x - min.x;
            var dy = max.y - min.y;
            dxs[k] = dx;
            dys[k] = dy;
          }

          mins[k] = min;
          maxs[k] = max;
        }

        var texs = GetTexturesFromMaterial(mat);

        foreach (var unpackUV in unpackUVList) {
          foreach (var texIdx in unpackUV.textureIndex) {
            if (texIdx < texs.Count) {
              var tex = texs[texIdx];
              var name = GlTF_Texture.GetNameFromObject(tex);
              // var dx = dxs[unpackUV.index];
              // var dy = dys[unpackUV.index];
              var max = maxs[unpackUV.index];
              var min = mins[unpackUV.index];

              max.x = Mathf.Clamp01(max.x);
              max.y = Mathf.Clamp01(max.y);
              min.x = Mathf.Clamp01(min.x);
              min.y = Mathf.Clamp01(min.y);

              var tw = tex.width;
              var th = tex.height;

              var sx = Mathf.FloorToInt(min.x * tw);
              var fx = Mathf.CeilToInt(max.x * tw);
              var sy = Mathf.FloorToInt(min.y * th);
              var fy = Mathf.CeilToInt(max.y * th);
              int wx = fx - sx;
              int wy = fy - sy;

              wx = Mathf.NextPowerOfTwo(wx);
              wy = Mathf.NextPowerOfTwo(wy);

              var meshName = GlTF_Mesh.GetNameFromObject(m);
              Entry e;
              if (entries.ContainsKey(name)) {
                e = entries[name];

                //merge
                var minX = Mathf.Min(e.left, sx);
                var maxX = Mathf.Max(e.right, fx);
                var minY = Mathf.Min(e.top, sy);
                var maxY = Mathf.Max(e.bottom, fy);

                var mw = maxX - minX;
                var mh = maxY - minY;

                mw = Mathf.NextPowerOfTwo(mw);
                mh = Mathf.NextPowerOfTwo(mh);

                e.left = minX;
                e.right = maxX;
                e.top = minY;
                e.bottom = maxY;
              } else {
                e = new Entry();
                e.left = sx;
                e.right = fx;
                e.top = sy;
                e.bottom = fy;
                e.texWidth = tex.width;
                e.texHeight = tex.height;
                entries[name] = e;
              }

              List<int> subMeshId = null;
              if (e.subMeshMap.ContainsKey(meshName)) {
                subMeshId = e.subMeshMap[meshName];
              } else {
                subMeshId = new List<int>();
                e.subMeshMap[meshName] = subMeshId;
              }

              if (!subMeshId.Contains(i)) {
                subMeshId.Add(i);
              }
            }
          }
        }
      }
    }
  }
#endif

  public static void Reset() {
    entries.Clear();
    meshMap.Clear();
  }

  public static void Build() {
    var skipList = new List<string>();
    foreach (var i in entries) {
      var e = i.Value;
      var mw = e.right - e.left;
      var mh = e.bottom - e.top;
      var dx = (float) mw / (float) e.texWidth;
      var dy = (float) mh / (float) e.texHeight;

      if (dx >= 0.9 || dy >= 0.9) {
        skipList.Add(i.Key);
      }
    }

    foreach (var sl in skipList) {
      entries.Remove(sl);
    }

    foreach (var i in entries) {
      var e = i.Value;

      var mw = e.right - e.left;
      var mh = e.bottom - e.top;

      mw = Mathf.NextPowerOfTwo(mw);
      mh = Mathf.NextPowerOfTwo(mh);

      int left = e.left;
      int right = left + (mw - 1);
      if (right > e.texWidth - 1) {
        // shift left
        right = e.texWidth - 1;
        left = right - (mw - 1);
      }

      int top = e.top;
      int bottom = top + (mh - 1);
      if (bottom > e.texHeight - 1) {
        // shift up
        bottom = e.texHeight - 1;
        top = bottom - (mh - 1);
      }

      UVTransform uvt = new UVTransform();
      uvt.offset = new Vector2(-(float) left / (float) e.texWidth, -(float) top / (float) e.texHeight);
      uvt.scale = new Vector2((float) e.texWidth / (float) mw, (float) e.texHeight / (float) mh);

      foreach (var j in e.subMeshMap) {
        var list = j.Value;
        Dictionary<int, UVTransform> uvtMap = null;
        if (meshMap.ContainsKey(j.Key)) {
          uvtMap = meshMap[j.Key];
        } else {
          uvtMap = new Dictionary<int, UVTransform>();
          meshMap[j.Key] = uvtMap;
        }

        foreach (var subMeshIdx in list) {
          uvtMap[subMeshIdx] = uvt;
        }
      }
    }
  }

  public static Texture2D ProcessTexture(string name, Texture2D tex) {
    if (entries.ContainsKey(name)) {
      Entry e = entries[name];

      var mw = e.right - e.left;
      var mh = e.bottom - e.top;

      mw = Mathf.NextPowerOfTwo(mw);
      mh = Mathf.NextPowerOfTwo(mh);

      int left = e.left;
      int right = left + (mw - 1);
      if (right > e.texWidth - 1) {
        // shift left
        right = e.texWidth - 1;
        left = right - (mw - 1);
      }

      int top = e.top;
      int bottom = top + (mh - 1);
      if (bottom > e.texHeight - 1) {
        // shift up
        bottom = e.texHeight - 1;
        top = bottom - (mh - 1);
      }

      // flip top & bottom
      var ftop = e.texHeight - 1 - top;
      var fbottom = e.texHeight - 1 - bottom;
      top = fbottom;
      bottom = ftop;

      var src = tex.GetPixels32();
      var dst = new Color32[mw * mh];

      for (int i = 0; i < mh; ++i) {
        for (int j = 0; j < mw; ++j) {
          var dstIdx = i * mw + j;
          var srcIdx = (top + i) * tex.width + (left + j);
          dst[dstIdx] = src[srcIdx];
        }
      }

      Texture2D t = new Texture2D(mw, mh, TextureFormat.RGBA32, false);
      t.SetPixels32(dst);
      t.Apply();

      return t;
    }
    return tex;
  }
}
#endif

