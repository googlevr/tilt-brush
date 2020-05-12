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

/*
 * For a given texture file path like this:
 *
 *   Tilt Brush/Media Library/Images/foo.png
 *
 * this creates a cache like this:
 *
 *   Tilt Brush/Media Library/Images/Cache/foo.png/Signature.bin
 *   Tilt Brush/Media Library/Images/Cache/foo.png/Icon.bin
 *
 * The cache guarantees that the texture will be reconstructed with the same width, height, format,
 * and raw low-level texture data.
 *
 */

namespace TiltBrush {
public static class ImageCache {
  private const string kImageCacheNamespace = "Images";
  private const string kSignatureFile = "Signature.bin";
  private const string kIconFile = "Icon.bin";
  private const string kImageFile = "Image.bin";
  private const string kAspectRatioFile = "AspectRatio.bin";

  // Save a cache of the icon texture which came from filePath.
  public static void SaveIconCache(Texture2D iconTexture, string filePath, float aspectRatio) {
    SaveTextureCache(kIconFile, iconTexture, filePath, aspectRatio);
  }

  public static Texture2D LoadIconCache(string filePath, out float aspectRatio) {
    return LoadTextureCache(kIconFile, filePath, out aspectRatio);
  }

  public static void SaveImageCache(Texture2D imageTexture, string filePath) {
    SaveTextureCache(kImageFile, imageTexture, filePath);
  }

  public static Texture2D LoadImageCache(string filePath) {
    float unused;
    return LoadTextureCache(kImageFile, filePath, out unused);
  }

  public static byte[] BytesFromTexture(Texture2D texture) {
    using (var memoryStream = new MemoryStream()) {
      using (var cacheFile = new BinaryWriter(memoryStream)) {
        cacheFile.Write(texture.width);
        cacheFile.Write(texture.height);
        cacheFile.Write((int)texture.format);
        cacheFile.Write(texture.mipmapCount > 1);
        var data = texture.GetRawTextureData();
        cacheFile.Write(data.Length);
        cacheFile.Write(data);
      }
      return memoryStream.ToArray();
    }
  }

  public static Texture2D TextureFromBytes(byte[] bytes) {
    using (var memoryStream = new MemoryStream(bytes)) {
      using (var cacheFile = new BinaryReader(memoryStream)) {
        int width = cacheFile.ReadInt32();
        int height = cacheFile.ReadInt32();
        TextureFormat format = (TextureFormat)cacheFile.ReadInt32();
        bool mipmap = cacheFile.ReadBoolean();
        Texture2D texture = new Texture2D(width, height, format, mipmap);
        int dataLength = cacheFile.ReadInt32();
        var data = cacheFile.ReadBytes(dataLength);
        try {
          texture.LoadRawTextureData(data);
        } catch (UnityException e) {
          Debug.LogErrorFormat("Error creating texture from bytes: {0}", e.Message);
          return null;
        }
        texture.Apply();
        return texture;
      }
    }
  }

  public static string CacheBaseDirectory {
    get { return Path.Combine(Application.temporaryCachePath, kImageCacheNamespace); }
  }

  public static string CacheDirectory(string filePath) {
    return Path.Combine(CacheBaseDirectory, FileUtils.GetHash(filePath));
  }

  public static void DeleteObsoleteCaches() {
    string[] dirs;
    try {
      dirs = Directory.GetDirectories(CacheBaseDirectory);
    } catch (DirectoryNotFoundException) {
      // Expected
      return;
    } catch (Exception e) {
      // Not expected: IOException, PathTooLongException, UnauthorizedAccessException, ...
      Debug.LogException(e);
      return;
    }
    foreach (var cacheDirectory in dirs) {
      bool deleteThisCache = false;
      string cacheSignature = Path.Combine(cacheDirectory, kSignatureFile);
      if (File.Exists(cacheSignature)) {
        using (var fileStream = File.OpenRead(cacheSignature)) {
          using (var binaryReader = new BinaryReader(fileStream)) {
            // Originating file path should always be the first element of the cache signature.
            // Caches that don't have a valid string path at the beginning of their signature will
            // be deleted in this function.
            try {
              var originatingFileOfCache = binaryReader.ReadString();
              if (originatingFileOfCache == null || !File.Exists(originatingFileOfCache)) {
                // Cache has a valid signature but references a non-existing file.
                deleteThisCache = true;
              }
            } catch (IOException) {
              // Invalid cache, so delete.
              deleteThisCache = true;
            }
          }
        }
      } else {
        deleteThisCache = true;
      }
      if (deleteThisCache) {
        Directory.Delete(cacheDirectory, true);
      }
    }
  }

  private static byte[] GetCacheSignature(string filePath) {
    using (var memoryStream = new MemoryStream()) {
      using (var binaryWriter = new BinaryWriter(memoryStream)) {
        var fileInfo = new FileInfo(filePath);
        // Originating file path should always be the first element of the cache signature.
        binaryWriter.Write(filePath);
        binaryWriter.Write(fileInfo.Length);
        binaryWriter.Write(fileInfo.CreationTime.Ticks);
        return memoryStream.ToArray();
      }
    }
  }

  private static void SaveTextureCache(
      string cacheFileName, Texture2D texture, string filePath, float aspectRatio = -1) {
    if (texture == null) {
      return;
    }

    // Create cache directory if necessary.
    string cacheDirectory = CacheDirectory(filePath);
    if (!Directory.Exists(cacheDirectory)) {
      Directory.CreateDirectory(cacheDirectory);
    }

    // Save off file signature.
    string signatureFileName = Path.Combine(cacheDirectory, kSignatureFile);
    File.WriteAllBytes(signatureFileName, GetCacheSignature(filePath));

    // Save off the aspect ratio if one was passed in.
    if (aspectRatio != -1) {
      string aspectRatioFileName = Path.Combine(cacheDirectory, kAspectRatioFile);
      File.WriteAllBytes(aspectRatioFileName, BitConverter.GetBytes(aspectRatio));
    }

    // Save the texture.
    string cachePath = Path.Combine(cacheDirectory, cacheFileName);
    File.WriteAllBytes(cachePath, BytesFromTexture(texture));
  }

  // Tries to load an image cache, returns null on failure.
  private static Texture2D LoadTextureCache(
      string cacheFileName, string filePath, out float aspectRatio) {
    aspectRatio = 1.0f;
    string cacheDirectory = CacheDirectory(filePath);
    if (!Directory.Exists(cacheDirectory)) {
      return null;
    }

    // Check the file signature.
    byte[] cachedSignature = File.ReadAllBytes(Path.Combine(cacheDirectory, kSignatureFile));
    byte[] signatureFromSource = GetCacheSignature(filePath);
    if (cachedSignature.Length != signatureFromSource.Length) {
      return null;
    }
    for (int i = 0; i < cachedSignature.Length; i++) {
      if (cachedSignature[i] != signatureFromSource[i]) {
        return null;
      }
    }

    // Load the cache
    string cachePath = Path.Combine(cacheDirectory, cacheFileName);
    if (File.Exists(cachePath)) {
      string aspectRatioFileName = Path.Combine(cacheDirectory, kAspectRatioFile);
      aspectRatio = BitConverter.ToSingle(File.ReadAllBytes(aspectRatioFileName), 0);
      return TextureFromBytes(File.ReadAllBytes(cachePath));
    }

    // If the signature matches but the file does not exist, it could just mean
    // we haven't cached this particular part yet. (Ie., we've cached the icon
    // but not the full image.) Return null here so that this file gets cached
    // as normal.
    return null;
  }
}
} // namespace TiltBrush