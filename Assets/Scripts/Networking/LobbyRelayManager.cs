using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blocks.Sessions.Common;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Multiplayer;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyRelayManager : MonoBehaviour
{
    private const string GameplaySceneName = "Gameplay";
    private const string GameplayScenePath = "Scenes/Gameplay";
    private const string MainMenuSceneName = "Main Menu";
    private const string MainMenuScenePath = "Assets/Scenes/Main Menu.unity";
    private const string RelayJoinCodeKey = "relay-join-code";

    private string currentLobbyCode = "";
    private string statusMessage = "";
    private bool m_IsClientLobbyConnection;
    private bool m_HandlingClientDisconnect;

    public string CurrentLobbyCode => currentLobbyCode;
    public string StatusMessage => statusMessage;

    private void OnEnable()
    {
        PlayGameViewModel.GameplayStartRequested += OnGameplayStartRequested;

        // Supply the NGO client count to PlayGameViewModel (which cannot reference
        // NGO directly due to its assembly constraints).
        PlayGameViewModel.GetNgoConnectedClientCount =
            () => NetworkManager.Singleton?.ConnectedClients.Count ?? 0;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnNgoClientCountChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnNgoClientCountChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
        }
    }

    private void OnDisable()
    {
        PlayGameViewModel.GameplayStartRequested -= OnGameplayStartRequested;
        PlayGameViewModel.GetNgoConnectedClientCount = null;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnNgoClientCountChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnNgoClientCountChanged;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }

    private void OnNgoClientCountChanged(ulong clientId)
    {
        PlayGameViewModel.NgoConnectedClientsChanged?.Invoke();
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

        // Subscribe BEFORE calling LoadScene so the event is guaranteed to be
        // caught. LobbyRelayManager lives on the DontDestroyOnLoad NetworkManager
        // object, so it persists through the scene transition and cannot miss it.
        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnGameplaySceneLoadCompleted;

        var loadStatus = NetworkManager.Singleton.SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
        statusMessage = $"Gameplay scene load requested. Status: {loadStatus}";

        if (loadStatus != SceneEventProgressStatus.Started)
        {
            // Load failed immediately — clean up the subscription.
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnGameplaySceneLoadCompleted;
        }
    }

    private void OnGameplaySceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        // Only handle the gameplay scene; ignore any later scene events.
        if (sceneName != GameplaySceneName)
            return;

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnGameplaySceneLoadCompleted;

        if (clientsTimedOut.Count > 0)
            Debug.LogWarning($"[LobbyRelayManager] {clientsTimedOut.Count} client(s) timed out confirming scene load — spawning them anyway.");

        var spawnManager = UnityEngine.Object.FindFirstObjectByType<GameSpawnManager>();
        if (spawnManager == null)
        {
            Debug.LogError("[LobbyRelayManager] GameSpawnManager not found in the gameplay scene.");
            return;
        }

        // Use ConnectedClients rather than clientsCompleted because over
        // Relay/WebSocket clients can land in clientsTimedOut even when connected.
        var allClients = new List<ulong>(NetworkManager.Singleton.ConnectedClients.Keys);
        spawnManager.SpawnPlayersForClients(allClients);
    }

    public void StartGameplayForLobby()
    {
        OnGameplayStartRequested();
    }

    /// <summary>
    /// Restarts the gameplay scene via NGO's scene manager so that
    /// OnLoadEventCompleted fires and players are spawned correctly.
    /// Call this from GameManager.RestartGame() instead of Unity's raw SceneManager.
    /// </summary>
    public void RestartGameplay()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            // Offline / singleplayer fallback.
            SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
            return;
        }

        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[LobbyRelayManager] Only the host can restart the gameplay scene.");
            return;
        }

        NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnGameplaySceneLoadCompleted;

        // Dynamically spawned NetworkObjects are not bound to a scene and survive
        // a single-mode scene reload. Despawn all player objects now so that
        // SpawnPlayersForClients finds no existing objects and spawns fresh ones.
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(client.ClientId);
            if (playerObj != null)
                playerObj.Despawn(destroy: true);
        }

        var loadStatus = NetworkManager.Singleton.SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
        statusMessage = $"Gameplay scene restart requested. Status: {loadStatus}";

        if (loadStatus != SceneEventProgressStatus.Started)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnGameplaySceneLoadCompleted;
            Debug.LogError($"[LobbyRelayManager] Restart scene load failed immediately: {loadStatus}");
        }
    }

    public async Task StartHostWithLobbyAsync(IHostSession hostSession)
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "NetworkManager not found in scene.";
            return;
        }

        try
        {
            statusMessage = "Creating Relay allocation...";

            // Create a Relay allocation; maxConnections is the number of non-host players
            int maxConnections = Math.Max(1, hostSession.MaxPlayers - 1);
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Store the Relay join code in the session so clients can retrieve it
            hostSession.SetProperty(RelayJoinCodeKey,
                new Unity.Services.Multiplayer.SessionProperty(relayJoinCode, VisibilityPropertyOptions.Member));
            await hostSession.SavePropertiesAsync();

            // Configure UnityTransport to use Relay with wss (WebSocket Secure) for WebGL
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationToRelayServerData(allocation));

            NetworkManager.Singleton.Shutdown();
            await Task.Yield();

            statusMessage = "Starting relay host...";
            bool started = NetworkManager.Singleton.StartHost();
            if (!started)
            {
                statusMessage = "Failed to start relay host.";
                return;
            }

            statusMessage = $"Hosting via Relay - Share Code: {currentLobbyCode}";
        }
        catch (Exception ex)
        {
            statusMessage = "Relay host error.";
            Debug.LogException(ex);
        }
    }

    public async Task StartClientWithLobbyAsync(ISession session)
    {
        if (NetworkManager.Singleton == null)
        {
            statusMessage = "NetworkManager not found in scene.";
            return;
        }

        try
        {
            // Read the Relay join code stored by the host in session properties
            if (!session.Properties.TryGetValue(RelayJoinCodeKey, out var relayCodeProp)
                || string.IsNullOrWhiteSpace(relayCodeProp?.Value))
            {
                statusMessage = "Relay join code not found in session properties.";
                Debug.LogWarning("[LobbyRelayManager] Relay join code missing from session. The host may not have set up Relay yet.");
                return;
            }

            statusMessage = "Joining Relay allocation...";

            // Join the Relay allocation and configure transport with wss for WebGL
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCodeProp.Value);
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(JoinAllocationToRelayServerData(joinAllocation));

            bool started = NetworkManager.Singleton.StartClient();
            if (!started)
            {
                statusMessage = "Failed to connect via Relay.";
                m_IsClientLobbyConnection = false;
                return;
            }

            m_IsClientLobbyConnection = true;
            statusMessage = "Connected via Relay.";
        }
        catch (Exception ex)
        {
            m_IsClientLobbyConnection = false;
            statusMessage = "Relay client error.";
            Debug.LogException(ex);
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
        statusMessage = message;

        LoadMainMenuScene();
    }

    private void LoadMainMenuScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == MainMenuSceneName || activeScene.path.EndsWith("/Main Menu.unity"))
        {
            UnlockCursorForMainMenu();
            return;
        }

        if (SceneUtility.GetBuildIndexByScenePath(MainMenuScenePath) >= 0)
        {
            PrepareNetworkManagerForMainMenuLoad();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            UnlockCursorForMainMenu();
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            return;
        }

        Debug.LogWarning($"Main menu scene was not found in build settings at path '{MainMenuScenePath}'.");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (scene.name != MainMenuSceneName && !scene.path.EndsWith("/Main Menu.unity"))
            return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnlockCursorForMainMenu();
    }

    private static void UnlockCursorForMainMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void PrepareNetworkManagerForMainMenuLoad()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        UnityEngine.Object.Destroy(networkManager.gameObject);
    }

    private string GenerateLobbyCode()
    {
        string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 6; i++)
        {
            code += chars[UnityEngine.Random.Range(0, chars.Length)];
        }
        return code;
    }
    // Build RelayServerData using the wss endpoint from an Allocation (host side).
    // Uses the explicit isWebSocket parameter to avoid requiring RELAY_SDK_INSTALLED define.
    private static RelayServerData AllocationToRelayServerData(Allocation allocation)
    {
        var ep = allocation.ServerEndpoints.First(e => e.ConnectionType == "wss");
        return new RelayServerData(ep.Host, (ushort)ep.Port,
            allocation.AllocationIdBytes, allocation.ConnectionData,
            allocation.ConnectionData,  // hostConnectionData == connectionData for the host
            allocation.Key, isSecure: ep.Secure, isWebSocket: true);
    }

    // Build RelayServerData using the wss endpoint from a JoinAllocation (client side).
    private static RelayServerData JoinAllocationToRelayServerData(JoinAllocation joinAllocation)
    {
        var ep = joinAllocation.ServerEndpoints.First(e => e.ConnectionType == "wss");
        return new RelayServerData(ep.Host, (ushort)ep.Port,
            joinAllocation.AllocationIdBytes, joinAllocation.ConnectionData,
            joinAllocation.HostConnectionData, joinAllocation.Key,
            isSecure: ep.Secure, isWebSocket: true);
    }}

