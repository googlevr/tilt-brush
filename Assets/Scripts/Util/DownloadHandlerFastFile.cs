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

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush {
/// This is a replacement for Unity's DownloadHandlerFile class. DownloadHandlerFile is designed to
/// stream downloaded files directly to disk, but on 2017.4 at least, it seems to have a really
/// small disk buffer, so downloads take *ages*.
/// With this download handler, you can pass through the buffer you wish to use (which determines
/// the maximum that can be downloaded every frame).
public class DownloadHandlerFastFile : DownloadHandlerScript {
  private FileStream m_File;
  private string m_Filename;

  public DownloadHandlerFastFile(string filename, byte[] buffer)  : base(buffer) {
    m_Filename = filename;
    m_File = File.Create(filename);
  }

  protected override void CompleteContent() {
    m_File?.Close();
  }

  protected override bool ReceiveData(byte[] data, int dataLength) {
    try {
      m_File?.Write(data, 0, dataLength);
    } catch (Exception ex) {
      if (ex is IOException) {
        Debug.unityLogger.Log(LogType.Exception,
            $"Info: Cannot write file to disk ({ex.GetType()}), probably out of disk space.\n" +
            $"{ex.Message}");
      } else {
        Debug.LogException(ex);
      }
      if (m_File != null) {
        m_File.Close();
        File.Delete(m_Filename);
      }
      return false;
    }
    return true;
  }
}
} // namespace TiltBrush
