using UnityEngine;

public class HeadHitboxDamage : MonoBehaviour
{
    [SerializeField] private PlayerManager targetPlayerManager;

    public PlayerManager TargetPlayerManager => targetPlayerManager;

    public void ApplyHitDamage(int baseDamage, int multiplier = 2)
    {
        if (targetPlayerManager == null)
            return;

        var finalDamage = Mathf.Max(1, baseDamage * multiplier);
        targetPlayerManager.ApplyDamage(finalDamage);
    }
}
