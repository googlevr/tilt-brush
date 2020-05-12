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
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TiltBrush {

abstract class Bytable {
  abstract public int Length { get; }
  abstract public void ToBytes(ref byte[] bytes);
  abstract public void FromBytes(byte[] buf);
  abstract public System.Array GetArray();
}

class Color32Bytable : Bytable {
  private Color32[] m_buffer;

  public Color32Bytable(Color32[] buffer) {
    m_buffer = buffer;
  }

  public override int Length {
    get { return m_buffer.Length; }
  }

  public override System.Array GetArray() {
    return m_buffer;
  }

  public override void ToBytes(ref byte[] buf) {
    unsafe {
      fixed (Color32* p = m_buffer) {
        int eltSize = sizeof(Color32);
        if (buf == null || buf.Length != eltSize * m_buffer.Length) {
          // We only reallocate here when the buffer size does not match.
          buf = new byte[m_buffer.Length * eltSize];
        }
        // Also see this thread for discussion of conversion options:
        // http://stackoverflow.com/questions/619041/what-is-the-fastest-way-to-convert-a-float-to-a-byte/3577253#3577253
        System.Runtime.InteropServices.Marshal.Copy((System.IntPtr)p, buf, 0, buf.Length);
      }
    }
  }
  public override void FromBytes(byte[] buf) {
    long eltSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Color32));
    if (m_buffer == null || m_buffer.Length != buf.Length / eltSize) {
      m_buffer = new Color32[buf.Length / eltSize];
    }
    unsafe {
      fixed (Color32* p = m_buffer) {
        // Also see this thread for discussion of conversion options:
        // http://stackoverflow.com/questions/619041/what-is-the-fastest-way-to-convert-a-float-to-a-byte/3577253#3577253
        System.Runtime.InteropServices.Marshal.Copy(buf, 0, (System.IntPtr)p, buf.Length);
      }
    }
  }
}

class FloatBytable : Bytable {
  private float[] m_buffer;

  public FloatBytable(float[] buffer) {
    m_buffer = buffer;
  }

  public override int Length {
    get { return m_buffer.Length; }
  }

  public override System.Array GetArray() {
    return m_buffer;
  }

  public override void ToBytes(ref byte[] buf) {
    int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(float)) * m_buffer.Length;

    // We only reallocate here when the buffer size does not match.
    if (buf == null || buf.Length != size) {
      buf = new byte[size];
    }

    // Buffer.BlockCopy is slightly faster (in a micro-benchmark) than the unsafe pointer copy, but
    // can only be used for primitive types.
    System.Buffer.BlockCopy(m_buffer, 0, buf, 0, size);
  }

  public override void FromBytes(byte[] buf) {
    // We only reallocate here when the buffer size does not match.
    int size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(float)) / buf.Length;
    if (m_buffer == null || m_buffer.Length != size) {
      m_buffer = new float[size];
    }
    System.Buffer.BlockCopy(buf, 0, m_buffer, 0, buf.Length);
  }
}


/// A helper class to manage async communication with external process.
///
class FfmpegPipe {
  public const string kFfmpegDir = "Support/ThirdParty/ffmpeg";

  public static string GetFfmpegExe() {
    string exe = $"{kFfmpegDir}/bin/ffmpeg.exe";
    if (File.Exists(exe)) { return exe; }
    return null;
  }

  private System.Diagnostics.Process m_encoderProc;

  private Thread m_dataWriterThread;
  private Thread m_dataReaderThread;
  private Thread m_logReaderThread;
  private bool m_shouldExit;
  private bool m_shouldPause;

  private System.IO.StreamReader m_stderr;
  private System.IO.BinaryWriter m_dataWriter;
  private System.IO.BinaryReader m_dataReader;

  // The number of frames actually sent to FFMPEG.
  private int m_frameCount;

  // This WaitHandleEvent is used to pause threads when video capture is disabled, to avoid
  // burning CPU cycles.
  private ManualResetEvent m_ready;

