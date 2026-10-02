using System.Collections;
using UnityEngine;

public sealed partial class GameSessionFlow
{
    private string towerEndingId;
    public bool HasTowerEnding => !string.IsNullOrEmpty(towerEndingId);
    public string TowerEndingId => towerEndingId;
    public int TowerCompletionCount { get; private set; }

    private IEnumerator RevealFinalCarrier()
    {
        ActiveFight.SetNarrativePaused(true);
        var actor = ActiveFight.Boss.transform;
        Vector3 final = CurrentRoom.transform.position + new Vector3(0,1,0);
        actor.position = final + Vector3.up * 5;
        yield return MovePresentation(actor, final, .8f);
        ActiveFight.SetNarrativePaused(false);
        ActiveFight.StartPreparedEncounter();
    }

    private TowerCharacter FinalInteractionFocus()
    {
        Narrative.ObserveFinalLives(president.IsAlive, continuingCore.IsAlive);
        TowerCharacter best = null;
        float distance = 2.4f;
        foreach (var npc in new[]{president,continuingCore})
        {
            bool unresolved = npc == president ? Narrative.CeoDisposition == TowerNarrativeState.CeoDecision.Unresolved
                : Narrative.CoreDisposition == TowerNarrativeState.CoreDecision.Unresolved;
            if (!unresolved) continue;
            float d = Vector2.Distance(ActiveFight.Player.transform.position, npc.transform.position);
            if (d < distance) { best = npc; distance = d; }
        }
        return best;
    }

    private void InteractFinal(TowerCharacter target)
    {
        Narrative.ObserveFinalLives(president.IsAlive, continuingCore.IsAlive);
        if (!target.IsAlive)
        {
            // F acknowledges an already irreversible physical act. No resurrection, corpse speech or impossible choices.
            if (target == president) Narrative.ResolveCeo(TowerNarrativeState.CeoDecision.Execute);
            else Narrative.ResolveCore(TowerNarrativeState.CoreDecision.Destroy);
            CompleteTowerIfResolved();
            return;
        }
        string[] ids = target == president ? new[]{"office.final.ceo.kill","office.final.ceo.spare"}
            : Narrative.CanControl ? new[]{"office.final.puppeteer.preserve","office.final.puppeteer.destroy","office.final.puppeteer.control"}
            : new[]{"office.final.puppeteer.preserve","office.final.puppeteer.destroy"};
        Choose(ids, id =>
        {
            if (towerTerminal || ActiveFight == null || ActiveFight.Player.IsDead) return;
            if (FinalChoiceCue(id) != null) Narrative.ConfirmRestoreWarning();
            if (id == "office.final.ceo.kill" || id == "office.final.puppeteer.destroy")
                target.TakeDamage(int.MaxValue, ActiveFight.Player.gameObject); // Explicit finisher damage, never claims a projectile hit.
            Narrative.ObserveFinalLives(president.IsAlive, continuingCore.IsAlive);
            if (target == president) Narrative.ResolveCeo(target.IsAlive ? TowerNarrativeState.CeoDecision.Spare : TowerNarrativeState.CeoDecision.Execute);
            else Narrative.ResolveCore(!target.IsAlive ? TowerNarrativeState.CoreDecision.Destroy
                : id == "office.final.puppeteer.control" ? TowerNarrativeState.CoreDecision.Control : TowerNarrativeState.CoreDecision.Preserve);
            CompleteTowerIfResolved();
        }, false, FinalChoiceCue);
    }

    private string FinalChoiceCue(string id)
    {
        if (!president.IsAlive || !continuingCore.IsAlive) return null;
        return (id == "office.final.ceo.spare" && Narrative.CoreDisposition == TowerNarrativeState.CoreDecision.Preserve)
            || (id == "office.final.puppeteer.preserve" && Narrative.CeoDisposition == TowerNarrativeState.CeoDecision.Spare)
            ? "office.final.restore_queued" : null;
    }

    private void CompleteTowerIfResolved()
    {
        if (towerTerminal || !Narrative.TryFreezeExit(!ActiveFight.Player.IsDead)) return;
        towerTerminal = true;
        TowerCompletionCount++;
        towerEndingId = "ending." + Narrative.FrozenExit;
        CancelStory(); CancelIntervention();
        ActiveFight.CompleteTowerMission();
        Victory = new VictoryRecord(Deployment);
        LastRunRecord = new PoliceRunRecord(Officer, Deployment, true, ActiveFight.CombatDuration,
            ActiveFight.ResultBossHealth, ActiveFight.ResultPlayerHealth, ActiveFight.AccuracyPercent, towerEndingId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!developmentJumpRun)
#endif
        if (!PoliceArchive.RecordCompletedRun(LastRunRecord, out string error)) Debug.LogWarning("Police Archive run save failed: " + error, this);
        SetState(FlowState.Result);
    }
    private void PresentTowerEnding()
    {
        Narrative.BeginEndingConsequence();
        if (!StoryUI.ShowNarrative(towerEndingId, ReturnToMainMenu)) Debug.LogError("Required ending missing: " + towerEndingId, this);
    }
}
