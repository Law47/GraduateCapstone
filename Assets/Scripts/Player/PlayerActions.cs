using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : NetworkBehaviour
{
    [Header("Shooting")]
    [SerializeField] private int bulletDamage = 10;
    [SerializeField] private float shootRange = 100f;
    [SerializeField] private Transform firePoint;

    [Header("Bullet Tracer")]
    [SerializeField] private LineRenderer m_BulletTracer;
    [SerializeField] private float m_TracerDuration = 0.1f;

    private InputSystem_Actions m_InputActions;
    private Camera m_PlayerCamera;
    private Coroutine m_TracerCoroutine;

    private void Awake()
    {
        m_InputActions = new InputSystem_Actions();
        m_PlayerCamera = GetComponentInChildren<Camera>(true);

        if (m_BulletTracer == null)
        {
            m_BulletTracer = gameObject.AddComponent<LineRenderer>();
            m_BulletTracer.material = new Material(Shader.Find("Sprites/Default"));
            m_BulletTracer.startColor = Color.yellow;
            m_BulletTracer.endColor = new Color(1f, 0.5f, 0f);
            m_BulletTracer.startWidth = 0.03f;
            m_BulletTracer.endWidth = 0.01f;
            m_BulletTracer.positionCount = 2;
            m_BulletTracer.useWorldSpace = true;
            m_BulletTracer.enabled = false;
        }
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
        var hitPoint = origin + direction.normalized * shootRange;

        var hits = Physics.RaycastAll(origin, direction, shootRange);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var headHitbox = hit.collider.GetComponent<HeadHitboxDamage>();
            if (headHitbox != null)
            {
                if (headHitbox.TargetPlayerManager != null && headHitbox.TargetPlayerManager.NetworkObjectId == NetworkObjectId)
                    continue;

                hitPoint = hit.point;
                headHitbox.ApplyHitDamage(bulletDamage);
                break;
            }

            var targetPlayerManager = hit.collider.GetComponentInParent<PlayerManager>();

            if (targetPlayerManager != null && targetPlayerManager.NetworkObjectId == NetworkObjectId)
                continue;

            hitPoint = hit.point;

            if (targetPlayerManager != null)
                targetPlayerManager.ApplyDamage(bulletDamage);

            break;
        }

        ShowTracerClientRpc(origin, hitPoint);
    }

    [ClientRpc]
    private void ShowTracerClientRpc(Vector3 start, Vector3 end)
    {
        if (m_BulletTracer == null)
            return;

        m_BulletTracer.SetPosition(0, start);
        m_BulletTracer.SetPosition(1, end);
        m_BulletTracer.enabled = true;

        if (m_TracerCoroutine != null)
            StopCoroutine(m_TracerCoroutine);

        m_TracerCoroutine = StartCoroutine(HideTracerAfterDelay());
    }

    private IEnumerator HideTracerAfterDelay()
    {
        yield return new WaitForSeconds(m_TracerDuration);

        if (m_BulletTracer != null)
            m_BulletTracer.enabled = false;
    }
}