  // This WaitHandleEvent is used to put threads to sleep for a specific timeout.
  // Note that Sleep() is inappropriate because the actual sleep time is nondeterministic.
  // It is only set in Stop().
  private ManualResetEvent m_sleeper;

  // Used to indicate that a frame is ready for encoding.
  private AutoResetEvent m_frameReady;

  // When blocking encoding is enabled, is used to stall the calling thread while encoding of the
  // previous frame completes.
  private ManualResetEvent m_frameWritten;

  // Why Bytable instead of Array? The Bytable interface lets us use unsafe pointer -> byte[]
  // conversion, which cannot be done in a "generics" way. Also, fast array -> byte[] conversions
  // (e.g. Buffer.BlockCopy) won't work with non-primitive types. Therefore, we need a special
  // abstraction to allow us to convert between T[] and byte[].
  //
  // In practice T is either Color32 or float.

  // A single frame of raw audio or video.
  private Bytable m_frame;
  // A ring buffer of potentially many audio or video frames, used for playback.
  RingBuffer<Bytable> m_framesOut;

  private bool m_shouldBlock;

  // TODO: It would be nice to exclude height/width from this class, since they don't apply
  // to audio.
  int m_height;
  int m_width;
  string m_outputFile;

  private int Height {
    get { return m_height; }
  }

  private int Width {
    get { return m_width; }
  }

  public int FrameCount {
    get { return m_frameCount; }
  }

  public string OutputFile {
    get { return m_outputFile; }
    set { m_outputFile = value; }
  }

  public bool IsReadyForInput {
    get {
      // This is not atomic, so m_frame can become null after the null check.
      // Using a local variable solves this problem.
      Bytable b = m_frame;
      return b == null || b.Length == 0;
    }
  }

  public bool DidExit {
    get { return m_dataReaderThread == null || !m_dataReaderThread.IsAlive; }
  }

  public System.Action<Bytable> ReleaseFrame { get; set; }

  public FfmpegPipe() {
    // We're relying on garbage collection to clean these objects up, in the worst case, we should
    // only have exactly two of each of these objects in memory (since we run garbage collection
    // during video capture).
    m_ready = new ManualResetEvent(false);
    m_sleeper = new ManualResetEvent(false);
    m_frameReady = new AutoResetEvent(false);
    m_frameWritten = new ManualResetEvent(true);
  }

  // Returns false if ffmpeg fails to start.
  // Note that sampleRate is FPS for video and sample rate in Hz for audio
  public bool Start(string source, string outputFile, int width, int height, float sampleRate,
                    bool blocking) {
    m_outputFile = "";
    if (!LaunchEncoder(source, outputFile, width, height, sampleRate)) {
      // ffmpeg failed to launch
      return false;
    }

    m_outputFile = outputFile;
    m_height = height;
    m_width = width;
    m_shouldBlock = blocking;

    m_frameCount = 0;

    m_shouldExit = false;
    m_shouldPause = true;

    m_frame = null;
    m_stderr = m_encoderProc.StandardError;

    // Start status reader
    m_logReaderThread = new Thread(ReadFfmpegOutput);
    m_logReaderThread.IsBackground = true;
    m_logReaderThread.Start();

    if (source.StartsWith("pipe:")) {
      m_dataWriter = new BinaryWriter(m_encoderProc.StandardInput.BaseStream);
      m_dataReader = null;
      // Start frame writer
      m_dataWriterThread = new Thread(WriteFramesToFfmpeg);
      m_dataWriterThread.IsBackground = true;
      m_dataWriterThread.Start();
    } else {
      m_dataWriter = null;
      m_dataReader = new BinaryReader(m_encoderProc.StandardOutput.BaseStream);
      m_dataReaderThread = new Thread(ReadFramesFromFfmpeg);
      m_dataReaderThread.IsBackground = true;
      m_dataReaderThread.Start();
      m_framesOut = new RingBuffer<Bytable>(5);
    }

    m_shouldPause = false;
    m_ready.Set();

    return true;
  }

