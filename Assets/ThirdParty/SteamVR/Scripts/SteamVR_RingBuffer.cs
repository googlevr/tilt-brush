//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;

namespace Valve.VR
{
    public class SteamVR_RingBuffer<T>
    {
        protected T[] buffer;
        protected int currentIndex;
        protected T lastElement;

        public SteamVR_RingBuffer(int size)
        {
            buffer = new T[size];
            currentIndex = 0;
        }

        public void Add(T newElement)
        {
            buffer[currentIndex] = newElement;

            StepForward();
        }

        public virtual void StepForward()
        {
            lastElement = buffer[currentIndex];

            currentIndex++;
            if (currentIndex >= buffer.Length)
                currentIndex = 0;

            cleared = false;
        }

        public virtual T GetAtIndex(int atIndex)
        {
            if (atIndex < 0)
                atIndex += buffer.Length;

            return buffer[atIndex];
        }

        public virtual T GetLast()
        {
            return lastElement;
        }

        public virtual int GetLastIndex()
        {
            int lastIndex = currentIndex - 1;
            if (lastIndex < 0)
                lastIndex += buffer.Length;

            return lastIndex;
        }

        private bool cleared = false;
        public void Clear()
        {
            if (cleared == true)
                return;

            if (buffer == null)
                return;

            for (int index = 0; index < buffer.Length; index++)
            {
                buffer[index] = default(T);
            }

            lastElement = default(T);

            currentIndex = 0;

            cleared = true;
        }
    }

    public class SteamVR_HistoryBuffer : SteamVR_RingBuffer<SteamVR_HistoryStep>
    {
        public SteamVR_HistoryBuffer(int size) : base(size)
        {

        }

        public void Update(Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            if (buffer[currentIndex] == null)
                buffer[currentIndex] = new SteamVR_HistoryStep();

            buffer[currentIndex].position = position;
            buffer[currentIndex].rotation = rotation;
            buffer[currentIndex].velocity = velocity;
            buffer[currentIndex].angularVelocity = angularVelocity;
            buffer[currentIndex].timeInTicks = System.DateTime.Now.Ticks;

            StepForward();
        }

        public float GetVelocityMagnitudeTrend(int toIndex = -1, int fromIndex = -1)
        {
            if (toIndex == -1)
                toIndex = currentIndex - 1;

            if (toIndex < 0)
                toIndex += buffer.Length;

            if (fromIndex == -1)
                fromIndex = toIndex - 1;

            if (fromIndex < 0)
                fromIndex += buffer.Length;

            SteamVR_HistoryStep toStep = buffer[toIndex];
            SteamVR_HistoryStep fromStep = buffer[fromIndex];

            if (IsValid(toStep) && IsValid(fromStep))
            {
                return toStep.velocity.sqrMagnitude - fromStep.velocity.sqrMagnitude;
            }

            return 0;
        }

        public bool IsValid(SteamVR_HistoryStep step)
        {
            return step != null && step.timeInTicks != -1;
        }

        public int GetTopVelocity(int forFrames, int addFrames = 0)
        {
            int topFrame = currentIndex;
            float topVelocitySqr = 0;

            int currentFrame = currentIndex;

            while (forFrames > 0)
            {
                forFrames--;
                currentFrame--;

                if (currentFrame < 0)
                    currentFrame = buffer.Length - 1;

                SteamVR_HistoryStep currentStep = buffer[currentFrame];

                if (IsValid(currentStep) == false)
                    break;

                float currentSqr = buffer[currentFrame].velocity.sqrMagnitude;
                if (currentSqr > topVelocitySqr)
                {
                    topFrame = currentFrame;
                    topVelocitySqr = currentSqr;
                }
            }

            topFrame += addFrames;

            if (topFrame >= buffer.Length)
                topFrame -= buffer.Length;

            return topFrame;
        }

        public void GetAverageVelocities(out Vector3 velocity, out Vector3 angularVelocity, int forFrames, int startFrame = -1)
        {
            velocity = Vector3.zero;
            angularVelocity = Vector3.zero;

            if (startFrame == -1)
                startFrame = currentIndex - 1;

            if (startFrame < 0)
                startFrame = buffer.Length - 1;

            int endFrame = startFrame - forFrames;

            if (endFrame < 0)
                endFrame += buffer.Length;

            Vector3 totalVelocity = Vector3.zero;
            Vector3 totalAngularVelocity = Vector3.zero;
            float totalFrames = 0;
            int currentFrame = startFrame;
            while (forFrames > 0)
            {
                forFrames--;
                currentFrame--;

                if (currentFrame < 0)
                    currentFrame = buffer.Length - 1;

                SteamVR_HistoryStep currentStep = buffer[currentFrame];

                if (IsValid(currentStep) == false)
                    break;

                totalFrames++;

                totalVelocity += currentStep.velocity;
                totalAngularVelocity += currentStep.angularVelocity;
            }

            velocity = totalVelocity / totalFrames;
            angularVelocity = totalAngularVelocity / totalFrames;
        }
    }

    public class SteamVR_HistoryStep
    {
        public Vector3 position;
        public Quaternion rotation;

        public Vector3 velocity;

        public Vector3 angularVelocity;

        public long timeInTicks = -1;
    }
}