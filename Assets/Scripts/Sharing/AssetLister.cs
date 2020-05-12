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
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace TiltBrush {

/// Class that holds page state while calling Poly ListAssets.
public class AssetLister {
  private string m_Uri;
  private string m_ErrorMessage;
  private string m_PageToken;

  public bool HasMore { get { return m_PageToken != null; } }

  public AssetLister(string uri, string errorMessage) {
    m_Uri = uri;
    m_ErrorMessage = errorMessage;
  }

  public IEnumerator<object> NextPage(List<PolySceneFileInfo> files) {
    string uri = m_PageToken == null ? m_Uri
        : String.Format("{0}&page_token={1}", m_Uri, m_PageToken);

    WebRequest request = new WebRequest(uri, App.GoogleIdentity, UnityWebRequest.kHttpVerbGET);
    using (var cr = request.SendAsync().AsIeNull()) {
      while (!request.Done) {
        try {
          cr.MoveNext();
        } catch (VrAssetServiceException e) {
          e.UserFriendly = m_ErrorMessage;
          throw;
        }
        yield return cr.Current;
      }
    }

    Future<JObject> f = new Future<JObject>(() => JObject.Parse(request.Result));
    JObject json;
    while (!f.TryGetResult(out json)) { yield return null; }

    var assets = json["assets"];
    if (assets != null) {
      foreach (var asset in assets) {
        var info = new PolySceneFileInfo(asset);
        info.Author = asset["displayName"].ToString();;
        files.Add(info);
      }
    }
    JToken jPageToken = json["nextPageToken"];
    m_PageToken = jPageToken != null ? jPageToken.ToString() : null;
  }

  public IEnumerator<Null> NextPage(List<PolyAssetCatalog.AssetDetails> files,
      string thumbnailSuffix) {
    string uri = m_PageToken == null ? m_Uri
        : String.Format("{0}&page_token={1}", m_Uri, m_PageToken);

    WebRequest request = new WebRequest(uri, App.GoogleIdentity, UnityWebRequest.kHttpVerbGET);
    using (var cr = request.SendAsync().AsIeNull()) {
      while (!request.Done) {
        try {
          cr.MoveNext();
        } catch (VrAssetServiceException e) {
          e.UserFriendly = m_ErrorMessage;
          throw;
        }
        yield return cr.Current;
      }
    }
    Future<JObject> f = new Future<JObject>(() => JObject.Parse(request.Result));
    JObject json;
    while (!f.TryGetResult(out json)) { yield return null; }

    if (json.Count == 0) { yield break; }

    JToken lastAsset = null;
    var assets = json["assets"] ?? json["userAssets"];
    foreach (JToken possibleAsset in assets) {
      try {
        // User assets are nested in an 'asset' node.
        JToken asset = possibleAsset["asset"] ?? possibleAsset;
        if (asset["visibility"].ToString() == "PRIVATE") {
          continue;
        }

        // We now don't filter the liked Poly objects, but we don't want to return liked Tilt Brush
        // sketches so in this section we filter out anything with a Tilt file in it.
        // Also, although currently all Poly objects have a GLTF representation we should probably
        // not rely on that continuing, so we discard anything that doesn't have a GLTF (1)
        // representation. We look for PGLTF and GLTF as for a lot of objects Poly is returning
        // PGLTF without GLTF.
        bool skipObject = false;
        foreach (var format in asset["formats"]) {
          var formatType = format["formatType"].ToString();
          if (formatType == "TILT") {
            skipObject = true;
            break;
          }
        }
        if (skipObject) {
          continue;
        }
        lastAsset = asset;
        string accountName = asset["authorName"]?.ToString() ?? "Unknown";
        files.Add(new PolyAssetCatalog.AssetDetails(asset, accountName, thumbnailSuffix));
      } catch (NullReferenceException) {
        UnityEngine.Debug.LogErrorFormat("Failed to load asset: {0}",
            lastAsset == null ? "NULL" : lastAsset.ToString());
      }
      yield return null;
    }

    JToken jPageToken = json["nextPageToken"];
    m_PageToken = jPageToken != null ? jPageToken.ToString() : null;
  }
}
}  // namespace TiltBrush
