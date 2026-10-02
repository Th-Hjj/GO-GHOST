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
    public AimControl AimMode => officer != null && officer.Deployment != null ? officer.Deployment.Aim : AimControl.Manual;

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
    private bool hasFocus = true;
    // Release gates are (re)armed whenever input starts or focus returns. See Update.
    private bool waitingForRelease = true;
    private bool attackGated, jumpGated, dodgeGated, abilityGated, secondaryGated;
    private OfficerIdentity officer;
    private Enemy aimTarget;
    private Collider2D aimTargetCollider;
    private bool targetBound;

    private void Awake() => officer = GetComponent<OfficerIdentity>();

    // Bound exactly once by the new attempt. There is no live mode setter or global Boss lookup.
    internal void BindAimTarget(Enemy target)
    {
        if (targetBound) throw new System.InvalidOperationException("Aim target is assigned once per officer.");
        targetBound = true;
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
        InputActionAsset asset = InputSystem.actions;
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
        GameplayInputEnabled = false;
        ClearFrameState();
        waitingForRelease = true;
        aimTarget = null;
        aimTargetCollider = null;
    }

    private void OnDisable()
    {
        // Preserve the legacy temporary-disable behavior. Permanent input shutdown belongs to CleanupAttempt.
        ClearFrameState();
        waitingForRelease = true;
        aimTarget = null;
        aimTargetCollider = null;
    }

    public void SetGameplayInputEnabled(bool value)
    {
        GameplayInputEnabled = value;
        waitingForRelease = true;
        ClearFrameState();
    }

    private void OnApplicationFocus(bool value)
    {
        hasFocus = value;
        waitingForRelease = true;
        ClearFrameState();
    }

    private void Update()
    {
        if (!IsReady || !GameplayInputEnabled || !hasFocus)
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
        jumpGated &= jumpAction.IsPressed();
        dodgeGated &= dodgeAction.IsPressed();
        abilityGated &= abilityAction.IsPressed();
        secondaryGated &= secondaryAbilityAction.IsPressed();
        MoveInput = new Vector2(Mathf.Clamp(move.x, -1f, 1f), 0f);

        AttackHeld = !attackGated && attackAction.IsPressed();
        AttackPressedThisFrame = !attackGated && attackAction.WasPressedThisFrame();
        JumpPressedThisFrame = !jumpGated && jumpAction.WasPressedThisFrame();
        JumpHeld = !jumpGated && jumpAction.IsPressed();
        DodgePressedThisFrame = !dodgeGated && dodgeAction.WasPressedThisFrame();
        AbilityPressedThisFrame = !abilityGated && abilityAction.WasPressedThisFrame();
        SecondaryAbilityPressedThisFrame = !secondaryGated && secondaryAbilityAction.WasPressedThisFrame();
        PreviousPressedThisFrame = previousAction != null && previousAction.WasPressedThisFrame();
        NextPressedThisFrame = nextAction != null && nextAction.WasPressedThisFrame();

    }

    private void UpdateAimDirection()
    {
        Vector2 dir;
        if (AimMode == AimControl.AI)
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
            dir = (Vector2)world - (Vector2)transform.position;
        }
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }

    private void ClearFrameState()
    {
        MoveInput = Vector2.zero;
        AttackHeld = false;
        AttackPressedThisFrame = false;
        JumpPressedThisFrame = false;
        JumpHeld = false;
        DodgePressedThisFrame = false;
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
