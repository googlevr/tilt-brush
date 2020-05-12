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
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush {
class SketchfabService {
  public const string kModelLandingPage = "https://sketchfab.com/3d-models/";
  const string kApiHost = "https://api.sketchfab.com";

  /// A paginated response, for use with GetNextPageAsync()
  public interface Paginated {
    /// URI to the next page of results, or null
    string NextUri { get; }
    /// URI to the previous page of results, or null
    string PreviousUri { get; }
  }

  // Classes named "Related" seem to be shared between multiple different response types.
  // My guess is "related" is meant in the database-join sense of the word, and each of
  // these is its own table in the backend.

  // XxxRelated classes are listed here in alphabetical order.
  // Classes specific to an API call are found right after the API wrapper.
  // This violates conventions but is more convenient.

  [Serializable, UsedImplicitly] public class AvatarRelated {
    [Serializable, UsedImplicitly] public class inline_model {
      public string url;
      public int width;
      public int height;
      public int size;
    }
    public inline_model[] images;
    public string uri;
  }

  [Serializable, UsedImplicitly] public class TagRelated {
    public string slug;
    public string uri;
  }

  [Serializable, UsedImplicitly] public class ThumbnailsRelated {
    [Serializable, UsedImplicitly] public class inline_model_2 {
      public string url;
      public int width;
      public int size;
      public string uid;
      public int height;
    }
    public inline_model_2[] images;
  }

  [Serializable, UsedImplicitly] public class UserRelated {
    public string username;
    public string profileUrl;
    public string account;
    public string displayName;
    public string uid;
    public AvatarRelated[] avatars;
    public string uri;
  }

  /// Options are constructed according to:
  ///   https://docs.sketchfab.com/data-api/v3/index.html#!/models/patch_v3_models_uid_options
  [Serializable, UsedImplicitly] public class Options {
    // shading (string, optional),
    public Dictionary<string, string> background;
    // orientation (string, optional),

    public void SetBackgroundColor(Color color) {
      background = new Dictionary<string, string>();
      background["color"] = "#" + ColorUtility.ToHtmlStringRGB(color);
    }
  }

  private readonly OAuth2Identity m_identity;

  public SketchfabService(OAuth2Identity identity) {
    m_identity = identity;
  }

  /// Returns null if there is no next page.
  /// It's assumed that you've used the results from the current page, meaning
  /// that the task for fetching the current page must be completed.
  public Task<T> GetNextPageAsync<T>(Task<T> task) where T : Paginated {
    // Ensure task.Result does not block.
    // If this function were async we could await, but then couldn't return null.
    // In C# 8 this could be switched to use async enumerators.
    if (!task.IsCompleted) {
      throw new ArgumentException("page");
    }
    var uri = task.Result.NextUri;
    if (string.IsNullOrEmpty(uri)) { return null; }
    return new WebRequest(uri, m_identity, "GET")
        .SendAsync()
        .ContinueWith(antecedent => antecedent.Result.Deserialize<T>());
  }

  public async Task<MeDetail> GetUserInfo() {
    var result = await new WebRequest($"{kApiHost}/v3/me", m_identity, "GET").SendAsync();
    return result.Deserialize<MeDetail>();
  }
  [Serializable, UsedImplicitly] public class MeDetail {
    // subscriptionCount (integer, optional),
    // followerCount (integer, optional),
    public string uid;
    public string modelsUrl;
    // likeCount (integer, optional),
    // facebookUsername (string, optional),
    // biography (string, optional),
    // city (string, optional),
    // tagline (string, optional),
    public int modelCount;
    // twitterUsername (string, optional),
    public string email;
    public string website;
    // billingCycle (string, optional),
    // followersUrl (string, optional),
    // collectionCount (integer, optional),
    // dateJoined (string, optional),
   public string account;
   public string displayName;
   public string profileUrl;
    // followingsUrl (string, optional),
    // skills (Array[SkillDetail], optional),
    // country (string, optional),
    public string uri;
    // apiToken (string, optional),
    public string username;
    // linkedinUsername (string, optional),
    // likesUrl (string, optional),
    public AvatarRelated avatar;
    public bool isLimited;
    // followingCount (integer, optional),
    // collectionsUrl (string, optional)
  }


  /// Returns the metadata for a model.
  /// See also GetModelDownload().
  public async Task<ModelDetail> GetModelDetail(string uid) {
    var result = await new WebRequest(
        $"{kApiHost}/v3/models/{uid}", m_identity, "GET").SendAsync();
    return result.Deserialize<ModelDetail>();
  }
  [Serializable, UsedImplicitly] public class ModelDetail {
    [Serializable, UsedImplicitly] public class File {
      public int wireframeSize;
      public int flag;
      public string version;
      public int modelSize;
      public string uri;
      public int osgjsSize;
      public JObject metadata;
    }
    public JObject status;
    public File[] files;
    public string uid;
    public TagRelated[] tags;
    public string viewerUrl;
    // categories (Array[CategoriesRelated], optional),
    public string publishedAt;
    public int likeCount;
    public int commentCount;
    public int vertexCount;
    public UserRelated user;
    // animationCount (integer, optional),
    public bool isDownloadable;
    public string description;
    // viewCount (integer, optional),
    public string name;
    public JObject license;  // license (object, optional),
    public string editorUrl;
    // soundCount (integer, optional),
    // isAgeRestricted (boolean, optional),
    public string uri;
    public int faceCount;
    // ext (string,null, optional),
    // staffpickedAt (string,null, optional),
    // createdAt (string, optional),
    public ThumbnailsRelated thumbnails;
    // downloadCount (integer, optional),
    // embedUrl (string, optional),
    public JObject options;  // options (object, optional)
  }


  /// Returns a temporary URL where a model can be downloaded from
  public async Task<ModelDownload> GetModelDownload(string uid) {
    var result = await new WebRequest(
        $"{kApiHost}/v3/models/{uid}/download", m_identity, "GET").SendAsync();
    return result.Deserialize<ModelDownload>();
  }
  [Serializable, UsedImplicitly] public class ModelDownload {
    [Serializable, UsedImplicitly] public class inline_model_1 {
      public string url;  // temporary URL where the archive can be downloaded
      public int expires;  // when the temporary URL will expire (in seconds)
    }
    // This is called "gltf" but it's actually just the .zip archive that was uploaded, I believe
    public inline_model_1 gltf;
  }


  public Task<ModelLikesResponse> GetMeLikes() {
    return new WebRequest($"{kApiHost}/v3/me/likes", m_identity, "GET")
        .SendAsync()
        .ContinueWith(antecedent => antecedent.Result.Deserialize<ModelLikesResponse>());
  }
  // Documentation for this API is incorrect, so this was created by inspection of the result
  // (and therefore may itself have errors)
  [Serializable, UsedImplicitly] public class ModelLikesResponse : Paginated {
    [Serializable, UsedImplicitly] public class ModelLikesList {
      public string uid;
      // public TagRelated[] tags;
      public string viewerUrl;
      public bool isProtected;
      // categories (Array[CategoriesRelated], optional),
      // public string publishedAt;
      // public int likeCount;
      // public int commentCount;
      // public int viewCount;
      public int vertexCount;
      public UserRelated user;
      public bool isDownloadable;
      public string description;
      // public int animationCount;
      public string name;
      // public int soundCount;
      // public bool isAgeRestricted;
      public string uri;
      public int faceCount;
      // staffpickedAt (string,null, optional),
      public ThumbnailsRelated thumbnails;
      // public string embedUrl;

      // public CategoriesRelated categories;
      // public string createdAt;
      // public string license;  // not documented and sometimes null?
      // public ?string? price;
    }
    public string previous;  // uri
    public string next;  // uri
    public ModelLikesList[] results;

    string Paginated.NextUri => next;
    string Paginated.PreviousUri => previous;
  }

  // TODO: /v3/search and /v3/me/search?
  // The parameters to the search functions are the same, except /v3/me/search omits
  // the "username" parameter, so maybe we can have a single function which wraps both.

  //
  /// Pass:
  ///   temporaryDirectory - if passed, caller is responsible for cleaning it up
  public async Task<CreateResponse> CreateModel(
      string name,
      string zipPath, IProgress<double> progress, CancellationToken token,
      Options options=null, string temporaryDirectory=null) {

    // No compression because it's a compressed .zip already
    WebRequest uploader = new WebRequest(
        $"{kApiHost}/v3/models", m_identity, "POST", compress: false);

    var moreParams = new List<(string, string)> {
        ("name", name),
        ("source", "tilt-brush"),
        ("private", "true"),  // TODO: remove when this feature is not secret
        // https://docs.sketchfab.com/data-api/v3/index.html#!/models/post_v3_models
        // "Enables 2D view in model inspector. All downloadable models must have isInspectable
        // enabled."
        ("isInspectable", "true"),
        // ??? https://docs.sketchfab.com/data-api/v3/index.html#!/licenses/get_v3_licenses
        ("license", "by-sa"),
        // This is how you specify multiple tags:
        // ("tags", "[\"tiltbrush\", \"some-other-tag\"]"),
        ("tags", "tiltbrush"),
        ("isPublished", "false"),
        // ("description", "Dummy description"),
    };
    if (options != null) {
      moreParams.Add(("options", JsonConvert.SerializeObject(options)));
    }
    uploader.ProgressObject = progress;
    var reply = await uploader.SendNamedDataAsync(
        "modelFile", File.OpenRead(zipPath), Path.GetFileName(zipPath), "application/zip",
        moreParams: moreParams, token, temporaryDirectory);
    return reply.Deserialize<CreateResponse>();
  }
  [Serializable, UsedImplicitly] public struct CreateResponse {
    public string uid;
    public string uri;
  }
}
} // namespace TiltBrush