using UnityEngine;

/// <summary>Existing prototype Torso ability, activated with Q (temporary MSP mapping). Hostile-only clock; no assigned final tier.</summary>
public sealed class PlayerTimeSlow : MonoBehaviour, IAttemptAugmentation
{
    public const float Duration = 3f;
    public const float StartInterval = 10f;
    public const float SlowRate = 0.25f;
    private OfficerIdentity officer;
    private PlayerInputReader input;
    private PlayerHealth health;
    private float activeUntil;
    private float readyAt;
    private bool attemptActive;

    public bool IsEquipped => attemptActive && officer != null && officer.Deployment != null && officer.Deployment.HasTimeSlow;
    public bool IsActive => isActiveAndEnabled && IsEquipped && Time.unscaledTime < activeUntil;
    public float WorldRate => IsActive ? SlowRate : 1f;
    public float ActiveRemaining => IsActive ? activeUntil - Time.unscaledTime : 0f;
    public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.unscaledTime);
    public string Cue => IsActive ? $"TIME SLOW ACTIVE {ActiveRemaining:0.0}s / WORLD 25%"
        : CooldownRemaining > 0 ? $"TIME SLOW COOLDOWN {CooldownRemaining:0.0}s" : "TIME SLOW READY [Q]";

    private void Awake()
    {
        officer = GetComponent<OfficerIdentity>();
        input = GetComponent<PlayerInputReader>();
        health = GetComponent<PlayerHealth>();
    }

    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        CleanupAttempt();
        attemptActive = true;
    }

    private void Update()
    {
        if (input.SecondaryAbilityPressedThisFrame) TryActivate();
    }

    public bool TryActivate()
    {
        if (!isActiveAndEnabled || !IsEquipped || !input.GameplayInputEnabled || health.IsDead || CooldownRemaining > 0f) return false;
        activeUntil = Time.unscaledTime + Duration;
        readyAt = Time.unscaledTime + StartInterval;
        return true;
    }

    public void CleanupAttempt()
    {
        attemptActive = false;
        activeUntil = 0f;
        readyAt = 0f;
    }

    private void OnDisable() => CleanupAttempt();
}
