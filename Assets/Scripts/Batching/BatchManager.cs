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
using System.Linq;
using UnityEngine;

namespace TiltBrush {

public class BatchManager {
  /// Leave this many batches per pool mutable
  public const int kBatchesToLeaveMutable = 1;
  /// In frames
  public const int kTimeUntilBatchImmutable = 60;

  private List<BatchPool> m_Pools;
  private Dictionary<Guid, BatchPool> m_BrushToPool;
  private Bounds m_MeshBounds;
  private CanvasScript m_owner;
  private Transform m_ParentTransform;
  private List<string> m_MaterialKeywords = new List<string>();
  private int m_CurrentTimestamp;

  static private Dictionary<ushort, Batch> sm_BatchMap = new Dictionary<ushort, Batch>();

  public CanvasScript Canvas { get { return m_owner; } }
  public List<string> MaterialKeywords {
    get { return m_MaterialKeywords;  }
  }

  /// Used by BatchPool and Batch to keep track of recent usage.
  public int CurrentTimestamp {
    get { return m_CurrentTimestamp; }
  }

  public void Init(CanvasScript owner) {
    m_Pools = new List<BatchPool>();
    m_BrushToPool = new Dictionary<Guid, BatchPool>();
    m_MeshBounds = new Bounds();
    m_MeshBounds.Expand(100.0f);
    m_owner = owner;
    m_ParentTransform = owner.transform;
    m_CurrentTimestamp = 0;
  }

  public void Update() {
    m_CurrentTimestamp += 1;
    foreach (var pool in m_Pools) {
      pool.TrimBatches();
      if (App.Config.m_EnableBatchMemoryOptimization) {
        pool.ClearCachedGeometryFromBatches();
      }
    }
  }

  /// In playmode, BatchManager takes care of flushing updates when necessary.
  /// This is useful when running in edit mode.
  public void FlushMeshUpdates() {
    foreach (var pool in m_Pools) {
      pool.FlushMeshUpdates();
    }
  }

  BatchPool GetPool(BrushDescriptor brush) {
    return GetPool(brush.m_Guid);
  }

  BatchPool GetPool(Guid brushGuid) {
    try {
      return m_BrushToPool[brushGuid];
    } catch (KeyNotFoundException) {
      BatchPool rNewPool = new BatchPool(this);
      rNewPool.m_BrushGuid = brushGuid;
      rNewPool.m_Batches = new List<Batch>();
      Batch b = Batch.Create(rNewPool, m_ParentTransform, m_MeshBounds);
      sm_BatchMap.Add(b.BatchId, b);
      m_Pools.Add(rNewPool);
      m_BrushToPool[rNewPool.m_BrushGuid] = rNewPool;
      foreach (string keyword in m_MaterialKeywords) {
        b.InstantiatedMaterial.EnableKeyword(keyword);
      }
      return rNewPool;
    }
  }

  // Returns the associated batch for the given batchId.
  // Returns null if key doesn't exist.
  public Batch GetBatch(ushort batchId) {
    if (sm_BatchMap.ContainsKey(batchId)) {
      return sm_BatchMap[batchId];
    }
    return null;
  }

  // Returns a batch such that it has space for at least nVerts
  Batch GetBatch(BatchPool pool, int nVerts) {
    if (pool.m_Batches.Count > 0) {
      var batch = pool.m_Batches[pool.m_Batches.Count - 1];
      if (batch.HasSpaceFor(nVerts)) {
        return batch;
      }
    }

    {
      Batch b = Batch.Create(pool, m_ParentTransform, m_MeshBounds);
      sm_BatchMap.Add(b.BatchId, b);
      foreach (string keyword in m_MaterialKeywords) {
        b.InstantiatedMaterial.EnableKeyword(keyword);
      }
      Debug.Assert(pool.m_Batches[pool.m_Batches.Count - 1] == b);
      return b;
    }
  }

  /// Creates and returns a new subset containing the passed geometry.
  /// Pass:
  ///   brush - Selects the material/batch
  ///   nVerts - Amount to copy from the MasterBrush
  ///   nTris - Amount to copy from MasterBrush.m_Tris
  public BatchSubset CreateSubset(BrushDescriptor brush, int nVerts, int nTris, MasterBrush geometry) {
    var pool = GetPool(brush);
    var batch = GetBatch(pool, nVerts);
    return batch.AddSubset(nVerts, nTris, geometry);
  }

