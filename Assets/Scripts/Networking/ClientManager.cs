using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class ClientManager : MonoBehaviour
{
    public void Join(string ip, ushort port)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData(ip, port);

        NetworkManager.Singleton.StartClient();
    }
}