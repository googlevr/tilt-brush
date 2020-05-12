using System;
using System.IO;

namespace TiltBrush {
/// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
/// Under the MIT License, details: License.txt.
public class JPEGMarkerFoundException : Exception {
  public JPEGMarkerFoundException(byte marker) { this.Marker = marker; }
  public byte Marker;
}

/// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
/// Under the MIT License, details: License.txt.
public class JPEGBinaryReader : System.IO.BinaryReader {
  const byte kJpegMarkerXFF = (byte)0xff;
  private byte marker;

  // JPEG is written big endian.
  public JPEGBinaryReader(Stream input) : base(input) { }

  public byte GetNextMarker() {
    try { while (true) { ReadJpegByte(); } } catch (JPEGMarkerFoundException ex) {
      return ex.Marker;
    }
  }

  protected byte ReadJpegByte() {
    byte c = ReadByte();

    /* If it's 0xFF, check and discard stuffed zero byte */
    if (c == kJpegMarkerXFF) {
      // Discard padded oxFFs
      while ((c = ReadByte()) == 0xff) ;

      // ff00 is the escaped form of 0xff
      if (c == 0) c = 0xff;
      else {
        // Otherwise we've found a new marker.
        marker = c;
        throw new JPEGMarkerFoundException(marker);
      }
    }
    return c;
  }
}
}  // TiltBrush

