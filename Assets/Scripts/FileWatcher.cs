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

using System.IO;
using UnityEngine;

namespace TiltBrush {
  // Internal File Watcher class for abstracting between manual events and system level
  // monitoring.  On platforms that support system level file watching, events are piped
  // to an internal file watcher, and for platforms that do not, events are handled
  // manually.
  public class FileWatcher {
    private System.IO.FileSystemWatcher m_InternalFileWatcher;

    public FileWatcher(string path) {
      if (App.PlatformConfig.UseFileSystemWatcher) {
        m_InternalFileWatcher = new FileSystemWatcher(path);
        AddEventsToInternalFileWatcher();
      }
    }

    public FileWatcher(string path, string filter) {
      if (App.PlatformConfig.UseFileSystemWatcher) {
        m_InternalFileWatcher = new FileSystemWatcher(path, filter);
        AddEventsToInternalFileWatcher();
      }
    }

    void AddEventsToInternalFileWatcher() {
      m_InternalFileWatcher.Created += (sender, args) => {
        if (FileCreated != null) { FileCreated(this, args); }
      };
      m_InternalFileWatcher.Changed += (sender, args) => {
        if (FileChanged != null) { FileChanged(this, args); }
      };
      m_InternalFileWatcher.Deleted += (sender, args) => {
        if (FileDeleted != null) { FileDeleted(this, args); }
      };
    }

    // Wrap any FileSystemWatcher members that need to be accessed.
    public NotifyFilters NotifyFilter {
      get {
        if (m_InternalFileWatcher != null) {
          return m_InternalFileWatcher.NotifyFilter;
        } else {
          // This is the default combo for System.IO.FileSystemWatcher.NotifyFilter.
          return NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        }
      }
      set {
        if (m_InternalFileWatcher != null) {
          m_InternalFileWatcher.NotifyFilter = value;
        }
      }
    }

    public bool EnableRaisingEvents {
      get {
        if (m_InternalFileWatcher != null) {
          return m_InternalFileWatcher.EnableRaisingEvents;
        } else {
          return true;
        }
      }
      set {
        if (m_InternalFileWatcher != null) {
          m_InternalFileWatcher.EnableRaisingEvents = value;
        }
      }
    }

    // Events used for manual callbacks.
    public event FileSystemEventHandler FileChanged;
    public event FileSystemEventHandler FileCreated;
    public event FileSystemEventHandler FileDeleted;

    // Notifications used in Tilt Brush code.  If there's an internal file watcher active,
    // just quick exit and let it handle the events.
    public void NotifyDelete(string fullpath) {
      if (m_InternalFileWatcher != null) {
        return;
      }
      if (FileDeleted != null) {
        Debug.Assert(System.IO.Path.IsPathRooted(fullpath));
        var args = new FileSystemEventArgs(WatcherChangeTypes.Deleted,
            System.IO.Path.GetDirectoryName(fullpath), System.IO.Path.GetFileName(fullpath));
        FileDeleted(this, args);
      }
    }

    public void NotifyCreated(string fullpath) {
      if (m_InternalFileWatcher != null) {
        return;
      }
      if (FileCreated != null) {
        Debug.Assert(System.IO.Path.IsPathRooted(fullpath));
        var args = new FileSystemEventArgs(WatcherChangeTypes.Created,
            System.IO.Path.GetDirectoryName(fullpath), System.IO.Path.GetFileName(fullpath));
        FileCreated(this, args);
      }
    }

    public void NotifyChanged(string fullpath) {
      if (m_InternalFileWatcher != null) {
        return;
      }
      if (FileChanged != null) {
        Debug.Assert(System.IO.Path.IsPathRooted(fullpath));
        var args = new FileSystemEventArgs(WatcherChangeTypes.Changed,
            System.IO.Path.GetDirectoryName(fullpath), System.IO.Path.GetFileName(fullpath));
        FileChanged(this, args);
      }
    }
  }
} // namespace TiltBrush