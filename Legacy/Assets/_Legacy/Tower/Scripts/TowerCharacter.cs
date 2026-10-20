using System;
using UnityEngine;

/// <summary>Local Tower greybox life/interaction anchor; no navigation or combat AI.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TowerCharacter : MonoBehaviour, IDamageable, IProjectileReceiver
{
    [SerializeField] protected Transform visual;
    [SerializeField] private Transform interactionAnchor;
    [Min(1)] [SerializeField] protected int maxHealth = 20;
    protected BoxCollider2D hitbox;
    private Vector3 restPosition, visualRestPosition;
    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;
    public string LethalCause { get; private set; }
    public ProjectileActor? CausalActor { get; private set; }
    public int PermissionTier { get; private set; }
    public Transform InteractionAnchor => interactionAnchor != null ? interactionAnchor : transform;
    public event Action<TowerCharacter> OnDied;

    protected virtual void Awake()
    {
        hitbox = GetComponent<BoxCollider2D>();
        restPosition = transform.localPosition;
        if (visual != null) visualRestPosition = visual.localPosition;
        ResetEncounter();
    }
    public virtual void ResetEncounter()
    {
        if (hitbox == null) return;
        CurrentHealth = maxHealth;
        LethalCause = null; CausalActor = null; PermissionTier = 0;
        hitbox.enabled = true;
        transform.localPosition = restPosition;
        if (visual == null) return;
        visual.localPosition = visualRestPosition;
        visual.localRotation = Quaternion.identity;
        foreach (var part in visual.GetComponentsInChildren<SpriteRenderer>()) part.color = Color.white;
    }
    public bool ReceiveProjectile(Projectile projectile, Collider2D contact)
    {
        if (!IsAlive || projectile.Actor == ProjectileActor.Environment) return false;
        ApplyDamage(projectile.Damage, "dead_projectile", projectile.Actor, projectile.PermissionTier);
        return true;
    }
    public void TakeDamage(int amount, GameObject source)
    {
        if (source == null || source.GetComponent<PlayerHealth>() == null) return;
        var identity = source.GetComponent<OfficerIdentity>();
        ApplyDamage(amount, "dead_attack", ProjectileActor.Player, identity?.Deployment?.BrainPermissionTier ?? 0);
    }
    protected void ApplyDamage(int amount, string cause, ProjectileActor actor, int tier)
    {
        if (!IsAlive || amount <= 0) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        if (IsAlive) return;
        LethalCause = cause; CausalActor = actor; PermissionTier = tier;
        hitbox.enabled = false;
        if (visual != null)
        {
            visual.localRotation = Quaternion.Euler(0,0,90);
            visual.localPosition = new Vector3(0,.18f,0);
            foreach (var part in visual.GetComponentsInChildren<SpriteRenderer>()) part.color = Color.gray;
        }
        OnDied?.Invoke(this);
    }
}
