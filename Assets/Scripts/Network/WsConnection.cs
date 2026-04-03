using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NpcAi.Network
{
    /// <summary>
    /// Low-level WebSocket connection. Runs receive loop on a background thread
    /// and queues incoming text messages for main-thread consumption.
    /// </summary>
    public class WsConnection : IDisposable
    {
        public enum State { Disconnected, Connecting, Connected }

        public State CurrentState { get; private set; } = State.Disconnected;

        /// <summary>Fires on the background thread when a complete text message arrives.</summary>
        public event Action<string> OnMessageReceived;

        /// <summary>Fires on the background thread when the connection is lost.</summary>
        public event Action<string> OnDisconnected;

        /// <summary>Fires on the background thread when the connection is established.</summary>
        public event Action OnConnected;

        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly Uri _uri;
        private readonly int _receiveBufferSize;

        public WsConnection(string url, int receiveBufferSize = 8192)
        {
            _uri = new Uri(url);
            _receiveBufferSize = receiveBufferSize;
        }

        /// <summary>Connect (or reconnect). Safe to call repeatedly.</summary>
        public async Task ConnectAsync()
        {
            Close();

            CurrentState = State.Connecting;
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();

            try
            {
                await _ws.ConnectAsync(_uri, _cts.Token);
                CurrentState = State.Connected;
                OnConnected?.Invoke();
                _ = ReceiveLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                CurrentState = State.Disconnected;
                OnDisconnected?.Invoke(ex.Message);
            }
        }

        public async Task SendAsync(string message)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;

            var bytes = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(bytes);
            try
            {
                await _ws.SendAsync(segment, WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WsConnection] Send failed: {ex.Message}");
            }
        }

        public void Close()
        {
            CurrentState = State.Disconnected;
            _cts?.Cancel();
            if (_ws != null)
            {
                try { _ws.Abort(); } catch { }
                _ws.Dispose();
                _ws = null;
            }
            _cts?.Dispose();
            _cts = null;
        }

        public void Dispose() => Close();

        // ---- Private ----

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[_receiveBufferSize];
            var sb = new StringBuilder();

            try
            {
                while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            CurrentState = State.Disconnected;
                            OnDisconnected?.Invoke("Server closed connection");
                            return;
                        }
                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    OnMessageReceived?.Invoke(sb.ToString());
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    CurrentState = State.Disconnected;
                    OnDisconnected?.Invoke(ex.Message);
                }
            }
        }
    }
}
