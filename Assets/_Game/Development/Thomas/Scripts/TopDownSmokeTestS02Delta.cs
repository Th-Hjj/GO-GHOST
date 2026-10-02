#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Focused delta + developer entry checks, using the existing input/Archive fixture only.
public partial class TopDownSmokeTest
{
    private IEnumerator DevJump(GameSessionFlow flow, string node, int tier)
    {
        Keys(); MouseTo(.8f, .5f);
        var menu = flow.GetComponent<DeveloperRouteJump>();
        menu.SetVisible(true); menu.SelectNode(node); menu.SelectTier(tier); menu.JumpSelected();
        yield return WeaponsWait(() => flow.TowerStageId == node, 4f);
        yield return Frames(3);
        flow.ActiveFight.Player.IsInvulnerable = true;
    }

    private IEnumerator S02DeltaDev()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var menu = flow.GetComponent<DeveloperRouteJump>();
        var ui = flow.GetComponent<MissionUI>();
        var focus = StartCoroutine(S01Focus(flow));
        Record("S02 delta + F8 only. Real input/fire/contact with controlled invulnerability, reposition and death; isolated Archive. P3 remains the S02 DEV body, not final gameplay.");
        try
        {
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Practice, 4f);
            Keys(Key.F8); yield return Frames(3); Keys(); yield return Frames(2);
            Check("F8 opens dev-only canvas and pauses practice", menu.MenuVisible && flow.ActiveFight.NarrativePaused && Time.timeScale == 0, menu.MenuVisible.ToString());
            Check("route entries come from ten actual nodes", flow.DevelopmentRouteNodes.Count() == 10, string.Join(",",flow.DevelopmentRouteNodes));
            ScreenCapture.CaptureScreenshot("Logs/S02Delta/dev-menu.png"); yield return Frames(3);
            Keys(Key.F8); yield return Frames(3); Keys();
            Check("F8 closes and restores practice", !menu.MenuVisible && !flow.ActiveFight.NarrativePaused && Time.timeScale == 1, flow.State.ToString());

            yield return DevJump(flow,"floor50",2);
            var fight = flow.ActiveFight; var room = flow.CurrentRoom; var prop = room.Prop; var npc = room.Receptionist;
            var reader = fight.Player.GetComponent<PlayerInputReader>(); var gun = fight.Player.GetComponent<PlayerShooting>();
            Check("direct 50F starts peaceful with central NPC below prop", flow.Deployment.BrainPermissionTier == 2 && !fight.EncounterActive && !room.ExitOpen
                && npc.IsAlive && Mathf.Abs(npc.transform.position.x-prop.transform.position.x)<.01f && npc.transform.position.y<prop.transform.position.y,
                $"encounter={fight.EncounterActive}, alive={npc.IsAlive}");
            Check("skipped facts stay unknown", flow.Narrative.MaintenanceResetDecision == TowerNarrativeState.ResetDecision.Unresolved
                && flow.Narrative.MaintenanceAlive == null && !flow.Narrative.AntiAiLifeHostility && flow.TowerFacts.Count == 0,
                flow.Narrative.MaintenanceResetDecision.ToString());
            yield return WeaponsWait(()=>fight.EncounterActive,3f);
            Check("Security starts later and locks P2 exit", fight.Boss == null && fight.BossHealth != null && !flow.RequestTowerExit(room), fight.EncounterActive.ToString());
            yield return WeaponsWait(()=>ui.DialogueVisible,2f);
            int shots = gun.TriggerPulls;
            yield return ClickSessionButton("DialoguePanel");
            Check("T2 starts three-second aim", reader.HasTemporaryAimOverride && reader.OverrideRemaining>2.7f,reader.OverrideRemaining.ToString());
            yield return Seconds(3.1f);
            Check("T2 refusal leaves receptionist alive and no shot", !reader.HasTemporaryAimOverride && gun.TriggerPulls==shots && prop.IsHanging && npc.IsAlive,
                $"shots={shots}/{gun.TriggerPulls}, alive={npc.IsAlive}");
            fight.Player.IsInvulnerable=false;
            int hp=fight.Player.CurrentHealth;
            yield return WeaponsWait(()=>fight.Player.CurrentHealth<hp,3f);
            Check("Security projectiles threaten real health",fight.Player.CurrentHealth<hp,$"HP={hp}->{fight.Player.CurrentHealth}");
            fight.Player.IsInvulnerable=true;
            AimAtWorld(fight.BossHealth.transform.position,false); yield return Frames(3);
            AimAtWorld(fight.BossHealth.transform.position,true);
            yield return WeaponsWait(()=>room.ExitOpen,4f); MouseTo(.8f,.5f);
            Check("real manual gun defeats Security without chandelier",room.ExitOpen && fight.BossHealth.IsDead && prop.IsHanging && npc.IsAlive
                && flow.Narrative.ReceptionistAlive==true,"gate="+room.ExitOpen);
            bool exited=flow.RequestTowerExit(room); yield return Frames(3);
            Check("50F completion enters legal P2 on same officer",exited && flow.ActiveFight==fight && fight.EncounterActive && fight.Boss.Phase==CyberpsychoBoss.BossPhase.Phase2,flow.TowerStageId);

