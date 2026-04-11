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

    void Start()
    {
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
    /// Call this when a player gets a kill. Adds 1 point to the killer.
    /// </summary>
    /// <param name="playerName">The name/ID of the player who got the kill</param>
    public void OnPlayerKill(string playerName)
    {
        if (gameOver)
            return;

        // Add or update player score
        if (!playerScores.ContainsKey(playerName))
        {
            playerScores[playerName] = 0;
        }

        playerScores[playerName]++;
        RefreshGameplayScoreboard();

        // Check if player has reached win condition
        if (playerScores[playerName] >= pointsToWin)
        {
            EndGame(playerName, null);
        }
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

    /// <summary>
    /// Call this to register a new player in the game (initialize their score to 0)
    /// </summary>
    /// <param name="playerName">The name/ID of the player</param>
    public void RegisterPlayer(string playerName)
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

    private void EndGame(string winnerName, ulong? winnerClientId)
    {
        gameOver = true;

        // Ensure everyone can interact with end-of-match UI immediately.
        UnlockCursorForMenu();

        UpdateHostOnlyControls();

        bool localPlayerWon = !winnerClientId.HasValue;
        if (winnerClientId.HasValue && NetworkManager.Singleton != null)
        {
            localPlayerWon = NetworkManager.Singleton.LocalClientId == winnerClientId.Value;
        }

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

        // Reload the current scene to reset gameplay
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Call this from a UI button to return all players to the lobby.
    /// Loads the main menu scene and activates the lobby submenu.
    /// </summary>
    public void ReturnToLobby()
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("Only the host can return everyone to lobby.");
                return;
            }

            var loadStatus = NetworkManager.Singleton.SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            Debug.Log($"Return to lobby scene load requested. Status: {loadStatus}");
        }
        else
        {
            // Fallback for local/offline mode.
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }
    }
}
