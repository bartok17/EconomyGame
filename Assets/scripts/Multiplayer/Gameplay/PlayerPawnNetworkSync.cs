using System.Collections;
using MonopolyGame.Board;
using MonopolyGame.Pawns;
using Unity.Netcode;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class PlayerPawnNetworkSync : NetworkBehaviour
    {
        [SerializeField] private PlayerPawn pawn;
        [SerializeField] private BoardManager boardManager;

        private readonly NetworkVariable<int> syncedSpaceIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> isMovingNet = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public string PlayerId => pawn != null ? pawn.PlayerId : string.Empty;
        public string DisplayName => pawn != null ? pawn.DisplayName : string.Empty;
        public int PawnSlot => pawn != null ? pawn.PawnSlot : -1;
        public int CurrentSpaceIndex => pawn != null ? pawn.CurrentSpaceIndex : 0;

        public void Initialize(PlayerPawn pawn, BoardManager boardManager)
        {
            this.pawn = pawn;
            this.boardManager = boardManager;
        }

        public override void OnNetworkSpawn()
        {
            syncedSpaceIndex.OnValueChanged += HandleSyncedSpaceIndexChanged;
            isMovingNet.OnValueChanged += HandleIsMovingChanged;

            if (pawn != null)
            {
                pawn.PlaceOnSpace(syncedSpaceIndex.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            syncedSpaceIndex.OnValueChanged -= HandleSyncedSpaceIndexChanged;
            isMovingNet.OnValueChanged -= HandleIsMovingChanged;
        }

        public void PlayAuthoritativeMove(int targetSpaceIndex, float duration)
        {
            if (pawn == null || boardManager == null)
            {
                return;
            }

            int normalizedTarget = boardManager.NormalizeIndex(targetSpaceIndex);

            if (IsServer)
            {
                isMovingNet.Value = true;
                StartCoroutine(AnimateMoveRoutine(normalizedTarget, duration, writeBackToNetwork: true));
            }
            else
            {
                StartCoroutine(AnimateMoveRoutine(normalizedTarget, duration, writeBackToNetwork: false));
            }
        }

        public void ForceSpaceIndex(int targetSpaceIndex)
        {
            if (pawn == null || boardManager == null)
            {
                return;
            }

            int normalizedTarget = boardManager.NormalizeIndex(targetSpaceIndex);
            pawn.PlaceOnSpace(normalizedTarget);

            if (IsServer)
            {
                syncedSpaceIndex.Value = normalizedTarget;
                isMovingNet.Value = false;
            }
        }

        private IEnumerator AnimateMoveRoutine(int targetSpaceIndex, float duration, bool writeBackToNetwork)
        {
            if (pawn == null || boardManager == null)
            {
                yield break;
            }

            yield return StartCoroutine(pawn.MoveToSpaceRoutine(targetSpaceIndex, duration));

            if (writeBackToNetwork && IsServer)
            {
                syncedSpaceIndex.Value = targetSpaceIndex;
                isMovingNet.Value = false;
            }
        }

        private void HandleSyncedSpaceIndexChanged(int previousValue, int newValue)
        {
            if (pawn == null || boardManager == null)
            {
                return;
            }

            if (!IsServer && !isMovingNet.Value)
            {
                pawn.PlaceOnSpace(newValue);
            }
        }

        private void HandleIsMovingChanged(bool previousValue, bool newValue)
        {
            if (!newValue && pawn != null && boardManager != null && !IsServer)
            {
                pawn.PlaceOnSpace(syncedSpaceIndex.Value);
            }
        }
    }
}