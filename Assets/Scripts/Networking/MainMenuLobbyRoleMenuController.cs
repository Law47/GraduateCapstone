using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class MainMenuLobbyRoleMenuController : MonoBehaviour
{
    [Header("Session Menus")]
    [SerializeField] private GameObject hostSessionMenu;
    [SerializeField] private GameObject joinedSessionMenu;

    [Header("Optional Default Menu")]
    [SerializeField] private GameObject defaultMenuWhenOffline;

    [Header("Behavior")]
    [SerializeField] private bool runOnStart = true;

    private void OnEnable()
    {
        EnsureCursorUnlocked();
    }

    private void Start()
    {
        EnsureCursorUnlocked();

        if (!runOnStart)
            return;

        StartCoroutine(ApplyRoleMenuNextFrame());
    }

    public void ApplyRoleMenuNow()
    {
        EnsureCursorUnlocked();

        bool isHost = NetworkManager.Singleton != null &&
                      NetworkManager.Singleton.IsListening &&
                      (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer);

        bool isJoinedClient = NetworkManager.Singleton != null &&
                              NetworkManager.Singleton.IsListening &&
                              NetworkManager.Singleton.IsClient &&
                              !isHost;

        if (hostSessionMenu != null)
            hostSessionMenu.SetActive(isHost);

        if (joinedSessionMenu != null)
            joinedSessionMenu.SetActive(isJoinedClient);

        if (defaultMenuWhenOffline != null)
            defaultMenuWhenOffline.SetActive(!isHost && !isJoinedClient);
    }

    private IEnumerator ApplyRoleMenuNextFrame()
    {
        // Wait one frame so Netcode state is fully available after scene load.
        yield return null;
        ApplyRoleMenuNow();
    }

    private static void EnsureCursorUnlocked()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
