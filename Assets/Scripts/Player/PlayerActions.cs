using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : NetworkBehaviour
{
    [Header("Shooting")]
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float shootRange = 100f;
    [SerializeField] private Transform firePoint;

    private InputSystem_Actions m_InputActions;
    private Camera m_PlayerCamera;

    private void Awake()
    {
        m_InputActions = new InputSystem_Actions();
        m_PlayerCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
            return;

        m_InputActions.Player.Attack.performed += OnAttackPerformed;
        m_InputActions.Player.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && m_InputActions != null)
        {
            m_InputActions.Player.Attack.performed -= OnAttackPerformed;
            m_InputActions.Player.Disable();
        }

        base.OnNetworkDespawn();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;

        if (!TryGetShotRay(out var origin, out var direction))
            return;

        ShootServerRpc(origin, direction);
    }

    private bool TryGetShotRay(out Vector3 origin, out Vector3 direction)
    {
        if (firePoint != null)
        {
            origin = firePoint.position;
            direction = firePoint.forward;
            return true;
        }

        if (m_PlayerCamera != null)
        {
            origin = m_PlayerCamera.transform.position;
            direction = m_PlayerCamera.transform.forward;
            return true;
        }

        origin = transform.position + Vector3.up;
        direction = transform.forward;
        return true;
    }

    [ServerRpc]
    private void ShootServerRpc(Vector3 origin, Vector3 direction)
    {
        if (!Physics.Raycast(origin, direction, out var hit, shootRange))
            return;

        var targetPlayerManager = hit.collider.GetComponentInParent<PlayerManager>();
        if (targetPlayerManager == null)
            return;

        if (targetPlayerManager.NetworkObjectId == NetworkObjectId)
            return;

        targetPlayerManager.ApplyDamage(bulletDamage);
    }
}
