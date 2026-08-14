using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Bivrost
{
    internal class UnityMainThread : MonoBehaviour
    {
        private static UnityMainThread _instance;
        private static readonly ConcurrentQueue<Action> _actions = new ConcurrentQueue<Action>();

        private void Awake()
        {
            _instance = this;
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