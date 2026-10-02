using System;
using System.Collections;
using UnityEngine;

/// <summary>One scene-local session. Each deployment owns a fresh combat prefab; no static/persistent session state.</summary>
public sealed partial class GameSessionFlow : MonoBehaviour
{
    public enum FlowState { NewGameInitialization, Deployment, Combat, DefeatTransition, Loadout, Result, NextOfficerTransition, VictoryTransition, MainMenu, Archive, Practice, Ending }
    [SerializeField] private BossFight attemptPrefab;
    [SerializeField] private GameObject highCPlatform;
    [Tooltip("The three high platforms BLACKOUT may break (High C, D, E). All are restored at every deployment.")]
    [SerializeField] private Collider2D[] breakableHighPlatforms = new Collider2D[0];
    [Tooltip("Ground and the two low platforms. Never broken.")]
    [SerializeField] private Collider2D[] permanentSupports = new Collider2D[0];
    [Tooltip("World-space interior between the arena walls, floor surface and ceiling (not the camera).")]
    [SerializeField] private Bounds playableBounds;
    [Tooltip("Practice room root. Its position is the room's offset from the Boss arena (same layout, same fixed camera framing).")]
    [SerializeField] private Transform practiceRoom;
    public FlowState State { get; private set; }
    public int OfficerIndex { get; private set; }
    public bool AugmentationsUnlocked { get; private set; }
    public LoadoutDraft Draft { get; private set; }
    public DeploymentSnapshot Deployment { get; private set; }
    public VictoryRecord Victory { get; private set; }
    /// <summary>TEMP generated officer for the current mission; stable from START MISSION until the mission ends.</summary>
    public OfficerProfile Officer { get; private set; }
    /// <summary>The frozen Archive record for the latest resolved attempt in this scene session.</summary>
    public PoliceRunRecord LastRunRecord { get; private set; }
    public BossFight ActiveFight { get; private set; }
    public event Action<FlowState> OnStateChanged;
    private float stateEnteredAt;
    public float StateAge => Time.unscaledTime - stateEnteredAt;
    public float HoldRemaining => 0f;
    public bool CanDeploy => State == FlowState.Loadout;
    public bool CanNewGame => State == FlowState.MainMenu || State == FlowState.Result || State == FlowState.Loadout;
    public bool CanContinue => State == FlowState.Result || State == FlowState.Ending;
    /// <summary>The ending earned by the winning officer's immutable deployment; null without a victory.</summary>
    public string EndingId => Victory != null ? PoliceArchive.EndingFor(Victory.BodyModificationPercent) : null;

    private Action<BossFight.Outcome> outcomeHandler;
    private int generation;
    private Transform view;
    private Vector3 arenaView;

    private void Awake()
    {
        // Explicit instance initialization also works with Domain Reload disabled.
        InitializeTowerRooms();
        State = FlowState.MainMenu;
        OfficerIndex = PoliceArchive.Current.NextOfficerNumber;
        AugmentationsUnlocked = false;
        Draft = null;
        Deployment = null;
        Victory = null;
        Officer = null;
        LastRunRecord = null;
        ActiveFight = null;
        generation = 0;
        stateEnteredAt = Time.unscaledTime;
        if (Camera.main != null) { view = Camera.main.transform; arenaView = view.position; }
    }

    // Launch goes straight to the practice room with the default human / Basic Gun build. The Archive is only read.
    private void Start()
    {
        if (RequestNewGame()) RequestDeploy();
    }

    public bool RequestNewGame()
    {
        if (!CanNewGame) return false;
        if (attemptPrefab == null) { Debug.LogError("GameSessionFlow requires a combat attempt prefab.", this); return false; }
        StopAllCoroutines();
        generation++;
        DisposeAttempt();
        ResetTowerRun();
        Draft?.Lock();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        developmentJumpRun = false;
#endif
        OfficerIndex = PoliceArchive.Current.NextOfficerNumber;
        AugmentationsUnlocked = true;
        Draft = new LoadoutDraft(true);
        Deployment = null;
        Victory = null;
        // One generated profile per new mission, before its deployment. Numbering follows this session's Archive records.
        Officer = OfficerProfile.Generate(PoliceArchive.Current.NextOfficerNumber);
        SetState(FlowState.Loadout);
        return true;
    }

    public bool RequestContinue()
    {
        if (!CanContinue) return false;
        // Victory: Mission Result -> Ending -> Main Menu. Defeat: Mission Result -> Main Menu. The record was saved at resolution.
        if (State == FlowState.Result && Victory != null) SetState(FlowState.Ending);
        else ReturnToMainMenu();
        return true;
    }

    /// <summary>The practice door calls this when the officer walks through: the one real combat start of this deployment.</summary>
    public bool RequestEnterBossRoom()
    {
        if (State != FlowState.Practice || ActiveFight == null) return false;
        if (towerPrototypeEnabled) return BeginTowerRoute();
        ActiveFight.PlacePlayer(Vector2.zero);
        SetView(Vector2.zero);
        ActiveFight.BeginCombat();
        SetState(FlowState.Combat);
        return true;
    }

    private void ReturnToMainMenu()
    {
        StopAllCoroutines();
        generation++;
        DisposeAttempt();
        ResetTowerRun();
        Draft?.Lock();
        Draft = null;
        Deployment = null;
        Officer = null;
        SetState(FlowState.MainMenu);
    }

