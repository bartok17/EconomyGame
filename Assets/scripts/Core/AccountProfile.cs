using System.Security.Cryptography;
using System.Text;

namespace MonopolyGame.Multiplayer
{
    /// <summary>
    /// Derives the Unity Authentication "profile" name used to isolate each
    /// remembered account's cached session token.
    /// The SDK restricts profiles to ^[a-zA-Z0-9_-]{1,30}$ and usernames have no
    /// documented length limit, so a hash is used instead of the raw username.
    /// </summary>
    public static class AccountProfile
    {
        private const string Prefix = "acc_";
        private const int HashLength = 24;

        public static string FromUsername(string username)
        {
            var normalized = (username ?? string.Empty).ToLowerInvariant();

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));

            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                builder.Append(hash[i].ToString("x2"));
            }

            return Prefix + builder.ToString().Substring(0, HashLength);
        }
    }
}
