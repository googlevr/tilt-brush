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

# Historical Brush trivia

## Double-sided brushes

In M15 we created double-sided-shader variants of several brushes that
previously used single-sided-shaders with double-sided-geometry. At
the last minute, the decision was made not to ship these brushes in
M15. However, the brush assets and materials were included in the
impending first release of Poly Toolkit to help decouple a future
TB release from a PT release.

The single-sided brushes that were converted and shipped in PT are:

* CoarseBristles
* DuctTape
* Flat
* Hypercolor
* Ink
* Leaves
* OilPaint
* Paper
* Splatter
* TaperedFlat
* ThickPaint
* Leaves (but see below)

The double-sided variants were made by:

1. Renaming Brush.mat to BrushSingleSided.mat
2. Renaming Brush.asset to BrushSingleSided.asset
3. Creating a new file BrushDoubleSided.mat
4. Creating a new Brush.asset, referencing the double-sided material
5. Mark the DS brush as superseding the SS brush

## M16

M16 adds 12 new user-visible brushes.

* CelVinyl
* Comet
* DiamondHull
* Icing
* Lofted
* MatteHull
* Petal
* ShinyHull
* Spikes
* UnlitHull
* WetPaint
* WigglyGraphite

M16 also adds 11 new DS variants of previously-SS brushes; see list above.

LeavesDS is _not_ one of these; see below.

M16 also adds 2 unused SS variants of new-in-M16 brushes; see below.

## Leaves

In M15, Leaves had a SS -> DS conversion just like the other SS
brushes. Leaves DS made its way into PT1 along with the other DS
brushes.

Leaves is a compatibility brush and is not exposed by the brush
picker.  Perhaps via an oversight, the M16 manifest still contains
LeavesSS. As a result, LeavesDS did not ship in M16.

LeavesDS is also not in TBT16.

tl;dr: PT has an unused LeavesDS brush.

## WigglyGraphite, WetPaint

WigglyGraphite and WetPaint are new brushes for M16.  They started out
as SS brushes during during M16 development.  New DS brushes were then
made from the SS versions, although arguably we could have (should
have) just mutated the unreleased brushes to make them double-sided.

M16 ships the double-sided versions. However, because of step 5
(setting the "supersedes" field), the single-sided verions also ship
in M16. This has a few implications:

* PT and TBT16 contain unnecessary brushes for the SS versions. They
  are unnecessary because the gltf and fbx will always be generated
  with the most-recent version of the brush, which is DS.

* The .tilt file records the most-compatible (ie least-recent) version
  of the brush. IOW, .tilt files that use WigglyGraphite show the
  WigglyGraphiteSingleSided brush guid.