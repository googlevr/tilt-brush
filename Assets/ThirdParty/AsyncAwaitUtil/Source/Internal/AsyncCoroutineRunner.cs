#define HAVE_EDITOR_COROUTINES
using System;
using System.Collections;

using UnityEngine;
#if UNITY_EDITOR && HAVE_EDITOR_COROUTINES
using Unity.EditorCoroutines.Editor;
#endif

namespace UnityAsyncAwaitUtil
{
public class AsyncCoroutineRunner : MonoBehaviour, AsyncCoroutineRunner.ICoroutineRunner
{
  public interface ICoroutineRunner
  {
    object StartCoroutine(IEnumerator routine);
  }

#if UNITY_EDITOR
  class EditorAsyncCoroutineRunner : ICoroutineRunner
  {
    object ICoroutineRunner.StartCoroutine(IEnumerator routine)
    {
#if HAVE_EDITOR_COROUTINES
                return EditorCoroutineUtility.StartCoroutine(routine, this);
#elif UNITY_2019_1_OR_NEWER
                throw new NotImplementedException("Install package com.unity.editorcoroutines");
#else
      // asmdef "Version Defines" support doesn't exist yet
      throw new NotImplementedException("Install package com.unity.editorcoroutines and define HAVE_EDITOR_COROUTINES");
#endif
    }
  }
#endif

  static ICoroutineRunner _instance;

  public static ICoroutineRunner Instance
  {
    get
    {
#if UNITY_EDITOR
      if (_instance == null && !Application.isPlaying)
      {
        _instance = new EditorAsyncCoroutineRunner();
      }
#endif
      if (_instance == null)
      {
        _instance = new GameObject("AsyncCoroutineRunner")
            .AddComponent<AsyncCoroutineRunner>();
      }

      return _instance;
    }
  }

  void Awake()
  {
    // Don't show in scene hierarchy
    gameObject.hideFlags = HideFlags.HideAndDontSave;

    DontDestroyOnLoad(gameObject);
  }

  object ICoroutineRunner.StartCoroutine(IEnumerator routine)
  {
    return StartCoroutine(routine);
  }
}
}