using UnityEngine;

public partial class CyberpsychoBoss
{
    public const float BlackoutSequenceSeconds = 8.28f;
    private const float RedWidth = .6f;
    private sealed class RedCut
    {
        public Vector2 from, to;
        public float warn, fire, end;
        public int damage, slot;
        public bool locked, warned, started, hit;
    }
    private readonly RedCut[] redCuts = new RedCut[7];
    private readonly Collider2D[] redContacts = new Collider2D[8];
    private readonly Bounds[] breakBounds = new Bounds[3];
    private readonly Bounds[] routeSupports = new Bounds[6];
    private readonly bool[] routeSupportActive = new bool[6];
    private Collider2D[] breakableHigh = new Collider2D[0], permanentGround = new Collider2D[0];
    private Bounds blackoutArena;
    private bool arenaBound, platformsBroken;
    public bool BlackoutPlacementFailed { get; private set; }
    private float blackoutClock, finalEnd;
    public float BlackoutElapsed => blackoutClock;
    public int BlackoutCutsExecuted { get; private set; }

    // A supplies world-space interior bounds and actual stationary scene colliders before combat.
    public bool BindBlackoutArena(Bounds playableBounds, Collider2D[] breakableHighPlatforms, Collider2D[] permanentSupports)
    {
        arenaBound = false;
        if (combatEnabled || playableBounds.size.x <= 1f || playableBounds.size.y <= 1f
            || breakableHighPlatforms == null || breakableHighPlatforms.Length != 3
            || permanentSupports == null || permanentSupports.Length != 3)
            return InvalidArena();
        for (int i = 0; i < 3; i++)
        {
            var high = breakableHighPlatforms[i];
            var low = permanentSupports[i];
            if (high == null || low == null || !high.isActiveAndEnabled || !low.isActiveAndEnabled
                || high.isTrigger || low.isTrigger || high.bounds.size.x <= 0f
                || Mathf.Abs(high.bounds.max.y - breakableHighPlatforms[0].bounds.max.y) > .05f)
                return InvalidArena();
            for (int j = 0; j < 3; j++)
                if (high == permanentSupports[j] || (j < i && high == breakableHighPlatforms[j])) return InvalidArena();
            if (high.bounds.min.x < playableBounds.min.x || high.bounds.max.x > playableBounds.max.x
                || high.bounds.max.y >= playableBounds.max.y) return InvalidArena();
        }
        blackoutArena = playableBounds;
        breakableHigh = (Collider2D[])breakableHighPlatforms.Clone();
        permanentGround = (Collider2D[])permanentSupports.Clone();
        highCPlatform = breakableHigh[0].gameObject;
        arenaBound = true;
        return true;
    }

    private bool InvalidArena()
    {
        Debug.LogError("BLACKOUT requires actual arena interior bounds, three aligned active high platforms and ground/two permanent low supports. Bind before combat.", this);
        return false;
    }

    private void BeginBlackout()
    {
        RestoreDropBody();
        RestorePlayerCollision();
        IgnorePlayerCollision(); // Body pair only; damage queries, AI/Drone targets and projectile layers stay unchanged.
        BlackoutUsed = true;
        BlackoutsStarted++;
        DistortionRunsStarted = BlackoutCutsExecuted = 0;
        blackoutClock = 0f;
        finalEnd = 6.48f;
        platformsBroken = false;
        BlackoutPlacementFailed = false;
        for (int i = 0; i < 3; i++) breakBounds[i] = breakableHigh[i].bounds;
        highCBounds = breakBounds[0];
        redCuts[0] = MakeCut(.7f, 1.6f, 1.72f, 32, 0);
        redCuts[1] = MakeCut(2.02f, 2.92f, 3.04f, 32, 0);
        for (int i = 0; i < 4; i++) redCuts[i + 2] = MakeCut(3.34f + i * .18f, 4.34f + i * .22f, 4.46f + i * .22f, 18, i);
        redCuts[6] = MakeCut(5.42f, 6.32f, 6.48f, 32, 0);
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (vfx != null) vfx.ClearBossEffects();
        Enter(AttackState.BlackoutSignal, .7f);
    }

