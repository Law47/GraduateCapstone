using Unity.Netcode;
using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    private void Start()
    {
        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            return;

        var spawnManager = UnityEngine.Object.FindFirstObjectByType<GameSpawnManager>();
        if (spawnManager != null)
        {
            spawnManager.SpawnAllPlayers();
        }
    }
}
