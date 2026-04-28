using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class GameManager : MonoBehaviour
{
    [Header("Win/Lose Conditions")]
    [SerializeField] private int pointsToWin = 10;

    [Header("UI References - Gameplay Scoreboard")]
    [SerializeField] private TextMeshProUGUI gameplayScoresText; // Multiline TMP text that shows live player scores

    [Header("UI References - Win Menu")]
    [SerializeField] private GameObject winMenuPanel;
    [SerializeField] private TextMeshProUGUI winnerNameText;
    [SerializeField] private GameObject hostControlsWin;

    [Header("UI References - Lose Menu")]
    [SerializeField] private GameObject loseMenuPanel;
    [SerializeField] private GameObject hostControlsLose;

    [Header("Scene Management")]
    [SerializeField] private string mainMenuSceneName = "Main Menu"; // Name of the main menu scene to load

    private Dictionary<string, int> playerScores = new Dictionary<string, int>();
    private bool gameOver = false;
    private bool m_IsReturningToMainMenu;

    void Start()
    {
        // Lock cursor for gameplay on every client, including after a scene restart.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize the game
        if (winMenuPanel != null)
            winMenuPanel.SetActive(false);

        if (loseMenuPanel != null)
            loseMenuPanel.SetActive(false);

        UpdateHostOnlyControls();

        RegisterAllConnectedPlayers();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        RefreshGameplayScoreboard();
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    /// <summary>
    /// Call this when a player gets a kill and only a clientId is known.
    /// </summary>
    public void OnPlayerKillByClientId(ulong killerClientId)
    {
        if (gameOver)
            return;

        string killerName = GetDisplayNameForClientId(killerClientId);
        if (!playerScores.ContainsKey(killerName))
        {
            playerScores[killerName] = 0;
        }

        playerScores[killerName]++;
        RefreshGameplayScoreboard();

        if (playerScores[killerName] >= pointsToWin)
        {
            EndGame(killerName, killerClientId);
        }
    }

    private void RegisterPlayer(string playerName)
    {
        if (!playerScores.ContainsKey(playerName))
        {
            playerScores[playerName] = 0;
            RefreshGameplayScoreboard();
        }
    }

    public void RegisterPlayerByClientId(ulong clientId)
    {
        RegisterPlayer(GetDisplayNameForClientId(clientId));
    }

    private void RegisterAllConnectedPlayers()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        foreach (var client in networkManager.ConnectedClientsList)
        {
            RegisterPlayerByClientId(client.ClientId);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        RegisterPlayerByClientId(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        var networkManager = NetworkManager.Singleton;
        bool isLocalDisconnect = networkManager != null && clientId == networkManager.LocalClientId;
        bool isClientOnly = networkManager != null && networkManager.IsClient && !networkManager.IsHost && !networkManager.IsServer;

        if (isLocalDisconnect && isClientOnly)
        {
            ReturnLocalPlayerToMainMenu();
            return;
        }

        string playerName = GetDisplayNameForClientId(clientId);
        if (playerScores.Remove(playerName))
        {
            RefreshGameplayScoreboard();
        }
    }

    private static string GetDisplayNameForClientId(ulong clientId)
    {
        return $"Player {clientId}";
    }

    private void EndGame(string winnerName, ulong winnerClientId)
    {
        gameOver = true;

        // Ensure everyone can interact with end-of-match UI immediately.
        UnlockCursorForMenu();

        UpdateHostOnlyControls();

        bool localPlayerWon = NetworkManager.Singleton != null &&
                              NetworkManager.Singleton.LocalClientId == winnerClientId;

        // Winner sees win menu, everyone else sees lose menu.
        if (winMenuPanel != null)
            winMenuPanel.SetActive(localPlayerWon);

        if (loseMenuPanel != null)
            loseMenuPanel.SetActive(!localPlayerWon);

        if (winnerNameText != null)
            winnerNameText.text = winnerName + " Wins!";
    }

    private static void UnlockCursorForMenu()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void UpdateHostOnlyControls()
    {
        bool isHost = NetworkManager.Singleton != null &&
                      NetworkManager.Singleton.IsListening &&
                      (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

        if (hostControlsWin != null)
            hostControlsWin.SetActive(isHost);

        if (hostControlsLose != null)
            hostControlsLose.SetActive(isHost);
    }

    private void WriteScoresToText(TextMeshProUGUI targetText)
    {
        if (targetText == null)
            return;

        targetText.text = BuildScoresText();
    }

    private string BuildScoresText()
    {
        if (playerScores.Count == 0)
            return "No scores yet";

        var orderedScores = new List<KeyValuePair<string, int>>(playerScores);
        orderedScores.Sort((left, right) =>
        {
            int scoreCompare = right.Value.CompareTo(left.Value);
            if (scoreCompare != 0)
                return scoreCompare;

            return string.Compare(left.Key, right.Key, System.StringComparison.OrdinalIgnoreCase);
        });

        var lines = new System.Text.StringBuilder();
        for (int index = 0; index < orderedScores.Count; index++)
        {
            var entry = orderedScores[index];
            if (index > 0)
                lines.Append('\n');

            lines.Append(entry.Key);
            lines.Append(": ");
            lines.Append(entry.Value);
            lines.Append(" Points");
        }

        return lines.ToString();
    }

    private void RefreshGameplayScoreboard()
    {
        WriteScoresToText(gameplayScoresText);
    }

    /// <summary>
    /// Call this from a UI button to restart the game.
    /// Resets all scores and hides the end game menus.
    /// </summary>
    public void RestartGame()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            !NetworkManager.Singleton.IsHost &&
            !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Only the host can restart the game.");
            return;
        }

        gameOver = false;
        playerScores.Clear();
        if (winMenuPanel != null)
            winMenuPanel.SetActive(false);

        if (loseMenuPanel != null)
            loseMenuPanel.SetActive(false);

        RefreshGameplayScoreboard();

        // Use LobbyRelayManager so NGO's scene manager drives the load and
        // OnLoadEventCompleted fires — which is what triggers player spawning.
        var lobbyRelayManager = UnityEngine.Object.FindFirstObjectByType<LobbyRelayManager>();
        if (lobbyRelayManager != null)
        {
            lobbyRelayManager.RestartGameplay();
        }
        else
        {
            // Offline / editor fallback only.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    /// <summary>
    /// Call this from a UI button to return all players to the lobby.
    /// Loads the main menu scene and activates the lobby submenu.
    /// </summary>
    public void ReturnToLobby()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("Only the host can return everyone to lobby.");
                return;
            }

            m_IsReturningToMainMenu = true;
            DespawnAllPlayersOnServer();

            // End the network session entirely, clients will return to menu via disconnect callback.
            NetworkManager.Singleton.Shutdown();
            LoadMainMenuLocally();
            m_IsReturningToMainMenu = false;
        }
        else
        {
            // Fallback for local/offline mode.
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }
    }

    private void ReturnLocalPlayerToMainMenu()
    {
        if (m_IsReturningToMainMenu)
            return;

        m_IsReturningToMainMenu = true;
        LoadMainMenuLocally();
        m_IsReturningToMainMenu = false;
    }

    private void LoadMainMenuLocally()
    {
        UnlockCursorForMenu();
        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private static void DespawnAllPlayersOnServer()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
            return;

        var spawnedObjects = networkManager.SpawnManager.SpawnedObjectsList;
        if (spawnedObjects == null || spawnedObjects.Count == 0)
            return;

        // Copy first to avoid modifying collection while iterating.
        var toDespawn = new List<NetworkObject>(spawnedObjects.Count);
        foreach (var networkObject in spawnedObjects)
        {
            if (networkObject == null || !networkObject.IsSpawned)
                continue;

            if (networkObject.GetComponent<PlayerManager>() != null)
            {
                toDespawn.Add(networkObject);
            }
        }

        foreach (var networkObject in toDespawn)
        {
            networkObject.Despawn(true);
        }
    }
}
