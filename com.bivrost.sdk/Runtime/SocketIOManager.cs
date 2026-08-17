using System;
using System.Text.Json;
using System.Threading.Tasks;
using SocketIOClient;
using UnityEngine;

namespace Bivrost
{
    internal class SocketIOManager
    {
        private SocketIOClient.SocketIO _socket;
        private BivrostConfig _config;
        private BivrostEvents _events;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        public async Task Connect(BivrostConfig config, BivrostEvents events)
        {
            _config = config;
            _events = events;

            _socket = new SocketIOClient.SocketIO(config.ServerUrl, new SocketIOOptions
            {
                Auth = new
                {
                    type = "headset",
                    sessionId = config.SessionId,
                    studentName = config.StudentName,
                    deviceInfo = SystemInfo.deviceModel
                },
                Reconnection = true,
                ReconnectionAttempts = config.ReconnectAttempts,
                ReconnectionDelay = config.ReconnectDelayMs
            });

            RegisterEvents();

            try
            {
                await _socket.ConnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BIVROST] Socket.IO connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task Disconnect()
        {
            if (_socket == null) return;

            try
            {
                await _socket.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BIVROST] Socket.IO disconnect error: {ex.Message}");
            }
            finally
            {
                _socket.Dispose();
                _socket = null;
                _isConnected = false;
            }
        }

        public async Task SendHeartbeat(string currentTask = null, float? progress = null)
        {
            if (!_isConnected || _socket == null) return;

            await _socket.EmitAsync("student:heartbeat", new
            {
                studentName = _config.StudentName,
                currentTask,
                progress
            });
        }

        public async Task SendStatus(string status)
        {
            if (!_isConnected || _socket == null) return;

            await _socket.EmitAsync("student:status", new
            {
                studentName = _config.StudentName,
                status
            });

            Debug.Log($"[BIVROST] Sent status: {status}");
        }

        /// <summary>Sends a student -> instructor action, e.g. "objective_completed".</summary>
        public async Task SendAction(string key, object payload = null)
        {
            if (!_isConnected || _socket == null) return;

            await _socket.EmitAsync("student:action", new
            {
                studentName = _config.StudentName,
                key,
                payload
            });

            Debug.Log($"[BIVROST] Sent action: {key}");
        }

        private void RegisterEvents()
        {
            _socket.OnConnected += (sender, args) =>
            {
                _isConnected = true;
                Debug.Log("[BIVROST] Socket.IO connected.");
                MainThreadDispatcher.Enqueue(() => _events.RaiseConnected());
            };

            _socket.OnDisconnected += (sender, reason) =>
            {
                _isConnected = false;
                Debug.Log($"[BIVROST] Socket.IO disconnected: {reason}");
                MainThreadDispatcher.Enqueue(() => _events.RaiseDisconnected(reason));
            };

            _socket.OnReconnectAttempt += (sender, attempt) =>
            {
                Debug.Log($"[BIVROST] Reconnecting... attempt {attempt}");
            };

            _socket.OnReconnectFailed += (sender, args) =>
            {
                Debug.LogError("[BIVROST] Reconnection failed.");
                MainThreadDispatcher.Enqueue(() => _events.RaiseError("Reconnection failed"));
            };

            // Server sends session state on join
            _socket.On("session:state", response =>
            {
                var json = response.GetValue<JsonElement>();
                Debug.Log($"[BIVROST] Session state received: {json}");
            });

            // Instructor started the session
            _socket.On("session:start", response =>
            {
                Debug.Log("[BIVROST] Session started by instructor.");
                MainThreadDispatcher.Enqueue(() => _events.RaiseSessionStarted());
            });

            // Instructor ended the session
            _socket.On("session:end", response =>
            {
                Debug.Log("[BIVROST] Session ended by instructor.");
                MainThreadDispatcher.Enqueue(() => _events.RaiseSessionEnded());
            });

            // Instructor -> Student action, e.g. { "key": "turn_off_light", "payload": {...} }
            _socket.On("instructor:action", response =>
            {
                var json = response.GetValue<JsonElement>();
                var key = json.GetProperty("key").GetString();

                string payloadJson = null;
                if (json.TryGetProperty("payload", out var payloadElement))
                    payloadJson = payloadElement.GetRawText();

                Debug.Log($"[BIVROST] Action received: {key}");
                var action = new BivrostAction(key, payloadJson);
                MainThreadDispatcher.Enqueue(() => _events.RaiseActionReceived(action));
            });

            // Error from server
            _socket.On("error", response =>
            {
                var json = response.GetValue<JsonElement>();
                var message = json.GetProperty("message").GetString();
                Debug.LogError($"[BIVROST] Server error: {message}");
                MainThreadDispatcher.Enqueue(() => _events.RaiseError(message));
            });
        }
    }
}