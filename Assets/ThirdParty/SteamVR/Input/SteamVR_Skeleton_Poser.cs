//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using System;
using System.Collections;
using UnityEngine;
using Valve.VR;
using System.Collections.Generic;
using System.Linq;

namespace Valve.VR
{
    public class SteamVR_Skeleton_Poser : MonoBehaviour
    {
        #region Editor Storage
        public bool poseEditorExpanded = true;
        public bool blendEditorExpanded = true;
        public string[] poseNames;
        #endregion

        public GameObject previewLeftHandPrefab;
        public GameObject previewRightHandPrefab;

        public SteamVR_Skeleton_Pose skeletonMainPose;
        public List<SteamVR_Skeleton_Pose> skeletonAdditionalPoses = new List<SteamVR_Skeleton_Pose>();

        [SerializeField]
        protected bool showLeftPreview = false;

        [SerializeField]
        protected bool showRightPreview = true; //show the right hand by default

        [SerializeField]
        protected GameObject previewLeftInstance;

        [SerializeField]
        protected GameObject previewRightInstance;

        [SerializeField]
        protected int previewPoseSelection = 0;
        
        public int blendPoseCount { get { return blendPoses.Length; } }

        public List<PoseBlendingBehaviour> blendingBehaviours = new List<PoseBlendingBehaviour>();

        public SteamVR_Skeleton_PoseSnapshot blendedSnapshotL;
        public SteamVR_Skeleton_PoseSnapshot blendedSnapshotR;

        private SkeletonBlendablePose[] blendPoses;

        private int boneCount;

        private bool poseUpdatedThisFrame;

        public float scale;


        protected void Awake()
        {
            if (previewLeftInstance != null)
                DestroyImmediate(previewLeftInstance);
            if (previewRightInstance != null)
                DestroyImmediate(previewRightInstance);

            blendPoses = new SkeletonBlendablePose[skeletonAdditionalPoses.Count + 1];
            for (int i = 0; i < blendPoseCount; i++)
            {
                blendPoses[i] = new SkeletonBlendablePose(GetPoseByIndex(i));
                blendPoses[i].PoseToSnapshots();
            }

            boneCount = skeletonMainPose.leftHand.bonePositions.Length;
            // NOTE: Is there a better way to get the bone count? idk
            blendedSnapshotL = new SteamVR_Skeleton_PoseSnapshot(boneCount, SteamVR_Input_Sources.LeftHand);
            blendedSnapshotR = new SteamVR_Skeleton_PoseSnapshot(boneCount, SteamVR_Input_Sources.RightHand);

        }



        /// <summary>
        /// Set the blending value of a blendingBehaviour. Works best on Manual type behaviours.
        /// </summary>
        public void SetBlendingBehaviourValue(string behaviourName, float value)
        {
            PoseBlendingBehaviour behaviour = blendingBehaviours.Find(b => b.name == behaviourName);
            if(behaviour == null)
            {
                Debug.LogError("[SteamVR] Blending Behaviour: " + behaviourName + " not found on Skeleton Poser: " + gameObject.name);
                return;
            }
            if(behaviour.type != PoseBlendingBehaviour.BlenderTypes.Manual)
            {
                Debug.LogWarning("[SteamVR] Blending Behaviour: " + behaviourName + " is not a manual behaviour. Its value will likely be overriden.");
            }
            behaviour.value = value;
        }
        /// <summary>
        /// Get the blending value of a blendingBehaviour.
        /// </summary>
        public float GetBlendingBehaviourValue(string behaviourName)
        {
            PoseBlendingBehaviour behaviour = blendingBehaviours.Find(b => b.name == behaviourName);
            if (behaviour == null)
            {
                Debug.LogError("[SteamVR] Blending Behaviour: " + behaviourName + " not found on Skeleton Poser: " + gameObject.name);
                return 0;
            }
            return behaviour.value;
        }

