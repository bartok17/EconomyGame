using TMPro;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class PlayerHudEntry : MonoBehaviour
    {
        private const float InactiveAlpha = 0.7f;
        private const float OfflineAlpha = 0.45f;
        private const float EliminatedAlpha = 0.35f;

        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI balanceLabel;
        [SerializeField] private GameObject turnMarker;
        [SerializeField] private CanvasGroup canvasGroup;

        public void Apply(
            string displayName,
            int balance,
            bool isActivePlayer,
            ParticipantSessionStatus status = ParticipantSessionStatus.Connected)
        {
            if (nameLabel != null)
            {
                nameLabel.text = status switch
                {
                    ParticipantSessionStatus.Disconnected => $"{displayName} (Offline)",
                    ParticipantSessionStatus.Eliminated   => $"{displayName} (Left)",
                    _                                     => displayName
                };
            }

            if (balanceLabel != null)
            {
                balanceLabel.text = status == ParticipantSessionStatus.Eliminated
                    ? "—"
                    : balance.ToString("N0");
            }

            if (turnMarker != null)
            {
                turnMarker.SetActive(isActivePlayer && status != ParticipantSessionStatus.Eliminated);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = status switch
                {
                    ParticipantSessionStatus.Eliminated   => EliminatedAlpha,
                    ParticipantSessionStatus.Disconnected => OfflineAlpha,
                    _                                     => isActivePlayer ? 1f : InactiveAlpha
                };
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }
    }
}