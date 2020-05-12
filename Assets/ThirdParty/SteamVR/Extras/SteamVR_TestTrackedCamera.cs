//======= Copyright (c) Valve Corporation, All rights reserved. ===============
using UnityEngine;

namespace Valve.VR.Extras
{
    public class SteamVR_TestTrackedCamera : MonoBehaviour
    {
        public Material material;
        public Transform target;
        public bool undistorted = true;
        public bool cropped = true;

        private void OnEnable()
        {
            // The video stream must be symmetrically acquired and released in
            // order to properly disable the stream once there are no consumers.
            SteamVR_TrackedCamera.VideoStreamTexture source = SteamVR_TrackedCamera.Source(undistorted);
            source.Acquire();

            // Auto-disable if no camera is present.
            if (!source.hasCamera)
                enabled = false;
        }

        private void OnDisable()
        {
            // Clear the texture when no longer active.
            material.mainTexture = null;

            // The video stream must be symmetrically acquired and released in
            // order to properly disable the stream once there are no consumers.
            SteamVR_TrackedCamera.VideoStreamTexture source = SteamVR_TrackedCamera.Source(undistorted);
            source.Release();
        }

        private void Update()
        {
            SteamVR_TrackedCamera.VideoStreamTexture source = SteamVR_TrackedCamera.Source(undistorted);
            Texture2D texture = source.texture;
            if (texture == null)
            {
                return;
            }

            // Apply the latest texture to the material.  This must be performed
            // every frame since the underlying texture is actually part of a ring
            // buffer which is updated in lock-step with its associated pose.
            // (You actually really only need to call any of the accessors which
            // internally call Update on the SteamVR_TrackedCamera.VideoStreamTexture).
            material.mainTexture = texture;

            // Adjust the height of the quad based on the aspect to keep the texels square.
            float aspect = (float)texture.width / texture.height;

            // The undistorted video feed has 'bad' areas near the edges where the original
            // square texture feed is stretched to undo the fisheye from the lens.
            // Therefore, you'll want to crop it to the specified frameBounds to remove this.
            if (cropped)
            {
                VRTextureBounds_t bounds = source.frameBounds;
                material.mainTextureOffset = new Vector2(bounds.uMin, bounds.vMin);

                float du = bounds.uMax - bounds.uMin;
                float dv = bounds.vMax - bounds.vMin;
                material.mainTextureScale = new Vector2(du, dv);

                aspect *= Mathf.Abs(du / dv);
            }
            else
            {
                material.mainTextureOffset = Vector2.zero;
                material.mainTextureScale = new Vector2(1, -1);
            }

            target.localScale = new Vector3(1, 1.0f / aspect, 1);

            // Apply the pose that this frame was recorded at.
            if (source.hasTracking)
            {
                SteamVR_Utils.RigidTransform rigidTransform = source.transform;
                target.localPosition = rigidTransform.pos;
                target.localRotation = rigidTransform.rot;
            }
        }
    }
}