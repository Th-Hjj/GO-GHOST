using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class GameSessionFlow
{
    [Header("S02 DEV route: unfinished content never grants a formal ending")]
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
        foreach (var room in towerRooms)
        {
            if (room == null) continue;
            room.Bind(this);
            if (room.Prop != null) room.Prop.OnWorldOutcome += RecordTowerPropOutcome;
            if (room.Receptionist != null) room.Receptionist.OnDied += RecordReceptionistDeath;
        }
    }

    private void ResetTowerRun()
    {
        if (!towerPrototypeEnabled) return;
        ClearTowerWorld();
        towerTerminal = false;
        DevelopmentCompletionCount = 0;
        LastDevelopmentCompletionFacts = null;
    }

    private void ClearTowerWorld()
    {
        if (!towerPrototypeEnabled) return;
        CancelIntervention();
        towerEvents.Clear();
        towerFacts.Clear();
        Narrative = new TowerNarrativeState(true);
        P3EntryFacts = null;
        P3DevelopmentMode = DevelopmentP3Mode.Unspecified;
        towerRoomIndex = -1;
        chandelier = null;
        foreach (var room in towerRooms)
        {
            if (room == null) continue;
            room.Prop?.ResetEncounter();
            room.Receptionist?.ResetEncounter();
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
        EnterTowerRoom(initialRoomIndex);
        return true;
    }

    private void EnterTowerRoom(int index)
    {
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
        ActiveFight.PlacePlayer(room.Offset);
        ActiveFight.Player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        SetView(room.Offset);
        chandelier = room.Prop;
        interventionOffered = false;
        roomEnteredAt = Time.time;
        opportunityReadyAt = Time.time + 1f;
        bool encounter = room.Kind == TowerRoom.RoomKind.Gun || room.Kind == TowerRoom.RoomKind.Blade
            || room.Kind == TowerRoom.RoomKind.DevelopmentFinal;
        room.SetExitOpen(!encounter && room.Kind != TowerRoom.RoomKind.Security);
        if (room.Kind == TowerRoom.RoomKind.Security)
        {
            if (room.SecurityTemplate == null || room.Receptionist == null)
                Debug.LogError("[S02] 50F requires Security and Receptionist bindings.", this);
            else Narrative.ConnectFloor50World(room.Receptionist.IsAlive);
        }
        if (encounter) ActiveFight.BeginRoomEncounter(attemptPrefab.Boss, room);
        if (room.Kind == TowerRoom.RoomKind.DevelopmentFinal)
        {
            Narrative.FreezeP3Mode(); // Approved OPEN-02 logic; the actual P3 actor is still a DEV dummy.
            P3EntryFacts = Array.AsReadOnly(towerFacts.ToArray());
            P3DevelopmentMode = developmentP3Mode; // DEV selection only; no inferred formal mode fact.
        }
        ReceiveTowerRoomEntered();
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
        if (source.Kind == TowerRoom.RoomKind.DevelopmentDisposition)
        {
            // One DEV completion transaction, deliberately outside the old percentage/Archive success route.
            towerTerminal = true;
            LastDevelopmentCompletionFacts = Array.AsReadOnly(towerFacts.ToArray());
            DevelopmentCompletionCount++;
            Debug.Log($"[S02 DEV ending] Completion {DevelopmentCompletionCount}; frozen facts={towerFacts.Count}; no formal ending or Archive write.", this);
            DisposeAttempt();
            ClearTowerWorld();
            Draft?.Lock();
            Draft = null;
            Deployment = null;
            Officer = null;
            SetState(FlowState.MainMenu);
        }
        else EnterTowerRoom(towerRoomIndex + 1);
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
    }

    private void TickTowerRoom()
    {
        var room = CurrentRoom;
        if (room == null || room.Kind != TowerRoom.RoomKind.Security || towerTerminal || ActiveFight.Player.IsDead
            || room.SecurityTemplate == null || room.Receptionist == null
            || ActiveFight.NarrativePaused || Time.time - roomEnteredAt < room.SecurityDelay) return;
        if (!towerEvents.Add("floor50.security.started")) return;
        ActiveFight.BeginSecurityEncounter(room);
        opportunityReadyAt = Time.time + 1f;
        Narrative.ObserveExternalOutcome("floor50.security", "activated");
    }

    private void RecordReceptionistDeath(Floor50Receptionist npc)
    {
        if (towerTerminal || State != FlowState.Combat || CurrentRoom == null || CurrentRoom.Receptionist != npc
            || ActiveFight.Player.IsDead || !towerEvents.Add("floor50.receptionist.death")) return;
        Narrative.CommitReceptionistDeath(npc.LethalCause, npc.CausalActor.Value, npc.PermissionTier);
        towerFacts.Add(new TowerFact("floor50.receptionist", npc.LethalCause, npc.CausalActor.Value, npc.PermissionTier, false));
        Debug.Log($"[50F actual death] {npc.LethalCause}; actor={npc.CausalActor}; tier={npc.PermissionTier}", this);
    }

    private void TickTowerOpportunity()
    {
        var room = CurrentRoom;
        if (room == null || ActiveFight.NarrativePaused || chandelier == null || !chandelier.IsHanging || Time.time < opportunityReadyAt
            || Deployment.BrainPermissionTier <= 0 || interventionOffered) return;
        // A real, still-living threat is required in both rooms. NPC survival is not a trigger condition.
        if (room.Kind != TowerRoom.RoomKind.Gun && room.Kind != TowerRoom.RoomKind.Security) return;
        if (!ActiveFight.EncounterActive || ActiveFight.BossHealth == null || ActiveFight.BossHealth.IsDead) return;
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
        Debug.Log("[S02 DEV] Controlled encounter completion; not a gameplay victory or narrative fact.", this);
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
