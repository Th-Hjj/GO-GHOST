using System;
using UnityEngine;

/// <summary>One disposable combat attempt. Reports one outcome; session flow owns what happens next.</summary>
public class BossFight : MonoBehaviour
{
    public enum Outcome { Fighting, Won, Died }
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Enemy bossHealth;
    [SerializeField] private CyberpsychoBoss boss;

    public Outcome Result { get; private set; }
    public PlayerHealth Player => player;
    public Enemy BossHealth => bossHealth;
    public CyberpsychoBoss Boss => boss;
    public DeploymentSnapshot Deployment { get; private set; }
    public event Action<Outcome> OnResolved;
    private bool deathPending;
    private bool combatRunning;
    private bool subscribed;
    private bool started;
    private float combatStartedAt;
    public float CombatDuration { get; private set; }
    public int ResultBossHealth { get; private set; }
    public int ResultPlayerHealth { get; private set; }
    public float AccuracyPercent { get; private set; }

    private void Awake() => player.GetComponent<PlayerInputReader>().SetGameplayInputEnabled(false);

    public void Prepare(DeploymentSnapshot snapshot)
    {
        if (Deployment != null) throw new InvalidOperationException("Create a fresh BossFight for each officer.");
        Deployment = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        player.GetComponent<OfficerIdentity>().Assign(snapshot);
        player.GetComponent<PlayerInputReader>().BindAimTarget(bossHealth);
        player.GetComponent<PlayerDetachedDroneArm>()?.BindTarget(bossHealth);
        player.OnDeath += PlayerDied;
        bossHealth.OnDied += BossDied;
        subscribed = true;
    }

    public void BeginCombat()
    {
        if (Deployment == null || started) return;
        started = true;
        combatRunning = true;
        combatStartedAt = Time.unscaledTime;
        player.GetComponent<PlayerInputReader>().SetGameplayInputEnabled(true);
        boss.BeginCombat();
    }

    private void OnDisable()
    {
        combatRunning = false;
        if (!subscribed) return;
        if (player != null) player.OnDeath -= PlayerDied;
        if (bossHealth != null) bossHealth.OnDied -= BossDied;
        subscribed = false;
    }

    private void PlayerDied() => deathPending = true;
    private void BossDied(Enemy _) => deathPending = true;

    private void LateUpdate()
    {
        if (!combatRunning || !deathPending || Result != Outcome.Fighting) return;
        // Resolve after damage callbacks for this frame. TEST rule: mutual death is failure.
        Result = player.IsDead ? Outcome.Died : Outcome.Won;
        // Freeze the four requested result values before actor/projectile cleanup.
        CombatDuration = Time.unscaledTime - combatStartedAt;
        ResultBossHealth = bossHealth.CurrentHealth;
        ResultPlayerHealth = player.CurrentHealth;
        var shooting = player.GetComponent<PlayerShooting>();
        AccuracyPercent = shooting.ShotsFired == 0 ? 0 : 100f * shooting.ShotsHit / shooting.ShotsFired;
        CancelCombat();
        Debug.Log($"[BossFight] Officer #{Deployment.OfficerIndex}: {Result}", this);
        OnResolved?.Invoke(Result);
    }

    public void CancelCombat()
    {
        combatRunning = false;
        player.StopCombat();
        boss.StopCombat();
        bossHealth.StopCombat();
        bossHealth.GetComponent<Rigidbody2D>().simulated = false;
        foreach (var bullet in GetComponentsInChildren<Projectile>(true)) bullet.Retire();
    }
}
