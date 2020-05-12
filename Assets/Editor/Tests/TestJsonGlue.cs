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
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush {

internal class TestJsonGlue : MathTestUtils {
  static string Serialize<T>(T obj) {
    var js = GetSerializer();
    var sw = new StringWriter();
    js.Serialize(new CustomJsonWriter(sw), obj);
    return sw.ToString();
  }

  static T Deserialize<T>(string json) {
    var js = GetSerializer();
    return js.Deserialize<T>(new JsonTextReader(new StringReader(json)));
  }

  static JsonSerializer GetSerializer() {
    JsonSerializer s = new JsonSerializer();
    s.ContractResolver = new CustomJsonContractResolver();
    return s;
  }

  static JsonTextReader GetReader(string input) {
    return new JsonTextReader(new StringReader(input));
  }

  static Color32 RandomColor32() {
    return new Color32(
        (byte)UnityEngine.Random.Range(0,255),
        (byte)UnityEngine.Random.Range(0,255),
        (byte)UnityEngine.Random.Range(0,255),
        (byte)UnityEngine.Random.Range(0,255));
  }

  [Test]
  public void TestReadTrTransform() {
    var xf = Deserialize<TrTransform>("[ [0,1,0], [0,0,0,1], 3]");
    Assert.AreEqual(xf, TrTransform.TRS(Vector3.up, Quaternion.identity, 3));
  }

  [Test]
  public void TestVector2() {
    var xf = Deserialize<Vector2>("[5, 4]");
    Assert.AreEqual(xf, new Vector2(5, 4));
  }

  [Test]
  public void TestRoundTripTrTransform() {
    var xfs = new List<TrTransform>();
    for (int i = 0; i < 10; ++i) {
      xfs.Add(RandomTr());
    }

    var json = Serialize(xfs);
    var xfs2 = Deserialize<List<TrTransform>>(json);

    Assert.AreEqual(xfs.Count, xfs2.Count);
    for (int i = 0; i < xfs.Count; ++i) {
      AssertAlmostEqual(xfs[i], xfs2[i]);
    }
  }

  [Test]
  public void TestReadWriteColor() {
    var color = Deserialize<Color>("[1,2,3,4]");
    Assert.AreEqual(color, new Color(1,2,3,4));
    var json = Serialize(color);
    Assert.AreEqual(json, "[ 1.0, 2.0, 3.0, 4.0 ]");
  }

  [Test]
  public void TestReadWriteColor32() {
    var color = Deserialize<Color32>("[1,2,3,4]");
    Assert.AreEqual(color, new Color32(1,2,3,4));
    var json = Serialize(color);
    Assert.AreEqual(json, "[ 1, 2, 3, 4 ]");
  }

  [Test]
  public void TestRoundTripColor32() {
    var colors = new List<Color32>();
    for (int i = 0; i < 10; ++i) {
      colors.Add(RandomColor32());
    }

    var json = Serialize(colors);
    var colors2 = Deserialize<List<Color32>>(json);

    Assert.AreEqual(colors.Count, colors2.Count);
    for (int i = 0; i < colors.Count; ++i) {
      Assert.AreEqual(colors[i], colors2[i]);
    }
  }

  [Test]
  public void TestReadWritePalette_Null() {
    // Test proper round-tripping of palette data: entries is null
    Palette p = new Palette();
    p.Colors = null;
    var json = Serialize(p);
    Assert.AreEqual("{}", json);
    Palette p2 = Deserialize<Palette>(json);
    Assert.AreEqual(p2.Colors, p.Colors);
  }

  [Test]
  public void TestReadWritePalette_Empty() {
    // Test proper round-tripping of palette data: entries is empty
    Palette p = new Palette();
    p.Colors = new Color32[] {};
    var json = Serialize(p);
    Assert.AreEqual(@"{
  ""Entries"": []
}", json);
    Palette p2 = Deserialize<Palette>(json);
    Assert.AreEqual(p2.Colors, p.Colors);
  }

  [Test]
  public void TestReadWritePalette_NonEmpty() {
    // Test proper round-tripping of palette data: entries is non-empty

    Palette p = new Palette();
    p.Colors = new Color32[] { new Color32(1,2,3,255) };
    var json = Serialize(p);
    Assert.AreEqual(@"{
  ""Entries"": [ [ 1, 2, 3, 255 ]
  ]
}", json);
    Palette p2 = Deserialize<Palette>(json);
    Assert.AreEqual(p2.Colors, p.Colors);
  }

  [Test]
  public void TestReadWritePalette_NonOpaque() {
    // Entries contains non-opaque colors.
    // They should be written out faithfully, but should read back in as opaque.

    Palette p = new Palette();
    p.Colors = new Color32[] { new Color32(1,2,3,4), new Color32(5,6,7,8) };
    var json = Serialize(p);
    Assert.AreEqual(@"{
  ""Entries"": [ [ 1, 2, 3, 4 ], [ 5, 6, 7, 8 ]
  ]
}", json);
    Palette p2 = Deserialize<Palette>(json);
    var expected = new Color32[] { new Color32(1,2,3,255), new Color32(5,6,7,255) };
    Assert.AreEqual(p2.Colors, expected);
  }

  [Test]
  public void TestReadPalette_90bFormat() {
    // Test reading of the 9.0b format. It should come in null.
    Palette p = Deserialize<Palette>(@"{ ""Colors"": [ { ""r"": 50 } ] }");
    Assert.AreEqual(null, p.Colors);
  }

}

}  // namespace TiltBrush
