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
using UnityEngine;

namespace TiltBrush {

class BubbleWandBrush : TubeBrush {
  const ushort kVertsInClosedCircle = 9;

  public BubbleWandBrush() : base(false) {}

  protected override void InitBrush(BrushDescriptor desc, TrTransform localPointerXf) {
    base.InitBrush(desc, localPointerXf);
    m_geometry.Layout = GetVertexLayout(desc);
  }

  override public GeometryPool.VertexLayout GetVertexLayout(BrushDescriptor desc) {
    return new GeometryPool.VertexLayout {
      uv0Size = 3,
      uv1Size = 4,
      bUseNormals = true,
      bUseColors = true,
    };
  }

  override protected void ControlPointsChanged(int iKnot0) {
    base.ControlPointsChanged(iKnot0);
    // Update the UVWs.
    int numUvws = m_geometry.m_Texcoord0.v3.Count;
    for (int i = 0; i < numUvws; i++) {
      Vector3 uvw = m_geometry.m_Texcoord0.v3[i];
      float y = (i + 1) % kVertsInClosedCircle;
      uvw[0] = (i + 1 - y) / (numUvws + 2 - kVertsInClosedCircle);
      uvw[1] = y / (kVertsInClosedCircle - 1);
      m_geometry.m_Texcoord0.v3[i] = uvw;
    }

    // Update the currently moving verts.
    int iVert0 = m_knots[iKnot0].iVert;
    for (int i = iVert0; i < numUvws; i++) {
      Vector3 uvw = m_geometry.m_Texcoord0.v3[i];
      uvw[2] = Time.time;
      m_geometry.m_Texcoord0.v3[i] = uvw;
    }

    // Construct a list of the tube centers.
    var tubeCenters = new List<Vector3>();
    var radii = new List<float>();
    int numVerts = m_geometry.m_Vertices.Count;
    for (int i = kVertsInClosedCircle - 1; i < numVerts - kVertsInClosedCircle + 1; i += kVertsInClosedCircle) {
      Vector3 vertexSumCircle = new Vector3(0, 0, 0);
      for (int j = 0; j < kVertsInClosedCircle - 1; j++) {
        vertexSumCircle += m_geometry.m_Vertices[i + j];
      }
      vertexSumCircle /= kVertsInClosedCircle - 1;
      tubeCenters.Add(vertexSumCircle);
      radii.Add(Vector3.Distance(vertexSumCircle, m_geometry.m_Vertices[i]));
    }

    // Calculate volume.
    float volume = 0;
    int numCircles = tubeCenters.Count;
    for (int i = 1; i < numCircles; i++) {
      volume += Vector3.Distance(tubeCenters[i], tubeCenters[i - 1]) *
        Mathf.PI * (radii[i] + radii[i - 1]);
    }
    float radius = Mathf.Pow(0.75f * volume / Mathf.PI, 1.0f / 3.0f);
    GetComponent<MeshRenderer>().material.SetFloat("_Radius", radius);

    // Find geometry center.
    Vector3 vertexSum = new Vector3(0, 0, 0);
    for (int i = 0; i < numVerts; i++) {
      vertexSum += m_geometry.m_Vertices[i];
    }
    vertexSum /= numVerts;
    GetComponent<MeshRenderer>().material.SetVector("_BubbleCenter", vertexSum);
  }

  override public void FinalizeSolitaryBrush() {
    int numVerts = m_geometry.m_Vertices.Count;

    // Store original geometry positions.
    for (int i = 0; i < numVerts; i++) {
      m_geometry.m_Texcoord1.v4[i] = m_geometry.m_Vertices[i];
    }

    for (int smoothPass = 0; smoothPass < 2; smoothPass++) {
      // Smooth center tube.
      for (int i = numVerts - kVertsInClosedCircle; i > kVertsInClosedCircle - 2; i--) {
        int prevIndex = i - kVertsInClosedCircle;
        if (prevIndex < 0) {
          prevIndex = 0;
        }
        int nextIndex = i + kVertsInClosedCircle;
        if (nextIndex > numVerts - 1) {
          nextIndex = numVerts - 1;
        }
        m_geometry.m_Vertices[i] = 0.5f * m_geometry.m_Vertices[prevIndex] + 0.5f * m_geometry.m_Vertices[nextIndex];
      }

      // Smooth first endcap.
      Vector3 vertexSum = new Vector3(0, 0, 0);
      for (int i = kVertsInClosedCircle - 1; i < 2 * kVertsInClosedCircle - 1; i++) {
        vertexSum += m_geometry.m_Vertices[i];
      }
      vertexSum /= kVertsInClosedCircle;
      for (int i = 0; i < kVertsInClosedCircle - 1; i++) {
        m_geometry.m_Vertices[i] = vertexSum;
      }

      // Smooth last endcap.
      vertexSum[0] = vertexSum[1] = vertexSum[2] = 0.0f;
      for (int i = numVerts - 2 * kVertsInClosedCircle + 1; i < numVerts - kVertsInClosedCircle + 1; i++) {
        vertexSum += m_geometry.m_Vertices[i];
      }
      vertexSum /= kVertsInClosedCircle;
      for (int i = numVerts - kVertsInClosedCircle + 1; i < numVerts; i++) {
        m_geometry.m_Vertices[i] = vertexSum;
      }
    }

    GetComponent<MeshRenderer>().material.SetFloat("_ReleaseTime", Time.time);

    base.FinalizeSolitaryBrush();
  }

}
}  // namespace TiltBrush
