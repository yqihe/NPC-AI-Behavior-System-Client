using System.Collections.Generic;
using NpcAi.Network;
using NpcAi.Network.Messages;
using UnityEngine;

namespace NpcAi.Visualization
{
    /// <summary>
    /// Listens to world_snapshot broadcasts and keeps the scene's NPC GameObjects in sync.
    /// No local caching — the snapshot is the single source of truth.
    /// </summary>
    public class NpcManager : MonoBehaviour
    {
        [SerializeField] private NpcClient npcClient;

        /// <summary>Currently active NPC visuals keyed by npc_id.</summary>
        public IReadOnlyDictionary<string, NpcVisual> Npcs => _npcs;

        /// <summary>Latest tick number received.</summary>
        public long CurrentTick { get; private set; }

        private readonly Dictionary<string, NpcVisual> _npcs = new Dictionary<string, NpcVisual>();
        private readonly HashSet<string> _seenThisFrame = new HashSet<string>();

        private void OnEnable()
        {
            if (npcClient == null) npcClient = FindFirstObjectByType<NpcClient>();
            npcClient.OnWorldSnapshot += OnWorldSnapshot;
        }

        private void OnDisable()
        {
            if (npcClient != null) npcClient.OnWorldSnapshot -= OnWorldSnapshot;
        }

        private void OnWorldSnapshot(WorldSnapshotData snapshot)
        {
            CurrentTick = snapshot.tick;
            if (snapshot.npcs == null) return;
            _seenThisFrame.Clear();

            // Update or create
            foreach (var snap in snapshot.npcs)
            {
                _seenThisFrame.Add(snap.npc_id);

                if (_npcs.TryGetValue(snap.npc_id, out var visual))
                {
                    visual.ApplySnapshot(snap);
                }
                else
                {
                    visual = CreateNpcVisual(snap);
                    _npcs[snap.npc_id] = visual;
                }
            }

            // Remove NPCs no longer in snapshot
            var toRemove = new List<string>();
            foreach (var kv in _npcs)
            {
                if (!_seenThisFrame.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove)
            {
                Destroy(_npcs[id].gameObject);
                _npcs.Remove(id);
            }
        }

        private NpcVisual CreateNpcVisual(NpcSnapshot snap)
        {
            var go = new GameObject();
            go.transform.SetParent(transform);
            var visual = go.AddComponent<NpcVisual>();
            visual.Init(snap.npc_id, snap.type_name);
            visual.ApplySnapshot(snap);
            return visual;
        }
    }
}
