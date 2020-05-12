//======= Copyright (c) Valve Corporation, All rights reserved. ===============
//
// Purpose: Utilities for working with SteamVR
//
//=============================================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using Valve.VR;
using System.IO;

namespace Valve.VR
{
    public static class SteamVR_Utils
    {
        // this version does not clamp [0..1]
        public static Quaternion Slerp(Quaternion A, Quaternion B, float time)
        {
            float cosom = Mathf.Clamp(A.x * B.x + A.y * B.y + A.z * B.z + A.w * B.w, -1.0f, 1.0f);
            if (cosom < 0.0f)
            {
                B = new Quaternion(-B.x, -B.y, -B.z, -B.w);
                cosom = -cosom;
            }

            float sclp, sclq;
            if ((1.0f - cosom) > 0.0001f)
            {
                float omega = Mathf.Acos(cosom);
                float sinom = Mathf.Sin(omega);
                sclp = Mathf.Sin((1.0f - time) * omega) / sinom;
                sclq = Mathf.Sin(time * omega) / sinom;
            }
            else
            {
                // "from" and "to" very close, so do linear interp
                sclp = 1.0f - time;
                sclq = time;
            }

            return new Quaternion(
                sclp * A.x + sclq * B.x,
                sclp * A.y + sclq * B.y,
                sclp * A.z + sclq * B.z,
                sclp * A.w + sclq * B.w);
        }

        public static Vector3 Lerp(Vector3 from, Vector3 to, float amount)
        {
            return new Vector3(
                Lerp(from.x, to.x, amount),
                Lerp(from.y, to.y, amount),
                Lerp(from.z, to.z, amount));
        }

        public static float Lerp(float from, float to, float amount)
        {
            return from + (to - from) * amount;
        }

        public static double Lerp(double from, double to, double amount)
        {
            return from + (to - from) * amount;
        }

        public static float InverseLerp(Vector3 from, Vector3 to, Vector3 result)
        {
            return Vector3.Dot(result - from, to - from);
        }

        public static float InverseLerp(float from, float to, float result)
        {
            return (result - from) / (to - from);
        }

        public static double InverseLerp(double from, double to, double result)
        {
            return (result - from) / (to - from);
        }

        public static float Saturate(float A)
        {
            return (A < 0) ? 0 : (A > 1) ? 1 : A;
        }

        public static Vector2 Saturate(Vector2 A)
        {
            return new Vector2(Saturate(A.x), Saturate(A.y));
        }

        public static Vector3 Saturate(Vector3 A)
        {
            return new Vector3(Saturate(A.x), Saturate(A.y), Saturate(A.z));
        }

        public static float Abs(float A)
        {
            return (A < 0) ? -A : A;
        }

        public static Vector2 Abs(Vector2 A)
        {
            return new Vector2(Abs(A.x), Abs(A.y));
        }

        public static Vector3 Abs(Vector3 A)
        {
            return new Vector3(Abs(A.x), Abs(A.y), Abs(A.z));
        }

        private static float _copysign(float sizeval, float signval)
        {
            return Mathf.Sign(signval) == 1 ? Mathf.Abs(sizeval) : -Mathf.Abs(sizeval);
        }

        public static Quaternion GetRotation(this Matrix4x4 matrix)
        {
            Quaternion q = new Quaternion();
            q.w = Mathf.Sqrt(Mathf.Max(0, 1f + matrix.m00 + matrix.m11 + matrix.m22)) / 2f;
            q.x = Mathf.Sqrt(Mathf.Max(0, 1f + matrix.m00 - matrix.m11 - matrix.m22)) / 2f;
            q.y = Mathf.Sqrt(Mathf.Max(0, 1f - matrix.m00 + matrix.m11 - matrix.m22)) / 2f;
            q.z = Mathf.Sqrt(Mathf.Max(0, 1f - matrix.m00 - matrix.m11 + matrix.m22)) / 2f;
            q.x = _copysign(q.x, matrix.m21 - matrix.m12);
            q.y = _copysign(q.y, matrix.m02 - matrix.m20);
            q.z = _copysign(q.z, matrix.m10 - matrix.m01);
            return q;
        }

