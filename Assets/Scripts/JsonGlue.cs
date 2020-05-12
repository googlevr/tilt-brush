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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IO;
using UnityEngine;

namespace TiltBrush {

public class BadJson : Exception {
  public BadJson(string message) : base(message) {}
  public BadJson(string fmt, params System.Object[] args)
    : base(string.Format(fmt, args)) {}
  public BadJson(Exception inner, string fmt, params System.Object[] args)
    : base(string.Format(fmt, args), inner) {}
}

// Extends JsonTextWriter with support for terse array & object formatting, so
// that we can have indented formatting but keep short arrays compact.  E.g.:
//    {
//        "Foo": [ 1, 2, 3 ],
//        "Bar": 10
//    }
//
// Also adds a trailing newline.
// Also lumped in custom writing of vector types.  (Yes, we could use
// Serializer infrustructure, but it will be more complex and slower.)
//
// Nesting normal formatting within terse formatting is not supported.
public class CustomJsonWriter : JsonTextWriter {
  public bool terseFormatting;

  public CustomJsonWriter(TextWriter textWriter) : base(textWriter) {
    Formatting = Formatting.Indented;
  }

  public void WriteStartArray(bool terse) {
    terseFormatting = terse;
    base.WriteStartArray();
  }

  public void WriteStartObject(bool terse) {
    terseFormatting = terse;
    base.WriteStartObject();
  }

  protected override void WriteEnd(JsonToken token) {
    base.WriteEnd(token);
    terseFormatting = false;
  }

  protected override void WriteIndent() {
    if (terseFormatting) {
      WriteIndentSpace();
    } else {
      base.WriteIndent();
    }
  }

  public override void Close() {
    // give file a terminating newline
    // NOTE: this matches what Json.NET does on Windows.
    // Ideally this should not change based on the platform, but that would
    // involve hacking Json.NET to have a configurable newline.
    WriteRaw(System.Environment.NewLine);
    base.Close();
  }

  public void WriteValue(Vector3 v) {
    WriteStartArray(true);
    for (var i = 0; i < 3; ++i) {
      WriteValue(v[i]);
    }
    WriteEndArray();
  }

  public void WriteValue(Vector2 v) {
    WriteStartArray(true);
    for (var i = 0; i < 2; ++i) {
      WriteValue(v[i]);
    }
    WriteEndArray();
  }

  public void WriteValue(Color v) {
    WriteStartArray(true);
    for (var i = 0; i < 4; ++i) {
      WriteValue(v[i]);
    }
    WriteEndArray();
  }

  public void WriteValue(Color32 v) {
    WriteStartArray(true);
    WriteValue(v.r);
    WriteValue(v.g);
    WriteValue(v.b);
    WriteValue(v.a);
    WriteEndArray();
  }

  public void WriteValue(Quaternion v) {
    WriteStartArray(true);
    for (var i = 0; i < 4; ++i) {
      WriteValue(v[i]);
    }
    WriteEndArray();
  }
}

// Supports Unity vectors represented as arrays.
// This must be used with our CustomJsonWriter.
public class JsonVectorConverter : JsonConverter {

  public override bool CanConvert(Type objectType) {
    return (objectType == typeof(Vector3)
            || objectType == typeof(Vector2)
            || objectType == typeof(Color)
            || objectType == typeof(Color32)
            || objectType == typeof(Quaternion));
  }

  private static float ReadFloat(JsonReader reader) {
    reader.Read();
    if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer) {
      return Convert.ToSingle(reader.Value);
    }
    throw new TiltBrush.BadJson("Expected numeric value");
  }