        /// <summary>
        /// Enable or disable a blending behaviour.
        /// </summary>
        public void SetBlendingBehaviourEnabled(string behaviourName, bool value)
        {
            PoseBlendingBehaviour behaviour = blendingBehaviours.Find(b => b.name == behaviourName);
            if (behaviour == null)
            {
                Debug.LogError("[SteamVR] Blending Behaviour: " + behaviourName + " not found on Skeleton Poser: " + gameObject.name);
                return;
            }
            behaviour.enabled = value;
        }
        /// <summary>
        /// Check if a blending behaviour is enabled.
        /// </summary>
        /// <param name="behaviourName"></param>
        /// <returns></returns>
        public bool GetBlendingBehaviourEnabled(string behaviourName)
        {
            PoseBlendingBehaviour behaviour = blendingBehaviours.Find(b => b.name == behaviourName);
            if (behaviour == null)
            {
                Debug.LogError("[SteamVR] Blending Behaviour: " + behaviourName + " not found on Skeleton Poser: " + gameObject.name);
                return false;
            }
            return behaviour.enabled;
        }
        /// <summary>
        /// Get a blending behaviour by name.
        /// </summary>
        public PoseBlendingBehaviour GetBlendingBehaviour(string behaviourName)
        {
            PoseBlendingBehaviour behaviour = blendingBehaviours.Find(b => b.name == behaviourName);
            if (behaviour == null)
            {
                Debug.LogError("[SteamVR] Blending Behaviour: " + behaviourName + " not found on Skeleton Poser: " + gameObject.name);
                return null;
            }
            return behaviour;
        }




        public SteamVR_Skeleton_Pose GetPoseByIndex(int index)
        {
            if (index == 0) { return skeletonMainPose; }
            else { return skeletonAdditionalPoses[index - 1]; }
        }

        private SteamVR_Skeleton_PoseSnapshot GetHandSnapshot(SteamVR_Input_Sources inputSource)
        {
            if (inputSource == SteamVR_Input_Sources.LeftHand)
                return blendedSnapshotL;
            else
                return blendedSnapshotR;
        }

        /// <summary>
        /// Retrieve the final animated pose, to be applied to a hand skeleton
        /// </summary>
        /// <param name="forAction">The skeleton action you want to blend between</param>
        /// <param name="handType">If this is for the left or right hand</param>
        public SteamVR_Skeleton_PoseSnapshot GetBlendedPose(SteamVR_Action_Skeleton skeletonAction, SteamVR_Input_Sources handType)
        {
            UpdatePose(skeletonAction, handType);
            return GetHandSnapshot(handType);
        }

        /// <summary>
        /// Retrieve the final animated pose, to be applied to a hand skeleton
        /// </summary>
        /// <param name="skeletonBehaviour">The skeleton behaviour you want to get the action/input source from to blend between</param>
        public SteamVR_Skeleton_PoseSnapshot GetBlendedPose(SteamVR_Behaviour_Skeleton skeletonBehaviour)
        {
            return GetBlendedPose(skeletonBehaviour.skeletonAction, skeletonBehaviour.inputSource);
        }


        /// <summary>
        /// Updates all pose animation and blending. Can be called from different places without performance concerns, as it will only let itself run once per frame.
        /// </summary>
        public void UpdatePose(SteamVR_Action_Skeleton skeletonAction, SteamVR_Input_Sources inputSource)
        {
            // only allow this function to run once per frame
            if (poseUpdatedThisFrame)
                return;

            poseUpdatedThisFrame = true;

            if (skeletonAction.activeBinding)
            {
                // always do additive animation on main pose
                blendPoses[0].UpdateAdditiveAnimation(skeletonAction, inputSource);
            }

            //copy from main pose as a base
            SteamVR_Skeleton_PoseSnapshot snap = GetHandSnapshot(inputSource);
            snap.CopyFrom(blendPoses[0].GetHandSnapshot(inputSource));

            ApplyBlenderBehaviours(skeletonAction, inputSource, snap);


            if (inputSource == SteamVR_Input_Sources.RightHand)
            {
                blendedSnapshotR = snap;
            }
            else if (inputSource == SteamVR_Input_Sources.LeftHand)
            {
                blendedSnapshotL = snap;
            }
        }

