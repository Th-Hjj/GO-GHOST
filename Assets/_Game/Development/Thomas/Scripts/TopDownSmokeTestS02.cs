#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// The three S02 paths only, on the existing virtual-input / isolated-Archive driver.
public partial class TopDownSmokeTest
{
    private void S02ApprovedInterfaces()
    {
        // Detached DEV fixtures exercise the approved interfaces. They never populate the live room's unknown content.
        var state = new TowerNarrativeState(true);
        state.EnterStage("floor01");
        Check("OPEN-01 unconnected room cannot invent an outcome", !state.CommitFloor01Outcome(TowerNarrativeState.FloorAction.Leave, ProjectileActor.Environment)
            && state.Result == TowerNarrativeState.FloorResult.Unconnected, state.Result.ToString());
        state.ConnectFloor01Module();
        bool saved = state.CommitFloor01Outcome(TowerNarrativeState.FloorAction.Interrupt, ProjectileActor.Player);
        Check("OPEN-01 finite actual result is once only", saved && !state.CommitFloor01Outcome(TowerNarrativeState.FloorAction.Leave, ProjectileActor.Environment)
            && state.Result == TowerNarrativeState.FloorResult.Preserved, state.Result.ToString());
        var left = new TowerNarrativeState(true); left.EnterStage("floor01"); left.ConnectFloor01Module(); left.EnterStage("boss_p1");
        bool pending = left.Result == TowerNarrativeState.FloorResult.Pending;
        bool restored = left.CommitFloor01Outcome(TowerNarrativeState.FloorAction.Leave, ProjectileActor.Environment);
        left.EnterStage("boss_p2");
        Check("OPEN-01 departure alone is not a device result or witnessed recovery", pending && restored
            && !left.Floor01ReportDelivered(TowerNarrativeState.FloorReport.RestoredConfirmed)
            && left.Floor01ReportDelivered(TowerNarrativeState.FloorReport.LeftUnattended), left.KnownFloor01.ToString());
        state.EnterStage("boss_p3");
        Check("OPEN-02 undisclosed preservation is normal", state.FreezeP3Mode() == TowerNarrativeState.Mode.FullForce, state.FrozenP3Mode.ToString());
        var restrained = new TowerNarrativeState(true);
        restrained.EnterStage("floor01"); restrained.ConnectFloor01Module();
        restrained.CommitFloor01Outcome(TowerNarrativeState.FloorAction.Interrupt, ProjectileActor.Player);
        restrained.EnterStage("boss_p2");
        bool report = restrained.Floor01ReportDelivered(TowerNarrativeState.FloorReport.Preserved);
        restrained.CoexistenceReportDelivered(); restrained.EnterStage("boss_p3");
        Check("OPEN-02 actual preservation plus delivered knowledge/stance", report && restrained.FreezeP3Mode() == TowerNarrativeState.Mode.Restrained,
            restrained.FrozenP3Mode.ToString());
        restrained.EnterStage("final_disposition");
        Check("OPEN-02 mode is frozen after P3 entry", !restrained.ThreatReportDelivered(TowerNarrativeState.Threat.Erase, true)
            && restrained.FreezeP3Mode() == TowerNarrativeState.Mode.Restrained, restrained.FrozenP3Mode.ToString());
        var threat = new TowerNarrativeState(true);
        threat.EnterStage("floor01"); threat.ConnectFloor01Module();
        threat.CommitFloor01Outcome(TowerNarrativeState.FloorAction.Interrupt, ProjectileActor.Player);
        threat.EnterStage("boss_p2"); threat.Floor01ReportDelivered(TowerNarrativeState.FloorReport.Preserved); threat.CoexistenceReportDelivered();
        threat.EnterStage("office_intro");
        bool rejectsAmbiguous = !threat.ThreatReportDelivered(TowerNarrativeState.Threat.Control, false);
        threat.ThreatReportDelivered(TowerNarrativeState.Threat.Control, true); threat.EnterStage("boss_p3");
        Check("OPEN-02 explicit threat takes priority", rejectsAmbiguous && threat.FreezeP3Mode() == TowerNarrativeState.Mode.Overdrive, threat.FrozenP3Mode.ToString());
        state.EnterStage("final_disposition");
        Check("OPEN-03 no world binding means no ending", !state.TryFreezeExit(true) && state.PresidentAlive == null, state.FrozenExit.ToString());
        state.ConnectDispositionWorld(true, true); state.TransferOfferDelivered(); state.CancelTransfer();
        bool cancelled = !state.CommitPlayerTransferAndOrder(true) && !state.ForcedInterceptOrderActive;
        state.TransferOfferDelivered(); bool transferred = state.CommitPlayerTransferAndOrder(true);
        state.CommitPresidentDeath(); state.TryFreezeExit(true);
        Check("OPEN-03 cancel leaves no half state; commit survives president death", cancelled && transferred && state.ForcedInterceptOrderActive
            && state.FrozenExit == TowerNarrativeState.ExitResult.E, state.FrozenExit.ToString());
        var destroyed = new TowerNarrativeState(true); destroyed.EnterStage("final_disposition"); destroyed.ConnectDispositionWorld(true, true);
        destroyed.TransferOfferDelivered(); destroyed.CommitPlayerTransferAndOrder(true); destroyed.CommitCoreDestruction(); destroyed.TryFreezeExit(true);
        Check("OPEN-03 destroyed core takes priority over transfer", destroyed.FrozenExit == TowerNarrativeState.ExitResult.C, destroyed.FrozenExit.ToString());
        var warning = new TowerNarrativeState(true); warning.EnterStage("final_disposition"); warning.ConnectDispositionWorld(true, true);
        bool held = !warning.TryFreezeExit(true); warning.RestoreRiskDelivered();
        bool noAutoExit = warning.FrozenExit == TowerNarrativeState.ExitResult.Unresolved;
        bool exited = warning.TryFreezeExit(true);
        Check("OPEN-03 D requires delivered warning and another exit, then freezes once", held && noAutoExit && exited
            && warning.FrozenExit == TowerNarrativeState.ExitResult.D && !warning.TryFreezeExit(true) && !warning.CommitCoreDestruction(), warning.FrozenExit.ToString());
    }

