using UnityEngine;
using System.Collections;
using UnityEditor;
using System;
using System.Linq;
using System.IO;

namespace Valve.VR
{
    public class SteamVR_CopyExampleInputFiles : Editor
    {
        public const string steamVRInputExampleJSONCopiedKey = "SteamVR_Input_CopiedExamples";

        public const string exampleJSONFolderName = "ExampleJSON";

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnReloadScripts()
        {
            CopyFiles();
        }

        public static void CopyFiles(bool force = false)
        {
            bool hasCopied = EditorPrefs.GetBool(steamVRInputExampleJSONCopiedKey, false);
            if (hasCopied == false || force == true)
            {
                string actionsFilePath = SteamVR_Settings.instance.actionsFilePath;
                bool exists = File.Exists(actionsFilePath);
                if (exists == false)
                {
                    MonoScript[] monoScripts = MonoImporter.GetAllRuntimeMonoScripts();

                    Type steamVRInputType = typeof(SteamVR_Input);
                    MonoScript monoScript = monoScripts.FirstOrDefault(script => script.GetClass() == steamVRInputType);
                    string path = AssetDatabase.GetAssetPath(monoScript);

                    int lastIndex = path.LastIndexOf("/");
                    path = path.Substring(0, lastIndex + 1);
                    path += exampleJSONFolderName;

                    string dataPath = Application.dataPath;
                    lastIndex = dataPath.LastIndexOf("/Assets");
                    dataPath = dataPath.Substring(0, lastIndex + 1);

                    path = dataPath + path;

                    string[] files = Directory.GetFiles(path, "*.json");
                    foreach (string file in files)
                    {
                        lastIndex = file.LastIndexOf("\\");
                        string filename = file.Substring(lastIndex + 1);

                        string newPath = Path.Combine(dataPath, filename);

                        try
                        {
                            File.Copy(file, newPath, false);
                            Debug.Log("<b>[SteamVR]</b> Copied example input JSON to path: " + newPath);
                        }
                        catch
                        {
                            Debug.LogError("<b>[SteamVR]</b> Could not copy file: " + file + " to path: " + newPath);
                        }
                    }

                    EditorPrefs.SetBool(steamVRInputExampleJSONCopiedKey, true);
                }
            }
        }
    }
}