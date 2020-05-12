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
using System.Linq;
using UnityEngine;

public class SecretsConfig : ScriptableObject {
  public enum Service {
    Google = 0,
    Sketchfab = 1,
    Oculus = 2,
    OculusMobile = 3,
  }
  
  [Serializable]
  public class ServiceAuthData {
    public Service Service;
    public string ApiKey;
    public string ClientId;
    public string ClientSecret;
  }

  public ServiceAuthData[] Secrets;

  public ServiceAuthData this[Service service] {
    get => Secrets.FirstOrDefault(x => x.Service == service);
  }
}
