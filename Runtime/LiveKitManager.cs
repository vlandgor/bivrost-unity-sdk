using System;
using System.Collections;
using UnityEngine;
using LiveKit;
using LiveKit.Proto;
using RoomOptions = LiveKit.RoomOptions;

namespace Bivrost
{
    internal class LiveKitManager
    {
        private Room _room;
        private MonoBehaviour _coroutineRunner;
        private BivrostConfig _config;
        private BivrostEvents _events;

        private TextureVideoSource _videoSource;
        private LocalVideoTrack _videoTrack;
        private MicrophoneSource _micSource;
        private LocalAudioTrack _audioTrack;
        private GameObject _audioObject;
        private RenderTexture _renderTexture;
        private Camera _streamCamera;

        public Room Room => _room;
        public bool IsConnected { get; private set; }

        public LiveKitManager(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

        public void Connect(string url, string token, BivrostConfig config, BivrostEvents events)
        {
            _config = config;
            _events = events;
            _room = new Room();

            _room.TrackSubscribed += OnTrackSubscribed;
            _room.TrackUnsubscribed += OnTrackUnsubscribed;

            _coroutineRunner.StartCoroutine(ConnectCoroutine(url, token));
        }

        private IEnumerator ConnectCoroutine(string url, string token)
        {
            var connect = _room.Connect(url, token, new RoomOptions());
            yield return connect;

            if (connect.IsError)
            {
                Debug.LogError("[BIVROST] LiveKit connection failed");
                UnityMainThread.Enqueue(() => _events.RaiseError("LiveKit connection failed"));
                yield break;
            }

            IsConnected = true;
            Debug.Log($"[BIVROST] LiveKit connected to room: {_room.Name}");

            // Listen for data messages (voice channel notifications from instructor)
            _room.DataReceived += OnDataReceived;

            // Auto-publish microphone so instructor can hear the student
            _coroutineRunner.StartCoroutine(PublishMicCoroutine());
        }

        public void PublishCamera(Camera camera, int width = 1280, int height = 720, int framerate = 15, int bitrate = 512000)
        {
            if (!IsConnected || _room == null)
            {
                Debug.LogWarning("[BIVROST] Cannot publish camera — not connected to LiveKit.");
                return;
            }

            _coroutineRunner.StartCoroutine(PublishCameraCoroutine(camera, width, height, framerate, bitrate));
        }

        private IEnumerator PublishCameraCoroutine(Camera camera, int width, int height, int framerate, int bitrate)
        {
            // Create a duplicate camera for streaming — don't touch the original
            var streamObj = new GameObject("BivrostStreamCamera");
            streamObj.transform.SetParent(_coroutineRunner.transform);
            _streamCamera = streamObj.AddComponent<Camera>();
            _streamCamera.CopyFrom(camera);

            _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            _renderTexture.Create();
            _streamCamera.targetTexture = _renderTexture;

            // Create video source from RenderTexture
            _videoSource = new TextureVideoSource(_renderTexture);
            _videoTrack = LocalVideoTrack.CreateVideoTrack("bivrost-video", _videoSource, _room);

            var options = new TrackPublishOptions();
            options.VideoCodec = VideoCodec.Vp8;
            var encoding = new VideoEncoding();
            encoding.MaxBitrate = (ulong)bitrate;
            encoding.MaxFramerate = (uint)framerate;
            options.VideoEncoding = encoding;
            options.Source = TrackSource.SourceCamera;

            var publish = _room.LocalParticipant.PublishTrack(_videoTrack, options);
            yield return publish;

            if (publish.IsError)
            {
                Debug.LogError($"[BIVROST] Failed to publish video");
                yield break;
            }

            _videoSource.Start();
            _coroutineRunner.StartCoroutine(_videoSource.Update());
            Debug.Log($"[BIVROST] Video published: {width}x{height} @ {framerate}fps");
        }

        public void PublishMicrophone()
        {
            if (!IsConnected || _room == null)
            {
                Debug.LogWarning("[BIVROST] Cannot publish mic — not connected to LiveKit.");
                return;
            }

            _coroutineRunner.StartCoroutine(PublishMicCoroutine());
        }

        private IEnumerator PublishMicCoroutine()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[BIVROST] No microphone found.");
                yield break;
            }

