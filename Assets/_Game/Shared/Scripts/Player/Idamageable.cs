using UnityEngine;

/// <summary>
/// Implement this on anything that can take damage (your partner's enemy/AI
/// scripts, breakable objects, etc). The player's gun and dodge-slash call
/// TakeDamage() through this interface, so the player scripts never need a
/// direct reference to the enemy class.
/// </summary>
// FUNCTION SUMMARY
// TakeDamage() — applies damage to whatever implements this interface (enemy, breakable, etc).
public interface IDamageable
{
    /// <param name="amount">How much damage to apply.</param>
    /// <param name="source">Who dealt the damage (useful for knockback, kill credit, etc).</param>
    void TakeDamage(int amount, GameObject source);
}