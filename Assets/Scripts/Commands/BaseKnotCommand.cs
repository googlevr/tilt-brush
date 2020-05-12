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

namespace TiltBrush {

/// More like "BaseModifyKnotCommand"
public class BaseKnotCommand : BaseCommand {
  public bool MergesWithCreateCommand { get; }

  public CameraPathKnot Knot { get; }

  public BaseKnotCommand(CameraPathKnot knot, bool mergesWithCreateCommand, BaseCommand parent)
      : base(parent) {
    Knot = knot;
    MergesWithCreateCommand = mergesWithCreateCommand;
  }
}

public class BaseKnotCommand<T> : BaseKnotCommand
    where T : CameraPathKnot {
  public new T Knot { get; }
  public BaseKnotCommand(
      T knot, bool mergesWithCreateCommand = false, BaseCommand parent = null)
      : base(knot, mergesWithCreateCommand, parent) {
    Knot = knot;
  }
}
} // namespace TiltBrush
