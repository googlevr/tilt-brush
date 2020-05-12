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
using UnityEngine.Networking;
using System;
using System.Collections;
using System.IO;

using Newtonsoft.Json.Linq;

namespace TiltBrush {

/**
 * Why not use Google's .NET API packages?  Because they need .NET 4
 *
 * TODO:
 * - Implement a uploadHandler that reads from the file rather than reading it all into an array
 **/
class YouTube : MonoBehaviour {
  // Some fields are intentionally unused because we may want them in the future.

  [SerializeField] private string m_UploadUri;
  // ReSharper disable once NotAccessedField.Local
  [SerializeField] private string m_ChannelListUri;
  // ReSharper disable once NotAccessedField.Local
  [SerializeField] private string m_UserInfoUri;
  // ReSharper disable once NotAccessedField.Local
  [SerializeField] private string m_RevokeUri;

  [SerializeField] private string m_UploadedMessage;
  // ReSharper disable once NotAccessedField.Local
  [SerializeField] private string m_AuthorizeMessage;
  [SerializeField] private string m_DefaultTitle;
  [SerializeField] private string m_DefaultDescription;

  public static YouTube m_Instance;

  private int m_UploadsInProgress = 0;
  private FileStream m_UploadStream;

  public bool AuthNeeded {
    get { return !App.GoogleIdentity.HasAccessToken; }
  }

  public bool InProgress {
    get { return m_UploadsInProgress > 0; }
  }

  public void Awake() {
    m_Instance = this;
  }

  void OnDestroy() {
    if (m_UploadStream != null) {
      m_UploadStream.Close();
      m_UploadStream = null;
    }
  }

  public IEnumerator ShareVideo(string filename) {
    m_UploadsInProgress++;

    if (!App.GoogleIdentity.LoggedIn) {
      // Need user to login
      yield return App.GoogleIdentity.ReauthorizeAsync().AsIeNull();
    }
    FileInfo fileInfo = new FileInfo(filename);
    long filesize = fileInfo.Length;
    if (filesize == 0) {
      OutputWindowScript.Error("Video processing failed");
      m_UploadsInProgress--;
      yield break;
    }

    string uploadUrl = null;

    using (UnityWebRequest www = InitResumableUpload(filename, filesize)) {
      yield return App.GoogleIdentity.Authenticate(www).AsIeNull();
      yield return www.SendWebRequest();
      if (www.isNetworkError) {
        OutputWindowScript.Error("Network error");
        m_UploadsInProgress--;
        yield break;
      }

      if (www.responseCode == 200) {
        uploadUrl = www.GetResponseHeader("Location");
      } else if (www.responseCode == 401) {
        OutputWindowScript.Error("Authorization failed");
        m_UploadsInProgress--;
        yield break;
      } else if (www.responseCode == 403) {
        OutputWindowScript.Error(
            "Upload forbidden.\nYou may need to create a YouTube channel first.");
        m_UploadsInProgress--;
        yield break;
      } else {
        OutputWindowScript.Error(String.Format("Error pushing to YouTube: {0}", www.responseCode));
        m_UploadsInProgress--;
        yield break;
      }
    }

    if (uploadUrl == null) {
      OutputWindowScript.Error(
          "Upload failed.\nYou may need to create a YouTube channel first.");
      m_UploadsInProgress--;
      yield break;
    }

    m_UploadStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
    byte[] buffer = new byte[filesize];
    m_UploadStream.Read(buffer, 0, (int)filesize);
    using (UnityWebRequest www = UnityWebRequest.Put(uploadUrl, buffer)) {
      www.SetRequestHeader("Content-Type", "video/*");
      yield return App.GoogleIdentity.Authenticate(www).AsIeNull();
      yield return www.SendWebRequest();

      m_UploadStream.Close();
      m_UploadStream = null;

      if (www.isNetworkError) {
        OutputWindowScript.Error("Network error");
        m_UploadsInProgress--;
        yield break;
      } else if (www.responseCode >= 400) {
        // Some kind of error - extract the error message
        while (!www.downloadHandler.isDone) { yield return null; }
        JObject json = JObject.Parse(www.downloadHandler.text);
        string message = json["message"].ToString();
        OutputWindowScript.Error(String.Format("YouTube error: {0}", message));
        m_UploadsInProgress--;
        yield break;
      }
    }

    App.OpenURL("http://www.youtube.com/my_videos");
    OutputWindowScript.m_Instance.CreateInfoCardAtController(InputManager.ControllerName.Brush,
        m_UploadedMessage);
    m_UploadsInProgress--;
  }

  private UnityWebRequest InitResumableUpload(string filename, long size) {
    string uri = String.Format("{0}?uploadType=resumable&part=snippet,status,contentDetails",
      m_UploadUri);
    string title = String.Format("{0} {1}", m_DefaultTitle, DateTime.Now.ToString("g"));
    string json = String.Format("{{\n"
      + "  \"snippet\": {{\n"
      + "    \"title\": \"{0}\",\n"
      + "    \"description\": \"{1}\",\n"
      + "    \"tags\": [\"tiltbrush\"]\n"
      + "  }},\n"
      + "  \"status\": {{\n"
      + "    \"privacyStatus\": \"private\"\n"
      + "  }}\n"
      + "}}", title, m_DefaultDescription);
    UnityWebRequest www = new UnityWebRequest(uri);
    www.method = UnityWebRequest.kHttpVerbPOST;
    UploadHandler uploader = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
    www.uploadHandler = uploader;
    // We don't set a download handler, due to a bug in Unity
    // https://support.unity3d.com/hc/en-us/requests/428539
    www.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
    www.SetRequestHeader("X-Upload-Content-Length", size.ToString());
    www.SetRequestHeader("X-Upload-Content-Type", "video/*");
    return www;
  }
}

}  // namespace TiltBrush
