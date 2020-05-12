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
using System.Reflection;
using UnityEngine;

namespace TiltBrush {
// SerializedPropertyReference<T> allows you to serialize a reference to a property on a component
// on a gameobject. However, because you can't have a PropertyDrawer for a generic type we have
// to derive from SerializedPropertyReference<T> to make a class that can have a propertydrawer
// attached.

[Serializable]
public class SerializedPropertyReferenceBool : SerializedPropertyReference<bool> { }

[Serializable]
public class SerializedPropertyReferenceInt : SerializedPropertyReference<int> { }

[Serializable]
public class SerializedPropertyReferenceFloat : SerializedPropertyReference<float> { }

[Serializable]
public class SerializedPropertyReferenceString : SerializedPropertyReference<string> { }

[Serializable]
public class SerializedPropertyReference<T> : ISerializationCallbackReceiver {
  [SerializeField] private UnityEngine.Object m_Target;
  [SerializeField] private string m_PropertyName;

  private PropertyInfo m_Property;

  public bool HasValue {
    get { return m_Target != null && m_Property != null; }
  }

  public T Value {
    get { return (T) m_Property.GetValue(m_Target); }
    set { m_Property.SetValue(m_Target, value); }
  }

  private void Resolve() {
    if (m_Target != null && !string.IsNullOrEmpty(m_PropertyName)) {
        m_Property = m_Target.GetType().GetProperty(m_PropertyName);
    }
  }

  public void OnAfterDeserialize() {
    Resolve();
  }

  public void OnBeforeSerialize() { }
}

} // namespace TiltBrush
