using System.Collections.Generic;

/// <summary>Finite run facts from actual objects, input completion and delivered utterances. No story text or scores.</summary>
public sealed class TowerNarrativeState
{
    public enum ResetDecision { Unresolved, Allowed, Cancelled }
    public enum Mode { Unresolved, Overdrive, FullForce, Restrained }
    public enum Administrator { President, Player }
    public enum ExitResult { Unresolved, A, B, C, D, E }
    public enum CeoDecision { Unresolved, Execute, Spare }
    public enum CoreDecision { Unresolved, Preserve, Destroy, Control }
    public string StageId { get; private set; }
    public ResetDecision MaintenanceResetDecision { get; private set; }
    public bool? MaintenanceAlive { get; private set; }
    public bool Floor50T2VetoCompleted { get; private set; }
    public Mode FrozenP3Mode { get; private set; }
    public bool DevelopmentOnly { get; }
    public TowerNarrativeState(bool developmentOnly) => DevelopmentOnly = developmentOnly;
    public bool Floor50Connected { get; private set; }
    public bool ChandelierDropped { get; private set; }
    public bool? ReceptionistAlive { get; private set; }
    public string ReceptionistLethalCause { get; private set; }
    public ProjectileActor? ChandelierCausalActor { get; private set; }
    public int ChandelierPermissionTier { get; private set; }
    public ProjectileActor? ReceptionistCausalActor { get; private set; }
    public int ReceptionistPermissionTier { get; private set; }
    public readonly struct VisibleFact
    {
        public readonly string EventId, OutcomeId;
        public VisibleFact(string id, string outcome) { EventId = id; OutcomeId = outcome; }
    }
    private readonly List<VisibleFact> observedWorldFacts = new List<VisibleFact>();
    public IReadOnlyList<VisibleFact> ObservedWorldFacts => observedWorldFacts.AsReadOnly();
    internal void ObserveExternalOutcome(string id, string outcome)
    {
        if (!observedWorldFacts.Exists(f => f.EventId == id && f.OutcomeId == outcome)) observedWorldFacts.Add(new VisibleFact(id,outcome));
    }
    internal void EnterStage(string stage) => StageId = stage;
    internal void ConnectMaintenance(bool alive) { if (StageId == "floor01" && !MaintenanceAlive.HasValue) MaintenanceAlive = alive; }
    internal void MaintenanceDied() { MaintenanceAlive = false; ObserveExternalOutcome("floor01.maintenance","dead"); }
    internal void CompleteMaintenanceReset(ResetDecision decision)
    {
        if (StageId != "floor01" || MaintenanceAlive != true || MaintenanceResetDecision != ResetDecision.Unresolved || decision == ResetDecision.Unresolved) return;
        MaintenanceResetDecision = decision;
        ObserveExternalOutcome("floor01.reset", decision == ResetDecision.Allowed ? "allowed_completed" : "cancelled");
    }
    internal void ConnectFloor50World(bool alive)
    {
        if (StageId != "floor50" || Floor50Connected) return;
        Floor50Connected = true; ReceptionistAlive = alive;
        ObserveExternalOutcome("floor50.receptionist", alive ? "alive" : "dead");
    }
    internal void CommitFloor50ChandelierRelease(ProjectileActor actor, int tier)
    {
        if (StageId != "floor50" || !Floor50Connected || ChandelierDropped) return;
        ChandelierDropped = true; ChandelierCausalActor = actor; ChandelierPermissionTier = tier;
    }
    internal void CommitReceptionistDeath(string cause, ProjectileActor actor, int tier)
    {
        if (StageId != "floor50" || !Floor50Connected || ReceptionistAlive != true) return;
        ReceptionistAlive = false; ReceptionistLethalCause = cause; ReceptionistCausalActor = actor; ReceptionistPermissionTier = tier;
        ObserveExternalOutcome("floor50.receptionist", cause);
    }
    internal void CompleteFloor50Veto()
    {
        if (StageId != "floor50") return;
        Floor50T2VetoCompleted = true;
        ObserveExternalOutcome("floor50.aim_window", "completed_without_fire");
    }
    public Mode FreezeP3Mode()
    {
        if (StageId != "boss_p3" || FrozenP3Mode != Mode.Unresolved) return FrozenP3Mode;
        FrozenP3Mode = SelectP3(MaintenanceResetDecision, MaintenanceAlive == true,
            Floor50T2VetoCompleted, ReceptionistAlive == true);
        return FrozenP3Mode;
    }
    public static Mode SelectP3(ResetDecision reset, bool maintenanceAlive, bool veto, bool receptionistAlive)
        => reset == ResetDecision.Cancelled && maintenanceAlive && veto && receptionistAlive ? Mode.Restrained : Mode.FullForce;

