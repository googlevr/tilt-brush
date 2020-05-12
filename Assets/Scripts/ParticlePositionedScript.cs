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

public class ParticlePositionedScript : MonoBehaviour {
  public GameObject m_ObjectPrefab;

  public int m_NumObjects;
  public class ManagedObject {
    public GameObject m_Object;
    public TrailRenderer m_TrailRenderer;
    public bool m_Enabled;
  }
  private ManagedObject[] m_Objects;
  private bool[] m_ObjectUpdated;
  private Dictionary<uint, int> m_ObjectParticleMap;
  private ParticleSystem.Particle[] m_Particles;
  private float m_BaseTrailTime;

  void Awake() {
    m_Objects = new ManagedObject[m_NumObjects];
    m_ObjectUpdated = new bool[m_NumObjects];
    PopulateObjects();

    m_ObjectParticleMap = new Dictionary<uint, int>();
    m_Particles = new ParticleSystem.Particle[m_NumObjects];
  }

  void Update() {
    UpdateObjects();
  }

  protected void UpdateObjects() {
    //reset flag table
    for (int i = 0; i < m_NumObjects; ++i) {
      m_ObjectUpdated[i] = false;
    }

    //get particles
    int iNumParticles = GetComponent<ParticleSystem>().GetParticles(m_Particles);

    //run through the particles and look for the correlating entry in the hash map
    for (int i = 0; i < iNumParticles; ++i) {
      int iObjectIndex = -1;
      if (!m_ObjectParticleMap.ContainsKey(m_Particles[i].randomSeed)) {
        //couldn't be found-- add a new entry
        iObjectIndex = GetOpenIndex();
        if (iObjectIndex != -1) {
          m_ObjectParticleMap.Add(m_Particles[i].randomSeed, iObjectIndex);
        }
      } else {
        iObjectIndex = m_ObjectParticleMap[m_Particles[i].randomSeed];
      }

      if (iObjectIndex != -1) {
        //turn this guy on if he's new
        ManagedObject rObj = m_Objects[iObjectIndex];
        if (!rObj.m_Enabled) {
          rObj.m_Object.SetActive(true);
          rObj.m_Enabled = true;
        }

        //set position
        rObj.m_Object.transform.position = m_Particles[i].position;
        if (rObj.m_TrailRenderer) {
          //update trail renderer
          float fToStart = m_Particles[i].startLifetime - m_Particles[i].remainingLifetime;
          float fToEnd = m_Particles[i].remainingLifetime;
          float fMinDist = Mathf.Min(Mathf.Min(fToStart, fToEnd) - 0.2f, m_BaseTrailTime);
          rObj.m_TrailRenderer.time = fMinDist;
        }

        //set flag for later
        m_ObjectUpdated[iObjectIndex] = true;
      }
    }

    //run through and turn off all particles that weren't updated
    for (int i = 0; i < m_NumObjects; ++i) {
      ManagedObject rObj = m_Objects[i];
      if (!m_ObjectUpdated[i] && rObj.m_Enabled) {
        rObj.m_Object.SetActive(false);
        rObj.m_Enabled = false;
      }
    }
  }

  int GetOpenIndex() {
    for (int i = 0; i < m_NumObjects; ++i) {
      if (!m_Objects[i].m_Enabled) {
        return i;
      }
    }
    return -1;
  }

  void PopulateObjects() {
    for (int i = 0; i < m_NumObjects; ++i) {
      m_Objects[i] = new ManagedObject();
      m_Objects[i].m_Object = (GameObject)Instantiate(m_ObjectPrefab);
      m_Objects[i].m_Object.transform.parent = transform;
      m_Objects[i].m_TrailRenderer = m_Objects[i].m_Object.GetComponentInChildren<TrailRenderer>();
      if (m_Objects[i].m_TrailRenderer) {
        m_BaseTrailTime = m_Objects[i].m_TrailRenderer.time;
      }
      m_Objects[i].m_Enabled = false;
      m_Objects[i].m_Object.SetActive(false);
    }
  }
}
}  // namespace TiltBrush
