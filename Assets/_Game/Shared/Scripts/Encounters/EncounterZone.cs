using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Minimal combat encounter: a trigger area that activates its pre-placed
/// enemies when the player enters and reports completion once they are all
/// dead. Reusable by any level; each level configures its own instance
/// (enemies, optional objects to disable on completion such as a barrier).
/// Enemies listed here are deactivated on Awake so they can stay visible in
/// the editor for placement. Everything resets naturally on scene reload.
/// </summary>
// FUNCTION SUMMARY
// Awake()            — hides the encounter's enemies until the player enters.
// OnTriggerEnter2D() — starts the encounter the first time the player enters the zone.
// Begin()            — activates the enemies, hands them the player, and starts counting.
// Update()           — fallback completion check in case an enemy was destroyed without firing OnDied.
// Complete()         — marks the encounter complete, disables the configured objects, fires onEncounterComplete.
[RequireComponent(typeof(Collider2D))]
public class EncounterZone : MonoBehaviour
{
    public enum EncounterState { Idle, Active, Complete }

    [Tooltip("Enemies that belong to this encounter. Kept inactive until the player enters the zone.")]
    [SerializeField] private List<Enemy> enemies = new List<Enemy>();

    [Tooltip("Objects to deactivate when the encounter is complete (e.g. a barrier wall blocking the exit).")]
    [SerializeField] private List<GameObject> disableOnComplete = new List<GameObject>();

    public UnityEvent onEncounterStarted;
    public UnityEvent onEncounterComplete;

    public EncounterState State { get; private set; } = EncounterState.Idle;
    public int RemainingEnemies { get; private set; }

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (!col.isTrigger)
            Debug.LogWarning("EncounterZone: the collider should be a trigger.", this);

        foreach (var enemy in enemies)
            if (enemy != null) enemy.gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (State != EncounterState.Idle) return;

        var player = other.GetComponentInParent<PlayerHealth>();
        if (player == null) return;

        Begin(player);
    }

    private void Begin(PlayerHealth player)
    {
        State = EncounterState.Active;
        RemainingEnemies = 0;

        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            enemy.SetTarget(player);
            enemy.gameObject.SetActive(true);
            enemy.OnDied += HandleEnemyDied;
            RemainingEnemies++;
        }

        Debug.Log($"[EncounterZone] {name}: started with {RemainingEnemies} enemies", this);
        onEncounterStarted?.Invoke();

        if (RemainingEnemies == 0)
            Complete();
    }

    private void HandleEnemyDied(Enemy enemy)
    {
        enemy.OnDied -= HandleEnemyDied;
        RemainingEnemies = Mathf.Max(RemainingEnemies - 1, 0);
        if (RemainingEnemies == 0)
            Complete();
    }

    private void Update()
    {
        if (State != EncounterState.Active) return;

        // Fallback: an enemy destroyed by something that bypassed TakeDamage still counts as gone.
        int alive = 0;
        foreach (var enemy in enemies)
            if (enemy != null && !enemy.IsDead) alive++;

        if (alive == 0)
            Complete();
    }

    private void Complete()
    {
        if (State == EncounterState.Complete) return;
        State = EncounterState.Complete;
        RemainingEnemies = 0;

        foreach (var go in disableOnComplete)
            if (go != null) go.SetActive(false);

        Debug.Log($"[EncounterZone] {name}: complete", this);
        onEncounterComplete?.Invoke();
    }
}
