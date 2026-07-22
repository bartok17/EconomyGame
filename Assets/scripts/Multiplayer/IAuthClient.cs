using System.Threading.Tasks;

namespace MonopolyGame.Multiplayer
{
    public interface IAuthClient
    {
        bool IsSignedIn { get; }
        string PlayerId { get; }
        string DisplayName { get; }

        Task SignUpAsync(string username, string password, string displayName);
        Task SignInAsync(string username, string password);
        Task SetDisplayNameAsync(string displayName);
        void SignOut();
    }
}
