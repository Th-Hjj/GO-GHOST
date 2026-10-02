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

    public SpriteRenderer[] RoomPresentationParts()
    {
        var parts = new List<SpriteRenderer> { body };
        if (gunVisual != null) parts.AddRange(gunVisual.GetComponentsInChildren<SpriteRenderer>(true));
        if (bladeVisual != null) parts.AddRange(bladeVisual.GetComponentsInChildren<SpriteRenderer>(true));
        return parts.ToArray();
    }
}
