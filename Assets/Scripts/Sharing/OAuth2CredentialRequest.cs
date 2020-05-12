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
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using UnityEngine;

namespace TiltBrush {

public class OAuth2CredentialRequest {
  private UserCredential m_UserCredential;
  private TaskAndCts<UserCredential> m_Authorization;
  private Task m_Revocation;
  private readonly AuthorizationCodeInstalledApp m_AppFlow;

  public UserCredential UserCredential => m_UserCredential;
  public bool IsAuthorizing => m_Authorization != null;
  public bool IsGoogle { get; }

  /// Request Oauth2 credentials through Google.
  public OAuth2CredentialRequest(ClientSecrets secrets, IEnumerable<string> scopes,
                                 string callbackPath, IDataStore dataStore) {
    // Use Google authorization code flow, which has hardcoded authorization server and token server
    // URLs.
    IsGoogle = true;
    var initializer = new GoogleAuthorizationCodeFlow.Initializer {
        ClientSecrets = secrets,
        Scopes = scopes,
        DataStore = dataStore,
    };
    var authFlow = new GoogleAuthorizationCodeFlow(initializer);
    var codeReceiver = new LocalHttpCodeReceiver(callbackPath);
    m_AppFlow = new AuthorizationCodeInstalledApp(authFlow, codeReceiver);
  }

  /// Request Oauth2 credentials through a third party by providing authorization and token server
  /// URLs.
  public OAuth2CredentialRequest(ClientSecrets secrets, IEnumerable<string> scopes,
                                 string callbackPath, IDataStore dataStore,
                                 string authorizationServerUrl, string tokenServerUrl) {
    Debug.Assert(!string.IsNullOrWhiteSpace(authorizationServerUrl),
                 "Missing authorization server url");
    Debug.Assert(!string.IsNullOrWhiteSpace(tokenServerUrl),
                 "Missing token server url");

    // Use the generic authorization code flow with the provided authorization and token
    // server urls.
    IsGoogle = false;
    var initializer = new AuthorizationCodeFlow.Initializer(authorizationServerUrl,
                                                            tokenServerUrl) {
        ClientSecrets = secrets,
        Scopes = scopes,
        DataStore = dataStore,
    };
    var authFlow = new AuthorizationCodeFlow(initializer);
    var codeReceiver = new LocalHttpCodeReceiver(callbackPath);
    m_AppFlow = new AuthorizationCodeInstalledApp(authFlow, codeReceiver);
  }

  /// Authorizes the user. Must not be called when already authorizing.
  public async Task AuthorizeAsync() {
    await CancelAuthorizationAsync();
    // If we're currently revoking, just wait for it to finish.
    if (m_Revocation != null) {
      await m_Revocation;
    }
    try {
      m_Authorization = new TaskAndCts<UserCredential>();
      m_Authorization.Task = m_AppFlow.AuthorizeAsync("user", m_Authorization.Token);
      m_UserCredential = await m_Authorization.Task;
    } catch (OperationCanceledException) {
      // Do nothing with this exception as we only get it when the user cancels.
    } finally {
      m_Authorization = null;
    }
  }

  private async Task CancelAuthorizationAsync() {
    if (m_Authorization != null) {
      m_Authorization.Cancel();
      try {
        await m_Authorization.Task;
      } catch (OperationCanceledException) {
      } finally {
        m_Authorization = null;
      }
    }
  }

  /// Cancels an in-process authorization. Shouldn't be called when no authorization is in progress.
  public async Task Cancel() {
    Debug.Assert(IsAuthorizing);
    await CancelAuthorizationAsync();
  }

  public async void RevokeCredential() {
    await CancelAuthorizationAsync();
    try {
      m_Revocation = m_UserCredential.RevokeTokenAsync(CancellationToken.None);
      await m_Revocation;
    } finally {
      m_Revocation = null;
      m_UserCredential = null;
    }
  }
}
} // namespace TiltBrush
