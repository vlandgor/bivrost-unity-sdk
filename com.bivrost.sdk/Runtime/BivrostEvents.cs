using System;
using System.Collections.Generic;

namespace Bivrost
{
    public class BivrostEvents
    {
        public event Action OnConnected;
        public event Action<string> OnDisconnected; // reason
        public event Action<string> OnError; // error message
        public event Action<ConnectionState> OnStateChanged;

        // Instructor -> Student actions. Fires for every incoming action
        // regardless of key. Prefer On(key, handler) below if you only
        // care about one specific action.
        public event Action<BivrostAction> OnActionReceived;
        public event Action<string> OnInstructorMessage; // text message

        // Voice channel
        public event Action OnVoiceChannelStarted;
        public event Action OnVoiceChannelEnded;

        // Session lifecycle
        public event Action OnSessionStarted;
        public event Action OnSessionEnded;

        private readonly Dictionary<string, Action<BivrostAction>> _actionHandlers =
            new Dictionary<string, Action<BivrostAction>>();

        /// <summary>
        /// Subscribe to one specific instructor->student action by key (e.g.
        /// "turn_off_light"), as set up on the Bivrost web platform's Actions
        /// page. Avoids every listener having to filter OnActionReceived by hand.
        /// </summary>
        public void On(string key, Action<BivrostAction> handler)
        {
            if (string.IsNullOrEmpty(key) || handler == null) return;

            _actionHandlers[key] = _actionHandlers.TryGetValue(key, out var existing)
                ? existing + handler
                : handler;
        }

        public void Off(string key, Action<BivrostAction> handler)
        {
            if (string.IsNullOrEmpty(key) || handler == null) return;
            if (!_actionHandlers.TryGetValue(key, out var existing)) return;

            existing -= handler;
            if (existing == null)
                _actionHandlers.Remove(key);
            else
                _actionHandlers[key] = existing;
        }

        internal void RaiseConnected() => OnConnected?.Invoke();
        internal void RaiseDisconnected(string reason) => OnDisconnected?.Invoke(reason);
        internal void RaiseError(string error) => OnError?.Invoke(error);
        internal void RaiseStateChanged(ConnectionState state) => OnStateChanged?.Invoke(state);

        internal void RaiseActionReceived(BivrostAction action)
        {
            if (action == null) return;

            OnActionReceived?.Invoke(action);

            if (_actionHandlers.TryGetValue(action.Key, out var handler))
                handler.Invoke(action);
        }

        internal void RaiseInstructorMessage(string message) => OnInstructorMessage?.Invoke(message);
        internal void RaiseVoiceChannelStarted() => OnVoiceChannelStarted?.Invoke();
        internal void RaiseVoiceChannelEnded() => OnVoiceChannelEnded?.Invoke();
        internal void RaiseSessionStarted() => OnSessionStarted?.Invoke();
        internal void RaiseSessionEnded() => OnSessionEnded?.Invoke();
    }
}