        public static Vector3 GetPosition(this Matrix4x4 matrix)
        {
            float x = matrix.m03;
            float y = matrix.m13;
            float z = matrix.m23;

            return new Vector3(x, y, z);
        }

        public static Vector3 GetScale(this Matrix4x4 m)
        {
            float x = Mathf.Sqrt(m.m00 * m.m00 + m.m01 * m.m01 + m.m02 * m.m02);
            float y = Mathf.Sqrt(m.m10 * m.m10 + m.m11 * m.m11 + m.m12 * m.m12);
            float z = Mathf.Sqrt(m.m20 * m.m20 + m.m21 * m.m21 + m.m22 * m.m22);

            return new Vector3(x, y, z);
        }

        public static Quaternion GetRotation(HmdMatrix34_t matrix)
        {
            if ((matrix.m2 != 0 || matrix.m6 != 0 || matrix.m10 != 0) && (matrix.m1 != 0 || matrix.m5 != 0 || matrix.m9 != 0))
                return Quaternion.LookRotation(new Vector3(-matrix.m2, -matrix.m6, matrix.m10), new Vector3(matrix.m1, matrix.m5, -matrix.m9));
            else
                return Quaternion.identity;
        }

        public static Vector3 GetPosition(HmdMatrix34_t matrix)
        {
            return new Vector3(matrix.m3, matrix.m7, -matrix.m11);
        }

        [System.Serializable]
        public struct RigidTransform
        {
            public Vector3 pos;
            public Quaternion rot;

            public static RigidTransform identity
            {
                get { return new RigidTransform(Vector3.zero, Quaternion.identity); }
            }

            public static RigidTransform FromLocal(Transform fromTransform)
            {
                return new RigidTransform(fromTransform.localPosition, fromTransform.localRotation);
            }

            public RigidTransform(Vector3 position, Quaternion rotation)
            {
                this.pos = position;
                this.rot = rotation;
            }

            public RigidTransform(Transform fromTransform)
            {
                this.pos = fromTransform.position;
                this.rot = fromTransform.rotation;
            }

            public RigidTransform(Transform from, Transform to)
            {
                Quaternion inverse = Quaternion.Inverse(from.rotation);
                rot = inverse * to.rotation;
                pos = inverse * (to.position - from.position);
            }

            public RigidTransform(HmdMatrix34_t pose)
            {
                Matrix4x4 matrix = Matrix4x4.identity;

                matrix[0, 0] = pose.m0;
                matrix[0, 1] = pose.m1;
                matrix[0, 2] = -pose.m2;
                matrix[0, 3] = pose.m3;

                matrix[1, 0] = pose.m4;
                matrix[1, 1] = pose.m5;
                matrix[1, 2] = -pose.m6;
                matrix[1, 3] = pose.m7;

                matrix[2, 0] = -pose.m8;
                matrix[2, 1] = -pose.m9;
                matrix[2, 2] = pose.m10;
                matrix[2, 3] = -pose.m11;

                this.pos = matrix.GetPosition();
                this.rot = matrix.GetRotation();
            }

            public RigidTransform(HmdMatrix44_t pose)
            {
                Matrix4x4 matrix = Matrix4x4.identity;

                matrix[0, 0] = pose.m0;
                matrix[0, 1] = pose.m1;
                matrix[0, 2] = -pose.m2;
                matrix[0, 3] = pose.m3;

                matrix[1, 0] = pose.m4;
                matrix[1, 1] = pose.m5;
                matrix[1, 2] = -pose.m6;
                matrix[1, 3] = pose.m7;

                matrix[2, 0] = -pose.m8;
                matrix[2, 1] = -pose.m9;
                matrix[2, 2] = pose.m10;
                matrix[2, 3] = -pose.m11;

                matrix[3, 0] = pose.m12;
                matrix[3, 1] = pose.m13;
                matrix[3, 2] = -pose.m14;
                matrix[3, 3] = pose.m15;

                this.pos = matrix.GetPosition();
                this.rot = matrix.GetRotation();
            }

