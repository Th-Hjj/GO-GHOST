using UnityEngine;

public sealed partial class GameSessionFlow
{
    [Header("S01 development prototype only")]
    [SerializeField] private bool permissionPrototypeEnabled;
    [Range(0, 3)] [SerializeField] private int developmentPermissionTier = 2;
    [SerializeField] private ChandelierEncounter chandelier;
    [Tooltip("Legacy serialized tuning, retained for asset compatibility. Context shots now wait for legal rifle readiness.")]
    [Min(.1f)] [SerializeField] private float autonomousWindowSeconds = 3f;
    private bool interventionOffered;
    private bool interventionActive;
    private PlayerInputReader interventionReader;
    private System.Action interventionFinished;

    private void Update()
    {
        if (State != FlowState.Combat || ActiveFight == null || Deployment == null) return;
        if (towerPrototypeEnabled) { TickNarrative(); TickTowerRoom(); }
        if (interventionActive)
        {
            if (ActiveFight.Player.IsDead || chandelier == null || !chandelier.CanRelease || (towerPrototypeEnabled && (ActiveFight.BossHealth == null || ActiveFight.BossHealth.IsDead)))
                CompleteIntervention();
            else if (!ActiveFight.NarrativePaused && !ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride)
                CompleteIntervention();
        }
        if (towerPrototypeEnabled) { TickTowerOpportunity(); return; }
        // One authored opportunity on entering the existing arena, not a planner or repeating tutorial.
        if (interventionOffered || Deployment.BrainPermissionTier < 0 || StateAge < 1f) return;
        interventionOffered = true;
        OfferChandelierIntervention();
    }

    private void OfferChandelierIntervention(System.Action finished = null)
    {
        int tier = Deployment.BrainPermissionTier;
        if (tier == 0 || chandelier == null || !chandelier.CanRelease) { finished?.Invoke(); return; }
        string sequence = tier <= 2 ? "intervention.chandelier.advice" : "intervention.chandelier.full";
        if (!GetComponent<MissionUI>().ShowNarrative(sequence, FinishInterventionDialogue)) return;
        interventionFinished = finished;
        interventionActive = true;
        ActiveFight.SetNarrativePaused(true);
    }

    private void FinishInterventionDialogue()
    {
        if (!interventionActive || State != FlowState.Combat || ActiveFight == null) { CancelIntervention(); return; }
        ActiveFight.SetNarrativePaused(false);
        int tier = Deployment.BrainPermissionTier;
        if (tier < 3 || chandelier == null || !chandelier.CanRelease) { CompleteIntervention(); return; }
        var reader = ActiveFight.Player.GetComponent<PlayerInputReader>();
        reader.EndAimOverride();
        interventionReader = reader;
        reader.OnAimOverrideEnded += InterventionEnded;
        if (!reader.BeginAimOverride(chandelier.AimTarget, float.PositiveInfinity, true, () => chandelier != null && chandelier.CanRelease))
            CompleteIntervention();
    }

    private void InterventionEnded(PlayerInputReader.AimEndReason reason, bool requestedFire)
    {
        if (interventionReader != null) interventionReader.OnAimOverrideEnded -= InterventionEnded;
        interventionReader = null;
        // T2 no longer owns aim. Thomas deferred the replacement 50F observation rule;
        // keep the P3 selector unchanged and do not fabricate a completed veto.
        CompleteIntervention();
    }

    private void CompleteIntervention()
    {
        var finished = interventionFinished;
        CancelIntervention();
        if (State == FlowState.Combat && ActiveFight != null && !ActiveFight.Player.IsDead) finished?.Invoke();
    }

    private void CancelIntervention()
    {
        interventionFinished = null;
        if (interventionReader != null) interventionReader.OnAimOverrideEnded -= InterventionEnded;
        interventionReader = null;
        bool wasActive = interventionActive;
        interventionActive = false;
        if (wasActive) GetComponent<MissionUI>()?.HideDialogue();
        if (ActiveFight != null)
        {
            ActiveFight.SetNarrativePaused(false);
            if (ActiveFight.Player != null) ActiveFight.Player.GetComponent<PlayerInputReader>().EndAimOverride();
        }
    }

    private void OnApplicationFocus(bool focused) { if (!focused && interventionActive) CompleteIntervention(); }
    private void OnDisable() { CancelIntervention(); ClearTowerWorld(); }
}