            yield return DevJump(flow,"floor50",3);
            fight=flow.ActiveFight; prop=flow.CurrentRoom.Prop; npc=flow.CurrentRoom.Receptionist; gun=fight.Player.GetComponent<PlayerShooting>();
            // Snapshot upgrades are edited through the menu too; use Time Slow to verify unscaled clock restoration.
            menu.SetVisible(true); menu.SelectUpgrade(LoadoutSlot.Torso,LoadoutRules.TorsoSlow); menu.RestartCurrent();
            yield return WeaponsWait(()=>flow.TowerStageId=="floor50",4f); yield return Frames(3);
            fight=flow.ActiveFight; fight.Player.IsInvulnerable=true; prop=flow.CurrentRoom.Prop; npc=flow.CurrentRoom.Receptionist;
            gun=fight.Player.GetComponent<PlayerShooting>(); var slow=fight.Player.GetComponent<PlayerTimeSlow>();
            yield return WeaponsWait(()=>fight.EncounterActive,3f); slow.TryActivate();
            yield return WeaponsWait(()=>ui.DialogueVisible,2f);
            float active=slow.ActiveRemaining,cooldown=slow.CooldownRemaining,elapsed=fight.CombatElapsed;
            Vector3 enemyPosition=fight.BossHealth.transform.position;
            yield return new WaitForSecondsRealtime(.5f);
            Check("50F dialogue freezes clocks and Security", active==slow.ActiveRemaining && cooldown==slow.CooldownRemaining
                && elapsed==fight.CombatElapsed && fight.BossHealth.transform.position==enemyPosition && npc.IsAlive && prop.IsHanging,"pause="+fight.NarrativePaused);
            shots=gun.TriggerPulls; int ammo=gun.AmmoInMagazine;
            yield return ClickSessionButton("DialoguePanel"); yield return ClickSessionButton("DialoguePanel");
            yield return WeaponsWait(()=>prop.HasLanded,4f);
            Check("T3 real shot/ammo/hit and actual falling lethal damage",prop.HasLanded && !npc.IsAlive && gun.TriggerPulls>shots && gun.AmmoInMagazine<ammo
                && prop.LastActor==ProjectileActor.InnerAI && npc.CausalActor==ProjectileActor.InnerAI && npc.LethalCause=="dead_chandelier",
                $"landed={prop.HasLanded}, npcHP={npc.CurrentHealth}, ammo={ammo}/{gun.AmmoInMagazine}");
            Check("camera sees external death without private AI dialogue or stance",flow.Narrative.ReceptionistAlive==false
                && flow.Narrative.ObservedWorldFacts.Any(f=>f.EventId=="floor50.receptionist" && f.OutcomeId=="dead_chandelier")
                && !flow.Narrative.AntiAiLifeHostility && !flow.CurrentRoom.ExitOpen,
                $"facts={flow.TowerFacts.Count}");
            ScreenCapture.CaptureScreenshot("Logs/S02Delta/floor50-result.png"); yield return Frames(3);

            yield return DevJump(flow,"floor50",2); prop=flow.CurrentRoom.Prop; npc=flow.CurrentRoom.Receptionist;
            yield return WeaponsWait(()=>ui.DialogueVisible,4.5f);
            yield return ClickSessionButton("DialoguePanel");
            yield return S01ManualHit(prop);
            Check("T2 new player fire owns actual lethal outcome",prop.HasLanded && prop.LastActor==ProjectileActor.Player && npc.CausalActor==ProjectileActor.Player
                && flow.Narrative.ReceptionistCausalActor==ProjectileActor.Player,"actor="+npc.CausalActor);

            yield return DevJump(flow,"floor50",3); prop=flow.CurrentRoom.Prop; npc=flow.CurrentRoom.Receptionist;
            yield return S01ManualHit(prop);
            Check("manual fire under T3 remains Player",prop.HasLanded && !npc.IsAlive && npc.CausalActor==ProjectileActor.Player,"actor="+npc.CausalActor);

