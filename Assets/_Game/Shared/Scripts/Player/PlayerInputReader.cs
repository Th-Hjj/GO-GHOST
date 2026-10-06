using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The single place that reads the project-wide Input Actions (Player map)
/// and computes the unified AimDirection. Every player script that needs
/// input reads from this component instead of UnityEngine.Input.
/// Actions are polled each frame (no callbacks), so nothing has to be
/// unsubscribed when a scene reloads.
/// Must be present on the Player prefab; the other player scripts fail with
/// a clear error if it is missing.
/// </summary>
// FUNCTION SUMMARY
// OnEnable()          — resolves the Player actions from InputSystem.actions and logs an error for any missing action.
// Update()            — caches Move / Aim / Attack / Jump / Dodge / Ability / Previous / Next for this frame and recomputes AimDirection.
// UpdateAimDirection() — resolves exactly one authority: mouse world point or live Boss collider centre (never zero).
[DefaultExecutionOrder(-100)]
public class PlayerInputReader : MonoBehaviour, IAttemptAugmentation
{
    [Tooltip("Camera used to convert the mouse position to world space. Leave empty to use Camera.main.")]
    [SerializeField] private Camera aimCamera;

    /// <summary>Horizontal input, clamped to -1..1; y is always zero in the side-view slice.</summary>
    public Vector2 MoveInput { get; private set; }
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;

    /// <summary>Raw mouse position in screen pixels.</summary>
    public Vector2 AimScreenPosition { get; private set; }

    /// <summary>Final manual/AI world-space aim. All aim consumers read this output; never zero.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public AimControl AimMode => LegacyCombatActive ? (LegacyArmsAI ? AimControl.AI : AimControl.Manual)
        : officer != null && officer.Deployment != null ? officer.Deployment.Aim : AimControl.Manual;
    public bool LegacyCombatActive { get; private set; }
    public bool LegacyLegsAI { get; private set; }
    public bool LegacyArmsAI { get; private set; }
    public bool LegacySpineAI { get; private set; }
    public float DodgeDirection { get; private set; }
    private System.Func<LegacyCombatIntent> legacyLegs;
    private System.Func<LegacyCombatIntent> legacyArms;
    private System.Func<float, bool, LegacyCombatIntent> legacySpine;
    private InputActionAsset legacyActions;
    private bool legacyFireHeld;

