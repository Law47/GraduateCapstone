using System;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuJoinSessionByCodeController : MonoBehaviour
{
    [SerializeField] private string sessionType = "default-session";
    [SerializeField] private string hostIpForDirectConnect = "127.0.0.1";
    [SerializeField] private GameObject[] m_OnJoinSuccessObjects;

    private TMP_InputField m_SessionCodeInput;
    private Button m_JoinByCodeButton;
    private LobbyRelayManager m_LobbyRelayManager;
    private bool m_IsJoining;

    private void Awake()
    {
        m_SessionCodeInput = GetComponentInChildren<TMP_InputField>(true);
        m_JoinByCodeButton = FindButtonByText(gameObject, "Join");
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

            var joinedSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, new JoinSessionOptions
            {
                Type = sessionType,
            });

            foreach (var obj in m_OnJoinSuccessObjects)
                if (obj != null) obj.SetActive(true);

            EnsureLobbyRelayManager();
            m_LobbyRelayManager.SetCurrentLobbyCode(code);
            m_LobbyRelayManager.StartClientWithLobby(hostIpForDirectConnect, code);
        }
        catch (Exception)
        {
            // Invalid or non-existent code — do nothing.
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