            public HmdMatrix44_t ToHmdMatrix44()
            {
                Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                HmdMatrix44_t pose = new HmdMatrix44_t();

                pose.m0 = matrix[0, 0];
                pose.m1 = matrix[0, 1];
                pose.m2 = -matrix[0, 2];
                pose.m3 = matrix[0, 3];

                pose.m4 = matrix[1, 0];
                pose.m5 = matrix[1, 1];
                pose.m6 = -matrix[1, 2];
                pose.m7 = matrix[1, 3];

                pose.m8 = -matrix[2, 0];
                pose.m9 = -matrix[2, 1];
                pose.m10 = matrix[2, 2];
                pose.m11 = -matrix[2, 3];

                pose.m12 = matrix[3, 0];
                pose.m13 = matrix[3, 1];
                pose.m14 = -matrix[3, 2];
                pose.m15 = matrix[3, 3];

                return pose;
            }

            public HmdMatrix34_t ToHmdMatrix34()
            {
                Matrix4x4 matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
                HmdMatrix34_t pose = new HmdMatrix34_t();

                pose.m0 = matrix[0, 0];
                pose.m1 = matrix[0, 1];
                pose.m2 = -matrix[0, 2];
                pose.m3 = matrix[0, 3];

                pose.m4 = matrix[1, 0];
                pose.m5 = matrix[1, 1];
                pose.m6 = -matrix[1, 2];
                pose.m7 = matrix[1, 3];

                pose.m8 = -matrix[2, 0];
                pose.m9 = -matrix[2, 1];
                pose.m10 = matrix[2, 2];
                pose.m11 = -matrix[2, 3];

                return pose;
            }

            public override bool Equals(object other)
            {
                if (other is RigidTransform)
                {
                    RigidTransform t = (RigidTransform)other;
                    return pos == t.pos && rot == t.rot;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return pos.GetHashCode() ^ rot.GetHashCode();
            }

            public static bool operator ==(RigidTransform a, RigidTransform b)
            {
                return a.pos == b.pos && a.rot == b.rot;
            }

            public static bool operator !=(RigidTransform a, RigidTransform b)
            {
                return a.pos != b.pos || a.rot != b.rot;
            }

            public static RigidTransform operator *(RigidTransform a, RigidTransform b)
            {
                return new RigidTransform
                {
                    rot = a.rot * b.rot,
                    pos = a.pos + a.rot * b.pos
                };
            }

            public void Inverse()
            {
                rot = Quaternion.Inverse(rot);
                pos = -(rot * pos);
            }

            public RigidTransform GetInverse()
            {
                RigidTransform transform = new RigidTransform(pos, rot);
                transform.Inverse();
                return transform;
            }

            public void Multiply(RigidTransform a, RigidTransform b)
            {
                rot = a.rot * b.rot;
                pos = a.pos + a.rot * b.pos;
            }

            public Vector3 InverseTransformPoint(Vector3 point)
            {
                return Quaternion.Inverse(rot) * (point - pos);
            }

            public Vector3 TransformPoint(Vector3 point)
            {
                return pos + (rot * point);
            }

            public static Vector3 operator *(RigidTransform t, Vector3 v)
            {
                return t.TransformPoint(v);
            }

            public static RigidTransform Interpolate(RigidTransform a, RigidTransform b, float t)
            {
                return new RigidTransform(Vector3.Lerp(a.pos, b.pos, t), Quaternion.Slerp(a.rot, b.rot, t));
            }

            public void Interpolate(RigidTransform to, float t)
            {
                pos = SteamVR_Utils.Lerp(pos, to.pos, t);
                rot = SteamVR_Utils.Slerp(rot, to.rot, t);
            }
        }

        public delegate object SystemFn(CVRSystem system, params object[] args);

        public static object CallSystemFn(SystemFn fn, params object[] args)
        {
            bool initOpenVR = (!SteamVR.active && !SteamVR.usingNativeSupport);
            if (initOpenVR)
            {
                EVRInitError error = EVRInitError.None;
                OpenVR.Init(ref error, EVRApplicationType.VRApplication_Utility);
            }

            CVRSystem system = OpenVR.System;
            object result = (system != null) ? fn(system, args) : null;

            if (initOpenVR)
                OpenVR.Shutdown();

            return result;
        }

