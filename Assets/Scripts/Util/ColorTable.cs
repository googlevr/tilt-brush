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
using System.Collections.Generic;

namespace TiltBrush {
public class ColorTable : MonoBehaviour {
  [SerializeField] private float m_SecretDistance = 1.0f;

  // From http://samples.msdn.microsoft.com/workshop/samples/author/dhtml/colors/ColorTable.htm
  // Some names changed to paint-based description for clarity, string length, and to remove
  // a surprising amount of insensitive names.
  private Dictionary<Color32, string> m_Colors = new Dictionary<Color32, string> {
    { new Color32(240, 248, 255, 255), "Cool White" },
    { new Color32(250, 235, 215, 255), "Antique White" },
    { new Color32(127, 255, 212, 255), "Light Cyan" },
    { new Color32(240, 255, 255, 255), "White" },
    { new Color32(245, 245, 220, 255), "Beige" },
    { new Color32(255, 228, 196, 255), "Bisque" },
    { new Color32(0, 0, 0, 255), "Black" },
    { new Color32(141, 141, 141, 255), "Middle Grey" },
    { new Color32(255, 235, 205, 255), "Bone White" },
    { new Color32(0, 0, 255, 255), "Blue" },
    { new Color32(138, 43, 226, 255), "Blue Violet" },
    { new Color32(165, 42, 42, 255), "Brown" },
    { new Color32(222, 184, 135, 255), "Deep Beige" },
    { new Color32(95, 158, 160, 255), "Ash Blue" },
    { new Color32(127, 255, 0, 255), "Chartreuse" },
    { new Color32(210, 105, 30, 255), "Raw Sienna" },
    { new Color32(100, 149, 237, 255), "Cornflower Blue" },
    { new Color32(255, 248, 220, 255), "Cornsilk" },
    { new Color32(220, 20, 60, 255), "Crimson" },
    { new Color32(0, 255, 255, 255), "Cyan" },
    { new Color32(0, 0, 139, 255), "Dark Blue" },
    { new Color32(0, 139, 139, 255), "Dark Teal" },
    { new Color32(184, 134, 11, 255), "Dark Ochre" },
    { new Color32(0, 100, 0, 255), "Dark Green" },
    { new Color32(189, 183, 107, 255), "Dark Khaki" },
    { new Color32(139, 0, 139, 255), "Dark Magenta" },
    { new Color32(85, 107, 47, 255), "Dark Olive Green" },
    { new Color32(255, 140, 0, 255), "Dark Orange" },
    { new Color32(153, 50, 204, 255), "Dark Orchid" },
    { new Color32(139, 0, 0, 255), "Dark Red" },
    { new Color32(233, 150, 122, 255), "Dark Salmon" },
    { new Color32(143, 188, 139, 255), "Dark Sea Green" },
    { new Color32(72, 61, 139, 255), "Dark Slate Blue" },
    { new Color32(47, 79, 79, 255), "Neutral Grey" },
    { new Color32(0, 206, 209, 255), "Dark Turquoise" },
    { new Color32(148, 0, 211, 255), "Dark Violet" },
    { new Color32(255, 20, 147, 255), "Deep Pink" },
    { new Color32(0, 191, 255, 255), "Deep Sky Blue" },
    { new Color32(105, 105, 105, 255), "Dim Grey" },
    { new Color32(30, 144, 255, 255), "Cerulean Blue" },
    { new Color32(178, 34, 34, 255), "Carmine" },
    { new Color32(255, 250, 240, 255), "Floral White" },
    { new Color32(34, 139, 34, 255), "Forest Green" },
    { new Color32(220, 220, 220, 255), "Silver" },
    { new Color32(248, 248, 255, 255), "Ghost White" },
    { new Color32(255, 215, 0, 255), "Cadmium Yellow" },
    { new Color32(218, 165, 32, 255), "Ochre" },
    { new Color32(128, 128, 128, 255), "Grey" },
    { new Color32(0, 128, 0, 255), "Green" },
    { new Color32(173, 255, 47, 255), "Green Yellow" },
    { new Color32(240, 255, 240, 255), "Honeydew" },
    { new Color32(255, 105, 180, 255), "Hot Pink" },
    { new Color32(205, 92, 92, 255), "Red Oxide" },
    { new Color32(75, 0, 130, 255), "Indigo" },
    { new Color32(255, 255, 240, 255), "Ivory" },
    { new Color32(240, 230, 140, 255), "Khaki" },
    { new Color32(230, 230, 250, 255), "Lavender" },
    { new Color32(255, 240, 245, 255), "Pale Lavender" },
    { new Color32(124, 252, 0, 255), "Luminous Green" },
    { new Color32(255, 250, 205, 255), "Pale Lemon" },
    { new Color32(173, 216, 230, 255), "Light Blue" },
    { new Color32(240, 128, 128, 255), "Light Coral" },
    { new Color32(224, 255, 255, 255), "Light Cyan" },
    { new Color32(250, 250, 210, 255), "Pale Lime" },
    { new Color32(144, 238, 144, 255), "Light Green" },
    { new Color32(211, 211, 211, 255), "Light Grey" },
    { new Color32(255, 182, 193, 255), "Light Pink" },
    { new Color32(255, 160, 122, 255), "Light Salmon" },
    { new Color32(32, 178, 170, 255), "Light Sea Green" },
    { new Color32(135, 206, 250, 255), "Light Sky Blue" },
    { new Color32(119, 136, 153, 255), "Light Ash Blue" },
    { new Color32(176, 196, 222, 255), "Light Steel Blue" },
    { new Color32(255, 255, 224, 255), "Light Yellow" },
    { new Color32(0, 255, 0, 255), "Lime" },
    { new Color32(50, 205, 50, 255), "Lime Green" },
    { new Color32(250, 240, 230, 255), "Linen" },
    { new Color32(255, 0, 255, 255), "Magenta" },
    { new Color32(128, 0, 0, 255), "Maroon" },
    { new Color32(102, 205, 170, 255), "Medium Aquamarine" },
    { new Color32(0, 0, 205, 255), "Ultramarine" },
    { new Color32(186, 85, 211, 255), "Medium Orchid" },
    { new Color32(147, 112, 219, 255), "Medium Purple" },
    { new Color32(60, 179, 113, 255), "Medium Sea Green" },
    { new Color32(123, 104, 238, 255), "Medium Slate Blue" },
    { new Color32(0, 250, 154, 255), "Medium Spring Green" },
    { new Color32(72, 209, 204, 255), "Medium Turquoise" },
    { new Color32(199, 21, 133, 255), "Rose Voilet" },
    { new Color32(25, 25, 112, 255), "Midnight Blue" },
    { new Color32(245, 255, 250, 255), "Mint Cream" },
    { new Color32(255, 228, 225, 255), "Misty Rose" },
    { new Color32(255, 228, 181, 255), "Naples Yellow" },
    { new Color32(255, 222, 173, 255), "Titan Buff" },
    { new Color32(0, 0, 128, 255), "Navy" },
    { new Color32(253, 245, 230, 255), "Old Lace" },
    { new Color32(128, 128, 0, 255), "Olive" },
    { new Color32(107, 142, 35, 255), "Moss Green" },
    { new Color32(255, 165, 0, 255), "Orange Yellow" },
    { new Color32(255, 69, 0, 255), "Scarlet" },
    { new Color32(218, 112, 214, 255), "Orchid" },
    { new Color32(238, 232, 170, 255), "Pale Ochre" },
    { new Color32(152, 251, 152, 255), "Pale Green" },
    { new Color32(175, 238, 238, 255), "Pale Turquoise" },
    { new Color32(219, 112, 147, 255), "Pale Violet Red" },
    { new Color32(255, 239, 213, 255), "Papaya Whip" },
    { new Color32(255, 218, 185, 255), "Peach Puff" },
    { new Color32(205, 133, 63, 255), "Raw Sienna" },
    { new Color32(255, 192, 203, 255), "Pink" },
    { new Color32(221, 160, 221, 255), "Lilac" },
    { new Color32(176, 224, 230, 255), "Powder Blue" },
    { new Color32(95, 0, 128, 255), "Purple" },
    { new Color32(255, 0, 0, 255), "Red" },
    { new Color32(188, 143, 143, 255), "Rosy Brown" },
    { new Color32(65, 105, 225, 255), "Royal Blue" },
    { new Color32(139, 69, 19, 255), "Burnt Umber" },
    { new Color32(250, 128, 114, 255), "Salmon" },
    { new Color32(244, 164, 96, 255), "Sandy Brown" },
    { new Color32(46, 139, 87, 255), "Sea Green" },
    { new Color32(255, 245, 238, 255), "Seashell" },
    { new Color32(160, 82, 45, 255), "Sienna" },
    { new Color32(192, 192, 192, 255), "Silver" },
    { new Color32(135, 206, 235, 255), "Sky Blue" },
    { new Color32(106, 90, 205, 255), "Slate Blue" },
    { new Color32(112, 128, 144, 255), "Slate Grey" },
    { new Color32(255, 250, 250, 255), "Snow" },
    { new Color32(0, 255, 127, 255), "Spring Green" },
    { new Color32(70, 130, 180, 255), "Steel Blue" },
    { new Color32(210, 180, 140, 255), "Tan" },
    { new Color32(0, 128, 128, 255), "Teal" },
    { new Color32(216, 191, 216, 255), "Pale Lilac" },
    { new Color32(255, 99, 71, 255), "Tomato" },
    { new Color32(64, 224, 208, 255), "Turquoise" },
    { new Color32(238, 130, 238, 255), "Violet" },
    { new Color32(245, 222, 179, 255), "Titan" },
    { new Color32(255, 255, 255, 255), "Bright White" },
    { new Color32(245, 245, 245, 255), "White" },
    { new Color32(255, 255, 0, 255), "Yellow" },
    { new Color32(154, 205, 50, 255), "Leaf Green" },
    { new Color32(201, 104, 0, 255), "Orange" },
  };

