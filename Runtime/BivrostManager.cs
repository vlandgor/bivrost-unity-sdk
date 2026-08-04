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
            Debug.Log($"[BIVROST] Connecting to {config.ServerUrl} | Session: {config.SessionId} | Student: {config.StudentName}");

            try
            {
                // TODO: Initialize SocketIOManager and connect
                // TODO: Fetch LiveKit token from server
                // TODO: Initialize LiveKitManager and connect

                // Simulated connection for now
                await Task.Delay(100);

                SetState(ConnectionState.Connected);
                Events.RaiseConnected();
                Debug.Log("[BIVROST] Connected successfully.");
            }
            catch (Exception ex)
            {
                SetState(ConnectionState.Error);
                Events.RaiseError(ex.Message);
                Debug.LogError($"[BIVROST] Connection failed: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (State == ConnectionState.Disconnected)
                return;

            Debug.Log("[BIVROST] Disconnecting...");

            // TODO: Disconnect SocketIOManager
            // TODO: Disconnect LiveKitManager

            SetState(ConnectionState.Disconnected);
            Events.RaiseDisconnected("manual");
            Debug.Log("[BIVROST] Disconnected.");
        }

        public void SetStatus(StudentStatus status)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[BIVROST] Cannot set status while disconnected.");
                return;
            }

            Debug.Log($"[BIVROST] Status: {status}");
            // TODO: Send status via SocketIOManager
        }

        public void SetStatus(string customStatus)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[BIVROST] Cannot set status while disconnected.");
                return;
            }

            Debug.Log($"[BIVROST] Status: {customStatus}");
            // TODO: Send custom status via SocketIOManager
        }

        private void Update()
        {
            if (State != ConnectionState.Connected)
                return;

            // Heartbeat
            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= (Config.HeartbeatIntervalMs / 1000f))
            {
                _heartbeatTimer = 0f;
                SendHeartbeat();
            }
        }

        private void SendHeartbeat()
        {
            // TODO: Send heartbeat via SocketIOManager
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