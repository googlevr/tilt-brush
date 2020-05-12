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

using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TiltBrush {

public class OutputWindowScript : MonoBehaviour {
  const int CONTROLLER_DETAIL_MAX_LENGTH = 70;
  static public OutputWindowScript m_Instance;

  public enum LineType {
    None,
    Standard,
    StandardNoSound,
    Special,
    SpecialWithSound
  }

  public enum InfoCardSpawnPos {
    None,
    Wand,
    Brush,
    UIReticle
  }

  public class OutputLine {
    public bool m_Enabled;
    public GameObject m_Line;
    public Renderer m_Renderer;
    public TextMeshPro m_TextMesh;
    public float m_CurrentHeight;
    public float m_DesiredHeight;
    public float m_SpringAngle;
    public float m_SpringVelocity;
    public float m_LifeCountdown;
    public Color m_Color;
  }

  public class QueuedLine {
    public string m_Text;
    public LineType m_Type;
  }

  public class DeferredErrorMessage {
    public InputManager.ControllerName m_Controller;
    public string m_Msg;
    public System.Object m_Detail;
  }

  [SerializeField] private AudioClip m_StandardOutputSound;
  [SerializeField] private AudioClip m_SpecialOutputSound;
  [SerializeField] private float m_AudioMinTriggerTime;

  [SerializeField] private GameObject m_TextLine;
  [SerializeField] private Color m_TextColor;
  [SerializeField] private Color m_TextSpecialColor;
  [SerializeField] private float m_SpringK = 1.0f;
  [SerializeField] private float m_SpringDampen = 0.1f;
  [SerializeField] private float m_SpawnAngle;
  [SerializeField] private float m_DesiredAngle;

  [SerializeField] private Vector3 m_BaseOffset;
  [SerializeField] private float m_LineSpacing;
  [SerializeField] private float m_LineOffset;
  [SerializeField] private float m_LineHeightSpeed;

  [SerializeField] private float m_HoldDuration;
  [SerializeField] private float m_FadeDuration;

  [SerializeField] private int m_NumLines;

  [SerializeField] private GameObject m_InfoCardPrefab;

  [SerializeField] private float m_AudioVolume = 1.0f;

  private float m_AudioTimestamp;
  private Vector3 m_BasePosition;

  private OutputLine[] m_Lines;
  private Queue<QueuedLine> m_LineQueue;
  private static List<DeferredErrorMessage> m_DeferredErrors;

  // Static, high-level API (Maybe move to an error manager?)

  /// Display an error message to the user.
  public static void Error(string msg, System.Object detail=null) {
    // I guess... use the brush instead of the output window?
    Error(InputManager.ControllerName.Brush, msg, detail);
  }

  /// Display an error message to the user, related to an action taken
  /// by the specified controller.
  public static void Error(InputManager.ControllerName controller,
                           string msg,
                           System.Object detail=null) {
    // If our singleton instances haven't been assigned, defer the error.
    if (m_Instance == null || ControllerConsoleScript.m_Instance == null) {
      DeferError(controller, msg, detail);
    } else {
      Debug.LogErrorFormat("User-visible error: {0}\nDetail: {1}", msg, detail);

      // In the future, maybe use color or something instead of prepending ERROR
      string card = string.Format("ERROR: {0}", msg);
      float cardPop = 1.0f + InfoCardAnimation.s_NumCards * 0.2f;
      m_Instance.CreateInfoCardAtController(controller, card, cardPop, false);

      ControllerConsoleScript.m_Instance.AddNewLine(msg, true);
      if (detail != null) {
        string detailStr = detail.ToString();
        if (detailStr.Length > CONTROLLER_DETAIL_MAX_LENGTH) {
          detailStr = detailStr.Substring(0, CONTROLLER_DETAIL_MAX_LENGTH-3) + " ...";
        }
        ControllerConsoleScript.m_Instance.AddNewLine(detailStr, false);
      }
    }
  }

  /// Returns a Tilt Brush filename, shortened and suitable for UI display.
  public static string GetShorterFileName(string path) {
    var docs = App.DocumentsPath();
    if (path.StartsWith(docs)) {
      return "Documents" + path.Substring(docs.Length);
    } else {
      return path;
    }
  }

  /// Report file saved as a card and detailed controller message.
  /// If ControllerName is null, card is skipped.
  /// If filename is null, detailed controller message is skipped.
  public static void ReportFileSaved(
      string exclamation, string filename, InfoCardSpawnPos spawnPos = InfoCardSpawnPos.None) {
    switch (spawnPos) {
    case InfoCardSpawnPos.Brush:
      m_Instance.CreateInfoCardAtController(
          InputManager.ControllerName.Brush, exclamation, fPopScalar: 0.5f, false);
      break;
    case InfoCardSpawnPos.Wand:
      m_Instance.CreateInfoCardAtController(
          InputManager.ControllerName.Wand, exclamation, fPopScalar: 0.5f, false);
      break;
    case InfoCardSpawnPos.UIReticle:
      m_Instance.CreateInfoCard(SketchControlsScript.m_Instance.GetUIReticlePos(),
          exclamation, fPopScalar: 0.5f, false);
      break;
    }
    if (filename != null) {
      var ccs = ControllerConsoleScript.m_Instance;
      ccs.AddNewLine(exclamation, true);

      // Path to file is irrelevant on mobile hardware.
      if (!App.Config.IsMobileHardware) {
        ccs.AddNewLine(GetShorterFileName(filename));
      }
    }
  }

  // Instance API

  public void UpdateBasePositionHeight(float fHeightAdjust) {
    m_BasePosition.y = m_BaseOffset.y + fHeightAdjust;
  }

  private static void DeferError(InputManager.ControllerName controller,
                                 string msg, System.Object obj) {
    if (m_DeferredErrors == null) {
      m_DeferredErrors = new List<DeferredErrorMessage>();
    }

    DeferredErrorMessage err = new DeferredErrorMessage();
    err.m_Controller = controller;
    err.m_Detail = obj;
    err.m_Msg = msg;
    m_DeferredErrors.Add(err);
  }

  void Awake() {
    m_Instance = this;

    m_BasePosition = m_BaseOffset;

    m_Lines = new OutputLine[m_NumLines];
    for (int i = 0; i < m_NumLines; ++i) {
      m_Lines[i] = new OutputLine();
      m_Lines[i].m_Line = (GameObject)Instantiate(m_TextLine, m_BasePosition, Quaternion.identity);
      m_Lines[i].m_Renderer = m_Lines[i].m_Line.GetComponent<Renderer>();
      m_Lines[i].m_TextMesh = m_Lines[i].m_Line.GetComponent<TextMeshPro>();
      m_Lines[i].m_Color = m_TextColor;
      m_Lines[i].m_TextMesh.color = m_TextColor;
      m_Lines[i].m_Line.transform.SetParent(transform);
      m_Lines[i].m_Line.transform.localPosition += new Vector3(m_LineOffset, 0, 0);
      m_Lines[i].m_Line.SetActive(false);
    }

    m_LineQueue = new Queue<QueuedLine>(m_NumLines * 4);
  }

  void Start() {
    // If some errors were logged during startup, we may have deferred errors we need to report.
    if (m_DeferredErrors != null) {
      for (int i = 0; i < m_DeferredErrors.Count; ++i) {
        Error(m_DeferredErrors[i].m_Controller, m_DeferredErrors[i].m_Msg,
            m_DeferredErrors[i].m_Detail);
      }
      m_DeferredErrors.Clear();
    }
  }

  void Update() {
    //update all lines
    for (int i = 0; i < m_NumLines; ++i) {
      OutputLine rLine = m_Lines[i];
      if (rLine.m_Enabled) {
        //update spring
        float fToDesired = m_DesiredAngle - rLine.m_SpringAngle;
        fToDesired *= m_SpringK;
        float fDampenedVel = rLine.m_SpringVelocity * m_SpringDampen;
        float fSpringForce = fToDesired - fDampenedVel;
        rLine.m_SpringVelocity += fSpringForce;
        rLine.m_SpringAngle += (rLine.m_SpringVelocity * Time.deltaTime);

        Quaternion qOrient = Quaternion.Euler(0.0f, rLine.m_SpringAngle, 0.0f);
        rLine.m_Line.transform.rotation = qOrient;

        //update height
        float fToDesiredHeight = rLine.m_DesiredHeight - rLine.m_CurrentHeight;
        rLine.m_CurrentHeight += (fToDesiredHeight * Time.deltaTime * m_LineHeightSpeed);

        Vector3 vPos = rLine.m_Line.transform.position;
        vPos.y = rLine.m_CurrentHeight;
        rLine.m_Line.transform.position = vPos;

        //update life
        float fLife = rLine.m_LifeCountdown - Time.deltaTime;
        rLine.m_LifeCountdown = fLife;
        if (fLife <= 0.0f) {
          //turn off line!
          rLine.m_Line.SetActive(false);
          rLine.m_Enabled = false;

          //scoot everything up
          AdjustLineHeights(m_LineSpacing, rLine.m_DesiredHeight);
        } else {
          //fade the line out
          Color rColor = rLine.m_Color;
          rColor.a = Mathf.Min(fLife / m_FadeDuration, 1.0f);
          rLine.m_TextMesh.color = rColor;
        }
      }
    }

    //add next queued line
    if (m_LineQueue.Count > 0) {
      var line = m_LineQueue.Dequeue();
      CreateLineFromQueue(line.m_Text, line.m_Type);
    }
  }

  public void CreateInfoCardAtController(
      InputManager.ControllerName eName, string sText,
      float fPopScalar = 1.0f, bool alsoConsole=true) {
    if (InputManager.m_Instance.AllowVrControllers) {
      Vector3 vPos = InputManager.m_Instance.GetControllerPosition(eName);
      CreateInfoCard(vPos, sText, fPopScalar, alsoConsole);
    } else if (alsoConsole) {
      ControllerConsoleScript.m_Instance.AddNewLine(sText);
    }
  }

  public void CreateInfoCard(
      Vector3 vPos, string sText, float fPopScalar = 1.0f, bool alsoConsole=true) {
    GameObject rNewCard = (GameObject)Instantiate(m_InfoCardPrefab);
    rNewCard.transform.position = vPos;

    InfoCardAnimation rAnimScript = rNewCard.GetComponent<InfoCardAnimation>();
    rAnimScript.Init(sText, fPopScalar);
    if (alsoConsole) {
      ControllerConsoleScript.m_Instance.AddNewLine(sText);
    }
  }

  public void AddNewLine(string fmt, params System.Object[] args) {
    AddNewLine(LineType.Standard, fmt, args);
  }

  public void AddNewLine(LineType type, string fmt, params System.Object[] args) {
    QueuedLine rNewLine = new QueuedLine();
    rNewLine.m_Text = string.Format(fmt, args);
    rNewLine.m_Type = type;
    m_LineQueue.Enqueue(rNewLine);
    // Debug.Log(rNewLine.m_Text);

    ControllerConsoleScript.m_Instance.AddNewLine(rNewLine.m_Text);
  }

  void CreateLineFromQueue(string sNewLine, LineType rSoundType) {
    int iLineIndex = GetOpenLineIndex();

    //set text
    m_Lines[iLineIndex].m_TextMesh.parseCtrlCharacters = false;
    m_Lines[iLineIndex].m_TextMesh.text = sNewLine;

    //position
    Vector3 vPos = m_Lines[iLineIndex].m_Line.transform.position;
    vPos.y = m_BasePosition.y;
    m_Lines[iLineIndex].m_Line.transform.position = vPos;
    m_Lines[iLineIndex].m_CurrentHeight = m_BasePosition.y;
    m_Lines[iLineIndex].m_DesiredHeight = m_BasePosition.y;
    m_Lines[iLineIndex].m_Line.transform.position = vPos;
    Quaternion qOrient = Quaternion.Euler(0.0f, m_SpawnAngle, 0.0f);
    m_Lines[iLineIndex].m_Line.transform.rotation = qOrient;

    //enable line
    m_Lines[iLineIndex].m_Line.SetActive(true);
    m_Lines[iLineIndex].m_SpringAngle = m_SpawnAngle;
    m_Lines[iLineIndex].m_Enabled = true;
    m_Lines[iLineIndex].m_LifeCountdown = m_HoldDuration + m_FadeDuration;

    //scoot everything else down
    AdjustLineHeights(-m_LineSpacing, m_BasePosition.y + (m_LineSpacing * 0.5f));

    if (rSoundType == LineType.Special) {
      m_Lines[iLineIndex].m_Color = m_TextSpecialColor;
    } else if (rSoundType == LineType.SpecialWithSound) {
      RequestPlaySound(m_SpecialOutputSound, m_BasePosition);
      m_Lines[iLineIndex].m_Color = m_TextSpecialColor;
    } else if (rSoundType == LineType.Standard) {
      RequestPlaySound(m_StandardOutputSound, m_BasePosition);
      m_Lines[iLineIndex].m_Color = m_TextColor;
    } else if (rSoundType == LineType.StandardNoSound) {
      m_Lines[iLineIndex].m_Color = m_TextColor;
    }
  }

  void RequestPlaySound(AudioClip sound, Vector3 vPos) {
    if (Time.realtimeSinceStartup - m_AudioTimestamp > m_AudioMinTriggerTime) {
      AudioManager.m_Instance.TriggerOneShot(sound, vPos, m_AudioVolume);
      m_AudioTimestamp = Time.realtimeSinceStartup;
    }
  }

  int GetOpenLineIndex() {
    float fShortestLife = 999.0f;
    int iShortestLifeIndex = -1;
    for (int i = 0; i < m_NumLines; ++i) {
      if (!m_Lines[i].m_Enabled) {
        return i;
      }

      if (m_Lines[i].m_LifeCountdown < fShortestLife) {
        fShortestLife = m_Lines[i].m_LifeCountdown;
        iShortestLifeIndex = i;
      }
    }

    //couldn't find an open line.. return the one with the shortest life
    return iShortestLifeIndex;
  }

  void AdjustLineHeights(float fAmount, float fCutoffPoint) {
    for (int i = 0; i < m_NumLines; ++i) {
      if (m_Lines[i].m_Enabled) {
        if (m_Lines[i].m_DesiredHeight < fCutoffPoint) {
          m_Lines[i].m_DesiredHeight += fAmount;
        }
      }
    }
  }

  public void EnableRendering(bool bEnable) {
    //turn off our rendering
    if (GetComponent<Renderer>() != null) {
      GetComponent<Renderer>().enabled = bEnable;
    }

    //turn off our kids
    for (int i = 0; i < transform.childCount; ++i) {
      Transform rChild = transform.GetChild(i);
      if (rChild.GetComponent<Renderer>()) {
        rChild.GetComponent<Renderer>().enabled = bEnable;
      }
    }

    //turn off our lines
    for (int i = 0; i < m_NumLines; ++i) {
      if (m_Lines[i].m_Enabled) {
        m_Lines[i].m_Renderer.enabled = bEnable;
      }
    }
  }
}
}  // namespace TiltBrush
