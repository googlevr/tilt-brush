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
using UnityEngine;

namespace TiltBrush {

[Serializable]
public class TiltasaurusContent {
  public List<TiltasaurusCategory> Categories;
  [System.NonSerialized] public List<TiltasaurusCategory> UsedCategories;

  // Json deserializer leaves some arrays null, etc; so finish loading
  public void Init() {
    if (Categories == null) { Categories = new List<TiltasaurusCategory>(); }
    UsedCategories = new List<TiltasaurusCategory>();
    foreach (TiltasaurusCategory c in Categories) {
      c.Init();
    }
  }
}

[Serializable]
public class TiltasaurusCategory {
  public string Name;
  public List<string> Words;
  [System.NonSerialized] public List<string> UsedWords;

  // Json deserializer leaves some arrays null, etc; so finish loading
  public void Init() {
    if (Name == null) { Name = "No Name"; }
    if (Words == null) { Words = new List<string>(); }
    UsedWords = new List<string>();
  }
}

public class Tiltasaurus : MonoBehaviour {
  static public Tiltasaurus m_Instance;

  [SerializeField] private string m_Filename;

  private TiltasaurusContent m_Content;
  private TiltasaurusCategory m_ActiveCategory;
  private string m_ActivePrompt;

  /// May be null
  public string Prompt { get { return m_ActivePrompt; } }
  /// May be null
  public string Category { get {
      return (m_ActiveCategory == null) ? null : m_ActiveCategory.Name;
    }
  }

  public bool TiltasaurusAvailable() {
    return m_Content != null;
  }

  // Return -1 on failure
  static int RandomIndex<T>(List<T> list) {
    // Unity's Random.Range(0, 0) returns 0, so write our own
    if (list.Count == 0) { return -1; }
    return UnityEngine.Random.Range(0, list.Count);
  }

  // Return a non-empty category, or null.
  // May mutate Categories to move empty ones into "UsedCategories".
  TiltasaurusCategory ChooseNonEmptyCategory() {
    while (true) {
      int i = RandomIndex(m_Content.Categories);
      if (i == -1) {
        return null;
      }
      var category = m_Content.Categories[i];
      if (category.Words.Count == 0) {
        m_Content.UsedCategories.Add(category);
        m_Content.Categories.RemoveAt(i);
      } else {
        return category;
      }
    }
  }

  /// Choose a new prompt word.
  /// Mutates category to move the word into "UsedWords".
  public void ChooseNewPrompt() {
    if (m_Content == null) { return; }

    m_ActivePrompt = null;

    m_ActiveCategory = ChooseNonEmptyCategory();  // for now, choose new category every round
    if (m_ActiveCategory == null) { return; }

    int iWord = RandomIndex(m_ActiveCategory.Words);
    if (iWord == -1) { return; }  // should never happen
    var word = m_ActiveCategory.Words[iWord];
    m_ActiveCategory.Words.RemoveAt(iWord);
    m_ActiveCategory.UsedWords.Add(word);

    m_ActivePrompt = word;
  }

  void Awake() {
    m_Instance = this;
  }

  void Start () {
    // First, look for the Tiltasaurus file in the user path.
    string sFullPath = Path.Combine(App.UserPath(), m_Filename + ".json");
    if (!File.Exists(sFullPath)) {
      // If user path doesn't exist, look for it in the Support folder.
      sFullPath = Path.Combine(App.SupportPath(), m_Filename + ".json");
    }

    if (File.Exists(sFullPath)) {
      m_Content = JsonUtility.FromJson<TiltasaurusContent>(File.ReadAllText(sFullPath));
      m_Content.Init();
      ChooseNewPrompt();
    }
  }
}

} // namespace TiltBrush
