using UnityEditor;
using UnityEngine;

using System.CodeDom;
using Microsoft.CSharp;
using System.IO;
using System.CodeDom.Compiler;

using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;
using System;
using UnityEditorInternal;

namespace Valve.VR
{
    public class SteamVR_Input_LiveWindow : EditorWindow
    {
        private GUIStyle labelStyle;
        private GUIStyle setLabelStyle;

        [MenuItem("Window/SteamVR Input Live View")]
        public static void ShowWindow()
        {
            GetWindow<SteamVR_Input_LiveWindow>(false, "SteamVR Input Live View", true);
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private Vector2 scrollPosition;

        private Dictionary<SteamVR_Input_Sources, bool> sourceFoldouts = null;
        private Dictionary<SteamVR_Input_Sources, Dictionary<string, bool>> setFoldouts = null;

        Color inactiveSetColor = Color.Lerp(Color.red, Color.white, 0.5f);
        Color actionUnboundColor = Color.red;
        Color actionChangedColor = Color.green;
        Color actionNotUpdatingColor = Color.yellow;

        private void DrawMap()
        {
            EditorGUILayout.BeginHorizontal();
            //GUILayout.FlexibleSpace();

            GUI.backgroundColor = actionUnboundColor;
            EditorGUILayout.LabelField("Not Bound", labelStyle);

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = inactiveSetColor;
            EditorGUILayout.LabelField("Inactive", labelStyle);

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = actionNotUpdatingColor;
            EditorGUILayout.LabelField("Not Used Yet", labelStyle);

            GUILayout.FlexibleSpace();

            GUI.backgroundColor = actionChangedColor;
            EditorGUILayout.LabelField("Changed", labelStyle);

            //GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
        }

        private void OnGUI()
        {
            if (SteamVR_Input.actionSets == null)
            {
                EditorGUILayout.LabelField("Must first generate actions. Open SteamVR Input window.");
                return;
            }

            bool startUpdatingSourceOnAccess = SteamVR_Action.startUpdatingSourceOnAccess;
            SteamVR_Action.startUpdatingSourceOnAccess = false;

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.textField);
                labelStyle.normal.background = Texture2D.whiteTexture;
                
                setLabelStyle = new GUIStyle(EditorStyles.label);
                setLabelStyle.wordWrap = true;
                setLabelStyle.normal.background = Texture2D.whiteTexture;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            Color defaultColor = GUI.backgroundColor;

            SteamVR_ActionSet[] actionSets = SteamVR_Input.actionSets;
            SteamVR_Input_Sources[] sources = SteamVR_Input_Source.GetAllSources();

            if (sourceFoldouts == null)
            {
                sourceFoldouts = new Dictionary<SteamVR_Input_Sources, bool>();
                setFoldouts = new Dictionary<SteamVR_Input_Sources, Dictionary<string, bool>>();
                for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                {
                    sourceFoldouts.Add(sources[sourceIndex], false);
                    setFoldouts.Add(sources[sourceIndex], new Dictionary<string, bool>());

                    for (int actionSetIndex = 0; actionSetIndex < actionSets.Length; actionSetIndex++)
                    {
                        SteamVR_ActionSet set = actionSets[actionSetIndex];
                        setFoldouts[sources[sourceIndex]].Add(set.GetShortName(), true);
                    }
                }

                sourceFoldouts[SteamVR_Input_Sources.Any] = true;
                sourceFoldouts[SteamVR_Input_Sources.LeftHand] = true;
                sourceFoldouts[SteamVR_Input_Sources.RightHand] = true;
            }

            DrawMap();
            
            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                SteamVR_Input_Sources source = sources[sourceIndex];
                sourceFoldouts[source] = EditorGUILayout.Foldout(sourceFoldouts[source], source.ToString());

                if (sourceFoldouts[source] == false)
                    continue;

                EditorGUI.indentLevel++;

                for (int actionSetIndex = 0; actionSetIndex < actionSets.Length; actionSetIndex++)
                {
                    SteamVR_ActionSet set = actionSets[actionSetIndex];
                    bool setActive = set.IsActive(source);
                    string activeText = setActive ? "Active" : "Inactive";
                    float setLastChanged = set.GetTimeLastChanged();

                    if (setLastChanged != -1)
                    {
                        float timeSinceLastChanged = Time.realtimeSinceStartup - setLastChanged;
                        if (timeSinceLastChanged < 1)
                        {
                            Color blendColor = setActive ? Color.green : inactiveSetColor;
                            Color setColor = Color.Lerp(blendColor, defaultColor, timeSinceLastChanged);
                            GUI.backgroundColor = setColor;
                        }
                    }

                    EditorGUILayout.BeginHorizontal();
                        setFoldouts[source][set.GetShortName()] = EditorGUILayout.Foldout(setFoldouts[source][set.GetShortName()], set.GetShortName());

                        EditorGUILayout.LabelField(activeText, labelStyle);

                        GUI.backgroundColor = defaultColor;
                    EditorGUILayout.EndHorizontal();

                    if (setFoldouts[source][set.GetShortName()] == false)
                        continue;

                    EditorGUI.indentLevel++;

                    for (int actionIndex = 0; actionIndex < set.allActions.Length; actionIndex++)
                    {
                        SteamVR_Action action = set.allActions[actionIndex];
                        if (source != SteamVR_Input_Sources.Any && action is SteamVR_Action_Skeleton)
                            continue;

                        bool isUpdating = action.IsUpdating(source);
                        bool inAction = action is ISteamVR_Action_In;

                        bool noData = false;
                        if (inAction && isUpdating == false)
                        {
                            GUI.backgroundColor = Color.yellow;
                            noData = true;
                        }
                        else
                        {
                            bool actionBound = action.GetActiveBinding(source);
                            if (setActive == false)
                            {
                                GUI.backgroundColor = inactiveSetColor;
                            }
                            else if (actionBound == false)
                            {
                                GUI.backgroundColor = Color.red;
                                noData = true;
                            }
                        }

                        if (noData)
                        {
                            EditorGUILayout.LabelField(action.GetShortName(), "-", labelStyle);
                            GUI.backgroundColor = defaultColor;
                            continue;
                        }

                        float actionLastChanged = action.GetTimeLastChanged(source);

                        string actionText = "";

                        float timeSinceLastChanged = -1;

                        if (actionLastChanged != -1)
                        {
                            timeSinceLastChanged = Time.realtimeSinceStartup - actionLastChanged;

                            if (timeSinceLastChanged < 1)
                            {
                                Color setColor = Color.Lerp(Color.green, defaultColor, timeSinceLastChanged);
                                GUI.backgroundColor = setColor;
                            }
                        }


                        if (action is SteamVR_Action_Boolean)
                        {
                            SteamVR_Action_Boolean actionBoolean = (SteamVR_Action_Boolean)action;
                            actionText = actionBoolean.GetState(source).ToString();
                        }
                        else if (action is SteamVR_Action_Single)
                        {
                            SteamVR_Action_Single actionSingle = (SteamVR_Action_Single)action;
                            actionText = actionSingle.GetAxis(source).ToString("0.0000");
                        }
                        else if (action is SteamVR_Action_Vector2)
                        {
                            SteamVR_Action_Vector2 actionVector2 = (SteamVR_Action_Vector2)action;
                            actionText = string.Format("({0:0.0000}, {1:0.0000})", actionVector2.GetAxis(source).x, actionVector2.GetAxis(source).y);
                        }
                        else if (action is SteamVR_Action_Vector3)
                        {
                            SteamVR_Action_Vector3 actionVector3 = (SteamVR_Action_Vector3)action;
                            Vector3 axis = actionVector3.GetAxis(source);
                            actionText = string.Format("({0:0.0000}, {1:0.0000}, {2:0.0000})", axis.x, axis.y, axis.z);
                        }
                        else if (action is SteamVR_Action_Pose)
                        {
                            SteamVR_Action_Pose actionPose = (SteamVR_Action_Pose)action;
                            Vector3 position = actionPose.GetLocalPosition(source);
                            Quaternion rotation = actionPose.GetLocalRotation(source);
                            actionText = string.Format("({0:0.0000}, {1:0.0000}, {2:0.0000}) : ({3:0.0000}, {4:0.0000}, {5:0.0000}, {6:0.0000})",
                                position.x, position.y, position.z,
                                rotation.x, rotation.y, rotation.z, rotation.w);
                        }
                        else if (action is SteamVR_Action_Skeleton)
                        {
                            SteamVR_Action_Skeleton actionSkeleton = (SteamVR_Action_Skeleton)action;
                            Vector3 position = actionSkeleton.GetLocalPosition(source);
                            Quaternion rotation = actionSkeleton.GetLocalRotation(source);
                            actionText = string.Format("({0:0.0000}, {1:0.0000}, {2:0.0000}) : ({3:0.0000}, {4:0.0000}, {5:0.0000}, {6:0.0000})",
                                position.x, position.y, position.z,
                                rotation.x, rotation.y, rotation.z, rotation.w);
                        }
                        else if (action is SteamVR_Action_Vibration)
                        {
                            //SteamVR_Input_Action_Vibration actionVibration = (SteamVR_Input_Action_Vibration)action;

                            if (timeSinceLastChanged == -1)
                                actionText = "never used";

                            actionText = string.Format("{0:0} seconds since last used", timeSinceLastChanged);
                        }

                        EditorGUILayout.LabelField(action.GetShortName(), actionText, labelStyle);
                        GUI.backgroundColor = defaultColor;
                    }

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space();
                }


                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Active Action Set List");
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(SteamVR_ActionSet_Manager.debugActiveSetListText, setLabelStyle);
            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            EditorGUILayout.EndScrollView();

            SteamVR_Action.startUpdatingSourceOnAccess = startUpdatingSourceOnAccess;
        }
    }
}