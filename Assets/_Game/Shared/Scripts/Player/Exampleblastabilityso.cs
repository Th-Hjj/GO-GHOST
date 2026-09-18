using UnityEngine;

/// <summary>
/// Example ability so you can see the pattern for extending PlayerAbilitySO.
/// Damages every enemy in a radius around the player. Delete or replace this
/// once you're designing your real abilities.
/// </summary>
[CreateAssetMenu(fileName = "NewBlastAbility", menuName = "Player/Abilities/Example Blast")]
// FUNCTION SUMMARY
// Activate() — damages every enemy within a radius of the player when this ability is used.
public class ExampleBlastAbilitySO : PlayerAbilitySO
{
    [SerializeField] private float radius = 3f;
    [SerializeField] private int damage = 15;
    [SerializeField] private LayerMask enemyLayer;

    public override bool Activate(GameObject player)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius, enemyLayer);
        foreach (var hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage, player);
        }
        return true;
    }
}