  public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
                                  JsonSerializer serializer) {
    if (reader.TokenType != JsonToken.StartArray) {
      throw new TiltBrush.BadJson("Expected array");
    }
    object result;
    if (objectType == typeof(Vector3)) {
      result = new Vector3(
          ReadFloat(reader),
          ReadFloat(reader),
          ReadFloat(reader));
    } else if (objectType == typeof(Vector2)) {
      result = new Vector2(
          ReadFloat(reader),
          ReadFloat(reader));
    } else if (objectType == typeof(Color)) {
      result = new Color(
          ReadFloat(reader),
          ReadFloat(reader),
          ReadFloat(reader),
          ReadFloat(reader));
    } else if (objectType == typeof(Color32)) {
      result = new Color32(
          (byte)ReadFloat(reader),
          (byte)ReadFloat(reader),
          (byte)ReadFloat(reader),
          (byte)ReadFloat(reader));
    } else if (objectType == typeof(Quaternion)) {
      result = new Quaternion(
        ReadFloat(reader),
        ReadFloat(reader),
        ReadFloat(reader),
        ReadFloat(reader));
    } else {
      Debug.Assert(false, "Converter registered with bad type");
      throw new TiltBrush.BadJson("Internal error");
    }
    reader.Read();
    if (reader.TokenType != JsonToken.EndArray) {
      throw new TiltBrush.BadJson("Expected array end");
    }
    return result;
  }

  public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
    var customWriter = writer as CustomJsonWriter;
    var objectType = value.GetType();
    if (objectType == typeof(Vector3)) {
      customWriter.WriteValue((Vector3)value);
    } else if (objectType == typeof(Vector2)) {
      customWriter.WriteValue((Vector2)value);
    } else if (objectType == typeof(Color)) {
      customWriter.WriteValue((Color)value);
    } else if (objectType == typeof(Color32)) {
      customWriter.WriteValue((Color32)value);
    } else if (objectType == typeof(Quaternion)) {
      customWriter.WriteValue((Quaternion)value);
    } else {
      Debug.Assert(false, "Converter registered with bad type");
      throw new TiltBrush.BadJson("Internal error");
    }
  }
}

// If the obfuscator renames this class, we get crashes at runtime
// instantiating this attribute:
//   [Newtonsoft.Json.JsonConverter(typeof(JsonTrTransformConverter))]
// which is applied to TrTransform.
[System.Reflection.Obfuscation(Exclude=true)]
public class JsonTrTransformConverter : JsonConverter {
  public override bool CanConvert(Type objectType) {
    return objectType == typeof(TrTransform);
  }

  public override object ReadJson(JsonReader reader, Type t, object existingValue,
                                  JsonSerializer s) {
    TrTransform result;

    // Reader is at the start token of the object.
    // Reader should be left at the last token of the object.
    if (reader.TokenType != JsonToken.StartArray) {
      throw new BadJson("Expected array");
    }
    reader.Read();
    result.translation = s.Deserialize<Vector3>(reader);
    reader.Read();
    result.rotation = s.Deserialize<Quaternion>(reader);
    reader.Read();
    result.scale = s.Deserialize<float>(reader);
    reader.Read();
    if (reader.TokenType != JsonToken.EndArray) {
      throw new BadJson("Expected array end");
    }
    return result;
  }

  public override void WriteJson(JsonWriter writer, object value, JsonSerializer s) {
    TrTransform xf = (TrTransform)value;
    var customWriter = writer as CustomJsonWriter;
    if (customWriter != null) {
      customWriter.WriteStartArray(true);
    } else {
      writer.WriteStartArray();
    }
    s.Serialize(writer, xf.translation);
    s.Serialize(writer, xf.rotation);
    if (customWriter != null) { customWriter.terseFormatting = true; }
    s.Serialize(writer, xf.scale);
    writer.WriteEndArray();
  }
}

public class JsonGuidConverter : JsonConverter {
  public override bool CanConvert(Type objectType) { return true; }
  public override object ReadJson(JsonReader reader, Type t, object o, JsonSerializer s) {
    if (reader.TokenType != JsonToken.String) {
      throw new BadJson("Expected string");
    }
    return new Guid((string)reader.Value);
  }
  public override void WriteJson(JsonWriter writer, object value, JsonSerializer s) {
    string sGuid = ((Guid)value).ToString("D");
    writer.WriteValue(sGuid);
  }
}

// This extends the cached mapping for Json type converters.
// see http://www.newtonsoft.com/json/help/html/Performance.htm
public class CustomJsonContractResolver : DefaultContractResolver {
  // results are cached
  protected override JsonContract CreateContract(Type objectType) {
    JsonContract contract = base.CreateContract(objectType);
    if (objectType == typeof(Vector3)
        || objectType == typeof(Vector2)
        || objectType == typeof(Color)
        || objectType == typeof(Color32)
        || objectType == typeof(Quaternion)) {
      contract.Converter = new JsonVectorConverter();
    } else if (objectType == typeof(Guid)) {
      contract.Converter = new JsonGuidConverter();
    }
    // Don't need to check for TrTransform because we've annotated that type
    return contract;
  }
}

}  // namespace TiltBrush