  private Dictionary<Color32, string> m_SecretColors = new Dictionary<Color32, string> {
    { new Color32(27, 15, 253, 255), "Patrick's Favorite Color" },
    { new Color32(72, 9, 12, 255), "Mach's Favorite Color" },
    { new Color32(126, 71, 143, 255), "Joyce's Favorite Color" },
    { new Color32(66, 113, 120, 255), "Tim's Favorite Color" },
    { new Color32(14, 81, 53, 255), "Drew's Favorite Color" },
    { new Color32(255, 220, 202, 255), "Jeremy's Favorite Color" },
    { new Color32(16, 100, 173, 255), "Elisabeth's Favorite Color" },
    { new Color32(217, 255, 109, 255), "Ashley's Favorite Color" },
    { new Color32(255, 241, 27, 255), "Tory's Favorite Color" },
    { new Color32(29, 59, 93, 255), "Paul's Favorite Color" },
    { new Color32(238, 70, 153, 255), "Izzy's Favorite Color" },
    { new Color32(255, 127, 80, 255), "Jon's Favorite Color" },
    { new Color32(176, 25, 126, 255), "Gottlieb's Favorite Color" },
    { new Color32(11, 28, 92, 255), "Coco's Favorite Color" },
  };

  public static ColorTable m_Instance;

