# Copyright 2020 The Tilt Brush Authors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import sys,math,random
from pxr import Usd,Sdf,UsdGeom,Tf,Gf,Vt

# Offset to a point of interest.
worldCenter = Gf.Vec3f(0, 0, 0)

def clamp(x, lowerlimit, upperlimit):
  if x < lowerlimit: x = lowerlimit
  if x > upperlimit: x = upperlimit
  return x

def smoothstep(edge0, edge1, x):
  x = clamp((x - edge0)/(edge1 - edge0), 0.0, 1.0)
  return x*x*(3 - 2*x)

def smootherstep(edge0, edge1, x):
  x = clamp((x - edge0)/(edge1 - edge0), 0.0, 1.0)
  return x*x*x*(x*(x*6 - 15) + 10)

#
# Filters dictate when a stroke becomes active, i.e. predicates for activation.
#
def isHigherVert(vertPos, height, radius):
  l = Gf.Vec2f(vertPos[0], vertPos[2]).GetLength()
  return vertPos[1] < height and l < radius

def isHigher(substroke, height, radius):
  return substroke.avgHeight < height and substroke.minLen < radius

def isInRadius(vert, radius):
  return (vert - worldCenter).GetLength() < radius 

#
# Animation Algorithm Starts Here, animate().
#
def animate(stage):
  """The output from Tilt Brush is strokes with a growth velocity.
  This could be authored per vertex later (e.g. for easing).
  """
  # The current animation time, this will increase monotonicly to generate frames of animation.
  time = 1

  # The maximum number of animated strokes, for performance constraints.
  maxActive = 30

  # Filters dictate when a stroke becomes active, i.e. predicates for activation.
  activeFilter = isInRadius 
  activeFilterVert = isHigherVert
  
  # The target length of the animation.
  lengthInSeconds = 30 
  # The playback framerate.
  framesPerSecond = 30 
  # The number of frames we will generate based on target time and rate.
  numFrames = lengthInSeconds * framesPerSecond

  # Boundaries for activation.
  minHeight = 0 
  maxHeight = 20 
  radius = 17.0 
  maxRadius = 100.
  height = minHeight
 
  # Compute the actual radius of the world bounds.
  worldBounds = UsdGeom.Xform(stage.GetPrimAtPath("/")).ComputeWorldBound(0, "default")
  maxRadius = (worldBounds.GetRange().max - worldBounds.GetRange().min).GetLength()

  # Compute the centroid of the world.
  global worldCenter
  worldCenter = Gf.Vec3f(worldBounds.ComputeCentroid())
  # Just for newIntroSketch.tilt
  if "NewIntroSketch" in stage.GetRootLayer().identifier:
    worldCenter = Gf.Vec3f(0.73135, 19.92212, 33.2210311)
  worldCenter[1] = worldBounds.GetRange().min[1]

  print "World Center:", worldCenter
  print "Max Radius:", maxRadius

  # Visualize the radius.
  debugSphere = UsdGeom.Sphere(stage.DefinePrim("/debug", "Sphere"))
  debugSphere.CreateRadiusAttr(radius)
  debugSphere.GetPrim().GetAttribute("purpose").Set("guide")
  attr = debugSphere.GetPrim().GetAttribute("primvars:displayOpacity")
  attr.Set([0.125])
  attr.SetMetadata("interpolation", "constant")
  UsdGeom.Xform(attr.GetPrim()).AddTranslateOp().Set(worldCenter)
 

  # Initialize data structures.
  #   - strokes are Unity meshes (or Tilt Brush batches).
  #   - substrokes are the individual brush strokes within a single mesh.
  #   - activeSubstrokes are sub-strokes currently in-flight.
  #   - completeSubstrokes are sub-strokes that are done animating.
  strokes = MakeStrokes(stage)
  substrokes = MakeSubstrokes(strokes)
  activeStrokes = set()
  activeSubstrokes = set()
  completeSubstrokes = set() 

  # Compute step sizes based on target animation length.
  dRadius = (maxRadius - radius) / float(numFrames) / 1.5 
  dHeight = (maxHeight - minHeight) / float(numFrames)

  # Set USD animation start/end times.
  stage.SetStartTimeCode(time)
  stage.SetEndTimeCode(numFrames)

  # Zero out stroke opacities
  for s in strokes:
    s.Save(time)

  # Main animation loop.
  for time in range(0, numFrames):
    print
    print "Time:", time, height, radius, smoothstep(1.0, float(numFrames), time)
    curActive = 0
    
    if len(activeStrokes) < maxActive:
      # On the final frame, increase activation volumes to "infinity" (and beyond ;)
      if time == numFrames - 1:
        height = 10000000
        radius = 10000000
      
      # Search for strokes to be activated.
      didAddStroke = 0
      for ss in substrokes:
        # Already animating, skip.
        if ss in activeSubstrokes:
          continue
        # Done animating, skip.
        if ss in completeSubstrokes:
          continue
        # Overloaded.
        if len(activeStrokes) >= maxActive:
          break
        # If this sub-stroke passes the filter, add it to the animating list.
        if activeFilter(ss.minPoint, radius):
          didAddStroke = 1
          activeSubstrokes.add(ss)
          activeStrokes.add(ss.stroke)        
          # Mark the stroke as dirty to save its initial state.
          ss.stroke.dirty = True
          ss.SetRadius(radius, time)
          print "+",
      if not didAddStroke:
        # We didn't add any strokes, which means the radius needs to increase.
        # Grow the activation volumes (increase sphere size, raise floor plane height).
        height += dHeight 
        radius += dRadius * smoothstep(1.0, float(numFrames), time)



    # Update debug vis.
    debugSphere.GetRadiusAttr().Set(radius, time)

    # Call save on everything, but only dirty strokes will actually write data.
    # Save a key at the previous frame here so that when a stroke starts animating, when linearly
    # interpolated, it will not start animating from frame zero to the first key frame.
    #for s in strokes:
    #  s.Save(time - 1)

    # Update stroke animation.
    remove = []
    for ss in activeSubstrokes:
      print ".",
      if not ss.Update(dRadius, smoothstep(1.0, float(numFrames), time)):
        if ss.indicesWritten != ss.indexCount:
          raise "Fail"
        remove.append(ss)
    
    # Remove all the completed strokes.
    for ss in remove:
      activeSubstrokes.remove(ss)
      completeSubstrokes.add(ss)

    # Save keyframes for the current time.
    for s in strokes:
      s.Save(time)

    # Rebuild the activeStrokes set.
    activeStrokes = set()
    for ss in activeSubstrokes:
      activeStrokes.add(ss.stroke)

  # Drainstop: we have leftover strokes that didn't finish animating within the target time, rather
  # than popping them, we let them finish animating and run over the target time.
  while len(activeSubstrokes) > 0:
    remove = []
    time += 1
    # Since we blew past the initial frame estimate, we also need to update the USD end time.
    stage.SetEndTimeCode(time)
    # Loop: update, remove, save, rinse, repeat.
    for ss in activeSubstrokes:
      if not ss.Update(dRadius, 2.0):
        if ss.indicesWritten != ss.indexCount:
          raise "Fail"
        remove.append(ss)
    for ss in remove:
      activeSubstrokes.remove(ss)
      completeSubstrokes.add(ss)
    for s in strokes:
      s.Save(time)

