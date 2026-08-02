using System.Collections.Generic;
using System.Linq;
using MonopolyGame.Pawns;
using Unity.Netcode;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class GameSessionPawnRegistry
    {
        private readonly List<PlayerPawnNetworkSync> pawnSyncs = new List<PlayerPawnNetworkSync>();
        private readonly PlayerPawnSpawner spawner;

        public GameSessionPawnRegistry(PlayerPawnSpawner spawner)
        {
            this.spawner = spawner;
        }

        public int Count => pawnSyncs.Count;

        public void Populate(IEnumerable<PlayerPawnNetworkSync> syncs)
        {
            pawnSyncs.Clear();
            pawnSyncs.AddRange(syncs.Where(s => s != null));
        }

        public void Refresh()
        {
            pawnSyncs.Clear();
            pawnSyncs.AddRange(PlayerPawnNetworkSync.GetSpawnedPawnSyncs().Where(s => s != null).OrderBy(s => s.PawnSlot));

            if (pawnSyncs.Count == 0 && spawner != null)
            {
                pawnSyncs.AddRange(spawner.GetSpawnedPawnSyncs().Where(s => s != null).OrderBy(s => s.PawnSlot));
            }
        }

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

        public PlayerPawnNetworkSync GetAtTurnIndex(int turnIndex)
        {
            Refresh();

            if (pawnSyncs.Count == 0)
            {
                return null;
            }

            return pawnSyncs[turnIndex % pawnSyncs.Count];
        }

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
