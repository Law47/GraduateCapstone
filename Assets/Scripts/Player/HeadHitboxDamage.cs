using UnityEngine;

public class HeadHitboxDamage : MonoBehaviour
{
    [SerializeField] private PlayerManager targetPlayerManager;
    [SerializeField] private float damageMultiplier = 2.5f;

    public PlayerManager TargetPlayerManager => targetPlayerManager;

    public void ApplyHitDamage(int baseDamage)
    {
        if (targetPlayerManager == null)
            return;

        var finalDamage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        targetPlayerManager.ApplyDamage(finalDamage);
    }
}
