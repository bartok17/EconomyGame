using UnityEngine;

namespace MonopolyGame.DesignSystem
{
    /// <summary>
    /// Central runtime lookup for the shared UI style sheet.
    /// Resolves the asset at <c>Assets/Resources/UIStyleSheet</c> and caches it.
    /// </summary>
    public static class UIStyleSheetLocator
    {
        private const string ResourcePath = "UIStyleSheet";

        private static UIStyleSheet _cached;
        private static bool _warned;

        /// <summary>
        /// The shared style sheet, resolved via <see cref="Resources.Load{T}(string)"/>.
        /// Returns <see langword="null"/> until a <c>UIStyleSheet</c> asset is created
        /// under <c>Assets/Resources/</c> (Assets → Create → Monopoly Game → UI Style Sheet).
        /// </summary>
        public static UIStyleSheet Instance
        {
            get
            {
                if (_cached == null)
                {
                    _cached = Resources.Load<UIStyleSheet>(ResourcePath);

                    if (_cached == null)
                    {
                        if (!_warned)
                        {
                            _warned = true;
                            Debug.LogWarning(
                                $"[UIStyleSheetLocator] No UIStyleSheet asset found at Resources/{ResourcePath}. " +
                                "Create one via Assets → Create → Monopoly Game → UI Style Sheet.");
                        }
                    }
                    else
                    {
                        _warned = false;
                    }
                }

                return _cached;
            }
        }

        /// <summary>Clears the cache and re-resolves on the next access (editor convenience).</summary>
        public static void Invalidate()
        {
            _cached = null;
            _warned = false;
        }
    }
}
