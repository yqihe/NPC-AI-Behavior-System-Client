using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NpcAi.Network.Messages;
using UnityEngine;

namespace NpcAi.Network
{
    /// <summary>
    /// High-level NPC AI client. Attach to a GameObject in the scene.
    /// Manages WebSocket lifecycle, auto-reconnect, and exposes typed request methods.
    /// </summary>
    public class NpcClient : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string serverUrl = "ws://localhost:9820/ws";

        [Header("Reconnect")]
        [SerializeField] private float reconnectInterval = 3f;

        // ---- Events (main thread) ----

        /// <summary>Fires every time a world_snapshot arrives (main thread).</summary>
        public event Action<WorldSnapshotData> OnWorldSnapshot;

        /// <summary>Connection state changed (main thread).</summary>
        public event Action<bool> OnConnectionStateChanged;

        public bool IsConnected => _ws != null && _ws.CurrentState == WsConnection.State.Connected;

        // ---- Private ----

        private WsConnection _ws;
        private MessageDispatcher _dispatcher;
        private float _reconnectTimer;
        private bool _wantConnection = true;
        private bool _lastConnectedState;

        // Pending request callbacks keyed by request id
        private readonly Dictionary<string, Action<string>> _pendingSuccess = new Dictionary<string, Action<string>>();
        private readonly Dictionary<string, Action<ErrorResponse>> _pendingError = new Dictionary<string, Action<ErrorResponse>>();
        private int _requestCounter;

        // ---- Lifecycle ----

        private void Awake()
        {
            _dispatcher = new MessageDispatcher();
            _dispatcher.OnWorldSnapshot += snapshot => OnWorldSnapshot?.Invoke(snapshot);
            _dispatcher.OnResponse += HandleResponse;
            _dispatcher.OnError += HandleError;
        }

        private async void Start()
        {
            await ConnectAsync();
        }

        private void Update()
        {
            // Dispatch queued messages on main thread
            _dispatcher.ProcessQueue();

            // Reconnect logic
            if (_wantConnection && !IsConnected)
            {
                _reconnectTimer -= Time.unscaledDeltaTime;
                if (_reconnectTimer <= 0f)
                {
                    _reconnectTimer = reconnectInterval;
                    _ = ConnectAsync();
                }
            }

            // Notify connection state changes
            bool connected = IsConnected;
            if (connected != _lastConnectedState)
            {
                _lastConnectedState = connected;
                OnConnectionStateChanged?.Invoke(connected);
                Debug.Log($"[NpcClient] {(connected ? "Connected" : "Disconnected")}");
            }
        }

        private void OnDestroy()
        {
            _wantConnection = false;
            _ws?.Dispose();
        }

        // ---- Connection ----

        private async Task ConnectAsync()
        {
            _ws?.Dispose();
            _ws = new WsConnection(serverUrl);
            _ws.OnMessageReceived += _dispatcher.Enqueue;
            _ws.OnDisconnected += reason => Debug.Log($"[NpcClient] WS disconnected: {reason}");

            Debug.Log($"[NpcClient] Connecting to {serverUrl} ...");
            await _ws.ConnectAsync();
        }

        // ---- Public API ----

        /// <summary>Spawn an NPC on the server.</summary>
        public void SpawnNpc(string npcId, string typeName, float x, float z,
            Action<SpawnNpcResponse> onSuccess = null, Action<ErrorResponse> onError = null)
        {
            var data = new SpawnNpcRequest { npc_id = npcId, type_name = typeName, x = x, z = z };
            SendRequest("spawn_npc", data,
                json => onSuccess?.Invoke(JsonUtility.FromJson<SpawnNpcResponse>(json)),
                onError);
        }

        /// <summary>Remove an NPC from the server.</summary>
        public void RemoveNpc(string npcId,
            Action<RemoveNpcResponse> onSuccess = null, Action<ErrorResponse> onError = null)
        {
            var data = new RemoveNpcRequest { npc_id = npcId };
            SendRequest("remove_npc", data,
                json => onSuccess?.Invoke(JsonUtility.FromJson<RemoveNpcResponse>(json)),
                onError);
        }

        /// <summary>Publish a world event.</summary>
        public void PublishEvent(string eventType, float x, float z, float severity = 0f, string sourceId = null,
            Action<PublishEventResponse> onSuccess = null, Action<ErrorResponse> onError = null)
        {
            var data = new PublishEventRequest
            {
                event_type = eventType, x = x, z = z, severity = severity, source_id = sourceId ?? ""
            };
            SendRequest("publish_event", data,
                json => onSuccess?.Invoke(JsonUtility.FromJson<PublishEventResponse>(json)),
                onError);
        }

        /// <summary>Query detailed NPC state.</summary>
        public void QueryNpc(string npcId,
            Action<QueryNpcResponse> onSuccess = null, Action<ErrorResponse> onError = null)
        {
            var data = new QueryNpcRequest { npc_id = npcId };
            SendRequest("query_npc", data,
                json => onSuccess?.Invoke(JsonUtility.FromJson<QueryNpcResponse>(json)),
                onError);
        }

        // ---- Internals ----

        private string NextRequestId() => $"req_{++_requestCounter}";

        private async void SendRequest<T>(string type, T data, Action<string> onSuccess, Action<ErrorResponse> onError)
        {
            if (!IsConnected)
            {
                onError?.Invoke(new ErrorResponse { code = "not_connected", message = "WebSocket is not connected" });
                return;
            }

            string reqId = NextRequestId();
            if (onSuccess != null) _pendingSuccess[reqId] = onSuccess;
            if (onError != null) _pendingError[reqId] = onError;

            string dataJson = JsonUtility.ToJson(data);
            // Build envelope manually to embed data as a raw JSON object (not escaped string)
            string envelope = $"{{\"type\":\"{type}\",\"id\":\"{reqId}\",\"data\":{dataJson}}}";

            await _ws.SendAsync(envelope);
        }

        private void HandleResponse(string reqId, string dataJson)
        {
            if (reqId != null && _pendingSuccess.TryGetValue(reqId, out var cb))
            {
                _pendingSuccess.Remove(reqId);
                _pendingError.Remove(reqId);
                cb.Invoke(dataJson);
            }
        }

        private void HandleError(string reqId, ErrorResponse err)
        {
            if (reqId != null && _pendingError.TryGetValue(reqId, out var cb))
            {
                _pendingError.Remove(reqId);
                _pendingSuccess.Remove(reqId);
                cb.Invoke(err);
            }
            Debug.LogWarning($"[NpcClient] Server error [{err.code}]: {err.message}");
        }
    }
}
