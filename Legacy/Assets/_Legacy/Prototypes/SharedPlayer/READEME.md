# Player Scripts — Setup Guide

## GameObject setup
Put all of these on your Player GameObject:
- `Rigidbody2D` (Body Type: Dynamic, Freeze Rotation Z: on)
- `Collider2D` (e.g. CapsuleCollider2D)
- `PlayerStatsSO` asset (Assets > Create > Player > Player Stats), dragged into
  the `stats` field of `PlayerHealth`, `PlayerMovement`, `PlayerDodge`, `PlayerShooting`
- `PlayerHealth`
- `PlayerMovement` (needs an empty child Transform as `groundCheck`, positioned at the character's feet)
- `PlayerDodge`
- `PlayerShooting` (needs an empty child Transform as `firePoint`, and a `Projectile` prefab)
- `PlayerAbilitySystem`
- `PlayerUpgradeManager`

## Input Manager axes (Edit > Project Settings > Input Manager)
These scripts use the legacy Input Manager for simplicity:
- `Horizontal` — already exists by default (A/D, arrows)
- `Jump` — already exists by default (Space)
- `Fire1` — already exists by default (Ctrl / mouse) → **the gun**
- `Fire2` — already exists by default (Alt / mouse) → **special ability**
- `Fire3` — you'll likely need to add this one (e.g. bind to Left Shift) → **dodge**

If your project uses the new Input System package instead, swap the
`Input.GetButtonDown(...)` calls in `PlayerMovement`, `PlayerDodge`,
`PlayerShooting`, and `PlayerAbilitySystem` for your Input Actions — the
rest of the logic doesn't change.

## Layers
- Put enemies on an `Enemy` layer, and assign that layer to the
  `enemyLayer` field on `PlayerDodge`, `Projectile`'s trigger (if you filter
  by layer), and any ability that uses `OverlapCircle`.
- Put ground/platforms on a `Ground` layer and assign it to `groundLayer`
  on `PlayerMovement`.

## Partner's enemy scripts
Have their enemy scripts implement `IDamageable`:

```csharp
public class Enemy : MonoBehaviour, IDamageable
{
    public int health = 50;

    public void TakeDamage(int amount, GameObject source)
    {
        health -= amount;
        if (health <= 0) Destroy(gameObject); // or whatever your death flow is
    }
}
```

That's the *only* contract the player scripts need — nothing else about
the enemy code has to match anything here.

## Unity version note
Newer Unity (6+) renamed `Rigidbody2D.velocity` to `Rigidbody2D.linearVelocity`.
These scripts use `linearVelocity`. If you're on an older LTS version and it
doesn't compile, do a find-and-replace of `linearVelocity` → `velocity` in
`PlayerMovement.cs` and `Projectile.cs`.

## Progression / upgrades
Wire your level-up UI buttons to `PlayerUpgradeManager`:
- `UnlockDoubleJump()`
- `UnlockSlashDodge()` — dodge becomes longer and damages enemies it passes through
- `UnlockLastStand()` — arms the one-extra-life ability
- `UnlockAbility(PlayerAbilitySO ability)` — adds an ability to the special-button list

`ExampleBlastAbilitySO.cs` shows the pattern for writing new abilities —
duplicate it, rename the class, write your own `Activate()`, then make an
asset from it and pass that asset to `UnlockAbility()`.

## Last Stand ("second life")
- Starts unlocked = false. Call `PlayerUpgradeManager.UnlockLastStand()`
  when the player earns it.
- Once armed, it's usable **once per battle**: after it triggers,
  `PlayerHealth.LastStandAvailable` becomes false until you call
  `playerHealth.ResetLastStand()`. Call that from whatever starts a new
  encounter/battle in your game (a trigger zone, a "battle start" event
  from your partner's AI/arena system, a scene load, etc).
- On trigger: player becomes briefly invulnerable, force-dashes through the
  nearest enemy in range (or straight ahead if none is found) dealing
  slash damage, then settles at 1 HP instead of dying.
- Subscribe to `PlayerHealth.OnLastStandTriggered` to play a VFX/animation/
  camera shake for the moment.

## Events for UI
`PlayerHealth` exposes:
- `OnHealthChanged(int current, int max)`
- `OnDeath()`
- `OnLastStandTriggered()`

Subscribe to these from your HUD/UI scripts rather than polling `CurrentHealth`
every frame.