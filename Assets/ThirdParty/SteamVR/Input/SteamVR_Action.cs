//======= Copyright (c) Valve Corporation, All rights reserved. ===============
// Action implementation overview:
// Actions are split into three parts:
//     * Action: The user-accessible class that is the interface for accessing action data. 
//          There may be many Action instances per Actual SteamVR Action, but these instances are just interfaces to the data and should have virtually no overhead.
//     * Action Map: This is basically a wrapper for a list of Action_Source instances. 
//          The idea being there is one Map per Actual SteamVR Action. 
//          These maps can be retrieved from a static store in SteamVR_Input so we're not duplicating data.
//     * Action Source: This is a collection of cached data retrieved by calls to the underlying SteamVR Input system. 
//          Each Action Source has an inputSource that it is associated with.

using UnityEngine;
using System.Collections;
using System;
using Valve.VR;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Valve.VR
{
    [Serializable]
    /// <summary>
    /// This is the base level action for SteamVR Input Actions. All SteamVR_Action_In and SteamVR_Action_Out inherit from this.
    /// Initializes the ulong handle for the action, has some helper references that all actions will have.
    /// </summary>
    public abstract class SteamVR_Action<SourceMap, SourceElement> : SteamVR_Action, ISteamVR_Action where SourceMap : SteamVR_Action_Source_Map<SourceElement>, new() where SourceElement : SteamVR_Action_Source, new()
    {
        /// <summary>
        /// The map to the source elements, a dictionary of source elements. Should be accessed through the action indexer
        /// </summary>
        [NonSerialized]
        protected SourceMap sourceMap;

        /// <summary>
        /// Access this action restricted to individual input sources.
        /// </summary>
        /// <param name="inputSource">The input source to access for this action</param>
        public virtual SourceElement this[SteamVR_Input_Sources inputSource]
        {
            get
            {
                return sourceMap[inputSource];
            }
        }

        /// <summary>The full string path for this action</summary>
        public override string fullPath
        {
            get
            {
                return sourceMap.fullPath;
            }
        }

        /// <summary>The underlying handle for this action used for native SteamVR Input calls</summary>
        public override ulong handle { get { return sourceMap.handle; } }

        /// <summary>The actionset this action is contained within</summary>
        public override SteamVR_ActionSet actionSet
        {
            get
            {
                return sourceMap.actionSet;
            }
        }

        /// <summary>The action direction of this action (in for input - most actions, out for output - mainly haptics)</summary>
        public override SteamVR_ActionDirections direction
        {
            get
            {
                return sourceMap.direction;
            }
        }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action is bound and the actionset is active</summary>
        public override bool active { get { return sourceMap[SteamVR_Input_Sources.Any].active; } }
        
        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action was bound and the ActionSet was active during the previous update</summary>
        public override bool lastActive { get { return sourceMap[SteamVR_Input_Sources.Any].lastActive; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action is bound</summary>
        public override bool activeBinding { get { return sourceMap[SteamVR_Input_Sources.Any].activeBinding; } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action was bound at the previous update</summary>
        public override bool lastActiveBinding { get { return sourceMap[SteamVR_Input_Sources.Any].lastActiveBinding; } }


        [NonSerialized]
        protected bool initialized = false;

        /// <summary>
        /// Prepares the action to be initialized. Creating dictionaries, finding the right existing action, etc.
        /// </summary>
        public override void PreInitialize(string newActionPath)
        {
            actionPath = newActionPath;

            sourceMap = new SourceMap();
            sourceMap.PreInitialize(this, actionPath);

            initialized = true;
        }

        protected override void CreateUninitialized(string newActionPath, bool caseSensitive)
        {
            actionPath = newActionPath;

            sourceMap = new SourceMap();
            sourceMap.PreInitialize(this, actionPath, false);

            needsReinit = true;
            initialized = false;
        }

        protected override void CreateUninitialized(string newActionSet, SteamVR_ActionDirections direction, string newAction, bool caseSensitive)
        {
            actionPath = SteamVR_Input_ActionFile_Action.CreateNewName(newActionSet, direction, newAction);

            sourceMap = new SourceMap();
            sourceMap.PreInitialize(this, actionPath, false);

            needsReinit = true;
            initialized = false;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> If it looks like we aren't attached to a real action then try and find the existing action for our given path.
        /// </summary>
        public override string TryNeedsInitData()
        {
            if (needsReinit && actionPath != null)
            {
                SteamVR_Action existingAction = FindExistingActionForPartialPath(actionPath);

                if (existingAction == null)
                {
                    this.sourceMap = null;
                }
                else
                { 
                    this.actionPath = existingAction.fullPath;
                    this.sourceMap = (SourceMap)existingAction.GetSourceMap();

                    initialized = true;
                    needsReinit = false;
                    return actionPath;
                }
            }

            return null;
        }


        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Initializes the individual sources as well as the base map itself. 
        /// Gets the handle for the action from SteamVR and does any other SteamVR related setup that needs to be done
        /// </summary>
        public override void Initialize(bool createNew = false, bool throwErrors = true)
        {
            if (needsReinit)
            {
                TryNeedsInitData();
            }

            if (createNew)
            {
                sourceMap.Initialize();
            }
            else
            {
                sourceMap = SteamVR_Input.GetActionDataFromPath<SourceMap>(actionPath);

                if (sourceMap == null)
                {
#if UNITY_EDITOR
                    if (throwErrors)
                    {
                        if (string.IsNullOrEmpty(actionPath))
                        {
                            Debug.LogError("<b>[SteamVR]</b> Action has not been assigned.");
                        }
                        else
                        {
                            Debug.LogError("<b>[SteamVR]</b> Could not find action with path: " + actionPath);
                        }
                    }
#endif
                }
            }

            initialized = true;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Returns the underlying source map for the action.
        /// <strong>[Should not be called by user code]</strong> Returns the underlying source map for the action.
        /// </summary>
        public override SteamVR_Action_Source_Map GetSourceMap()
        {
            return sourceMap;
        }

        protected override void InitializeCopy(string newActionPath, SteamVR_Action_Source_Map newData)
        {
            this.actionPath = newActionPath;
            this.sourceMap = (SourceMap)newData;

            initialized = true;
        }

        protected void InitAfterDeserialize()
        {
            if (sourceMap != null)
            {
                if (sourceMap.fullPath != actionPath)
                {
                    needsReinit = true;
                    TryNeedsInitData();
                }

                if (string.IsNullOrEmpty(actionPath))
                    sourceMap = null;
            }

            if (initialized == false)
            {
                Initialize(false, false);
            }
        }


        /// <summary>
        /// Gets a value indicating whether or not the action is currently bound and if the containing action set is active
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public override bool GetActive(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].active;
        }

        /// <summary>
        /// Gets a value indicating whether or not the action is currently bound
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public override bool GetActiveBinding(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].activeBinding;
        }


        /// <summary>
        /// Gets the value from the previous update indicating whether or not the action was currently bound and if the containing action set was active
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public override bool GetLastActive(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastActive;
        }

        /// <summary>
        /// Gets the value from the previous update indicating whether or not the action is currently bound
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public override bool GetLastActiveBinding(SteamVR_Input_Sources inputSource)
        {
            return sourceMap[inputSource].lastActiveBinding;
        }
    }


    [Serializable]
    public abstract class SteamVR_Action : IEquatable<SteamVR_Action>, ISteamVR_Action
    {
        public SteamVR_Action() { }

        [SerializeField]
        protected string actionPath;

        [SerializeField]
        protected bool needsReinit;

        /// <summary>
        /// <strong>Not recommended.</strong> Determines if we should do a lazy-loading style of updating actions where we don't check for their data until the code asks for it. Note: You will have to manually activate actions otherwise. Not recommended.
        /// </summary>
        public static bool startUpdatingSourceOnAccess = true;

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Creates an actual action that will later be called by user code.
        /// </summary>
        public static CreateType Create<CreateType>(string newActionPath) where CreateType : SteamVR_Action, new()
        {
            CreateType action = new CreateType();
            action.PreInitialize(newActionPath);
            return action;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Creates an uninitialized action that can be saved without being attached to a real action
        /// </summary>
        public static CreateType CreateUninitialized<CreateType>(string setName, SteamVR_ActionDirections direction, string newActionName, bool caseSensitive) where CreateType : SteamVR_Action, new()
        {
            CreateType action = new CreateType();
            action.CreateUninitialized(setName, direction, newActionName, caseSensitive);
            return action;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Creates an uninitialized action that can be saved without being attached to a real action
        /// </summary>
        public static CreateType CreateUninitialized<CreateType>(string actionPath, bool caseSensitive) where CreateType : SteamVR_Action, new()
        {
            CreateType action = new CreateType();
            action.CreateUninitialized(actionPath, caseSensitive);
            return action;
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Gets a copy of the underlying source map so we're always using the same underlying event data
        /// </summary>
        public CreateType GetCopy<CreateType>() where CreateType : SteamVR_Action, new()
        {
            if (SteamVR_Input.ShouldMakeCopy()) //no need to make copies at runtime
            {
                CreateType action = new CreateType();
                action.InitializeCopy(this.actionPath, this.GetSourceMap());
                return action;
            }
            else
            {
                return (CreateType)this;
            }
        }

        public abstract string TryNeedsInitData();

        protected abstract void InitializeCopy(string newActionPath, SteamVR_Action_Source_Map newData);

        /// <summary>The full string path for this action</summary>
        public abstract string fullPath { get; }

        /// <summary>The underlying handle for this action used for native SteamVR Input calls</summary>
        public abstract ulong handle { get; }

        /// <summary>The actionset this action is contained within</summary>
        public abstract SteamVR_ActionSet actionSet { get; }

        /// <summary>The action direction of this action (in for input - most actions, out for output - mainly haptics)</summary>
        public abstract SteamVR_ActionDirections direction { get; }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action set that contains this action is active for Any input source.</summary>
        public bool setActive { get { return actionSet.IsActive(SteamVR_Input_Sources.Any); } }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action is bound and the actionset is active</summary>
        public abstract bool active { get; }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action is bound</summary>
        public abstract bool activeBinding { get; }

        /// <summary><strong>[Shortcut to: SteamVR_Input_Sources.Any]</strong> Returns true if the action was bound and the actionset was active at the previous update</summary>
        public abstract bool lastActive { get; }

        /// <summary>
        /// 
        /// </summary>
        public abstract bool lastActiveBinding { get; }
        
        /// <summary>
        /// Prepares the action to be initialized. Creating dictionaries, finding the right existing action, etc.
        /// </summary>
        public abstract void PreInitialize(string newActionPath);

        protected abstract void CreateUninitialized(string newActionPath, bool caseSensitive);
        
        protected abstract void CreateUninitialized(string newActionSet, SteamVR_ActionDirections direction, string newAction, bool caseSensitive);

        /// <summary>
        /// Initializes the individual sources as well as the base map itself. Gets the handle for the action from SteamVR and does any other SteamVR related setup that needs to be done
        /// </summary>
        public abstract void Initialize(bool createNew = false, bool throwNotSetError = true);

        /// <summary>Gets the last timestamp this action was changed. (by Time.realtimeSinceStartup)</summary>
        /// <param name="inputSource">The input source to use to select the last changed time</param>
        public abstract float GetTimeLastChanged(SteamVR_Input_Sources inputSource);

        public abstract SteamVR_Action_Source_Map GetSourceMap();


        /// <summary>
        /// Gets a value indicating whether or not the action is currently bound and if the containing action set is active
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public abstract bool GetActive(SteamVR_Input_Sources inputSource);


        /// <summary>
        /// Gets a value indicating whether or not the containing action set is active
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public bool GetSetActive(SteamVR_Input_Sources inputSource)
        {
            return actionSet.IsActive(inputSource);
        }

        /// <summary>
        /// Gets a value indicating whether or not the action is currently bound
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public abstract bool GetActiveBinding(SteamVR_Input_Sources inputSource);


        /// <summary>
        /// Gets the value from the previous update indicating whether or not the action is currently bound and if the containing action set is active
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public abstract bool GetLastActive(SteamVR_Input_Sources inputSource);

        /// <summary>
        /// Gets the value from the previous update indicating whether or not the action is currently bound
        /// </summary>
        /// <param name="inputSource">The device you would like to get data from. Any if the action is not device specific.</param>
        public abstract bool GetLastActiveBinding(SteamVR_Input_Sources inputSource);

        /// <summary>Returns the full action path for this action.</summary>
        public string GetPath()
        {
            return actionPath;
        }

        /// <summary>
        /// Returns true if the data for this action is being updated for the specified input source. This can be triggered by querying the data
        /// </summary>
        public abstract bool IsUpdating(SteamVR_Input_Sources inputSource);


        /// <summary>
        /// Creates a hashcode from the full action path of this action
        /// </summary>
        public override int GetHashCode()
        {
            if (actionPath == null)
                return 0;
            else
                return actionPath.GetHashCode();
        }

        /// <summary>
        /// Compares two SteamVR_Actions by their action path instead of references
        /// </summary>
        public bool Equals(SteamVR_Action other)
        {
            if (ReferenceEquals(null, other))
                return false;

            //SteamVR_Action_Source_Map thisMap = this.GetSourceMap();
            //SteamVR_Action_Source_Map otherMap = other.GetSourceMap();

            //return this.actionPath == other.actionPath && thisMap.fullPath == otherMap.fullPath;
            return this.actionPath == other.actionPath;
        }

        /// <summary>
        /// Compares two SteamVR_Actions by their action path instead of references
        /// </summary>
        public override bool Equals(object other)
        {
            if (ReferenceEquals(null, other))
            {
                if (string.IsNullOrEmpty(this.actionPath)) //if we haven't set a path, say this action is equal to null
                    return true;
                if (this.GetSourceMap() == null)
                    return true;
                
                return false;
            }

            if (ReferenceEquals(this, other))
                return true;

            if (other is SteamVR_Action)
                return this.Equals((SteamVR_Action)other);

            return false;
        }

        /// <summary>
        /// Compares two SteamVR_Actions by their action path.
        /// </summary>
        public static bool operator !=(SteamVR_Action action1, SteamVR_Action action2)
        {
            return !(action1 == action2);
        }

        /// <summary>
        /// Compares two SteamVR_Actions by their action path.
        /// </summary>
        public static bool operator ==(SteamVR_Action action1, SteamVR_Action action2)
        {
            bool action1null = (ReferenceEquals(null, action1) || string.IsNullOrEmpty(action1.actionPath) || action1.GetSourceMap() == null);
            bool action2null = (ReferenceEquals(null, action2) || string.IsNullOrEmpty(action2.actionPath) || action2.GetSourceMap() == null);

            if (action1null && action2null)
                return true;
            else if (action1null != action2null)
                return false;

            return action1.Equals(action2);
        }

        /// <summary>
        /// Tries to find an existing action matching some subsection of an action path. More useful functions in SteamVR_Input.
        /// </summary>
        public static SteamVR_Action FindExistingActionForPartialPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.IndexOf('/') == -1)
                return null;

            //   0    1       2     3   4
            //    /actions/default/in/foobar
            string[] pathParts = path.Split('/');
            SteamVR_Action existingAction;

            if (pathParts.Length >= 2 && string.IsNullOrEmpty(pathParts[2]))
            {
                string set = pathParts[2];
                string name = pathParts[4];
                existingAction = SteamVR_Input.GetBaseAction(set, name);
            }
            else
            {
                existingAction = SteamVR_Input.GetBaseActionFromPath(path);
            }

            return existingAction;
        }


        [NonSerialized]
        private string cachedShortName;

        /// <summary>Gets just the name of this action. The last part of the path for this action. Removes action set, and direction.</summary>
        public string GetShortName()
        {
            if (cachedShortName == null)
            {
                cachedShortName = SteamVR_Input_ActionFile.GetShortName(fullPath);
            }

            return cachedShortName;
        }

        public void ShowOrigins()
        {
            OpenVR.Input.ShowActionOrigins(actionSet.handle, handle);
        }

        public void HideOrigins()
        {
            OpenVR.Input.ShowActionOrigins(0,0);
        }
    }

    public abstract class SteamVR_Action_Source_Map<SourceElement> : SteamVR_Action_Source_Map where SourceElement : SteamVR_Action_Source, new()
    {
        /// <summary>
        /// Gets a reference to the action restricted to a certain input source. LeftHand or RightHand for example.
        /// </summary>
        /// <param name="inputSource">The device you would like data from</param>
        public SourceElement this[SteamVR_Input_Sources inputSource]
        {
            get
            {
                return GetSourceElementForIndexer(inputSource);
            }
        }

        protected virtual void OnAccessSource(SteamVR_Input_Sources inputSource) { }

        protected Dictionary<SteamVR_Input_Sources, SourceElement> sources = new Dictionary<SteamVR_Input_Sources, SourceElement>(new SteamVR_Input_Sources_Comparer());

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Initializes the individual sources as well as the base map itself. Gets the handle for the action from SteamVR and does any other SteamVR related setup that needs to be done
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            var sourceEnumerator = sources.GetEnumerator();
            while (sourceEnumerator.MoveNext())
            {
                var sourceElement = sourceEnumerator.Current;
                sourceElement.Value.Initialize();
            }
        }

        protected override void PreinitializeMap(SteamVR_Input_Sources inputSource, SteamVR_Action wrappingAction)
        {
            sources.Add(inputSource, new SourceElement());
            sources[inputSource].Preinitialize(wrappingAction, inputSource);
        }

        // Normally I'd just make the indexer virtual and override that but some unity versions don't like that
        protected virtual SourceElement GetSourceElementForIndexer(SteamVR_Input_Sources inputSource)
        {
            OnAccessSource(inputSource);
            return sources[inputSource];
        }
    }

    public abstract class SteamVR_Action_Source_Map
    {
        /// <summary>The full string path for this action (from the action manifest)</summary>
        public string fullPath { get; protected set; }

        /// <summary>The underlying handle for this action used for native SteamVR Input calls. Retrieved on Initialization from SteamVR.</summary>
        public ulong handle { get; protected set; }

        /// <summary>The ActionSet this action is contained within</summary>
        public SteamVR_ActionSet actionSet { get; protected set; }

        /// <summary>The action direction of this action (in for input - most actions, out for output - haptics)</summary>
        public SteamVR_ActionDirections direction { get; protected set; }

        /// <summary>The base SteamVR_Action this map corresponds to</summary>
        public SteamVR_Action action;

        public virtual void PreInitialize(SteamVR_Action wrappingAction, string actionPath, bool throwErrors = true)
        {
            fullPath = actionPath;
            action = wrappingAction;

            actionSet = SteamVR_Input.GetActionSetFromPath(GetActionSetPath());

            direction = GetActionDirection();

            SteamVR_Input_Sources[] sources = SteamVR_Input_Source.GetAllSources();
            for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                PreinitializeMap(sources[sourceIndex], wrappingAction);
            }
        }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Sets up the internals of the action source before SteamVR has been initialized.
        /// </summary>
        protected abstract void PreinitializeMap(SteamVR_Input_Sources inputSource, SteamVR_Action wrappingAction);

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Initializes the handle for the action and any other related SteamVR data.
        /// </summary>
        public virtual void Initialize()
        {
            ulong newHandle = 0;
            EVRInputError err = OpenVR.Input.GetActionHandle(fullPath.ToLower(), ref newHandle);
            handle = newHandle;

            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetActionHandle (" + fullPath.ToLower() + ") error: " + err.ToString());
        }

        private string GetActionSetPath()
        {
            int actionsEndIndex = fullPath.IndexOf('/', 1);
            int setStartIndex = actionsEndIndex + 1;
            int setEndIndex = fullPath.IndexOf('/', setStartIndex);
            int count = setEndIndex;

            return fullPath.Substring(0, count);
        }

        private SteamVR_ActionDirections GetActionDirection()
        {
            int actionsEndIndex = fullPath.IndexOf('/', 1);
            int setStartIndex = actionsEndIndex + 1;
            int setEndIndex = fullPath.IndexOf('/', setStartIndex);
            int directionEndIndex = fullPath.IndexOf('/', setEndIndex + 1);
            int count = directionEndIndex - setEndIndex - 1;
            string direction = fullPath.Substring(setEndIndex + 1, count);

            if (direction == "in")
                return SteamVR_ActionDirections.In;
            else if (direction == "out")
                return SteamVR_ActionDirections.Out;
            else
                Debug.LogError("Could not find match for direction: " + direction);
            return SteamVR_ActionDirections.In;
        }
    }

    public abstract class SteamVR_Action_Source : ISteamVR_Action_Source
    {
        /// <summary>The full string path for this action (from the action manifest)</summary>
        public string fullPath { get { return action.fullPath; } }

        /// <summary>The underlying handle for this action used for native SteamVR Input calls. Retrieved on Initialization from SteamVR.</summary>
        public ulong handle { get { return action.handle; } }

        /// <summary>The ActionSet this action is contained within</summary>
        public SteamVR_ActionSet actionSet { get { return action.actionSet; } }
        
        /// <summary>The action direction of this action (in for input - most actions, out for output - haptics)</summary>
        public SteamVR_ActionDirections direction { get { return action.direction; } }
        
        /// <summary>The input source that this instance corresponds to. ex. LeftHand, RightHand</summary>
        public SteamVR_Input_Sources inputSource { get; protected set; }
        
        /// <summary>Returns true if the action set this is contained in is active for this input source (or Any)</summary>
        public bool setActive { get { return actionSet.IsActive(inputSource); } }

        
        /// <summary>Returns true if this action is bound and the ActionSet is active</summary>
        public abstract bool active { get; }
        
        /// <summary>Returns true if the action is bound</summary>
        public abstract bool activeBinding { get; }

        /// <summary>Returns true if the action was bound and the ActionSet was active during the previous update</summary>
        public abstract bool lastActive { get; protected set; }

        /// <summary>Returns true if the action was bound during the previous update</summary>
        public abstract bool lastActiveBinding { get; }


        protected ulong inputSourceHandle;

        protected SteamVR_Action action;

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> Sets up the internals of the action source before SteamVR has been initialized.
        /// </summary>
        public virtual void Preinitialize(SteamVR_Action wrappingAction, SteamVR_Input_Sources forInputSource)
        {
            action = wrappingAction;
            inputSource = forInputSource;
        }

        public SteamVR_Action_Source() { }

        /// <summary>
        /// <strong>[Should not be called by user code]</strong> 
        /// Initializes the handle for the inputSource, and any other related SteamVR data.
        /// </summary>
        public virtual void Initialize()
        {
            inputSourceHandle = SteamVR_Input_Source.GetHandle(inputSource);
        }
    }


    public interface ISteamVR_Action : ISteamVR_Action_Source
    {
        /// <summary>Returns the active state of the action for the specified Input Source</summary>
        /// <param name="inputSource">The input source to check</param>
        bool GetActive(SteamVR_Input_Sources inputSource);
        
        /// <summary>Returns the name of the action without the action set or direction</summary>
        string GetShortName();
    }


    public interface ISteamVR_Action_Source
    {
        /// <summary>Returns true if this action is bound and the ActionSet is active</summary>
        bool active { get; }
        
        /// <summary>Returns true if the action is bound</summary>
        bool activeBinding { get; }

        /// <summary>Returns true if the action was bound and the ActionSet was active during the previous update</summary>
        bool lastActive { get; }
        
        /// <summary>Returns true if the action was bound last update</summary>
        bool lastActiveBinding { get; }

        /// <summary>The full string path for this action (from the action manifest)</summary>
        string fullPath { get; }

        /// <summary>The underlying handle for this action used for native SteamVR Input calls. Retrieved on Initialization from SteamVR.</summary>
        ulong handle { get; }

        /// <summary>The ActionSet this action is contained within</summary>
        SteamVR_ActionSet actionSet { get; }

        /// <summary>The action direction of this action (in for input, out for output)</summary>
        SteamVR_ActionDirections direction { get; }
    }
}