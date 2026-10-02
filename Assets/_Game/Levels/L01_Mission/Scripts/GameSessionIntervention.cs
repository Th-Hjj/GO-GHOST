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

    private void Update()
    {
        if (State != FlowState.Combat || ActiveFight == null || Deployment == null) return;
        if (interventionActive)
        {
            if (ActiveFight.Player.IsDead || chandelier == null || !chandelier.IsHanging)
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
        if (tier == 0 || chandelier == null || !chandelier.IsHanging) return;
        string sequence = tier == 1 ? "intervention.chandelier.advice"
            : tier == 2 ? "intervention.chandelier.aim" : "intervention.chandelier.full";
        if (!GetComponent<MissionUI>().ShowNarrative(sequence, FinishInterventionDialogue)) return;
        interventionActive = true;
        ActiveFight.SetNarrativePaused(true);
    }

    private void FinishInterventionDialogue()
    {
        if (!interventionActive || State != FlowState.Combat || ActiveFight == null) { CancelIntervention(); return; }
        ActiveFight.SetNarrativePaused(false);
        int tier = Deployment.BrainPermissionTier;
        if (tier < 2 || chandelier == null || !chandelier.IsHanging) { CancelIntervention(); return; }
        var reader = ActiveFight.Player.GetComponent<PlayerInputReader>();
        if (!reader.BeginAimOverride(chandelier.AimTarget, tier == 2 ? 3f : autonomousWindowSeconds, tier == 3))
            CancelIntervention();
    }

    private void CancelIntervention()
    {
        interventionActive = false;
        GetComponent<MissionUI>()?.HideDialogue();
        if (ActiveFight != null)
        {
            ActiveFight.SetNarrativePaused(false);
            if (ActiveFight.Player != null) ActiveFight.Player.GetComponent<PlayerInputReader>().EndAimOverride();
        }
    }

    private void OnApplicationFocus(bool focused) { if (!focused) CancelIntervention(); }
    private void OnDisable() { CancelIntervention(); ClearTowerWorld(); }
}
