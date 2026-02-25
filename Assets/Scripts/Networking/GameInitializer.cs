using Unity.Netcode;
using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    private void Start()
    {
        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            return;

        // Spawn all players at random locations when this scene loads
        var spawnManager = UnityEngine.Object.FindFirstObjectByType<GameSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.SpawnAllPlayers();
        }
        else
        {
            Debug.LogError("GameSpawnManager not found in Gameplay scene!");
        }
    }
}