  /// Creates and returns a new subset containing the passed geometry.
  /// Pass:
  ///   brush - Selects the material/batch
  ///   nVerts - Amount to copy from the MasterBrush
  public BatchSubset CreateSubset(BrushDescriptor brush, GeometryPool geometry) {
    var pool = GetPool(brush);
    var batch = GetBatch(pool, geometry.NumVerts);
    return batch.AddSubset(geometry);
  }

  /// Creates and returns a new subset containing the passed geometry.
  /// Pass:
  ///   otherSubset -
  ///     Specifies both the material/batch to copy into,
  ///     as well as the geometry to copy.
  ///     May be owned by a different BatchManager.
  ///   leftTransform - optional transform to transform the subset.
  public BatchSubset CreateSubset(BatchSubset otherSubset, TrTransform? leftTransform = null) {
    Batch otherBatch = otherSubset.m_ParentBatch;
    BatchPool otherPool = otherBatch.ParentPool;

    var pool = GetPool(otherPool.m_BrushGuid);
    var batch = GetBatch(pool, otherSubset.m_VertLength);
    return batch.AddSubset(
        otherBatch.Geometry,
        otherSubset.m_StartVertIndex, otherSubset.m_VertLength,
        otherSubset.m_iTriIndex, otherSubset.m_nTriIndex,
        leftTransform);
  }

  public void SetVisibility(bool visibility) {
    foreach (BatchPool pool in m_Pools) {
      foreach (Batch batch in pool.m_Batches) {
        batch.GetComponent<Renderer>().enabled = visibility;
      }
    }
  }

  public void ResetPools() {
    foreach (BatchPool pool in m_Pools) {
      pool.Destroy();
    }
    m_Pools.Clear();
    m_BrushToPool.Clear();
  }

  public BatchPool GetBatchPool(int iPool) {
    if (iPool < 0) { throw new ArgumentException(); }
    // Historical: keep semantics of previous version of this code
    return (iPool < m_Pools.Count) ? m_Pools[iPool] : null;
  }

  public int GetNumBatchPools() {
    return m_Pools.Count;
  }

  public Bounds GetBoundsOfAllStrokes(bool onlyActive = false) {
    Bounds rBatchBounds = new Bounds();
    bool bBatchInitialized = false;

    //run through each vert group in each stroke in each pool and total up their bounds
    foreach (BatchPool pool in m_Pools) {
      for (int i = 0; i < pool.m_Batches.Count; ++i) {
        Batch batch = pool.m_Batches[i];
        for (int j = 0; j < batch.m_Groups.Count; ++j) {
          if (!batch.m_Groups[j].m_Active && onlyActive) {
            continue;
          }
          if (!bBatchInitialized) {
            //if this is the first group we're looking at, just start with these bounds
            rBatchBounds = batch.m_Groups[j].m_Bounds;
            bBatchInitialized = true;
          } else {
            rBatchBounds.Encapsulate(batch.m_Groups[j].m_Bounds);
          }
        }
      }
    }

    return rBatchBounds;
  }

  /// Like BaseBrushScript.CloneAsUndoObject(), except:
  /// - to avoid waste, use a pre-instantiated object
  /// - assume object already has UndoMeshAnimScript, doesn't have BaseBrushScript
  public void CloneAsUndoObject(BatchSubset subset, GameObject clone) {
    // GameObject clone = Instantiate<GameObject>(...);  premade for us
    // clone.name = ...;
    subset.m_ParentBatch.CopyToMesh(subset, clone);
    clone.transform.parent = m_ParentTransform;
    Coords.AsLocal[clone.transform] = TrTransform.identity;
    clone.SetActive(true);
    clone.GetComponent<UndoMeshAnimScript>().Init();
  }

  public IEnumerable<Batch> AllBatches() {
    for (int iPool = 0; iPool < m_Pools.Count; ++iPool) {
      var pool = m_Pools[iPool];
      for (int iBatch = 0; iBatch < pool.m_Batches.Count; ++iBatch) {
        yield return pool.m_Batches[iBatch];
      }
    }
  }

  public void RegisterHighlight() {
    foreach (var batch in AllBatches()) {
      batch.RegisterHighlight();
    }
  }

  public int CountBatches() {
    return m_Pools.Select(pool => pool.m_Batches.Count).Sum();
  }

  public int CountAllBatchTriangles() {
    return AllBatches().Select(batch => batch.Geometry.NumTriIndices / 3).Sum();
  }

  public int CountAllBatchVertices() {
    return AllBatches().Select(batch => batch.Geometry.NumVerts).Sum();
  }
}
} // namespace TiltBrush
