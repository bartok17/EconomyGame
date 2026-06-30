using System.Collections.Generic;
using System.Linq;
using MonopolyGame.Pawns;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    /// <summary>
    /// Tracks the live set of spawned pawn syncs and owns ownership assignment.
    /// Plain C# class — no MonoBehaviour overhead.
    /// </summary>
    public sealed class GameSessionPawnRegistry
    {
        private readonly List<PlayerPawnNetworkSync> pawnSyncs = new List<PlayerPawnNetworkSync>();
        private readonly PlayerPawnSpawner spawner;

        public GameSessionPawnRegistry(PlayerPawnSpawner spawner)
        {
            this.spawner = spawner;
        }

        public int Count => pawnSyncs.Count;

        /// <summary>Replaces the list with a freshly spawned set (server only, at game start).</summary>
        public void Populate(IEnumerable<PlayerPawnNetworkSync> syncs)
        {
            pawnSyncs.Clear();
            pawnSyncs.AddRange(syncs.Where(s => s != null));
        }

        /// <summary>Rebuilds the list from the static NGO registry, falling back to the spawner.</summary>
        public void Refresh()
        {
            pawnSyncs.Clear();
            pawnSyncs.AddRange(PlayerPawnNetworkSync.GetSpawnedPawnSyncs().Where(s => s != null).OrderBy(s => s.PawnSlot));

            if (pawnSyncs.Count == 0 && spawner != null)
            {
                pawnSyncs.AddRange(spawner.GetSpawnedPawnSyncs().Where(s => s != null).OrderBy(s => s.PawnSlot));
            }
        }

        /// <summary>Assigns NetworkObject ownership so pawn slot i belongs to clientId i.</summary>
        public void AssignOwnerships()
        {
            if (NetworkManager.Singleton == null)
            {
                return;
            }

            for (int i = 0; i < pawnSyncs.Count; i++)
            {
                PlayerPawnNetworkSync pawnSync = pawnSyncs[i];

                if (pawnSync == null || pawnSync.NetworkObject == null || !pawnSync.NetworkObject.IsSpawned)
                {
                    continue;
                }

                ulong ownerClientId = (ulong)i;

                if (pawnSync.NetworkObject.OwnerClientId != ownerClientId)
                {
                    pawnSync.NetworkObject.ChangeOwnership(ownerClientId);
                }
            }
        }

        /// <summary>Builds the participant list for the turn state machine from current pawn ownership.</summary>
        public IReadOnlyList<TurnParticipant> BuildParticipants()
        {
            List<TurnParticipant> participants = new List<TurnParticipant>();

            for (int i = 0; i < pawnSyncs.Count; i++)
            {
                PlayerPawnNetworkSync pawnSync = pawnSyncs[i];

                if (pawnSync == null)
                {
                    continue;
                }

                ulong clientId = pawnSync.NetworkObject != null ? pawnSync.NetworkObject.OwnerClientId : (ulong)i;
                participants.Add(new TurnParticipant(i, pawnSync.DisplayName, clientId));
            }

            return participants;
        }

        /// <summary>Returns the pawn sync whose turn index maps to <paramref name="turnIndex"/>. Refreshes first.</summary>
        public PlayerPawnNetworkSync GetAtTurnIndex(int turnIndex)
        {
            Refresh();

            if (pawnSyncs.Count == 0)
            {
                return null;
            }

            return pawnSyncs[turnIndex % pawnSyncs.Count];
        }

        /// <summary>Finds a pawn sync by slot number. Refreshes first, falls back to the static registry.</summary>
        public PlayerPawnNetworkSync FindBySlot(int pawnSlot)
        {
            Refresh();

            for (int i = 0; i < pawnSyncs.Count; i++)
            {
                if (pawnSyncs[i] != null && pawnSyncs[i].PawnSlot == pawnSlot)
                {
                    return pawnSyncs[i];
                }
            }

            PlayerPawnNetworkSync.TryGetPawnSyncBySlot(pawnSlot, out PlayerPawnNetworkSync fallback);
            return fallback;
        }

        /// <summary>Returns all alive PlayerPawn components across spawned syncs.</summary>
        public IReadOnlyList<PlayerPawn> GetAllPawns()
        {
            Refresh();
            return pawnSyncs
                .Select(s => s != null ? s.GetComponent<PlayerPawn>() : null)
                .Where(p => p != null)
                .ToList();
        }
    }
}
