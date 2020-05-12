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

using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using UnityEngine;

namespace TiltBrush {
public class LocalHttpCodeReceiver : ICodeReceiver {
  private string m_LocalPath;

  public string RedirectUri =>
      $"http://localhost:{App.HttpServer.HttpPort}{m_LocalPath}";

  private TaskCompletionSource<NameValueCollection> m_QueryString;

  private string FinalRedirectPage => Application.platform == RuntimePlatform.Android
      ? "http://localhost:40074/login/ReplaceHeadsetMobile.html"
      : "http://localhost:40074/login/ReplaceHeadset.html";

  public LocalHttpCodeReceiver(string localPath) {
    m_LocalPath = localPath;
  }

  public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url,
      CancellationToken taskCancellationToken) {
    try {
      // We use a CompletionSource to pass the query string back from the callback.
      // If a cancellation comes through, we need to cancel it as well, as we'll await it later.
      m_QueryString = new TaskCompletionSource<NameValueCollection>();
      taskCancellationToken.Register(m_QueryString.SetCanceled);

      // Store the query string and redirect to replace headset webpage once the request arrives.
      App.HttpServer.AddHttpHandler(m_LocalPath, (context) => {
        m_QueryString.SetResult(context.Request.QueryString);
        context.Response.RedirectLocation = FinalRedirectPage;
        context.Response.StatusCode = (int) HttpStatusCode.SeeOther;
      });

      App.OpenURL(url.Build().AbsoluteUri);

      var result = await m_QueryString.Task;

      return new AuthorizationCodeResponseUrl(result.AllKeys.ToDictionary(k => k, k => result[k]));
    } finally {
      App.HttpServer.RemoveHttpHandler(m_LocalPath);
    }
  }
}
} // namespace TiltBrush
