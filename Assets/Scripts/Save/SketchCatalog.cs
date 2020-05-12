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

namespace TiltBrush {

public enum SketchSetType {
  User,
  Curated,
  Liked,
  Drive,
}

// SketchCatalog.Awake must come after App.Awake
public class SketchCatalog : MonoBehaviour {
  static public SketchCatalog m_Instance;

  // This folder contains json files which define where to pull the sketch thumbnail and data
  // from Poly.  These are used to populate the showcase when we can't query Poly.
  // Obviously, if Poly as a database is deleted or moved, accessing these files will fail.
  public const string kDefaultShowcaseSketchesFolder = "DefaultShowcaseSketches";

  private SketchSet[] m_Sets;

  public SketchSet GetSet(SketchSetType eType) {
    return m_Sets[(int)eType];
  }

  void Awake() {
    m_Instance = this;

    if (Application.platform == RuntimePlatform.OSXEditor ||
        Application.platform == RuntimePlatform.OSXPlayer) {
      // force KEvents implementation of FileSystemWatcher
      // source: https://github.com/mono/mono/blob/master/mcs/class/System/System.IO/FileSystemWatcher.cs
      // Unity bug: https://fogbugz.unity3d.com/default.asp?778750_fncnl0np45at4mq1
      System.Environment.SetEnvironmentVariable ("MONO_MANAGED_WATCHER", "3");
    }

    int maxTriangles = QualityControls.m_Instance.AppQualityLevels.MaxPolySketchTriangles;

    m_Sets = new SketchSet[] {
      new FileSketchSet(),
      new PolySketchSet(this, SketchSetType.Curated, maxTriangles),
      new PolySketchSet(this, SketchSetType.Liked, maxTriangles, needsLogin: true),
      new GoogleDriveSketchSet(),
    };
  }

  void Start() {
    foreach (SketchSet s in m_Sets) {
      s.Init();
    }
  }

  void Update() {
    foreach (SketchSet s in m_Sets) {
      s.Update();
    }
  }

  public void NotifyUserFileCreated(string fullpath) {
    m_Sets[(int)SketchSetType.User].NotifySketchCreated(fullpath);
  }

  public void NotifyUserFileChanged(string fullpath) {
    m_Sets[(int)SketchSetType.User].NotifySketchChanged(fullpath);
  }
}


}  // namespace TiltBrush