        public static void TakeStereoScreenshot(uint screenshotHandle, GameObject target, int cellSize, float ipd, ref string previewFilename, ref string VRFilename)
        {
            const int width = 4096;
            const int height = width / 2;
            const int halfHeight = height / 2;

            Texture2D texture = new Texture2D(width, height * 2, TextureFormat.ARGB32, false);

            System.Diagnostics.Stopwatch timer = new System.Diagnostics.Stopwatch();

            Camera tempCamera = null;

            timer.Start();

            Camera camera = target.GetComponent<Camera>();
            if (camera == null)
            {
                if (tempCamera == null)
                    tempCamera = new GameObject().AddComponent<Camera>();
                camera = tempCamera;
            }

            // Render preview texture
            const int previewWidth = 2048;
            const int previewHeight = 2048;
            Texture2D previewTexture = new Texture2D(previewWidth, previewHeight, TextureFormat.ARGB32, false);
            RenderTexture targetPreviewTexture = new RenderTexture(previewWidth, previewHeight, 24);

            RenderTexture oldTargetTexture = camera.targetTexture;
            bool oldOrthographic = camera.orthographic;
            float oldFieldOfView = camera.fieldOfView;
            float oldAspect = camera.aspect;
            StereoTargetEyeMask oldstereoTargetEye = camera.stereoTargetEye;
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.fieldOfView = 60.0f;
            camera.orthographic = false;
            camera.targetTexture = targetPreviewTexture;
            camera.aspect = 1.0f;
            camera.Render();

            // copy preview texture
            RenderTexture.active = targetPreviewTexture;
            previewTexture.ReadPixels(new Rect(0, 0, targetPreviewTexture.width, targetPreviewTexture.height), 0, 0);
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(targetPreviewTexture);

            SteamVR_SphericalProjection fx = camera.gameObject.AddComponent<SteamVR_SphericalProjection>();

            Vector3 oldPosition = target.transform.localPosition;
            Quaternion oldRotation = target.transform.localRotation;
            Vector3 basePosition = target.transform.position;
            Quaternion baseRotation = Quaternion.Euler(0, target.transform.rotation.eulerAngles.y, 0);

            Transform transform = camera.transform;

            int vTotal = halfHeight / cellSize;
            float dv = 90.0f / vTotal; // vertical degrees per segment
            float dvHalf = dv / 2.0f;

            RenderTexture targetTexture = new RenderTexture(cellSize, cellSize, 24);
            targetTexture.wrapMode = TextureWrapMode.Clamp;
            targetTexture.antiAliasing = 8;

            camera.fieldOfView = dv;
            camera.orthographic = false;
            camera.targetTexture = targetTexture;
            camera.aspect = oldAspect;
            camera.stereoTargetEye = StereoTargetEyeMask.None;

            // Render sections of a sphere using a rectilinear projection
            // and resample using a sphereical projection into a single panorama
            // texture per eye.  We break into sections in order to keep the eye
            // separation similar around the sphere.  Rendering alternates between
            // top and bottom sections, sweeping horizontally around the sphere,
            // alternating left and right eyes.
            for (int v = 0; v < vTotal; v++)
            {
                float pitch = 90.0f - (v * dv) - dvHalf;
                int uTotal = width / targetTexture.width;
                float du = 360.0f / uTotal; // horizontal degrees per segment
                float duHalf = du / 2.0f;

                int vTarget = v * halfHeight / vTotal;

                for (int i = 0; i < 2; i++) // top, bottom
                {
                    if (i == 1)
                    {
                        pitch = -pitch;
                        vTarget = height - vTarget - cellSize;
                    }

                    for (int u = 0; u < uTotal; u++)
                    {
                        float yaw = -180.0f + (u * du) + duHalf;

                        int uTarget = u * width / uTotal;

                        int vTargetOffset = 0;
                        float xOffset = -ipd / 2 * Mathf.Cos(pitch * Mathf.Deg2Rad);

                        for (int j = 0; j < 2; j++) // left, right
                        {
                            if (j == 1)
                            {
                                vTargetOffset = height;
                                xOffset = -xOffset;
                            }

                            Vector3 offset = baseRotation * Quaternion.Euler(0, yaw, 0) * new Vector3(xOffset, 0, 0);
                            transform.position = basePosition + offset;

                            Quaternion direction = Quaternion.Euler(pitch, yaw, 0.0f);
                            transform.rotation = baseRotation * direction;

                            // vector pointing to center of this section
                            Vector3 N = direction * Vector3.forward;

                            // horizontal span of this section in degrees
                            float phi0 = yaw - (du / 2);
                            float phi1 = phi0 + du;

                            // vertical span of this section in degrees
                            float theta0 = pitch + (dv / 2);
                            float theta1 = theta0 - dv;

                            float midPhi = (phi0 + phi1) / 2;
                            float baseTheta = Mathf.Abs(theta0) < Mathf.Abs(theta1) ? theta0 : theta1;

                            // vectors pointing to corners of image closes to the equator
                            Vector3 V00 = Quaternion.Euler(baseTheta, phi0, 0.0f) * Vector3.forward;
                            Vector3 V01 = Quaternion.Euler(baseTheta, phi1, 0.0f) * Vector3.forward;

                            // vectors pointing to top and bottom midsection of image
                            Vector3 V0M = Quaternion.Euler(theta0, midPhi, 0.0f) * Vector3.forward;
                            Vector3 V1M = Quaternion.Euler(theta1, midPhi, 0.0f) * Vector3.forward;

                            // intersection points for each of the above
                            Vector3 P00 = V00 / Vector3.Dot(V00, N);
                            Vector3 P01 = V01 / Vector3.Dot(V01, N);
                            Vector3 P0M = V0M / Vector3.Dot(V0M, N);
                            Vector3 P1M = V1M / Vector3.Dot(V1M, N);

                            // calculate basis vectors for plane
                            Vector3 P00_P01 = P01 - P00;
                            Vector3 P0M_P1M = P1M - P0M;

                            float uMag = P00_P01.magnitude;
                            float vMag = P0M_P1M.magnitude;

                            float uScale = 1.0f / uMag;
                            float vScale = 1.0f / vMag;

                            Vector3 uAxis = P00_P01 * uScale;
                            Vector3 vAxis = P0M_P1M * vScale;

                            // update material constant buffer
                            fx.Set(N, phi0, phi1, theta0, theta1,
                                uAxis, P00, uScale,
                                vAxis, P0M, vScale);

                            camera.aspect = uMag / vMag;
                            camera.Render();

                            RenderTexture.active = targetTexture;
                            texture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), uTarget, vTarget + vTargetOffset);
                            RenderTexture.active = null;
                        }

                        // Update progress
                        float progress = (float)(v * (uTotal * 2.0f) + u + i * uTotal) / (float)(vTotal * (uTotal * 2.0f));
                        OpenVR.Screenshots.UpdateScreenshotProgress(screenshotHandle, progress);
                    }
                }
            }

