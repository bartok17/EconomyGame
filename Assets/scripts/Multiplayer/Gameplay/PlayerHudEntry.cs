using TMPro;
using UnityEngine;

namespace MonopolyGame.Multiplayer.Gameplay
{
    public sealed class PlayerHudEntry : MonoBehaviour
    {
        private const float InactiveAlpha = 0.7f;

        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI balanceLabel;
        [SerializeField] private GameObject turnMarker;
        [SerializeField] private CanvasGroup canvasGroup;

        public void Apply(string displayName, int balance, bool isActivePlayer)
        {
            if (nameLabel != null)
            {
                nameLabel.text = displayName;
            }

            if (balanceLabel != null)
            {
                balanceLabel.text = balance.ToString("N0");
            }

            if (turnMarker != null)
            {
                turnMarker.SetActive(isActivePlayer);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = isActivePlayer ? 1f : InactiveAlpha;
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
