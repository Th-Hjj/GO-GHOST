using UnityEngine;

public sealed partial class GameSessionFlow
{
    [Header("S01 development prototype only")]
    [SerializeField] private bool permissionPrototypeEnabled;
    [Range(0, 3)] [SerializeField] private int developmentPermissionTier = 2;
    [SerializeField] private ChandelierEncounter chandelier;
    [Tooltip("TEST bounded T3 attempt; no unlimited retries when the gun or target is unavailable.")]
    [Min(.1f)] [SerializeField] private float autonomousWindowSeconds = 3f;
    private bool interventionOffered;
    private bool interventionActive;
    private PlayerInputReader interventionReader;

    private void Update()
    {
        if (State != FlowState.Combat || ActiveFight == null || Deployment == null) return;
        if (towerPrototypeEnabled) { TickNarrative(); TickTowerRoom(); }
        if (interventionActive)
        {
            if (ActiveFight.Player.IsDead || chandelier == null || !chandelier.CanRelease || (towerPrototypeEnabled && (!ActiveFight.EncounterActive || ActiveFight.BossHealth == null || ActiveFight.BossHealth.IsDead)))
                CancelIntervention();
            else if (!ActiveFight.NarrativePaused && !ActiveFight.Player.GetComponent<PlayerInputReader>().HasTemporaryAimOverride)
                interventionActive = false;
        }
        if (towerPrototypeEnabled) { TickTowerOpportunity(); return; }
        // One authored opportunity on entering the existing arena, not a planner or repeating tutorial.
        if (interventionOffered || Deployment.BrainPermissionTier < 0 || StateAge < 1f) return;
        interventionOffered = true;
        OfferChandelierIntervention();
    }

    private void OfferChandelierIntervention()
    {
        int tier = Deployment.BrainPermissionTier;
        if (tier == 0 || chandelier == null || !chandelier.CanRelease) return;
        string sequence = tier <= 2 || TowerStageId == "boss_p1" ? "intervention.chandelier.advice" : "intervention.chandelier.full";
        if (!GetComponent<MissionUI>().ShowNarrative(sequence, FinishInterventionDialogue)) return;
        interventionActive = true;
        ActiveFight.SetNarrativePaused(true);
    }

    private void FinishInterventionDialogue()
    {
        if (!interventionActive || State != FlowState.Combat || ActiveFight == null) { CancelIntervention(); return; }
        ActiveFight.SetNarrativePaused(false);
        int tier = Deployment.BrainPermissionTier;
        if (tier < 3 || chandelier == null || !chandelier.CanRelease) { CancelIntervention(); return; }
        var reader = ActiveFight.Player.GetComponent<PlayerInputReader>();
        interventionReader = reader;
        reader.OnAimOverrideEnded += InterventionEnded;
        if (!reader.BeginAimOverride(chandelier.AimTarget, autonomousWindowSeconds, true, () => chandelier != null && chandelier.CanRelease))
            CancelIntervention();
    }

    private void InterventionEnded(PlayerInputReader.AimEndReason reason, bool requestedFire)
    {
        if (interventionReader != null) interventionReader.OnAimOverrideEnded -= InterventionEnded;
        interventionReader = null;
        // T2 no longer owns aim. Thomas deferred the replacement 50F observation rule;
        // keep the P3 selector unchanged and do not fabricate a completed veto.
        interventionActive = false;
    }

    private void CancelIntervention()
    {
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

    private void OnApplicationFocus(bool focused) { if (!focused && interventionActive) CancelIntervention(); }
    private void OnDisable() { CancelIntervention(); ClearTowerWorld(); }
}
