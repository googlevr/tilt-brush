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
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using CompressionLevel = System.IO.Compression.CompressionLevel;

using JObject = Newtonsoft.Json.Linq.JObject;

namespace TiltBrush {

public class VrAssetServiceException : Exception {
  /// High-level, user-friendly summary of the problem.
  public string UserFriendly { get; set; }

  public VrAssetServiceException(string userFriendly, Exception inner = null)
    : base(userFriendly, inner) {
    UserFriendly = userFriendly;
  }

  public VrAssetServiceException(string userFriendly, string details, Exception inner = null)
    : base(string.Format("{0}\n{1}", userFriendly, details), inner) {
    UserFriendly = userFriendly;
  }
}

/// Sends an authenticated web request and store the returned text.
/// Refreshes OAuth token if required.
/// If an IsTransientServerError() occurs it will retry.
public class WebRequest {
  /// Helper that makes the web request reply easier to consume.
  public struct Reply {
    private byte[] bytes;
    // Any or all of these properties might return null
    public byte[] Raw => bytes;
    public string Text => bytes == null ? null : Encoding.UTF8.GetString(bytes);
    public JObject JObject => bytes == null ? null : JObject.Parse(Text);
    public Reply(byte[] bytes) { this.bytes = bytes; }
    public T Deserialize<T>() {
      var jsonSerializer = JsonSerializer.CreateDefault();
      using (JsonTextReader jsonTextReader = new JsonTextReader(
          new StreamReader(new MemoryStream(this.bytes)))) {
        return jsonSerializer.Deserialize<T>(jsonTextReader);
      }
    }
  }

  static bool kEnableHttpCompression = true;  // not const to avoid dumb compiler warning
  private static readonly string[] kInterestingRequestHeaders =
      { "Authorization", "Content-Type", "Content-Encoding" };
  const int kNumRetries = 3;
  const int kDebugRequestLogLength = 2000;
  static int sm_DebugRequestNextId = 0;
  static Encoding sm_StrictAscii = Encoding.GetEncoding(
      "us-ascii", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);

  // Returns a log filename that can be written to, or null if logging is disabled.
  // For internal use.
  private static string RequestDebugGetLogFile(
      int id, string description, string ext, int retryIndex=0) {
    if (!App.Config.m_DebugWebRequest) { return null; }
#if UNITY_EDITOR
    string dir = "Requests";
#else
    string dir = Path.Combine(Application.temporaryCachePath, "Requests");
#endif
    FileUtils.InitializeDirectory(dir);
    string niceDescription = description?.Replace("/", "_").Replace("\\", "_").Replace(":", "");
    if (retryIndex > 0) {
      return Path.Combine(dir, $"{id}_r{retryIndex}_{niceDescription}.{ext}");
    } else {
      return Path.Combine(dir, $"{id}_{niceDescription}.{ext}");
    }
  }

  /// Returns a unique id to be used with the RequestDebugLog{File,Text,Bytes} functions.
  private static int RequestDebugGetNewId() {
    var id = sm_DebugRequestNextId;
    sm_DebugRequestNextId = (sm_DebugRequestNextId + 1) % kDebugRequestLogLength;
    return id;
  }

