//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using Valve.VR;

namespace Valve.VR
{
    /// <summary>
    /// The major difference between this component and the standard SteamVR_Behaviour_Skeleton is this one lets you
    /// only use the joints you care about. You can set the transforms you're concerned with and ignore the ones you're not.
    /// </summary>
    public class SteamVR_Behaviour_SkeletonCustom : SteamVR_Behaviour_Skeleton
    {
        [SerializeField]
        protected Transform _wrist;

        [SerializeField]
        protected Transform _thumbMetacarpal;

        [SerializeField]
        protected Transform _thumbProximal;

        [SerializeField]
        protected Transform _thumbMiddle;

        [SerializeField]
        protected Transform _thumbDistal;

        [SerializeField]
        protected Transform _thumbTip;

        [SerializeField]
        protected Transform _thumbAux;

        [SerializeField]
        protected Transform _indexMetacarpal;

        [SerializeField]
        protected Transform _indexProximal;

        [SerializeField]
        protected Transform _indexMiddle;

        [SerializeField]
        protected Transform _indexDistal;

        [SerializeField]
        protected Transform _indexTip;

        [SerializeField]
        protected Transform _indexAux;

        [SerializeField]
        protected Transform _middleMetacarpal;

        [SerializeField]
        protected Transform _middleProximal;

        [SerializeField]
        protected Transform _middleMiddle;

        [SerializeField]
        protected Transform _middleDistal;

        [SerializeField]
        protected Transform _middleTip;

        [SerializeField]
        protected Transform _middleAux;

        [SerializeField]
        protected Transform _ringMetacarpal;

        [SerializeField]
        protected Transform _ringProximal;

        [SerializeField]
        protected Transform _ringMiddle;

        [SerializeField]
        protected Transform _ringDistal;

        [SerializeField]
        protected Transform _ringTip;

        [SerializeField]
        protected Transform _ringAux;

        [SerializeField]
        protected Transform _pinkyMetacarpal;

        [SerializeField]
        protected Transform _pinkyProximal;

        [SerializeField]
        protected Transform _pinkyMiddle;

        [SerializeField]
        protected Transform _pinkyDistal;

        [SerializeField]
        protected Transform _pinkyTip;

        [SerializeField]
        protected Transform _pinkyAux;


        protected override void AssignBonesArray()
        {
            if (bones == null)
                bones = new Transform[SteamVR_Action_Skeleton.numBones];

            bones[SteamVR_Skeleton_JointIndexes.wrist] = _wrist;
            bones[SteamVR_Skeleton_JointIndexes.thumbProximal] = _thumbProximal;
            bones[SteamVR_Skeleton_JointIndexes.thumbMiddle] = _thumbMiddle;
            bones[SteamVR_Skeleton_JointIndexes.thumbDistal] = _thumbDistal;
            bones[SteamVR_Skeleton_JointIndexes.thumbTip] = _thumbTip;
            bones[SteamVR_Skeleton_JointIndexes.thumbAux] = _thumbAux;
            bones[SteamVR_Skeleton_JointIndexes.indexProximal] = _indexProximal;
            bones[SteamVR_Skeleton_JointIndexes.indexMiddle] = _indexMiddle;
            bones[SteamVR_Skeleton_JointIndexes.indexDistal] = _indexDistal;
            bones[SteamVR_Skeleton_JointIndexes.indexTip] = _indexTip;
            bones[SteamVR_Skeleton_JointIndexes.indexAux] = _indexAux;
            bones[SteamVR_Skeleton_JointIndexes.middleProximal] = _middleProximal;
            bones[SteamVR_Skeleton_JointIndexes.middleMiddle] = _middleMiddle;
            bones[SteamVR_Skeleton_JointIndexes.middleDistal] = _middleDistal;
            bones[SteamVR_Skeleton_JointIndexes.middleTip] = _middleTip;
            bones[SteamVR_Skeleton_JointIndexes.middleAux] = _middleAux;
            bones[SteamVR_Skeleton_JointIndexes.ringProximal] = _ringProximal;
            bones[SteamVR_Skeleton_JointIndexes.ringMiddle] = _ringMiddle;
            bones[SteamVR_Skeleton_JointIndexes.ringDistal] = _ringDistal;
            bones[SteamVR_Skeleton_JointIndexes.ringTip] = _ringTip;
            bones[SteamVR_Skeleton_JointIndexes.ringAux] = _ringAux;
            bones[SteamVR_Skeleton_JointIndexes.pinkyProximal] = _pinkyProximal;
            bones[SteamVR_Skeleton_JointIndexes.pinkyMiddle] = _pinkyMiddle;
            bones[SteamVR_Skeleton_JointIndexes.pinkyDistal] = _pinkyDistal;
            bones[SteamVR_Skeleton_JointIndexes.pinkyTip] = _pinkyTip;
            bones[SteamVR_Skeleton_JointIndexes.pinkyAux] = _pinkyAux;
        }
    }
}