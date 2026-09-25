using System;
using System.Collections;
using UnityEngine;

/// <summary>One scene-local session. Each deployment owns a fresh combat prefab; no static/persistent session state.</summary>
public sealed class GameSessionFlow : MonoBehaviour
{
    public enum FlowState { NewGameInitialization, Deployment, Combat, DefeatTransition, Loadout, Result, NextOfficerTransition, VictoryTransition }
    [SerializeField] private BossFight attemptPrefab;
    [Header("Presentation holds — real seconds, independent of combat slow time")]
    [SerializeField] private float defeatNoticeSeconds = 8f;
    [SerializeField] private float nextOfficerSeconds = 8f;
    [SerializeField] private float newGameIntroSeconds = 8f;
    [SerializeField] private float deploymentSeconds = 8f;
    [SerializeField] private float victoryNoticeSeconds = 8f;
    [SerializeField] private float loadoutReadSeconds = 8f;
    [SerializeField] private float resultReadSeconds = 8f;

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
    public float HoldRemaining => Mathf.Max(0f, (State switch
    {
        FlowState.NewGameInitialization => newGameIntroSeconds,
        FlowState.Deployment => deploymentSeconds,
        FlowState.DefeatTransition => defeatNoticeSeconds,
        FlowState.NextOfficerTransition => nextOfficerSeconds,
        FlowState.VictoryTransition => victoryNoticeSeconds,
        FlowState.Loadout => loadoutReadSeconds,
        FlowState.Result => resultReadSeconds,
        _ => 0f
    }) - StateAge);
    public bool CanDeploy => State == FlowState.Loadout && HoldRemaining <= 0f;
    public bool CanNewGame => (State == FlowState.Result || State == FlowState.Loadout) && HoldRemaining <= 0f;

    private Action<BossFight.Outcome> outcomeHandler;
    private int generation;

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
        yield return new WaitForSecondsRealtime(newGameIntroSeconds);
        yield return DeployRoutine();
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
        while (token == generation && HoldRemaining > 0f) yield return null;
        if (token != generation || fight != ActiveFight) yield break;
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
        yield return new WaitForSecondsRealtime(defeatNoticeSeconds);
        if (token != generation || State != FlowState.DefeatTransition) yield break;
        var previous = Deployment;
        DisposeAttempt();
        OfficerIndex++;
        Draft = new LoadoutDraft(previous, AugmentationsUnlocked);
        Deployment = null;
        SetState(FlowState.NextOfficerTransition);
        yield return new WaitForSecondsRealtime(nextOfficerSeconds);
        if (token == generation) SetState(FlowState.Loadout);
    }

    private IEnumerator VictoryRoutine(int token)
    {
        yield return new WaitForSecondsRealtime(victoryNoticeSeconds);
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
