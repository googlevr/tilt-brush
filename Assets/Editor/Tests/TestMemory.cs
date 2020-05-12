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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NUnit.Framework;

using StrokeFlags = TiltBrush.SketchMemoryScript.StrokeFlags;

namespace TiltBrush {

internal class TestMemory {
  [Test]
  public void TestMemoryListForSave() {
    var input = new List<Stroke>();
    foreach (var flags in new[] {
        StrokeFlags.None,
        StrokeFlags.None, StrokeFlags.IsGroupContinue, StrokeFlags.IsGroupContinue,
        StrokeFlags.None, StrokeFlags.IsGroupContinue }) {
      var stroke = new Stroke();
      stroke.m_Flags = flags;
      // TODO: put unit test objects under a common parent
      stroke.m_Object = new GameObject("(unit test)");
        stroke.m_Object.SetActive(true);
      stroke.m_ControlPoints = new PointerManager.ControlPoint[0];
      input.Add(stroke);
    }

    // no erased strokes
    var output = SketchWriter.EnumerateAdjustedSnapshots(input).ToList();
    CollectionAssert.AreEqual(
        output.ConvertAll(x => x.adjustedStrokeFlags),
        new[] {
          StrokeFlags.None,
          StrokeFlags.None, StrokeFlags.IsGroupContinue, StrokeFlags.IsGroupContinue,
          StrokeFlags.None, StrokeFlags.IsGroupContinue });

    CollectionAssert.AreEqual(
        output.ConvertAll(x => x.adjustedStrokeFlags),
        new[] {
          StrokeFlags.None,
          StrokeFlags.None, StrokeFlags.IsGroupContinue, StrokeFlags.IsGroupContinue,
          StrokeFlags.None, StrokeFlags.IsGroupContinue });

    // head stroke of group erased
    input[1].m_Object.SetActive(false);
    output = SketchWriter.EnumerateAdjustedSnapshots(input).ToList();
    CollectionAssert.AreEqual(
        output.ConvertAll(x => x.adjustedStrokeFlags),
        new[] {
          StrokeFlags.None,
          StrokeFlags.None, StrokeFlags.IsGroupContinue,
          StrokeFlags.None, StrokeFlags.IsGroupContinue });

    // secondary stroke of group erased
    input[1].m_Object.SetActive(true);
    input[2].m_Object.SetActive(false);
    output = SketchWriter.EnumerateAdjustedSnapshots(input).ToList();
    CollectionAssert.AreEqual(
        output.ConvertAll(x => x.adjustedStrokeFlags),
        new[] {
          StrokeFlags.None,
          StrokeFlags.None, StrokeFlags.IsGroupContinue,
          StrokeFlags.None, StrokeFlags.IsGroupContinue });

    // orphaned group (head stroke remaining)
    input[3].m_Object.SetActive(false);
    output = SketchWriter.EnumerateAdjustedSnapshots(input).ToList();
    CollectionAssert.AreEqual(
        output.ConvertAll(x => x.adjustedStrokeFlags),
        new[] {
          StrokeFlags.None,
          StrokeFlags.None,
          StrokeFlags.None, StrokeFlags.IsGroupContinue });

    // orphaned group (1 secondary stroke remaining)
    input[1].m_Object.SetActive(false);
    input[3].m_Object.SetActive(true);
    output = SketchWriter.EnumerateAdjustedSnapshots(input).ToList();
    CollectionAssert.AreEqual(
        output.ConvertAll(x => x.adjustedStrokeFlags),
        new[] {
          StrokeFlags.None,
          StrokeFlags.None,
          StrokeFlags.None, StrokeFlags.IsGroupContinue });

    // orphaned group (0 strokes remaining)
    input[3].m_Object.SetActive(false);
    output = SketchWriter.EnumerateAdjustedSnapshots(input).ToList();
    CollectionAssert.AreEqual(
        output.ConvertAll(x => x.adjustedStrokeFlags),
        new[] {
          StrokeFlags.None,
          StrokeFlags.None, StrokeFlags.IsGroupContinue });
  }
}
}
