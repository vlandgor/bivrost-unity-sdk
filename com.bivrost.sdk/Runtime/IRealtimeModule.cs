using System;
using UnityEngine;

namespace Bivrost
{
    public interface IRealtimeModule
    {
        bool IsConnected { get; }
        void Connect(string url, string token, BivrostEvents events);
        void Disconnect();
        void PublishCamera(Camera camera, int width, int height, int framerate, int bitrate = 512000);
        void PublishMicrophone();
    }

    public static class RealtimeModuleRegistry
    {
        private static Func<BivrostManager, IRealtimeModule> _factory;

        public static bool IsRegistered => _factory != null;

        public static void Register(Func<BivrostManager, IRealtimeModule> factory)
        {
            if (_factory != null)
            {
                Debug.LogWarning("[BIVROST] Realtime module factory already registered — overwriting previous registration.");
            }

            _factory = factory;
        }

        internal static IRealtimeModule Create(BivrostManager manager)
        {
            return _factory?.Invoke(manager);
        }
    }
}