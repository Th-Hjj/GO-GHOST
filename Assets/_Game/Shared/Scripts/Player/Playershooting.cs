using UnityEngine;

/// <summary>
/// Fires a Projectile along the mouse aim direction (PlayerInputReader.AimDirection).
/// The muzzle is the player's position offset along the aim by the FirePoint
/// child's original distance, so aiming is independent of the movement direction.
/// </summary>
// FUNCTION SUMMARY
// Update()            — keeps the FirePoint child on the muzzle; if the Attack button is held and the fire rate allows, fires.
// GetMuzzlePosition() — world-space spawn point for a given aim direction.
// Fire()              — spawns a bullet at the muzzle, rotated to the aim, and launches it along the aim direction.
public class PlayerShooting : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO stats;
    [Tooltip("Child transform marking the muzzle. Its distance from the player is used as the spawn radius; it is moved to follow the aim.")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Projectile bulletPrefab;

    private PlayerInputReader input;
    private float nextFireTime;
    private float firePointRadius = 0.5f;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        if (input == null)
        {
            Debug.LogError("PlayerShooting requires a PlayerInputReader on the same GameObject. Add it to the Player prefab.", this);
            enabled = false;
        }

        if (firePoint != null && firePoint.localPosition.magnitude > 0.01f)
            firePointRadius = firePoint.localPosition.magnitude;
    }

    private void Update()
    {
        Vector2 aim = input.AimDirection;

        if (firePoint != null)
            firePoint.position = GetMuzzlePosition(aim);

        if (input.AttackHeld && Time.time >= nextFireTime)
        {
            Fire(aim);
            nextFireTime = Time.time + stats.fireRate;
        }
    }

    /// <summary>World-space muzzle position for the given (normalized) aim direction.</summary>
    public Vector2 GetMuzzlePosition(Vector2 aimDirection)
    {
        return (Vector2)transform.position + aimDirection * firePointRadius;
    }

    private void Fire(Vector2 direction)
    {
        if (bulletPrefab == null) return;

        Vector2 spawn = GetMuzzlePosition(direction);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Projectile bullet = Instantiate(bulletPrefab, spawn, Quaternion.Euler(0f, 0f, angle));
        bullet.Launch(direction, stats.bulletSpeed, stats.gunDamage, gameObject);
    }
}