        protected void ApplyBlenderBehaviours(SteamVR_Action_Skeleton skeletonAction, SteamVR_Input_Sources inputSource, SteamVR_Skeleton_PoseSnapshot snapshot)
        {
            // apply blending for each behaviour
            for (int behaviourIndex = 0; behaviourIndex < blendingBehaviours.Count; behaviourIndex++)
            {
                blendingBehaviours[behaviourIndex].Update(Time.deltaTime, inputSource);
                // if disabled or very low influence, skip for perf
                if (blendingBehaviours[behaviourIndex].enabled && blendingBehaviours[behaviourIndex].influence * blendingBehaviours[behaviourIndex].value > 0.01f)
                {
                    if (blendingBehaviours[behaviourIndex].pose != 0 && skeletonAction.activeBinding)
                    {
                        // update additive animation only as needed
                        blendPoses[blendingBehaviours[behaviourIndex].pose].UpdateAdditiveAnimation(skeletonAction, inputSource);
                    }

                    blendingBehaviours[behaviourIndex].ApplyBlending(snapshot, blendPoses, inputSource);
                }
            }
        }

        protected void LateUpdate()
        {
            // let the pose be updated again the next frame
            poseUpdatedThisFrame = false;
        }
        
        /// <summary>Weighted average of n vector3s</summary>
        protected Vector3 BlendVectors(Vector3[] vectors, float[] weights)
        {
            Vector3 blendedVector = Vector3.zero;
            for (int i = 0; i < vectors.Length; i++)
            {
                blendedVector += vectors[i] * weights[i];
            }
            return blendedVector;
        }

        /// <summary>Weighted average of n quaternions</summary>
        protected Quaternion BlendQuaternions(Quaternion[] quaternions, float[] weights)
        {
            Quaternion outquat = Quaternion.identity;
            for (int i = 0; i < quaternions.Length; i++)
            {
                outquat *= Quaternion.Slerp(Quaternion.identity, quaternions[i], weights[i]);
            }
            return outquat;
        }

        /// <summary>
        /// A SkeletonBlendablePose holds a reference to a Skeleton_Pose scriptableObject, and also contains some helper functions. 
        /// Also handles pose-specific animation like additive finger motion.
        /// </summary>
        public class SkeletonBlendablePose
        {
            public SteamVR_Skeleton_Pose pose;
            public SteamVR_Skeleton_PoseSnapshot snapshotR;
            public SteamVR_Skeleton_PoseSnapshot snapshotL;

            /// <summary>
            /// Get the snapshot of this pose with effects such as additive finger animation applied.
            /// </summary>
            public SteamVR_Skeleton_PoseSnapshot GetHandSnapshot(SteamVR_Input_Sources inputSource)
            {
                if (inputSource == SteamVR_Input_Sources.LeftHand)
                {
                    return snapshotL;
                }
                else
                {
                    return snapshotR;
                }
            }

            //buffers for mirrored poses
            private Vector3[] additivePositionBuffer;
            private Quaternion[] additiveRotationBuffer;

            public void UpdateAdditiveAnimation(SteamVR_Action_Skeleton skeletonAction, SteamVR_Input_Sources inputSource)
            {
                SteamVR_Skeleton_PoseSnapshot snapshot = GetHandSnapshot(inputSource);
                SteamVR_Skeleton_Pose_Hand poseHand = pose.GetHand(inputSource);

                //setup mirrored pose buffers
                if (additivePositionBuffer == null) additivePositionBuffer = new Vector3[skeletonAction.boneCount];
                if (additiveRotationBuffer == null) additiveRotationBuffer = new Quaternion[skeletonAction.boneCount];


                for (int boneIndex = 0; boneIndex < snapshotL.bonePositions.Length; boneIndex++)
                {
                    int fingerIndex = SteamVR_Skeleton_JointIndexes.GetFingerForBone(boneIndex);
                    SteamVR_Skeleton_FingerExtensionTypes extensionType = poseHand.GetMovementTypeForBone(boneIndex);

                    //do target pose mirroring on left hand
                    if(inputSource == SteamVR_Input_Sources.LeftHand)
                    {
                        SteamVR_Behaviour_Skeleton.MirrorBonePosition(ref skeletonAction.bonePositions[boneIndex], ref additivePositionBuffer[boneIndex], boneIndex);
                        SteamVR_Behaviour_Skeleton.MirrorBoneRotation(ref skeletonAction.boneRotations[boneIndex], ref additiveRotationBuffer[boneIndex], boneIndex);
                    }
                    else
                    {
                        additivePositionBuffer[boneIndex] = skeletonAction.bonePositions[boneIndex];
                        additiveRotationBuffer[boneIndex] = skeletonAction.boneRotations[boneIndex];
                    }



                    if (extensionType == SteamVR_Skeleton_FingerExtensionTypes.Free)
                    {
                        snapshot.bonePositions[boneIndex] = additivePositionBuffer[boneIndex];
                        snapshot.boneRotations[boneIndex] = additiveRotationBuffer[boneIndex];
                    }
                    else if (extensionType == SteamVR_Skeleton_FingerExtensionTypes.Extend)
                    {

                        // lerp to open pose by fingercurl
                        snapshot.bonePositions[boneIndex] = Vector3.Lerp(poseHand.bonePositions[boneIndex], additivePositionBuffer[boneIndex], 1 - skeletonAction.fingerCurls[fingerIndex]);
                        snapshot.boneRotations[boneIndex] = Quaternion.Lerp(poseHand.boneRotations[boneIndex], additiveRotationBuffer[boneIndex], 1 - skeletonAction.fingerCurls[fingerIndex]);
                        

                    }
                    else if (extensionType == SteamVR_Skeleton_FingerExtensionTypes.Contract)
                    {
                        // lerp to closed pose by fingercurl
                        snapshot.bonePositions[boneIndex] = Vector3.Lerp(poseHand.bonePositions[boneIndex], additivePositionBuffer[boneIndex], skeletonAction.fingerCurls[fingerIndex]);
                        snapshot.boneRotations[boneIndex] = Quaternion.Lerp(poseHand.boneRotations[boneIndex], additiveRotationBuffer[boneIndex], skeletonAction.fingerCurls[fingerIndex]);
                    }
                }
            }

