using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Bivrost
{
    internal class UnityMainThread : MonoBehaviour
    {
        private static UnityMainThread _instance;
        private static readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null) return;
            var go = new GameObject("BivrostMainThread");
            _instance = go.AddComponent<UnityMainThread>();
            DontDestroyOnLoad(go);
        }

        public static void Enqueue(Action action)
        {
            _actions.Enqueue(action);
        }

        private void Update()
        {
            while (_actions.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BIVROST] Main thread action error: {ex.Message}");
                }
            }
        }
    }
}