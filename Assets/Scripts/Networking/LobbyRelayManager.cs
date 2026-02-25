using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class LobbyRelayManager : MonoBehaviour
{
    [SerializeField] private ushort port = 7777;

    private string currentLobbyCode = "";
    private string statusMessage = "";
    private string hostIp = "";

    public string CurrentLobbyCode => currentLobbyCode;
    public string StatusMessage => statusMessage;
    public string HostIp => hostIp;

    public void SetHostIp(string ip)
    {
        hostIp = ip;
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "NetworkManager not found in scene.";
            Debug.LogError(statusMessage);
        }
    }

    public void StartHostWithLobby()
    {
        try
        {
            statusMessage = "Starting host...";

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", port);

            bool started = NetworkManager.Singleton.StartHost();
            if (!started)
            {
                statusMessage = "Failed to start host.";
                return;
            }

            // Generate a random lobby code
            currentLobbyCode = GenerateLobbyCode();
            statusMessage = $"Hosting on port {port} - Share Code: {currentLobbyCode}";
            Debug.Log(statusMessage);
        }
        catch (System.Exception ex)
        {
            statusMessage = "Host error: " + ex.Message;
            Debug.LogError(statusMessage);
        }
    }

    public void StartClientWithLobby(string hostIpInput, string lobbyCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hostIpInput))
            {
                statusMessage = "Enter the host IP address.";
                return;
            }

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                statusMessage = "Enter a lobby code.";
                return;
            }

            hostIp = hostIpInput;
            statusMessage = "Connecting to host...";

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(hostIp, port);

            bool started = NetworkManager.Singleton.StartClient();
            if (!started)
            {
                statusMessage = "Failed to connect to host.";
                return;
            }

            currentLobbyCode = lobbyCode;
            statusMessage = $"Connected to {hostIp}:{port}";
            Debug.Log($"Client joined - Host: {hostIp}:{port}, Code: {lobbyCode}");
        }
        catch (System.Exception ex)
        {
            statusMessage = "Client error: " + ex.Message;
            Debug.LogError(statusMessage);
        }
    }

    private string GenerateLobbyCode()
    {
        // Generate a 6-character alphanumeric code
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 6; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }
}

