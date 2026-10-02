using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Left Arm E action. Arm-Blade: one short slash that damages an enemy once per swing and turns hostile bullets inside
/// the slash around. Legacy Counter Arm (hidden from the catalog): the window only cancels compatible bullets on contact.
/// Both let a normal Boss blade contact be parried through TryCounter. Never grants general invulnerability.
/// </summary>
public sealed class PlayerCounterArm : MonoBehaviour, IAttemptAugmentation
{
    public const float WindowSeconds = .35f;
    public const float StartInterval = 1f;
    // TEST Arm-Blade values, not final balance.
    public const int BladeDamage = 20;
    public const float BladeReach = 1.7f;
    public const float BladeHeight = 1.7f;
    private PlayerInputReader input;
    private PlayerHealth health;
    private float windowUntil, readyAt, feedbackUntil;
    private bool narrativePaused;
    private float pauseStartedAt;
    private float Clock => narrativePaused ? pauseStartedAt : Time.unscaledTime;
    private bool equipped;
    private bool blade;
    private Vector2 slashDirection = Vector2.right;
    private readonly HashSet<Enemy> struck = new HashSet<Enemy>();
    private SpriteRenderer feedback;
    private SfxSet sfx;
    private AttemptPresentationVfx vfx;
    public bool IsEquipped => equipped && isActiveAndEnabled;
    public bool IsBlade => IsEquipped && blade;
    public bool IsWindowOpen => IsEquipped && input.GameplayInputEnabled && !health.IsDead && Clock < windowUntil;
    /// <summary>Only the legacy Counter Arm cancels a compatible bullet on body contact; Arm-Blade deflects inside its slash.</summary>
    public bool NullifiesProjectiles => IsEquipped && !blade;
    public int CountersSucceeded { get; private set; }
    /// <summary>Arm-Blade swings started (hit or miss), enemies damaged by a swing, and bullets turned around.</summary>
    public int SlashesStarted { get; private set; }
    public int BladeHits { get; private set; }
    public int Deflections { get; private set; }
    public float CooldownRemaining => Mathf.Max(0, readyAt - Clock);
    public string Cue => Clock < feedbackUntil ? "COUNTERED!"
        : IsWindowOpen ? "COUNTER WINDOW [E]" : CooldownRemaining > 0 ? $"COUNTER {CooldownRemaining:0.0}s" : "COUNTER READY [E]";

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        health = GetComponent<PlayerHealth>();
        sfx = GetComponentInParent<SfxSet>();
        vfx = GetComponentInParent<AttemptPresentationVfx>();
    }

    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        CleanupAttempt();
        blade = snapshot.HasArmBlade;
        equipped = blade || snapshot.HasCounterArm;
        CountersSucceeded = SlashesStarted = BladeHits = Deflections = 0;
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
        if (narrativePaused) return;
        if (IsEquipped && input.GameplayInputEnabled && !health.IsDead
            && input.AbilityPressedThisFrame && CooldownRemaining <= 0)
        {
            windowUntil = Time.unscaledTime + WindowSeconds;
            readyAt = Time.unscaledTime + StartInterval;
            if (blade)
            {
                // One swing along the existing aim output; the swing keeps this direction for its whole window.
                slashDirection = input.AimDirection;
                struck.Clear();
                SlashesStarted++;
                if (sfx != null) sfx.Play(Sfx.BladeSwing);
                if (vfx != null) vfx.ArmBladeSwing(transform.position, slashDirection, BladeReach);
            }
        }
        if (feedback == null) return;
        bool success = Time.unscaledTime < feedbackUntil;
        feedback.enabled = !blade && IsEquipped && (IsWindowOpen || success); // Arm-Blade shows its white swing line instead
        feedback.color = success ? new Color(.3f, 1f, .45f, .9f) : new Color(.2f, 1f, 1f, .7f);
        feedback.transform.localPosition = new Vector3(input.AimDirection.x < 0 ? -.75f : .75f, 0, 0);
        feedback.transform.localScale = new Vector3(.15f, 1.25f, 1);
    }

    private void FixedUpdate()
    {
        if (!blade || !IsWindowOpen) return;
        // Short box in front of the officer for the length of the window. Platforms and walls do not block it.
        Vector2 centre = (Vector2)transform.position + slashDirection * (BladeReach * .5f + .2f);
        float angle = Mathf.Atan2(slashDirection.y, slashDirection.x) * Mathf.Rad2Deg;
        foreach (var hit in Physics2D.OverlapBoxAll(centre, new Vector2(BladeReach, BladeHeight), angle))
        {
            var bullet = hit.GetComponent<Projectile>();
            if (bullet != null)
            {
                // Hostile bullets only, each handled once: Deflect refuses spent or already deflected bullets.
                if (bullet.OwnerLayer != gameObject.layer && bullet.Deflect(gameObject))
                {
                    Deflections++;
                    feedbackUntil = Time.unscaledTime + .3f;
                    if (sfx != null) sfx.Play(Sfx.Counter);
                    if (vfx != null) vfx.CounterSuccess(bullet.transform.position, slashDirection);
                }
                continue;
            }
            var enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || enemy.IsDead || !struck.Add(enemy)) continue; // one melee hit per target per swing
            BladeHits++;
            if (sfx != null) sfx.Play(Sfx.BulletHit);
            if (vfx != null) vfx.BulletHit(hit.ClosestPoint(transform.position), -slashDirection);
            enemy.TakeDamage(BladeDamage, gameObject);
        }
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
        narrativePaused = false;
        pauseStartedAt = 0f;
        equipped = blade = false;
        windowUntil = readyAt = feedbackUntil = 0;
        struck.Clear();
        if (feedback != null) feedback.enabled = false;
    }

    public void SetNarrativePaused(bool paused)
    {
        if (narrativePaused == paused) return;
        if (paused) pauseStartedAt = Time.unscaledTime;
        else
        {
            float duration = Time.unscaledTime - pauseStartedAt;
            windowUntil += duration;
            readyAt += duration;
            feedbackUntil += duration;
        }
        narrativePaused = paused;
    }

    private void OnDisable() => CleanupAttempt();
}