  /// Copies a file into the request debug log.
  private static void RequestDebugLogFile(int id, string description, string sourceFile) {
    if (RequestDebugGetLogFile(id, description, "file") is string destinationFile) {
      try {
        File.Copy(sourceFile, destinationFile, true);
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }
  }

  /// Writes some text into the request debug log.
  private static void RequestDebugLogText(
      int id, string description, string text, int retryIndex=0) {
    if (text == null) { return; }
    if (RequestDebugGetLogFile(id, description, "txt", retryIndex) is string destinationFile) {
      try {
        File.WriteAllText(destinationFile, text);
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }
  }

  /// Writes some bytes into the request debug log.
  private static void RequestDebugLogBytes(
      int id, string description, byte[] bytes, int retryIndex=0) {
    if (RequestDebugGetLogFile(id, description, "bytes", retryIndex) is string destinationFile) {
      try {
        File.WriteAllBytes(destinationFile, bytes);
      } catch (Exception e) {
        Debug.LogException(e);
      }
    }
  }

  static bool IsTransientServerError(long httpStatus) {
    // Should this 503 and 500 only?
    return httpStatus / 100 == 5;
  }

  static bool IsAuthError(long httpStatus) {
    return httpStatus == 401;
  }

  static bool IsSuccess(long httpStatus) {
    return httpStatus / 100 == 2;
  }

  static bool IsForbiddenError(long httpStatus) {
    return httpStatus == 403;
  }

  /// Returns a writable stream that writes to filename.
  /// If compress=true, the file will be written compressed.
  private static Stream OpenFileForWrite(string filename, bool compress) {
    Stream bareFile = File.Open(filename, FileMode.CreateNew, FileAccess.Write);
    if (! compress) {
      return bareFile;
    } else {
      return new GZipStream(bareFile, CompressionLevel.Optimal);
    }
  }

  // URIs don't contain any PII, but they cause a lot of unique entries in our analytics.
  // Throw away unnecessary information to collapse them.
  public static string RedactUriForError(string uriString) {
#if DEBUG
    return uriString;
#else
    // Messy, but lots of different exceptions can be raised, and this method needs to be robust.
    try {
      // Can raise FormatException.
      Uri uri = new Uri(uriString);
      // Can raise InvalidOperationException (if not absolute), maybe others.
      return string.Format("{0}://{1}:{2}/...", uri.Scheme, uri.Host, uri.Port);
    } catch (Exception) {
      return uriString;
    }
#endif
  }

  /// Convenience method
  public static Task<Reply> GetAsync(string uri) {
    return new WebRequest(uri, App.GoogleIdentity, UnityWebRequest.kHttpVerbGET)
        .SendAsync();
  }

  private string m_Uri;
  private string m_Method;
  private OAuth2Identity m_Identity;
  private byte[] m_Result = null;
  private int m_UploadedBytes = 0;
  private float? m_PreUploadProgress = null;
  private float m_Progress = 0;
  private bool m_Done = false;

  // Now that we use the well-supported gzip (rather than poorly-supported "deflate")
  // this should be safe to use for all services.
  private bool m_Compressed = false;

  // Filename being sent, for more useful log messages; null if not from a file
  private string m_UploadDescription;

  public bool Done {
    get { return m_Done; }
  }

  public string Result {
    get { return m_Result != null ? System.Text.Encoding.UTF8.GetString(m_Result) : null; }
  }

  public byte[] ResultBytes {
    get { return m_Result; }
  }

  public int UploadedBytes {
    get { return m_UploadedBytes; }
  }

  // Upload progress in range [0.0, 1.0] (does not take compression time into account)
  public float Progress {
    get {
      if (m_PreUploadProgress is float preUploadProgress) {
        // I plucked these numbers mostly out of the air
        float weight = m_Compressed ? 0.4f : 0.2f;
        return weight * preUploadProgress + (1-weight) * m_Progress;
      } else {
        return m_Progress;
      }
    }
  }

  public IProgress<double> ProgressObject { get; set; }

  // identity may be null, in which case no authentication takes place
  public WebRequest(string uri, OAuth2Identity identity,
      string method = UnityWebRequest.kHttpVerbGET, bool compress = false) {
    if (string.IsNullOrEmpty(uri)) {
      throw new ArgumentException("uri");
    }
    if (!kEnableHttpCompression) {
      compress = false;
    }
    m_Method = method;
    m_Compressed = compress;
    m_Uri = uri;
    m_Identity = identity;
  }

  /// Sends a multipart form that includes a parameter with data that comes from a stream.
  /// This method takes ownership of the stream and guarantees that it will be closed.
  /// Pass:
  ///   parameterName, srcData, destFilename, contentType -
  ///      Data bundled into one of the form parameters.
  ///   IEnumerable<(string, string)> - optional other form parameters as (key, value) pairs
  ///   temporaryDirectory - optional; if passed, caller is responsible for cleaning it up
  public async Task<Reply> SendNamedDataAsync(
      string parameterName, Stream srcData, string destFilename, string contentType,
      [CanBeNull] IEnumerable<(string key, string val)> moreParams,
      CancellationToken? token,
      string temporaryDirectory=null) {
    string temporaryFileName = null;
    try {
      m_UploadDescription = destFilename;
      var (prefix, suffix, wrapperType) = SerializeMultipartForm(
          parameterName, destFilename, contentType,
          moreParams);

      temporaryFileName = FileUtils.GenerateNonexistentFilename(
          temporaryDirectory ?? Path.Combine(Application.temporaryCachePath, "WebRequest"),
          Path.GetFileName(destFilename), ".form.tmp");
      if (! FileUtils.InitializeDirectoryWithUserError(Path.GetDirectoryName(temporaryFileName))) {
        return new Reply(null);
      }

      using (Stream outputStream = OpenFileForWrite(temporaryFileName, m_Compressed)) {
        outputStream.Write(prefix, 0, prefix.Length);

        long srcLength = srcData.Length;
        // No cancellation token because the copy will self-destruct when the using() blocks exit.
        Task task = srcData.CopyToAsync(outputStream, 81920);
        // Use task.Wait() so exceptions and task cancellation get propagated.
        while (!task.Wait(0)) {
          m_PreUploadProgress = Mathf.Clamp01((srcData.Position+1) / (float)(srcLength+1));
          await Awaiters.NextFrame;
        }

        outputStream.Write(suffix, 0, suffix.Length);
      }

      int debugId = RequestDebugGetNewId();
      RequestDebugLogFile(debugId, "form", temporaryFileName);

      Func<UploadHandler> payloadCreator;
      if (App.PlatformConfig.AvoidUploadHandlerFile) {
        byte[] fileContents = await Task.Run(() => File.ReadAllBytes(temporaryFileName));
        payloadCreator = () => new UploadHandlerRaw(fileContents);
      } else {
        payloadCreator = () => new UploadHandlerFile(temporaryFileName) {
            contentType = wrapperType
        };
      }

      return await SendAsync(
          payloadCreator, contentType: wrapperType, isCompressed: m_Compressed, token,
          debugId: debugId);
    } finally {
      if (srcData != null) {
        srcData.Close();
      }
      // If directory is passed, caller is reponsible for cleaning it up
      if (temporaryDirectory == null && temporaryFileName != null) {
        try { File.Delete(temporaryFileName); }
        catch (Exception) { }
      }
    }
  }

  public Task<Reply> SendAsync() {
    return SendAsync(null, null, false);
  }

  /// This version should only be used with small amounts of data.
  public Task<Reply> SendAsync(byte[] uncompressedData, string contentType) {
    if (uncompressedData == null) { throw new ArgumentNullException("uncompressedData"); }
    int debugId = RequestDebugGetNewId();
    RequestDebugLogBytes(debugId, "bytes", uncompressedData);
    return SendAsync(() => new UploadHandlerRaw(uncompressedData) { contentType = contentType },
                     contentType: contentType,
                     isCompressed: false,
                     debugId: debugId);
  }

  /// Pass:
  ///   isGzipped - true if payload != null and the contents have been isGzipped
  ///   contentType - the contentType for the payload. This is to work
  ///     around a bug where the UploadHandlerFile.contentType getter always
  ///     returns "text/plain".
  public async Task<Reply> SendAsync(
      Func<UploadHandler> payloadCreator, string contentType, bool isCompressed,
      CancellationToken? token=null,
      int? debugId=null) {
    int debugId_ = debugId ?? RequestDebugGetNewId();
    // Avoids having to use ?. and ?? all over the place
    CancellationToken realToken = token ?? new CancellationToken();
    // TODO: Consider having an outer controller managing the retry logic because
    // in-lining the checks may be error prone.
    int retries = kNumRetries;
    for (int retryIndex=0; ; ++retryIndex) {
      using (UnityWebRequest www = new UnityWebRequest(m_Uri, m_Method)) {
        UploadHandler payload = payloadCreator?.Invoke();
        www.uploadHandler = payload;
        www.disposeUploadHandlerOnDispose = true;  // the default, but just to be expicit about it

        if (contentType != null) {
          // Workaround; UploadHandlerFile.contentType's getter always returns "text/plain".
          // See https://support.unity3d.com/hc/en-us/requests/737513
          www.SetRequestHeader("Content-Type", contentType);
        }
        if (isCompressed) {
          // Should match the algorithm used by OpenFileForWrite
          www.SetRequestHeader("Content-Encoding", "gzip");
        }

        www.downloadHandler = new DownloadHandlerBuffer();
        if (m_Identity != null) {
          await m_Identity.Authenticate(www);
        }

        if (App.Config.m_DebugWebRequest) {
          var headers = new StringBuilder();
          headers.AppendLine($"URI: {m_Method} {m_Uri}");
          if (!string.IsNullOrEmpty(m_UploadDescription)) {
            headers.AppendLine($"Description: {m_UploadDescription}");
          }
          if (payload != null) {
            // Unity bug? Soemtimes payload.contentType throws a NRE even if payload != null
            try { headers.AppendLine($"Payload type: {payload.contentType}"); }
            catch (Exception) {}
          }
          headers.AppendLine("-- Headers follow --");
          foreach (string key in kInterestingRequestHeaders) {
            // GetRequestHeader returns "" if the header was not set
            string value = www.GetRequestHeader(key);
            if (!string.IsNullOrEmpty(value)) {
              headers.AppendLine($"{key}: {value}");
            }
          }
          RequestDebugLogText(debugId_, "headers", headers.ToString(), retryIndex);
        }

        var sendTask = www.SendWebRequest();
        // Spinning this loop ourselves allows us to cancel without waiting for Send to finish.
        while (!www.isDone) {
          m_UploadedBytes = (int)www.uploadedBytes;
          m_Progress = www.uploadProgress;
          ProgressObject?.Report(m_Progress);
          // Exiting the "using" propagates the cancellation downwards, but doesn't guarantee
          // that all children have stopped. In particular, if uploadHandler still has the
          // file open, caller won't be able to delete the file.
          if (realToken.IsCancellationRequested) {
            try {
              // Unity would be within its rights to barf if we await right after disposing,
              // so try to await before disposing. It might still barf (but currently doesn't).
              async void DelayedDispose(IDisposable id) { await Awaiters.NextFrame; id.Dispose(); }
              DelayedDispose(www);
              await sendTask;
            } catch (Exception e) {
              // If we get this, we should investigate whether we can claim the child's stopped.
              Debug.LogWarning($"Unexpected exception {e} waiting for child to exit");
            } finally {
              throw new OperationCanceledException(realToken);
            }
          }
          await Awaiters.NextFrame;
        }

        if (www.isNetworkError) {
          // This is always a bug, rather than some error on the other end
          if (www.error == "Malformed URL") {
            throw new ArgumentException($"{www.error}: '{m_Uri}'");
          }
          if (retries-- == 0) {
            throw new VrAssetServiceException(
                "Error connecting to server.",
                string.Format("Error connecting to {0} : {1}",
                              RedactUriForError(m_Uri), www.error));
          }
          Debug.LogFormat("Network error ({0} retries remaining): {1} : {2}", retries, m_Uri, www.error);
          await Awaiters.SecondsRealtime(BackoffSeconds(retries));
          continue;
        }

        if (App.Config.m_DebugWebRequest && www.downloadHandler?.data is byte[] data) {
          if (data.Length < 50000) {
            RequestDebugLogBytes(debugId_, "responseData", www.downloadHandler.data, retryIndex);
          }
        }

        if (App.Config.m_DebugWebRequest) {
          RequestDebugLogText(
              debugId_, "responseHdrs", GetResponseHeadersAsString(www), retryIndex);
        }

        // Break out of loop on success
        if (IsSuccess(www.responseCode)) {
          await www.downloadHandler;
          m_Result = www.downloadHandler.data;
          m_UploadedBytes = (int)www.uploadedBytes;
          m_Done = true;
          return new Reply(m_Result);
        }

        // Auth failed - note this cannot be because the refresh token is old because it now
        // gets automatically refreshed inside OAuthIdentity2.Authenticate. This can happen if the
        // authorization has been revoked, so just log out here.
        if (IsAuthError(www.responseCode)) {
          m_Identity.Logout();
          throw new VrAssetServiceException("Not authorized for login. Automatically logged out.",
                                            RedactUriForError(m_Uri));
        }

        // We failed for some reason besides auth.

        string ptype = payload?.GetType().Name ?? "null";
        string errorMessage = $"HTTP error {www.responseCode}\n"
          + $"URI: {m_Method} {RedactUriForError(m_Uri)} {ptype} {m_UploadDescription ?? "n/a"}\n"
#if DEBUG
          // Unique info in the headers (dates, etc) keep the exceptions from aggregating
          // in analytics.
          + $"Headers: {GetResponseHeadersAsString(www) ?? "No response headers"}\n"
#endif
          + $"Body: {www.downloadHandler?.text ?? "<null>"}";

        if (retries > 0 && IsTransientServerError(www.responseCode)) {
          Debug.LogFormat("{0}\n({1} retries remaining)", errorMessage, retries);
          await Awaiters.SecondsRealtime(BackoffSeconds(retries));
          retries -= 1;
          continue;
        }
        string extra = App.Config.m_DebugWebRequest ? $" (id {debugId_})" : "";
        throw new VrAssetServiceException(
            $"{m_Method} failed" + extra, errorMessage);
      }
    }
  }

  private static string GetResponseHeadersAsString(UnityWebRequest www) {
    if (www.GetResponseHeaders() == null) {
      return null;
    } else {
      var builder = new StringBuilder();
      foreach (KeyValuePair<string, string> kvp in www.GetResponseHeaders()) {
        builder.AppendFormat("{0}: {1}\n", kvp.Key, kvp.Value);
      }
      return builder.ToString();
    }
  }

  // retries: a decreasing number in [kNumRetries, 0)
  private static float BackoffSeconds(int retries) {
    // TODO: Since this is such a small bandwidth thing and we're only trying a few times, this
    // is probably okay. But figure out if exponential backoff is really what we want to do here.
    return Mathf.Pow(2f, kNumRetries - retries);
  }

  // Unity doesn't do this correctly so we do it ourselves
  // https://fogbugz.unity3d.com/default.asp?846309_0391asaijk4j3vnt
  // Pass:
  //   parameterName - The name of the form parameter which receives the file info.
  //      eg "file" for Poly APIs, "modelFile" for Sketchfab's Create API
  //   destFilename - For the Content-Disposition "filename" or "filename*" field
  static private (byte[] prefix, byte[] suffix, string wrappedType) SerializeMultipartForm(
      string parameterName, string destFilename, string contentType,
      [CanBeNull] IEnumerable<(string key, string val)> moreParams) {
    const byte dash = 0x2d;

    if (Path.IsPathRooted(destFilename)) {
      throw new ArgumentException("destFilename");
    }
    byte[] boundary = UnityWebRequest.GenerateBoundary();

    byte[] prefix; {
      MemoryStream ms = new MemoryStream();
      if (moreParams != null) {
        // See https://dev.to/sidthesloth92/understanding-html-form-encoding-url-encoded-and-multipart-forms-3lpa
        // and https://sketchfab.com/developers/data-api/v3/python#example-python-upload
        foreach (var (key, val) in moreParams) {
          // Invariant: if this boundary needs a \r\n before the --, it's already been written
          ms.WriteByte(dash);
          ms.WriteByte(dash);
          ms.Write(boundary, 0, boundary.Length);
          ms.WriteByte(0x0d);
          ms.WriteByte(0x0a);
          // The trailing \r\n serves as the next boundary's \r\n prefix.
          string chunk = $"Content-Disposition: form-data; name=\"{key}\"\r\n\r\n{val}\r\n";
          byte[] chunkBytes = Encoding.UTF8.GetBytes(chunk);
          ms.Write(chunkBytes, 0, chunkBytes.Length);
        }
      }
      // If file name contains difficult characters, we need to use RFC 5987 encoding.
      string filenameParam;
      if (Encoding.UTF8.GetByteCount(destFilename) != destFilename.Length ||
          destFilename.Contains("\\") ||
          destFilename.Contains("\"")) {
        Debug.LogWarningFormat("Sending RFC 5987 filename {0}", destFilename);
        // b/66965913: We get a 400 when we encode this way
        // It claims "Invalid multipart request with 0 mime parts"
        filenameParam = string.Format("filename*={0}", TextUtils.Rfc5987Encode(destFilename));
      } else {
        filenameParam = string.Format("filename=\"{0}\"", destFilename);
      }

      // Invariant: if this boundary needs a \r\n before the --, it's already been written
      ms.WriteByte(dash);
      ms.WriteByte(dash);
      ms.Write(boundary, 0, boundary.Length);
      ms.WriteByte(0x0d);
      ms.WriteByte(0x0a);
      string header =
          $"Content-Disposition: form-data; name=\"{parameterName}\"; {filenameParam}\r\n" +
          $"Content-Type: {contentType}\r\n" +
          "\r\n";
      byte[] headerBytes = sm_StrictAscii.GetBytes(header);
      ms.Write(headerBytes, 0, headerBytes.Length);
      prefix = ms.ToArray();
    }

    // caller writes the buffer, buffer.Length here.

    byte[] suffix; {
      MemoryStream ms = new MemoryStream();

      ms.WriteByte(0x0d);
      ms.WriteByte(0x0a);
      ms.WriteByte(dash);
      ms.WriteByte(dash);
      ms.Write(boundary, 0, boundary.Length);
      ms.WriteByte(dash);
      ms.WriteByte(dash);
      suffix = ms.ToArray();
    }
    string envelopeContentType =
        $"multipart/form-data; boundary={Encoding.ASCII.GetString(boundary)}";
    return (prefix, suffix, envelopeContentType);
  }
}
} // namespace TiltBrush