            _audioObject = new GameObject("BivrostAudioSource");
            _audioObject.transform.SetParent(_coroutineRunner.transform);
            _micSource = new MicrophoneSource(Microphone.devices[0], _audioObject);
            _audioTrack = LocalAudioTrack.CreateAudioTrack("bivrost-audio", _micSource, _room);

            var options = new TrackPublishOptions();
            options.AudioEncoding = new AudioEncoding();
            options.AudioEncoding.MaxBitrate = 64000;
            options.Source = TrackSource.SourceMicrophone;

            var publish = _room.LocalParticipant.PublishTrack(_audioTrack, options);
            yield return publish;

            if (publish.IsError)
            {
                Debug.LogError($"[BIVROST] Failed to publish audio");
                yield break;
            }

            _micSource.Start();
            Debug.Log("[BIVROST] Microphone published.");
        }

        private void OnTrackSubscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
        {
            // Instructor audio (voice channel)
            if (track is RemoteAudioTrack audioTrack)
            {
                Debug.Log($"[BIVROST] Received audio from instructor: {participant.Identity}");
                var audObj = new GameObject($"InstructorAudio_{participant.Identity}");
                var source = audObj.AddComponent<AudioSource>();
                var stream = new AudioStream(audioTrack, source);
                UnityMainThread.Enqueue(() => _events.RaiseVoiceChannelStarted());
            }
        }

        private void OnTrackUnsubscribed(IRemoteTrack track, RemoteTrackPublication publication, RemoteParticipant participant)
        {
            if (track is RemoteAudioTrack)
            {
                Debug.Log($"[BIVROST] Instructor audio ended: {participant.Identity}");
                UnityMainThread.Enqueue(() => _events.RaiseVoiceChannelEnded());
            }
        }
        
        private void OnDataReceived(byte[] data, Participant participant, DataPacketKind kind, string topic)
        {
            try
            {
                var json = System.Text.Encoding.UTF8.GetString(data);
                var message = JsonUtility.FromJson<VoiceChannelMessage>(json);

                if (message.type == "voice-channel")
                {
                    if (message.action == "start")
                    {
                        Debug.Log("[BIVROST] Instructor opened voice channel.");
                        UnityMainThread.Enqueue(() => _events.RaiseVoiceChannelStarted());
                    }
                    else if (message.action == "stop")
                    {
                        Debug.Log("[BIVROST] Instructor closed voice channel.");
                        UnityMainThread.Enqueue(() => _events.RaiseVoiceChannelEnded());
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BIVROST] Failed to parse data message: {ex.Message}");
            }
        }

        [System.Serializable]
        private class VoiceChannelMessage
        {
            public string type;
            public string action;
        }

        public void Disconnect()
        {
            if (_videoSource != null)
            {
                _videoSource.Stop();
                _videoSource = null;
            }

            if (_micSource != null)
            {
                _micSource.Stop();
                _micSource = null;
            }
            
            if (_streamCamera != null)
            {
                UnityEngine.Object.Destroy(_streamCamera.gameObject);
                _streamCamera = null;
            }

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                UnityEngine.Object.Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_audioObject != null)
            {
                UnityEngine.Object.Destroy(_audioObject);
                _audioObject = null;
            }

            if (_room != null)
            {
                _room.TrackSubscribed -= OnTrackSubscribed;
                _room.TrackUnsubscribed -= OnTrackUnsubscribed;
                _room.DataReceived -= OnDataReceived;
                _room.Disconnect();
                _room = null;
            }

            IsConnected = false;
            Debug.Log("[BIVROST] LiveKit disconnected.");
        }
    }
}