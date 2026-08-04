using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Bivrost
{
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

        public BivrostEvents Events { get; } = new BivrostEvents();
        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
        public BivrostConfig Config { get; private set; }
        public bool IsConnected => State == ConnectionState.Connected;

        private SocketIOManager _socketIO;
        private LiveKitManager _liveKit;
        private float _heartbeatTimer;

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

                _liveKit = new LiveKitManager(this);

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

        public void PublishCamera(Camera camera, int width = 1280, int height = 720, int framerate = 15)
        {
            if (_liveKit == null || !_liveKit.IsConnected)
            {
                Debug.LogWarning("[BIVROST] LiveKit not connected. Call ConnectLiveKit first.");
                return;
            }

            _liveKit.PublishCamera(camera, width, height, framerate);
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
            _liveKit = new LiveKitManager(this);
            _liveKit.Connect(url, token, Config, Events);
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