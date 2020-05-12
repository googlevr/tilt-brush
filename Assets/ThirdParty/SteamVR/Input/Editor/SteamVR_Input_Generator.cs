using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEditor;
using UnityEngine;

using System.CodeDom;
using Microsoft.CSharp;
using System.IO;
using System.CodeDom.Compiler;

using System.Reflection;
using System.Linq.Expressions;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using Valve.Newtonsoft.Json;

namespace Valve.VR
{
#pragma warning disable 0219 // variable assigned but not used.

    public static class SteamVR_Input_Generator
    {
        public const string steamVRInputOverwriteBuildKey = "SteamVR_Input_OverwriteBuild";
        public const string steamVRInputDeleteUnusedKey = "SteamVR_Input_DeleteUnused";

        private const string actionSetClassNamePrefix = "SteamVR_Input_ActionSet_";

        public const string generationNeedsReloadKey = "SteamVR_Input_GenerationNeedsReload";

        private const string progressBarTitle = "SteamVR Input Generation";

        public const string steamVRInputActionSetClassesFolder = "ActionSetClasses";
        public const string steamVRInputActionsClass = "SteamVR_Input_Actions";
        public const string steamVRInputActionSetsClass = "SteamVR_Input_ActionSets";
        public const string steamVRInputInitializationClass = "SteamVR_Input_Initialization";
        public const string steamVRActionsAssemblyDefinition = "SteamVR_Actions";

        private static bool generating = false;

        public static void BeginGeneration()
        {
            generating = true;
            fileChanged = false;
            string currentPath = Application.dataPath;
            int lastIndex = currentPath.LastIndexOf('/');
            currentPath = currentPath.Remove(lastIndex, currentPath.Length - lastIndex);


            SteamVR_Input_EditorWindow.SetProgressBarText("Beginning generation...", 0);

            GenerationStep_CreateActionSetClasses();
            GenerationStep_CreateHelperClasses();
            GenerationStep_CreateInitClass();
            GenerationStep_CreateAssemblyDefinition();
            DeleteUnusedScripts();

            if (fileChanged)
                EditorPrefs.SetBool(generationNeedsReloadKey, true);

            AssetDatabase.Refresh();

            SteamVR_Input_EditorWindow.ClearProgressBar();
            generating = false;
        }

