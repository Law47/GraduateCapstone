using System;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuClientLeaveLobbyButton : MonoBehaviour
{
    [SerializeField] private MainMenuJoinSessionByCodeController joinSessionController;
    [SerializeField] private LobbyRelayManager lobbyRelayManager;

    private Button m_Button;
    private bool m_IsLeaving;
    private bool m_IsSimulatingClick;

    private void Awake()
    {
        m_Button = GetComponent<Button>();
        EnsureReferences();
    }

    public void SimulateLeaveButtonClick()
    {
        if (m_IsLeaving || m_IsSimulatingClick)
            return;

        if (m_Button == null)
        {
            OnLeaveLobbyClicked();
            return;
        }

        try
        {
            m_IsSimulatingClick = true;
            m_Button.onClick.Invoke();
        }
        finally
        {
            m_IsSimulatingClick = false;
        }
    }

    public async void OnLeaveLobbyClicked()
    {
        if (m_IsLeaving)
            return;

        try
        {
            m_IsLeaving = true;

            EnsureReferences();

            var session = joinSessionController != null ? joinSessionController.CurrentSession : null;
            await SessionLifecycleUtility.TryLeaveSessionAsync(session);

            if (joinSessionController != null)
                joinSessionController.ClearLobbySessionState();

            if (lobbyRelayManager != null)
                lobbyRelayManager.StopLobbyClient();
        }
        catch (Exception)
        {
        }
        finally
        {
            m_IsLeaving = false;
        }
    }

    private void EnsureReferences()
    {
        if (joinSessionController == null)
            joinSessionController = UnityEngine.Object.FindFirstObjectByType<MainMenuJoinSessionByCodeController>();

        if (lobbyRelayManager == null)
            lobbyRelayManager = UnityEngine.Object.FindFirstObjectByType<LobbyRelayManager>();
    }
}
