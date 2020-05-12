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

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace TiltBrush {
  internal class TestImageCache {
    [Test]
    public void TestTextureToBytesRoundtrip() {
      // Get the texture.
      string imageFile = "Assets/Editor/Tests/TestData/TiltBrushLogo.jpg";
      Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imageFile);

      // Reconstruct the texture.
      byte[] textureBytes = ImageCache.BytesFromTexture(texture);
      Texture2D reconstructedTexture = ImageCache.TextureFromBytes(textureBytes);

      // Compare the textures.
      CompareTextures(texture, reconstructedTexture);
    }

    [Test]
    public void TestCachingRoundtrip() {
      // Get the texture.
      string imageFile = "Assets/Editor/Tests/TestData/TiltBrushLogo.jpg";
      Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imageFile);
      float aspectRatio = 0.75f;

      // Cache the texture.
      ImageCache.SaveIconCache(texture, imageFile, aspectRatio);

      try {
        // Retreive the cache.
        float reconstructedAspectRatio;
        Texture2D reconstructedTexture =
            ImageCache.LoadIconCache(imageFile, out reconstructedAspectRatio);
        Assert.NotNull(reconstructedTexture);

        // Compare the textures.
        CompareTextures(texture, reconstructedTexture);
        Assert.AreEqual(aspectRatio, reconstructedAspectRatio);
      } finally {
        // Clean up the test cache.
        string cacheDirectory = ImageCache.CacheDirectory(imageFile);
        Directory.Delete(cacheDirectory, true);
      }
    }

    [Test]
    public void TestDeletingObsoleteCaches() {
      // Clear out obsolete caches before testing it with a new asset.
      ImageCache.DeleteObsoleteCaches();

      // Create a temporary image.
      string imageFile = "Assets/Editor/Tests/TestData/TiltBrushLogo.jpg";
      var tempFileName = Path.GetTempFileName();
      File.Copy(imageFile, tempFileName, true);

      // Create a cache from the temporary image.
      Texture2D texture = new Texture2D(2, 2);
      texture.LoadImage(File.ReadAllBytes(tempFileName));
      ImageCache.SaveIconCache(texture, tempFileName, 1.0f);
      int cacheCountWithTempImage = Directory.GetDirectories(ImageCache.CacheBaseDirectory).Length;

      // Delete the temporary image and rely on DeleteObsoleteCache() do delete the cache.
      File.Delete(tempFileName);
      ImageCache.DeleteObsoleteCaches();
      int cacheCountWithoutTempImage =
          Directory.GetDirectories(ImageCache.CacheBaseDirectory).Length;

      // Check counts.
      Assert.AreEqual(cacheCountWithTempImage - 1, cacheCountWithoutTempImage);
    }

    // Compare the textures by checking their sizes, formats, and raw datas.
    private void CompareTextures(Texture2D textureA, Texture2D textureB) {
      Assert.AreEqual(textureA.width, textureB.width);
      Assert.AreEqual(textureA.height, textureB.height);
      Assert.AreEqual(textureA.format, textureB.format);

      byte[] textureDataA = textureA.GetRawTextureData();
      byte[] textureDataB = textureB.GetRawTextureData();
      Assert.AreEqual(textureDataA.Length, textureDataB.Length);
      for (int i = 0; i < textureDataA.Length; i++) {
        Assert.AreEqual(textureDataA[i], textureDataB[i]);
      }
    }
  }
}
