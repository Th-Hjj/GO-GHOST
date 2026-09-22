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
// Update()            — caches Move / Aim / Attack / Dodge / Ability / Previous / Next for this frame and recomputes AimDirection.
// UpdateAimDirection() — mouse screen position -> world point -> normalized direction from the player (never zero).
[DefaultExecutionOrder(-100)]
public class PlayerInputReader : MonoBehaviour
{
    [Tooltip("Camera used to convert the mouse position to world space. Leave empty to use Camera.main.")]
    [SerializeField] private Camera aimCamera;

    /// <summary>WASD / left stick, magnitude clamped to 1.</summary>
    public Vector2 MoveInput { get; private set; }
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;

    /// <summary>Raw mouse position in screen pixels.</summary>
    public Vector2 AimScreenPosition { get; private set; }

    /// <summary>Normalized direction from the player to the mouse cursor in world space. Never zero.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;

    public bool AttackHeld { get; private set; }
    public bool AttackPressedThisFrame { get; private set; }
    public bool DodgePressedThisFrame { get; private set; }
    public bool AbilityPressedThisFrame { get; private set; }
    public bool PreviousPressedThisFrame { get; private set; }
    public bool NextPressedThisFrame { get; private set; }

    /// <summary>False when the actions asset or a required action could not be resolved.</summary>
    public bool IsReady { get; private set; }

    private InputAction moveAction;
    private InputAction aimAction;
    private InputAction attackAction;
    private InputAction dodgeAction;
    private InputAction abilityAction;
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
        dodgeAction = Find(asset, "Player/Dodge");
        abilityAction = Find(asset, "Player/Ability");
        previousAction = Find(asset, "Player/Previous");
        nextAction = Find(asset, "Player/Next");

        IsReady = moveAction != null && aimAction != null && attackAction != null && dodgeAction != null && abilityAction != null;

        // Project-wide actions are enabled automatically; this only covers a map that was disabled by other code.
        if (IsReady && !moveAction.actionMap.enabled)
            moveAction.actionMap.Enable();
    }

    private void OnDisable()
    {
        ClearFrameState();
    }

    private void Update()
    {
        if (!IsReady)
        {
            ClearFrameState();
            return;
        }

        Vector2 move = moveAction.ReadValue<Vector2>();
        MoveInput = move.sqrMagnitude > 1f ? move.normalized : move;
        AimScreenPosition = aimAction.ReadValue<Vector2>();

        AttackHeld = attackAction.IsPressed();
        AttackPressedThisFrame = attackAction.WasPressedThisFrame();
        DodgePressedThisFrame = dodgeAction.WasPressedThisFrame();
        AbilityPressedThisFrame = abilityAction.WasPressedThisFrame();
        PreviousPressedThisFrame = previousAction != null && previousAction.WasPressedThisFrame();
        NextPressedThisFrame = nextAction != null && nextAction.WasPressedThisFrame();

        UpdateAimDirection();
    }

    private void UpdateAimDirection()
    {
        Camera cam = aimCamera != null ? aimCamera : Camera.main;
        if (cam == null) return;

        // Orthographic camera: the z argument only affects the returned z, which is discarded here.
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(AimScreenPosition.x, AimScreenPosition.y, 0f));
        Vector2 dir = (Vector2)world - (Vector2)transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }

    private void ClearFrameState()
    {
        MoveInput = Vector2.zero;
        AttackHeld = false;
        AttackPressedThisFrame = false;
        DodgePressedThisFrame = false;
        AbilityPressedThisFrame = false;
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
