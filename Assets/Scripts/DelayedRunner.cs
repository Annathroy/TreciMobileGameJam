using UnityEngine;
using System;
using System.Collections;

[DefaultExecutionOrder(-10000)]
public class DelayedRunner : MonoBehaviour
{
    private static DelayedRunner _instance;

    public static void Run(IEnumerator routine)
    {
        if (_instance == null)
        {
            var go = new GameObject("~DelayedRunner");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DelayedRunner>();
        }
        _instance.StartCoroutine(routine);
    }

    public static void Run(Action action, float delay)
    {
        Run(_instance.ExecuteAfter(action, delay));
    }

    private IEnumerator ExecuteAfter(Action action, float delay)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }
}
