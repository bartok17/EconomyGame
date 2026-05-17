using System.Threading.Tasks;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace MonopolyGame.Multiplayer
{
    public sealed class RelayClient
    {
        public async Task<Allocation> CreateAllocationAsync(int maxConnections)
        {
            return await RelayService.Instance.CreateAllocationAsync(maxConnections);
        }

        public async Task<string> GetJoinCodeAsync(Allocation allocation)
        {
            return await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }

        public async Task<JoinAllocation> JoinAllocationAsync(string joinCode)
        {
            return await RelayService.Instance.JoinAllocationAsync(joinCode);
        }
    }
}