  public static string GetVideoEncoder() {
    string friendlyName = App.UserConfig.Video.Encoder;
    switch (friendlyName.ToLower()) {
      case "h.264":
      default:
        return "libx264 -preset faster -crf 23";
    }
  }

  private bool LaunchEncoder(string inputFile,
                             string outputFile,
                             int width,
                             int height,
                             float sampleRate) {
    string streamInput = @"-y -r {4} -f rawvideo -codec rawvideo -s {0}x{1} " +
                               @"-pixel_format rgba -i {2} ";

    string outputArgs = @"-vf ""transpose=3,transpose=1"" ";

    // Helpful references:
    //  * https://trac.ffmpeg.org/wiki/Encode/H.264
    //  * https://trac.ffmpeg.org/wiki/Encode/YouTube
    string streamOutput = @"-r {4} -threads 8 -c:v " + GetVideoEncoder() + " -pix_fmt yuv420p " +
                        @" ""{3}""";

    bool isReading = false;
    if (outputFile.EndsWith("m4a")) {
      outputArgs = "";
      streamInput = @"-y -f f32le -acodec pcm_f32le -ar {4} -ac 2 -i {2} ";
      streamOutput = @"-ar {4} -c:a aac ""{3}""";
    } else if (outputFile.StartsWith("pipe:")) {
      streamInput = @"-i ""{2}"" ";
      streamOutput = @"-an -f rawvideo -pix_fmt rgba ""{3}""";
      isReading = true;
    }


    string ffmpegExe = GetFfmpegExe();
    if (ffmpegExe == null) {
      UnityEngine.Debug.LogError("ffmpeg.exe could not be found.");
      return false;
    }
    m_encoderProc = new Process();

    m_encoderProc.StartInfo.FileName = Path.GetFullPath(ffmpegExe);
    m_encoderProc.StartInfo.Arguments = System.String.Format(streamInput+outputArgs+streamOutput,
                                                    width, height, inputFile, outputFile, sampleRate);

    m_encoderProc.StartInfo.CreateNoWindow = true;
    m_encoderProc.StartInfo.UseShellExecute = false;
    m_encoderProc.StartInfo.RedirectStandardInput = !isReading; // true when writing to ffmpeg
    m_encoderProc.StartInfo.RedirectStandardOutput = isReading; // true when reading from ffmpeg
    m_encoderProc.StartInfo.RedirectStandardError = true;

    System.Console.WriteLine("Opening FFMPEG pipe: {0} {1}",
                             ffmpegExe,
                             m_encoderProc.StartInfo.Arguments);

    try {
      if (!m_encoderProc.Start()) {
        UnityEngine.Debug.LogWarningFormat("Start returned false {0}", inputFile);
        return false;
      }
    } catch (System.Exception e) {
      UnityEngine.Debug.LogException(e);
      return false;
    }

    if (m_encoderProc.HasExited) {
      string err = m_encoderProc.StandardError.ReadToEnd();
      UnityEngine.Debug.LogError(err);
      return false;
    }

    return true;
  }

  public static bool Mux(string audioFile, string videoFile, string outputFile) {
    string ffmpegExe = GetFfmpegExe();
    if (ffmpegExe == null) { return false; }
    Process p = new Process();

    p.StartInfo.FileName = Path.GetFullPath(ffmpegExe);
    p.StartInfo.Arguments = string.Format("-y -i \"{0}\" -i \"{1}\" -c:v copy -c:a copy \"{2}\"",
                              audioFile, videoFile, outputFile);

    p.StartInfo.CreateNoWindow = true;
    p.StartInfo.UseShellExecute = false;

    System.Console.WriteLine("Opening FFMPEG Merge: {0} {1}",
                             ffmpegExe,
                             p.StartInfo.Arguments);

    try {
      p.Start();
    } catch (System.Exception e) {
      UnityEngine.Debug.LogException(e);
      return false;
    }

    if (!p.WaitForExit(60 * 1000)) {
      p.Kill();
      UnityEngine.Debug.LogWarning("Killed FFMPEG Merge after timeout");
      return false;
    }

    return true;
  }

