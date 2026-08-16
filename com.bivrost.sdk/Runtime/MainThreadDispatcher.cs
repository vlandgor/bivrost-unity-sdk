using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Bivrost
{
    internal class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            _instance = null; // guards against stale refs when domain reload is disabled
        }

        public static void Enqueue(Action action)
        {
            EnsureInstance();
            _actions.Enqueue(action);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("[Bivrost] MainThreadDispatcher");
            _instance = go.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            while (_actions.TryDequeue(out var action))
            {
                try { action?.Invoke(); }
                catch (Exception ex) { Debug.LogError($"[BIVROST] Main thread action error: {ex.Message}"); }
            }
        }
    }
}