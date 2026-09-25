using System;
using System.Collections;
using UnityEngine;

/// <summary>One scene-local session. Each deployment owns a fresh combat prefab; no static/persistent session state.</summary>
public sealed class GameSessionFlow : MonoBehaviour
{
    public enum FlowState { NewGameInitialization, Deployment, Combat, DefeatTransition, Loadout, Result, NextOfficerTransition, VictoryTransition }
    [SerializeField] private BossFight attemptPrefab;
    // Retained for existing scene serialization; presentation now advances only on user input.
#pragma warning disable CS0414 // These legacy serialized fields intentionally have no runtime readers.
    [SerializeField, HideInInspector] private float defeatNoticeSeconds = 8f;
    [SerializeField, HideInInspector] private float nextOfficerSeconds = 8f;
    [SerializeField, HideInInspector] private float newGameIntroSeconds = 8f;
    [SerializeField, HideInInspector] private float deploymentSeconds = 8f;
    [SerializeField, HideInInspector] private float victoryNoticeSeconds = 8f;
    [SerializeField, HideInInspector] private float loadoutReadSeconds = 8f;
    [SerializeField, HideInInspector] private float resultReadSeconds = 8f;
#pragma warning restore CS0414

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
    public bool CanNewGame => State == FlowState.Result || State == FlowState.Loadout;
    public bool CanContinue => !continueRequested && Draft != null && (State == FlowState.NewGameInitialization
        || State == FlowState.Deployment || State == FlowState.DefeatTransition
        || State == FlowState.NextOfficerTransition || State == FlowState.VictoryTransition);

    private Action<BossFight.Outcome> outcomeHandler;
    private int generation;
    private bool continueRequested;

    private void Awake()
    {
        // Explicit instance initialization also works with Domain Reload disabled.
        State = FlowState.NewGameInitialization;
        OfficerIndex = 1;
        AugmentationsUnlocked = false;
        Draft = null;
        Deployment = null;
        Victory = null;
        ActiveFight = null;
        generation = 0;
        continueRequested = false;
        stateEnteredAt = Time.unscaledTime;
    }

    private void Start() => RequestNewGame();

    public bool RequestNewGame()
    {
        if (!(State == FlowState.NewGameInitialization && Draft == null) && !CanNewGame) return false;
        if (attemptPrefab == null) { Debug.LogError("GameSessionFlow requires a combat attempt prefab.", this); return false; }
        StopAllCoroutines();
        generation++;
        DisposeAttempt();
        Draft?.Lock();
        OfficerIndex = 1;
        AugmentationsUnlocked = false;
        Draft = new LoadoutDraft();
        Deployment = null;
        Victory = null;
        SetState(FlowState.NewGameInitialization);
        StartCoroutine(NewGameRoutine());
        return true;
    }

    private IEnumerator NewGameRoutine()
    {
        yield return WaitForContinue();
        yield return DeployRoutine();
    }

    public bool RequestContinue()
    {
        if (!CanContinue) return false;
        continueRequested = true; // Consume once; a second click in this state cannot skip the next page.
        return true;
    }

    private IEnumerator WaitForContinue()
    {
        while (!continueRequested) yield return null;
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
        SetState(FlowState.Deployment); // Close the API/UI gate before a second click can be accepted.
        Deployment = Draft.Deploy(OfficerIndex);
        int token = ++generation;
        var fight = Instantiate(attemptPrefab, transform);
        ActiveFight = fight;
        fight.name = "CombatAttempt_Officer_" + OfficerIndex;
        fight.Prepare(Deployment);
        outcomeHandler = outcome => ResolveAttempt(fight, token, outcome);
        fight.OnResolved += outcomeHandler;

        // Settle the existing gravity-driven bodies before allowing input or starting the Boss timer.
        for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
        yield return WaitForContinue();
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
            SetState(FlowState.VictoryTransition);
            StartCoroutine(VictoryRoutine(token));
        }
        else if (outcome == BossFight.Outcome.Died)
        {
            AugmentationsUnlocked = true;
            SetState(FlowState.DefeatTransition);
            StartCoroutine(NextOfficerRoutine(token));
        }
    }

    private IEnumerator NextOfficerRoutine(int token)
    {
        yield return WaitForContinue();
        if (token != generation || State != FlowState.DefeatTransition) yield break;
        var previous = Deployment;
        DisposeAttempt();
        OfficerIndex++;
        Draft = new LoadoutDraft(previous, AugmentationsUnlocked);
        Deployment = null;
        SetState(FlowState.NextOfficerTransition);
        yield return WaitForContinue();
        if (token == generation && State == FlowState.NextOfficerTransition) SetState(FlowState.Loadout);
    }

    private IEnumerator VictoryRoutine(int token)
    {
        yield return WaitForContinue();
        if (token == generation && State == FlowState.VictoryTransition) SetState(FlowState.Result);
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
        continueRequested = false;
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