    private IEnumerator S02Fresh(GameSessionFlow flow, int tier)
    {
        Keys(); MouseTo(.8f, .5f);
        if (flow.State == GameSessionFlow.FlowState.Deployment)
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Practice, 4f);
        if (flow.ActiveFight != null && !flow.ActiveFight.Player.IsDead)
        {
            flow.ActiveFight.Player.IsInvulnerable = false;
            flow.ActiveFight.Player.TakeDamage(100000, gameObject);
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Result, 2f);
        }
        if (flow.CanContinue) flow.RequestContinue();
        S01Field(flow, "developmentPermissionTier", tier);
        flow.RequestNewGame();
        flow.TrySetDraftSlot(LoadoutSlot.Torso, LoadoutRules.TorsoSlow);
        flow.RequestDeploy();
        yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Practice, 4f);
        flow.ActiveFight.Player.IsInvulnerable = true;
        flow.RequestEnterBossRoom();
        yield return Frames(2);
    }

    private IEnumerator S02WalkExit(GameSessionFlow flow)
    {
        string from = flow.TowerStageId;
        flow.ActiveFight.PlacePlayer(flow.CurrentRoom.Offset + new Vector2(20.5f, 0));
        yield return Frames(2);
        Keys(Key.D);
        yield return WeaponsWait(() => flow.TowerStageId != from, 3f);
        Keys();
        yield return Frames(2);
    }

    private IEnumerator S02Tower()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var ui = flow.GetComponent<MissionUI>();
        var focus = StartCoroutine(S01Focus(flow));
        Record("S02 only: three controlled paths. P1/P2 forced completions prove lifecycle, not balance. P3 body, 50F opportunity and completion are explicitly DEV. Isolated Archive.");
        try
        {
            S02ApprovedInterfaces();
            yield return S02Fresh(flow, 3);
            var fight = flow.ActiveFight;
            var player = fight.Player;
            var identity = player.GetComponent<OfficerIdentity>();
            var snapshot = flow.Deployment;
            var officer = flow.Officer;
            var gun = player.GetComponent<PlayerShooting>();
            var reader = player.GetComponent<PlayerInputReader>();
            var slow = player.GetComponent<PlayerTimeSlow>();
            int records = PoliceArchive.Current.RunRecords.Count, endings = PoliceArchive.Current.UnlockedEndings.Count;
            Check("route starts at tower_entry after practice", flow.TowerStageId == "tower_entry" && !fight.EncounterActive, flow.TowerStageId);
            yield return S02WalkExit(flow);
            Check("real doorway connects floor01", flow.TowerStageId == "floor01", flow.TowerStageId);
            yield return S02WalkExit(flow);
            Check("P1 is independent and gate locked", flow.TowerStageId == "boss_p1" && fight.EncounterActive
                && !flow.RequestTowerExit(flow.CurrentRoom), flow.TowerStageId);
            fight.Boss.StopCombat(); // keep the causal gun/prop pipeline real; suppress incoming damage in this flow fixture.
            slow.TryActivate();
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            float active = slow.ActiveRemaining, cooldown = slow.CooldownRemaining, elapsed = fight.CombatElapsed;
            int pulls = gun.TriggerPulls, ammo = gun.AmmoInMagazine;
            flow.DevelopmentRepeatRoomEvent();
            yield return new WaitForSecondsRealtime(.4f);
            Check("dialogue pauses clocks and creates no world fact", ui.DialogueVisible && Time.timeScale == 0
                && active == slow.ActiveRemaining && cooldown == slow.CooldownRemaining && elapsed == fight.CombatElapsed
                && flow.TowerFacts.Count == 0, $"facts={flow.TowerFacts.Count}, pause={fight.NarrativePaused}");
            var firstProp = flow.CurrentRoom.Prop;
            yield return ClickSessionButton("DialoguePanel");
            yield return ClickSessionButton("DialoguePanel");
            yield return WeaponsWait(() => firstProp.HasLanded, 4f);
            Check("T3 real gun, ammo, hit, release and landing", firstProp.HasLanded && gun.TriggerPulls > pulls
                && gun.AmmoInMagazine < ammo && firstProp.LastActor == ProjectileActor.InnerAI && flow.TowerFacts.Count == 2,
                $"landed={firstProp.HasLanded}, ammo={ammo}->{gun.AmmoInMagazine}, facts={flow.TowerFacts.Count}");
            flow.DevelopmentRepeatRoomEvent();
            yield return Frames(3);
            Check("same instance does not repeat dialogue or facts", !ui.DialogueVisible && flow.TowerFacts.Count == 2,
                $"dialogue={ui.DialogueVisible}, facts={flow.TowerFacts.Count}");
            player.IsInvulnerable = false; player.TakeDamage(17, gameObject); player.IsInvulnerable = true;
            int hp = player.CurrentHealth;
            ammo = gun.AmmoInMagazine; cooldown = slow.CooldownRemaining;
            fight.BossHealth.TakeDamage(fight.BossHealth.MaxHealth / 2 + 1, player.gameObject);
            yield return Frames(3);
            Check("P1 does not switch to old phase two", fight.Boss.Phase == CyberpsychoBoss.BossPhase.Phase1, fight.Boss.Phase.ToString());
            flow.DevelopmentCompleteEncounter();
            yield return Frames(3);
            Check("P1 completion is nonterminal", flow.TowerStageId == "boss_p1" && flow.CurrentRoom.ExitOpen
                && !fight.EncounterActive && fight.Result == BossFight.Outcome.Fighting && PoliceArchive.Current.RunRecords.Count == records
                && reader.GameplayInputEnabled, flow.State.ToString());
            flow.RequestTowerExit(flow.CurrentRoom);
            yield return Frames(2);
            Check("room transition preserves resources and prior facts", flow.TowerStageId == "floor50" && player.CurrentHealth == hp
                && gun.AmmoInMagazine == ammo && slow.CooldownRemaining <= cooldown && slow.CooldownRemaining > cooldown - 1f
                && flow.TowerFacts.Count == 2 && !reader.HasTemporaryAimOverride, $"hp={hp}/{player.CurrentHealth}, ammo={ammo}/{gun.AmmoInMagazine}");
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            var secondProp = flow.CurrentRoom.Prop;
            Check("separate DEV 50F instance offers same JSON sequence", ui.DialogueVisible && secondProp != firstProp
                && flow.TowerFacts.All(f=>f.EventId == "boss_p1.chandelier"), $"instance={flow.CurrentRoom.PropEventInstanceId}");
            yield return ClickSessionButton("DialoguePanel");
            yield return ClickSessionButton("DialoguePanel");
            yield return WeaponsWait(() => secondProp.HasLanded, 4f);
            Check("separate actual result retains scope and causal actor", flow.TowerFacts.Count == 4
                && flow.TowerFacts.Count(f=>f.DevelopmentOnly && f.Actor == ProjectileActor.InnerAI) == 2,
                $"facts={flow.TowerFacts.Count}");
            flow.RequestTowerExit(flow.CurrentRoom);
            yield return Frames(3);
            Check("P2 starts existing blade phase in its own bounds", flow.TowerStageId == "boss_p2"
                && fight.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2 && fight.BossHealth.PhaseIndex == 2
                && flow.CurrentRoom.PlayableBounds.Contains(fight.Boss.transform.position), fight.Boss.transform.position.ToString());
            yield return Seconds(.6f); // allow a live P2 update in the translated room.
            flow.DevelopmentCompleteEncounter();
            yield return Frames(3);
            Check("P2 is nonterminal with same identity and snapshot", flow.CurrentRoom.ExitOpen && flow.ActiveFight == fight
                && fight.Player == player && flow.Officer == officer && ReferenceEquals(snapshot, identity.Deployment)
                && ReferenceEquals(snapshot, flow.Deployment) && PoliceArchive.Current.RunRecords.Count == records, flow.TowerStageId);
            flow.RequestTowerExit(flow.CurrentRoom); // 99F
            yield return Frames(2);
            Check("99F empty DEV node does not invent choices", flow.TowerStageId == "floor99" && flow.TowerFacts.Count == 4, flow.TowerStageId);
            flow.RequestTowerExit(flow.CurrentRoom); // office
            flow.RequestTowerExit(flow.CurrentRoom); // P3
            yield return Frames(3);
            Check("P3 captures input facts without guessing a mode", flow.TowerStageId == "boss_p3" && flow.P3EntryFacts.Count == 4
                && flow.P3DevelopmentMode == GameSessionFlow.DevelopmentP3Mode.Unspecified
                && flow.Narrative.FrozenP3Mode == TowerNarrativeState.Mode.FullForce && !flow.Narrative.DispositionConnected, flow.TowerStageId);
            var carrier = fight.Boss;
            flow.DevelopmentCompleteEncounter();
            yield return Frames(3);
            Check("P3 enters disposition with player and carrier retained", flow.TowerStageId == "final_disposition" && carrier != null
                && flow.ActiveFight == fight && fight.Player == player && reader.GameplayInputEnabled && gun.enabled
                && ReferenceEquals(flow.Deployment, snapshot) && flow.EndingId == null
                && PoliceArchive.Current.RunRecords.Count == records, flow.State.ToString());
            var exit = flow.CurrentRoom;
            bool firstExit = flow.RequestTowerExit(exit), repeatedExit = flow.RequestTowerExit(exit);
            yield return Frames(3);
            Check("final DEV exit completes once without Archive or percentage fallback", firstExit && !repeatedExit
                && flow.DevelopmentCompletionCount == 1 && flow.LastDevelopmentCompletionFacts.Count == 4
                && flow.State == GameSessionFlow.FlowState.MainMenu && flow.EndingId == null
                && PoliceArchive.Current.RunRecords.Count == records && PoliceArchive.Current.UnlockedEndings.Count == endings,
                $"completions={flow.DevelopmentCompletionCount}, records={PoliceArchive.Current.RunRecords.Count}");

            // Mid-mission death while a later event is paused; new officer starts from the beginning.
            yield return S02Fresh(flow, 2);
            fight = flow.ActiveFight; player = fight.Player; reader = player.GetComponent<PlayerInputReader>();
            snapshot = flow.Deployment; officer = flow.Officer;
            flow.RequestTowerExit(flow.CurrentRoom); flow.RequestTowerExit(flow.CurrentRoom);
            fight.Boss.StopCombat();
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            gun = player.GetComponent<PlayerShooting>(); pulls = gun.TriggerPulls;
            yield return ClickSessionButton("DialoguePanel");
            yield return Frames(2);
            Check("T2 starts temporary aim after close", reader.HasTemporaryAimOverride && reader.OverrideRemaining > 2.7f, $"timer={reader.OverrideRemaining}");
            yield return Seconds(3.1f);
            Check("T2 refusal restores aim without a success fact", !reader.HasTemporaryAimOverride && gun.TriggerPulls == pulls
                && flow.TowerFacts.Count == 0 && flow.CurrentRoom.Prop.IsHanging, $"pulls={gun.TriggerPulls}, facts={flow.TowerFacts.Count}");
            yield return S01ManualHit(flow.CurrentRoom.Prop);
            Check("T2 later human hit records human actor", flow.TowerFacts.Count == 2
                && flow.TowerFacts.All(f=>f.Actor == ProjectileActor.Player && f.PermissionTier == 2), $"facts={flow.TowerFacts.Count}");
            flow.DevelopmentCompleteEncounter(); yield return Frames(2);
            flow.RequestTowerExit(flow.CurrentRoom);
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            records = PoliceArchive.Current.RunRecords.Count;
            var resetProp = flow.CurrentRoom.Prop;
            player.IsInvulnerable = false;
            player.TakeDamage(100000, gameObject);
            yield return Frames(4);
            Check("middle-room death clears pause, locks, facts and mechanism", flow.State == GameSessionFlow.FlowState.Result
                && Time.timeScale == 1f && !ui.DialogueVisible && !reader.HasTemporaryAimOverride && flow.TowerFacts.Count == 0
                && firstProp.IsHanging && resetProp.IsHanging && PoliceArchive.Current.RunRecords.Count == records + 1
                && flow.DevelopmentCompletionCount == 0, flow.State.ToString());
            flow.RequestContinue(); flow.RequestNewGame(); flow.RequestDeploy();
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Practice, 4f);
            Check("new officer redeploys at practice with fresh snapshot/resources", flow.ActiveFight.Player != player && flow.Officer != officer
                && !ReferenceEquals(flow.Deployment, snapshot) && flow.Deployment.OfficerIndex > snapshot.OfficerIndex
                && flow.ActiveFight.Player.CurrentHealth == flow.ActiveFight.Player.MaxHealth
                && flow.ActiveFight.Player.GetComponent<PlayerShooting>().AmmoInMagazine == flow.ActiveFight.Player.GetComponent<PlayerShooting>().MagazineSize
                && !flow.ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride && flow.TowerFacts.Count == 0,
                $"officer={snapshot.OfficerIndex}->{flow.Deployment.OfficerIndex}, stage={flow.TowerStageId}");
            flow.RequestEnterBossRoom();
            Check("redeployment restarts route, no checkpoint", flow.TowerStageId == "tower_entry" && flow.ActiveFight.BossHealth == null, flow.TowerStageId);
        }
        finally { StopCoroutine(focus); Keys(); MouseTo(.8f, .5f); }
    }
}
#endif