    private static RedCut MakeCut(float warn, float fire, float end, int damage, int slot)
        => new RedCut { warn = warn, fire = fire, end = end, damage = damage, slot = slot };

    private void TickBlackout(float dt)
    {
        blackoutClock += dt;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        if (BlackoutPlacementFailed)
        {
            timer -= dt;
            if (timer <= 0) BeginSlash();
            else ShowWarnings();
            return;
        }
        // Restore as the .1 s reveal finishes, before the visible recovery window.
        if (blackoutClock >= finalEnd + .6f) RestorePlayerCollision();
        for (int i = 0; i < redCuts.Length; i++)
        {
            var cut = redCuts[i];
            if (cut == null || blackoutClock < cut.warn) continue;
            if (!cut.locked)
            {
                if (i < 2) LockDiagonal(cut, i == 0 ? 1 : -1);
                else if (i == 2) LockCurtain();
                else if (i == 6)
                {
                    // The real cut crosses the actual platform tops, not a disconnected line above them.
                    SetCut(cut, new Vector2(blackoutArena.center.x, highCBounds.max.y), Vector2.right);
                    cut.locked = true;
                }
                if (BlackoutPlacementFailed) return;
                // Curtain geometry was locked as a group; later tells only reveal their saved lines.
                if (!cut.locked) continue;
            }
            // Never shorten a visible warning if a fixed step straddled its scheduled start.
            if (!cut.warned)
            {
                cut.warned = true;
                float shift = Mathf.Max(0f, blackoutClock - cut.warn);
                cut.fire += shift;
                cut.end += shift;
                if (i == 6) finalEnd = cut.end;
            }
            if (blackoutClock >= cut.fire && !cut.started)
            {
                cut.started = true;
                BlackoutCutsExecuted++;
                if (i < 2) DistortionRunsStarted++;
                if (sfx != null) sfx.Play(Sfx.BladeSwing);
            }
            if (cut.started && blackoutClock < cut.end && !cut.hit)
            {
                var filter = new ContactFilter2D();
                filter.SetLayerMask(1 << playerCollider.gameObject.layer);
                filter.useTriggers = true;
                Vector2 delta = cut.to - cut.from;
                int count = Physics2D.OverlapBox((cut.from + cut.to) * .5f,
                    new Vector2(delta.magnitude, RedWidth), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg,
                    filter, redContacts);
                for (int j = 0; j < count; j++)
                    if (redContacts[j] == playerCollider || redContacts[j].GetComponentInParent<PlayerHealth>() == player)
                    {
                        cut.hit = true; // One attempt even if existing Dash invulnerability rejects it.
                        player.TakeDamage(cut.damage, gameObject);
                        break;
                    }
                if (!combatEnabled || health.IsDead || player.IsDead) { StopCombat(); return; }
            }
        }
        if (!platformsBroken && blackoutClock >= finalEnd + .5f)
        {
            platformsBroken = true;
            for (int i = 0; i < 3; i++)
            {
                var platform = breakableHigh[i];
                if (platform == null || !platform.isActiveAndEnabled) continue;
                // Only the explicitly bound platforms intersected by the locked final band can break.
                if (!IntersectsCut(redCuts[6], platform.bounds)) continue;
                if (vfx != null) vfx.HighCBroken(platform.bounds);
                platform.gameObject.SetActive(false);
            }
        }
        if (blackoutClock >= finalEnd + 1.8f)
        {
            RestorePlayerCollision();
            if (vfx != null) vfx.ClearBlackoutLines();
            BeginSlash();
            return;
        }
        AttackState next = blackoutClock < .7f ? AttackState.BlackoutSignal
            : blackoutClock < 3.34f ? AttackState.BlackoutRun
            : blackoutClock < 5.42f ? AttackState.BlackoutMark
            : blackoutClock < redCuts[6].fire ? AttackState.BlackoutFinalWarning
            : blackoutClock < finalEnd ? AttackState.BlackoutFinalSlash
            : blackoutClock < finalEnd + .5f ? AttackState.BlackoutBreakDelay : AttackState.BlackoutRecovery;
        if (State != next) Enter(next, Mathf.Max(0, finalEnd + 1.8f - blackoutClock));
        else timer = Mathf.Max(0, finalEnd + 1.8f - blackoutClock);
        RenderBlackout();
        ShowWarnings();
    }

