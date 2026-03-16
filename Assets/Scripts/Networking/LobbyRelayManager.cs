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

    public string CurrentLobbyCode => currentLobbyCode;
    public string StatusMessage => statusMessage;
    public string HostIp => hostIp;

    private void OnEnable()
    {
        PlayGameViewModel.GameplayStartRequested += OnGameplayStartRequested;
    }

    private void OnDisable()
    {
        PlayGameViewModel.GameplayStartRequested -= OnGameplayStartRequested;
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
                return;
            }

            currentLobbyCode = lobbyCode;
            statusMessage = $"Connected to {hostIp}:{port}";
        }
        catch (System.Exception)
        {
            statusMessage = "Client error.";
        }
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