            // 100% flush
            OpenVR.Screenshots.UpdateScreenshotProgress(screenshotHandle, 1.0f);

            // Save textures to disk.
            // Add extensions
            previewFilename += ".png";
            VRFilename += ".png";

            // Preview
            previewTexture.Apply();
            System.IO.File.WriteAllBytes(previewFilename, previewTexture.EncodeToPNG());

            // VR
            texture.Apply();
            System.IO.File.WriteAllBytes(VRFilename, texture.EncodeToPNG());

            // Cleanup.
            if (camera != tempCamera)
            {
                camera.targetTexture = oldTargetTexture;
                camera.orthographic = oldOrthographic;
                camera.fieldOfView = oldFieldOfView;
                camera.aspect = oldAspect;
                camera.stereoTargetEye = oldstereoTargetEye;

                target.transform.localPosition = oldPosition;
                target.transform.localRotation = oldRotation;
            }
            else
            {
                tempCamera.targetTexture = null;
            }

            Object.DestroyImmediate(targetTexture);
            Object.DestroyImmediate(fx);

            timer.Stop();
            Debug.Log(string.Format("<b>[SteamVR]</b> Screenshot took {0} seconds.", timer.Elapsed));

            if (tempCamera != null)
            {
                Object.DestroyImmediate(tempCamera.gameObject);
            }