    // UI-only ownership predicate. The level presenter supplies it; Shared never references level UI.
    public System.Func<bool> UiBlocksManualFire { get; set; }
    public bool AttackHeld { get; private set; }
    public bool AttackPressedThisFrame { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool DodgePressedThisFrame { get; private set; }
    public bool AbilityPressedThisFrame { get; private set; }
    public bool SecondaryAbilityPressedThisFrame { get; private set; }
    public bool PreviousPressedThisFrame { get; private set; }
    public bool NextPressedThisFrame { get; private set; }

    /// <summary>False when the actions asset or a required action could not be resolved.</summary>
    public bool IsReady { get; private set; }

    public bool GameplayInputEnabled { get; private set; } = true;
    public bool NarrativePaused { get; private set; }
    public bool HasTemporaryAimOverride => overrideTarget != null && overrideTarget.gameObject.activeInHierarchy && (overrideTargetValid == null || overrideTargetValid()) && Time.time < overrideEndsAt;
    public float OverrideRemaining => HasTemporaryAimOverride ? Mathf.Max(0f, overrideEndsAt - Time.time) : 0f;
    public bool AutomaticFireRequested => !LegacyCombatActive && HasTemporaryAimOverride && overrideFire && GameplayInputEnabled && hasFocus && !NarrativePaused;
    private Transform overrideTarget;
    private System.Func<bool> overrideTargetValid;
    private float overrideEndsAt;
    private bool overrideFire, overrideRunning, overridePlayerRequestedFire;
    public enum AimEndReason { Expired, Cancelled, TargetLost }
    public event System.Action<AimEndReason,bool> OnAimOverrideEnded;
    private int suppressAttackFrame = -1;
    private bool hasFocus = true;
    // Release gates are (re)armed whenever input starts or focus returns. See Update.
    private bool waitingForRelease = true;
    private bool attackGated, jumpGated, dodgeGated, abilityGated, secondaryGated;
    private OfficerIdentity officer;
    private PlayerShooting shooting;
    private Enemy aimTarget;
    private Collider2D aimTargetCollider;
    private bool targetBound;

    private void Awake()
    {
        officer = GetComponent<OfficerIdentity>();
        shooting = GetComponent<PlayerShooting>();
    }

    /// <summary>Called once on a fresh combat actor. Ownership never changes during this actor's wave.</summary>
    public void ConfigureLegacyControls(bool legsAI, bool armsAI, bool spineAI,
        System.Func<LegacyCombatIntent> legs, System.Func<LegacyCombatIntent> arms,
        System.Func<float, bool, LegacyCombatIntent> spine)
    {
        if (LegacyCombatActive) throw new System.InvalidOperationException("Combat permissions are already locked.");
        if ((legsAI && legs == null) || (armsAI && arms == null) || (spineAI && spine == null))
            throw new System.ArgumentException("An automated module requires its intent source.");
        EndAimOverride();
        LegacyCombatActive = true;
        LegacyLegsAI = legsAI; LegacyArmsAI = armsAI; LegacySpineAI = spineAI;
        legacyLegs = legs; legacyArms = arms; legacySpine = spine;
        // Preserve the project action asset and its old consumers. Only this actor's clone is restricted.
        legacyActions = Instantiate(InputSystem.actions);
        Restrict(legacyActions.FindAction("Player/Move", true), "<Keyboard>/a", "<Keyboard>/d");
        Restrict(legacyActions.FindAction("Player/Aim", true), "<Mouse>/position");
        Restrict(legacyActions.FindAction("Player/Attack", true), "<Mouse>/leftButton");
        Restrict(legacyActions.FindAction("Player/Jump", true), "<Keyboard>/space");
        Restrict(legacyActions.FindAction("Player/Dodge", true), "<Keyboard>/leftShift");
        Restrict(legacyActions.FindAction("Player/Ability", true));
        Restrict(legacyActions.FindAction("Player/SecondaryAbility", true));
        Restrict(legacyActions.FindAction("Player/Previous", true));
        Restrict(legacyActions.FindAction("Player/Next", true));
        OnEnable();
        SetGameplayInputEnabled(false);
    }

    private static void Restrict(InputAction action, params string[] paths)
    {
        for (int i = 0; i < action.bindings.Count; i++)
            if (!action.bindings[i].isComposite && System.Array.IndexOf(paths, action.bindings[i].path) < 0)
                action.ApplyBindingOverride(i, "");
    }

    private void OnDestroy()
    {
        if (legacyActions != null) { legacyActions.Disable(); Destroy(legacyActions); }
    }

    // Bound exactly once by the new attempt. There is no live mode setter or global Boss lookup.
    internal void BindAimTarget(Enemy target)
    {
        if (targetBound) throw new System.InvalidOperationException("Aim target is assigned once per officer.");
        targetBound = true;
        SetEncounterAimTarget(target);
    }

    // Room target changes never reapply the deployment or change its permission.
    public void SetEncounterAimTarget(Enemy target)
    {
        EndAimOverride();
        aimTarget = target;
        aimTargetCollider = target != null ? target.GetComponent<Collider2D>() : null;
    }

    private InputAction moveAction;
    private InputAction aimAction;
    private InputAction attackAction;
    private InputAction jumpAction;
    private InputAction dodgeAction;
    private InputAction abilityAction;
    private InputAction secondaryAbilityAction;
    private InputAction previousAction;
    private InputAction nextAction;

    private void OnEnable()
    {
        IsReady = false;
        InputActionAsset asset = legacyActions != null ? legacyActions : InputSystem.actions;
        if (asset == null)
        {
            Debug.LogError("PlayerInputReader: no project-wide Input Actions asset is assigned (Edit > Project Settings > Input System Package).", this);
            return;
        }

        moveAction = Find(asset, "Player/Move");
        aimAction = Find(asset, "Player/Aim");
        attackAction = Find(asset, "Player/Attack");
        jumpAction = Find(asset, "Player/Jump");
        dodgeAction = Find(asset, "Player/Dodge");
        abilityAction = Find(asset, "Player/Ability");
        secondaryAbilityAction = Find(asset, "Player/SecondaryAbility");
        previousAction = Find(asset, "Player/Previous");
        nextAction = Find(asset, "Player/Next");

        IsReady = moveAction != null && aimAction != null && attackAction != null && jumpAction != null
                  && dodgeAction != null && abilityAction != null && secondaryAbilityAction != null;

        // Project-wide actions are enabled automatically; this only covers a map that was disabled by other code.
        if (IsReady && !moveAction.actionMap.enabled)
            moveAction.actionMap.Enable();
    }

    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        CleanupAttempt();
        targetBound = false; // BossFight binds the new attempt's target after OfficerIdentity.Assign.
    }

