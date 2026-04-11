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
    [SerializeField] private int headshotMultiplier = 2;

    [Header("Magazine & Fire Rate")]
    [SerializeField] private int magazineSize = 30;
    [SerializeField] private float fireRate = 0.1f; // seconds between shots
    [SerializeField] private float reloadTime = 2f; // seconds to reload

    [Header("Bullet Tracer")]
    [SerializeField] private LineRenderer m_BulletTracer;
    [SerializeField] private float m_TracerDuration = 0.1f;

    private InputSystem_Actions m_InputActions;
    private Camera m_PlayerCamera;
    private Coroutine m_TracerCoroutine;

    private int m_CurrentMagazineAmmo;
    private float m_LastShotTime;
    private bool m_IsReloading;
    private Coroutine m_ReloadCoroutine;

    public int CurrentAmmo => m_CurrentMagazineAmmo;
    public int MagazineSize => magazineSize;

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

    private void Update()
    {
        if (!IsOwner)
            return;

        HandleContinuousFire();
        HandleReload();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
            return;

        m_CurrentMagazineAmmo = magazineSize;
        m_LastShotTime = -fireRate;
        m_IsReloading = false;

        m_InputActions.Player.Reload.performed += OnReloadPerformed;
        m_InputActions.Player.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && m_InputActions != null)
        {
            m_InputActions.Player.Reload.performed -= OnReloadPerformed;
            m_InputActions.Player.Disable();
        }

        if (m_ReloadCoroutine != null)
        {
            StopCoroutine(m_ReloadCoroutine);
            m_ReloadCoroutine = null;
        }

        base.OnNetworkDespawn();
    }

    private void HandleContinuousFire()
    {
        if (m_IsReloading)
            return;

        if (!m_InputActions.Player.Attack.IsPressed())
            return;

        if (m_CurrentMagazineAmmo <= 0)
        {
            m_ReloadCoroutine = StartCoroutine(ReloadCoroutine());
            return;
        }

        if (Time.time - m_LastShotTime < fireRate)
            return;

        if (!TryGetShotRay(out var origin, out var direction))
            return;

        m_LastShotTime = Time.time;
        m_CurrentMagazineAmmo--;

        ShootServerRpc(origin, direction);
    }

    private void HandleReload()
    {
        // Reload is handled by input action and coroutine
    }

    private void OnReloadPerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;

        if (m_IsReloading)
            return;

        if (m_CurrentMagazineAmmo == magazineSize)
            return;

        m_ReloadCoroutine = StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        m_IsReloading = true;
        yield return new WaitForSeconds(reloadTime);
        m_CurrentMagazineAmmo = magazineSize;
        m_IsReloading = false;
        m_ReloadCoroutine = null;
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
                headHitbox.ApplyHitDamage(bulletDamage, OwnerClientId, headshotMultiplier);
                break;
            }

            var targetPlayerManager = hit.collider.GetComponentInParent<PlayerManager>();

            if (targetPlayerManager != null && targetPlayerManager.NetworkObjectId == NetworkObjectId)
                continue;

            hitPoint = hit.point;

            if (targetPlayerManager != null)
                targetPlayerManager.ApplyDamage(bulletDamage, OwnerClientId);

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
