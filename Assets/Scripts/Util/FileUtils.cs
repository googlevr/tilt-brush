// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;

namespace TiltBrush {

static public class FileUtils {
  // Ideally, we would use DiskInfo, however this is not implemented in Mono, thus we must drop down
  // to the Win32 API.
  [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
                                                out ulong lpFreeBytesAvailable,
                                                out ulong lpTotalNumberOfBytes,
                                                out ulong lpTotalNumberOfFreeBytes);

  public const ulong MIN_DISK_SPACE_MB = 500;

  /// Returns a name of the form <dir>/<basename>___<extension>
  /// where the ___ is a potentially-empty string.
  /// The returned name is guaranteed to not exist.
  static public string GenerateNonexistentFilename(
      string directory, string basename, string extension) {
    string attempt = Path.Combine(
        directory,
        string.Format("{0}{1}", basename, extension));
    int i = -1;
    while (File.Exists(attempt) || Directory.Exists(attempt)) {
      if (i == int.MaxValue) {
        return null;
      }
      i += 1;
      attempt = Path.Combine(
          directory,
          string.Format("{0} {1}{2}", basename, i, extension));
    }
    return attempt;
  }

  static public bool CheckDiskSpaceWithError(string path, string error = null) {
    if (error == null) {
      error = "Out of disk space!";
    }
    if (!FileUtils.HasFreeSpace(path)) {
      OutputWindowScript.ReportFileSaved(error, null,
          OutputWindowScript.InfoCardSpawnPos.Brush);
      return false;
    }
    return true;
  }

  /// Returns true on success.
  /// Returns false and shows a user-visible error on failure.
  static public bool InitializeDirectoryWithUserError(
      string directoryName,
      string failureMessage=null) {
    string err = null;
    try {
      if (Directory.Exists(directoryName)) { return true; }
      Directory.CreateDirectory(directoryName);
    } catch (System.UnauthorizedAccessException e) {
      err = e.Message;
    } catch (IOException e) {
      err = e.Message;
    }

    if (failureMessage == null) {
      failureMessage = "Failed to create directory";
    }
    if (err != null) {
      OutputWindowScript.Error(failureMessage, err);
      return false;
    }
    return true;
  }

  /// Returns true on success. No user-visible message on failure
  static public bool InitializeDirectory(string directoryName) {
    try {
      if (Directory.Exists(directoryName)) { return true; }
      Directory.CreateDirectory(directoryName);
      return true;
    } catch (UnauthorizedAccessException) {
      return false;
    } catch (IOException) {
      return false;
    }
  }

  public static bool WriteBytesIgnoreExceptions(byte[] data, string path) {
    try {
      File.WriteAllBytes(path, data);
    }
    catch (DirectoryNotFoundException e) { Debug.Log(e); return false; }
    catch (IOException e) { Debug.Log(e); return false; }
    catch (UnauthorizedAccessException e) { Debug.Log(e); return false; }
    catch (System.Security.SecurityException e) { Debug.Log(e); return false; }
    return true;
  }

  // sourcePath *must not* have the file extension
  public static void WriteTextureFromResources(string sourcePath, string targetPath) {
    var file = Resources.Load<Texture2D>(sourcePath.Substring(0, sourcePath.IndexOf('.')));
    try {
      File.WriteAllBytes(targetPath, file.EncodeToPNG());
    } catch (UnauthorizedAccessException) {
      // Potentially thrown if the operation is not supported on the current platform or
      // caller does not have the required permission.
    }
  }

  public static void WriteTextFromResources(string sourcePath, string targetPath) {
    var file = Resources.Load<TextAsset>(sourcePath);
    try {
      File.WriteAllText(targetPath, file.text);
    } catch (UnauthorizedAccessException) {
        // Potentially thrown if the operation is not supported on the current platform or
        // caller does not have the required permission.
    }
  }

  public static void WriteBytesFromResources(string sourcePath, string targetPath) {
    var file = Resources.Load<TextAsset>(sourcePath);
    try {
      File.WriteAllBytes(targetPath, file.bytes);
    } catch (UnauthorizedAccessException) {
      // Potentially thrown if the operation is not supported on the current platform or
      // caller does not have the required permission.
    }
  }

