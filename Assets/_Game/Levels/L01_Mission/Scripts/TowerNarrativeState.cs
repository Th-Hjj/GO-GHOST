/// <summary>Approved OPEN-01/02/03 interfaces only. Callbacks acknowledge actual world changes or delivered reports.
/// S02 has no NPC/terminal adapters: unconnected rooms remain Unknown and cannot produce a formal ending.</summary>
public sealed class TowerNarrativeState
{
    public enum FloorAction { None, Interrupt, Confirm, Leave }
    public enum FloorResult { Unconnected, Pending, Preserved, Restored }
    public enum FloorReport { Unknown, Preserved, RestoredConfirmed, LeftUnattended }
    public enum Threat { None, Erase, Control }
    public enum Mode { Unresolved, Overdrive, FullForce, Restrained }
    public enum Administrator { President, Player }
    // Working interface labels only. These are not PoliceArchive IDs or authored ending presentations.
    public enum ExitResult { Unresolved, A, B, C, D, E }

    public string StageId { get; private set; }
    public FloorAction Action { get; private set; }
    public FloorResult Result { get; private set; }
    public ProjectileActor? FloorActor { get; private set; }
    public FloorReport KnownFloor01 { get; private set; }
    public bool CoexistenceHeard { get; private set; }
    public Threat ThreatHeard { get; private set; }
    public Mode FrozenP3Mode { get; private set; }
    public bool DispositionConnected { get; private set; }
    public bool? PresidentAlive { get; private set; }
    public bool? CoreIntact { get; private set; }
    public Administrator? CurrentAdministrator { get; private set; }
    // Cannot represent a half transfer: administrator and command authority are one committed state.
    public bool ForcedInterceptOrderActive => CurrentAdministrator == Administrator.Player;
    public bool TransferTermsDelivered { get; private set; }
    public bool RestoreWarningDelivered { get; private set; }
    public ExitResult FrozenExit { get; private set; }
    public ExitSnapshot? FrozenDisposition { get; private set; }
    public bool DevelopmentOnly { get; }
    public TowerNarrativeState(bool developmentOnly) => DevelopmentOnly = developmentOnly;

    private bool floor01Departed;
    internal void EnterStage(string stage)
    {
        if (StageId == "floor01" && stage != "floor01") floor01Departed = true;
        StageId = stage;
    }

    // OPEN-01 adapter boundary: only the external recovery module is a damage target.
    // No maintenance-body or terminal-destruction outcome exists in this interface.
    public bool ConnectFloor01Module()
    {
        if (StageId != "floor01" || Result != FloorResult.Unconnected) return false;
        Result = FloorResult.Pending;
        return true;
    }

    public bool CommitFloor01Outcome(FloorAction action, ProjectileActor actor)
    {
        if (Result != FloorResult.Pending || (action != FloorAction.Interrupt && action != FloorAction.Confirm && action != FloorAction.Leave)) return false;
        // A scheduled restoration may complete after departure; acknowledgement still needs the actual device result.
        if (action == FloorAction.Leave ? !floor01Departed : StageId != "floor01") return false;
        if (actor != (action == FloorAction.Leave ? ProjectileActor.Environment : ProjectileActor.Player)) return false;
        Action = action;
        Result = action == FloorAction.Interrupt ? FloorResult.Preserved : FloorResult.Restored;
        FloorActor = actor;
        return true;
    }

    public bool Floor01ReportDelivered(FloorReport report)
    {
        if (StageId != "boss_p2" || KnownFloor01 != FloorReport.Unknown) return false;
        bool matches = report == FloorReport.Preserved && Action == FloorAction.Interrupt && Result == FloorResult.Preserved
            || report == FloorReport.RestoredConfirmed && Action == FloorAction.Confirm && Result == FloorResult.Restored
            || report == FloorReport.LeftUnattended && Action == FloorAction.Leave && Result == FloorResult.Restored;
        if (!matches) return false;
        KnownFloor01 = report;
        return true;
    }

