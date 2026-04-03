using System;
using UnityEngine;

namespace NpcAi.Network.Messages
{
    // ---- Envelope ----

    /// <summary>
    /// Universal message envelope. All WebSocket messages (both directions) use this shape.
    /// </summary>
    [Serializable]
    public class Envelope
    {
        public string type;
        public string id;
        public string data; // raw JSON — deserialized in a second pass based on type
    }

    // ---- Client -> Server ----

    [Serializable]
    public class SpawnNpcRequest
    {
        public string npc_id;
        public string type_name;
        public float x;
        public float z;
    }

    [Serializable]
    public class RemoveNpcRequest
    {
        public string npc_id;
    }

    [Serializable]
    public class PublishEventRequest
    {
        public string event_type;
        public float x;
        public float z;
        public float severity;
        public string source_id;
    }

    [Serializable]
    public class QueryNpcRequest
    {
        public string npc_id;
    }

    // ---- Server -> Client ----

    [Serializable]
    public class SpawnNpcResponse
    {
        public string npc_id;
        public string type_name;
    }

    [Serializable]
    public class RemoveNpcResponse
    {
        public string npc_id;
    }

    [Serializable]
    public class PublishEventResponse
    {
        public string event_id;
    }

    [Serializable]
    public class ErrorResponse
    {
        public string code;
        public string message;
    }

    [Serializable]
    public class NpcSnapshot
    {
        public string npc_id;
        public string type_name;
        public float x;
        public float z;
        public string fsm_state;
        public string current_action;
        public float threat_level;
    }

    [Serializable]
    public class WorldSnapshotData
    {
        public long tick;
        public NpcSnapshot[] npcs;
    }

    // ---- Helpers for JsonUtility (it cannot deserialize arrays at root) ----

    [Serializable]
    public class QueryNpcResponse
    {
        public string npc_id;
        public string type_name;
        public float x;
        public float z;
        public string fsm_state;
        public string current_action;
        public float threat_level;
    }
}