    public bool RequestArchive()
    {
        if (State != FlowState.MainMenu) return false;
        SetState(FlowState.Archive);
        return true;
    }

    public bool RequestBackToMainMenu()
    {
        if (State != FlowState.Archive && State != FlowState.Loadout) return false;
        Draft?.Lock();
        Draft = null;
        Officer = null; // Backing out abandons an unresolved mission; nothing is recorded.
        SetState(FlowState.MainMenu);
        return true;
    }

    public bool RequestDeploy()
    {
        if (!CanDeploy || Draft == null || Draft.IsLocked || Officer == null) return false;
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
        CancelIntervention();
        interventionOffered = false;
        if (chandelier != null) chandelier.ResetEncounter();
        Deployment = Draft.Deploy(OfficerIndex, permissionPrototypeEnabled ? developmentPermissionTier : -1);
        int token = ++generation;
        // Restore the breakable scene platforms at every deployment; never persist terrain in Police Archive.
        if (highCPlatform != null) highCPlatform.SetActive(true);
        foreach (var platform in breakableHighPlatforms) if (platform != null) platform.gameObject.SetActive(true);
        var fight = Instantiate(attemptPrefab, transform);
        ActiveFight = fight;
        fight.name = "CombatAttempt_Officer_" + OfficerIndex;
        fight.Prepare(Deployment);
        if (towerPrototypeEnabled) fight.EnableTowerMission();
        fight.Boss.BindHighC(highCPlatform);
        // An empty platform list intentionally leaves the old platform-dependent BLACKOUT unbound.
        // Configured platform arenas still receive the existing strict validation.
        if (breakableHighPlatforms.Length > 0 && !fight.Boss.BindBlackoutArena(playableBounds, breakableHighPlatforms, permanentSupports))
            Debug.LogError("BLACKOUT arena bind failed; check the GameSession platform references.", this);
        outcomeHandler = outcome => ResolveAttempt(fight, token, outcome);
        fight.OnResolved += outcomeHandler;

        // Every deployment starts in the practice room; the Boss room, its timer and statistics wait for the door.
        Vector2 practiceOffset = practiceRoom.position;
        fight.PlacePlayer(practiceOffset);
        SetView(practiceOffset);

        // Settle the existing gravity-driven bodies before allowing input.
        for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
        if (token != generation || fight != ActiveFight || State != FlowState.Deployment) yield break;
        fight.BeginPractice();
        SetState(FlowState.Practice);
    }

    // Hard cut of the existing fixed camera between the two rooms; no transition.
    private void SetView(Vector2 offsetFromArena)
    {
        if (view != null) view.position = arenaView + (Vector3)offsetFromArena;
    }

    private void ResolveAttempt(BossFight source, int token, BossFight.Outcome outcome)
    {
        if ((State != FlowState.Combat && !(towerPrototypeEnabled && State == FlowState.Practice)) || token != generation || source != ActiveFight) return;
        if (towerPrototypeEnabled)
        {
            if (outcome != BossFight.Outcome.Died || towerTerminal) return;
            towerTerminal = true;
            ClearTowerWorld();
        }
        if (outcome != BossFight.Outcome.Won && outcome != BossFight.Outcome.Died) return;
        if (outcome == BossFight.Outcome.Won) Victory = new VictoryRecord(source.Deployment);
        // Exactly one frozen record per resolved attempt: this officer, the immutable deployment and BossFight's frozen values.
        // Archive evaluates the TEMP achievement/Drone unlock and TEMP ending from it, then saves once.
        LastRunRecord = new PoliceRunRecord(Officer, source.Deployment, outcome == BossFight.Outcome.Won,
            source.CombatDuration, source.ResultBossHealth, source.ResultPlayerHealth, source.AccuracyPercent);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!developmentJumpRun)
#endif
        if (!PoliceArchive.RecordCompletedRun(LastRunRecord, out string archiveError))
            Debug.LogWarning("Police Archive run save failed: " + archiveError, this);
        // Both outcomes stop on Mission Result (the attempt stays alive for its frozen values) and wait for CONTINUE.
        SetState(FlowState.Result);
    }

    private void DisposeAttempt()
    {
        CancelIntervention();
        if (ActiveFight == null) return;
        ActiveFight.OnEncounterEnded -= TowerEncounterEnded;
        ActiveFight.OnResolved -= outcomeHandler;
        outcomeHandler = null;
        ActiveFight.CancelCombat();
        ActiveFight.gameObject.SetActive(false);
        Destroy(ActiveFight.gameObject);
        ActiveFight = null;
        SetView(Vector2.zero);
    }

    private void SetState(FlowState state)
    {
        if (state != FlowState.Combat) CancelIntervention();
        State = state;
        stateEnteredAt = Time.unscaledTime;
        Debug.Log($"[Session] {state} / Officer #{OfficerIndex}", this);
        OnStateChanged?.Invoke(state);
    }

    private void OnDestroy()
    {
        CancelIntervention();
        generation++;
        StopAllCoroutines();
        if (ActiveFight != null) ActiveFight.OnResolved -= outcomeHandler;
        Draft?.Lock();
    }
}
