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

        var spawnPointIndex = Random.Range(0, spawnPoints.Length);
        return spawnPoints[spawnPointIndex].position;
    }

    private void SpawnAllPlayersAtRandomLocations()
    {
        var connectedClients = NetworkManager.Singleton.ConnectedClients;
        var availableSpawnPoints = new List<int>();

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            availableSpawnPoints.Add(i);
        }

        foreach (var client in connectedClients.Values)
        {
            if (availableSpawnPoints.Count == 0)
            {
                for (int i = 0; i < spawnPoints.Length; i++)
                {
                    availableSpawnPoints.Add(i);
                }
            }

            int randomIndex = Random.Range(0, availableSpawnPoints.Count);
            int spawnPointIndex = availableSpawnPoints[randomIndex];
            availableSpawnPoints.RemoveAt(randomIndex);

            Vector3 spawnPos = spawnPoints[spawnPointIndex].position;
            var playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            
            var networkObject = playerInstance.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(client.ClientId);
        }
    }
}