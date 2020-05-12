# Copyright 2020 The Tilt Brush Authors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Code for creating a BVH out of Tilt Brush data
# Usage:
#   RTree.from_bounds_iter()

import os
import struct
import sys
from cStringIO import StringIO

try:
  import rtree
except ImportError:
  print "You need to install rtree (https://pypi.org/project/Rtree/)."
  sys.exit(1)


# ---------------------------------------------------------------------------
# BBox
# ---------------------------------------------------------------------------

class BBox(tuple):
  @staticmethod
  def union(lhs, rhs):
    lmin, lmax = lhs
    rmin, rmax = rhs
    nmin = ( min(lmin[0], rmin[0]),
             min(lmin[1], rmin[1]),
             min(lmin[2], rmin[2]) )
    nmax = ( max(lmax[0], rmax[0]),
             max(lmax[1], rmax[1]),
             max(lmax[2], rmax[2]) )
    for i in xrange(3):
      assert nmax[i] > nmin[i], self
    return BBox((nmin, nmax))

  def half_width(self):
    bmin, bmax = self
    return ((bmax[0]-bmin[0]) * .5,
            (bmax[1]-bmin[1]) * .5,
            (bmax[2]-bmin[2]) * .5)

  def surface_area(self):
    dx, dy, dz = self.half_width()
    # * 4 because these are half-widths; * 2 because there are 2 faces per axis
    ret = (dx * dy + dy * dz + dz * dx) * 8
    return ret

# ---------------------------------------------------------------------------
# RTree
# ---------------------------------------------------------------------------

# Tunables
INDEX_CAPACITY = 80
LEAF_CAPACITY = 410

def str_vec3(vec3):
  return "(%6.1f %6.1f %6.1f)" % vec3

def str_bounds(bounds):
  #return "(%s, %s)" % (str_vec3(bounds[0]), str_vec3(bounds[1]))
  bmin, bmax = bounds
  halfsize = (bmax[0]-bmin[0], bmax[1]-bmin[1], bmax[2]-bmin[2])
  return "(%5.1f %5.1f %5.1f)" % halfsize


class BinaryReader(object):
  # Wraps struct.unpack
  def __init__(self, inf):
    if isinstance(inf, bytes):
      inf = StringIO(inf)
    self.inf = inf

  def read(self, fmt):
    fmt_size = struct.calcsize(fmt)
    return struct.unpack(fmt, self.inf.read(fmt_size))

  def read_bounds(self):
    bmin = self.read("<3d")
    bmax = self.read("<3d")
    return (bmin, bmax)


class RTreeStorageDict(rtree.index.CustomStorage):
  def __init__(self):
    self.datas = {}
    self.cached_nodes = {}

  # CustomStorage API

  def flush(self, returnError):
    # print "flush"
    pass
  def create(self, returnError):
    # print "create"
    pass
  def destroy(self, returnError):
    # print "destroy"
    pass

  def loadByteArray(self, page, returnError):
    print "RTreeStorageDict: load", page
    assert page >= 0
    try:
      data = self.datas[page]
      assert data != 'deleted'
    except KeyError:
      returnError.contents.value = self.InvalidPageError

  def storeByteArray(self, page, data, returnError):
    if page == self.NewPage:
      page = len(self.datas)
      self.datas[page] = data
      # print "store new %s %s" % (page, len(data))
    else:
      assert page in self.datas
      old_data = self.datas[page]
      self.datas[page] = data
      # print "store %s %s -> %s" % (page, len(old_data), len(data))

    return page

  def deleteByteArray(self, page, returnError):
    # print "RTreeStorageDict: delete", page
    assert page in self.datas
    self.datas[page] = 'deleted'
    
class RTree(object):
  @classmethod
  def from_bounds_iter(cls, bounds_iter, leaf_capacity_multiplier=1):
    storage = RTreeStorageDict()
    p = rtree.index.Property()
    p.dimension = 3
    # p.variant = rtree.index.RT_Star
    p.index_capacity = INDEX_CAPACITY
    p.leaf_capacity = int(LEAF_CAPACITY * leaf_capacity_multiplier)
    index = rtree.index.Index(storage, bounds_iter, interleaved=True, properties=p)
    # Must close in order to flush changes to the storage
    index.close()
    return cls(storage, 1)

  def __init__(self, storage, header_id=1):
    self.header = None          # RTreeHeader
    self.root = None            # RTreeNode
    self.nodes_by_id = {}       # dict<int, RTreeNode>

    self.header = RTreeHeader(storage.datas[header_id])
    self.root = self._recursive_create_node(storage, self.header.rootId)

  def _recursive_create_node(self, storage, node_id):
    node_data = storage.datas[node_id]
    assert node_data != 'deleted'
    node = self.nodes_by_id[node_id] = RTreeNode(node_id, node_data)
    if node.is_index():
      for c in node.children:
        assert c.data is None
        c.node = self._recursive_create_node(storage, c.id)
      
    return node

  def dfs_iter(self):
    from collections import deque
    q = deque([self.root])
    while q:
      n = q.popleft()
      yield n
      if n.is_index():
        q.extend(c.node for c in n.children)


