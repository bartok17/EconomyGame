using System;

namespace MonopolyGame.Multiplayer
{
    /// <summary>
    /// A locally remembered account that can be restored with one click.
    /// Plain data (no Unity types) so it stays in the Core assembly.
    /// </summary>
    [Serializable]
    public sealed class RememberedAccount
    {
        // JsonUtility serializes public fields only.
        public string id;
        public string username;
        public string displayName;
        public long lastUsedTicks;

        public RememberedAccount()
        {
        }

        public RememberedAccount(string id, string username, string displayName, long lastUsedTicks)
        {
            this.id = id;
            this.username = username;
            this.displayName = displayName;
            this.lastUsedTicks = lastUsedTicks;
        }
    }
}
