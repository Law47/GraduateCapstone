using UnityEngine;
using Unity.Netcode;

public class NetworkStartUI : MonoBehaviour
{
    private NetworkManager networkManager;
    private LobbyRelayManager lobbyManager;

    void Start()
    {
        networkManager = NetworkManager.Singleton;
        lobbyManager = Object.FindAnyObjectByType<LobbyRelayManager>();
    }

    void OnGUI()
    {
        if (networkManager == null) return;

        if (lobbyManager == null)
        {
            GUI.Label(new Rect(10f, 10f, 350f, 40f), "LobbyRelayManager not found in scene.");
            return;
        }

        float h = 40f;
        float x = 10f, y = 10f;

        if (!networkManager.IsClient && !networkManager.IsServer)
        {
            GUI.Label(new Rect(x, y, 400f, h), "Use the lobby UI to host or join a game.");
        }
        else
        {
            GUI.Label(new Rect(x, y, 300f, h), "Connected. Code: " + lobbyManager.CurrentLobbyCode);
        }

        if (!string.IsNullOrWhiteSpace(lobbyManager.StatusMessage))
        {
            GUI.Label(new Rect(x, y + h + 10, 400f, h), lobbyManager.StatusMessage);
        }
    }
}