  ///  Returns true the disk containing the file specified has more space than the spaceRequired.
  public static bool HasFreeSpace(string filePath, ulong spaceRequiredMb = MIN_DISK_SPACE_MB) {
    if (Application.platform == RuntimePlatform.Android) {
      AndroidJavaObject statFs = new AndroidJavaObject("android.os.StatFs",
                                                       Application.persistentDataPath);
      ulong freeBytes = (ulong)statFs.Call<long>("getAvailableBytes");
      return freeBytes / 1024L / 1024L > spaceRequiredMb;
    } else if(Application.platform == RuntimePlatform.WindowsEditor
           || Application.platform == RuntimePlatform.WindowsPlayer) {
      ulong lpFreeBytesAvailable,
            lpTotalNumberOfBytes,
            lpTotalNumberOfFreeBytes;

      // Get the drive path, C:\, etc.
      string drivePath = System.IO.Path.GetPathRoot(filePath);

      // DriveInfo is not implemented by Mono, we must resort to a platform-specific solution.
      bool success = GetDiskFreeSpaceEx(drivePath,
                                        out lpFreeBytesAvailable,
                                        out lpTotalNumberOfBytes,
                                        out lpTotalNumberOfFreeBytes);
      if (!success) {
        int error = Marshal.GetLastWin32Error();
        UnityEngine.Debug.LogException(new Exception(
          String.Format("HasFreeSpace: GetDiskFreeSpaceEx returned error code: {0}", error)));

        // Something went wrong (perhaps in our own code), make sure we don't accidentally block the
        // user.
        return true;
      }

      // Units:       Bytes       -> KB  -> MB          MB
      return lpFreeBytesAvailable / 1024 / 1024 > spaceRequiredMb;
    } else {
      // Fallback to avoid crashes
      Debug.LogWarning("HasFreeSpace is not implemented for current platform");
      return true;
    }
  }

  private static char[] m_SanitizeMap = null;

  private static char[] SanitizeMap {
    get {
      if (m_SanitizeMap == null) {
        m_SanitizeMap = new char[256];

        for (char valid = '!'; valid <= '~'; ++valid) {
          m_SanitizeMap[valid] = valid;
        }
        foreach (char invalid in Path.GetInvalidFileNameChars()) {
          m_SanitizeMap[invalid] = '\0';
        }
        m_SanitizeMap['#'] = '\0'; // Strictly speaking, valid on filenames, but Poly barfs on it.
        m_SanitizeMap[' '] = '_';
        m_SanitizeMap['/'] = '_';
        m_SanitizeMap['\\'] = '_';
      }
      return m_SanitizeMap;
    }
  }

  /// Removes any filesystem-invalid characters from a filename.
  public static string SanitizeFilename(string filename) {
    if (filename == null || filename == "") {
      return "";
    }

    byte[] asciiBytes = System.Text.Encoding.ASCII.GetBytes(filename);
    var sanitizedBytes = asciiBytes.Select(x => SanitizeMap[x]).Where(x => x != 0).ToArray();
    return new string(sanitizedBytes);
  }

  /// Removes any filesystem-invalid characters from a filename. It will attempt to retain as much
  /// of the original name as possible. It will also try to preserve the uniqueness of the original
  /// filename. Ie., two different inputs should return two different sanitized outputs.
  public static string SanitizeFilenameAndPreserveUniqueness(string filename) {
    if (filename == null || filename == "") {
      return "";
    }

    // Convert the string into hexadecimal strings where needed.
    System.Text.StringBuilder sb = new System.Text.StringBuilder();
    foreach (char c in filename) {
      if (c > 255 || SanitizeMap[(int)c] != c) {
        sb.Append('x').Append(((int)c).ToString("X"));
      } else {
        sb.Append(c);
      }
    }

    if (sb.Length > 100) {
      // This is an absurdly long file name. Truncate it and just use the hash. But preserve the
      // extension.
      string extension = Path.GetExtension(sb.ToString());
      return GetHash(sb.ToString()) + extension;
    }

    return sb.ToString();
  }

  // Generate a unique hash for a given input string.
  public static string GetHash(string text) {
    using (System.Security.Cryptography.MD5CryptoServiceProvider md5 =
        new System.Security.Cryptography.MD5CryptoServiceProvider()) {
      byte[] hashData = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
      var sb = new System.Text.StringBuilder();
      foreach (byte b in hashData) {
        sb.Append(b.ToString("X2"));
      }
      return sb.ToString();
    }
  }

  [DllImport("shell32.dll", CharSet = CharSet.Auto)]
  private static extern int SHFileOperation(ref ShellFileOpStruct operation);

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
  private struct ShellFileOpStruct {
    public IntPtr WindowHandle;
    [MarshalAs(UnmanagedType.U4)] public int FunctionCode;
    public string From;
    public string To;
    public int Flags;
    [MarshalAs(UnmanagedType.Bool)] public bool Aborted;
    public IntPtr NameMappings;
    public string ProgressBarTitle;
  }

  public static bool DeleteWithRecycleBin(string path, bool forceDelete = false) {
    if (Application.platform == RuntimePlatform.WindowsEditor ||
        Application.platform == RuntimePlatform.WindowsPlayer) {
      var operation = new ShellFileOpStruct {
          FunctionCode = 0x3, // FO_DELETE
          From = path + "\0\0",
          Flags = 0x004 | // FOF_SILENT
                  0x010 | // FOF_NOCONFIRMATION
                  0x040 | // FOF_ALLOWUNDO
                  0x400,  // FOF_NOERRORUI
      };
      bool deleted = SHFileOperation(ref operation) == 0;
      if (!deleted && forceDelete) {
        File.Delete(path);
        return true;
      }
      return deleted;
    } else {
      File.Delete(path);
      return true;
    }
  }

}  // FileUtils
}  // TiltBrush
