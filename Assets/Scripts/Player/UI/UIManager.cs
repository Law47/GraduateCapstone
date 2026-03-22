using TMPro;
using Unity.Netcode;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private string healthFormat = "Health: {0}/{1}";

    private PlayerManager m_LocalPlayerManager;

    private void Update()
    {
        if (healthText == null)
            return;

        if (m_LocalPlayerManager == null || !m_LocalPlayerManager.IsSpawned)
        {
            TryFindLocalPlayerManager();
        }

        if (m_LocalPlayerManager == null)
        {
            healthText.text = "Health: --";
            return;
        }

        healthText.text = string.Format(healthFormat, m_LocalPlayerManager.CurrentHealth, m_LocalPlayerManager.MaxHealth);
    }

    private void TryFindLocalPlayerManager()
    {
        var playerManagers = FindObjectsByType<PlayerManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var index = 0; index < playerManagers.Length; index++)
        {
            var playerManager = playerManagers[index];
            if (playerManager != null && playerManager.IsOwner)
            {
                m_LocalPlayerManager = playerManager;
                return;
            }
        }
    }
}
