using System.Threading.Tasks;
using Unity.Services.Relay.Models;

namespace MonopolyGame.Multiplayer
{
    public interface IRelayClient
    {
        Task<Allocation> CreateAllocationAsync(int maxConnections);
        Task<string> GetJoinCodeAsync(Allocation allocation);
        Task<JoinAllocation> JoinAllocationAsync(string joinCode);
    }
}
