namespace Bivrost
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }

    public enum StudentStatus
    {
        Idle,
        Loading,
        InProgress,
        Paused,
        Completed
    }
}