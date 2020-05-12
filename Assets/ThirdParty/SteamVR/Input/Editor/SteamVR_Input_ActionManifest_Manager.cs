using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Valve.Newtonsoft.Json;

namespace Valve.VR
{
    public class SteamVR_Input_ActionManifest_Manager : AssetPostprocessor
    {
        private static bool importing = false;

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importing)
                return;

            importing = true;

            Dictionary<string, List<SteamVR_PartialInputBindings>> partials = ScanForPartials();
            if (partials != null)
            {
                foreach (var element in partials)
                {
                    if (element.Value != null && element.Value.Count > 0 && element.Value[0].imported == false)
                        ConfirmImport(element.Value);
                }
            }

            importing = false;
        }

        public const string partialManifestFilename = "steamvr_partial_manifest.json";
        public static void CreatePartial(string name, int version, bool overwriteOld, bool removeUnused)
        {
            if (SteamVR_Input.actionFile.action_sets.Any(set => set.name == "default"))
            {
                bool confirm = EditorUtility.DisplayDialog("Confirmation", "We don't recommend you create a partial binding manifest with an action set named 'default'. There will often be collisions with existing actions. Are you sure you want to continue creating this partial binding manifest?", "Create", "Cancel");
                if (confirm == false)
                    return;
            }


            string folderName = "SteamVR_" + SteamVR_Input_ActionFile.GetCodeFriendlyName(name);

            string mainFolderPath = string.Format("{0}", folderName);
            string versionFolderPath = string.Format("{0}/{1}", folderName, version.ToString());
            string manifestPath = string.Format("{0}/{1}/{2}", folderName, version.ToString(), partialManifestFilename);

            if (Directory.Exists(mainFolderPath) == false)
            {
                Directory.CreateDirectory(mainFolderPath);
            }

            if (Directory.Exists(versionFolderPath) == false)
            {
                Directory.CreateDirectory(versionFolderPath);
            }


            SteamVR_PartialInputBindings partial = new SteamVR_PartialInputBindings();
            partial.name = name;
            partial.version = version;
            partial.overwriteOld = overwriteOld;
            partial.removeUnused = removeUnused;


            string jsonText = JsonConvert.SerializeObject(partial, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            if (File.Exists(manifestPath))
            {
                FileInfo manifestFile = new FileInfo(manifestPath);
                manifestFile.IsReadOnly = false;
            }

            File.WriteAllText(manifestPath, jsonText);

            SteamVR_Input.actionFile.CopyFilesToPath(versionFolderPath, true);

            EditorUtility.RevealInFinder(mainFolderPath);
        }

        protected static string FindLanguageInDictionary(Dictionary<string, string> dictionary)
        {
            foreach (var localizationMember in dictionary)
            {
                if (localizationMember.Key == SteamVR_Input_ActionFile_LocalizationItem.languageTagKeyName)
                    return localizationMember.Value;
            }

            return null;
        }

        protected static int ImportLocalization(SteamVR_Input_ActionFile currentActionsFile, SteamVR_Input_ActionFile newActionsFile, SteamVR_PartialInputBindings partialBinding)
        {
            int count = 0;

            foreach (var newLocalDictionary in newActionsFile.localization)
            {
                string newLanguage = FindLanguageInDictionary(newLocalDictionary);

                if (string.IsNullOrEmpty(newLanguage))
                {
                    Debug.LogError("<b>[SteamVR Input]</b> Localization entry in partial actions file is missing a language tag: " + partialBinding.path);
                    continue;
                }

                int currentLanguage = -1;
                for (int currentLanguageIndex = 0; currentLanguageIndex < currentActionsFile.localization.Count; currentLanguageIndex++)
                {
                    string language = FindLanguageInDictionary(currentActionsFile.localization[currentLanguageIndex]);
                    if (newLanguage == language)
                    {
                        currentLanguage = currentLanguageIndex;
                        break;
                    }
                }

                if (currentLanguage == -1)
                {
                    Dictionary<string, string> newDictionary = new Dictionary<string, string>();
                    foreach (var element in newLocalDictionary)
                    {
                        newDictionary.Add(element.Key, element.Value);
                        count++;
                    }

                    currentActionsFile.localization.Add(newDictionary);
                }
                else
                {
                    foreach (var element in newLocalDictionary)
                    {
                        Dictionary<string, string> currentDictionary = currentActionsFile.localization[currentLanguage];
                        bool exists = currentDictionary.Any(inCurrent => inCurrent.Key == element.Key);

                        if (exists)
                        {
                            //todo: should we overwrite?
                            currentDictionary[element.Key] = element.Value;
                        }
                        else
                        {
                            currentDictionary.Add(element.Key, element.Value);
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        protected static int ImportActionSets(SteamVR_Input_ActionFile currentActionsFile, SteamVR_Input_ActionFile newActionsFile)
        {
            int count = 0;

            foreach (var newSet in newActionsFile.action_sets)
            {
                if (currentActionsFile.action_sets.Any(setInCurrent => newSet.name == setInCurrent.name) == false)
                {
                    currentActionsFile.action_sets.Add(newSet.GetCopy());
                    count++;
                }
            }

            return count;
        }

        protected static int ImportActions(SteamVR_Input_ActionFile currentActionsFile, SteamVR_Input_ActionFile newActionsFile)
        {
            int count = 0;

            foreach (var newAction in newActionsFile.actions)
            {
                if (currentActionsFile.actions.Any(actionInCurrent => newAction.name == actionInCurrent.name) == false)
                {
                    currentActionsFile.actions.Add(newAction.GetCopy());
                    count++;
                }
                else
                {
                    SteamVR_Input_ActionFile_Action existingAction = currentActionsFile.actions.First(actionInCurrent => newAction.name == actionInCurrent.name);

                    //todo: better merge? should we overwrite?
                    existingAction.type = newAction.type;
                    existingAction.scope = newAction.scope;
                    existingAction.skeleton = newAction.skeleton;
                    existingAction.requirement = newAction.requirement;
                }
            }

            return count;
        }

        protected static SteamVR_Input_BindingFile GetBindingFileObject(string path)
        {
            if (File.Exists(path) == false)
            {
                Debug.LogError("<b>[SteamVR]</b> Could not access file at path: " + path);
                return null;
            }

            string jsonText = File.ReadAllText(path);

            SteamVR_Input_BindingFile importingBindingFile = JsonConvert.DeserializeObject<SteamVR_Input_BindingFile>(jsonText);

            return importingBindingFile;
        }


        protected static void WriteBindingFileObject(SteamVR_Input_BindingFile currentBindingFile, string currentBindingPath)
        {
            if (File.Exists(currentBindingPath))
            {
                FileInfo fileInfo = new FileInfo(currentBindingPath);
                fileInfo.IsReadOnly = false;
            }

            string newJSON = JsonConvert.SerializeObject(currentBindingFile, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            File.WriteAllText(currentBindingPath, newJSON);

            Debug.Log("<b>[SteamVR]</b> Added action bindings to: " + currentBindingPath);
        }

        protected static void ImportBindings(SteamVR_Input_ActionFile currentActionsFile, SteamVR_Input_ActionFile newActionsFile, string directory)
        {
            foreach (var newDefaultPath in newActionsFile.default_bindings)
            {
                if (currentActionsFile.default_bindings.Any(currentDefaultPath => newDefaultPath.controller_type == currentDefaultPath.controller_type) == false)
                {
                    currentActionsFile.default_bindings.Add(newDefaultPath.GetCopy());

                    string bindingPath = Path.Combine(directory, newDefaultPath.binding_url);
                    File.Copy(bindingPath, newDefaultPath.binding_url);
                }
                else
                {
                    string currentBindingPath = currentActionsFile.default_bindings.First(binding => binding.controller_type == newDefaultPath.controller_type).binding_url;

                    SteamVR_Input_BindingFile currentBindingFile = GetBindingFileObject(currentBindingPath);
                    if (currentBindingFile == null)
                    {
                        Debug.LogError("<b>[SteamVR]</b> There was an error deserializing the binding at path: " + currentBindingPath);
                        continue;
                    }
                    
                    SteamVR_Input_BindingFile importingBindingFile = GetBindingFileObject(newDefaultPath.binding_url);
                    if (importingBindingFile == null)
                    {
                        Debug.LogError("<b>[SteamVR]</b> There was an error deserializing the binding at path: " + newDefaultPath.binding_url);
                        continue;
                    }

                    bool changed = false;

                    foreach (var importingActionList in importingBindingFile.bindings)
                    {
                        if (currentBindingFile.bindings.Any(binding => binding.Key == importingActionList.Key))
                        {
                            var currentSetBinding = currentBindingFile.bindings.FirstOrDefault(binding => binding.Key == importingActionList.Key);

                            //todo: better merge? if we don't have an exact copy of the item then we add a new one

                            foreach (var importingChord in importingActionList.Value.chords)
                            {
                                if (currentSetBinding.Value.chords.Any(currentChord => importingChord.Equals(currentChord)) == false)
                                {
                                    changed = true;
                                    currentSetBinding.Value.chords.Add(importingChord);
                                }
                            }

                            foreach (var importingHaptic in importingActionList.Value.haptics)
                            {
                                if (currentSetBinding.Value.haptics.Any(currentHaptic => importingHaptic.Equals(currentHaptic)) == false)
                                {
                                    changed = true;
                                    currentSetBinding.Value.haptics.Add(importingHaptic);
                                }
                            }

                            foreach (var importingPose in importingActionList.Value.poses)
                            {
                                if (currentSetBinding.Value.poses.Any(currentPose => importingPose.Equals(currentPose)) == false)
                                {
                                    changed = true;
                                    currentSetBinding.Value.poses.Add(importingPose);
                                }
                            }

                            foreach (var importingSkeleton in importingActionList.Value.skeleton)
                            {
                                if (currentSetBinding.Value.skeleton.Any(currentSkeleton => importingSkeleton.Equals(currentSkeleton)) == false)
                                {
                                    changed = true;
                                    currentSetBinding.Value.skeleton.Add(importingSkeleton);
                                }
                            }

                            foreach (var importingSource in importingActionList.Value.sources)
                            {
                                if (currentSetBinding.Value.sources.Any(currentSource => importingSource.Equals(currentSource)) == false)
                                {
                                    changed = true;
                                    currentSetBinding.Value.sources.Add(importingSource);
                                }
                            }
                        }
                        else
                        {
                            changed = true;
                            currentBindingFile.bindings.Add(importingActionList.Key, importingActionList.Value);
                        }
                    }

                    if (changed)
                    {
                        WriteBindingFileObject(currentBindingFile, currentBindingPath);
                    }
                }
            }
        }

        public static void CleanBindings(bool verbose = false)
        {
            SteamVR_Input.InitializeFile(true);
            SteamVR_Input_ActionFile currentActionsFile = SteamVR_Input.actionFile;
            
            for (int localizationIndex = 0; localizationIndex < currentActionsFile.localization.Count; localizationIndex++)
            {
                Dictionary<string, string> dictionary = currentActionsFile.localization[localizationIndex];
                bool removed;
                do
                {
                    removed = false;
                    string missingAction = null;
                    foreach (string key in dictionary.Keys)
                    {
                        if (key == SteamVR_Input_ActionFile_LocalizationItem.languageTagKeyName)
                            continue;

                        if (currentActionsFile.actions.Any(action => string.Equals(action.name, key, StringComparison.CurrentCultureIgnoreCase)) == false)
                        {
                            missingAction = key;
                        }
                    }

                    if (missingAction != null)
                    {
                        removed = true;
                        dictionary.Remove(missingAction);
                        if (verbose)
                            Debug.Log("<b>[SteamVR Input]</b> Removing localization entry for: " + missingAction);
                    }
                } while (removed);
            }

            for (int bindingIndex = 0; bindingIndex < currentActionsFile.default_bindings.Count; bindingIndex++)
            {
                SteamVR_Input_ActionFile_DefaultBinding currentBinding = currentActionsFile.default_bindings[bindingIndex];

                if (File.Exists(currentBinding.binding_url) == false)
                {
                    if (verbose)
                        Debug.Log("<b>[SteamVR Input]</b> Removing binding entry for missing file: '" + currentBinding.controller_type + "' at: " + currentBinding.binding_url);

                    currentActionsFile.default_bindings.RemoveAt(bindingIndex);
                    bindingIndex--;
                    continue;
                }

                SteamVR_Input_BindingFile bindingFile = GetBindingFileObject(currentBinding.binding_url);
                if (bindingFile == null)
                {
                    Debug.LogError("<b>[SteamVR Input]</b> Error parsing binding file for: '" + currentBinding.controller_type + "' at: " + currentBinding.binding_url);
                    continue;
                }

                int changed = 0;

                foreach (var actionList in bindingFile.bindings)
                {
                    for (int itemIndex = 0; itemIndex < actionList.Value.chords.Count; itemIndex++)
                    {
                        string outputActionPath = actionList.Value.chords[itemIndex].output;
                        if (currentActionsFile.actions.Any(action => string.Equals(action.name, outputActionPath, StringComparison.CurrentCultureIgnoreCase)) == false)
                        {
                            if (verbose)
                                Debug.Log("<b>[SteamVR Input]</b> " + currentBinding.controller_type + ": Removing chord binding for action: " + outputActionPath);

                            actionList.Value.chords.RemoveAt(itemIndex);
                            itemIndex--;
                            changed++;
                        }
                    }

                    for (int itemIndex = 0; itemIndex < actionList.Value.haptics.Count; itemIndex++)
                    {
                        string outputActionPath = actionList.Value.haptics[itemIndex].output;
                        if (currentActionsFile.actions.Any(action => string.Equals(action.name, outputActionPath, StringComparison.CurrentCultureIgnoreCase)) == false)
                        {
                            if (verbose)
                                Debug.Log("<b>[SteamVR Input]</b> " + currentBinding.controller_type + ": Removing haptics binding for action: " + outputActionPath);

                            actionList.Value.haptics.RemoveAt(itemIndex);
                            itemIndex--;
                            changed++;
                        }
                    }

                    for (int itemIndex = 0; itemIndex < actionList.Value.poses.Count; itemIndex++)
                    {
                        string outputActionPath = actionList.Value.poses[itemIndex].output;
                        if (currentActionsFile.actions.Any(action => string.Equals(action.name, outputActionPath, StringComparison.CurrentCultureIgnoreCase)) == false)
                        {
                            if (verbose)
                                Debug.Log("<b>[SteamVR Input]</b> " + currentBinding.controller_type + ": Removing pose binding for action: " + outputActionPath);

                            actionList.Value.poses.RemoveAt(itemIndex);
                            itemIndex--;
                            changed++;
                        }
                    }

                    for (int itemIndex = 0; itemIndex < actionList.Value.skeleton.Count; itemIndex++)
                    {
                        string outputActionPath = actionList.Value.skeleton[itemIndex].output;
                        if (currentActionsFile.actions.Any(action => string.Equals(action.name, outputActionPath, StringComparison.CurrentCultureIgnoreCase)) == false)
                        {
                            if (verbose)
                                Debug.Log("<b>[SteamVR Input]</b> " + currentBinding.controller_type + ": Removing skeleton binding for action: " + outputActionPath);

                            actionList.Value.skeleton.RemoveAt(itemIndex);
                            itemIndex--;
                            changed++;
                        }
                    }

                    for (int itemIndex = 0; itemIndex < actionList.Value.sources.Count; itemIndex++)
                    {
                        string outputActionPath = actionList.Value.sources[itemIndex].GetOutput();
                        if (currentActionsFile.actions.Any(action => string.Equals(action.name, outputActionPath, StringComparison.CurrentCultureIgnoreCase)) == false)
                        {
                            if (verbose)
                                Debug.Log("<b>[SteamVR Input]</b> " + currentBinding.controller_type + ": Removing source binding for action: " + outputActionPath);

                            actionList.Value.sources.RemoveAt(itemIndex);
                            itemIndex--;
                            changed++;
                        }
                    }
                }

                if (changed > 0)
                {
                    WriteBindingFileObject(bindingFile, currentBinding.binding_url);
                }
            }

            if (SteamVR_Input.HasFileInMemoryBeenModified())
            {
                SteamVR_Input.actionFile.Save(SteamVR_Input.actionsFilePath);

                if (verbose)
                    Debug.Log("<b>[SteamVR Input]</b> Saved new actions file: " + SteamVR_Input.actionsFilePath);
            }
        }


        protected static void ImportPartialBinding(SteamVR_PartialInputBindings partialBinding)
        {
            SteamVR_Input.InitializeFile();
            SteamVR_Input_ActionFile currentActionsFile = SteamVR_Input.actionFile;

            SteamVR_Input_ActionFile newActionsFile = ReadJson<SteamVR_Input_ActionFile>(partialBinding.GetActionsPath());

            /*
            int sets = ImportActionSets(currentActionsFile, newActionsFile);
            int locs = ImportLocalization(currentActionsFile, newActionsFile, partialBinding);
            int actions = ImportActions(currentActionsFile, newActionsFile);
            */

            ImportActionSets(currentActionsFile, newActionsFile);
            ImportLocalization(currentActionsFile, newActionsFile, partialBinding);
            ImportActions(currentActionsFile, newActionsFile);

            if (SteamVR_Input.HasFileInMemoryBeenModified())
            {
                SteamVR_Input.actionFile.Save(SteamVR_Input.actionsFilePath);

                Debug.Log("<b>[SteamVR]</b> Saved new actions file: " + SteamVR_Input.actionsFilePath);
            }

            ImportBindings(currentActionsFile, newActionsFile, partialBinding.GetDirectory());

            partialBinding.imported = true;
            partialBinding.Save();

            SteamVR_Input.InitializeFile(true);
            SteamVR_Input_EditorWindow.ReopenWindow();

            //todo: ask first?
            /*string dialogText = string.Format("{0} new action sets, {1} new actions, and {2} new localization strings have been added. Would you like to regenerate SteamVR Input code files?", sets, actions, locs);

            bool confirm = EditorUtility.DisplayDialog("SteamVR Input", dialogText, "Generate", "Cancel");
            if (confirm)
                SteamVR_Input_Generator.BeginGeneration();
            */

            SteamVR_Input_Generator.BeginGeneration();

            Debug.Log("<b>[SteamVR]</b> Reloaded actions file with additional actions from " + partialBinding.name);
        }

        protected static void ReplaceBinding(SteamVR_PartialInputBindings partialBinding)
        {
            SteamVR_Input.DeleteManifestAndBindings();

            string newActionsFilePath = partialBinding.GetActionsPath();
            if (File.Exists(newActionsFilePath))
            {
                File.Copy(newActionsFilePath, SteamVR_Input.actionsFilePath);
            }

            SteamVR_Input_ActionFile newActionsFile = ReadJson<SteamVR_Input_ActionFile>(SteamVR_Input.actionsFilePath);
            string partialBindingDirectory = partialBinding.GetDirectory();

            foreach (var newDefaultPath in newActionsFile.default_bindings)
            {
                string bindingPath = Path.Combine(partialBindingDirectory, newDefaultPath.binding_url);
                File.Copy(bindingPath, newDefaultPath.binding_url);
            }

            partialBinding.imported = true;
            partialBinding.Save();

            SteamVR_Input.InitializeFile(true);
            SteamVR_Input_EditorWindow.ReopenWindow();

            //todo: ask first?
            /*string dialogText = string.Format("{0} new action sets, {1} new actions, and {2} new localization strings have been added. Would you like to regenerate SteamVR Input code files?", sets, actions, locs);

            bool confirm = EditorUtility.DisplayDialog("SteamVR Input", dialogText, "Generate", "Cancel");
            if (confirm)
                SteamVR_Input_Generator.BeginGeneration();
            */

            SteamVR_Input_Generator.BeginGeneration();

            Debug.Log("<b>[SteamVR Input]</b> Reloaded with new actions from " + partialBinding.name);
        }

        protected static T ReadJson<T>(string path)
        {
            if (File.Exists(path))
            {
                string jsonText = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<T>(jsonText);
            }

            return default(T);
        }

        protected static List<SteamVR_Input_ActionFile_Action> RemoveOldActions(List<SteamVR_PartialInputBindings> partialBindingList)
        {
            List<SteamVR_Input_ActionFile_Action> toRemove = new List<SteamVR_Input_ActionFile_Action>();

            SteamVR_Input_ActionFile newestActionsFile = ReadJson<SteamVR_Input_ActionFile>(partialBindingList[0].GetActionsPath());

            for (int partialBindingIndex = 1; partialBindingIndex < partialBindingList.Count; partialBindingIndex++)
            {
                SteamVR_Input_ActionFile oldActionsFile = ReadJson<SteamVR_Input_ActionFile>(partialBindingList[partialBindingIndex].GetActionsPath());

                for (int oldActionIndex = 0; oldActionIndex < oldActionsFile.actions.Count; oldActionIndex++)
                {
                    var oldAction = oldActionsFile.actions[oldActionIndex];

                    if (newestActionsFile.actions.Any(newAction => oldAction.Equals(newAction)) == false)
                    {
                        var existing = SteamVR_Input.actionFile.actions.FirstOrDefault(action => oldAction.Equals(action));
                        if (existing != null)
                        {
                            SteamVR_Input.actionFile.actions.Remove(existing);
                            toRemove.Add(oldAction);
                        }
                    }
                }
            }

            return toRemove;
        }

        protected static List<SteamVR_Input_ActionFile_ActionSet> RemoveOldActionSets(List<SteamVR_PartialInputBindings> partialBindingList)
        {
            List<SteamVR_Input_ActionFile_ActionSet> toRemove = new List<SteamVR_Input_ActionFile_ActionSet>();

            SteamVR_Input_ActionFile newestActionsFile = ReadJson<SteamVR_Input_ActionFile>(partialBindingList[0].GetActionsPath());

            for (int partialBindingIndex = 1; partialBindingIndex < partialBindingList.Count; partialBindingIndex++)
            {
                SteamVR_Input_ActionFile oldActionsFile = ReadJson<SteamVR_Input_ActionFile>(partialBindingList[0].GetActionsPath());

                for (int oldActionIndex = 0; oldActionIndex < oldActionsFile.action_sets.Count; oldActionIndex++)
                {
                    var oldActionSet = oldActionsFile.action_sets[oldActionIndex];

                    if (newestActionsFile.action_sets.Any(newAction => oldActionSet.Equals(newAction)) == false)
                    {
                        var existing = SteamVR_Input.actionFile.action_sets.FirstOrDefault(actionSet => oldActionSet.Equals(actionSet));
                        if (existing != null)
                        {
                            SteamVR_Input.actionFile.action_sets.Remove(existing);
                            toRemove.Add(oldActionSet);
                        }
                    }
                }
            }

            return toRemove;
        }

        protected static int RemoveOldLocalizations(List<SteamVR_Input_ActionFile_Action> removedActionList)
        {
            int count = 0;

            foreach (var action in removedActionList)
            {
                foreach (var locDictionary in SteamVR_Input.actionFile.localization)
                {
                    bool removed = locDictionary.Remove(action.name);
                    if (removed)
                        count++;
                }
            }

            return count;
        }

        protected static void RemoveOldActionsAndSetsFromBindings(List<SteamVR_Input_ActionFile_ActionSet> setsToRemove, List<SteamVR_Input_ActionFile_Action> actionsToRemove)
        {
            foreach (var defaultBindingItem in SteamVR_Input.actionFile.default_bindings)
            {
                string currentBindingPath = defaultBindingItem.binding_url;

                SteamVR_Input_BindingFile currentBindingFile = GetBindingFileObject(currentBindingPath);
                if (currentBindingFile == null)
                {
                    Debug.LogError("<b>[SteamVR]</b> There was an error deserializing the binding at path: " + currentBindingPath);
                    continue;
                }

                bool changed = false;

                List<string> bindingListsToRemove = new List<string>();
                foreach (var actionList in currentBindingFile.bindings)
                {
                    if (setsToRemove.Any(set => set.name == actionList.Key))
                    {
                        bindingListsToRemove.Add(actionList.Key);
                        changed = true;
                        continue;
                    }

                    for (int chordIndex = 0; chordIndex < actionList.Value.chords.Count; chordIndex++)
                    {
                        var existingChord = actionList.Value.chords[chordIndex];
                        if (actionsToRemove.Any(action => action.name == existingChord.output))
                        {
                            actionList.Value.chords.Remove(existingChord);
                            chordIndex--;
                            changed = true;
                        }
                    }

                    for (int hapticIndex = 0; hapticIndex < actionList.Value.haptics.Count; hapticIndex++)
                    {
                        var existingHaptic = actionList.Value.haptics[hapticIndex];
                        if (actionsToRemove.Any(action => action.name == existingHaptic.output))
                        {
                            actionList.Value.haptics.Remove(existingHaptic);
                            hapticIndex--;
                            changed = true;
                        }
                    }

                    for (int poseIndex = 0; poseIndex < actionList.Value.poses.Count; poseIndex++)
                    {
                        var existingPose = actionList.Value.poses[poseIndex];
                        if (actionsToRemove.Any(action => action.name == existingPose.output))
                        {
                            actionList.Value.poses.Remove(existingPose);
                            poseIndex--;
                            changed = true;
                        }
                    }

                    for (int skeletonIndex = 0; skeletonIndex < actionList.Value.skeleton.Count; skeletonIndex++)
                    {
                        var existingSkeleton = actionList.Value.skeleton[skeletonIndex];
                        if (actionsToRemove.Any(action => action.name == existingSkeleton.output))
                        {
                            actionList.Value.skeleton.Remove(existingSkeleton);
                            skeletonIndex--;
                            changed = true;
                        }
                    }

                    for (int sourceIndex = 0; sourceIndex < actionList.Value.sources.Count; sourceIndex++)
                    {
                        var existingSource = actionList.Value.sources[sourceIndex];
                        if (actionsToRemove.Any(action => action.name == existingSource.GetOutput()))
                        {
                            actionList.Value.sources.Remove(existingSource);
                            sourceIndex--;
                            changed = true;
                        }
                    }
                }

                for (int bindingListToRemoveIndex = 0; bindingListToRemoveIndex < bindingListsToRemove.Count; bindingListToRemoveIndex++)
                {
                    currentBindingFile.bindings.Remove(bindingListsToRemove[bindingListToRemoveIndex]);
                }

                if (changed)
                {
                    WriteBindingFileObject(currentBindingFile, currentBindingPath);
                }
            }
        }

        protected static void RemoveOldPartialBindings(List<SteamVR_PartialInputBindings> partialBindingList)
        {
            List<SteamVR_Input_ActionFile_Action> actionsToRemove = RemoveOldActions(partialBindingList);
            List<SteamVR_Input_ActionFile_ActionSet> setsToRemove = RemoveOldActionSets(partialBindingList);

            int sets = setsToRemove.Count;
            int actions = actionsToRemove.Count;
            int locs = RemoveOldLocalizations(actionsToRemove);

            string dialogText = string.Format("We've found a old {0} action sets, {1} actions, and {2} localization entries from old versions of this partial binding. Would you like to remove them from the actions file and default bindings?", sets, actions, locs);

            bool confirm = EditorUtility.DisplayDialog("SteamVR Input", dialogText, "Import", "Cancel");
            if (confirm)
            {
                RemoveOldActionsAndSetsFromBindings(setsToRemove, actionsToRemove);

                SteamVR_Input.actionFile.Save(SteamVR_Input.actionsFilePath);

                SteamVR_Input.InitializeFile(true); // reload after the save
            }
            else
            {
                SteamVR_Input.InitializeFile(true); // reload since we actually removed the actions / sets to display this message
            }
        }

        protected const string dontAskAgainTemplate = "{0}_{1}_DontAskAgain";
        protected static void ConfirmImport(List<SteamVR_PartialInputBindings> partialBindingList)
        {
            SteamVR_PartialInputBindings partial = partialBindingList.First();

            //bool dontAskAgain = EditorPrefs.GetBool(dontAskAgainTemplate, false);

            //todo: implement 'do not ask again'
            string dialogText = string.Format("We've found a partial SteamVR Input binding for '{0}' version '{1}'. Would you like to import it?", partial.name, partial.version);

            bool confirm = EditorUtility.DisplayDialog("SteamVR Input", dialogText, "Import", "Cancel");
            if (confirm)
            {
                bool actionsExists = SteamVR_Input.DoesActionsFileExist();

                if (actionsExists)
                {
                    string mergeDialogText = "You have two options for importing this binding:\n Replace your current action file (delete all your actions)\n Merge the partial action file with your existing actions";
                    bool shouldMerge = EditorUtility.DisplayDialog("SteamVR Input", mergeDialogText, "Merge", "Replace");

                    if (shouldMerge)
                    {
                        ImportPartialBinding(partial);
                    }
                    else
                    {
                        ReplaceBinding(partial);
                    }
                }
                else
                {
                    ReplaceBinding(partial);
                }

                if (partialBindingList.Count > 1)
                {
                    RemoveOldPartialBindings(partialBindingList);
                }
            }
        }

        public static Dictionary<string, List<SteamVR_PartialInputBindings>> ScanForPartials()
        {
            string[] partialManifestPaths = Directory.GetFiles("Assets/", partialManifestFilename, SearchOption.AllDirectories);
            Dictionary<string, List<SteamVR_PartialInputBindings>> partialBindings = new Dictionary<string, List<SteamVR_PartialInputBindings>>();

            for (int partialIndex = 0; partialIndex < partialManifestPaths.Length; partialIndex++)
            {
                string path = partialManifestPaths[partialIndex];
                string jsonText = File.ReadAllText(path);

                SteamVR_PartialInputBindings partialBinding = JsonConvert.DeserializeObject<SteamVR_PartialInputBindings>(jsonText);
                partialBinding.path = path;

                if (partialBindings.ContainsKey(partialBinding.name))
                {
                    for (int versionIndex = 0; versionIndex < partialBindings[partialBinding.name].Count; versionIndex++)
                    {
                        if (partialBinding.version < partialBindings[partialBinding.name][versionIndex].version)
                            partialBindings[partialBinding.name].Insert(versionIndex, partialBinding);
                    }
                }
                else
                {
                    partialBindings.Add(partialBinding.name, new List<SteamVR_PartialInputBindings>() { partialBinding });
                }
            }

            return partialBindings;
        }
    }

    public class SteamVR_PartialInputBindings
    {
        public string name;
        public int version;
        public bool overwriteOld;
        public bool removeUnused;
        public bool imported;

        [JsonIgnore]
        public string path { get; set; }

        public string GetActionsPath()
        {
            return Path.Combine(GetDirectory(), "actions.json");
        }

        public string GetDirectory()
        {
            return new FileInfo(path).Directory.FullName;
        }

        public void Save()
        {
            FileInfo existingActionsFile = new FileInfo(path);
            if (existingActionsFile.Exists)
            {
                existingActionsFile.IsReadOnly = false;
            }

            //SanitizeActionFile(); //todo: shouldn't we be doing this?

            string json = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            File.WriteAllText(path, json);
        }
    }
}
