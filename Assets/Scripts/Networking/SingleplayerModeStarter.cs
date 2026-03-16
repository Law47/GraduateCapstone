using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SingleplayerModeStarter : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "Gameplay";

    private static bool s_PendingSingleplayerSpawn;
    private static bool s_HookRegistered;
    private static string s_TargetGameplaySceneName = "Gameplay";

    public void OnStartSingleplayerClicked()
    {
        s_PendingSingleplayerSpawn = true;
        s_TargetGameplaySceneName = gameplaySceneName;
        EnsureSceneHook();

        var networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    private static void EnsureSceneHook()
    {
        if (s_HookRegistered)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        s_HookRegistered = true;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (!s_PendingSingleplayerSpawn)
            return;

        if (!IsGameplayScene(scene))
            return;

        s_PendingSingleplayerSpawn = false;

        var spawnManager = Object.FindFirstObjectByType<GameSpawnManager>();
        if (spawnManager == null)
            return;

        spawnManager.SpawnSingleplayerPlayer();
    }

    private static bool IsGameplayScene(Scene scene)
    {
        if (scene.name == s_TargetGameplaySceneName)
            return true;

        return !string.IsNullOrWhiteSpace(scene.path) && scene.path.EndsWith($"/{s_TargetGameplaySceneName}.unity");
    }
}
