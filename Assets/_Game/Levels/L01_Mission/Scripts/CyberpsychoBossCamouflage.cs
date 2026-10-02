using UnityEngine;

public partial class CyberpsychoBoss
{
    private float camoTargetX;
    private bool transitionDrawCuePlayed;

    private void BeginCamoReposition()
    {
        camoRushRevealed = false;
        float distance = Mathf.Abs(player.transform.position.x - rb.position.x);
        float toward = player.transform.position.x < rb.position.x ? -1f : 1f;
        float direction = distance > 5f ? toward : -toward;
        float amount = distance > 5f ? Mathf.Min(4.5f, distance - 3f) : 4.5f;
        Bounds bodyBounds = bossCollider != null ? bossCollider.bounds : new Bounds(rb.position, new Vector3(1, 1.5f, 1));
        float left = arenaBound ? blackoutArena.min.x : ArenaLeft;
        float right = arenaBound ? blackoutArena.max.x : ArenaRight;
        float target = Mathf.Clamp(rb.position.x + direction * amount,
            left + bodyBounds.extents.x + .1f, right - bodyBounds.extents.x - .1f);
        int groundMask = LayerMask.GetMask("Ground");
        var wall = Physics2D.BoxCast(bodyBounds.center, (Vector2)bodyBounds.size * .9f, 0,
            Vector2.right * direction, Mathf.Abs(target - rb.position.x), groundMask);
        if (wall.collider != null) target = rb.position.x + direction * Mathf.Max(0, wall.distance - .1f);
        // A short grounded route only. Do not teleport, climb, fall off an edge or pass through a player.
        float valid = rb.position.x;
        for (int i = 1; i <= 16; i++)
        {
            float x = Mathf.Lerp(rb.position.x, target, i / 16f);
            var support = Physics2D.Raycast(new Vector2(x, bodyBounds.min.y + .12f), Vector2.down, .32f, groundMask);
            if (support.collider == null || support.normal.y < .5f) break;
            if (playerCollider != null && Mathf.Abs(x - playerCollider.bounds.center.x) < bodyBounds.extents.x + playerCollider.bounds.extents.x + .3f) break;
            valid = x;
        }
        camoTargetX = valid;
        if (Mathf.Abs(camoTargetX - rb.position.x) < .1f) { LockSlashWarning(); return; }
        chargeDirection = direction;
        Enter(AttackState.CamoReposition, Mathf.Min(.25f, Mathf.Abs(camoTargetX - rb.position.x) / 18f));
    }

    private void TickCamoReposition(float dt)
    {
        float delta = camoTargetX - rb.position.x;
        if (timer <= 0 || Mathf.Abs(delta) <= .02f)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            LockSlashWarning(); // Start the entire .55 s reaction window now, not while hidden.
            return;
        }
        float travel = Mathf.Min(Mathf.Abs(delta), 18f * Mathf.Min(dt, timer));
        rb.linearVelocity = new Vector2(Mathf.Sign(delta) * travel / Time.fixedDeltaTime, rb.linearVelocity.y);
        timer -= dt;
        ShowWarnings();
    }

    private float NormalCamoAlpha(bool blade)
    {
        if (CamoRushActive) return blade ? .65f : .08f;
        if (camoRushRevealed && State == AttackState.SlashWarning) return 1f;
        if (State == AttackState.CamoReposition) return blade ? .65f : .35f;
        if (State == AttackState.SlashWarning)
        {
            // .15 s reveal, then .40 s stationary and fully visible before damage can start.
            float progress = Mathf.Clamp01((.55f - timer) / .15f);
            return Mathf.Lerp(blade ? .65f : .35f, 1f, progress);
        }
        return 1f; // Follow-up tells, execution and recovery stay legible.
    }

    // A owns the 3 s lifecycle, HP reset and immunity. This method changes only visual/audio presentation.
    public void PresentPhaseTransition(float progress01)
    {
        if (!combatEnabled || Phase != BossPhase.PhaseTransition || health == null || health.IsDead) return;
        float p = Mathf.Clamp01(progress01);
        if (p <= 0) transitionDrawCuePlayed = false;
        if (vfx != null) vfx.ClearBlackoutLines();
        SetWeapons(p < .3f, p >= .65f);
        if (visualParts != null)
            for (int i = 0; i < visualParts.Length; i++)
            {
                var part = visualParts[i];
                if (part == null || part == shotWarning || part == chargeWarning
                    || (spreadWarnings != null && System.Array.IndexOf(spreadWarnings, part) >= 0)) continue;
                part.color = visualColors[i];
            }
        if (bladeVisual != null)
            bladeVisual.transform.localRotation = bladeRestRotation * Quaternion.Euler(0, 0,
                Mathf.Lerp(-70f, 0f, Mathf.InverseLerp(.65f, .9f, p)));
        if (p >= .65f && !transitionDrawCuePlayed)
        {
            transitionDrawCuePlayed = true;
            if (sfx != null) sfx.Play(Sfx.BladeSwing);
        }
    }

    private void ClearCamoPresentation()
    {
        camoTargetX = 0;
        transitionDrawCuePlayed = false;
        if (bladeVisual != null) bladeVisual.transform.localRotation = bladeRestRotation;
    }
}
