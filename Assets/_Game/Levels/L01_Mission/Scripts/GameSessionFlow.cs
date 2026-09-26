using System;
using System.Collections;
using UnityEngine;

/// <summary>One scene-local session. Each deployment owns a fresh combat prefab; no static/persistent session state.</summary>
public sealed class GameSessionFlow : MonoBehaviour
{
    public enum FlowState { NewGameInitialization, Deployment, Combat, DefeatTransition, Loadout, Result, NextOfficerTransition, VictoryTransition, MainMenu, Archive }
    [SerializeField] private BossFight attemptPrefab;
    [SerializeField] private GameObject highCPlatform;
    public FlowState State { get; private set; }
    public int OfficerIndex { get; private set; }
    public bool AugmentationsUnlocked { get; private set; }
    public LoadoutDraft Draft { get; private set; }
    public DeploymentSnapshot Deployment { get; private set; }
    public VictoryRecord Victory { get; private set; }
    public BossFight ActiveFight { get; private set; }
    public event Action<FlowState> OnStateChanged;
    private float stateEnteredAt;
    public float StateAge => Time.unscaledTime - stateEnteredAt;
    public float HoldRemaining => 0f;
    public bool CanDeploy => State == FlowState.Loadout;
    public bool CanNewGame => State == FlowState.MainMenu || State == FlowState.Result || State == FlowState.Loadout;
    public bool CanContinue => State == FlowState.Result;

    private Action<BossFight.Outcome> outcomeHandler;
    private int generation;

    private void Awake()
    {
        // Explicit instance initialization also works with Domain Reload disabled.
        State = FlowState.MainMenu;
        OfficerIndex = 1;
        AugmentationsUnlocked = false;
        Draft = null;
        Deployment = null;
        Victory = null;
        ActiveFight = null;
        generation = 0;
        stateEnteredAt = Time.unscaledTime;
    }

    public bool RequestNewGame()
    {
        if (!CanNewGame) return false;
        if (attemptPrefab == null) { Debug.LogError("GameSessionFlow requires a combat attempt prefab.", this); return false; }
        StopAllCoroutines();
        generation++;
        DisposeAttempt();
        Draft?.Lock();
        OfficerIndex = 1;
        AugmentationsUnlocked = true;
        Draft = new LoadoutDraft(true);
        Deployment = null;
        Victory = null;
        SetState(FlowState.Loadout);
        return true;
    }

    public bool RequestContinue()
    {
        if (!CanContinue) return false;
        StopAllCoroutines();
        generation++;
        DisposeAttempt();
        Draft?.Lock();
        Draft = null;
        Deployment = null;
        SetState(FlowState.MainMenu);
        return true;
    }

    public bool RequestArchive()
    {
        if (State != FlowState.MainMenu) return false;
        SetState(FlowState.Archive);
        return true;
    }

    public bool RequestDeploy()
    {
        if (!CanDeploy || Draft == null || Draft.IsLocked) return false;
        StartCoroutine(DeployRoutine());
        return true;
    }

    public bool TrySetDraftSlot(LoadoutSlot slot, string itemId)
    {
        return State == FlowState.Loadout && AugmentationsUnlocked && Draft != null && Draft.TrySetSlot(slot, itemId);
    }

    private IEnumerator DeployRoutine()
    {
        SetState(FlowState.Deployment); // Reject duplicate deployment requests.
        Deployment = Draft.Deploy(OfficerIndex);
        int token = ++generation;
        // Restore only this scene platform at deployment; never persist terrain in Police Archive.
        if (highCPlatform != null) highCPlatform.SetActive(true);
        var fight = Instantiate(attemptPrefab, transform);
        ActiveFight = fight;
        fight.name = "CombatAttempt_Officer_" + OfficerIndex;
        fight.Prepare(Deployment);
        fight.Boss.BindHighC(highCPlatform);
        outcomeHandler = outcome => ResolveAttempt(fight, token, outcome);
        fight.OnResolved += outcomeHandler;

        // Settle the existing gravity-driven bodies before allowing input or starting the Boss timer.
        for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
        if (token != generation || fight != ActiveFight || State != FlowState.Deployment) yield break;
        fight.BeginCombat();
        SetState(FlowState.Combat);
    }

    private void ResolveAttempt(BossFight source, int token, BossFight.Outcome outcome)
    {
        if (State != FlowState.Combat || token != generation || source != ActiveFight) return;
        if (outcome == BossFight.Outcome.Won)
        {
            Victory = new VictoryRecord(source.Deployment);
            if (!PoliceArchive.RecordBossVictory(out string archiveError))
                Debug.LogWarning("Police Archive victory save failed: " + archiveError, this);
            SetState(FlowState.Result);
        }
        else if (outcome == BossFight.Outcome.Died)
        {
            SetState(FlowState.Result);
        }
    }

    private void DisposeAttempt()
    {
        if (ActiveFight == null) return;
        ActiveFight.OnResolved -= outcomeHandler;
        outcomeHandler = null;
        ActiveFight.CancelCombat();
        ActiveFight.gameObject.SetActive(false);
        Destroy(ActiveFight.gameObject);
        ActiveFight = null;
    }

    private void SetState(FlowState state)
    {
        State = state;
        stateEnteredAt = Time.unscaledTime;
        Debug.Log($"[Session] {state} / Officer #{OfficerIndex}", this);
        OnStateChanged?.Invoke(state);
    }

    private void OnDestroy()
    {
        generation++;
        StopAllCoroutines();
        if (ActiveFight != null) ActiveFight.OnResolved -= outcomeHandler;
        Draft?.Lock();
    }
}