    public bool CoexistenceReportDelivered()
    {
        if (StageId != "boss_p2" || CoexistenceHeard) return false;
        CoexistenceHeard = true;
        return true;
    }

    public bool ThreatReportDelivered(Threat threat, bool refusalWasDelivered)
    {
        if (StageId != "office_intro" || ThreatHeard != Threat.None || threat == Threat.None
            || (threat == Threat.Control && !refusalWasDelivered)) return false;
        ThreatHeard = threat;
        return true;
    }

    public Mode FreezeP3Mode()
    {
        if (StageId != "boss_p3" || FrozenP3Mode != Mode.Unresolved) return FrozenP3Mode;
        FrozenP3Mode = ThreatHeard != Threat.None ? Mode.Overdrive
            : Action == FloorAction.Interrupt && Result == FloorResult.Preserved
                && KnownFloor01 == FloorReport.Preserved && CoexistenceHeard ? Mode.Restrained : Mode.FullForce;
        return FrozenP3Mode;
    }

    // Invoked by actual post-P3 object binding, never merely because the room was entered.
    public bool ConnectDispositionWorld(bool presidentAlive, bool coreIntact)
    {
        if (StageId != "final_disposition" || DispositionConnected) return false;
        DispositionConnected = true;
        PresidentAlive = presidentAlive;
        CoreIntact = coreIntact;
        CurrentAdministrator = Administrator.President;
        return true;
    }

    private bool CanChangeDisposition => StageId == "final_disposition" && DispositionConnected && FrozenExit == ExitResult.Unresolved;
    public bool CommitPresidentDeath()
    {
        if (!CanChangeDisposition || PresidentAlive != true) return false;
        PresidentAlive = false;
        return true;
    }
    public bool CommitCoreDestruction()
    {
        if (!CanChangeDisposition || CoreIntact != true) return false;
        CoreIntact = false;
        return true;
    }
    private bool CanTransfer => CanChangeDisposition && PresidentAlive == true && CoreIntact == true
        && CurrentAdministrator == Administrator.President;
    public bool TransferOfferDelivered()
    {
        if (!CanTransfer) return false;
        TransferTermsDelivered = true; // Includes the actual refusal and terms, not merely opening a panel.
        return true;
    }
    public void CancelTransfer() => TransferTermsDelivered = false;
    public bool CommitPlayerTransferAndOrder(bool playerAlive)
    {
        if (!playerAlive || !CanTransfer || !TransferTermsDelivered) return false;
        CurrentAdministrator = Administrator.Player;
        TransferTermsDelivered = false;
        return true;
    }
    public bool RestoreRiskDelivered()
    {
        if (!CanTransfer) return false;
        RestoreWarningDelivered = true;
        return true;
    }

    // A missing D warning returns to the world. Delivery alone never exits: caller needs a new exit action.
    public bool TryFreezeExit(bool playerAlive)
    {
        if (!playerAlive || !CanChangeDisposition) return false;
        ExitResult result = CoreIntact == false ? (PresidentAlive == false ? ExitResult.B : ExitResult.C)
            : CurrentAdministrator == Administrator.Player ? ExitResult.E
            : PresidentAlive == false ? ExitResult.A : ExitResult.D;
        if (result == ExitResult.D && !RestoreWarningDelivered) return false;
        FrozenDisposition = new ExitSnapshot(PresidentAlive.Value, CoreIntact.Value, CurrentAdministrator.Value, RestoreWarningDelivered);
        FrozenExit = result;
        return true;
    }

    public readonly struct ExitSnapshot
    {
        public readonly bool PresidentAlive, CoreIntact, RestoreWarningDelivered;
        public readonly Administrator Administrator;
        public ExitSnapshot(bool president, bool core, Administrator admin, bool warning)
        { PresidentAlive = president; CoreIntact = core; Administrator = admin; RestoreWarningDelivered = warning; }
    }
}
