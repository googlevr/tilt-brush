using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEditor.Callbacks;
using System.IO;

namespace Valve.VR
{
    public class SteamVR_Input_PostProcessBuild
    {
        [PostProcessBuildAttribute(1)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            SteamVR_Input.InitializeFile();
            
            FileInfo fileInfo = new FileInfo(pathToBuiltProject);
            string buildPath = Path.Combine(fileInfo.Directory.FullName, Path.GetDirectoryName(SteamVR_Settings.instance.actionsFilePath));
            bool overwrite = EditorPrefs.GetBool(SteamVR_Input_Generator.steamVRInputOverwriteBuildKey);

            SteamVR_Input.actionFile.CopyFilesToPath(buildPath, overwrite);
        }

        private static void UpdateAppKey(string newFilePath, string executableName)
        {
            if (File.Exists(newFilePath))
            {
                string jsonText = System.IO.File.ReadAllText(newFilePath);

                string findString = "\"app_key\" : \"";
                int stringStart = jsonText.IndexOf(findString);

                if (stringStart == -1)
                {
                    findString = findString.Replace(" ", "");
                    stringStart = jsonText.IndexOf(findString);

                    if (stringStart == -1)
                        return; //no app key
                }

                stringStart += findString.Length;
                int stringEnd = jsonText.IndexOf("\"", stringStart);

                int stringLength = stringEnd - stringStart;

                string currentAppKey = jsonText.Substring(stringStart, stringLength);

                if (string.Equals(currentAppKey, SteamVR_Settings.instance.editorAppKey, System.StringComparison.CurrentCultureIgnoreCase) == false)
                {
                    jsonText = jsonText.Replace(currentAppKey, SteamVR_Settings.instance.editorAppKey);

                    FileInfo file = new FileInfo(newFilePath);
                    file.IsReadOnly = false;

                    File.WriteAllText(newFilePath, jsonText);
                }
            }
        }
    }
}