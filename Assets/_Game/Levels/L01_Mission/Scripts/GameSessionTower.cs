using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class GameSessionFlow
{
    [Header("Short Tower route: shared run, local encounters, explicit final dispositions")]
    [SerializeField] private bool towerPrototypeEnabled;
    [SerializeField] private TowerRoom[] towerRooms = new TowerRoom[0];
    public enum DevelopmentP3Mode { Unspecified, Overdrive, FullForce, Restrained }
    [SerializeField] private DevelopmentP3Mode developmentP3Mode;
    private static readonly string[] TowerRoute = { "tower_entry", "floor01", "boss_p1", "floor50",
        "boss_p2", "floor99", "office_intro", "boss_p3", "final_disposition" };
    private readonly HashSet<string> towerEvents = new HashSet<string>();
    private readonly List<TowerFact> towerFacts = new List<TowerFact>();
    private int towerRoomIndex = -1;
    private bool towerTerminal;
    private float roomEnteredAt;
    private float opportunityReadyAt;
    public TowerRoom CurrentRoom => towerRoomIndex >= 0 && towerRoomIndex < towerRooms.Length ? towerRooms[towerRoomIndex] : null;
    public string TowerStageId => CurrentRoom != null ? CurrentRoom.StageId
        : State == FlowState.Practice ? "practice" : State == FlowState.Loadout ? "loadout" : null;
    public IReadOnlyList<TowerFact> TowerFacts => towerFacts.AsReadOnly();
    public TowerNarrativeState Narrative { get; private set; } = new TowerNarrativeState(true);
    public IReadOnlyList<TowerFact> P3EntryFacts { get; private set; }
    public DevelopmentP3Mode P3DevelopmentMode { get; private set; }
    public IReadOnlyList<TowerFact> LastDevelopmentCompletionFacts { get; private set; }
    public int DevelopmentCompletionCount { get; private set; }

    public readonly struct TowerFact
    {
        public readonly string EventId, OutcomeId;
        public readonly ProjectileActor Actor;
        public readonly int PermissionTier;
        public readonly bool DevelopmentOnly;
        public TowerFact(string id, string outcome, ProjectileActor actor, int tier, bool developmentOnly)
        { EventId = id; OutcomeId = outcome; Actor = actor; PermissionTier = tier; DevelopmentOnly = developmentOnly; }
    }

    private void InitializeTowerRooms()
    {
        if (president != null) president.OnDied += RecordFinalDeath;
        if (continuingCore != null) continuingCore.OnDied += RecordFinalDeath;
        foreach (var room in towerRooms)
        {
            if (room == null) continue;
            room.Bind(this);
            if (room.Prop != null) room.Prop.OnWorldOutcome += RecordTowerPropOutcome;
            if (room.Receptionist != null) room.Receptionist.OnDied += RecordReceptionistDeath;
            if (room.Character != null) room.Character.OnDied += RecordCharacterDeath;
        }
    }

    private void ResetTowerRun()
    {
        if (!towerPrototypeEnabled) return;
        ClearTowerWorld();
        towerEndingId = null; TowerCompletionCount = 0;
        towerTerminal = false;
        DevelopmentCompletionCount = 0;
        LastDevelopmentCompletionFacts = null;
    }

    private void ClearTowerWorld()
    {
        if (!towerPrototypeEnabled) return;
        CancelStory();
        CancelIntervention();
        towerEvents.Clear();
        towerFacts.Clear();
        Narrative = new TowerNarrativeState(false);
        maintenancePhase = 0;
        if (managementRoot != null) managementRoot.gameObject.SetActive(false);
        president?.ResetEncounter(); continuingCore?.ResetEncounter();
        P3EntryFacts = null;
        P3DevelopmentMode = DevelopmentP3Mode.Unspecified;
        towerRoomIndex = -1;
        chandelier = null;
        foreach (var room in towerRooms)
        {
            if (room == null) continue;
            room.Prop?.ResetEncounter();
            room.Receptionist?.ResetEncounter();
            room.Character?.ResetEncounter();
            room.SetExitOpen(false);
            room.gameObject.SetActive(false);
        }
    }

    private bool BeginTowerRoute(int initialRoomIndex = 0)
    {
        if (towerRooms.Length != TowerRoute.Length) { Debug.LogError("[S02] Incomplete room route.", this); return false; }
        for (int i = 0; i < TowerRoute.Length; i++)
            if (towerRooms[i] == null || towerRooms[i].StageId != TowerRoute[i])
            { Debug.LogError("[S02] Missing or out-of-order room: " + TowerRoute[i], this); return false; }
        ActiveFight.BeginCombat(); // Existing practice-door refill exactly once; never on room transitions.
        ActiveFight.OnEncounterEnded += TowerEncounterEnded;
        SetState(FlowState.Combat);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (developmentJumpRun && towerRooms[initialRoomIndex].Kind == TowerRoom.RoomKind.DevelopmentDisposition)
        {
            // Local test prerequisite only: no invented earlier route facts or Archive result.
            ActiveFight.LeaveRoom();
            ActiveFight.BeginRoomEncounter(attemptPrefab.Boss, towerRooms[initialRoomIndex - 1], true);
            ActiveFight.BossHealth.TakeDamage(int.MaxValue, ActiveFight.Player.gameObject);
            ActiveFight.PlacePlayer(towerRooms[initialRoomIndex].Offset);
        }
#endif
        EnterTowerRoom(initialRoomIndex);
        return true;
    }

    private void EnterTowerRoom(int index)
    {
        CancelStory();
        CancelIntervention();
        bool postFinal = towerRooms[index].Kind == TowerRoom.RoomKind.DevelopmentDisposition;
        ActiveFight.LeaveRoom(postFinal);
        if (CurrentRoom != null) CurrentRoom.gameObject.SetActive(false);
        towerRoomIndex = index;
        var room = CurrentRoom;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (developmentJumpRun) developmentLastNode = room.StageId;
#endif
        Narrative.EnterStage(room.StageId);
        room.gameObject.SetActive(true);
        if (!postFinal) ActiveFight.PlacePlayer(room.Offset);
        ActiveFight.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        SetView(room.Offset);
        chandelier = room.Prop;
        interventionOffered = false;
        roomEnteredAt = Time.time;
        opportunityReadyAt = Time.time + 1f;
        bool encounter = room.Kind == TowerRoom.RoomKind.Gun || room.Kind == TowerRoom.RoomKind.Blade
            || room.Kind == TowerRoom.RoomKind.DevelopmentFinal;
        room.SetExitOpen(!encounter && room.Kind != TowerRoom.RoomKind.Security && room.StageId != "floor99" && room.StageId != "office_intro" && room.StageId != "final_disposition");
        if (room.Kind == TowerRoom.RoomKind.Security)
        {
            if (room.SecurityTemplate == null || room.Receptionist == null)
                Debug.LogError("[S02] 50F requires Security and Receptionist bindings.", this);
            else Narrative.ConnectFloor50World(room.Receptionist.IsAlive);
        }
        if (encounter) ActiveFight.BeginRoomEncounter(attemptPrefab.Boss, room, true);
        if (room.StageId == "floor99") ActiveFight.BeginSecurityEncounter(room, true);
        if (room.Kind == TowerRoom.RoomKind.DevelopmentFinal)
        {
            var mode = Narrative.FreezeP3Mode();
            P3EntryFacts = Array.AsReadOnly(towerFacts.ToArray());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (developmentJumpRun && developmentP3Mode != DevelopmentP3Mode.Unspecified)
                mode = (TowerNarrativeState.Mode)developmentP3Mode; // Explicit test profile; never a normal route fact.
#endif
            P3DevelopmentMode = (DevelopmentP3Mode)mode;
            ActiveFight.Boss.ApplyFinalProfile(room.ProfileFor(mode));
        }
        ReceiveTowerRoomEntered();
        BeginNarrativeRoom();
        Physics2D.SyncTransforms();
    }

    private void ReceiveTowerRoomEntered()
    {
        var room = CurrentRoom;
        if (room == null || !towerEvents.Add(room.StageId + ".enter")) return;
        Narrative.ObserveExternalOutcome(room.StageId, "entered");
        Debug.Log($"[S02 room] {room.StageId}; prior prop facts={towerFacts.Count}. Unconnected content stays unknown.", this);
    }

    public bool RequestTowerExit(TowerRoom source)
    {
        if (!towerPrototypeEnabled || towerTerminal || State != FlowState.Combat || source == null || source != CurrentRoom
            || !source.ExitOpen || ActiveFight == null || ActiveFight.Player.IsDead || ActiveFight.NarrativePaused) return false;
        if (!towerEvents.Add(source.StageId + ".exit")) return false;
        Narrative.ObserveExternalOutcome(source.StageId, "exited");
        Debug.Log("[S02 exit] " + source.StageId, this);
        if (source.Kind == TowerRoom.RoomKind.DevelopmentDisposition) return false;
        EnterTowerRoom(towerRoomIndex + 1);
        return true;
    }

    private void TowerEncounterEnded()
    {
        if (towerTerminal || CurrentRoom == null || ActiveFight.Player.IsDead) return;
        var room = CurrentRoom;
        if (!towerEvents.Add(room.StageId + ".encounter_end")) return;
        CancelIntervention();
        room.SetExitOpen(true);
        Narrative.ObserveExternalOutcome(room.StageId, "encounter_ended");
        Debug.Log("[S02 encounter ended] " + room.StageId + "; same officer remains active.", this);
        if (room.Kind == TowerRoom.RoomKind.DevelopmentFinal) EnterTowerRoom(towerRoomIndex + 1);
        else if (room.StageId == "floor50" && Narrative.ReceptionistAlive == false && HasInnerAI)
            Say(Narrative.ReceptionistCausalActor == ProjectileActor.InnerAI ? "npc.floor50.after.dead_ai" : "npc.floor50.after.dead_player", null);
    }

    private void TickTowerRoom() { } // Authored interactions now start Security; no entry timer.

    private void RecordReceptionistDeath(TowerCharacter npc)
    {
        if (towerTerminal || State != FlowState.Combat || CurrentRoom == null || CurrentRoom.Receptionist != npc
            || ActiveFight.Player.IsDead || !towerEvents.Add("floor50.receptionist.death")) return;
        CancelStory();
        Narrative.CommitReceptionistDeath(npc.LethalCause, npc.CausalActor.Value, npc.PermissionTier);
        StartFloor50Security();
        towerFacts.Add(new TowerFact("floor50.receptionist", npc.LethalCause, npc.CausalActor.Value, npc.PermissionTier, false));
        Debug.Log($"[50F actual death] {npc.LethalCause}; actor={npc.CausalActor}; tier={npc.PermissionTier}", this);
    }

    private void TickTowerOpportunity()
    {
        var room = CurrentRoom;
        if (room == null || StoryUI.DialogueVisible || ActiveFight.NarrativePaused || chandelier == null || !chandelier.IsHanging || Time.time < opportunityReadyAt
            || Deployment.BrainPermissionTier <= 0 || interventionOffered) return;
        // A real, still-living threat is required in both rooms. NPC survival is not a trigger condition.
        if (room.Kind != TowerRoom.RoomKind.Gun && room.Kind != TowerRoom.RoomKind.Security) return;
        if (!ActiveFight.EncounterActive || ActiveFight.BossHealth == null || ActiveFight.BossHealth.IsDead) return;
        if (Mathf.Abs(ActiveFight.Player.transform.position.x - room.Offset.x) > 12f) return;
        interventionOffered = true;
        if (!towerEvents.Add(room.PropEventInstanceId + ".offered")) return;
        Debug.Log($"[S02 opportunity] {room.PropEventInstanceId}; DEV={room.DevelopmentOpportunity}", this);
        OfferChandelierIntervention();
    }

    private void RecordTowerPropOutcome(ChandelierEncounter source, string outcome)
    {
        var room = CurrentRoom;
        if (towerTerminal || State != FlowState.Combat || room == null || source != room.Prop || ActiveFight.Player.IsDead) return;
        if (!towerEvents.Add(room.PropEventInstanceId + "." + outcome)) return;
        towerFacts.Add(new TowerFact(room.PropEventInstanceId, outcome, source.LastActor, source.LastPermissionTier, false));
        Narrative.ObserveExternalOutcome(room.PropEventInstanceId, outcome);
        if (room.Kind == TowerRoom.RoomKind.Security && outcome == "released")
            Narrative.CommitFloor50ChandelierRelease(source.LastActor, source.LastPermissionTier);
        Debug.Log($"[S02 actual fact] {room.PropEventInstanceId}/{outcome}; actor={source.LastActor}; tier={source.LastPermissionTier}; DEV={room.DevelopmentOpportunity}", this);
    }

#if UNITY_EDITOR
    [ContextMenu("S02 DEV/Complete current encounter (flow proof only)")]
    public void DevelopmentCompleteEncounter()
    {
        if (!Application.isPlaying || !towerPrototypeEnabled || ActiveFight == null || !ActiveFight.EncounterActive) return;
        developmentJumpRun = true;
        Debug.Log("[S02 DEV] Controlled encounter completion; tagged test, no Archive write.", this);
        ActiveFight.BossHealth.TakeDamage(int.MaxValue, ActiveFight.Player.gameObject);
    }

    [ContextMenu("S02 DEV/Re-deliver current room entry")]
    public void DevelopmentRepeatRoomEvent()
    {
        ReceiveTowerRoomEntered();
        interventionOffered = false;
        TickTowerOpportunity(); // Once key must reject it even after the local offered flag is cleared.
    }
#endif
}
