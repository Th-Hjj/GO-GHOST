using UnityEngine;

/// <summary>Same local life state, with opt-in damage from the actual falling chandelier.</summary>
public sealed class Floor50Receptionist : TowerCharacter
{
    [Min(1)] [SerializeField] private int fallingImpactDamage = 20;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAlive) return;
        var prop = other.GetComponentInParent<ChandelierEncounter>();
        var body = other.attachedRigidbody;
        if (prop == null || prop.IsHanging || prop.HasLanded || body == null
            || body.bodyType != RigidbodyType2D.Dynamic || body.linearVelocity.y >= -.1f) return;
        ApplyDamage(fallingImpactDamage, "dead_chandelier", prop.LastActor, prop.LastPermissionTier);
    }
}
