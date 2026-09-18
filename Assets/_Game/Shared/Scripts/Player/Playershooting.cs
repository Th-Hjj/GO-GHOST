using UnityEngine;

/// <summary>
/// Fires a Projectile from firePoint in the direction the player is facing.
/// </summary>
// FUNCTION SUMMARY
// Update() — checks if the fire button is held and enough time has passed since the last shot, then fires.
// Fire()   — spawns a bullet at the fire point and launches it in the direction the player is facing.
public class PlayerShooting : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO stats;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Projectile bulletPrefab;

    private PlayerMovement movement;
    private float nextFireTime;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + stats.fireRate;
        }
    }

    private void Fire()
    {
        if (bulletPrefab == null || firePoint == null) return;

        Projectile bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.identity);
        Vector2 direction = new Vector2(movement.FacingDirection, 0f);
        bullet.Launch(direction, stats.bulletSpeed, stats.gunDamage, gameObject);
    }
}