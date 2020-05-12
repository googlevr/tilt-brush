//
// Reaktion - An audio reactive animation toolkit for Unity.
//
// Copyright (C) 2013, 2014 Keijiro Takahashi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using UnityEngine;

namespace Reaktion {

// A class for managing a remote controlled value.
[System.Serializable]
public class Remote
{
    // Control source type.
    public enum Control { Off, InputAxis }
    [SerializeField] Control _control = Control.Off;

    // Joystick input settings.
    [SerializeField] string _inputAxis = "Jump";

    // Amplitude curve.
    [SerializeField] AnimationCurve _curve = AnimationCurve.Linear(0, 0, 1, 1);

    // Default value.
    float _defaultLevel;

    // Current value.
    float _level;
    public float level { get { return _level; } }

    public void Reset(float defaultLevel)
    {
        _defaultLevel = defaultLevel;
        Update();
    }

    public void Update()
    {
        if (_control == Control.Off)
        {
            _level = _defaultLevel;
        }
        else // _control == Control.InputAxis
        {
            if (string.IsNullOrEmpty(_inputAxis))
                _level = _defaultLevel;
            else
                _level = Input.GetAxis(_inputAxis);
        }
        _level = _curve.Evaluate(_level);
    }
}

} // Reaktion
