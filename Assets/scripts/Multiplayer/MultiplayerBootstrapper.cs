using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace MonopolyGame.Multiplayer
{
    public sealed class MultiplayerBootstrapper : MonoBehaviour
    {
        [SerializeField] private MultiplayerFlowCoordinator flowCoordinator;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            if (flowCoordinator == null)
            {
                flowCoordinator = GetComponent<MultiplayerFlowCoordinator>();
            }

            if (flowCoordinator == null)
            {
                flowCoordinator = gameObject.AddComponent<MultiplayerFlowCoordinator>();
            }

            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                var networkObject = new GameObject("NetworkManager");
                DontDestroyOnLoad(networkObject);
                networkManager = networkObject.AddComponent<NetworkManager>();
                networkObject.AddComponent<UnityTransport>();
            }

            flowCoordinator.AssignNetworkManager(networkManager);
        }
    }
}