            Object.DestroyImmediate(previewTexture);
            Object.DestroyImmediate(texture);
        }

        private const string secretKey = "foobar";
        ///<summary>Bad because the secret key is here in plain text</summary>
        public static string GetBadMD5Hash(string usedString)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(usedString + secretKey);

            return GetBadMD5Hash(bytes);
        }
        public static string GetBadMD5Hash(byte[] bytes)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] hash = md5.ComputeHash(bytes);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }
        public static string GetBadMD5HashFromFile(string filePath)
        {
            if (File.Exists(filePath) == false)
                return null;

            string data = File.ReadAllText(filePath);
            return GetBadMD5Hash(data + secretKey);
        }

        public static string ConvertToForwardSlashes(string fromString)
        {
            string newString = fromString.Replace("\\\\", "\\");
            newString = newString.Replace("\\", "/");

            return newString;
        }

        public static float GetLossyScale(Transform forTransform)
        {
            float scale = 1f;
            while (forTransform != null && forTransform.parent != null)
            {
                forTransform = forTransform.parent;
                scale *= forTransform.localScale.x;
            }

            return scale;
        }

        public static bool IsValid(Vector3 vector)
        {
            return (float.IsNaN(vector.x) == false && float.IsNaN(vector.y) == false && float.IsNaN(vector.z) == false);
        }
        public static bool IsValid(Quaternion rotation)
        {
            return (float.IsNaN(rotation.x) == false && float.IsNaN(rotation.y) == false && float.IsNaN(rotation.z) == false && float.IsNaN(rotation.w) == false) &&
                (rotation.x != 0 || rotation.y != 0 || rotation.z != 0 || rotation.w != 0);
        }

        private static Dictionary<int, GameObject> velocityCache = new Dictionary<int, GameObject>();
        public static void DrawVelocity(int key, Vector3 position, Vector3 velocity, float destroyAfterSeconds = 5f)
        {
            DrawVelocity(key, position, velocity, Color.green, destroyAfterSeconds);
        }
        public static void DrawVelocity(int key, Vector3 position, Vector3 velocity, Color color, float destroyAfterSeconds = 5f)
        {
            if (velocityCache.ContainsKey(key) == false || velocityCache[key] == null)
            {
                GameObject center = GameObject.CreatePrimitive(PrimitiveType.Cube);
                center.transform.localScale = Vector3.one * 0.025f;
                center.transform.position = position;

                if (velocity != Vector3.zero)
                    center.transform.forward = velocity;

                GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arrow.transform.parent = center.transform;

                if (velocity != Vector3.zero)
                {
                    arrow.transform.localScale = new Vector3(0.25f, 0.25f, 3 + (velocity.magnitude * 1.5f));
                    arrow.transform.localPosition = new Vector3(0, 0, arrow.transform.localScale.z / 2f);
                }
                else
                {
                    arrow.transform.localScale = Vector3.one;
                    arrow.transform.localPosition = Vector3.zero;
                }
                arrow.transform.localRotation = Quaternion.identity;

                GameObject.DestroyImmediate(arrow.GetComponent<Collider>());
                GameObject.DestroyImmediate(center.GetComponent<Collider>());

                center.GetComponent<MeshRenderer>().material.color = color;
                arrow.GetComponent<MeshRenderer>().material.color = color;

                velocityCache[key] = center;

                GameObject.Destroy(center, destroyAfterSeconds);
            }
            else
            {
                GameObject center = velocityCache[key];
                center.transform.position = position;

                if (velocity != Vector3.zero)
                    center.transform.forward = velocity;

                Transform arrow = center.transform.GetChild(0);

                if (velocity != Vector3.zero)
                {
                    arrow.localScale = new Vector3(0.25f, 0.25f, 3 + (velocity.magnitude * 1.5f));
                    arrow.localPosition = new Vector3(0, 0, arrow.transform.localScale.z / 2f);
                }
                else
                {
                    arrow.localScale = Vector3.one;
                    arrow.localPosition = Vector3.zero;
                }
                arrow.localRotation = Quaternion.identity;

                GameObject.Destroy(center, destroyAfterSeconds);
            }
        }
    }
}