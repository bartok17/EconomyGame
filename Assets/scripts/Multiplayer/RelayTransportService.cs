using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class RelayTransportService : IRelayTransportService
    {
        private readonly IRelayClient _relayClient;

        private NetworkManager _networkManager;
        private UnityTransport _transport;

        public bool IsReady => _networkManager != null && _transport != null;

        public RelayTransportService(IRelayClient relayClient)
        {
            _relayClient = relayClient ?? throw new ArgumentNullException(nameof(relayClient));
        }

        public void BindNetworkManager(NetworkManager networkManager)
        {
            _networkManager = networkManager;
            _transport = networkManager != null ? networkManager.GetComponent<UnityTransport>() : null;

            if (_networkManager != null && _transport != null)
            {
                _networkManager.NetworkConfig.NetworkTransport = _transport;
            }

            if (_networkManager != null && _transport == null)
            {
                Debug.LogWarning($"[{nameof(RelayTransportService)}] NetworkManager has no UnityTransport component.");
            }
        }

        public async Task<RelayConnectionSummary> CreateAndConfigureHostAsync(int maxConnections, string connectionType = "dtls")
        {
            EnsureReady();

            Debug.Log($"[{nameof(RelayTransportService)}] Allocating relay for host (maxConnections={maxConnections}).");

            Allocation allocation = await _relayClient.CreateAllocationAsync(maxConnections);

            if (allocation == null)
            {
                throw new InvalidOperationException("Relay allocation returned null.");
            }

            string joinCode = await _relayClient.GetJoinCodeAsync(allocation);

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new InvalidOperationException("Relay join code was empty.");
            }

            Debug.Log($"[{nameof(RelayTransportService)}] Host relay created. code={joinCode}, region={allocation.Region}, allocationId={allocation.AllocationId}");

            RelayServerData relayServerData = allocation.ToRelayServerData(connectionType);
            _transport.SetRelayServerData(relayServerData);

            return new RelayConnectionSummary(
                allocation.AllocationId.ToString(),
                joinCode,
                allocation.Region,
                connectionType);
        }

        public async Task<RelayConnectionSummary> JoinAndConfigureClientAsync(string joinCode, string connectionType = "dtls")
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                throw new ArgumentException("Join code is required.", nameof(joinCode));
            }

            EnsureReady();

            Debug.Log($"[{nameof(RelayTransportService)}] Joining relay allocation as client. joinCode={joinCode}");

            JoinAllocation joinAllocation = await _relayClient.JoinAllocationAsync(joinCode);

            Debug.Log($"[{nameof(RelayTransportService)}] Relay allocation joined. region={joinAllocation.Region}, allocationId={joinAllocation.AllocationId}");

            RelayServerData relayServerData = joinAllocation.ToRelayServerData(connectionType);
            _transport.SetRelayServerData(relayServerData);

            return new RelayConnectionSummary(
                joinAllocation.AllocationId.ToString(),
                joinCode,
                joinAllocation.Region,
                connectionType);
        }

        private void EnsureReady()
        {
            if (_networkManager == null)
            {
                _networkManager = NetworkManager.Singleton;
            }

            if (_networkManager == null)
            {
                _networkManager = UnityEngine.Object.FindAnyObjectByType<NetworkManager>();
            }

            if (_networkManager == null)
            {
                throw new InvalidOperationException(
                    "NetworkManager was not found. Add NetworkManager to the lobby scene and assign it in MultiplayerBootstrapper.");
            }

            if (_transport == null)
            {
                _transport = _networkManager.GetComponent<UnityTransport>();
            }

            if (_transport == null)
            {
                throw new InvalidOperationException(
                    "UnityTransport was not found on NetworkManager. Add UnityTransport to the NetworkManager object.");
            }

            if (_networkManager.NetworkConfig == null)
            {
                throw new InvalidOperationException(
                    "NetworkManager has no NetworkConfig. Configure NetworkManager in the scene instead of creating it at runtime.");
            }

            _networkManager.NetworkConfig.NetworkTransport = _transport;
        }
    }
}