class Substroke(object):
  def __init__(self, stroke, startVert, vertCount, startIndex, indexCount):
    self.stroke = stroke
    self.startVert = startVert
    self.vertCount = vertCount 
    self.startIndex = startIndex
    self.indexCount = indexCount
    self.i = startVert
    self.step = 10
    self.radius = 0
    self.indicesWritten = 0
    self.growthVel = 1
    self.minHeight = self.GetVertex(0)[2] 
    self.maxHeight = self.GetVertex(0)[2] 
    self.avgHeight = self.GetVertex(0)[2] 
    self.minLen = 10000000
    self.minPoint = self.GetVertex(0)

    minVal = (self.minPoint - worldCenter).GetLength()
    for i in range(vertCount):
      v = self.GetVertex(i)
      l = (v - worldCenter).GetLength()
      if l < minVal:
        minVal = l
        self.minPoint = v 
      if Gf.IsClose(v, Gf.Vec3f(), 1e-7):
        continue
      l = Gf.Vec2f(v[0], v[2]).GetLength()
      self.minHeight = min(self.minHeight, v[1])
      self.maxHeight = max(self.minHeight, v[1])
      self.avgHeight = (self.maxHeight - self.minHeight) / 2.0
      self.minLen = min(self.minLen, l)

    # Debug visualization.
    self.minPtDebug = UsdGeom.Sphere.Define(stroke.prim.GetStage(), str(stroke.prim.GetPath()) + "/minPt" + str(startIndex))
    self.minPtDebug.GetPrim().GetAttribute("purpose").Set("guide")
    attr = self.minPtDebug.GetPrim().GetAttribute("primvars:displayOpacity")
    attr.Set([0.25])
    attr.SetMetadata("interpolation", "constant")
    attr = self.minPtDebug.GetPrim().GetAttribute("primvars:displayColor")
    attr.Set([Gf.Vec3f(1, 1, 1)], 0)
    attr.SetMetadata("interpolation", "constant")
    self.minPtDebug.CreateRadiusAttr(1.0)
    UsdGeom.Xform(self.minPtDebug.GetPrim()).AddTranslateOp().Set(self.minPoint)

  def __len__(self):
    return self.vertCount

  def SetRadius(self, radius, time):
    self.radius = radius
    attr = self.minPtDebug.GetPrim().GetAttribute("primvars:displayColor")
    attr.Set([Gf.Vec3f(1, 1, 1)], time-1)
    attr.Set([Gf.Vec3f(0, .5, .5)], time)
    attr.SetMetadata("interpolation", "constant")
 
  def SetStep(targetFrameCount, strokeCount, maxActiveStrokes):
    pass
    #b = strokeCount / maxActiveStrokes
    #strokeLength = targetFrameCount / b
    #self.step = self.vertCount / strokeLength

  def GetVertex(self, i):
    return self.stroke.points[i + self.startVert]

  def GetIndex(self, i):
    return self.stroke.originalIndices[i + self.startIndex]

  def SetIndex(self, i, value):
    self.stroke.indices[i + self.startIndex] = value
    self.stroke.maskIndices[i + self.startIndex] = 1

  def Update(self, dRadius, t):
    self.radius += dRadius
    return self._GrowByTopology(t)

  def _GrowByTopology(self, t):
    for j in range(self.growthVel + int((t+.6) * 10)):
      for i in range(0, min(6, self.indexCount - self.indicesWritten), 1):
        self.SetIndex(self.indicesWritten, self.GetIndex(self.indicesWritten))
        self.indicesWritten += 1
      self.stroke.dirty = True
    self.growthVel += 4
    return self.indicesWritten < self.indexCount

  def _GrowByRadius(self):
    x = 0
    for vi in range(0, self.indexCount, 3):
      # Skip strokes that have already been processed.
      if self.stroke.maskIndices[vi + self.startIndex] != 0:
        continue
      # No need to go through GetVertex here, since GetIndex returns the points index
      for ii in range(3):
        i0 = self.GetIndex(vi + ii)
        p0 = self.stroke.points[i0]
        if isInRadius(p0, self.radius):
          for jj in range(3):
            self.SetIndex(vi + jj, self.GetIndex(vi + jj))
          self.stroke.dirty = True
          self.indicesWritten += 3
          break
    return self.indicesWritten < self.indexCount

