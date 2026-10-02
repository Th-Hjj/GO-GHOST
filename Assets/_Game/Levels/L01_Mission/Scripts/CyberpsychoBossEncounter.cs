using System.Collections.Generic;
using UnityEngine;

public partial class CyberpsychoBoss
{
    private bool independentEncounter;
    private float arenaCenterX;
    private float ArenaLeft => arenaCenterX - arenaHalfWidth;
    private float ArenaRight => arenaCenterX + arenaHalfWidth;

    // Fresh room actor. Only ownership, existing phase entry and bounds change; attacks are reused.
    public void BeginRoomEncounter(PlayerHealth target, Bounds bounds, bool blade, bool developmentDummy)
    {
        independentEncounter = true;
        player = target;
        health.SetTarget(target);
        timeSlow = target.GetComponent<PlayerTimeSlow>();
        playerCollider = target.GetComponent<Collider2D>();
        playerCounter = target.GetComponent<PlayerCounterArm>();
        playerOfficer = target.GetComponent<OfficerIdentity>();
        arenaCenterX = bounds.center.x;
        arenaHalfWidth = bounds.extents.x;
        highSweepY += bounds.min.y - floorY;
        floorY = bounds.min.y;
        // Camo Rush uses these bounds too, but BLACKOUT stays unbound without its authored platforms.
        blackoutArena = bounds;
        health.ConfigureSingleEncounter(blade);
        BeginCombat();
        if (developmentDummy)
        {
            StopCombat(); // S02 P3 DEV: damageable existing body, no invented moves or mode.
            return;
        }
        if (blade)
        {
            ClearAttack();
            Phase = BossPhase.Phase2;
            SetWeapons(false, true);
            BeginSlash();
        }
    }

    public void ApplyFinalProfile(TowerRoom.FinalProfile profile)
    {
        // Reuse the existing burst/mark/reposition/sweep cycle, with an actual floating body and Inspector tuning.
        rb.gravityScale = 0;
        rb.linearVelocity = Vector2.zero;
        repositionX = 4f;
        burstShots = profile.burstCount;
        burstInterval *= profile.cadence; shotWindup *= profile.cadence;
        markedTrackSeconds *= profile.cadence; markedLockSeconds *= profile.cadence;
        shotRecovery *= profile.recovery; spreadRecovery *= profile.recovery; chargeRecovery *= profile.recovery;
    }

    public void ResumeRoomCombat()
    {
        if (health.IsDead || player == null || player.IsDead) return;
        combatEnabled = true;
        ClearAttack();
        if (Phase == BossPhase.Phase2) { SetWeapons(false, true); BeginSlash(); }
        else { SetWeapons(true, false); Enter(AttackState.Arrival, arrivalDelay); }
    }

    public SpriteRenderer[] RoomPresentationParts()
    {
        var parts = new List<SpriteRenderer> { body };
        if (gunVisual != null) parts.AddRange(gunVisual.GetComponentsInChildren<SpriteRenderer>(true));
        if (bladeVisual != null) parts.AddRange(bladeVisual.GetComponentsInChildren<SpriteRenderer>(true));
        return parts.ToArray();
    }
}
