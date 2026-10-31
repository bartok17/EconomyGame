using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    /// <summary>
    /// Persists remembered accounts as a JSON array in PlayerPrefs.
    /// PlayerPrefs matches the Authentication SDK itself, which stores session
    /// tokens there too, so no additional storage dependencies are introduced.
    /// </summary>
    public sealed class PlayerPrefsRememberedAccountStore : IRememberedAccountStore
    {
        private const string PrefsKey = "monopoly.rememberedAccounts.v1";

        public IReadOnlyList<RememberedAccount> LoadAll()
        {
            var json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<RememberedAccount>();
            }

            try
            {
                var wrapper = JsonUtility.FromJson<RememberedAccountList>(json);
                return wrapper != null && wrapper.items != null
                    ? wrapper.items
                    : Array.Empty<RememberedAccount>();
            }
            catch (Exception)
            {
                // Corrupt or outdated data must never break startup.
                return Array.Empty<RememberedAccount>();
            }
        }

        public void SaveAll(IReadOnlyList<RememberedAccount> accounts)
        {
            var wrapper = new RememberedAccountList
            {
                items = accounts == null ? new List<RememberedAccount>() : new List<RememberedAccount>(accounts)
            };

            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(wrapper));
            PlayerPrefs.Save();
        }
    }

    [Serializable]
    internal sealed class RememberedAccountList
    {
        public List<RememberedAccount> items;
    }
}