class RTreeHeader(object):
  # RTree::storeHeader
  #  id_type    root
  #  u32        RTreeVariant (0 linear, 1 quadratic, 2 rstar)
  #  double     fill factor
  #  u32        index capacity
  #  u32        leaf capacity
  #  u32        nearMinimumOverlapFactor
  #  double     m_splitDistributionFactor
  #  double     m_reinsertFactor
  #  u32        m_dimension
  #  char       m_bTightMBRs
  #  u32        m_stats.m_nodes
  #  u64        m_stats.m_data
  #  u32        m_stats.m_treeHeight
  #  height * u32 nodes in level
  def __init__(self, data):
    """storage: a RTreeStorageDict instance"""
    reader = BinaryReader(data)
    (self.rootId,
     self.variant, self.fill, self.icap, self.lcap,
     self.nearMinimumOverlapFactor,
     self.splitDistributionFactor,
     self.reinsertFactor,
     self.dimension,
     self.bTightMBRs,
     self.nNodes,
     self.nData,
     self.treeHeight) = reader.read("<QIdIIIddI?IQI")
    fmt = "<%dI" % self.treeHeight
    self.nodesInLevel = reader.read(fmt)

  def as_str(self):
    return "RTree variant=%s  index nodes=%s  data items=%s  height=%s" % (
      self.variant, self.nNodes, self.nData, self.treeHeight)


class CannotSplit(Exception):
  pass
class RTreeNode(object):
  # for nodeType
  PERSISTENT_INDEX = 1
  PERSISTENT_LEAF = 2

  GENERATED_SPLIT_ID = -1

  # Node::storeToByteArray
  #  u32  PersistentLeaf (level 0) or PersistentIndex (otherwise)
  #  u32  level
  #  u32  nchildren
  #  nchild * (
  #    bounds
  #    id_type
  #    u32-length-prefixed data for child)
  #  bounds
  def __init__(self, node_id, inf_or_data):
    self.node_id = node_id
    reader = BinaryReader(inf_or_data)
    (self.nodeType, self.level, nChildren) = reader.read("III")
    self.children = [ RTreeChild(reader) for i in xrange(nChildren) ]
    self.bounds = reader.read_bounds()

  def is_index(self):
    return self.nodeType == self.PERSISTENT_INDEX

  def is_leaf(self):
    return self.nodeType == self.PERSISTENT_LEAF

  def collapse(self, i1, i2):
    """Move all children from i2 to i1, update i1's bounding box,
    and remove i2."""
    assert self.is_index()
    c1 = self.children[i1]
    c2 = self.children[i2]
    assert c1.node.is_leaf()
    assert c2.node.is_leaf()
    new_bounds = BBox.union(c1.bounds, c2.bounds)
    c1.bounds = c1.node.bounds = new_bounds
    c1.node.children.extend(c2.node.children)
    del self.children[i2]

  def resplit(self, multiplier):
    # Returns an list of RTreeChild instances. For each yielded value v:
    #   isinstance(v, RTreeChild) == True
    #   v.node.is_leaf() == True
    bounds = []
    def get_all_bounds(node, bounds):
      if node.is_leaf():
        for c in node.children:
          bounds.append((c.id, c.bounds[0] + c.bounds[1], None))
      else:
        for c in node.children:
          get_all_bounds(c.node, bounds)
    get_all_bounds(self, bounds)

    new_node = RTree.from_bounds_iter(bounds, multiplier).root
    if new_node.is_leaf():
      raise CannotSplit()
    def iter_leaf_children(node):
      for c in node.children:
        if c.node.is_leaf():
          c.id = c.node.node_id = RTreeNode.GENERATED_SPLIT_ID
          RTreeNode.GENERATED_SPLIT_ID -= 1
          yield c
        else:
          for leaf in iter_leaf_children(c.node):
            yield c

    lst = list(iter_leaf_children(new_node))
    for elt in lst:
      assert isinstance(elt, RTreeChild)
      assert elt.node.is_leaf()
    return lst

  def as_str(self):
    return "id=%3d nc=%3d %s" % (
      self.node_id, len(self.children),
      str_bounds(self.bounds))


class RTreeChild(object):
  # .id
  #   If parent is PERSISTENT_INDEX, this is a node id.
  #   If parent is PESISTENT_LEAF, this is some leaf-specific id (like a stroke number)
  def __init__(self, reader):
    self.bounds = reader.read_bounds()
    self.id, data_len = reader.read("<QI")
    if data_len > 0:
      self.data = reader.read("%ds" % data_len)
    else:
      self.data = None
    self.node = None

  def as_str(self):
    description = ''
    if self.data is not None:
      description = " + %d bytes" % len(self.data)
    return "leaf %4d: %s%s" % (self.id, str_bounds(self.bounds), description)