    private void RenderBlackout()
    {
        if (vfx == null) return;
        vfx.ClearBlackoutLines(); // Hide/reuse existing renderers; never destroy or allocate here.
        foreach (var cut in redCuts)
        {
            if (cut == null || !cut.locked || blackoutClock < cut.warn || blackoutClock >= cut.end + .18f) continue;
            bool active = blackoutClock >= cut.fire && blackoutClock < cut.end;
            float alpha = blackoutClock >= cut.end ? (1f - (blackoutClock - cut.end) / .18f) * .4f : active ? 1f : .55f;
            vfx.SetBlackoutLine(cut.slot, cut.from, cut.to, RedWidth, alpha, active);
        }
        if (!platformsBroken && blackoutClock >= .4f)
            for (int i = 0; i < 3; i++) vfx.SetBlackoutPlatformMark(i, breakBounds[i]);
    }

    private void LockDiagonal(RedCut cut, int sign)
    {
        Vector2 aim = playerCollider.bounds.center;
        // Try the original angle then its mirror. Offsets are chosen BEFORE the first visible tell.
        for (int offset = 0; offset <= 4; offset++)
            for (int mirror = 0; mirror < 2; mirror++)
            {
                float radians = (mirror == 0 ? sign : -sign) * 25f * Mathf.Deg2Rad;
                SetCut(cut, aim + Vector2.up * offset, new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)));
                if (HasHumanEscape(cut, false, .9f)) { cut.locked = true; return; }
            }
        AbortUnsafePlacement();
    }

    private void LockCurtain()
    {
        var direction = new Vector2(Mathf.Cos(25f * Mathf.Deg2Rad), Mathf.Sin(25f * Mathf.Deg2Rad));
        var normal = new Vector2(-direction.y, direction.x);
        Vector2 centre = new Vector2(blackoutArena.center.x, blackoutArena.min.y + blackoutArena.size.y * .5f);
        // Shift the whole parallel group once, before any warning, to retain a reachable corridor.
        for (int shift = 0; shift < 8; shift++)
        {
            for (int i = 0; i < 4; i++)
                SetCut(redCuts[i + 2], centre + normal * ((i - 1.5f) * 3.8f + shift * .475f), direction);
            if (HasHumanEscape(null, true, 1f))
            {
                for (int i = 2; i < 6; i++) redCuts[i].locked = true;
                return;
            }
        }
        AbortUnsafePlacement();
    }

    private void AbortUnsafePlacement()
    {
        RestorePlayerCollision();
        BlackoutPlacementFailed = true;
        platformsBroken = true; // Cancel pending break; no platform is actually changed here.
        finalEnd = blackoutClock - .5f;
        if (vfx != null) vfx.ClearBossEffects();
        Enter(AttackState.BlackoutRecovery, 1.3f);
        Debug.LogWarning("BLACKOUT cancelled: no conservative 0% walk/fall corridor found. Check the bound arena with the real player controller.", this);
    }

    private void SetCut(RedCut cut, Vector2 centre, Vector2 direction)
    {
        direction.Normalize();
        // Clip to actual arena X bounds; Y may extend offscreen, exactly as the corresponding hit rectangle does.
        cut.from = centre + direction * ((blackoutArena.min.x - centre.x) / direction.x);
        cut.to = centre + direction * ((blackoutArena.max.x - centre.x) / direction.x);
    }

    private bool HasHumanEscape(RedCut single, bool curtain, float warningSeconds)
    {
        // Conservative 0% walk/fall samples using actual collider size, gravity and bound platform tops.
        // This is a placement check, not a second player controller: it never writes a Transform/velocity.
        Bounds current = playerCollider.bounds;
        var playerBody = player.GetComponent<Rigidbody2D>();
        float gravity = Physics2D.gravity.y * (playerBody != null ? playerBody.gravityScale : 1f);
        for (int i = 0; i < 6; i++)
        {
            var collider = i < 3 ? permanentGround[i] : breakableHigh[i - 3];
            routeSupportActive[i] = collider != null && collider.isActiveAndEnabled;
            if (routeSupportActive[i]) routeSupports[i] = collider.bounds;
        }
        float travel = 6f * Mathf.Max(0, warningSeconds - .3f);
        for (int step = -8; step <= 8; step++)
        {
            var safe = current;
            float targetX = Mathf.Clamp(current.center.x + travel * step / 8f,
                blackoutArena.min.x + current.extents.x + .1f, blackoutArena.max.x - current.extents.x - .1f);
            float vy = playerBody != null ? playerBody.linearVelocity.y : 0f;
            bool clear = true;
            float duration = warningSeconds + (curtain ? .78f : .12f);
            for (float elapsed = .02f; elapsed <= duration + .02f; elapsed += .02f)
            {
                float oldFeet = safe.min.y;
                float x = elapsed >= .3f ? Mathf.MoveTowards(safe.center.x, targetX, 6f * .02f) : safe.center.x;
                vy += gravity * .02f;
                safe.center = new Vector3(x, safe.center.y + vy * .02f, safe.center.z);
                float supportY = float.NegativeInfinity;
                if (vy <= 0)
                    for (int k = 0; k < 6; k++)
                    {
                        if (!routeSupportActive[k]) continue;
                        var b = routeSupports[k];
                        if (safe.max.x > b.min.x && safe.min.x < b.max.x && oldFeet >= b.max.y - .03f && safe.min.y <= b.max.y)
                            supportY = Mathf.Max(supportY, b.max.y);
                    }
                if (!float.IsNegativeInfinity(supportY)) { safe.center = new Vector3(x, supportY + safe.extents.y, safe.center.z); vy = 0; }
                var margin = safe;
                margin.Expand(new Vector3(.2f, .2f, 0));
                if (curtain)
                {
                    for (int k = 0; k < 4; k++)
                        if (elapsed >= 1f + k * .22f && elapsed <= 1.12f + k * .22f && IntersectsCut(redCuts[k + 2], margin)) { clear = false; break; }
                }
                else if (elapsed >= warningSeconds && IntersectsCut(single, margin)) clear = false;
                if (!clear) break;
            }
            if (clear) return true;
        }
        return false;
    }

    // OBB versus AABB SAT; used for route margins and platform cuts, never as a substitute for player physics contact.
    private static bool IntersectsCut(RedCut cut, Bounds bounds)
    {
        Vector2 delta = cut.to - cut.from;
        float halfLength = delta.magnitude * .5f;
        Vector2 along = delta.normalized, normal = new Vector2(-along.y, along.x);
        Vector2 separation = (Vector2)bounds.center - (cut.from + cut.to) * .5f;
        Vector2 e = bounds.extents;
        return Mathf.Abs(Vector2.Dot(separation, along)) <= halfLength + Mathf.Abs(along.x) * e.x + Mathf.Abs(along.y) * e.y
            && Mathf.Abs(Vector2.Dot(separation, normal)) <= RedWidth * .5f + Mathf.Abs(normal.x) * e.x + Mathf.Abs(normal.y) * e.y
            && Mathf.Abs(separation.x) <= e.x + Mathf.Abs(along.x) * halfLength + Mathf.Abs(normal.x) * RedWidth * .5f
            && Mathf.Abs(separation.y) <= e.y + Mathf.Abs(along.y) * halfLength + Mathf.Abs(normal.y) * RedWidth * .5f;
    }

    private void ClearBlackout()
    {
        RestorePlayerCollision(); // StopCombat/OnDisable/death/cancel and next BeginCombat all clear through here.
        blackoutClock = 0;
        for (int i = 0; i < redCuts.Length; i++) redCuts[i] = null;
        if (vfx != null) vfx.ClearBossEffects();
    }
}