            /// <summary>
            /// Init based on an existing Skeleton_Pose
            /// </summary>
            public SkeletonBlendablePose(SteamVR_Skeleton_Pose p)
            {
                pose = p;
                snapshotR = new SteamVR_Skeleton_PoseSnapshot(p.rightHand.bonePositions.Length, SteamVR_Input_Sources.RightHand);
                snapshotL = new SteamVR_Skeleton_PoseSnapshot(p.leftHand.bonePositions.Length, SteamVR_Input_Sources.LeftHand);
            }

            /// <summary>
            /// Copy the base pose into the snapshots.
            /// </summary>
            public void PoseToSnapshots()
            {
                snapshotR.position = pose.rightHand.position;
                snapshotR.rotation = pose.rightHand.rotation;
                pose.rightHand.bonePositions.CopyTo(snapshotR.bonePositions, 0);
                pose.rightHand.boneRotations.CopyTo(snapshotR.boneRotations, 0);

                snapshotL.position = pose.leftHand.position;
                snapshotL.rotation = pose.leftHand.rotation;
                pose.leftHand.bonePositions.CopyTo(snapshotL.bonePositions, 0);
                pose.leftHand.boneRotations.CopyTo(snapshotL.boneRotations, 0);
            }

            public SkeletonBlendablePose() { }
        }

        /// <summary>
        /// A filter applied to the base pose. Blends to a secondary pose by a certain weight. Can be masked per-finger
        /// </summary>
        [System.Serializable]
        public class PoseBlendingBehaviour
        {
            public string name;
            public bool enabled = true;
            public float influence = 1;
            public int pose = 1;
            public float value = 0;
            public SteamVR_Action_Single action_single;
            public SteamVR_Action_Boolean action_bool;
            public float smoothingSpeed = 0;
            public BlenderTypes type;
            public bool useMask;
            public SteamVR_Skeleton_HandMask mask = new SteamVR_Skeleton_HandMask();

            public bool previewEnabled;

            /// <summary>
            /// Performs smoothing based on deltaTime parameter.
            /// </summary>
            public void Update(float deltaTime, SteamVR_Input_Sources inputSource)
            {
                if (type == BlenderTypes.AnalogAction)
                {
                    if (smoothingSpeed == 0)
                        value = action_single.GetAxis(inputSource);
                    else
                        value = Mathf.Lerp(value, action_single.GetAxis(inputSource), deltaTime * smoothingSpeed);
                }
                if (type == BlenderTypes.BooleanAction)
                {
                    if (smoothingSpeed == 0)
                        value = action_bool.GetState(inputSource) ? 1 : 0;
                    else
                        value = Mathf.Lerp(value, action_bool.GetState(inputSource) ? 1 : 0, deltaTime * smoothingSpeed);
                }
            }

