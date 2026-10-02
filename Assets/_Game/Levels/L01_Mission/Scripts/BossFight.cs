using System;
using UnityEngine;

/// <summary>One disposable combat attempt. Reports one outcome; session flow owns what happens next.</summary>
public partial class BossFight : MonoBehaviour
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
    private bool practicing;
    private float transitionDuration;
    private Vector2 playerSpawn;
    private float combatStartedAt;
    public float CombatDuration { get; private set; }
    public bool NarrativePaused { get; private set; }
    public float CombatElapsed => !started ? 0f : !combatRunning ? CombatDuration
        : Mathf.Max(0f, (NarrativePaused ? pauseStartedAt : Time.unscaledTime) - combatStartedAt - pausedSeconds);
    private float pauseStartedAt, pausedSeconds, previousTimeScale;
    public int ResultBossHealth { get; private set; }
    public int ResultPlayerHealth { get; private set; }
    public float AccuracyPercent { get; private set; }

    private void Awake()
    {
        playerSpawn = player.transform.position;
        player.GetComponent<PlayerInputReader>().SetGameplayInputEnabled(false);
    }

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
        var sfx = GetComponent<SfxSet>();
        if (sfx != null) sfx.BeginScope(); // this attempt's own voices: practice and combat
        var vfx = GetComponent<AttemptPresentationVfx>();
        if (vfx != null) vfx.BeginAttempt(); // practice shows the officer's own effects
    }

    /// <summary>Puts the officer (and its Drone unit) at the arena spawn plus an offset. The attempt lifecycle is untouched.</summary>
    public void PlacePlayer(Vector2 offsetFromSpawn)
    {
        Vector2 target = playerSpawn + offsetFromSpawn;
        Vector3 delta = target - (Vector2)player.transform.position;
        player.transform.position += delta;
        player.GetComponent<Rigidbody2D>().position = target;
        var drone = player.GetComponent<PlayerDetachedDroneArm>();
        if (drone != null && drone.Unit != null) drone.Unit.position += delta;
    }

    /// <summary>Practice room: formal player control with the locked build. No Boss, combat timer or statistics.</summary>
    public void BeginPractice()
    {
        if (Deployment == null || started || practicing) return;
        practicing = true;
        // An inactive Boss is the existing "no valid target" case: AI aim holds, the Drone does not fire, nothing can hit it.
        boss.gameObject.SetActive(false);
        player.GetComponent<PlayerInputReader>().SetGameplayInputEnabled(true);
    }

    public void BeginCombat()
    {
        if (Deployment == null || started) return;
        started = true;
        if (practicing)
        {
            practicing = false;
            // Practice -> combat boundary only. Practice shots never reach the Boss room or the result statistics.
            foreach (var bullet in GetComponentsInChildren<Projectile>(true)) bullet.Retire();
            var shooting = player.GetComponent<PlayerShooting>();
            shooting.ResetShotCounters();
            shooting.ApplyDeployment(Deployment); // ends an unfinished reload and refills the magazine
            // Same officer and locked build: end running effects, clear cooldowns, refill air resources and HP.
            player.GetComponent<PlayerDodge>().ResetCooldown();
            player.GetComponent<PlayerTimeSlow>().ApplyDeployment(Deployment);
            player.GetComponent<PlayerCounterArm>().ApplyDeployment(Deployment);
            player.GetComponent<PlayerMovement>().ApplyDeployment(Deployment);
            player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            player.Heal(player.MaxHealth);
            boss.gameObject.SetActive(true);
        }
        combatRunning = true;
        combatStartedAt = Time.unscaledTime;
        pausedSeconds = 0f; // A developer menu pause in practice is outside the combat clock.
        var vfx = GetComponent<AttemptPresentationVfx>();
        if (vfx != null) vfx.BeginAttempt(); // drops any practice leftovers, then draws for the real combat
        // Walking in from practice keeps live input; a fresh enable would wait for every key to be released.
        var reader = player.GetComponent<PlayerInputReader>();
        if (!reader.GameplayInputEnabled) reader.SetGameplayInputEnabled(true);
        if (!TowerMission) boss.BeginCombat();
    }

    private void OnDisable()
    {
        SetNarrativePaused(false);
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
        if (TowerMission) { TickTowerMission(); return; }
        if (NarrativePaused && !deathPending) return;
        if (combatRunning && boss.Phase == CyberpsychoBoss.BossPhase.PhaseTransition)
        {
            // B's transition presentation only follows the real transition clock; it never drives the phase.
            if (transitionDuration <= 0f) transitionDuration = Mathf.Max(.01f, boss.PhaseTimeRemaining);
            boss.PresentPhaseTransition(1f - boss.PhaseTimeRemaining / transitionDuration);
        }
        if (combatRunning && bossHealth.AwaitingSecondPhase)
        {
            // Phase 1 health is spent. Until the Boss's own transition ends it is immune (Enemy rejects all damage) and
            // no Phase 1 bullet may still land; then Phase 2 starts with its own full health. Same fight, same statistics.
            int hostileLayer = bossHealth.gameObject.layer;
            foreach (var bullet in GetComponentsInChildren<Projectile>())
                if (bullet.OwnerLayer == hostileLayer) bullet.Retire();
            if (boss.Phase == CyberpsychoBoss.BossPhase.Phase2) bossHealth.BeginSecondPhase();
        }
        if (!combatRunning || !deathPending || Result != Outcome.Fighting) return;
        // Resolve after damage callbacks for this frame. TEST rule: mutual death is failure.
        Result = player.IsDead ? Outcome.Died : Outcome.Won;
        // Freeze the four requested result values before actor/projectile cleanup.
        CombatDuration = CombatElapsed;
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
        SetNarrativePaused(false);
        combatRunning = false;
        var sfx = GetComponent<SfxSet>();
        if (sfx != null) sfx.EndScope(); // no combat voice survives death, victory or disposal
        var vfx = GetComponent<AttemptPresentationVfx>();
        if (vfx != null) vfx.EndAttempt();
        player.StopCombat();
        if (boss != null) boss.StopCombat();
        if (bossHealth != null) bossHealth.StopCombat();
        if (bossHealth != null) bossHealth.GetComponent<Rigidbody2D>().simulated = false;
        foreach (var bullet in GetComponentsInChildren<Projectile>(true)) bullet.Retire();
    }

    /// <summary>Reversible dialogue pause. No actor disable, terminal cleanup or ability reset.</summary>
    public void SetNarrativePaused(bool paused)
    {
        if (NarrativePaused == paused || (paused && !combatRunning && !practicing)) return;
        if (paused)
        {
            pauseStartedAt = Time.unscaledTime;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            pausedSeconds += Time.unscaledTime - pauseStartedAt;
            Time.timeScale = previousTimeScale;
        }
        NarrativePaused = paused;
        if (player != null)
        {
            player.GetComponent<PlayerInputReader>().SetNarrativePaused(paused);
            player.GetComponent<PlayerTimeSlow>()?.SetNarrativePaused(paused);
            player.GetComponent<PlayerCounterArm>()?.SetNarrativePaused(paused);
        }
    }
}
