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

using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush {

internal class TestTransformExtensions : MathTestUtils {
  Transform RandomTransform(Transform root=null, int num=1) {
    Transform parent = root;
    for (int i = 0; i < num; ++i) {
      var child = new GameObject("unittest").transform;
      child.parent = parent;
      RandomTr().ToLocalTransform(child);
      parent = child;
    }
    return parent;
  }

  // Concatenating long chains of transforms loses lots of precision
  static void VeryLooseEqual(TrTransform lhs, TrTransform rhs,
                             float translationEps=3e-2f) {
    if (   ! AlmostEqual(lhs.rotation, rhs.rotation, 1e-4f, 1e-4f, false)
        || ! AlmostEqual(lhs.translation, rhs.translation, translationEps, 1e-3f)
        || ! AlmostEqual(lhs.scale, rhs.scale, 1e-4f, 1e-4f)) {
      Assert.Fail("{0}\n  !~\n{1}", lhs, rhs);
    }
  }


  [Test]
  public void TestGetSetUniformScale() {
    Transform parent = new GameObject("").transform;
    Transform child = new GameObject("").transform;
    float PARENT_LOCAL_SCALE = 4;
    float CHILD_LOCAL_SCALE = .5f;
    child.parent = parent;
    parent.localRotation = Quaternion.Euler(18,23,47);
    parent.localScale = Vector3.one * PARENT_LOCAL_SCALE;
    child.localRotation = Quaternion.Euler(47,18,23);
    child.localScale = Vector3.one * CHILD_LOCAL_SCALE;

    float desired = PARENT_LOCAL_SCALE * CHILD_LOCAL_SCALE;
    float actual = child.GetUniformScale();
    // This fails because of the internal implementation of lossyScale
    // Assert.AreEqual(child.lossyScale.x, actual);
    AssertAlmostEqual(child.lossyScale.x, actual);
    // This succeeds because GetUniformScale() tries to be more precise
    Assert.AreEqual(desired, actual);  // not almost equal; exactly equal

    float CHILD_LOCAL_SCALE2 = 3;
    float CHILD_GLOBAL_SCALE2 = CHILD_LOCAL_SCALE2 * PARENT_LOCAL_SCALE;
    child.SetUniformScale(CHILD_GLOBAL_SCALE2);
    Assert.AreEqual(child.localScale.x, CHILD_LOCAL_SCALE2);
    Assert.AreEqual(child.GetUniformScale(), CHILD_GLOBAL_SCALE2);
    AssertAlmostEqual(child.lossyScale.x, CHILD_GLOBAL_SCALE2);

    UnityEngine.Object.DestroyImmediate(parent.gameObject);
  }

  [Test]
  public void TestGlobalAccessor() {
    var AsGlobal = new TransformExtensions.GlobalAccessor();
    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var xfGlobal = RandomTr();

    AsGlobal[child] = xfGlobal;
    VeryLooseEqual(xfGlobal, TrTransform.FromTransform(child));
    VeryLooseEqual(xfGlobal, AsGlobal[child]);

    UnityEngine.Object.DestroyImmediate(root.gameObject);
  }

  [Test]
  public void TestLocalAccessor() {
    var AsLocal = new TransformExtensions.LocalAccessor();

    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var xfLocal = RandomTr();

    AsLocal[child] = xfLocal;
    AssertAlmostEqual(xfLocal, TrTransform.FromLocalTransform(child));
    AssertAlmostEqual(xfLocal, AsLocal[child]);

    UnityEngine.Object.DestroyImmediate(root.gameObject);

  }

  [Test]
  public void TestRelativeAccessor_SameGet() {
    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var AsChild = new TransformExtensions.RelativeAccessor(child);
    // If the more precise implementation is used, Assert.AreEqual works
    VeryLooseEqual(TrTransform.identity, AsChild[child]);
    UnityEngine.Object.DestroyImmediate(root.gameObject);
  }

  [Test]
  public void TestRelativeAccessor_SameSet() {
    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var AsChild = new TransformExtensions.RelativeAccessor(child);
    try {
      Assert.That(() => AsChild[child] = RandomTr(), Throws.InvalidOperationException);
    } finally {
      UnityEngine.Object.DestroyImmediate(root.gameObject);
    }
  }

  [Test]
  public void TestRelativeAccessor_ParentGet() {
    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var AsParent = new TransformExtensions.RelativeAccessor(child.parent);
    // If the more precise implementation is used, Assert.AreEqual works
    VeryLooseEqual(TrTransform.FromLocalTransform(child), AsParent[child]);
    UnityEngine.Object.DestroyImmediate(root.gameObject);
  }

  [Test]
  public void TestRelativeAccessor_ParentSet() {
    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var AsParent = new TransformExtensions.RelativeAccessor(child.parent);
    var xf = RandomTr();
    AsParent[child] = xf;
    VeryLooseEqual(AsParent[child], xf);
    VeryLooseEqual(TrTransform.FromTransform(child),
                   TrTransform.FromTransform(child.parent) * xf);
    UnityEngine.Object.DestroyImmediate(root.gameObject);
  }

  [Test]
  public void TestRelativeAccessor_AncestorGet() {
    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var AsAncestor = new TransformExtensions.RelativeAccessor(child.parent.parent);
    VeryLooseEqual(TrTransform.FromLocalTransform(child.parent) *
                   TrTransform.FromLocalTransform(child),
                   AsAncestor[child]);
    UnityEngine.Object.DestroyImmediate(root.gameObject);
  }

  [Test]
  public void TestRelativeAccessor_AncestorSet() {
    var root = RandomTransform();
    var child = RandomTransform(root, 2);
    var AsAncestor = new TransformExtensions.RelativeAccessor(child.parent.parent);
    var xf = RandomTr();
    AsAncestor[child] = xf;
    VeryLooseEqual(AsAncestor[child], xf);
    VeryLooseEqual(TrTransform.FromTransform(child.parent.parent) * xf,
                   TrTransform.FromTransform(child));
    UnityEngine.Object.DestroyImmediate(root.gameObject);
  }

  [Test]
  public void TestRelativeAccessor_UnrelatedGet() {
    var root = RandomTransform();
    var left = RandomTransform(root, 2);
    var right = RandomTransform(root);
    var AsRight = new TransformExtensions.RelativeAccessor(right);
    VeryLooseEqual(TrTransform.FromTransform(right) * AsRight[left],
                   TrTransform.FromTransform(left));
  }

  [Test]
  public void TestRelativeAccessor_UnrelatedSet() {
    var root = RandomTransform();
    var left = RandomTransform(root, 2);
    var right = RandomTransform(root);
    var AsRight = new TransformExtensions.RelativeAccessor(right);
    var xf = RandomTr();
    AsRight[left] = xf;
    VeryLooseEqual(AsRight[left], xf);
    VeryLooseEqual(TrTransform.FromTransform(right) * xf,
                   TrTransform.FromTransform(left));
  }

}

}
