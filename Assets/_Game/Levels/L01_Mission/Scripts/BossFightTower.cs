using System;
using UnityEngine;

public partial class BossFight
{
    public bool TowerMission { get; private set; }
    public bool EncounterActive { get; private set; }
    public event Action OnEncounterEnded;

    public void EnableTowerMission() => TowerMission = true;

    public void LeaveRoom(bool retainDefeatedCarrier = false)
    {
        EncounterActive = false;
        deathPending = false;
        SetNarrativePaused(false);
        player.GetComponent<PlayerInputReader>().SetEncounterAimTarget(null);
        player.GetComponent<PlayerDetachedDroneArm>()?.BindTarget(null);
        foreach (var bullet in GetComponentsInChildren<Projectile>(true)) bullet.Retire();
        GetComponent<AttemptPresentationVfx>()?.ClearBossEffects();
        if (bossHealth == null) return;
        bossHealth.OnDied -= BossDied;
        if (boss != null) boss.StopCombat();
        bossHealth.StopCombat();
        if (retainDefeatedCarrier) return;
        bossHealth.gameObject.SetActive(false);
        Destroy(bossHealth.gameObject);
        boss = null;
        bossHealth = null;
    }

    public void BeginRoomEncounter(CyberpsychoBoss source, TowerRoom room, bool staged = false)
    {
        if (!TowerMission || !combatRunning || EncounterActive || player.IsDead) return;
        boss = Instantiate(source, room.BossSpawn, Quaternion.identity, transform);
        boss.gameObject.SetActive(true);
        bossHealth = boss.GetComponent<Enemy>();
        bossHealth.OnDied += BossDied;
        player.GetComponent<PlayerInputReader>().SetEncounterAimTarget(bossHealth);
        player.GetComponent<PlayerDetachedDroneArm>()?.BindTarget(staged ? null : bossHealth);
        GetComponent<AttemptPresentationVfx>()?.BindBossParts(boss.RoomPresentationParts());
        EncounterActive = !staged;
        boss.BeginRoomEncounter(player, room.PlayableBounds, room.Kind == TowerRoom.RoomKind.Blade,
            false);
        if (staged) boss.StopCombat();
    }

    public void StartPreparedEncounter()
    {
        if (bossHealth == null || player.IsDead) return;
        EncounterActive = true;
        player.GetComponent<PlayerDetachedDroneArm>()?.BindTarget(bossHealth);
        if (boss != null) boss.ResumeRoomCombat(); else bossHealth.ResumeCombat();
    }

    // Reuse the same encounter health/target/exit lifecycle for the existing basic enemy.
    public void BeginSecurityEncounter(TowerRoom room, bool staged = false)
    {
        if (!TowerMission || !combatRunning || EncounterActive || player.IsDead || room.SecurityTemplate == null) return;
        boss = null;
        bossHealth = Instantiate(room.SecurityTemplate, room.SecurityTemplate.transform.position, Quaternion.identity, transform);
        bossHealth.gameObject.SetActive(true);
        bossHealth.SetTarget(player);
        bossHealth.OnDied += BossDied;
        player.GetComponent<PlayerInputReader>().SetEncounterAimTarget(bossHealth);
        player.GetComponent<PlayerDetachedDroneArm>()?.BindTarget(staged ? null : bossHealth);
        EncounterActive = !staged;
        if (staged) bossHealth.StopCombat();
    }

    public void CompleteTowerMission()
    {
        if (!TowerMission || Result != Outcome.Fighting || player.IsDead) return;
        Result = Outcome.Won;
        CombatDuration = CombatElapsed;
        ResultBossHealth = bossHealth != null ? bossHealth.CurrentHealth : 0;
        ResultPlayerHealth = player.CurrentHealth;
        var shooting = player.GetComponent<PlayerShooting>();
        AccuracyPercent = shooting.ShotsFired == 0 ? 0 : 100f * shooting.ShotsHit / shooting.ShotsFired;
        EncounterActive = false;
        CancelCombat();
    }

    private void TickTowerMission()
    {
        if (Result != Outcome.Fighting || (!combatRunning && !practicing)) return;
        // Death wins even if it coincides with the final enemy hit or a doorway.
        if (player.IsDead)
        {
            Result = Outcome.Died;
            CombatDuration = CombatElapsed;
            ResultBossHealth = bossHealth != null ? bossHealth.CurrentHealth : 0;
            ResultPlayerHealth = 0;
            var shooting = player.GetComponent<PlayerShooting>();
            AccuracyPercent = shooting.ShotsFired == 0 ? 0 : 100f * shooting.ShotsHit / shooting.ShotsFired;
            EncounterActive = false;
            CancelCombat();
            OnResolved?.Invoke(Result);
            return;
        }
        if (NarrativePaused || !EncounterActive || bossHealth == null || !bossHealth.IsDead) return;
        EncounterActive = false;
        deathPending = false;
        player.GetComponent<PlayerInputReader>().SetEncounterAimTarget(null);
        player.GetComponent<PlayerDetachedDroneArm>()?.BindTarget(null);
        if (boss != null) boss.StopCombat();
        foreach (var bullet in GetComponentsInChildren<Projectile>(true)) bullet.Retire();
        GetComponent<AttemptPresentationVfx>()?.ClearBossEffects();
        OnEncounterEnded?.Invoke(); // Player, mission timer, ammunition and abilities stay alive.
    }
}
