using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NpcAi.Network.Messages;
using UnityEngine;

namespace NpcAi.Network
{
    /// <summary>
    /// Queues raw JSON messages from the WebSocket background thread and dispatches
    /// them on the Unity main thread via Update().
    /// </summary>
    public class MessageDispatcher
    {
        /// <summary>Fired for every world_snapshot broadcast.</summary>
        public event Action<WorldSnapshotData> OnWorldSnapshot;

        /// <summary>Fired when a request-response pair completes successfully.</summary>
        public event Action<string, string> OnResponse; // (requestId, rawDataJson)

        /// <summary>Fired when a request fails.</summary>
        public event Action<string, ErrorResponse> OnError; // (requestId, error)

        private readonly ConcurrentQueue<string> _inbound = new ConcurrentQueue<string>();

        /// <summary>Call from the WebSocket background thread.</summary>
        public void Enqueue(string rawJson)
        {
            _inbound.Enqueue(rawJson);
        }

        /// <summary>Call from MonoBehaviour.Update() to process queued messages on the main thread.</summary>
        public void ProcessQueue()
        {
            while (_inbound.TryDequeue(out var raw))
            {
                try
                {
                    DispatchOne(raw);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MessageDispatcher] Failed to dispatch: {ex.Message}\nRaw: {raw}");
                }
            }
        }

        private void DispatchOne(string raw)
        {
            var envelope = JsonUtility.FromJson<Envelope>(raw);
            if (envelope == null)
            {
                Debug.LogWarning($"[MessageDispatcher] Could not parse envelope: {raw}");
                return;
            }

            // Extract the "data" field as raw JSON for second-pass deserialization.
            // JsonUtility serializes Envelope.data as a string field, but the actual
            // JSON has data as an object. We extract it manually.
            string dataJson = ExtractDataJson(raw);

            switch (envelope.type)
            {
                case "world_snapshot":
                    var snapshot = JsonUtility.FromJson<WorldSnapshotData>(dataJson);
                    if (snapshot != null)
                        OnWorldSnapshot?.Invoke(snapshot);
                    break;

                case "response":
                    OnResponse?.Invoke(envelope.id, dataJson);
                    break;

                case "error":
                    var err = JsonUtility.FromJson<ErrorResponse>(dataJson);
                    OnError?.Invoke(envelope.id, err);
                    break;

                default:
                    Debug.LogWarning($"[MessageDispatcher] Unknown message type: {envelope.type}");
                    break;
            }
        }

        /// <summary>
        /// Extracts the value of the "data" key from the raw JSON envelope string.
        /// This avoids the limitation where JsonUtility cannot do two-pass generic deserialization.
        /// </summary>
        private static string ExtractDataJson(string raw)
        {
            // Find "data": and extract the object that follows.
            // This is a simple brace-matching parser — sufficient for our well-formed server JSON.
            int idx = raw.IndexOf("\"data\"", StringComparison.Ordinal);
            if (idx < 0) return "{}";

            // Skip past "data" :
            idx = raw.IndexOf(':', idx + 6);
            if (idx < 0) return "{}";
            idx++; // skip ':'

            // Skip whitespace
            while (idx < raw.Length && char.IsWhiteSpace(raw[idx])) idx++;

            if (idx >= raw.Length) return "{}";

            // The value should be a JSON object starting with '{'
            if (raw[idx] != '{') return "{}";

            int depth = 0;
            int start = idx;
            bool inString = false;
            bool escape = false;

            for (int i = start; i < raw.Length; i++)
            {
                char c = raw[i];
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') { inString = !inString; continue; }
                if (inString) continue;
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth == 0) return raw.Substring(start, i - start + 1); }
            }

            return "{}";
        }
    }
}