class Stroke(object):
  def __init__(self, prim, points, indices, vertOffsets, vertCounts, indexOffsets, indexCounts, displayOpacity):
    self.dirty = True 
    self.adj = 2 * random.random()
    self.prim = prim
    self.points = points
    self.indices = Vt.IntArray(len(indices), 0)
    self.originalIndices = Vt.IntArray(indices)
    self.maskIndices = Vt.IntArray(len(indices), 0)
    self.vertOffsets = vertOffsets
    self.vertCounts = vertCounts
    self.indexOffsets = indexOffsets
    self.indexCounts = indexCounts
    self.displayOpacity = Vt.FloatArray(len(displayOpacity))
    self.previousOpacity = Vt.FloatArray(displayOpacity)
    self.originalOpacity = Vt.FloatArray(displayOpacity)
    self.substrokes = self._GetSubstrokes()
    self.adjs = []
    for i in range(len(displayOpacity)):
      displayOpacity[i] = 0
    for i in self.vertOffsets:
      self.adjs.append(random.random() - 1)
    self.adjs.append(random.random() - 1)

  def _GetSubstrokes(self):
    ret = []
    for i,offset in enumerate(self.vertOffsets):
      ret.append(Substroke(self, offset, self.vertCounts[i], self.indexOffsets[i], self.indexCounts[i]))
    return ret
 
  def GetSubstroke(self, vertexIndex):
    for i,offset in enumerate(self.vertOffsets):
      if vertIndex >= offset:
        return (offset, self.vertCounts[i]) 
    raise "Vertex not found"

  def GetAdj(self, vertIndex):
    for i,offset in enumerate(self.vertOffsets):
      if vertIndex >= offset:
        return 3.0 * self.adjs[i]
    raise "Vertex not found"

  def Save(self, time):
    if not self.dirty:
      return
    self.prim.GetAttribute("faceVertexIndices").Set(self.indices, time)
    self.dirty = False

def MakeSubstrokes(strokes):
  ret = []
  for stroke in strokes:
    ret.extend(stroke.substrokes)
  return ret

def MakeStrokes(stage):
  ret = []
  print "Reading strokes..."
  for p in stage.Traverse():
    if not p.IsA(UsdGeom.Mesh):
      continue

    print ".",
    ret.append(Stroke(p,
                      p.GetAttribute("points").Get(0),
                      p.GetAttribute("faceVertexIndices").Get(0),
                      p.GetAttribute("stroke:vertOffsets").Get(0),
                      p.GetAttribute("stroke:vertCounts").Get(0),
                      p.GetAttribute("stroke:triOffsets").Get(0),
                      p.GetAttribute("stroke:triCounts").Get(0),
                      p.GetAttribute("primvars:displayOpacity").Get(0)))
  return ret

if __name__ == "__main__":
  usdFile = sys.argv[1]
  outputFile = usdFile.replace(".usd", "--animated.usd")
  stage = Usd.Stage.Open(usdFile)
  try:
    animate(stage)
  finally:
    print "Saving..."
    stage.Export(outputFile)
