using System;
using System.Collections.Generic;
using System.Text;
using Blocks.Sessions.Common;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuJoinSessionByCodeController : MonoBehaviour
{
    [SerializeField] private string sessionType = "default-session";
    [SerializeField] private string hostIpForDirectConnect = "127.0.0.1";
    [SerializeField] private GameObject[] m_OnJoinSuccessObjects;

    [Header("Lobby UI (Assign in Inspector)")]
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private string joinCodePrefix = "Code: ";
    [SerializeField] private string noJoinCodeText = "No join code";
    [SerializeField] private string noPlayersText = "No players in lobby";

    private TMP_InputField m_SessionCodeInput;
    private Button m_JoinByCodeButton;
    private LobbyRelayManager m_LobbyRelayManager;
    private ISession m_Session;
    private bool m_IsJoining;

    public ISession CurrentSession => m_Session;

    private void Awake()
    {
        m_SessionCodeInput = GetComponentInChildren<TMP_InputField>(true);
        m_JoinByCodeButton = FindButtonByText(gameObject, "Join");
        RefreshLobbyUi();
    }

    private void OnDestroy()
    {
        CleanupSession();
    }

    public async void OnJoinByCodeClicked()
    {
        if (m_IsJoining)
            return;

        var code = m_SessionCodeInput != null ? m_SessionCodeInput.text.Trim().ToUpperInvariant() : string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        try
        {
            m_IsJoining = true;
            if (m_JoinByCodeButton != null)
                m_JoinByCodeButton.interactable = false;

            await MultiplayerServiceBootstrap.EnsureInitializedAndSignedInAsync();
            await SessionLifecycleUtility.CleanupActiveSessionsAsync();
            ClearLobbySessionState();

            var joinedSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions
            {
                Type = sessionType,
            });

            foreach (var obj in m_OnJoinSuccessObjects)
                if (obj != null) obj.SetActive(true);

            SetSession(joinedSession);

            EnsureLobbyRelayManager();
            m_LobbyRelayManager.SetCurrentLobbyCode(code);
            m_LobbyRelayManager.StartClientWithLobby(hostIpForDirectConnect, code);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            m_IsJoining = false;
            if (m_JoinByCodeButton != null)
                m_JoinByCodeButton.interactable = true;
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

    private static Button FindButtonByText(GameObject root, string buttonText)
    {
        if (root == null)
            return null;

        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null && string.Equals(text.text.Trim(), buttonText, StringComparison.OrdinalIgnoreCase))
                return button;
        }

        return null;
    }
}
