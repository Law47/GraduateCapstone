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
        if (damage <= 0)
            return;

        if (IsServer)
        {
            ApplyDamageInternal(damage);
            return;
        }

        ApplyDamageServerRpc(damage);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ApplyDamageServerRpc(int damage)
    {
        ApplyDamageInternal(damage);
    }

    private void ApplyDamageInternal(int damage)
    {
        if (!IsServer)
            return;

        if (m_CurrentHealth.Value <= 0)
            return;

        m_CurrentHealth.Value = Mathf.Max(0, m_CurrentHealth.Value - damage);

        if (m_CurrentHealth.Value == 0)
        {
            Die();
        }
    }

    private void OnCurrentHealthChanged(int previousValue, int newValue)
    {
        currentHealth = newValue;
    }

    private void Die()
    {
        if (!IsServer)
            return;

        var spawnManager = UnityEngine.Object.FindFirstObjectByType<GameSpawnManager>();
        var nextSpawnPosition = spawnManager != null ? spawnManager.GetRandomSpawnPosition() : transform.position;

        SetPosition(nextSpawnPosition);
        RespawnClientRpc(nextSpawnPosition);

        m_CurrentHealth.Value = maxHealth;
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
