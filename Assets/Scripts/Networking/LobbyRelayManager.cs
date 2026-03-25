using System.Collections;
using Blocks.Sessions.Common;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyRelayManager : MonoBehaviour
{
    private const string GameplaySceneName = "Gameplay";
    private const string GameplayScenePath = "Scenes/Test Scenes/Gameplay";

    [SerializeField] private ushort port = 7788;

    private string currentLobbyCode = "";
    private string statusMessage = "";
    private string hostIp = "";
    private bool m_IsClientLobbyConnection;
    private bool m_HandlingClientDisconnect;

    public string CurrentLobbyCode => currentLobbyCode;
    public string StatusMessage => statusMessage;
    public string HostIp => hostIp;

    private void OnEnable()
    {
        PlayGameViewModel.GameplayStartRequested += OnGameplayStartRequested;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }
    }

    private void OnDisable()
    {
        PlayGameViewModel.GameplayStartRequested -= OnGameplayStartRequested;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }

    public void SetHostIp(string ip)
    {
        hostIp = ip;
    }

    public void SetCurrentLobbyCode(string lobbyCode)
    {
        currentLobbyCode = string.IsNullOrWhiteSpace(lobbyCode) ? string.Empty : lobbyCode.Trim().ToUpperInvariant();
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "NetworkManager not found in scene.";
        }
    }

    private void OnGameplayStartRequested()
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "NetworkManager not found. Loading Gameplay locally.";
            SceneManager.LoadScene(GameplayScenePath, LoadSceneMode.Single);
            return;
        }

        if (!NetworkManager.Singleton.IsListening)
        {
            statusMessage = "NetworkManager is not listening. Loading Gameplay locally.";
            SceneManager.LoadScene(GameplayScenePath, LoadSceneMode.Single);
            return;
        }

        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
        {
            statusMessage = "Only the host can start the synchronized gameplay scene.";
            return;
        }

        if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            statusMessage = "Enable Scene Management on the NetworkManager before starting Gameplay.";
            return;
        }

        var loadStatus = NetworkManager.Singleton.SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
        statusMessage = $"Gameplay scene load requested. Status: {loadStatus}";
    }

    public void StartGameplayForLobby()
    {
        OnGameplayStartRequested();
    }

    public void StartHostWithLobby()
    {
        StartCoroutine(StartHostWithLobbyRoutine());
    }

    private IEnumerator StartHostWithLobbyRoutine()
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "NetworkManager not found in scene.";
            yield break;
        }

        NetworkManager.Singleton.Shutdown();
        yield return null;

        try
        {
            statusMessage = "Starting host...";

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData("0.0.0.0", port);

            bool started = NetworkManager.Singleton.StartHost();
            if (!started)
            {
                statusMessage = "Failed to start host.";
                yield break;
            }

            if (string.IsNullOrWhiteSpace(currentLobbyCode))
            {
                currentLobbyCode = GenerateLobbyCode();
            }

            statusMessage = $"Hosting on port {port} - Share Code: {currentLobbyCode}";
        }
        catch (System.Exception)
        {
            statusMessage = "Host error.";
        }
    }

    public void StartClientWithLobby(string hostIpInput, string lobbyCode)
    {
        try
        {
            if (NetworkManager.Singleton == null)
            {
                statusMessage = "NetworkManager not found in scene.";
                return;
            }

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
            lobbyCode = lobbyCode.Trim().ToUpperInvariant();
            statusMessage = "Connecting to host...";

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetConnectionData(hostIp, port);

            bool started = NetworkManager.Singleton.StartClient();
            if (!started)
            {
                statusMessage = "Failed to connect to host.";
                m_IsClientLobbyConnection = false;
                return;
            }

            m_IsClientLobbyConnection = true;
            currentLobbyCode = lobbyCode;
            statusMessage = $"Connected to {hostIp}:{port}";
        }
        catch (System.Exception)
        {
            m_IsClientLobbyConnection = false;
            statusMessage = "Client error.";
        }
    }

    public void StopLobbyHost()
    {
        m_IsClientLobbyConnection = false;
        ShutdownNetworkAndReset("Lobby disbanded.");
    }

    public void StopLobbyClient()
    {
        m_IsClientLobbyConnection = false;
        ShutdownNetworkAndReset("Left lobby.");
    }

    private void OnClientDisconnect(ulong clientId)
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        if (clientId != networkManager.LocalClientId)
            return;

        if (!m_IsClientLobbyConnection || m_HandlingClientDisconnect)
            return;

        if (networkManager.IsHost || networkManager.IsServer)
            return;

        m_HandlingClientDisconnect = true;
        statusMessage = "Host ended lobby.";

        var leaveButton = UnityEngine.Object.FindFirstObjectByType<MainMenuClientLeaveLobbyButton>();
        if (leaveButton != null)
        {
            leaveButton.SimulateLeaveButtonClick();
        }
        else
        {
            StopLobbyClient();
        }

        m_HandlingClientDisconnect = false;
    }

    private void ShutdownNetworkAndReset(string message)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        currentLobbyCode = string.Empty;
        hostIp = string.Empty;
        statusMessage = message;
    }

    private string GenerateLobbyCode()
    {
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 6; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }
}