  float ColorDistance(Color32 colorA, Color32 colorB) {
    // From https://en.wikipedia.org/wiki/Color_difference
    float deltaR = (float)(colorA.r - colorB.r);
    float deltaG = (float)(colorA.g - colorB.g);
    float deltaB = (float)(colorA.b - colorB.b);
    float avgR = (float)(colorA.r + colorB.r) / 2.0f;
    float r = (2.0f + avgR / 256.0f) * deltaR * deltaR;
    float g = 4.0f * deltaG * deltaG;
    float b = (2.0f + (255.0f - avgR) / 256.0f) * deltaB * deltaB;
    return Mathf.Sqrt(r + g + b);
  }

  void Awake() {
    m_Instance = this;
  }

  public string NearestColorTo(Color color) {
    float dist = float.MaxValue;
    Color32? nearestColor = null;
    foreach (var col in m_SecretColors.Keys) {
      float newDist = ColorDistance(col, color);
      if (newDist < m_SecretDistance) {
        return m_SecretColors[col];
      }
    }

    foreach (var col in m_Colors.Keys) {
      float newDist = ColorDistance(col, color);
      if (newDist < dist) {
        dist = newDist;
        nearestColor = col;
      }
    }
    return m_Colors[nearestColor.Value];
  }
}
} // namespace TiltBrush
