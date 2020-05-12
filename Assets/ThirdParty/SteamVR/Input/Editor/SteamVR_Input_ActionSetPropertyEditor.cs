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


namespace Valve.VR
{
    [CustomPropertyDrawer(typeof(SteamVR_ActionSet))]
    public class SteamVR_Input_ActionSetPropertyEditor : PropertyDrawer
    {
        protected SteamVR_ActionSet[] actionSets;
        protected string[] enumItems;
        public int selectedIndex = notInitializedIndex;

        protected const int notInitializedIndex = -1;
        protected const int noneIndex = 0;
        protected int addIndex = 1;

        protected const string defaultPathTemplate = "    \u26A0 Missing action set: {0}";
        protected string defaultPathLabel = null;

        protected void Awake()
        {
            actionSets = SteamVR_Input.GetActionSets();
            if (actionSets != null && actionSets.Length > 0)
            {
                List<string> enumList = actionSets.Select(actionSet => actionSet.fullPath).ToList();

                enumList.Insert(noneIndex, "None");

                //replace forward slashes with backslack instead
                for (int index = 0; index < enumList.Count; index++)
                    enumList[index] = enumList[index].Replace('/', '\\');

                enumList.Add("Add...");
                enumItems = enumList.ToArray();
            }
            else
            {
                enumItems = new string[] { "None", "Add..." };
            }

            addIndex = enumItems.Length - 1;

            /*
            //keep sub menus:
            for (int index = 0; index < enumItems.Length; index++)
                if (enumItems[index][0] == '/')
                    enumItems[index] = enumItems[index].Substring(1);
            */
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = base.GetPropertyHeight(property, label);

            SerializedProperty actionPathProperty = property.FindPropertyRelative("actionSetPath");
            if (string.IsNullOrEmpty(actionPathProperty.stringValue) == false)
            {
                if (selectedIndex == 0)
                    return height * 2;
            }

            return height;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (SteamVR_Input.actions == null || SteamVR_Input.actions.Length == 0)
            {
                EditorGUI.BeginProperty(position, label, property);
                EditorGUI.LabelField(position, "Please generate SteamVR Input actions");
                EditorGUI.EndProperty();
                return;
            }

            if (enumItems == null || enumItems.Length == 0)
            {
                Awake();
            }

            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty actionPathProperty = property.FindPropertyRelative("actionSetPath");
            string currentPath = null;

            if (actionPathProperty != null)
            {
                currentPath = actionPathProperty.stringValue;
                if (string.IsNullOrEmpty(currentPath) == false)
                {
                    for (int actionSetIndex = 0; actionSetIndex < actionSets.Length; actionSetIndex++)
                    {
                        if (actionSets[actionSetIndex].fullPath == currentPath)
                        {
                            selectedIndex = actionSetIndex + 1; // account for none option
                            break;
                        }
                    }
                }
            }

            if (selectedIndex == notInitializedIndex)
                selectedIndex = 0;


            Rect labelPosition = position;
            labelPosition.width = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(labelPosition, label);

            Rect fieldPosition = position;
            fieldPosition.x = (labelPosition.x + labelPosition.width);
            fieldPosition.width = EditorGUIUtility.currentViewWidth - (labelPosition.x + labelPosition.width) - 5 - 16;

            if (selectedIndex == 0 && string.IsNullOrEmpty(currentPath) == false)
            {
                if (defaultPathLabel == null)
                    defaultPathLabel = string.Format(defaultPathTemplate, currentPath);

                Rect defaultLabelPosition = position;
                defaultLabelPosition.y = position.y + fieldPosition.height / 2f;

                EditorGUI.LabelField(defaultLabelPosition, defaultPathLabel);
            }

            Rect objectRect = position;
            objectRect.x = fieldPosition.x + fieldPosition.width + 15;
            objectRect.width = 10;

            bool showInputWindow = false;

            int wasSelected = selectedIndex;
            selectedIndex = EditorGUI.Popup(fieldPosition, selectedIndex, enumItems);
            if (selectedIndex != wasSelected)
            {
                if (selectedIndex == noneIndex || selectedIndex == notInitializedIndex)
                {
                    selectedIndex = noneIndex;

                    actionPathProperty.stringValue = null;
                }
                else if (selectedIndex == addIndex)
                {
                    selectedIndex = wasSelected; // don't change the index
                    showInputWindow = true;
                }
                else
                {
                    int actionIndex = selectedIndex - 1; // account for none option

                    actionPathProperty.stringValue = actionSets[actionIndex].GetPath();
                    //property.objectReferenceValue = actions[actionIndex];
                }

                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();

            if (showInputWindow)
                SteamVR_Input_EditorWindow.ShowWindow(); //show the input window so they can add a new actionset
        }
    }
}