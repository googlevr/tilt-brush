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

using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TiltBrush {

internal class TestFileUtils {
  private static string sm_TestDirectory;

  private static void CreateTestDirectory() {
    if (sm_TestDirectory == null) {
       sm_TestDirectory = Path.Combine(Path.GetTempPath(), "TiltBrushUnitTests");
    }

    if (Directory.Exists(sm_TestDirectory)) {
      Directory.Delete(sm_TestDirectory, true);
    }
    string failureMessage = string.Format("Can't create unit test directory: {0}", sm_TestDirectory);
    bool dirCreated = FileUtils.InitializeDirectoryWithUserError(sm_TestDirectory, failureMessage);
    if (!dirCreated) {
      throw new VrAssetServiceException(failureMessage);
    }
  }

  private static void DeleteTestDirectory() {
    Directory.Delete(sm_TestDirectory, true);
  }

  // Test that random filenames become sanitized.
  [Test]
  public void TestFilenameSanitization() {
    CreateTestDirectory();

    // Start with known difficult cases.
    List<string> testFilenames = new List<string>(new string[] {
        "!@#$%^&*()_+-=?.,\":;'/",
        "C:\\absolute\\path",
        "relative/path",
        "http://google.com/",
    });

    // Add random test cases.
    for (int i = 0; i < 100; i++) {
      // Create random filenames of increasing length.
      string filename = "";
      for (int j = 0; j < 3 * i; j++) {
        filename += char.ToString((char)(Random.value * char.MaxValue));
      }
      filename += ".txt";
      testFilenames.Add(filename);
    }

    // Test the sanitization.
    foreach (string filename in testFilenames) {
      string sanitizedFilename = FileUtils.SanitizeFilename(filename);
      try {
        File.WriteAllText(Path.Combine(sm_TestDirectory, sanitizedFilename), "test");
      } catch (System.Exception e) {
        Assert.Fail("\"{0}\" > \"{1}\" not sanitized, {2}.", filename, sanitizedFilename, e.Message);
      }
    }

    DeleteTestDirectory();
  }

  [Test]
  public void TestSanitizedFilenameUniqueness() {
    CreateTestDirectory();

    string[] similarFilenames = {
        "foobar.txt",
        "barfoo.txt",
        "foo_bar.txt",
        "foo/bar.txt",
        "foo\\bar.txt",
        "フーバー.txt",
        "バーフー.txt",
        "フー_バー.txt",
        "フー/バー.txt",
        "フー\\バー.txt",
    };

    Dictionary<string, string> mapFromSanitizedToOriginal = new Dictionary<string, string>();

    foreach (string filename in similarFilenames) {
      string sanitizedFilename = FileUtils.SanitizeFilenameAndPreserveUniqueness(filename);

      // Test the sanitization.
      try {
        File.WriteAllText(Path.Combine(sm_TestDirectory, sanitizedFilename), "test");
      } catch (System.Exception e) {
        Assert.Fail("\"{0}\" > \"{1}\" not sanitized, {2}.", filename, sanitizedFilename, e.Message);
      }

      // Test uniqueness.
      Debug.LogFormat("{0} maps to {1}", filename, sanitizedFilename);
      if (mapFromSanitizedToOriginal.ContainsKey(sanitizedFilename)) {
        Assert.Fail("\"{0}\" and \"{1}\" both sanitize to \"{2}\".",
                    filename,
                    mapFromSanitizedToOriginal[sanitizedFilename],
                    sanitizedFilename);
      } else {
        mapFromSanitizedToOriginal.Add(sanitizedFilename, filename);
      }
    }

    DeleteTestDirectory();
  }
}

}  // namespace TiltBrush