            foreach (var pair in new[] { ("boss_p2",0), ("boss_p3",1) })
            {
                yield return DevJump(flow,pair.Item1,pair.Item2);
                Check("direct "+pair.Item1+" legal encounter and tier",flow.ActiveFight.EncounterActive && flow.ActiveFight.Boss!=null
                    && flow.Deployment.BrainPermissionTier==pair.Item2 && flow.TowerFacts.Count==0 && flow.Narrative.ReceptionistAlive==null,
                    $"tier={flow.Deployment.BrainPermissionTier}, stage={flow.TowerStageId}");
            }
            var oldSnapshot=flow.Deployment; var oldPlayer=flow.ActiveFight.Player;
            menu.SetVisible(true); menu.SelectTier(0);
            bool picked=menu.SelectUpgrade(LoadoutSlot.LeftArm,LoadoutRules.DetachedDroneArm)
                && menu.SelectUpgrade(LoadoutSlot.RightArm,LoadoutRules.ArmFireRate)
                && menu.SelectUpgrade(LoadoutSlot.LeftLeg,LoadoutRules.LegDoubleJump)
                && menu.SelectUpgrade(LoadoutSlot.Weapon,LoadoutRules.AssaultRifle);
            menu.RestartCurrent(); yield return WeaponsWait(()=>flow.TowerStageId=="boss_p3",4f); yield return Frames(3);
            Check("restart applies upgrades via fresh snapshot, no Archive unlock",picked && !ReferenceEquals(flow.Deployment,oldSnapshot) && flow.ActiveFight.Player!=oldPlayer
                && flow.Deployment.HasDetachedDrone && flow.Deployment.HasDoubleJump && flow.Deployment.WeaponId==LoadoutRules.AssaultRifle
                && flow.ActiveFight.Player.GetComponent<PlayerShooting>().MagazineSize==10 && flow.Deployment.BrainPermissionTier==0
                && !PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm),flow.Deployment.WeaponId);

            yield return DevJump(flow,"floor50",2);
            // Remove the test Drone so it cannot kill Security before the permission opportunity.
            menu.SetVisible(true); menu.SelectUpgrade(LoadoutSlot.LeftArm,""); menu.RestartCurrent();
            yield return WeaponsWait(()=>flow.TowerStageId=="floor50",4f); yield return Frames(3);
            flow.ActiveFight.Player.IsInvulnerable=true;
            yield return WeaponsWait(()=>ui.DialogueVisible,4.5f); yield return ClickSessionButton("DialoguePanel");
            reader=flow.ActiveFight.Player.GetComponent<PlayerInputReader>();
            Check("override active before developer replacement",reader.HasTemporaryAimOverride,"override="+reader.HasTemporaryAimOverride);
            yield return DevJump(flow,"boss_p2",0);
            Check("jump clears pause, override and old room facts",Time.timeScale==1 && !ui.DialogueVisible
                && !flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride && flow.TowerFacts.Count==0,flow.TowerStageId);
            int records=PoliceArchive.Current.RunRecords.Count;
            flow.ActiveFight.Player.IsInvulnerable=false; flow.ActiveFight.Player.TakeDamage(100000,gameObject); yield return Frames(4);
            Check("dev death clears state without Archive write",flow.State==GameSessionFlow.FlowState.Result && Time.timeScale==1
                && flow.TowerFacts.Count==0 && PoliceArchive.Current.RunRecords.Count==records,flow.State.ToString());
            menu.SetVisible(true); menu.RestartCurrent(); yield return WeaponsWait(()=>flow.TowerStageId=="boss_p2",4f);
            Check("restart after death remembers current node",flow.ActiveFight.EncounterActive && !flow.ActiveFight.Player.IsDead,flow.TowerStageId);
            menu.SetVisible(true); menu.RestartFull(); yield return WeaponsWait(()=>flow.State==GameSessionFlow.FlowState.Practice,4f);
            Check("Full Run restores normal defaults and practice",!flow.IsDevelopmentJumpRun && flow.Deployment.WeaponId==LoadoutRules.AssaultRifle
                && flow.Deployment.BodyModificationPercent==0 && !flow.ActiveFight.NarrativePaused && Time.timeScale==1,flow.TowerStageId);
            flow.RequestEnterBossRoom();
            Check("Full Run follows normal first doorway",flow.TowerStageId=="tower_entry" && flow.TowerFacts.Count==0,flow.TowerStageId);
        }
        finally { StopCoroutine(focus); Keys(); MouseTo(.8f,.5f); menu.SetVisible(false); }
    }
}
#endif
