using UnityEngine;
using Unity.Netcode;

public class NetworkStartUI : MonoBehaviour
{
    private NetworkManager networkManager;
    private LobbyRelayManager lobbyManager;
    private string lobbyCodeInput = "";
    private string hostIpInput = "127.0.0.1";

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

        float w = 200f, h = 40f;
        float x = 10f, y = 10f;

        if (!networkManager.IsClient && !networkManager.IsServer)
        {
            GUI.Label(new Rect(x, y, 150f, h), "Host IP");
            hostIpInput = GUI.TextField(new Rect(x + 160f, y, w, h), hostIpInput);

            GUI.Label(new Rect(x, y + h + 10, 150f, h), "Lobby Code");
            lobbyCodeInput = GUI.TextField(new Rect(x + 160f, y + h + 10, w, h), lobbyCodeInput);

            if (GUI.Button(new Rect(x, y + 2 * (h + 10), w, h), "Host Game"))
            {
                lobbyManager.StartHostWithLobby();
            }

            if (GUI.Button(new Rect(x, y + 3 * (h + 10), w, h), "Join Game"))
            {
                lobbyManager.StartClientWithLobby(hostIpInput, lobbyCodeInput);
            }

            if (!string.IsNullOrWhiteSpace(lobbyManager.CurrentLobbyCode))
            {
                GUI.Label(new Rect(x, y + 4 * (h + 10), 350f, h), "Your Code: " + lobbyManager.CurrentLobbyCode);
            }

            if (!string.IsNullOrWhiteSpace(lobbyManager.StatusMessage))
            {
                GUI.Label(new Rect(x, y + 5 * (h + 10), 400f, h), lobbyManager.StatusMessage);
            }
        }
        else
        {
            GUI.Label(new Rect(x, y, 300f, h), "Connected. Code: " + lobbyManager.CurrentLobbyCode);
        }
    }
}