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

using System.Threading;
using System.Threading.Tasks;

namespace TiltBrush {
/// A class for containing a task and its cancellation token source.
public class TaskAndCts {
  public Task Task;
  public CancellationTokenSource Cts;

  public CancellationToken Token => Cts.Token;

  public TaskAndCts() {
    Cts = new CancellationTokenSource();
  }

  public void Cancel() {
    Cts.Cancel();
  }
}

/// A class for containing a task and its cancellation token source.
public class TaskAndCts<T> {
  public Task<T> Task;
  public CancellationTokenSource Cts;

  public CancellationToken Token => Cts.Token;

  public TaskAndCts() {
    Cts = new CancellationTokenSource();
  }

  public void Cancel() {
    Cts.Cancel();
  }
}
} // namespace TiltBrush