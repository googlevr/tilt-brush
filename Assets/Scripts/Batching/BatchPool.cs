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
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

namespace TiltBrush {

public class BatchPool {
  private BatchManager m_owner;

  public Guid m_BrushGuid;
  public List<Batch> m_Batches;

  public BatchManager Owner { get { return m_owner; } }

  public string Name {
    get {
      var desc = BrushCatalog.m_Instance.GetBrush(m_BrushGuid);
      if (desc == null) { return m_BrushGuid.ToString(); }
      return desc.m_DurableName;
    }
  }

  public BatchPool(BatchManager owner) {
    m_owner = owner;
  }

  /// Removes batches which are provably useless.
  /// Public only for use by BatchManager.
  public void TrimBatches() {
    // The last batch _may_ be useless, but I wouldn't go so far as to say I could prove it.
    // Let's play it safe for now and avoid the potential for the last batch to be repeatedly
    // destroyed then recreated.
    for (int batchIdx = 0; batchIdx < m_Batches.Count - 1; ++batchIdx) {
      Batch batch = m_Batches[batchIdx];
      // Batch is still in use.
      if (batch.m_Groups.Count > 0) { continue; }

      m_Batches.RemoveAt(batchIdx);
      batchIdx -= 1;

      batch.Destroy();
    }
  }

  // Public only for use by BatchManager.
  public void FlushMeshUpdates() {
    foreach (var batch in m_Batches) {
      batch.FlushMeshUpdates();
    }
  }

  /// Potentially removes cached mesh data some of the batches in this pool.
  public void ClearCachedGeometryFromBatches() {
    if (BatchManager.kTimeUntilBatchImmutable > 0) {
      // Current heuristics are:
      // - Always keep the cache for the most recent kBatchesToLeaveMutable Batches
      // - Otherwise, keep the cache until kTimeUntilBatchImmutable frames after the last write
      int currentTime = m_owner.CurrentTimestamp;

      for (int i = 0; i < m_Batches.Count - BatchManager.kBatchesToLeaveMutable; ++i) {
        Batch batch = m_Batches[i];
        if (batch.Geometry.IsGeometryResident) {
          int timeSinceLastUpdate = currentTime - batch.LastMeshUpdate;
          if (timeSinceLastUpdate > BatchManager.kTimeUntilBatchImmutable) {
            batch.ClearCachedGeometry();
          } else if (timeSinceLastUpdate < 0) {
            Debug.LogWarningFormat(
                "{0} {1}: Should never happen: batch updated in the future: {2}",
                BrushCatalog.m_Instance.GetBrush(m_BrushGuid).m_Description, i,
                timeSinceLastUpdate);
            // A cheesy way of forcing timeSinceLastUpdate to 0
            batch.DelayedUpdateMesh();
          }
        }
      }
    }
  }

  /// Destroys the pool and all resources+objects owned by it.
  /// The pool is no longer usable after this.
  public void Destroy() {
    m_owner = null;

    foreach (var batch in m_Batches) {
      batch.Destroy();
    }
    m_Batches = null;
  }
}
}  // namespace TiltBrush
