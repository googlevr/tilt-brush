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
using System.IO;

namespace TiltBrush {

/// Panel used for QA testing.
/// Displays test summary, repro steps, and expected result. Each test can be
/// marked as passing or failing.
///
/// To make available, add the following to Tilt Brush.cfg:
/// "Testing": {
///    "Enabled": true,
///    "InputFile": "testcases.txt",    <- this must be a tsv
///    "OutputFile": "testoutput.tsv",
/// }
///
/// The column headers this panel looks for are "Summary", "Steps", and "Expected".
/// These headers can have more than just that word in them, but there should not be
/// multiple columns with these keywords. The text under these columns is displayed
/// on the panel.
///
/// When the application exits, it will create an a tsv that tries to keep as much of
/// the original table as possible, then adds a "Result" column to the end. This will
/// be populated with "Pass" or "Fail" as indicated on the panel during that session.
/// The last column in the original table MUST have a header to not have data lost in
/// the output file.

public class TestingPanel : BasePanel {
  // Returns null if no input file specified
  static string InputFilePath {
    get {
      return Path.Combine(App.UserPath(), App.UserConfig.Testing.InputFile);
    }
  }

  // Returns a reasonable default if no output file specified
  static string OutputFilePath {
    get {
      string outputFile = App.UserConfig.Testing.OutputFile;
      if (string.IsNullOrEmpty(outputFile)) {
        outputFile = "QA.tsv";
      }
      return Path.Combine(App.UserPath(), outputFile);
    }
  }

  [SerializeField] private TestingButton m_PassButton;
  [SerializeField] private TestingButton m_FailButton;
  [SerializeField] private TestingButton m_NAButton;
  [SerializeField] private TMPro.TextMeshPro m_Summary;
  [SerializeField] private TMPro.TextMeshPro m_Steps;
  [SerializeField] private TMPro.TextMeshPro m_ExpectedResult;

  private string[,] m_TestCases; // 0th row is headers
  private int m_TestIndex; // valid from 1 to m_TextCases.GetLength(0) - 1
  private int m_SummaryIndex = -1;
  private int m_StepsIndex = -1;
  private int m_ExpectedResultIndex = -1;

  public override bool ShouldRegister { get { return false; } }

  public void OnButtonPressed(TestingButton.Type type, string text = "") {
    switch (type) {
    case TestingButton.Type.Next:
      m_TestIndex++;
      if (m_TestIndex == m_TestCases.GetLength(0)) {
        m_TestIndex = 1;
      }
      break;
    case TestingButton.Type.Back:
      m_TestIndex--;
      if (m_TestIndex == 0) {
        m_TestIndex = m_TestCases.GetLength(0) - 1;
      }
      break;
    case TestingButton.Type.Result:
      m_TestCases[m_TestIndex, m_TestCases.GetLength(1) - 1] = text;
      break;
    }
    RefreshPanel();
  }

  void OnApplicationQuit() {
    TsvIo.WriteResults(OutputFilePath, m_TestCases);
  }

  // Disable and reenable TMPro meshes due to block letters bug
  override protected void OnDisablePanel() {
    base.OnDisablePanel();
    m_Summary.enabled = false;
    m_Steps.enabled = false;
    m_ExpectedResult.enabled = false;
  }

  override protected void OnEnablePanel() {
    base.OnEnablePanel();
    m_Summary.enabled = true;
    m_Steps.enabled = true;
    m_ExpectedResult.enabled = true;
  }

  public void ResetTests() {
    PopulateCases(InputFilePath);
    m_TestIndex = 1;
    RefreshPanel();
  }

  void RefreshPanel() {
    int index = m_TestCases.GetLength(1) - 1;
    m_PassButton.ToggleActive(m_TestCases[m_TestIndex, index] == m_PassButton.ResultText);
    m_FailButton.ToggleActive(m_TestCases[m_TestIndex, index] == m_FailButton.ResultText);
    m_NAButton.ToggleActive(m_TestCases[m_TestIndex, index] == m_NAButton.ResultText);
    if (m_SummaryIndex > -1) {
      m_Summary.text = m_TestCases[m_TestIndex, m_SummaryIndex];
    }
    if (m_StepsIndex > -1) {
      m_Steps.text = m_TestCases[m_TestIndex, m_StepsIndex];
    }
    if (m_ExpectedResultIndex > -1) {
      m_ExpectedResult.text = m_TestCases[m_TestIndex, m_ExpectedResultIndex];
    }
  }

  private void PopulateCases(string input) {
    if (input == null) {
      OutputWindowScript.m_Instance.AddNewLine("No test case file specified");
      m_TestCases = new string[1, 1];
      return;
    }

    try {
      m_TestCases = TsvIo.TsvToTable(input);
      for (int c = 0; c < m_TestCases.GetLength(1); c++) {
        if (m_TestCases[0, c].Contains("Summary")) {
          m_SummaryIndex = c;
        } else if (m_TestCases[0, c].Contains("Steps")) {
          m_StepsIndex = c;
        } else if (m_TestCases[0, c].Contains("Expected")) {
          m_ExpectedResultIndex = c;
        }
      }
      AddResultsColumn();
    } catch (FileNotFoundException) {
      OutputWindowScript.m_Instance.AddNewLine("{0} not found!", input);
      Debug.Log(input + " not found!");
      m_TestCases = new string[1, 1];
    }
  }

  void AddResultsColumn() {
    if (m_TestCases[0, m_TestCases.GetLength(1) - 1] != "") {
      string[,] newTable = new string[m_TestCases.GetLength(0), m_TestCases.GetLength(1) + 1];
      for (int r = 0; r < m_TestCases.GetLength(0); r++) {
        for (int c = 0; c < m_TestCases.GetLength(1); c++) {
          newTable[r, c] = m_TestCases[r, c];
        }
      }
      m_TestCases = newTable;
    }
    m_TestCases[0, m_TestCases.GetLength(1) - 1] = "Result";
  }

  override public void InitPanel() {
    base.InitPanel();
    m_UIComponentManager.SetColor(PanelManager.m_Instance.PanelHighlightInactiveColor);
  }
}
}  // namespace TiltBrush
