using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameSpawnManager : MonoBehaviour
{
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerPrefab;
    
    private bool m_AutoSpawnOnStart = true;

    private void Start()
    {
        if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            return;
        
        if (m_AutoSpawnOnStart)
        {
            SpawnAllPlayers();
        }
    }

    public void SetAutoSpawnOnStart(bool autoSpawn)
    {
        m_AutoSpawnOnStart = autoSpawn;
    }

    public void SpawnAllPlayers()
    {
        SpawnAllPlayersAtRandomLocations();
    }

    private void SpawnAllPlayersAtRandomLocations()
    {
        var connectedClients = NetworkManager.Singleton.ConnectedClients;
        var availableSpawnPoints = new List<int>();
        
        // Initialize list with all spawn point indices
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            availableSpawnPoints.Add(i);
        }

        int clientIndex = 0;
        foreach (var client in connectedClients.Values)
        {
            // If we've used all spawn points, start reusing them
            if (availableSpawnPoints.Count == 0)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    availableSpawnPoints.Add(i);
                }
            }

            // Pick a random spawn point from available ones
            int randomIndex = Random.Range(0, availableSpawnPoints.Count);
            int spawnPointIndex = availableSpawnPoints[randomIndex];
            availableSpawnPoints.RemoveAt(randomIndex);

            Vector3 spawnPos = spawnPoints[spawnPointIndex].position;
            var playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            
            var networkObject = playerInstance.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(client.ClientId);
            
            clientIndex++;
        }
    }
}