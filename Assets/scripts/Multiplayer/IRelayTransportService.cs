using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Relay.Models;

namespace MonopolyGame.Multiplayer
{
    public interface IRelayTransportService
    {
        bool IsReady { get; }
        void BindNetworkManager(NetworkManager networkManager);
        Task<RelayConnectionSummary> CreateAndConfigureHostAsync(int maxConnections, string connectionType = "dtls");
        Task<RelayConnectionSummary> JoinAndConfigureClientAsync(string joinCode, string connectionType = "dtls");
    }
}
