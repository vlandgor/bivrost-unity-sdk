namespace Bivrost
{
    public class BivrostConfig
    {
        public string ServerUrl { get; set; }
        public string SessionId { get; set; }
        public string StudentName { get; set; }
        public int HeartbeatIntervalMs { get; set; } = 5000;
        public int ReconnectAttempts { get; set; } = 5;
        public int ReconnectDelayMs { get; set; } = 3000;
    }
}