using UnityEngine;

/// <summary>
/// Freezes the nearest enemy within range for a set duration. Doesn't
/// require aiming or landing a hit - it just finds and freezes a target.
/// Set maxUses = 1 on the asset (inherited from PlayerAbilitySO) so this
/// only fires once per battle; call PlayerAbilitySystem.ResetAbilityUses()
/// at the start of each new battle to recharge it, the same way
/// PlayerHealth.ResetLastStand() re-arms Last Stand.
/// </summary>
[CreateAssetMenu(fileName = "NewFreezeAbility", menuName = "Player/Abilities/Freeze Nearest Enemy")]
// FUNCTION SUMMARY
// Activate() — finds the nearest IFreezable enemy within range and freezes it for freezeDuration seconds.
public class FreezeNearestEnemyAbilitySO : PlayerAbilitySO
{
    [SerializeField] private float range = 15f;
    [SerializeField] private float freezeDuration = 2f;
    [SerializeField] private LayerMask enemyLayer;

    public override bool Activate(GameObject player)
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(player.transform.position, range, enemyLayer);
        if (candidates.Length == 0) return false; // nothing in range: don't spend the charge

        Collider2D nearest = null;
        float bestSqrDistance = float.MaxValue;
        foreach (var candidate in candidates)
        {
            float sqrDistance = ((Vector2)candidate.transform.position - (Vector2)player.transform.position).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                nearest = candidate;
            }
        }

        IFreezable freezable = nearest != null ? nearest.GetComponent<IFreezable>() : null;
        if (freezable == null) return false; // nearest thing in range isn't freezable: don't spend the charge

        freezable.Freeze(freezeDuration);
        return true;
    }
}