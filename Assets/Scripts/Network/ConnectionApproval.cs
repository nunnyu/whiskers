using Unity.Netcode;
using UnityEngine;

namespace ZincWhiskers
{
    [RequireComponent(typeof(NetworkManager))]
    public class ConnectionApproval : MonoBehaviour
    {
        const int MaxPayloadBytes = 1024;

        NetworkManager networkManager;

        void Awake()
        {
            networkManager = GetComponent<NetworkManager>();
            networkManager.ConnectionApprovalCallback = Approve;
        }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (request.Payload.Length > MaxPayloadBytes)
            {
                response.Approved = false;
                response.Reason = "Invalid connection data";
                return;
            }

            int playerCount = networkManager.ConnectedClients.Count;
            if (playerCount >= SessionNetwork.MaxPlayers)
            {
                response.Approved = false;
                response.Reason = "Lobby is full";
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = CatPlayer.SpawnPoint(playerCount);
        }
    }
}