    public void CleanupAttempt()
    {
        EndAimOverride();
        NarrativePaused = false;
        GameplayInputEnabled = false;
        ClearFrameState();
        waitingForRelease = true;
        aimTarget = null;
        aimTargetCollider = null;
    }

    private void OnDisable()
    {
        EndAimOverride();
        // Preserve the legacy temporary-disable behavior. Permanent input shutdown belongs to CleanupAttempt.
        ClearFrameState();
        waitingForRelease = true;
        aimTarget = null;
        aimTargetCollider = null;
    }

    public void SetGameplayInputEnabled(bool value)
    {
        if (!value) EndAimOverride();
        GameplayInputEnabled = value;
        waitingForRelease = true;
        ClearFrameState();
    }

    private void OnApplicationFocus(bool value)
    {
        if (!value) EndAimOverride();
        hasFocus = value;
        waitingForRelease = true;
        ClearFrameState();
    }

    private void Update()
    {
        if (overrideRunning && !HasTemporaryAimOverride)
            EndAimOverride(overrideTarget != null && overrideTarget.gameObject.activeInHierarchy ? AimEndReason.Expired : AimEndReason.TargetLost);
        if (!IsReady || !GameplayInputEnabled || !hasFocus || NarrativePaused)
        {
            ClearFrameState();
            return;
        }

        // Aim authority stays live even while action buttons wait for release after deployment.
        AimScreenPosition = aimAction.ReadValue<Vector2>();
        UpdateAimDirection();
        Vector2 move = moveAction.ReadValue<Vector2>();
        // A UI click or a button held through a focus return must not start an action: each button that is already
        // down when input (re)starts stays ignored until it is released once. Movement and every other button respond
        // immediately, so one held key never locks the whole officer. Focus loss still clears everything above.
        if (waitingForRelease)
        {
            waitingForRelease = false;
            attackGated = attackAction.IsPressed();
            jumpGated = jumpAction.IsPressed();
            dodgeGated = dodgeAction.IsPressed();
            abilityGated = abilityAction.IsPressed();
            secondaryGated = secondaryAbilityAction.IsPressed();
        }
        attackGated &= attackAction.IsPressed();
        if (attackAction.IsPressed() && UiBlocksManualFire?.Invoke() == true) SuppressUiFireUntilRelease();
        jumpGated &= jumpAction.IsPressed();
        dodgeGated &= dodgeAction.IsPressed();
        abilityGated &= abilityAction.IsPressed();
        secondaryGated &= secondaryAbilityAction.IsPressed();
        MoveInput = new Vector2(Mathf.Clamp(move.x, -1f, 1f), 0f);

        AttackHeld = Time.frameCount > suppressAttackFrame && !attackGated && attackAction.IsPressed();
        AttackPressedThisFrame = Time.frameCount > suppressAttackFrame && !attackGated && attackAction.WasPressedThisFrame();
        if (overrideRunning && AttackHeld) overridePlayerRequestedFire = true;
        JumpPressedThisFrame = !jumpGated && jumpAction.WasPressedThisFrame();
        JumpHeld = !jumpGated && jumpAction.IsPressed();
        DodgePressedThisFrame = !dodgeGated && dodgeAction.WasPressedThisFrame();
        AbilityPressedThisFrame = !abilityGated && abilityAction.WasPressedThisFrame();
        SecondaryAbilityPressedThisFrame = !secondaryGated && secondaryAbilityAction.WasPressedThisFrame();
        PreviousPressedThisFrame = previousAction != null && previousAction.WasPressedThisFrame();
        NextPressedThisFrame = nextAction != null && nextAction.WasPressedThisFrame();
        if (LegacyCombatActive) ResolveLegacyIntent();
    }

    private void ResolveLegacyIntent()
    {
        if (LegacyArmsAI)
        {
            LegacyCombatIntent decision = legacyArms();
            SetResolvedAim(decision.Aim);
            AttackHeld = decision.Fire;
            AttackPressedThisFrame = decision.Fire && !legacyFireHeld;
            legacyFireHeld = decision.Fire;
        }
        if (LegacyLegsAI)
        {
            LegacyCombatIntent decision = legacyLegs();
            MoveInput = new Vector2(Mathf.Clamp(decision.Move, -1f, 1f), 0f);
            JumpPressedThisFrame = decision.Jump;
            JumpHeld = false;
        }
        DodgeDirection = 0f; // Manual Dodge uses movement / last movement facing.
        if (LegacySpineAI) DodgePressedThisFrame = false;
        AbilityPressedThisFrame = SecondaryAbilityPressedThisFrame = PreviousPressedThisFrame = NextPressedThisFrame = false;
    }

