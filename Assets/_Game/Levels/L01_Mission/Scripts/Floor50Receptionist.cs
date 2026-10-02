using System;
using UnityEngine;

/// <summary>Local greybox victim of an actual falling chandelier. No NPC combat/controller framework.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class Floor50Receptionist : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [Min(1)] [SerializeField] private int maxHealth = 20; // TEST, not narrative balance.
    [Min(1)] [SerializeField] private int fallingImpactDamage = 20;
    private BoxCollider2D hitbox;
    private Vector3 visualRestPosition;
    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;
    public string LethalCause { get; private set; }
    public ProjectileActor? CausalActor { get; private set; }
    public int PermissionTier { get; private set; }
    public event Action<Floor50Receptionist> OnDied;

    private void Awake()
    {
        hitbox = GetComponent<BoxCollider2D>();
        visualRestPosition = visual.localPosition;
        ResetEncounter();
    }

    public void ResetEncounter()
    {
        if (hitbox == null) return;
        CurrentHealth = maxHealth;
        LethalCause = null;
        CausalActor = null;
        PermissionTier = 0;
        hitbox.enabled = true;
        visual.localPosition = visualRestPosition;
        visual.localRotation = Quaternion.identity;
        foreach (var part in visual.GetComponentsInChildren<SpriteRenderer>()) part.color = Color.white;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAlive) return;
        var prop = other.GetComponentInParent<ChandelierEncounter>();
        var fallingBody = other.attachedRigidbody;
        if (prop == null || prop.IsHanging || prop.HasLanded || fallingBody == null
            || fallingBody.bodyType != RigidbodyType2D.Dynamic || fallingBody.linearVelocity.y >= -.1f) return;
        // Trigger overlap is a real physical contact. Neither dialogue nor release alone reaches this damage path.
        CurrentHealth = Mathf.Max(0, CurrentHealth - fallingImpactDamage);
        if (IsAlive) return;
        LethalCause = "dead_chandelier";
        CausalActor = prop.LastActor;
        PermissionTier = prop.LastPermissionTier;
        hitbox.enabled = false;
        visual.localRotation = Quaternion.Euler(0, 0, 90);
        visual.localPosition = new Vector3(0, .18f, 0);
        foreach (var part in visual.GetComponentsInChildren<SpriteRenderer>()) part.color = Color.gray;
        OnDied?.Invoke(this);
    }
    // No IDamageable/IProjectileReceiver: ordinary bullets pass through this optional direct-fire target for now.
}
