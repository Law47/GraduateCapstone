using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class HostManager : MonoBehaviour
{
    public ushort port = 7777;

    public void StartHost()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        transport.SetConnectionData("0.0.0.0", port);

        NetworkManager.Singleton.StartHost();
    }
}