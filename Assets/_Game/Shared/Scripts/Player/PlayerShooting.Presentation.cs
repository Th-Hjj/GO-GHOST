using UnityEngine;

public partial class PlayerShooting
{
    public Vector2 CurrentMuzzlePosition => firePoint != null ? (Vector2)firePoint.position : GetMuzzlePosition(CurrentBarrelDirection);
    public Vector2 CurrentBarrelDirection { get; private set; } = Vector2.right;
    private float reloadStartedAt, reloadDuration, reloadSide;
    private Vector2 reloadFromDirection;

    private void ApplyMuzzlePose()
    {
        Vector2 aim = input.AimDirection.sqrMagnitude > .0001f ? input.AimDirection.normalized : CurrentBarrelDirection;
        Vector2 barrel = aim;
        Vector2 reloadOffset = Vector2.zero;
        if (legacyCombat && IsReloading)
        {
            float t = Mathf.Clamp01((Time.time - reloadStartedAt) / reloadDuration);
            if (t < .22f) barrel = RotatePose(reloadFromDirection, Vector2.up, Mathf.SmoothStep(0, 1, t / .22f));
            else if (t < .74f) barrel = Vector2.up;
            else barrel = RotatePose(Vector2.up, aim, Mathf.SmoothStep(0, 1, (t - .74f) / .26f));
            float lift = t < .22f ? Mathf.SmoothStep(0, 1, t / .22f)
                : t < .74f ? 1 : 1 - Mathf.SmoothStep(0, 1, (t - .74f) / .26f);
            // Hold the raised gun beside the head, with hands still attached to the body.
            reloadOffset = Vector2.right * (.38f * reloadSide * lift);
        }
        CurrentBarrelDirection = barrel;
        if (firePoint == null) return;
        firePoint.SetPositionAndRotation(GetMuzzlePosition(barrel) + reloadOffset, Quaternion.Euler(0, 0, Mathf.Atan2(barrel.y, barrel.x) * Mathf.Rad2Deg));
    }

    private static Vector2 RotatePose(Vector2 from, Vector2 to, float t)
    {
        float angle = Mathf.LerpAngle(Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg,
            Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, t) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
