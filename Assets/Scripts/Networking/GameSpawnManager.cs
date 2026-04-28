using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameSpawnManager : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerPrefab;

    // Called by LobbyRelayManager after all clients have confirmed the gameplay
    // scene has loaded (subscribed before LoadScene so the event is never missed).
    public void SpawnPlayersForClients(List<ulong> clientIds)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[GameSpawnManager] playerPrefab is not assigned.");
            return;
        }

        var availableSpawnPoints = BuildAvailableSpawnPoints();

        foreach (var clientId in clientIds)
        {
            // Guard against double-spawning (e.g. on respawn).
            if (NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId) != null)
                continue;

            Vector3 spawnPos = GetNextSpawnPosition(availableSpawnPoints);
            var playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            var networkObject = playerInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("[GameSpawnManager] playerPrefab does not have a NetworkObject component.");
                Destroy(playerInstance);
                continue;
            }

            networkObject.SpawnAsPlayerObject(clientId);
        }
    }

    public GameObject SpawnSingleplayerPlayer()
    {
        if (playerPrefab == null)
            return null;

        var spawnPosition = GetRandomSpawnPosition();
        return Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
    }

    public Vector3 GetRandomSpawnPosition()
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return transform.position;

        return spawnPoints[Random.Range(0, spawnPoints.Length)].position;
    }

    private List<int> BuildAvailableSpawnPoints()
    {
        var list = new List<int>();
        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
                list.Add(i);
        }
        return list;
    }

    private Vector3 GetNextSpawnPosition(List<int> available)
    {
        if (available.Count == 0)
        {
            // Refill if we've run out (more players than spawn points).
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                    available.Add(i);
            }
            else
            {
                return transform.position;
            }
        }

        int randomIndex = Random.Range(0, available.Count);
        int spawnPointIndex = available[randomIndex];
        available.RemoveAt(randomIndex);
        return spawnPoints[spawnPointIndex].position;
    }
}