  public void Stop() {
    m_ready.Reset();

    // Setup the threads to exit naturally.
    m_shouldPause = true;
    m_shouldExit = true;

    // Signal the threads to exit at the next clean exit point.
    if (m_logReaderThread != null) {
      m_logReaderThread.Interrupt();
    }
    if (m_dataWriterThread != null) {
      m_dataWriterThread.Interrupt();
    }
    if (m_dataReaderThread != null) {
      m_dataReaderThread.Interrupt();
    }

    // Resource cleanup.
    m_dataWriterThread = null;
    m_dataReaderThread = null;
    m_logReaderThread = null;

    // We rely on garbage collection to clean up m_sleeper and m_ready.

    // Note that the threads keep their own local references of these objects, so setting the global
    // reference to null is safe and allows Start() to be called immediately after Stop(), even if
    // the old worker threads have not yet exited.
    m_stderr = null;
    m_dataWriter = null;
    m_dataReader = null;
  }

  public bool WaitForEncoderExit(int milliseconds) {
    if (m_encoderProc == null) {
      return true;
    }
    if (m_encoderProc.HasExited) {
      return true;
    }
    if (!m_encoderProc.WaitForExit(milliseconds)) {
      m_encoderProc.Kill();
      return false;
    }
    return true;
  }

  /// Queues a buffer to the encoder and returns the previously queued buffer for reuse.
  public Bytable QueueFrame(Bytable buffer) {
    // Hand off to writer thread.
    // We may drop frames here if we don't write them fast enough, unless we have chosen to block.
    // Explanation of the blocking behavior follows:
    // What happens here is that m_frameWritten will not block the first time through. While the
    // frame is being encoded, m_frameWritten is reset, so that if we get here again before the
    // frame is done with, it will block until the frame has finished. In this way it will not
    // queue a frame when one is encoding, but once it has queued one, it will return to do
    // other things while that goes on in the background.
    if (m_shouldBlock) {
      // Assumption here is that it shouldn't take longer than five seconds to encode a single
      // frame. If it takes longer we'll just start using up the buffer.
      m_frameWritten.WaitOne(5 * 1000);
    }
    Interlocked.Exchange(ref m_frame, buffer);
    m_frameReady.Set();
    return buffer;
  }

  public void GetFrame(ref Color32Bytable buffer) {
    if (m_framesOut.IsEmpty) {
      buffer = null;
    } else {
      // The writer thread will never touch m_framesOut[0], therefore this should be thread safe.
      // This is true as long as the writer never calls Enqueue with overwriteIfFull=true.
      buffer = m_framesOut.Dequeue() as Color32Bytable;
    }

    m_frameReady.Set();
  }

  private void ReadFfmpegOutput() {
    StringWriter output = new StringWriter();
    System.IO.StreamReader stderrLocal = m_stderr;

    try {
      char[] errorBuff = new char[4 * 1024];

      while (!m_shouldExit) {
        while (!m_shouldPause) {

          int numBytes = stderrLocal.Read(errorBuff, 0, errorBuff.Length);

          if (numBytes == 0) {
            // End of stream, exit.
            return;
          }

          if (numBytes > 0) {
            output.Write(errorBuff, 0, numBytes);
          }

          // Thread.Sleep() is non-deterministic, a WaitHandleEvent is the preferred way to sleep
          // for a specific amount of time.
          m_sleeper.WaitOne(1);
        }

        // Free up this core while there is no input.
        m_ready.WaitOne();
      }

      UnityEngine.Debug.Log(output.ToString());
      stderrLocal.Close();

    } catch (System.Threading.ThreadInterruptedException) {
      // This is fine, the render thread sent an interrupt.
      UnityEngine.Debug.Log(output.ToString());
      stderrLocal.Close();
    } catch (System.Exception e) {
      UnityEngine.Debug.LogWarning(m_outputFile);
      UnityEngine.Debug.LogException(e);
    }
  }

