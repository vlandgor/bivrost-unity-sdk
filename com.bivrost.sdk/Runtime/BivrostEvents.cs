using System;

namespace Bivrost
{
    public class BivrostEvents
    {
        public event Action OnConnected;
        public event Action<string> OnDisconnected; // reason
        public event Action<string> OnError; // error message
        public event Action<ConnectionState> OnStateChanged;

        // Instructor commands
        public event Action<string> OnInstructorCommand; // command type
        public event Action<string> OnInstructorMessage; // text message

        // Voice channel
        public event Action OnVoiceChannelStarted;
        public event Action OnVoiceChannelEnded;

        // Session lifecycle
        public event Action OnSessionStarted;
        public event Action OnSessionEnded;

        internal void RaiseConnected() => OnConnected?.Invoke();
        internal void RaiseDisconnected(string reason) => OnDisconnected?.Invoke(reason);
        internal void RaiseError(string error) => OnError?.Invoke(error);
        internal void RaiseStateChanged(ConnectionState state) => OnStateChanged?.Invoke(state);
        internal void RaiseInstructorCommand(string command) => OnInstructorCommand?.Invoke(command);
        internal void RaiseInstructorMessage(string message) => OnInstructorMessage?.Invoke(message);
        internal void RaiseVoiceChannelStarted() => OnVoiceChannelStarted?.Invoke();
        internal void RaiseVoiceChannelEnded() => OnVoiceChannelEnded?.Invoke();
        internal void RaiseSessionStarted() => OnSessionStarted?.Invoke();
        internal void RaiseSessionEnded() => OnSessionEnded?.Invoke();
    }
}