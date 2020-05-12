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

using System.IO;

namespace TiltBrush {
public static class TsvIo {
  // Takes the tsv file name and returns its contents as a 2D array.
  public static string[,] TsvToTable(string input) {
    string tsv = File.ReadAllText(input);
    string[] rows = tsv.Split('\n');
    if (rows.Length == 0) { return new string[0, 0]; }
    string[] columns = rows[0].Split('\t');
    string[,] table = new string[rows.Length, columns.Length];
    for (int r = 0; r < rows.Length; r++) {
      string[] tokens = rows[r].Split('\t');
      for (int c = 0; c < tokens.Length; c++) {
        table[r, c] = tokens[c].Trim();
      }
    }
    return table;
  }

  // Writes out data to tsv format at output location.
  public static void WriteResults(string output, string[,] data) {
    string text = "";
    for (int r = 0; r < data.GetLength(0); r++) {
      for (int c = 0; c < data.GetLength(1); c++) {
        text += data[r, c] + "\t";
      }
      text += System.Environment.NewLine;
    }
    File.WriteAllText(output, text);
  }
}
} // namespace TiltBrush
