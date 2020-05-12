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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Json;
using Google.Apis.Util.Store;
using UnityEngine;

namespace TiltBrush {
/// Implements IDataStore, storing data in Unity PlayerPrefs.
public class PlayerPrefsDataStore : IDataStore {
  private string m_Prefix;
  private Dictionary<string, string> m_Data;
  // The DataStore may be called off the main thread, and therefore we can't actually interact with
  // PlayerPrefs straight away. A queue of actions is therefore created with the playerprefs
  // interactions in it, which are executed during the update phase.
  private ConcurrentQueue<Action> m_UpdateActions;
  // So that we can clear all existing keys, we need a list of keys
  private string m_KeysKey;

  public PlayerPrefsDataStore(string prefix) {
    m_Prefix = prefix;
    m_Data = new Dictionary<string, string>();
    m_KeysKey = m_Prefix + "KeyList";
    string json = PlayerPrefs.GetString(m_KeysKey, "[]");
    var keys = NewtonsoftJsonSerializer.Instance.Deserialize<string[]>(json);
    foreach (var key in keys) {
      string keyName = CreateKeyName(key);
      m_Data[key] = PlayerPrefs.GetString(keyName, "");
    }
    m_UpdateActions = new ConcurrentQueue<Action>();
  }

  /// Stores [value] at [key].
  public Task StoreAsync<T>(string key, T value) {
    if (string.IsNullOrEmpty(key))
      throw new ArgumentException("Cannot store a value with an empty or null key!");
    var serialized = NewtonsoftJsonSerializer.Instance.Serialize(value);
    m_Data[key] = serialized;
    DeferAction(() => PlayerPrefs.SetString(CreateKeyName(key), serialized));
    return Task.CompletedTask;
  }

  /// Deletes [key]
  public Task DeleteAsync<T>(string key) {
    if (string.IsNullOrEmpty(key))
      throw new ArgumentException("Cannot delete a value with an empty or null key!");
    m_Data.Remove(key);
    m_UpdateActions.Enqueue(() => {
      PlayerPrefs.DeleteKey(CreateKeyName(key));
    });
    DeferAction(() => PlayerPrefs.DeleteKey(CreateKeyName(key)));
    return Task.CompletedTask;
  }

  /// Gets the value of [key], or the default value is that does not exist.
  public Task<T> GetAsync<T>(string key) {
    if (string.IsNullOrEmpty(key))
      throw new ArgumentException("Cannot get a value with an empty or null key!");
    if (m_Data.TryGetValue(key, out string serialized)) {
      return Task.FromResult(NewtonsoftJsonSerializer.Instance.Deserialize<T>(serialized));
    }
    return Task.FromResult(default(T));
  }

  /// Clears all key/value pairs
  public Task ClearAsync() {
    var keys = m_Data.Keys.ToArray();
    m_Data.Clear();
    DeferAction(() => {
      foreach (var key in keys) {
        PlayerPrefs.DeleteKey(CreateKeyName(key));
      }
    });
    return Task.CompletedTask;
  }

  /// Gets the current user, if there is one.
  public TokenResponse UserTokens() {
    return GetAsync<TokenResponse>("user").Result;
  }

  private string CreateKeyName(string key) {
    return $"{m_Prefix}_{key}";
  }

  // When an action is deferred, it gets added to the update actions list. This is because we always
  // want actions to be performed in the order they were created. We also always want the set of
  // key names to match the stored keys, so they get saved off in the closure and saved after each
  // action.
  private async void DeferAction(Action action) {
    var keys = m_Data.Keys.ToArray();
    m_UpdateActions.Enqueue(() => {
      action.Invoke();
      string json = NewtonsoftJsonSerializer.Instance.Serialize(keys);
      PlayerPrefs.SetString(m_KeysKey, json);
    });
    await new WaitForUpdate();
    Update();
  }

  // This gets called in the Update phase, possibly several times if several actions have happened
  // in one frame. However, after the first one the queue will be empty and nothing further will
  // be done.
  private void Update() {
    if (m_UpdateActions.Count == 0) {
      return;
    }
    while (m_UpdateActions.TryDequeue(out Action action)) {
      action.Invoke();
    }
  }
}
} // namespace TiltBrush
