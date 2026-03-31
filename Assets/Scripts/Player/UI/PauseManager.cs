using System;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private GameObject pauseMenuButtons;

    private bool m_IsPaused;
    private bool m_IsQuitting;
    private bool m_WasMovementEnabled;
    private bool m_WasActionsEnabled;
    private InputSystem_Actions m_InputActions;

    private PlayerMovement m_LocalPlayerMovement;
    private PlayerActions m_LocalPlayerActions;
    private LobbyRelayManager m_LobbyRelayManager;
    private MainMenuCreateSessionController m_HostSessionController;
    private MainMenuJoinSessionByCodeController m_JoinSessionController;

    private void Awake()
    {
        m_InputActions = new InputSystem_Actions();

        if (pauseMenuButtons != null)
            pauseMenuButtons.SetActive(false);

        CacheSessionReferences();
    }

    private void OnEnable()
    {
        if (m_InputActions == null)
            m_InputActions = new InputSystem_Actions();

        m_InputActions.Player.Pause.performed += OnPausePerformed;
        m_InputActions.Player.Enable();
    }

    private void OnDisable()
    {
        if (m_InputActions != null)
        {
            m_InputActions.Player.Pause.performed -= OnPausePerformed;
            m_InputActions.Player.Disable();
        }

        if (m_IsPaused)
            SetPaused(false);
    }

    private void OnPausePerformed(InputAction.CallbackContext _)
    {
        TogglePause();
    }

    public void OnPauseButtonClicked()
    {
        TogglePause();
    }

    private void TogglePause()
    {
        if (m_IsQuitting)
            return;

        SetPaused(!m_IsPaused);
    }

    private void SetPaused(bool shouldPause)
    {
        if (m_IsPaused == shouldPause)
            return;

        m_IsPaused = shouldPause;

        if (pauseMenuButtons != null)
            pauseMenuButtons.SetActive(shouldPause);

        if (shouldPause)
        {
            DisableLocalPlayerControl();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            RestoreLocalPlayerControl();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void DisableLocalPlayerControl()
    {
        CacheLocalPlayerReferences();

        if (m_LocalPlayerMovement != null)
        {
            m_WasMovementEnabled = m_LocalPlayerMovement.enabled;
            m_LocalPlayerMovement.enabled = false;
        }

        if (m_LocalPlayerActions != null)
        {
            m_WasActionsEnabled = m_LocalPlayerActions.enabled;
            m_LocalPlayerActions.enabled = false;
        }
    }

    private void RestoreLocalPlayerControl()
    {
        if (m_LocalPlayerMovement != null)
            m_LocalPlayerMovement.enabled = m_WasMovementEnabled;

        if (m_LocalPlayerActions != null)
            m_LocalPlayerActions.enabled = m_WasActionsEnabled;
    }

    private void CacheLocalPlayerReferences()
    {
        if (m_LocalPlayerMovement == null)
        {
            var players = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var player in players)
            {
                if (player != null && player.IsOwner)
                {
                    m_LocalPlayerMovement = player;
                    break;
                }
            }
        }

        if (m_LocalPlayerActions == null)
        {
            var actions = FindObjectsByType<PlayerActions>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var playerAction in actions)
            {
                if (playerAction != null && playerAction.IsOwner)
                {
                    m_LocalPlayerActions = playerAction;
                    break;
                }
            }
        }
    }

    private void CacheSessionReferences()
    {
        if (m_LobbyRelayManager == null)
            m_LobbyRelayManager = UnityEngine.Object.FindFirstObjectByType<LobbyRelayManager>();

        if (m_HostSessionController == null)
            m_HostSessionController = UnityEngine.Object.FindFirstObjectByType<MainMenuCreateSessionController>();

        if (m_JoinSessionController == null)
            m_JoinSessionController = UnityEngine.Object.FindFirstObjectByType<MainMenuJoinSessionByCodeController>();
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public async void QuitGameFromLobby()
    {
        if (m_IsQuitting)
            return;

        m_IsQuitting = true;

        try
        {
            SetPaused(false);
            CacheSessionReferences();

            bool isHost = NetworkManager.Singleton != null &&
                          (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

            if (isHost)
            {
                var hostSession = m_HostSessionController != null
                    ? m_HostSessionController.CurrentSession
                    : SessionLifecycleUtility.TryGetAnyActiveSession();
                await SessionLifecycleUtility.TryDeleteSessionAsync(hostSession);

                if (m_HostSessionController != null)
                    m_HostSessionController.ClearLobbySessionState();

                if (m_LobbyRelayManager != null)
                    m_LobbyRelayManager.StopLobbyHost();
            }
            else
            {
                var joinedSession = m_JoinSessionController != null
                    ? m_JoinSessionController.CurrentSession
                    : SessionLifecycleUtility.TryGetAnyActiveSession();
                await SessionLifecycleUtility.TryLeaveSessionAsync(joinedSession);

                if (m_JoinSessionController != null)
                    m_JoinSessionController.ClearLobbySessionState();

                if (m_LobbyRelayManager != null)
                    m_LobbyRelayManager.StopLobbyClient();
            }

            if (MultiplayerService.Instance?.Sessions != null && MultiplayerService.Instance.Sessions.Count > 0)
                await SessionLifecycleUtility.CleanupActiveSessionsAsync();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            m_IsQuitting = false;
        }
    }
}
