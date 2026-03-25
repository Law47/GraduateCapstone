using System;
using UnityEngine;

public class MainMenuHostDisbandLobbyButton : MonoBehaviour
{
    [SerializeField] private MainMenuCreateSessionController hostSessionController;
    [SerializeField] private LobbyRelayManager lobbyRelayManager;

    private bool m_IsDisbanding;

    private void Awake()
    {
        EnsureReferences();
    }

    public async void OnDisbandLobbyClicked()
    {
        if (m_IsDisbanding)
            return;

        try
        {
            m_IsDisbanding = true;

            EnsureReferences();

            var session = hostSessionController != null ? hostSessionController.CurrentSession : null;
            await SessionLifecycleUtility.TryDeleteSessionAsync(session);

            if (hostSessionController != null)
                hostSessionController.ClearLobbySessionState();

            if (lobbyRelayManager != null)
                lobbyRelayManager.StopLobbyHost();
        }
        catch (Exception)
        {
        }
        finally
        {
            m_IsDisbanding = false;
        }
    }

    private void EnsureReferences()
    {
        if (hostSessionController == null)
            hostSessionController = UnityEngine.Object.FindFirstObjectByType<MainMenuCreateSessionController>();

        if (lobbyRelayManager == null)
            lobbyRelayManager = UnityEngine.Object.FindFirstObjectByType<LobbyRelayManager>();
    }
}
