using TMPro;
using Unity.Netcode;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private string healthFormat = "Health: {0}/{1}";

    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private string ammoFormat = "{0}/{1}";

    private PlayerManager m_LocalPlayerManager;
    private PlayerActions m_LocalPlayerActions;

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
            if (ammoText != null)
                ammoText.text = "--/--";
            return;
        }

        healthText.text = string.Format(healthFormat, m_LocalPlayerManager.CurrentHealth, m_LocalPlayerManager.MaxHealth);

        if (ammoText != null && m_LocalPlayerActions != null)
            ammoText.text = string.Format(ammoFormat, m_LocalPlayerActions.CurrentAmmo, m_LocalPlayerActions.MagazineSize);
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
                m_LocalPlayerActions = playerManager.GetComponent<PlayerActions>();
                return;
            }
        }
    }
}