    // Called after the movement component refreshes contacts, before accepting either action.
    // Spine observes this frame's executable movement/jump; it can write only Dodge.
    public void ResolveLegacyDodge(bool executableJump)
    {
        if (!LegacyCombatActive || !LegacySpineAI || !GameplayInputEnabled || NarrativePaused || !hasFocus) return;
        var decision = legacySpine(MoveInput.x, executableJump);
        DodgePressedThisFrame = decision.Dodge;
        DodgeDirection = decision.DodgeDirection;
    }

    private void SetResolvedAim(Vector2 direction)
    {
        if (direction.sqrMagnitude > .0001f) AimDirection = direction.normalized;
    }

    private void UpdateAimDirection()
    {
        Vector2 dir;
        if (!LegacyCombatActive && HasTemporaryAimOverride)
        {
            dir = (Vector2)overrideTarget.position - (Vector2)transform.position;
        }
        else if (!LegacyCombatActive && AimMode == AimControl.AI)
        {
            // Exclusive authority: an invalid/dead target holds the last aim; it never falls back to mouse.
            if (aimTarget == null || aimTarget.IsDead || !aimTarget.isActiveAndEnabled) return;
            Vector2 point = aimTargetCollider != null ? aimTargetCollider.bounds.center : aimTarget.transform.position;
            dir = point - (Vector2)transform.position;
        }
        else
        {
            Camera cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null) return;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(AimScreenPosition.x, AimScreenPosition.y, 0f));
            Vector2 origin = LegacyCombatActive && shooting != null ? shooting.AimOrigin : (Vector2)transform.position;
            dir = (Vector2)world - origin;
        }
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }

    public bool BeginAimOverride(Transform target, float seconds, bool autonomousFire, System.Func<bool> targetValid = null)
    {
        if (LegacyCombatActive) return false;
        var snapshot = officer != null ? officer.Deployment : null;
        if (snapshot == null || snapshot.BrainPermissionTier < 3
            || target == null || !target.gameObject.activeInHierarchy || seconds <= 0f || !GameplayInputEnabled) return false;
        EndAimOverride();
        overrideRunning = true;
        overridePlayerRequestedFire = false;
        overrideTarget = target;
        overrideTargetValid = targetValid;
        overrideEndsAt = Time.time + seconds;
        overrideFire = autonomousFire;
        UpdateAimDirection();
        return true;
    }

    // A context intervention requests one paid shot, then immediately returns aim to the mouse.
    public void CompleteAutomaticShot()
    {
        if (overrideFire) EndAimOverride();
    }

    public void AimPlayerActionAt(Vector2 point)
    {
        if (LegacyCombatActive) return;
        Vector2 dir = point - (Vector2)transform.position;
        if (dir.sqrMagnitude > .0001f) AimDirection = dir.normalized;
    }

    public void EndAimOverride() => EndAimOverride(AimEndReason.Cancelled);
    private void EndAimOverride(AimEndReason reason)
    {
        bool notify = overrideRunning, requested = overridePlayerRequestedFire;
        overrideRunning = false;
        overrideTarget = null;
        overrideTargetValid = null;
        overrideEndsAt = 0f;
        overrideFire = false;
        if (notify) OnAimOverrideEnded?.Invoke(reason, requested);
    }

    public void SuppressUiFireUntilRelease()
    {
        attackGated = true;
        suppressAttackFrame = Time.frameCount;
        AttackHeld = AttackPressedThisFrame = false;
    }

    public void SetNarrativePaused(bool paused)
    {
        NarrativePaused = paused;
        ClearFrameState();
        waitingForRelease = true;
        suppressAttackFrame = Time.frameCount; // the closing UI event cannot leak into this frame's shot
    }

    private void ClearFrameState()
    {
        legacyFireHeld = false;
        MoveInput = Vector2.zero;
        AttackHeld = false;
        AttackPressedThisFrame = false;
        JumpPressedThisFrame = false;
        JumpHeld = false;
        DodgePressedThisFrame = false;
        DodgeDirection = 0f;
        AbilityPressedThisFrame = false;
        SecondaryAbilityPressedThisFrame = false;
        PreviousPressedThisFrame = false;
        NextPressedThisFrame = false;
    }

    private InputAction Find(InputActionAsset asset, string path)
    {
        InputAction action = asset.FindAction(path);
        if (action == null)
            Debug.LogError($"PlayerInputReader: action '{path}' not found in '{asset.name}'. Add it in Assets/Settings/InputSystem_Actions.inputactions.", this);
        return action;
    }
}
