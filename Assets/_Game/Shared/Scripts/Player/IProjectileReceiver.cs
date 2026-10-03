using UnityEngine;

public enum ProjectileActor { Player, InnerAI, Environment }

/// <summary>A narrow opt-in for real projectile contacts. Shared bullets know no level-specific objects.</summary>
public interface IProjectileReceiver
{
    /// <returns>True if this contact consumes the projectile; false leaves normal damage/pass-through handling intact.</returns>
    bool ReceiveProjectile(Projectile projectile, Collider2D contact);
}

/// <summary>Opt-in solid combat cover, shared by projectile receivers and ray attacks.</summary>
public interface ICombatCover
{
    bool BlocksCombatRay(Collider2D contact);
}