        [DidReloadScripts]
        private static void OnReload()
        {
            bool didGenerate = EditorPrefs.GetBool(generationNeedsReloadKey);
            if (didGenerate)
            {
                EditorPrefs.SetBool(generationNeedsReloadKey, false);

                if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path) == false)
                    EditorApplication.delayCall += ReloadScene;
            }
        }


        public static void ReloadScene()
        {
            EditorPrefs.SetBool(generationNeedsReloadKey, false);

            if (string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path) == false)
            {
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                }

                string previousPath = EditorSceneManager.GetActiveScene().path;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                EditorSceneManager.OpenScene(previousPath); //reload open scene to avoid any weird serialization
            }
        }

        public static bool IsGenerating()
        {
            return generating;
        }

        public static void CancelGeneration()
        {
            generating = false;

        }

        private static List<CodeTypeDeclaration> setClasses = new List<CodeTypeDeclaration>();

        private static void GenerationStep_CreateInitClass()
        {
            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeTypeDeclaration inputClass = CreatePartialInputClass(compileUnit);

            CodeMemberMethod preinitMethod = CreateStaticMethod(inputClass, SteamVR_Input_Generator_Names.preinitializeMethodName, true);

            string steamVRInputClassName = typeof(SteamVR_Input).Name;

            AddStaticInvokeToMethod(preinitMethod, SteamVR_Input_Generator_Names.actionsClassName, startPreInitActionSetsMethodName);
            AddStaticInvokeToMethod(preinitMethod, steamVRInputClassName, initializeActionSetDictionariesMethodName);
            AddStaticInvokeToMethod(preinitMethod, SteamVR_Input_Generator_Names.actionsClassName, preInitActionsMethodName);
            AddStaticInvokeToMethod(preinitMethod, SteamVR_Input_Generator_Names.actionsClassName, initializeActionsArraysMethodName);
            AddStaticInvokeToMethod(preinitMethod, steamVRInputClassName, initializeActionDictionariesMethodName);
            AddStaticInvokeToMethod(preinitMethod, steamVRInputClassName, finishPreInitActionSetsMethodName);

            // Build the output file name.
            string fullSourceFilePath = GetSourceFilePath(steamVRInputInitializationClass);
            CreateFile(fullSourceFilePath, compileUnit);
        }

        private static void GenerationStep_CreateAssemblyDefinition()
        {
            string fullSourceFilePath = GetSourceFilePath(steamVRActionsAssemblyDefinition, ".asmdef");

            if (File.Exists(fullSourceFilePath) == false)
            {
                SteamVR_Input_Unity_AssemblyFile_Definition actionsAssemblyDefinitionData = new SteamVR_Input_Unity_AssemblyFile_Definition();
                actionsAssemblyDefinitionData.autoReferenced = true;
                string jsonText = JsonConvert.SerializeObject(actionsAssemblyDefinitionData, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });
                File.WriteAllText(fullSourceFilePath, jsonText);
            }
        }

        private static void GenerationStep_CreateActionSetClasses()
        {
            SteamVR_Input_EditorWindow.SetProgressBarText("Generating action set classes...", 0.25f);

            SteamVR_Input.InitializeFile();

            CreateActionsSubFolder();

            setClasses = GenerateActionSetClasses();

            Debug.LogFormat("<b>[SteamVR Input]</b> Created input script set classes: {0}", setClasses.Count);
        }

        private static void GenerationStep_CreateHelperClasses()
        {
            SteamVR_Input_EditorWindow.SetProgressBarText("Generating actions and actionsets classes...", 0.5f);

            GenerateActionHelpers(steamVRInputActionsClass);
            GenerateActionSetsHelpers(steamVRInputActionSetsClass);

            string actionsFullpath = Path.Combine(GetClassPath(), steamVRInputActionsClass + ".cs");
            string actionSetsFullpath = Path.Combine(GetClassPath(), steamVRInputActionSetsClass + ".cs");

            Debug.LogFormat("<b>[SteamVR Input]</b> Created input script main classes: {0} and {1}", actionsFullpath, actionSetsFullpath);
        }


        private static void DeleteUnusedScripts()
        {
            string folderPath = GetSubFolderPath();

            string[] files = Directory.GetFiles(folderPath);

            List<string> toDelete = new List<string>();

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                FileInfo file = new FileInfo(files[fileIndex]);

                if (file.Name.EndsWith(".cs") || file.Name.EndsWith(".cs.meta"))
                {
                    bool isSet = false;
                    if (SteamVR_Input.actionFile.action_sets.Any(set => string.Equals(GetSetClassName(set) + ".cs", file.Name, StringComparison.CurrentCultureIgnoreCase) ||
                                                                        string.Equals(GetSetClassName(set) + ".cs.meta", file.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {
                        isSet = true;
                    }

                    bool isAction = false;
                    if (SteamVR_Input.actionFile.actions.Any(action => string.Equals(action.codeFriendlyName + ".cs", file.Name, StringComparison.CurrentCultureIgnoreCase) ||
                                                                            string.Equals(action.codeFriendlyName + ".cs.meta", file.Name, StringComparison.CurrentCultureIgnoreCase)))
                    {

                        isAction = true;
                    }

                    if (isSet == false && isAction == false)
                    {
                        toDelete.Add(files[fileIndex]);
                    }
                }
            }

            if (toDelete.Count > 0)
            {
                string filesToDelete = "";
                foreach (string file in toDelete)
                    filesToDelete += file + "\n";

                bool confirm = EditorUtility.DisplayDialog("SteamVR Input", "Would you like to delete the following unused input files:\n" + filesToDelete, "Delete", "No");
                if (confirm)
                {
                    foreach (string fileName in toDelete)
                    {
                        FileInfo file = new FileInfo(fileName);
                        file.IsReadOnly = false;
                        file.Delete();
                    }
                }
            }
        }

        private static void CreateActionsSubFolder()
        {
            string folderPath = GetSubFolderPath();
            if (Directory.Exists(folderPath) == false)
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        public static void DeleteActionClassFiles()
        {
            DeleteActionClass(steamVRInputActionsClass);
            DeleteActionClass(steamVRInputActionSetsClass);

            string folderPath = GetSubFolderPath();
            bool confirm = EditorUtility.DisplayDialog("Confirmation", "Are you absolutely sure you want to delete all code files in " + folderPath + "?", "Delete", "Cancel");
            if (confirm)
            {
                DeleteActionObjects("*.cs*");
            }
        }

        public static void DeleteGeneratedFolder()
        {
            string generatedFolderPath = GetClassPath();
            string subFolderPath = GetSubFolderPath();
            bool confirm = EditorUtility.DisplayDialog("Confirmation", "Are you absolutely sure you want to delete all code files in " + generatedFolderPath + "?", "Delete", "Cancel");
            if (confirm)
            {
                DeleteActionObjects("*.cs*", generatedFolderPath);

                DeleteActionObjects("*.cs*", subFolderPath);
            }
        }

        public static void DeleteActionObjects(string filter, string folderPath = null)
        {
            if (folderPath == null)
                folderPath = GetSubFolderPath();

            string[] assets = Directory.GetFiles(folderPath, filter);

            for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
            {
                AssetDatabase.DeleteAsset(assets[assetIndex]);
            }

            Debug.LogFormat("<b>[SteamVR Input]</b> Deleted {0} files at path: {1}", assets.Length, folderPath);
        }

        private static void DeleteActionClass(string className)
        {
            string filePath = GetSourceFilePath(className);
            if (File.Exists(filePath) == true)
            {
                AssetDatabase.DeleteAsset(filePath);
                Debug.Log("<b>[SteamVR Input]</b> Deleted: " + filePath);
            }
            else
            {
                Debug.Log("<b>[SteamVR Input]</b> No file found at: " + filePath);
            }
        }

        private static string GetTypeStringForAction(SteamVR_Input_ActionFile_Action action)
        {
            return GetTypeForAction(action).Name;
        }

        private static Type GetTypeForAction(SteamVR_Input_ActionFile_Action action)
        {
            string actionType = action.type.ToLower();

            if (SteamVR_Input_ActionFile_ActionTypes.boolean == actionType)
            {
                return typeof(SteamVR_Action_Boolean);
            }

            if (SteamVR_Input_ActionFile_ActionTypes.vector1 == actionType)
            {
                return typeof(SteamVR_Action_Single);
            }

            if (SteamVR_Input_ActionFile_ActionTypes.vector2 == actionType)
            {
                return typeof(SteamVR_Action_Vector2);
            }

            if (SteamVR_Input_ActionFile_ActionTypes.vector3 == actionType)
            {
                return typeof(SteamVR_Action_Vector3);
            }

            if (SteamVR_Input_ActionFile_ActionTypes.pose == actionType)
            {
                return typeof(SteamVR_Action_Pose);
            }

            if (SteamVR_Input_ActionFile_ActionTypes.skeleton == actionType)
            {
                return typeof(SteamVR_Action_Skeleton);
            }

            if (SteamVR_Input_ActionFile_ActionTypes.vibration == actionType)
            {
                return typeof(SteamVR_Action_Vibration);
            }

            throw new System.Exception("unknown type (" + action.type + ") in actions file for action: " + action.name);
        }

        private static string GetClassPath()
        {
            string path = string.Format("Assets/{0}", SteamVR_Settings.instance.steamVRInputPath);

            if (path[0] == '/' || path[0] == '\\')
                path = path.Remove(0, 1);

            return path;
        }

        private static string GetSubFolderPath()
        {
            return Path.Combine(GetClassPath(), steamVRInputActionSetClassesFolder);
        }

        private static string GetSourceFilePath(string classname, string suffix = ".cs")
        {
            string sourceFileName = string.Format("{0}{1}", classname, suffix);

            return Path.Combine(GetClassPath(), sourceFileName);
        }

        private static bool fileChanged = false;
        private static void CreateFile(string fullPath, CodeCompileUnit compileUnit)
        {
            // Generate the code with the C# code provider.
            CSharpCodeProvider provider = new CSharpCodeProvider();

            // Build the output file name.
            string fullSourceFilePath = fullPath;
            //Debug.Log("[SteamVR] Writing class to: " + fullSourceFilePath);

            string path = GetClassPath();
            string[] parts = path.Split('/');

            for (int partIndex = 0; partIndex < parts.Length - 1; partIndex++)
            {
                string directoryPath = string.Join("/", parts.Take(partIndex + 1).ToArray());
                if (Directory.Exists(directoryPath) == false)
                {
                    Directory.CreateDirectory(directoryPath);
                    //Debug.Log("[SteamVR] Created directory: " + directoryPath);
                }
            }

            string priorMD5 = null;
            FileInfo file = new FileInfo(fullSourceFilePath);
            if (file.Exists)
            {
                file.IsReadOnly = false;
                priorMD5 = SteamVR_Utils.GetBadMD5HashFromFile(fullSourceFilePath);
            }

            // Create a TextWriter to a StreamWriter to the output file.
            using (StreamWriter sw = new StreamWriter(fullSourceFilePath, false))
            {
                IndentedTextWriter tw = new IndentedTextWriter(sw, "    ");

                // Generate source code using the code provider.
                provider.GenerateCodeFromCompileUnit(compileUnit, tw,
                    new CodeGeneratorOptions() { BracingStyle = "C" });

                // Close the output file.
                tw.Close();

                string newMD5 = SteamVR_Utils.GetBadMD5HashFromFile(fullSourceFilePath);

                if (priorMD5 != newMD5)
                    fileChanged = true;
            }

            //Debug.Log("[SteamVR] Complete! Input class at: " + fullSourceFilePath);
        }

        private const string getActionMethodParamName = "path";
        private const string skipStateUpdatesParamName = "skipStateAndEventUpdates";

        private static List<CodeTypeDeclaration> GenerateActionSetClasses()
        {
            List<CodeTypeDeclaration> setClasses = new List<CodeTypeDeclaration>();

            for (int actionSetIndex = 0; actionSetIndex < SteamVR_Input.actionFile.action_sets.Count; actionSetIndex++)
            {
                SteamVR_Input_ActionFile_ActionSet actionSet = SteamVR_Input.actionFile.action_sets[actionSetIndex];

                CodeTypeDeclaration setClass = CreateActionSetClass(actionSet);

                setClasses.Add(setClass);
            }

            return setClasses;
        }

        private const string initializeActionDictionariesMethodName = "PreinitializeActionDictionaries";
        private const string initializeActionSetDictionariesMethodName = "PreinitializeActionSetDictionaries";

        private const string preInitActionsMethodName = "PreInitActions";
        private const string initializeActionsArraysMethodName = "InitializeActionArrays";

        private static void GenerateActionHelpers(string actionsClassFileName)
        {
            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeTypeDeclaration inputClass = CreatePartialInputClass(compileUnit);

            CodeArrayCreateExpression actionsArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action)));

            CodeArrayCreateExpression actionsInArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(ISteamVR_Action_In)));

            CodeArrayCreateExpression actionsOutArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(ISteamVR_Action_Out)));

            CodeArrayCreateExpression actionsVibrationArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action_Vibration)));

            CodeArrayCreateExpression actionsPoseArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action_Pose)));

            CodeArrayCreateExpression actionsSkeletonArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action_Skeleton)));

            CodeArrayCreateExpression actionsBooleanArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action_Boolean)));

            CodeArrayCreateExpression actionsSingleArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action_Single)));

            CodeArrayCreateExpression actionsVector2Array = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action_Vector2)));

            CodeArrayCreateExpression actionsVector3Array = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_Action_Vector3)));

            CodeArrayCreateExpression actionsNonPoseNonSkeletonArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(ISteamVR_Action_In)));


            //add the getaction method to
            CodeMemberMethod actionsArraysInitMethod = CreateStaticMethod(inputClass, initializeActionsArraysMethodName, false);
            CodeMemberMethod actionsPreInitMethod = CreateStaticMethod(inputClass, preInitActionsMethodName, false);



            for (int actionSetIndex = 0; actionSetIndex < SteamVR_Input.actionFile.action_sets.Count; actionSetIndex++)
            {
                SteamVR_Input_ActionFile_ActionSet actionSet = SteamVR_Input.actionFile.action_sets[actionSetIndex];
                string actionSetShortName = actionSet.shortName;
                actionSetShortName = actionSetShortName.Substring(0, 1).ToLower() + actionSetShortName.Substring(1);

                for (int actionIndex = 0; actionIndex < actionSet.actionsList.Count; actionIndex++)
                {
                    SteamVR_Input_ActionFile_Action action = actionSet.actionsList[actionIndex];
                    string actionShortName = action.shortName;

                    string typeName = GetTypeStringForAction(action);

                    string codeFriendlyInstanceName;
                    if (actionSet.actionsList.Count(findAction => findAction.shortName == actionShortName) >= 2)
                        codeFriendlyInstanceName = string.Format("{0}_{1}_{2}", actionSetShortName, action.direction.ToString().ToLower(), actionShortName);
                    else
                        codeFriendlyInstanceName = string.Format("{0}_{1}", actionSetShortName, actionShortName);


                    CodeMemberField actionField = CreateFieldAndPropertyWrapper(inputClass, codeFriendlyInstanceName, typeName);

                    AddAssignActionStatement(actionsPreInitMethod, inputClass.Name, actionField.Name, action.name, typeName);

                    actionsArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));

                    if (action.direction == SteamVR_ActionDirections.In)
                    {
                        actionsInArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));

                        if (typeName == typeof(SteamVR_Action_Pose).Name)
                        {
                            actionsPoseArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                        }
                        else if (typeName == typeof(SteamVR_Action_Skeleton).Name)
                        {
                            actionsSkeletonArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                        }
                        else if (typeName == typeof(SteamVR_Action_Boolean).Name)
                        {
                            actionsBooleanArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                        }
                        else if (typeName == typeof(SteamVR_Action_Single).Name)
                        {
                            actionsSingleArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                        }
                        else if (typeName == typeof(SteamVR_Action_Vector2).Name)
                        {
                            actionsVector2Array.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                        }
                        else if (typeName == typeof(SteamVR_Action_Vector3).Name)
                        {
                            actionsVector3Array.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                        }

                        if ((typeName == typeof(SteamVR_Action_Skeleton).Name) == false && (typeName == typeof(SteamVR_Action_Pose).Name) == false)
                        {
                            actionsNonPoseNonSkeletonArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                        }
                    }
                    else
                    {
                        actionsVibrationArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));

                        actionsOutArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
                    }
                }
            }

            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsFieldName, actionsArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsInFieldName, actionsInArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsOutFieldName, actionsOutArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsVibrationFieldName, actionsVibrationArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsPoseFieldName, actionsPoseArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsBooleanFieldName, actionsBooleanArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsSingleFieldName, actionsSingleArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsVector2FieldName, actionsVector2Array);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsVector3FieldName, actionsVector3Array);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsSkeletonFieldName, actionsSkeletonArray);
            AddAssignStatement(actionsArraysInitMethod, SteamVR_Input_Generator_Names.actionsNonPoseNonSkeletonIn, actionsNonPoseNonSkeletonArray);


            // Build the output file name.
            string fullSourceFilePath = GetSourceFilePath(actionsClassFileName);
            CreateFile(fullSourceFilePath, compileUnit);
        }


        private const string startPreInitActionSetsMethodName = "StartPreInitActionSets";
        private const string finishPreInitActionSetsMethodName = "PreinitializeFinishActionSets";

        private static void GenerateActionSetsHelpers(string actionSetsClassFileName)
        {
            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeTypeDeclaration inputClass = CreatePartialInputClass(compileUnit);


            CodeMemberMethod startPreInitActionSetsMethod = CreateStaticMethod(inputClass, startPreInitActionSetsMethodName, false);

            CodeArrayCreateExpression actionSetsArray = new CodeArrayCreateExpression(new CodeTypeReference(typeof(SteamVR_ActionSet)));

            for (int actionSetIndex = 0; actionSetIndex < SteamVR_Input.actionFile.action_sets.Count; actionSetIndex++)
            {
                SteamVR_Input_ActionFile_ActionSet actionSet = SteamVR_Input.actionFile.action_sets[actionSetIndex];

                string shortName = GetValidIdentifier(actionSet.shortName);

                string codeFriendlyInstanceName = shortName;

                string setTypeName = GetSetClassName(actionSet);

                CodeMemberField actionSetField = CreateFieldAndPropertyWrapper(inputClass, shortName, setTypeName);

                AddAssignActionSetStatement(startPreInitActionSetsMethod, inputClass.Name, actionSetField.Name, actionSet.name, setTypeName);

                actionSetsArray.Initializers.Add(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), codeFriendlyInstanceName));
            }

            AddAssignStatement(startPreInitActionSetsMethod, SteamVR_Input_Generator_Names.actionSetsFieldName, actionSetsArray);

            // Build the output file name.
            string fullSourceFilePath = GetSourceFilePath(actionSetsClassFileName);
            CreateFile(fullSourceFilePath, compileUnit);
        }

        private static CSharpCodeProvider provider = new CSharpCodeProvider();
        private static string GetValidIdentifier(string name)
        {
            string newName = name.Replace("-", "_");
            newName = provider.CreateValidIdentifier(newName);
            return newName;
        }

        public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression)
        {
            var member = expression.Body as MethodCallExpression;

            if (member != null)
                return member.Method;

            throw new ArgumentException("Expression is not a method", "expression");
        }

        private static CodeTypeDeclaration CreatePartialInputClass(CodeCompileUnit compileUnit)
        {
            CodeNamespace codeNamespace = new CodeNamespace(typeof(SteamVR_Input).Namespace);
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("UnityEngine"));
            compileUnit.Namespaces.Add(codeNamespace);

            CodeTypeDeclaration inputClass = new CodeTypeDeclaration(SteamVR_Input_Generator_Names.actionsClassName);
            inputClass.IsPartial = true;
            codeNamespace.Types.Add(inputClass);

            return inputClass;
        }

        private static string GetSetClassName(SteamVR_Input_ActionFile_ActionSet set)
        {
            return actionSetClassNamePrefix + set.shortName;
        }

        private const string inActionFieldPrefix = "in_";
        private const string outActionFieldPrefix = "out_";
        private const string setFinishPreInitializeMethodName = "FinishPreInitialize";
        private static CodeTypeDeclaration CreateActionSetClass(SteamVR_Input_ActionFile_ActionSet set)
        {
            CodeCompileUnit compileUnit = new CodeCompileUnit();

            CodeNamespace codeNamespace = new CodeNamespace(typeof(SteamVR_Input).Namespace);
            codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
            codeNamespace.Imports.Add(new CodeNamespaceImport("UnityEngine"));
            compileUnit.Namespaces.Add(codeNamespace);

            CodeTypeDeclaration setClass = new CodeTypeDeclaration(GetSetClassName(set));
            setClass.BaseTypes.Add(typeof(SteamVR_ActionSet));
            setClass.Attributes = MemberAttributes.Public;
            codeNamespace.Types.Add(setClass);

            string actionSetShortName = set.shortName;
            actionSetShortName = actionSetShortName.Substring(0, 1).ToLower() + actionSetShortName.Substring(1);

            foreach (var inAction in set.actionsInList)
            {
                string inActionName = inAction.shortName;
                if (set.actionsOutList.Any(outAction => inAction.shortName == outAction.shortName))
                    inActionName = inActionFieldPrefix + inActionName;

                string actionClassPropertyName = string.Format("{0}_{1}", actionSetShortName, inActionName);

                CreateActionPropertyWrapper(setClass, SteamVR_Input_Generator_Names.actionsClassName, inActionName, actionClassPropertyName, inAction);
            }

            foreach (var outAction in set.actionsOutList)
            {
                string outActionName = outAction.shortName;
                if (set.actionsInList.Any(inAction => inAction.shortName == outAction.shortName))
                    outActionName = outActionFieldPrefix + outActionName;

                string actionClassPropertyName = string.Format("{0}_{1}", actionSetShortName, outActionName);

                CreateActionPropertyWrapper(setClass, SteamVR_Input_Generator_Names.actionsClassName, outActionName, actionClassPropertyName, outAction);
            }

            // Build the output file name.
            string folderPath = GetSubFolderPath();
            string fullSourceFilePath = Path.Combine(folderPath, setClass.Name + ".cs");
            CreateFile(fullSourceFilePath, compileUnit);

            return setClass;
        }

        private static CodeMemberMethod CreateStaticMethod(CodeTypeDeclaration inputClass, string methodName, bool isPublic)
        {
            CodeMemberMethod method = new CodeMemberMethod();
            method.Name = methodName;

            if (isPublic)
                method.Attributes = MemberAttributes.Public | MemberAttributes.Static;
            else
                method.Attributes = MemberAttributes.Private | MemberAttributes.Static;

            inputClass.Members.Add(method);
            return method;
        }

        private static CodeMemberMethod CreateStaticConstructorMethod(CodeTypeDeclaration inputClass)
        {
            CodeTypeConstructor method = new CodeTypeConstructor();
            method.Attributes = MemberAttributes.Static;

            inputClass.Members.Add(method);
            return method;
        }

        private static CodeMemberField CreateField(CodeTypeDeclaration inputClass, string fieldName, Type fieldType, bool isStatic)
        {
            if (fieldType == null)
                Debug.Log("null fieldType");

            CodeMemberField field = new CodeMemberField();
            field.Name = fieldName;
            field.Type = new CodeTypeReference(fieldType);
            field.Attributes = MemberAttributes.Public;
            if (isStatic)
                field.Attributes |= MemberAttributes.Static;

            inputClass.Members.Add(field);

            return field;
        }

        private static CodeMemberField CreateFieldAndPropertyWrapper(CodeTypeDeclaration inputClass, string name, string type)
        {
            CodeMemberField actionField = CreatePrivateField(inputClass, name, type, true);

            CodeMemberProperty actionProperty = CreateStaticProperty(inputClass, name, type, actionField);

            return actionField;
        }

        private static CodeMemberProperty CreateStaticProperty(CodeTypeDeclaration inputClass, string propertyName, string propertyType, CodeMemberField privateField)
        {
            CodeMemberProperty property = new CodeMemberProperty();
            property.Name = propertyName;
            property.Type = new CodeTypeReference(propertyType);
            property.Attributes = MemberAttributes.Public | MemberAttributes.Static;

            CodeFieldReferenceExpression fieldReference = new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(inputClass.Name), privateField.Name);
            CodeMethodInvokeExpression invokeExpression = new CodeMethodInvokeExpression(fieldReference, "GetCopy");
            invokeExpression.Method.TypeArguments.Add(property.Type);

            CodeMethodReturnStatement returnStatement = new CodeMethodReturnStatement(invokeExpression);

            property.GetStatements.Add(returnStatement);

            inputClass.Members.Add(property);

            return property;
        }

        private static CodeMemberProperty CreateActionPropertyWrapper(CodeTypeDeclaration addToClass, string actionClass, string propertyName, string actionClassFieldName, SteamVR_Input_ActionFile_Action action)
        {
            string propertyType = GetTypeStringForAction(action);

            CodeMemberProperty property = new CodeMemberProperty();
            property.Name = propertyName;
            property.Type = new CodeTypeReference(propertyType);
            property.Attributes = MemberAttributes.Public;

            CodeFieldReferenceExpression fieldReference = new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(actionClass), actionClassFieldName);

            CodeMethodReturnStatement returnStatement = new CodeMethodReturnStatement(fieldReference);

            property.GetStatements.Add(returnStatement);

            addToClass.Members.Add(property);

            return property;
        }

        private const string privateFieldPrefix = "p_";
        private static CodeMemberField CreatePrivateField(CodeTypeDeclaration inputClass, string fieldName, string fieldType, bool isStatic)
        {
            return CreateField(inputClass, privateFieldPrefix + fieldName, fieldType, isStatic, false);
        }

        private static CodeMemberField CreateField(CodeTypeDeclaration inputClass, string fieldName, string fieldType, bool isStatic, bool isPublic = true)
        {
            CodeMemberField field = new CodeMemberField();
            field.Name = fieldName;
            field.Type = new CodeTypeReference(fieldType);
            if (isPublic)
                field.Attributes = MemberAttributes.Public;
            else
                field.Attributes = MemberAttributes.Private;

            if (isStatic)
                field.Attributes |= MemberAttributes.Static;

            inputClass.Members.Add(field);

            return field;
        }

        private static CodeMethodInvokeExpression AddStaticInvokeToMethod(CodeMemberMethod methodToAddTo, string classToInvoke, string invokeMethodName)
        {
            CodeMethodInvokeExpression invokeMethod = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                new CodeTypeReferenceExpression(classToInvoke), invokeMethodName));

            methodToAddTo.Statements.Add(invokeMethod);

            return invokeMethod;
        }

        private static void AddAssignStatement(CodeMemberMethod methodToAddTo, string fieldToAssign, CodeArrayCreateExpression array)
        {
            methodToAddTo.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(typeof(SteamVR_Input)), fieldToAssign), array));
        }

        private const string createActionMethodName = "Create";
        private const string createActionSetMethodName = "Create";
        private const string getActionFromPathMethodName = "GetActionFromPath";

        //grab = SteamVR_Action.Create<SteamVR_Action_Boolean>("path");
        private static void AddAssignActionStatement(CodeMemberMethod methodToAddTo, string actionClassName, string fieldToAssign, string actionPath, string actionType)
        {
            CodeMethodInvokeExpression invokeMethod = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(SteamVR_Action).Name), createActionMethodName));

            invokeMethod.Method.TypeArguments.Add(actionType);
            invokeMethod.Parameters.Add(new CodePrimitiveExpression(actionPath));

            methodToAddTo.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(actionClassName), fieldToAssign), new CodeCastExpression(new CodeTypeReference(actionType), invokeMethod)));
        }
        private static void AddAssignActionSetStatement(CodeMemberMethod methodToAddTo, string actionClassName, string fieldToAssign, string actionSetName, string actionSetType)
        {
            CodeMethodInvokeExpression invokeMethod = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(SteamVR_ActionSet).Name), createActionSetMethodName));

            invokeMethod.Method.TypeArguments.Add(actionSetType);
            invokeMethod.Parameters.Add(new CodePrimitiveExpression(actionSetName));

            methodToAddTo.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(actionClassName), fieldToAssign), new CodeCastExpression(new CodeTypeReference(actionSetType), invokeMethod)));
        }
        private static void AddAssignLocalActionStatement(CodeMemberMethod methodToAddTo, string fieldToAssign, string actionPath, string actionType, bool create)
        {
            CodeMethodInvokeExpression invokeMethod;

            if (create)
                invokeMethod = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(SteamVR_Action).Name), createActionMethodName));
            else
                invokeMethod = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(SteamVR_Input).Name), getActionFromPathMethodName));

            invokeMethod.Method.TypeArguments.Add(actionType);
            invokeMethod.Parameters.Add(new CodePrimitiveExpression(actionPath));

            methodToAddTo.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), fieldToAssign), new CodeCastExpression(new CodeTypeReference(actionType), invokeMethod)));
        }
        private static void AddAssignNewInstanceStatement(CodeMemberMethod methodToAddTo, string fieldToAssign, string fieldType)
        {
            CodeObjectCreateExpression createExpression = new CodeObjectCreateExpression(new CodeTypeReference(fieldType));

            methodToAddTo.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), fieldToAssign), createExpression));
        }

        private static CodeConditionStatement CreateStringCompareStatement(CodeMemberMethod methodToAddTo, string action, string paramName, string returnActionName)
        {
            MethodInfo stringEqualsMethodInfo = GetMethodInfo<string>(set => string.Equals(null, null, StringComparison.CurrentCultureIgnoreCase));
            CodeTypeReferenceExpression stringType = new CodeTypeReferenceExpression(typeof(string));
            CodePrimitiveExpression actionName = new CodePrimitiveExpression(action);
            CodeVariableReferenceExpression pathName = new CodeVariableReferenceExpression(paramName);
            CodeVariableReferenceExpression caseInvariantName = new CodeVariableReferenceExpression("StringComparison.CurrentCultureIgnoreCase");
            CodeMethodInvokeExpression stringCompare = new CodeMethodInvokeExpression(stringType, stringEqualsMethodInfo.Name, pathName, actionName, caseInvariantName);
            CodeMethodReturnStatement returnAction = new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(typeof(SteamVR_Input)), returnActionName));

            CodeConditionStatement condition = new CodeConditionStatement(stringCompare, returnAction);
            methodToAddTo.Statements.Add(condition);

            return condition;
        }
    }
}