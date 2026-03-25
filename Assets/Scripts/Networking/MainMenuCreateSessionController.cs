using System;
using System.Collections.Generic;
using System.Text;
using Blocks.Sessions.Common;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;

public class MainMenuCreateSessionController : MonoBehaviour
{
    [Header("Session")]
    [SerializeField] private string sessionType = "default-session";
    [SerializeField] private int maxPlayers = 2;
    [SerializeField] private string fallbackSessionName = "Lobby";
    [SerializeField] private string directConnectHostIp = "127.0.0.1";

    [Header("Lobby UI (Assign in Inspector)")]
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private string joinCodePrefix = "Code: ";
    [SerializeField] private string noJoinCodeText = "No join code";
    [SerializeField] private string noPlayersText = "No players in lobby";

    private ISession m_Session;
    private TMP_InputField m_SessionNameInput;
    private LobbyRelayManager m_LobbyRelayManager;
    private bool m_IsCreating;

    public ISession CurrentSession => m_Session;

    private void Awake()
    {
        m_SessionNameInput = GetComponentInChildren<TMP_InputField>(true);
        RefreshLobbyUi();
    }

    private void OnDestroy()
    {
        CleanupSession();
    }

    public async void OnCreateSessionButtonClicked()
    {
        if (m_IsCreating)
            return;

        try
        {
            m_IsCreating = true;

            await MultiplayerServiceBootstrap.EnsureInitializedAndSignedInAsync();

            var sessionName = string.IsNullOrWhiteSpace(m_SessionNameInput?.text)
                ? fallbackSessionName
                : m_SessionNameInput.text.Trim();

            var options = new SessionOptions
            {
                Name = sessionName,
                Type = sessionType,
                MaxPlayers = maxPlayers,
            };

            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            SetSession(session);

            EnsureLobbyRelayManager();
            m_LobbyRelayManager.SetHostIp(directConnectHostIp);
            m_LobbyRelayManager.SetCurrentLobbyCode(session.Code);
            m_LobbyRelayManager.StartHostWithLobby();
        }
        catch (Exception)
        {
        }
        finally
        {
            m_IsCreating = false;
        }
    }

    public void OnStartGameButtonClicked()
    {
        if (m_Session == null)
        {
            return;
        }

        if (!m_Session.IsHost)
        {
            return;
        }

        if (m_Session.Players == null || m_Session.Players.Count < 2)
        {
            return;
        }

        EnsureLobbyRelayManager();
        m_LobbyRelayManager.StartGameplayForLobby();
    }

    private void SetSession(ISession session)
    {
        if (session == null)
            return;

        CleanupSession();

        m_Session = session;
        m_Session.Changed += OnSessionChanged;
        m_Session.PlayerJoined += OnPlayerCountChanged;
        m_Session.PlayerHasLeft += OnPlayerCountChanged;
        m_Session.PlayerPropertiesChanged += OnSessionChanged;
        m_Session.RemovedFromSession += OnSessionRemoved;
        m_Session.Deleted += OnSessionRemoved;

        RefreshLobbyUi();
    }

    private void OnSessionChanged()
    {
        RefreshLobbyUi();
    }

    private void OnPlayerCountChanged(string _)
    {
        RefreshLobbyUi();
    }

    private void OnSessionRemoved()
    {
        CleanupSession();
        RefreshLobbyUi();
    }

    private void CleanupSession()
    {
        if (m_Session == null)
            return;

        m_Session.Changed -= OnSessionChanged;
        m_Session.PlayerJoined -= OnPlayerCountChanged;
        m_Session.PlayerHasLeft -= OnPlayerCountChanged;
        m_Session.PlayerPropertiesChanged -= OnSessionChanged;
        m_Session.RemovedFromSession -= OnSessionRemoved;
        m_Session.Deleted -= OnSessionRemoved;
        m_Session = null;
    }

    public void ClearLobbySessionState()
    {
        CleanupSession();
        RefreshLobbyUi();
    }

    private void RefreshLobbyUi()
    {
        if (joinCodeText != null)
        {
            if (m_Session == null || string.IsNullOrWhiteSpace(m_Session.Code))
                joinCodeText.text = noJoinCodeText;
            else
                joinCodeText.text = joinCodePrefix + m_Session.Code;
        }

        if (playerListText != null)
        {
            if (m_Session == null || m_Session.Players == null || m_Session.Players.Count == 0)
            {
                playerListText.text = noPlayersText;
            }
            else
            {
                var builder = new StringBuilder();
                IReadOnlyList<IReadOnlyPlayer> players = m_Session.Players;
                for (var index = 0; index < players.Count; index++)
                {
                    var player = players[index];
                    var playerName = player.GetPlayerName();
                    var displayName = string.IsNullOrWhiteSpace(playerName) ? player.Id : playerName;

                    if (index > 0)
                        builder.Append('\n');

                    builder.Append(displayName);
                }

                playerListText.text = builder.ToString();
            }
        }
    }

    private void EnsureLobbyRelayManager()
    {
        if (m_LobbyRelayManager != null)
            return;

        m_LobbyRelayManager = UnityEngine.Object.FindFirstObjectByType<LobbyRelayManager>();
        if (m_LobbyRelayManager != null)
            return;

        var networkManager = Unity.Netcode.NetworkManager.Singleton;
        if (networkManager == null)
        {
            throw new InvalidOperationException("NetworkManager.Singleton was not found in the scene.");
        }

        m_LobbyRelayManager = networkManager.gameObject.AddComponent<LobbyRelayManager>();
    }
}