            /// <summary>
            /// Apply blending to this behaviour's pose to an existing snapshot.
            /// </summary>
            /// <param name="snapshot">Snapshot to modify</param>
            /// <param name="blendPoses">List of blend poses to get the target pose</param>
            /// <param name="inputSource">Which hand to receive input from</param>
            public void ApplyBlending(SteamVR_Skeleton_PoseSnapshot snapshot, SkeletonBlendablePose[] blendPoses, SteamVR_Input_Sources inputSource)
            {
                SteamVR_Skeleton_PoseSnapshot targetSnapshot = blendPoses[pose].GetHandSnapshot(inputSource);
                if (mask.GetFinger(0) || useMask == false)
                {
                    snapshot.position = Vector3.Lerp(snapshot.position, targetSnapshot.position, influence * value);
                    snapshot.rotation = Quaternion.Slerp(snapshot.rotation, targetSnapshot.rotation, influence * value);
                }

                for (int boneIndex = 0; boneIndex < snapshot.bonePositions.Length; boneIndex++)
                {
                    // verify the current finger is enabled in the mask, or if no mask is used.
                    if (mask.GetFinger(SteamVR_Skeleton_JointIndexes.GetFingerForBone(boneIndex) + 1) || useMask == false)
                    {
                        snapshot.bonePositions[boneIndex] = Vector3.Lerp(snapshot.bonePositions[boneIndex], targetSnapshot.bonePositions[boneIndex], influence * value);
                        snapshot.boneRotations[boneIndex] = Quaternion.Slerp(snapshot.boneRotations[boneIndex], targetSnapshot.boneRotations[boneIndex], influence * value);
                    }
                }
            }

            public PoseBlendingBehaviour()
            {
                enabled = true;
                influence = 1;
            }

            public enum BlenderTypes
            {
                Manual, AnalogAction, BooleanAction
            }
        }
    }

    /// <summary>
    /// PoseSnapshots hold a skeleton pose for one hand, as well as storing which hand they contain. 
    /// They have several functions for combining BlendablePoses.
    /// </summary>
    public class SteamVR_Skeleton_PoseSnapshot
    {
        public SteamVR_Input_Sources inputSource;

        public Vector3 position;
        public Quaternion rotation;

        public Vector3[] bonePositions;
        public Quaternion[] boneRotations;

        public SteamVR_Skeleton_PoseSnapshot(int boneCount, SteamVR_Input_Sources source)
        {
            inputSource = source;
            bonePositions = new Vector3[boneCount];
            boneRotations = new Quaternion[boneCount];
            position = Vector3.zero;
            rotation = Quaternion.identity;
        }

        /// <summary>
        /// Perform a deep copy from one poseSnapshot to another.
        /// </summary>
        public void CopyFrom(SteamVR_Skeleton_PoseSnapshot source)
        {
            inputSource = source.inputSource;
            position = source.position;
            rotation = source.rotation;

            for (int boneIndex = 0; boneIndex < bonePositions.Length; boneIndex++)
            {
                bonePositions[boneIndex] = source.bonePositions[boneIndex];
                boneRotations[boneIndex] = source.boneRotations[boneIndex];
            }
        }
    }

    /// <summary>
    /// Simple mask for fingers
    /// </summary>
    [System.Serializable]
    public class SteamVR_Skeleton_HandMask
    {
        public bool palm;
        public bool thumb;
        public bool index;
        public bool middle;
        public bool ring;
        public bool pinky;
        public bool[] values = new bool[6];

        public void SetFinger(int i, bool value)
        {
            values[i] = value;
            Apply();
        }

        public bool GetFinger(int i)
        {
            return values[i];
        }

        public SteamVR_Skeleton_HandMask()
        {
            values = new bool[6];
            Reset();
        }

        /// <summary>
        /// All elements on
        /// </summary>
        public void Reset()
        {
            values = new bool[6];
            for (int i = 0; i < 6; i++)
            {
                values[i] = true;
            }
            Apply();
        }

        protected void Apply()
        {
            palm = values[0];
            thumb = values[1];
            index = values[2];
            middle = values[3];
            ring = values[4];
            pinky = values[5];
        }

        public static readonly SteamVR_Skeleton_HandMask fullMask = new SteamVR_Skeleton_HandMask();
    };

}
