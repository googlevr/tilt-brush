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
using System;

namespace TiltBrush {

public class UndoParticleAnimScript : UndoBaseAnimScript {
  private ParticleSystem m_ParticleSystem;
  private int m_ParticleCount;
  private ParticleSystem.Particle[] m_Particles;
  private ParticleSystem.Particle[] m_WorkingParticles;

  void Awake() {
    OnAwake();
  }

  public void Init(ParticleSystem.Particle[] aParticles, int iParticleCount) {
    InitForHiding();

    m_ParticleSystem = GetComponent<ParticleSystem>();
    m_ParticleCount = iParticleCount;
    m_Particles = new ParticleSystem.Particle[m_ParticleCount];
    m_WorkingParticles = new ParticleSystem.Particle[m_ParticleCount];
    Array.Copy(aParticles, m_Particles, m_ParticleCount);
    Array.Copy(aParticles, m_WorkingParticles, m_ParticleCount);
  }

  override protected void AnimateHiding() {
    //lerp particles from base positions to target pos
    if (m_ParticleCount > 0) {
      Vector3 vTargetPos_CS = GetAnimationTarget_CS();

      // TODO: wouldn't it be better to move + scale the particle system object,
      // rather than all the particles?
      for (int i = 0; i < m_ParticleCount; ++i) {
        m_WorkingParticles[i].position = Vector3.Lerp(
            m_Particles[i].position,  // already in canvas space
            vTargetPos_CS,
            m_HiddenAmount);
        m_WorkingParticles[i].startSize = m_Particles[i].startSize * (1 - m_HiddenAmount);
      }

      m_ParticleSystem.SetParticles(m_WorkingParticles, m_ParticleCount);
    }
  }
}
}  // namespace TiltBrush
