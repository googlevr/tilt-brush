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
using CSCore.DSP;

namespace TiltBrush {
  /// Wrapper for CSCore.DSP.HighpassFilter and LowpassFilter
  public class VisualizerCSCoreFilter : VisualizerManager.Filter {
    public enum FilterType {
      Low,
      High,
    }

    private BiQuad m_Filter;
    private double m_Frequency;

    public VisualizerCSCoreFilter(FilterType type, int sampleRate, double frequency) {
      if (type == FilterType.High) {
        m_Filter = new HighpassFilter(sampleRate, frequency);
      } else {
        m_Filter = new LowpassFilter(sampleRate, frequency);
      }
    }

    public override void Process(float[] samples) {
        m_Filter.Process(samples);
    }

    public override double Frequency {
      get { return m_Frequency; }
      set {
        m_Frequency = value;
        m_Filter.Frequency = value;
      }
    }
  }
}
