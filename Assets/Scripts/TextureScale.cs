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

// Only works on ARGB32, RGB24 and Alpha8 textures that are marked readable

using System;
using System.Threading;
using UnityEngine;

namespace TiltBrush {

public class TextureScale {
  public class SharedData {
    // mutable
    public int finishCount;
    // read only
    public Mutex mutex;
    public Color32[] newColors;
    public Color32[] texColors;
    public int texWidth;
    public int newWidth;
    public float ratioX;
    public float ratioY;

  }
  public class ThreadData {
    // read only
    public SharedData shared;
    public int start;
    public int end;
  }

  /// Like Bilinear(), but with an easier interface.
  /// Blocking, runs on current thread.
  /// newColors may be null.
  /// Return value may reuse texColors or newColors (or neither!)
  public static Color32[] BlockingBilinear(
      Color32[] texColors, int texWidth, int texHeight,
      Color32[] newColors, int newWidth, int newHeight) {
    if (texWidth == newWidth && texHeight == newHeight) {
      return texColors;
    }
    if (newColors == null) {
      newColors = new Color32[newWidth * newHeight];
    }

    Bilinear(texColors, texWidth, texHeight,
        newColors, newWidth, newHeight);
    return newColors;
  }

  // Helper for Bilinear()
  // Returns a value up to 1 unit closer to desired (in log2 space)
  static int moveTowardsLog(int start, int desired) {
    if (start * desired <= 0) {
      throw new System.ArgumentException("Signs must be the same; 0 not allowed.");
    }
    // Want the non-multiple of two to be at the upper end of the progression,
    // for better filtering. But this is easier to write.
    if (desired < start) {
      int temp = start >> 1;
      return (temp < desired ? desired : temp);
    } else {
      long temp = start << 1;
      return (temp > desired ? desired : (int)temp);
    }
  }

  // Helper to deal with manual buffer management.
  // The repeated bilinear filtering seems to be causing excessive garbage
  // to stick around.
  unsafe struct ColorBuffer {
    IntPtr dealloc;             // Pointer to deallocate, or 0 if not explicitly allocated
    public Color32* array;
    public int length;          // number of elements in array
    public int width, height;   // 2D size (product must be <= length)

    // no allocation; pointer comes from a []
    public ColorBuffer(Color32[] c, Color32* pc, int width_, int height_) {
      width = width_;
      height = height_;
      length = c.Length;
      dealloc = IntPtr.Zero;
      array = pc;
    }

    // allocate non-garbage-collected memory
    public ColorBuffer(int width_, int height_) {
      width = width_;
      height = height_;
      length = width * height;
      dealloc = System.Runtime.InteropServices.Marshal.AllocHGlobal(length * sizeof(Color32));
      array = (Color32*)dealloc;
    }

    public void Deallocate() {
      if (dealloc != IntPtr.Zero) {
        System.Runtime.InteropServices.Marshal.FreeHGlobal(dealloc);
        dealloc = IntPtr.Zero;
        array = null;
        length = width = height = 0;
      }
    }
  };

  /// Simplified to be single-threaded and blocking
  private unsafe static void Bilinear(
      Color32[] texColors, int texWidth, int texHeight,
      Color32[] newColors, int newWidth, int newHeight) {
    // A single pass of bilinear filtering blends 4 pixels together,
    // so don't try to reduce by more than a factor of 2 per iteration
    Debug.Assert(newWidth * newHeight == newColors.Length);

    fixed (Color32* pTex = texColors) {
      ColorBuffer cur = new ColorBuffer(texColors, pTex, texWidth, texHeight);
      while (true) {
        int tmpWidth = moveTowardsLog(cur.width, newWidth);
        int tmpHeight = moveTowardsLog(cur.height, newHeight);
        if (newColors.Length == tmpWidth * tmpHeight) {
          fixed (Color32* pNew = newColors) {
            SinglePassBilinear(cur.array, cur.length, cur.width, cur.height,
                               pNew, newColors.Length, newWidth, newHeight);
          }
          return;
        }

        ColorBuffer tmp = new ColorBuffer(tmpWidth, tmpHeight);
        SinglePassBilinear(cur.array, cur.length, cur.width, cur.height,
                           tmp.array, tmp.length, tmp.width, tmp.height);
        cur.Deallocate();
        cur = tmp;
      }
    }
  }

  /// Unthreaded single pass bilinear filter
  private static unsafe void SinglePassBilinear(
      Color32* texColors, int texLen, int texWidth, int texHeight,
      Color32* newColors, int newLen, int newWidth, int newHeight) {
    if (newLen < newWidth * newHeight) {
      Debug.Assert(newLen >= newWidth * newHeight);
      return;
    }

#if false
    // For reference, this is the point-sampled loop
    float ratioX = ((float)texWidth) / newWidth;
    float ratioY = ((float)texHeight) / newHeight;
    for (var y = start; y < end; y++) {
      var thisY = (int)(ratioY * y) * w;
      var yw = y * w2;
      for (var x = 0; x < w2; x++) {
        newColors[yw + x] = texColors[(int)(thisY + ratioX * x)];
      }
    }
#endif

    float ratioX = 1.0f / ((float)newWidth / (texWidth - 1));
    float ratioY = 1.0f / ((float)newHeight / (texHeight - 1));
    {
      var w = texWidth;
      var w2 = newWidth;

      for (int y = 0; y < newHeight; y++) {
        int yFloor = (int)Mathf.Floor(y * ratioY);
        var y1 = yFloor * w;
        var y2 = (yFloor + 1) * w;
        var yw = y * w2;

        for (int x = 0; x < w2; x++) {
          int xFloor = (int)Mathf.Floor(x * ratioX);
          var xLerp = x * ratioX - xFloor;
          newColors[yw + x] = ColorLerpUnclamped(
              ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp),
              ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp),
              y * ratioY - yFloor);
        }
      }
    }
  }

  //
  // Safe Compress will only compress pow of 2 textures
  // Non pow 2 textures get extremely strange mipmap artifacts when compressed
  // If we run into memory issues here, we may need to convert all incoming textures textures to pow of 2
  // But if we do that we need to save the image aspect ratio before compression for reference Images
  //
  public static bool SafeCompress(Texture2D tex, bool highQuality = false) {
    if (Mathf.IsPowerOfTwo(tex.width) && Mathf.IsPowerOfTwo(tex.height)) {
      tex.Compress(highQuality);
      return true;
    } else {
      return false;
    }
  }

  private static Color32 ColorLerpUnclamped(Color32 c1, Color32 c2, float value) {
    return new Color32(
        (byte)(c1.r + (c2.r - c1.r) * value),
        (byte)(c1.g + (c2.g - c1.g) * value),
        (byte)(c1.b + (c2.b - c1.b) * value),
        (byte)(c1.a + (c2.a - c1.a) * value));
  }
}
}  // namespace TiltBrush
