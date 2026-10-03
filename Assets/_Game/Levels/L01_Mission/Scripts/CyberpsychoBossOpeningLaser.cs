using UnityEngine;

public partial class CyberpsychoBoss
{
    [Header("P1 opening laser — TEST production-greybox tuning")]
    [SerializeField, Min(.1f)] private float openingLaserChargeSeconds = 3.2f;
    [SerializeField, Min(.05f)] private float openingLaserFireSeconds = .22f;
    [SerializeField, Min(1)] private int openingLaserDamage = 28;
    private LineRenderer openingLaser;
    private Material laserMaterial;
    private bool openingLaserUsed;
    private Vector2 laserDirection, laserOrigin;
    private readonly RaycastHit2D[] laserHits = new RaycastHit2D[32];
    public Vector2 OpeningLaserDirection => laserDirection;
    public Vector2 OpeningLaserEnd { get; private set; }
    public int OpeningLaserShots { get; private set; }
    public bool OpeningLaserBlocked { get; private set; }

    public void BeginOpeningLaser()
    {
        if (openingLaserUsed || !combatEnabled || health.IsDead || player == null || player.IsDead) return;
        openingLaserUsed = true;
        if (openingLaser == null)
        {
            var go = new GameObject("OpeningLaser"); go.transform.SetParent(transform, false);
            openingLaser = go.AddComponent<LineRenderer>();
            laserMaterial = new Material(Shader.Find("Sprites/Default"));
            openingLaser.sharedMaterial = laserMaterial; openingLaser.useWorldSpace = true;
            openingLaser.positionCount = 2; openingLaser.sortingOrder = 18;
        }
        laserDirection = AimAtPlayer();
        Enter(AttackState.OpeningLaserCharge, openingLaserChargeSeconds);
        DrawOpeningLaser(false);
    }

    private bool TickOpeningLaser()
    {
        if (State != AttackState.OpeningLaserCharge && State != AttackState.OpeningLaserFire) return false;
        rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        bool charging = State == AttackState.OpeningLaserCharge;
        if (charging) laserDirection = AimAtPlayer(); // Track continuously, including the final charge frame.
        DrawOpeningLaser(!charging);
        timer -= Time.fixedDeltaTime * SimulationRate;
        if (timer > 0) return true;
        if (charging)
        {
            // Snapshot the final tracked ray once. Damage and drawing use the same nearest cover/player query.
            var contact = TraceOpeningLaser();
            OpeningLaserShots++;
            if (contact == playerCollider) player.TakeDamage(openingLaserDamage, gameObject);
            if (player.IsDead) { StopCombat(); return true; }
            Enter(AttackState.OpeningLaserFire, openingLaserFireSeconds);
            DrawOpeningLaser(true);
        }
        else
        {
            openingLaser.enabled = false;
            Enter(AttackState.Arrival, shotRecovery); // Resume the existing P1 moveset.
        }
        return true;
    }

    private Collider2D TraceOpeningLaser()
    {
        laserOrigin = rb.position;
        var filter = new ContactFilter2D { useTriggers = false };
        int count = Physics2D.Raycast(laserOrigin, laserDirection, filter, laserHits, warningRange);
        float distance = warningRange;
        Collider2D nearest = null;
        OpeningLaserBlocked = false;
        for (int i = 0; i < count; i++)
        {
            var hit = laserHits[i];
            if (hit.collider == null || hit.collider == bossCollider || hit.distance > distance) continue;
            var cover = hit.collider.GetComponentInParent<ICombatCover>();
            bool blocks = cover != null && cover.BlocksCombatRay(hit.collider);
            if (!blocks && hit.collider != playerCollider) continue;
            distance = hit.distance; nearest = hit.collider; OpeningLaserBlocked = blocks;
        }
        OpeningLaserEnd = laserOrigin + laserDirection * distance;
        return nearest;
    }

    private void DrawOpeningLaser(bool firing)
    {
        if (openingLaser == null) return;
        TraceOpeningLaser();
        openingLaser.enabled = true;
        openingLaser.SetPosition(0, laserOrigin); openingLaser.SetPosition(1, OpeningLaserEnd);
        openingLaser.widthMultiplier = firing ? .18f : .035f + .025f * Mathf.Abs(Mathf.Sin(timer * 14));
        openingLaser.startColor = openingLaser.endColor = new Color(1,1,1, firing ? 1 : .65f);
    }

    private void ClearOpeningLaser() { if (openingLaser != null) openingLaser.enabled = false; }
    private void OnDestroy() { if (laserMaterial != null) Destroy(laserMaterial); }
}
