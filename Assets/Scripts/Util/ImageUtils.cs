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
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush {
public class ImageLoadError : Exception {
  public ImageLoadError(string message) : base(message) {}
  public ImageLoadError(string fmt, params System.Object[] args)
    : base(string.Format(fmt, args)) {}
  public ImageLoadError(Exception inner, string fmt, params System.Object[] args)
    : base(string.Format(fmt, args), inner) {}
}

static public class ImageUtils {
  const byte kJpegMarkerSOF0 = (byte)0xc0;
  const byte kJpegMarkerSOF2 = (byte)0xc2;

  /// Raises TiltBrush.ImageDecodeError if the data is not in a supported image format.
  /// Raises TiltBrush.ImageTooLargeError if abortSize > 0 and the image width or height
  /// are larger than abortSize.
  static public RawImage FromImageData(
      byte[] data, string filename, int abortDimension = -1) {
    if (abortDimension > 0) {
      ValidateDimensions(data, abortDimension);
    }

    if (IsJpeg(data)) {
      return FromJpeg(data, filename);
    } else if (IsPng(data)) {
      return FromPng(data, filename);
    } else {
      throw new ImageLoadError("Unknown/unsupported format");
    }
  }

  static public void ValidateDimensions(byte[] data, int maxDimension) {
    int imageWidth = 0;
    int imageHeight = 0;
    using (var stream = new MemoryStream(data)) {
      if (IsJpeg(data)) {
        bool ok = GetJpegWidthAndHeight(stream, out imageWidth, out imageHeight);
        if (!ok) {
          throw new ImageLoadError("Failed to get dimensions from jpeg");
        }
      } else if (IsPng(data)) {
        GetPngWidthAndHeight(stream, out imageWidth, out imageHeight);
      } else {
        throw new ImageLoadError("Can't get dimensions from unknown image format!");
      }
    }

    if (imageWidth > maxDimension || imageHeight > maxDimension) {
      throw new ImageLoadError(
        String.Format("Image dimensions {0}x{1} are greater than max dimensions of {2}x{2}!",
            imageWidth, imageHeight, maxDimension));
    }
  }

  /// Returns true if the data looks like it might be a jpeg
  static public bool IsJpeg(byte[] data) {
    return (
        data.Length >= 3 &&
        data[0] == 0xff && data[1] == 0xd8 && /* SOI tag */
        data[2] == 0xff /* Start of some other tag */);
  }

  // [MustUseReturnValue] -- Unity doesn't seem to contain this annotation?!
  // Should probably return (int, int)? instead
  static public bool GetJpegWidthAndHeight(Stream stream,
      out int width, out int height) {
    width = 0;
    height = 0;

    byte marker = 0;
    byte [] twoBytes = new byte[2];
    JPEGBinaryReader reader = new JPEGBinaryReader(stream);

    // Run through all the markers in the jpeg and look for the SOF, or start of frames.
    // Most .jpegs have one SOF, but some have many.
    // We're concerned with the size of this image, so take the largest of any frames we find.
    while (true) {
      switch (marker)
      {
      case kJpegMarkerSOF0:
      case kJpegMarkerSOF2:
        // Skip the frame length.
        reader.ReadInt16();
        // Bits percision.
        reader.ReadByte();
        // Scan lines (height)
        twoBytes = reader.ReadBytes(2);
        height = Mathf.Max(height, (twoBytes[0] << 8) + twoBytes[1]);
        // Scan samples per line (width)
        twoBytes = reader.ReadBytes(2);
        width = Mathf.Max(width, (twoBytes[0] << 8) + twoBytes[1]);
        return true;
      }

      try {
        marker = reader.GetNextMarker();
      } catch (System.IO.EndOfStreamException) {
        break; /* done reading the file */
      }
    }
    return false;
  }

  /// Raises TiltBrush.ImageDecodeError if the data is not a valid jpeg
  static public RawImage FromJpeg(byte[] jpegData, string filename) {
    using (var stream = new MemoryStream(jpegData)) {
      FluxJpeg.Core.Image image;
      try {
        FluxJpeg.Core.DecodedJpeg decoded =
          new FluxJpeg.Core.Decoder.JpegDecoder(stream).Decode();
        image = decoded.Image;
        image.ChangeColorSpace(FluxJpeg.Core.ColorSpace.RGB);
      } catch (Exception e) {
        // Library throws bare Exception :-P
        throw new ImageLoadError(e, "JPEG decode error");
      }

      var reds   = image.Raster[0];
      var greens = image.Raster[1];
      var blues  = image.Raster[2];
      int _width = image.Width;
      int _height = image.Height;
      var buf = new Color32[_width * _height];
      unsafe {
        unchecked {
          fixed (Color32* pBuf = buf) {
            byte* cur = (byte*)pBuf;
            for (int y = _height-1; y >= 0; --y) {
              for (int x = 0; x < _width; ++x) {
                cur[0] = reds[x, y];
                cur[1] = greens[x, y];
                cur[2] = blues[x, y];
                cur[3] = 0xff;
                cur += 4;
              }
            }
          }
        }
      }
      return new RawImage {
          ColorData = buf,
          ColorWidth = _width,
          ColorHeight = _height,
          ColorAspect = _height == 0 ? 1f : ((float)_width / _height)
      };
    }
  }

  /// Returns true if the data look slike it might be a png
  static public bool IsPng(byte[] data) {
    return (
        data.Length >= 8 &&
        data[0] == 0x89 &&
        data[1] == 'P' && data[2] == 'N' && data[3] == 'G' &&  // "PNG"
        data[4] == '\r' && data[5] == '\n' &&  // CRLF
        data[6] == 0x1a &&  // DOS EOF
        data[7] == '\n');
  }

  static public void GetPngWidthAndHeight(System.IO.Stream stream,
      out int width, out int height) {
    BinaryReader br = new BinaryReader(stream);
    // Skip signature.
    br.ReadBytes(16);

    byte[] fourBytes = new byte[4];
    fourBytes = br.ReadBytes(4);
    width = (fourBytes[0] << 24) + (fourBytes[1] << 16) +
        (fourBytes[2] << 8) + fourBytes[3];

    fourBytes = br.ReadBytes(4);
    height = (fourBytes[0] << 24) + (fourBytes[1] << 16) +
        (fourBytes[2] << 8) + fourBytes[3];
  }

  /// Raises TiltBrush.ImageDecodeError if the data is not a valid png.
  static public RawImage FromPng(byte[] pngData, string filename) {
    try {
      return _FromPng(pngData, filename);
    } catch (Exception e) {
      // There are a ton of different exceptions it can throw, and there
      // is no convenient base class
      throw new ImageLoadError(e, "PNG decode error");
    }
  }

  static RawImage _FromPng(byte[] pngData, string filename) {
    // TODO: test the untested branches
    using (var stream = new MemoryStream(pngData)) {
      var png = new Hjg.Pngcs.PngReader(stream, filename);
      png.SetUnpackedMode(true);

      int rows = png.ImgInfo.Rows;
      int cols = png.ImgInfo.Cols;
      int chans = png.ImgInfo.Channels;

      Color32[] buf = new Color32[rows * cols];

      if (png.ImgInfo.Indexed) {
        var plte = png.GetMetadata().GetPLTE();

        byte[] alphas = new byte[256];
        {
          for (int i = 0; i < 256; ++i) {
            alphas[i] = 255;
          }
          var trns = png.GetMetadata().GetTRNS();
          if (trns != null) {
            // might be smaller than 256
            int[] palette = trns.GetPalletteAlpha();
            for (int i = 0; i < palette.Length; ++i) {
              alphas[i] = (byte)palette[i];
            }
          }
        }

        byte[] line = null;
        int[] rgb = new int[3];
        for (int r = 0; r < rows; ++r) {
          line = png.ReadRowByte(line, r);
          for (int c = 0; c < cols; ++c) {
            int iEntry = line[c];
            plte.GetEntryRgb(iEntry, rgb);
            byte alpha = alphas[iEntry];
            buf[(rows - r - 1) * cols + c] = new Color32((byte)rgb[0], (byte)rgb[1], (byte)rgb[2], alpha);
          }
        }
      } else if (png.ImgInfo.Greyscale && !png.ImgInfo.Alpha) {
        Debug.Assert(chans == 1);
        Debug.Assert(png.ImgInfo.BitDepth <= 8, "Unsupported: 16-bit grey");

        byte[] line = null;
        for (int r = 0; r < rows; ++r) {
          line = png.ReadRowByte(line, r);
          for (int c = 0; c < cols; ++c) {
            buf[(rows - r - 1) * cols + c] = new Color32(line[c], line[c], line[c], 255);
          }
        }
      } else if (png.ImgInfo.Greyscale && png.ImgInfo.Alpha) {
        Debug.Assert(chans == 2);
        Debug.Assert(png.ImgInfo.BitDepth <= 8, "Unsupported: 16-bit grey");
        Debug.Assert(false, "currently unsupported: grayscale alpha");

        byte[] line = null;
        for (int r = 0; r < rows; ++r) {
          line = png.ReadRowByte(line, r);
          for (int c = 0; c < cols; ++c) {
            int i = c * chans;
            buf[(rows - r - 1) * cols + c] = new Color32(line[i], line[i], line[i], line[i + 1]);
          }
        }
      } else if (chans == 3 || chans == 4) {
        // Can use ReadRowByte() if bitDepth <= 8
        if (png.ImgInfo.BitDepth <= 8) {
          byte[] line = null;
          for (int r = 0; r < rows; ++r) {
            line = png.ReadRowByte(line, r);
            for (int c = 0; c < cols; ++c) {
              int ichan = c * chans;
              buf[(rows - r - 1) * cols + c] = new Color32(
                  line[ichan], line[ichan+1], line[ichan+2],
                  (chans == 3) ? (byte)0xff : line[ichan+3]);
            }
          }
        } else {
          Debug.Assert(png.ImgInfo.BitDepth == 16);
          Debug.Assert(false, "Untested: 16-bit rgb");
          var lines = png.ReadRowsInt(0, png.ImgInfo.Rows, 1);
          for (int r = 0; r < rows; ++r) {
            int[] line = lines.Scanlines[r];
            for (int c = 0; c < cols; ++c) {
              int ichan = c * chans;
              buf[(rows - r - 1) * cols + c] = new Color32(
                  (byte)line[ichan], (byte)line[ichan+1], (byte)line[ichan+2],
                  (byte)((chans == 3) ? 0xff : line[ichan+3]));
            }
          }
        }
      } else {
        Debug.Assert(false, "Weird format");
      }

      return new RawImage {
          ColorData = buf,
          ColorWidth = cols,
          ColorHeight = rows,
          ColorAspect = (rows == 0) ? 1f : ((float)cols / rows)
      };
    }
  }

  /// Fetches the url and returns a Texture2D or null.
  public static async Task<Texture2D> DownloadTextureAsync(string uri) {
    using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(uri)) {
      await www.SendWebRequest();
      // If we don't do this, downloadHandler.data sometimes (always?) returns garbage
      while (!www.downloadHandler.isDone) {
        await Awaiters.NextFrame;
      }

      if (www.isNetworkError || www.responseCode >= 400) {
        Debug.LogErrorFormat("ImageUtils: Error downloading {0}, error {1}", uri, www.responseCode);
        return null;
      }


      // Try LoadImage first, because it's faster
      {
        Texture2D dest = new Texture2D(2, 2);
        if (dest.LoadImage(www.downloadHandler.data)) {
          if (dest.width == 8) {
            // Detect "false success" from LoadImage -- if we see this at all, we should
            // consider falling through to ThreadedImageReader?
            Debug.LogError("Got 8x8 from LoadImage!");
          }
          return dest;
        } else {
          Debug.LogError("DownloadTextureAsync: LoadImage failed");  // Unexpected
        }
        UnityEngine.Object.Destroy(dest);
      }

      // This case probably won't get hit any more
      {
        RawImage im;
        try {
          im = await new ThreadedImageReader(www.downloadHandler.data, uri);
        } catch (ImageLoadError e) {
          Console.WriteLine($"ImageUtils: cannot hand-create {uri}: {e}");
          im = null;
        }
        if (im != null) {
          Console.WriteLine($"ImageUtils: hand-creating {uri}: {www.isDone}");
          Texture2D dest = new Texture2D(im.ColorWidth, im.ColorHeight, TextureFormat.RGBA32, true);
          dest.SetPixels32(im.ColorData);
          dest.Apply();
          Console.WriteLine("ImageUtils: hand-created icon dimensions: " +
                            $"{dest.width}, {dest.height}");
          return dest;
        }
      }

      // b/37256058: Unity's DownloadHandlerTexture is buggy in 5.4.4 and returns Textures
      // which hard-crash Unity (for some URLs, sometimes).
      // That bug may be fixed by now, but since it doesn't show up in analytics and is
      // quite severeTry creating the texture ourselves.
      // Fall through to doing it the old, maybe b/37256058 buggy way.
      // This case should get hit even less than the previous case.
      {
        Console.WriteLine("ImageUtils: downloaded and handled {0}: {1} {2}",
                          uri, www.isDone, www.downloadHandler.isDone);
        // b/62269743: sometimes this returns an ugly 8x8 texture.
        Texture2D dest = DownloadHandlerTexture.GetContent(www);
        Console.WriteLine($"ImageUtils: icon dimensions: {dest.width}, {dest.height}");
        return dest;
      }
    }
  }
}  // ImageUtils
}  // TiltBrush

