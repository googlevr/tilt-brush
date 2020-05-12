using UnityEngine;
using System.Collections;

namespace Valve.VR.Extras
{
    /// <summary>
    /// This is an example class of how to force steamvr initialization. You still need to have vr mode enabled 
    /// but you can have the top sdk set to None, then this script will force it to OpenVR after a second
    /// </summary>
    public class SteamVR_ForceSteamVRMode : MonoBehaviour
    {
        public GameObject vrCameraPrefab;

        public GameObject[] disableObjectsOnLoad;

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(1f); // just here to show that you can wait a while.
            
            SteamVR.Initialize(true);

            while (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
                yield return null;

            for (int disableIndex = 0; disableIndex < disableObjectsOnLoad.Length; disableIndex++)
            {
                GameObject toDisable = disableObjectsOnLoad[disableIndex];
                if (toDisable != null)
                    toDisable.SetActive(false);
            }

            GameObject.Instantiate(vrCameraPrefab);
        }
    }
}