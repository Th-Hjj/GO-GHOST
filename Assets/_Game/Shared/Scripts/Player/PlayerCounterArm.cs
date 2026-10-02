using UnityEngine;

/// <summary>Left Arm MSP counter. Compatible attacks opt in; never grants general invulnerability.</summary>
public sealed class PlayerCounterArm : MonoBehaviour, IAttemptAugmentation
{
    public const float WindowSeconds = .35f;
    public const float StartInterval = 1f;
    private PlayerInputReader input;
    private PlayerHealth health;
    private float windowUntil, readyAt, feedbackUntil;
    private bool equipped;
    private SpriteRenderer feedback;
    private SfxSet sfx;
    public bool IsEquipped => equipped && isActiveAndEnabled;
    public bool IsWindowOpen => IsEquipped && input.GameplayInputEnabled && !health.IsDead && Time.unscaledTime < windowUntil;
    public int CountersSucceeded { get; private set; }
    public float CooldownRemaining => Mathf.Max(0, readyAt - Time.unscaledTime);
    public string Cue => Time.unscaledTime < feedbackUntil ? "COUNTERED!"
        : IsWindowOpen ? "COUNTER WINDOW [E]" : CooldownRemaining > 0 ? $"COUNTER {CooldownRemaining:0.0}s" : "COUNTER READY [E]";

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        health = GetComponent<PlayerHealth>();
        sfx = GetComponentInParent<SfxSet>();
    }

    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        CleanupAttempt();
        equipped = snapshot.HasCounterArm;
        CountersSucceeded = 0;
        if (!equipped || feedback != null) return;
        var marker = new GameObject("CounterWindow_TEST");
        marker.transform.SetParent(transform, false);
        feedback = marker.AddComponent<SpriteRenderer>();
        var body = GetComponent<SpriteRenderer>();
        feedback.sprite = body.sprite;
        feedback.sharedMaterial = body.sharedMaterial;
        feedback.sortingLayerID = body.sortingLayerID;
        feedback.sortingOrder = body.sortingOrder + 2;
        feedback.enabled = false;
    }

    private void Update()
    {
        if (IsEquipped && input.GameplayInputEnabled && !health.IsDead
            && input.AbilityPressedThisFrame && CooldownRemaining <= 0)
        {
            windowUntil = Time.unscaledTime + WindowSeconds;
            readyAt = Time.unscaledTime + StartInterval;
        }
        if (feedback == null) return;
        bool success = Time.unscaledTime < feedbackUntil;
        feedback.enabled = IsEquipped && (IsWindowOpen || success);
        feedback.color = success ? new Color(.3f, 1f, .45f, .9f) : new Color(.2f, 1f, 1f, .7f);
        feedback.transform.localPosition = new Vector3(input.AimDirection.x < 0 ? -.75f : .75f, 0, 0);
        feedback.transform.localScale = new Vector3(.15f, 1.25f, 1);
    }

    // Called only by an explicitly compatible projectile or normal blade contact.
    public bool TryCounter()
    {
        if (!IsWindowOpen) return false;
        windowUntil = 0;
        feedbackUntil = Time.unscaledTime + .3f;
        CountersSucceeded++;
        if (sfx != null) sfx.Play(Sfx.Counter);
        return true;
    }

    public void CleanupAttempt()
    {
        equipped = false;
        windowUntil = readyAt = feedbackUntil = 0;
        if (feedback != null) feedback.enabled = false;
    }

    private void OnDisable() => CleanupAttempt();
}
