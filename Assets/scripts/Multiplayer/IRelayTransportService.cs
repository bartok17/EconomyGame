using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Relay.Models;

namespace MonopolyGame.Multiplayer
{
    /// <summary>
    /// Handles relay allocation and Unity Transport configuration.
    /// Extracted from <see cref="MultiplayerFlowCoordinator"/> to keep it focused.
    /// </summary>
    public interface IRelayTransportService
    {
        /// <summary>True when the service has resolved NetworkManager + UnityTransport.</summary>
        bool IsReady { get; }

        /// <summary>Wire the NetworkManager used for transport setup.</summary>
        void BindNetworkManager(NetworkManager networkManager);

        /// <summary>Create a relay allocation, get a join code, and configure the transport as host.</summary>
        Task<RelayConnectionSummary> CreateAndConfigureHostAsync(int maxConnections, string connectionType = "dtls");

        /// <summary>Join a relay allocation by code and configure the transport as client.</summary>
        Task<RelayConnectionSummary> JoinAndConfigureClientAsync(string joinCode, string connectionType = "dtls");
    }
}
