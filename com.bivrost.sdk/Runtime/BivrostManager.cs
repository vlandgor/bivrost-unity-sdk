using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Bivrost
{
    public enum BivrostEnvironment { Production, Development }

    public class BivrostManager : MonoBehaviour
    {
        private static BivrostManager _instance;

        public static BivrostManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("BivrostManager");
                    _instance = go.AddComponent<BivrostManager>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

#if BIVROST_INTERNAL
        [SerializeField] private BivrostEnvironment environment = BivrostEnvironment.Development;
#else
        private const BivrostEnvironment environment = BivrostEnvironment.Production;
#endif
        
        [Space]
        [SerializeField] private string projectId;

        public BivrostEvents Events { get; } = new BivrostEvents();
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public BivrostConfig Config { get; private set; }
        public bool IsConnected => State == ConnectionState.Connected;
        public static bool IsRealtimeModuleInstalled => RealtimeModuleRegistry.IsRegistered;

        private SocketIOManager _socketIO;
        private IRealtimeModule _liveKit;
        private float _heartbeatTimer;

        public string ServerUrl => environment switch
        {
            BivrostEnvironment.Development => "http://localhost:3001",
            BivrostEnvironment.Production  => "https://bivrost-web-platform-production.up.railway.app",
            _ => throw new System.ArgumentOutOfRangeException()
        };

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (!TryGetComponent<MainThreadDispatcher>(out _))
            {
                gameObject.AddComponent<MainThreadDispatcher>();
            }
        }

        public async Task Connect(BivrostConfig config)
        {
            if (State != ConnectionState.Disconnected)
            {
                Debug.LogWarning("[BIVROST] Already connected or connecting.");
                return;
            }

            Config = config ?? throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrEmpty(config.ServerUrl))
                throw new ArgumentException("ServerUrl is required");
            if (string.IsNullOrEmpty(config.SessionId))
                throw new ArgumentException("SessionId is required");
            if (string.IsNullOrEmpty(config.StudentName))
                throw new ArgumentException("StudentName is required");

            SetState(ConnectionState.Connecting);
            Debug.Log(
                $"[BIVROST] Connecting to {config.ServerUrl} | Session: {config.SessionId} | Student: {config.StudentName}");

            try
            {
                _socketIO = new SocketIOManager();
                await _socketIO.Connect(Config, Events);

                // LiveKit is a separate connection — call ConnectLiveKit(url, token) once you
                // have room credentials (e.g. from a "session:state" socket message).
                SetState(ConnectionState.Connected);
                Debug.Log("[BIVROST] Connected successfully.");
            }
            catch (Exception ex)
            {
                SetState(ConnectionState.Error);
                Events.RaiseError(ex.Message);
                Debug.LogError($"[BIVROST] Connection failed: {ex.Message}");
            }
        }

        public async void Disconnect()
        {
            if (State == ConnectionState.Disconnected)
                return;

            Debug.Log("[BIVROST] Disconnecting...");

            if (_socketIO != null)
            {
                await _socketIO.Disconnect();
                _socketIO = null;
            }

            if (_liveKit != null)
            {
                _liveKit.Disconnect();
                _liveKit = null;
            }

            SetState(ConnectionState.Disconnected);
            Events.RaiseDisconnected("manual");
            Debug.Log("[BIVROST] Disconnected.");
        }

        public async void SetStatus(StudentStatus status)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[BIVROST] Cannot set status while disconnected.");
                return;
            }

            var statusString = status switch
            {
                StudentStatus.Idle => "idle",
                StudentStatus.Loading => "loading",
                StudentStatus.InProgress => "in-progress",
                StudentStatus.Paused => "paused",
                StudentStatus.Completed => "completed",
                _ => status.ToString().ToLower()
            };

            await _socketIO.SendStatus(statusString);
        }

        public async void SetStatus(string customStatus)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[BIVROST] Cannot set status while disconnected.");
                return;
            }

            await _socketIO.SendStatus(customStatus);
        }
        
        /// <summary>
        /// Sends a student -> instructor action/notification, e.g. NotifyInstructor("objective_completed").
        /// The key must match an action defined on the Bivrost web platform's Actions page
        /// with direction "Student -> Instructor".
        /// </summary>
        public async void NotifyInstructor(string key, object payload = null)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[BIVROST] Cannot notify instructor while disconnected.");
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                Debug.LogWarning("[BIVROST] NotifyInstructor called with an empty key.");
                return;
            }

            await _socketIO.SendAction(key, payload);
        }

        public void PublishCamera(Camera camera, int width = 1280, int height = 720, int framerate = 15, int bitrate = 512000)
        {
            if (_liveKit == null || !_liveKit.IsConnected)
            {
                Debug.LogWarning("[BIVROST] LiveKit not connected. Call ConnectLiveKit first.");
                return;
            }

            _liveKit.PublishCamera(camera, width, height, framerate, bitrate);
        }

        public void PublishMicrophone()
        {
            if (_liveKit == null || !_liveKit.IsConnected)
            {
                Debug.LogWarning("[BIVROST] LiveKit not connected.");
                return;
            }

            _liveKit.PublishMicrophone();
        }

        public void ConnectLiveKit(string url, string token)
        {
            _liveKit = CreateRealtimeModule();
            _liveKit?.Connect(url, token, Events);
        }

        private IRealtimeModule CreateRealtimeModule()
        {
            if (!RealtimeModuleRegistry.IsRegistered)
            {
                Debug.LogError("[BIVROST] Realtime module (LiveKit) isn't installed. " +
                                "Run Bivrost > Install Realtime Module (LiveKit).");
                return null;
            }

            return RealtimeModuleRegistry.Create(this);
        }

        private void Update()
        {
            if (State != ConnectionState.Connected || _socketIO == null)
                return;

            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= (Config.HeartbeatIntervalMs / 1000f))
            {
                _heartbeatTimer = 0f;
                SendHeartbeat();
            }
        }

        private async void SendHeartbeat()
        {
            await _socketIO.SendHeartbeat();
        }

        private void SetState(ConnectionState newState)
        {
            if (State == newState) return;
            State = newState;
            Events.RaiseStateChanged(newState);
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                Disconnect();
                _instance = null;
            }
        }
    }
}