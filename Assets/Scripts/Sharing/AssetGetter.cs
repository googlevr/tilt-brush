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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush {

/// Class that assists in getting a PolyRawAsset from Poly.
public class AssetGetter {
  private bool m_Ready;
  private string m_URI;
  private PolyRawAsset m_Asset;
  private JsonSerializer m_JsonSerializer;

  /// Converts a property from snake case to camel case.
  public class SnakeToCamelPropertyNameContractResolver : DefaultContractResolver {

    protected override JsonProperty CreateProperty(MemberInfo member,
        MemberSerialization memberSerialization) {
      JsonProperty jsonProperty = base.CreateProperty(member, memberSerialization);
      jsonProperty.PropertyName = SnakeToCamelCase(jsonProperty.PropertyName);
      return jsonProperty;
    }

    string SnakeToCamelCase(string s) {
      int index = s.IndexOf("_");
      while (index != -1) {
        string toHere = s.Substring(0, index);
        string upper = s.Substring(index + 1, 1);
        string theRest = s.Substring(index + 2);

        s = toHere + upper.ToUpper() + theRest;
        index = s.IndexOf("_");
      }
      return s;
    }
  }

  public bool IsCanceled {
    get;
    set;
  }

  public bool IsReady {
    get { return m_Ready; }
  }

  public PolyRawAsset Asset {
    get { return m_Asset; }
  }

  public string Reason { get; }

  public AssetGetter(string uri, string assetId, VrAssetFormat assetType,
                     string reason) {
    m_URI = uri;
    m_Asset = new PolyRawAsset(assetId, assetType);
    m_JsonSerializer = new JsonSerializer();
    m_JsonSerializer.ContractResolver = new SnakeToCamelPropertyNameContractResolver();
    Reason = reason;
  }

  // Initiates the contact with Poly.
  public IEnumerator<Null> GetAssetCoroutine() {
    if (string.IsNullOrEmpty(App.Config.GoogleSecrets?.ApiKey)) {
      IsCanceled = true;
      yield break;
    }
    m_Ready = false;

    WebRequest request = new WebRequest(m_URI, App.GoogleIdentity, UnityWebRequest.kHttpVerbGET);
    using (var cr = request.SendAsync().AsIeNull()) {
      while (!request.Done) {
        try {
          cr.MoveNext();
        } catch (VrAssetServiceException e) {
          Debug.LogException(e);
          IsCanceled = true;
          yield break;
        }
        yield return cr.Current;
      }
    }

    // Deserialize request string in to an Asset class.
    MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(request.Result));
    using (var jsonReader = new JsonTextReader(new StreamReader(memStream))) {
      Future<JObject> f = new Future<JObject>(() => JObject.Parse(request.Result));
      JObject json;
      while (!f.TryGetResult(out json)) { yield return null; }

      if (json.Count == 0) {
        Debug.LogErrorFormat("Failed to deserialize response for {0}", m_URI);
        yield break;
      }

      // Find the asset by looking through the format list for the specified type.
      VrAssetFormat requestedType = m_Asset.DesiredType;

      while (true) {
        var format = json["formats"]?.FirstOrDefault(x =>
            x["formatType"]?.ToString() == requestedType.ToString());
        if (format != null) {
          string internalRootFilePath = format["root"]?["relativePath"].ToString();
          // If we successfully get a gltf2 format file, internally change the extension to
          // "gltf2" so that the cache knows that it is a gltf2 file.
          if (requestedType == VrAssetFormat.GLTF2) {
            internalRootFilePath = Path.ChangeExtension(internalRootFilePath, "gltf2");
          }

          // Get root element info.
          m_Asset.SetRootElement(
            internalRootFilePath,
            format["root"]?["url"].ToString());

          // Get all resource infos.  There may be zero.
          foreach (var r in format["resources"]) {
            string path = r["relativePath"].ToString();
            m_Asset.AddResourceElement(path, r["url"].ToString());

            // The root element should be the only gltf file.
            Debug.Assert(!path.EndsWith(".gltf") && !path.EndsWith(".gltf2"),
                         string.Format("Found extra gltf resource: {0}", path));
          }
          break;
        } else {
          // We asked for an asset in a format that it doesn't have.
          // In some cases, we should look for a different format as backup.
          if (requestedType == VrAssetFormat.GLTF2) {
            requestedType = VrAssetFormat.GLTF;
          } else {
            // In other cases, we should fail and get out.
            Debug.LogErrorFormat("Can't download {0} in {1} format.", m_Asset.Id, m_Asset.DesiredType);
            yield break;
          }
        }
      }
    }

    // Download root asset.
    request = new WebRequest(m_Asset.RootDataURL, App.GoogleIdentity);
    using (var cr = request.SendAsync().AsIeNull()) {
      while (!request.Done) {
        try {
          cr.MoveNext();
        } catch (VrAssetServiceException e) {
          Debug.LogErrorFormat("Error downloading {0} at {1}\n{2}",
            m_Asset.Id, m_Asset.RootDataURL, e);
          yield break;
        }
        yield return cr.Current;
      }
    }
    m_Asset.CopyBytesToRootElement(request.ResultBytes);

    // Download all resource assets.
    foreach (var e in m_Asset.ResourceElements) {
      request = new WebRequest(e.dataURL, App.GoogleIdentity);
      using (var cr = request.SendAsync().AsIeNull()) {
        while (!request.Done) {
          try {
            cr.MoveNext();
          } catch (VrAssetServiceException ex) {
            Debug.LogErrorFormat("Error downloading {0} at {1}\n{2}",
              m_Asset.Id, m_Asset.RootDataURL, ex);
            e.assetBytes = null;
            yield break;
          }
          yield return cr.Current;
        }
      }
      e.assetBytes = request.ResultBytes;
    }

    m_Ready = true;
  }
}

}  // namespace TiltBrush