  private void WriteFramesToFfmpeg() {
    Bytable curFrame = null;
    byte[] buf = null;
    System.IO.BinaryWriter dataPipe = m_dataWriter;

    try {
      while (!m_shouldExit) {
        while (!m_shouldPause) {
          // Wait for the next frame to arrive.
          m_frameReady.WaitOne();
          m_frameWritten.Reset();

          // Grab the frame.
          curFrame = Interlocked.Exchange(ref m_frame, curFrame);

          if (curFrame == null || curFrame.Length == 0) {
            // This really shouldn't happen.
            UnityEngine.Debug.LogWarning("FfmpegPipe recieved invalid frame");
            m_frameWritten.Set();
            continue;
          }

          curFrame.ToBytes(ref buf);
          dataPipe.Write(buf);

          if (ReleaseFrame != null) {
            ReleaseFrame(curFrame);
          }

          m_frameCount++;

          m_frameWritten.Set();

          // It's safe to throw this memory away, because Unity is transferring ownership.
          curFrame = null;
        }

        // Wait for the next frame
        m_ready.WaitOne();
      }

      dataPipe.Flush();
      dataPipe.Close();

    } catch (System.Threading.ThreadInterruptedException) {
      // This is fine, the render thread sent an interrupt.
      dataPipe.Flush();
      dataPipe.Close();
    } catch (System.Exception e) {
      UnityEngine.Debug.LogWarning(m_outputFile);
      UnityEngine.Debug.LogException(e);
    }
  }

  private void ReadFramesFromFfmpeg() {
    long PIXEL_SIZE_BYTES = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Color32));
    // TODO: Use ffprobe instead of width/height, then w/h properties could be removed.
    byte[] buf = new byte[Height * Width * PIXEL_SIZE_BYTES];
    System.IO.BinaryReader dataPipe = m_dataReader;
    // Keep a local set of buffers to avoid garbage collection.
    Bytable[] localRefs = new Bytable[m_framesOut.Capacity];

    // Init local refs.
    int lastLocalRef = 0;
    for (int i = 0; i < localRefs.Length; i++) {
      localRefs[i] = new Color32Bytable(null);
    }

    try {
      using (dataPipe) {
        while (!m_shouldExit) {
          while (!m_shouldPause) {
            if (m_framesOut.IsFull) {
              // Wait for the consumer.
              m_frameReady.WaitOne();
            }

            int bytesRead = dataPipe.Read(buf, 0, buf.Length);
            while (bytesRead < buf.Length) {
              bytesRead += dataPipe.Read(buf, bytesRead, buf.Length - bytesRead);
              if (bytesRead == 0) {
                return;
              }
            }
            if (bytesRead != buf.Length) {
              // For some reason we only read the wrong amount of data.
              UnityEngine.Debug.LogWarningFormat("BAD READ RESULT: got {0} bytes, expected {1}",
                                                  bytesRead, buf.Length);
              continue;
            }

            // If the last buffer we had was the same size, no allocation will happen here. We
            // will also be holding a reference to that array, so even after it's removed from the
            // m_framesOut buffer, it should not generate garbage.
            Bytable curFrame = localRefs[lastLocalRef];
            lastLocalRef = (lastLocalRef + 1) % localRefs.Length;

            curFrame.FromBytes(buf);

            // If called with overwriteIfFull=true, this code will require a lock.
            m_framesOut.Enqueue(curFrame);
            m_frameCount++;
          }

          // Wait for the next frame
          m_ready.WaitOne();
        }
      }
    } catch (System.Threading.ThreadInterruptedException) {
      // This is fine, the render thread sent an interrupt.
    } catch (System.Exception e) {
      UnityEngine.Debug.LogException(e);
    } finally {
      Stop();
    }
  }
}
} // namespace TiltBrush