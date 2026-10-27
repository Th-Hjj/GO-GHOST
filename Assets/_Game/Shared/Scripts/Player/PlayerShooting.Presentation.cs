using UnityEngine;

public partial class PlayerShooting
{
    public Vector2 CurrentMuzzlePosition => firePoint != null ? (Vector2)firePoint.position : GetMuzzlePosition(CurrentBarrelDirection);
    public Vector2 CurrentBarrelDirection { get; private set; } = Vector2.right;
    public int CurrentWeaponFacing { get; private set; } = 1;
    // Accepted presentation follow-up: give the connected revolver clearance from the torso.
    private const float RevolverForwardReach = .16f;
    private float reloadStartedAt, reloadDuration;

    private void ApplyMuzzlePose()
    {
        Vector2 aim = input.AimDirection.sqrMagnitude > .0001f ? input.AimDirection.normalized : CurrentBarrelDirection;
        Vector2 barrel = aim;
        Vector2 reloadOffset = Vector2.zero;
        // Use the live resolved aim for the entire pose, including reload. A small deadband
        // keeps world-up/down stable; never infer handedness from the animated barrel.
        if (Mathf.Abs(aim.x) > .025f) CurrentWeaponFacing = aim.x < 0 ? -1 : 1;
        if (legacyCombat && IsReloading)
        {
            float t = Mathf.Clamp01((Time.time - reloadStartedAt) / reloadDuration);
            float lift = t < .22f ? Mathf.SmoothStep(0, 1, t / .22f)
                : t < .74f ? 1 : 1 - Mathf.SmoothStep(0, 1, (t - .74f) / .26f);
            barrel = ReloadDirection(aim, lift);
            // Hold the raised gun beside the head, with hands still attached to the body.
            reloadOffset = Vector2.right * (.22f * CurrentWeaponFacing * lift);
        }
        CurrentBarrelDirection = barrel;
        if (firePoint == null) return;
        firePoint.SetPositionAndRotation(GetMuzzlePosition(barrel) + reloadOffset, Quaternion.Euler(0, 0, Mathf.Atan2(barrel.y, barrel.x) * Mathf.Rad2Deg));
    }

    private Vector2 ReloadDirection(Vector2 aim, float lift)
    {
        // Choose the arc on the same side as the grip. At downward aim, shortest-angle
        // interpolation alone can choose the opposite semicircle and invert the silhouette.
        float x = Mathf.Abs(aim.x) * CurrentWeaponFacing;
        float angle = Mathf.Atan2(aim.y, x) * Mathf.Rad2Deg;
        if (CurrentWeaponFacing < 0 && angle < 0) angle += 360;
        angle = Mathf.Lerp(angle, 90, lift) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
