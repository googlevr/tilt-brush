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

using Hjg.Pngcs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace TiltBrush {

public class GifEncodeTask {
  readonly int m_GifWidth;
  readonly int m_GifHeight;
  readonly string m_GifName;
  // readonly bool m_Symmetric; Not actually needed at the moment
  readonly int m_FrameDelayMs;
  readonly bool m_bSavePngs = false;
  readonly bool m_bPalettePerFrame = false;
  readonly float m_DitherStrength;

  private List<Color32[]> m_Frames;

  private float m_CreationPercent;
  private Thread m_Thread;
  private string m_ErrorMessage;

  /// If true, the output file has been written.
  public bool IsDone { get { return !m_Thread.IsAlive; } }

  public string GifName { get { return m_GifName; } }

  /// An error message, or null if no error.
  public string Error {
    get {
      if (! IsDone) {
        throw new InvalidOperationException("Not done");
      }
      return m_ErrorMessage;
    }
  }

  /// A number in [0, 1] representing the state of the encode.
  public float CreationPercent { get { return m_CreationPercent; } }

  /// gifName: A path to the output file (either full, or relative to cwd)
  /// dither: A number from 0 to 1 (powers of 2 are best)
  /// TODO: when frameWidth == 500 and frameHeight == 500, the palettes seem to be
  /// created incorrectly.
  public GifEncodeTask(
      List<Color32[]> frames, int frameDelayMs,
      int frameWidth, int frameHeight, string gifName,
      float ditherStrength=1f/8, bool palettePerFrame = false) {
    m_DitherStrength = ditherStrength;
    // Browsers (and monitors) won't show more than 60Hz
    m_Frames = frames;
    // Gif delay is expressed in 100ths of a second
    m_FrameDelayMs = (int)Mathf.Round(frameDelayMs / 10f) * 10;
    m_GifWidth = frameWidth;
    m_GifHeight = frameHeight;
    m_GifName = gifName;
    m_bPalettePerFrame = palettePerFrame;
    // m_Symmetric = IsSymmetric(frames);

    m_CreationPercent = 0.0f;
  }

  public void Start() {
    m_Thread = new Thread(Run);
    m_Thread.Start();
  }

  private void WriteFrameAsPng(DirectoryInfo di, int i) {
    Color32[] frame = m_Frames[i];
    string filename = Path.Combine(di.FullName, string.Format("{0:00}.png", i));
    ImageInfo imi = new ImageInfo(m_GifWidth, m_GifHeight, 8, false);
    PngWriter png = FileHelper.CreatePngWriter(filename, imi, true);

    byte[] row = new byte[m_GifWidth * 3];
    for (int iRow = 0; iRow < m_GifHeight; ++iRow) {
      int iStartPixel = (m_GifHeight-1 - iRow) * m_GifWidth;
      for (int iCol = 0; iCol < m_GifWidth; ++iCol) {
        Color32 c = frame[iStartPixel + iCol];
        row[iCol*3 + 0] = c.r;
        row[iCol*3 + 1] = c.g;
        row[iCol*3 + 2] = c.b;
      }
      png.WriteRowByte(row, iRow);
    }

    png.End();
  }

  private void Run() {
    m_ErrorMessage = null;
    try {
      RunLow();
    }
    catch (IOException e) {
      m_ErrorMessage = e.Message;
    }
    catch (UnauthorizedAccessException e) {
      m_ErrorMessage = e.Message;
    }
    catch (Exception e) {
      m_ErrorMessage = "Unexpected error: " + e.Message;
    }
  }

  private void RunLow() {
    if (m_bSavePngs) {
      DirectoryInfo di;
      try {
        string dirname = m_GifName.Substring(0, m_GifName.Length-4) + "_raw";
        di = Directory.CreateDirectory(dirname);
      } catch (IOException) {
        di = null;
      }
      if (di != null) {
        for (int i = 0; i < m_Frames.Count; ++i) {
          WriteFrameAsPng(di, i);
        }
      }
    }

    Directory.CreateDirectory(Path.GetDirectoryName(m_GifName));
    // TODO: Add back gif encoding.
    // If you wish to use a gif encoder, you should add that code in here.
    // Alternatively, you could push the frames to ffmpeg and use that instead.
    /*
    using (var encoder = new Insert_Your_Own_Gif_Encoder_Here()) {
      encoder.SetEncodingParameters( ... )

      int nAdded = 0;
      int nTotal = m_Frames.Count;

      if (!m_bPalettePerFrame) {
        nTotal *= 2;

        for (int i = 0; i < m_Frames.Count; ++i) {
          encoder.AnalyzeFrameForPalette(m_Frames[i]);
          m_CreationPercent = (float)(++nAdded) / nTotal;
        }
      }

      for (int i = 0; i < m_Frames.Count; ++i) {
        encoder.AddFrame(m_Frames[i]);
        m_CreationPercent = (float)(++nAdded) / nTotal;
      }
    }
    */
  }

  // Helper function
  // Return true if frames[i] == frames[Count-i]
  static bool IsSymmetric(List<Color32[]> frames) {
    // Frame 0 would be symmetric with frames[Count], which doesn't exist.
    for (int i = 1; i <= frames.Count / 2; ++i) {
      var a = frames[i];
      var b = frames[frames.Count - i];
      if (a != b) {
        return false;
      }
    }
    return true;
  }
}

}  // namespace TiltBrush