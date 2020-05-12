using UnityEngine;
using System.Collections;

namespace Valve.VR
{
    public class SteamVR_Windows_Editor_Helper
    {
        public enum BrowserApplication
        {
            Unknown,
            InternetExplorer,
            Firefox,
            Chrome,
            Opera,
            Safari,
            Edge,
        }

        public static BrowserApplication GetDefaultBrowser()
        {
#if UNITY_EDITOR
    #if UNITY_STANDALONE_WIN
            const string userChoice = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";
            using (Microsoft.Win32.RegistryKey userChoiceKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(userChoice))
            {
                if (userChoiceKey == null)
                {
                    return BrowserApplication.Unknown;
                }

                object progIdValue = userChoiceKey.GetValue("Progid");
                if (progIdValue == null)
                {
                    return BrowserApplication.Unknown;
                }

                string browserId = progIdValue.ToString().ToLower();

                if (browserId.Contains("ie.http"))
                    return BrowserApplication.InternetExplorer;
                else if (browserId.Contains("firefox"))
                    return BrowserApplication.Firefox;
                else if (browserId.Contains("chrome"))
                    return BrowserApplication.Chrome;
                else if (browserId.Contains("opera"))
                    return BrowserApplication.Opera;
                else if (browserId.Contains("safari"))
                    return BrowserApplication.Safari;
                else if (browserId.Contains("appcq0fevzme2pys62n3e0fbqa7peapykr8v")) //AppXq0fevzme2pys62n3e0fbqa7peapykr8v
                    return BrowserApplication.Edge;
                else
                    return BrowserApplication.Unknown;
            }
    #else
            return BrowserApplication.Firefox;
    #endif
#else
            return BrowserApplication.Firefox;
#endif
        }
    }
}