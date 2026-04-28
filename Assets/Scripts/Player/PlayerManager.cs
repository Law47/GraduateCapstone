using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    private readonly NetworkVariable<int> m_CurrentHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private CharacterController m_CharacterController;
    private ulong? m_LastDamageDealerClientId;
    private bool m_IsDying;

    public int CurrentHealth => m_CurrentHealth.Value;
    public int MaxHealth => maxHealth;

    private void Awake()
    {
        m_CharacterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        m_CurrentHealth.OnValueChanged += OnCurrentHealthChanged;

        if (IsServer)
        {
            m_CurrentHealth.Value = maxHealth;
        }

        currentHealth = m_CurrentHealth.Value;
    }

    public override void OnNetworkDespawn()
    {
        m_CurrentHealth.OnValueChanged -= OnCurrentHealthChanged;
        base.OnNetworkDespawn();
    }

    public void ApplyDamage(int damage)
    {
        ApplyDamage(damage, ulong.MaxValue);
    }

    public void ApplyDamage(int damage, ulong attackerClientId)
    {
        if (damage <= 0)
            return;

        if (IsServer)
        {
            ApplyDamageInternal(damage, attackerClientId);
            return;
        }

        ApplyDamageServerRpc(damage, attackerClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ApplyDamageServerRpc(int damage, ulong attackerClientId)
    {
        ApplyDamageInternal(damage, attackerClientId);
    }

    private void ApplyDamageInternal(int damage, ulong attackerClientId)
    {
        if (!IsServer)
            return;

        if (m_CurrentHealth.Value <= 0)
            return;

        if (attackerClientId != ulong.MaxValue && attackerClientId != OwnerClientId)
        {
            m_LastDamageDealerClientId = attackerClientId;
        }

        m_CurrentHealth.Value = Mathf.Max(0, m_CurrentHealth.Value - damage);

        if (m_CurrentHealth.Value == 0)
        {
            Die(m_LastDamageDealerClientId);
        }
    }

    private void OnCurrentHealthChanged(int previousValue, int newValue)
    {
        currentHealth = newValue;
    }

    private void Die(ulong? killerClientId)
    {
        if (!IsServer)
            return;

        if (m_IsDying)
            return;

        m_IsDying = true;

        if (killerClientId.HasValue)
            AwardKillPointClientRpc(killerClientId.Value);

        // Hide and disable the player immediately on all clients.
        PlayerDiedClientRpc();

        StartCoroutine(RespawnCoroutine());
    }

    private IEnumerator RespawnCoroutine()
    {
        // Camera lingers at death position for 1 second.
        yield return new WaitForSeconds(1f);

        var spawnManager = UnityEngine.Object.FindFirstObjectByType<GameSpawnManager>();
        var nextSpawnPosition = spawnManager != null
            ? spawnManager.GetRandomSpawnPosition()
            : transform.position;

        SetPosition(nextSpawnPosition);

        m_CurrentHealth.Value = maxHealth;
        m_LastDamageDealerClientId = null;
        m_IsDying = false;

        PlayerRespawnedClientRpc(nextSpawnPosition);
    }

    [ClientRpc]
    private void PlayerDiedClientRpc()
    {
        // Hide all renderers so the player body disappears instantly.
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;

        // Disable colliders so dead players can't be shot.
        // Exclude CharacterController — it is used only for SetPosition teleporting
        // and re-enabling it via GetComponentsInChildren causes physics conflicts.
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col is CharacterController)
                continue;
            col.enabled = false;
        }

        // On the owning client: freeze input and detach the camera so it
        // lingers at the death position while the 1-second delay runs.
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.SetDeadStateOnOwner(isDead: true);

        // Cancel any in-flight bullet tracer so it doesn't linger while dead.
        var actions = GetComponent<PlayerActions>();
        if (actions != null)
            actions.CancelTracer();
    }

    [ClientRpc]
    private void PlayerRespawnedClientRpc(Vector3 respawnPosition)
    {
        // Teleport BEFORE re-enabling renderers so NGO's interpolation buffer
        // is snapped to the new position before the model becomes visible.

        // On the owning client: re-attach the camera and restore control.
        // SetDeadStateOnOwner moves the Rigidbody/transform to respawnPosition.
        var movement = GetComponent<PlayerMovement>();
        if (movement != null)
            movement.SetDeadStateOnOwner(isDead: false, respawnPosition: respawnPosition);

        // The NetworkTransform is owner-authoritative, so only the owning client
        // may call Teleport() to flush the interpolation buffer. Calling it here
        // (inside a ClientRpc, on IsOwner) is the correct place.
        if (IsOwner)
        {
            var networkTransform = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (networkTransform != null)
                networkTransform.Teleport(respawnPosition, transform.rotation, transform.localScale);
        }

        // Non-owner, non-server clients: snap position so the visual is correct
        // when renderers are re-enabled below.
        if (!IsServer && !IsOwner)
            SetPosition(respawnPosition);

        // Now that the transform is at the correct position, show the model.
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = true;

        // Re-enable colliders — exclude CharacterController (see PlayerDiedClientRpc).
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col is CharacterController)
                continue;
            col.enabled = true;
        }

        // The mass renderer re-enable above also turns the LineRenderer back on
        // with stale shot positions. Explicitly hide the tracer so it only
        // appears when the next shot fires.
        var actions = GetComponent<PlayerActions>();
        if (actions != null)
            actions.CancelTracer();
    }

    [ClientRpc]
    private void AwardKillPointClientRpc(ulong killerClientId)
    {
        var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (gameManager != null)
            gameManager.OnPlayerKillByClientId(killerClientId);
    }

    private void SetPosition(Vector3 newPosition)
    {
        if (m_CharacterController == null)
        {
            transform.position = newPosition;
            return;
        }

        var wasEnabled = m_CharacterController.enabled;
        m_CharacterController.enabled = false;
        transform.position = newPosition;
        m_CharacterController.enabled = wasEnabled;
    }
}
