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

        if (killerClientId.HasValue)
        {
            AwardKillPointClientRpc(killerClientId.Value);
        }

        var spawnManager = UnityEngine.Object.FindFirstObjectByType<GameSpawnManager>();
        var nextSpawnPosition = spawnManager != null ? spawnManager.GetRandomSpawnPosition() : transform.position;

        SetPosition(nextSpawnPosition);
        RespawnClientRpc(nextSpawnPosition);

        m_CurrentHealth.Value = maxHealth;
        m_LastDamageDealerClientId = null;
    }

    [ClientRpc]
    private void AwardKillPointClientRpc(ulong killerClientId)
    {
        var gameManager = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnPlayerKillByClientId(killerClientId);
        }
    }

    [ClientRpc]
    private void RespawnClientRpc(Vector3 nextSpawnPosition)
    {
        if (IsServer)
            return;

        SetPosition(nextSpawnPosition);
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
