using UnityEngine;

public partial class CyberpsychoBoss
{
    private const float CamoRushSpeed = 36f;
    private const int CamoRushDamage = 20;
    private const float CamoRushTell = 1f;
    private static readonly Vector2 CamoRushHitSize = new Vector2(1.4f, 1f);
    private const float CamoRushBladeOffset = .65f;
    private readonly Collider2D[] camoRushContacts = new Collider2D[8];
    private Vector2 camoRushFrom, camoRushTo, camoRushPrevious;
    private bool camoRushOwnsBody, camoRushHit, camoRushRevealed;
    private RigidbodyType2D camoRushPreviousBody;
    private int p2ComboOpenings;
    public int CamoRushesStarted { get; private set; }
    public int CamoRushHitAttempts { get; private set; }
    public Vector2 CamoRushFrom => camoRushFrom;
    public Vector2 CamoRushTo => camoRushTo;
    private bool CamoRushActive => State == AttackState.CamoRushWarning || State == AttackState.CamoRushExecute;

    private void BeginP2Opening()
    {
        // Two short reposition openings, then one rush. BLACKOUT's exit always gets a short opening.
        if (!BlackoutActive && ++p2ComboOpenings % 3 == 0 && TryBeginCamoRush()) return;
        BeginCamoReposition();
    }

    private bool TryBeginCamoRush()
    {
        if (!arenaBound || bossCollider == null || playerCollider == null
            || !combatEnabled || health.IsDead || player.IsDead) return false;
        RestoreDropBody();
        RestorePlayerCollision();
        Bounds bodyBounds = bossCollider.bounds;
        Vector2 offset = (Vector2)bodyBounds.center - rb.position;
        float left = blackoutArena.min.x + bodyBounds.extents.x - offset.x + .15f;
        float right = blackoutArena.max.x - bodyBounds.extents.x - offset.x - .15f;
        float bottom = blackoutArena.min.y + bodyBounds.extents.y - offset.y + .05f;
        float top = blackoutArena.max.y - bodyBounds.extents.y - offset.y - .05f;
        if (right - left < 4f || top <= bottom) return false;

        // Lock once, before the tell. Start on the side farther from the sampled player.
        chargeDirection = playerCollider.bounds.center.x >= blackoutArena.center.x ? 1f : -1f;
        float y = Mathf.Clamp(playerCollider.bounds.center.y, bottom, top);
        camoRushFrom = new Vector2(chargeDirection > 0 ? left : right, y);
        camoRushTo = new Vector2(chargeDirection > 0 ? right : left, y);
        camoRushPrevious = camoRushFrom;
        camoRushHit = camoRushRevealed = false;
        camoRushPreviousBody = rb.bodyType;
        camoRushOwnsBody = true;
        rb.bodyType = RigidbodyType2D.Kinematic; // Same temporary motion override as the existing Drop.
        rb.linearVelocity = Vector2.zero;
        IgnorePlayerCollision(); // Only the body pair; leave the collider, layers and target active.
        rb.position = camoRushFrom;
        Enter(AttackState.CamoRushWarning, CamoRushTell);
        return true;
    }

    private void TickCamoRush(float dt)
    {
        rb.linearVelocity = Vector2.zero;
        if (State == AttackState.CamoRushWarning)
        {
            timer -= dt;
            if (timer <= 0)
            {
                CamoRushesStarted++;
                camoRushPrevious = rb.position;
                Enter(AttackState.CamoRushExecute, Vector2.Distance(camoRushFrom, camoRushTo) / CamoRushSpeed);
            }
            else ShowWarnings();
            return; // No damage on the warning-to-execution boundary.
        }

        Vector2 next = Vector2.MoveTowards(rb.position, camoRushTo, CamoRushSpeed * dt);
        rb.position = next;
        ResolveCamoRushHit(camoRushPrevious, next);
        if (!combatEnabled || health.IsDead || player == null || player.IsDead) { StopCombat(); return; }
        if (State != AttackState.CamoRushExecute) return; // A parry may already have ended the rush.
        camoRushPrevious = next;
        timer = Vector2.Distance(next, camoRushTo) / CamoRushSpeed;
        if (timer <= .0001f) FinishCamoRush();
        else ShowWarnings();
    }

    private void ResolveCamoRushHit(Vector2 previous, Vector2 current)
    {
        if (camoRushHit) return;
        Vector2 center = (previous + current) * .5f + Vector2.right * (chargeDirection * CamoRushBladeOffset);
        Vector2 size = CamoRushHitSize + new Vector2(Mathf.Abs(current.x - previous.x), 0f);
        var filter = new ContactFilter2D();
        filter.SetLayerMask(1 << playerCollider.gameObject.layer);
        filter.useTriggers = true;
        int count = Physics2D.OverlapBox(center, size, 0f, filter, camoRushContacts);
        for (int i = 0; i < count; i++)
        {
            var contact = camoRushContacts[i];
            if (contact != playerCollider && contact.GetComponentInParent<PlayerHealth>() != player) continue;
            camoRushHit = true; // Consume before callbacks, including a rejected hit during Dodge.
            CamoRushHitAttempts++;
            // Preserve the existing normal-blade parry rule; this is not a BLACKOUT cut.
            if (playerCounter != null && playerCounter.TryCounter())
            {
                BladeParries++;
                if (vfx != null) vfx.CounterSuccess(playerCollider.ClosestPoint(current), Vector2.right * -chargeDirection);
                FinishCamoRush();
                return;
            }
            player.TakeDamage(CamoRushDamage, gameObject);
            return;
        }
    }

    private void FinishCamoRush()
    {
        EndCamoRushMotion();
        camoRushRevealed = true;
        // Fully visible for this entire existing .55 s tell. Lock the next attack now, never at execution.
        LockSlashWarning();
    }

    private void EndCamoRushMotion()
    {
        if (!camoRushOwnsBody) return;
        camoRushOwnsBody = false;
        if (rb != null) { rb.bodyType = camoRushPreviousBody; rb.linearVelocity = Vector2.zero; }
        RestorePlayerCollision();
        if (vfx != null) vfx.SetGhosting(false);
    }

    private void ClearCamoRush()
    {
        EndCamoRushMotion();
        camoRushRevealed = camoRushHit = false;
        camoRushFrom = camoRushTo = camoRushPrevious = Vector2.zero;
        p2ComboOpenings = 0;
    }

    private void ShowCamoRushWarning()
    {
        // The existing white warning sprite covers exactly the union of the locked swept blade boxes.
        Vector2 center = (camoRushFrom + camoRushTo) * .5f + Vector2.right * (chargeDirection * CamoRushBladeOffset);
        chargeWarning.enabled = true;
        chargeWarning.color = new Color(1f, 1f, 1f, .14f);
        chargeWarning.transform.position = new Vector3(center.x, center.y, 0);
        chargeWarning.transform.localScale = new Vector3(
            (Mathf.Abs(camoRushTo.x - camoRushFrom.x) + CamoRushHitSize.x) / Mathf.Abs(transform.lossyScale.x),
            CamoRushHitSize.y / Mathf.Abs(transform.lossyScale.y), 1f);
    }
}