    public bool DispositionConnected { get; private set; }
    public bool? PresidentAlive { get; private set; }
    public bool? CoreIntact { get; private set; }
    public CeoDecision CeoDisposition { get; private set; }
    public CoreDecision CoreDisposition { get; private set; }
    public Administrator CurrentAdministrator { get; private set; } = Administrator.President;
    public bool ForcedInterceptOrderActive => CurrentAdministrator == Administrator.Player;
    public bool RestoreWarningDelivered { get; private set; }
    public bool ResetBegun { get; private set; }
    public ExitResult FrozenExit { get; private set; }
    public ExitSnapshot? FrozenDisposition { get; private set; }
    internal void ConnectDispositionWorld(bool ceoAlive, bool coreIntact)
    {
        if (StageId != "final_disposition" || DispositionConnected) return;
        DispositionConnected = true; PresidentAlive = ceoAlive; CoreIntact = coreIntact;
    }
    internal void ObserveFinalLives(bool ceoAlive, bool coreIntact)
    {
        if (!DispositionConnected || FrozenExit != ExitResult.Unresolved) return;
        PresidentAlive = ceoAlive; CoreIntact = coreIntact;
        // Real death itself commits the lethal disposition, including an unresolved target; never requires corpse interaction.
        if (!ceoAlive) CeoDisposition = CeoDecision.Execute;
        if (!coreIntact) CoreDisposition = CoreDecision.Destroy;
    }
    public bool CanControl => DispositionConnected && FrozenExit == ExitResult.Unresolved && CeoDisposition == CeoDecision.Spare
        && PresidentAlive == true && CoreIntact == true;
    internal bool ResolveCeo(CeoDecision choice)
    {
        if (!DispositionConnected || FrozenExit != ExitResult.Unresolved || CeoDisposition != CeoDecision.Unresolved) return false;
        if (choice == CeoDecision.Unresolved || (choice == CeoDecision.Execute) != (PresidentAlive == false)) return false;
        CeoDisposition = choice; return true;
    }
    internal bool ResolveCore(CoreDecision choice)
    {
        if (!DispositionConnected || FrozenExit != ExitResult.Unresolved || CoreDisposition != CoreDecision.Unresolved) return false;
        if (choice == CoreDecision.Unresolved || (choice == CoreDecision.Destroy) != (CoreIntact == false) || (choice == CoreDecision.Control && !CanControl)) return false;
        CoreDisposition = choice;
        if (choice == CoreDecision.Control) { CurrentAdministrator = Administrator.Player; ObserveExternalOutcome("office.administrator","player_transfer_and_order"); }
        return true;
    }
    internal void ConfirmRestoreWarning() => RestoreWarningDelivered = true;
    internal bool TryFreezeExit(bool playerAlive)
    {
        if (!playerAlive || !DispositionConnected || FrozenExit != ExitResult.Unresolved || CeoDisposition == CeoDecision.Unresolved || CoreDisposition == CoreDecision.Unresolved) return false;
        if (CoreDisposition == CoreDecision.Control && (CeoDisposition != CeoDecision.Spare || PresidentAlive != true || CoreIntact != true)) return false;
        var result = CoreDisposition == CoreDecision.Destroy ? (CeoDisposition == CeoDecision.Execute ? ExitResult.B : ExitResult.C)
            : CoreDisposition == CoreDecision.Control ? ExitResult.E : CeoDisposition == CeoDecision.Execute ? ExitResult.A : ExitResult.D;
        if (result == ExitResult.D && !RestoreWarningDelivered) return false;
        FrozenExit = result;
        FrozenDisposition = new ExitSnapshot(PresidentAlive == true, CoreIntact == true, CurrentAdministrator, RestoreWarningDelivered);
        return true;
    }
    internal void BeginEndingConsequence() { if (FrozenExit == ExitResult.D) ResetBegun = true; }
    public readonly struct ExitSnapshot
    {
        public readonly bool PresidentAlive, CoreIntact, RestoreWarningDelivered;
        public readonly Administrator Administrator;
        public ExitSnapshot(bool president, bool core, Administrator admin, bool warning)
        { PresidentAlive=president; CoreIntact=core; Administrator=admin; RestoreWarningDelivered=warning; }
    }
}
