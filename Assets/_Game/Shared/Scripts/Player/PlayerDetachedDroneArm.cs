using UnityEngine;

/// <summary>Left Arm becomes one disposable autonomous companion. Never reads or writes player aim/fire input.</summary>
public sealed class PlayerDetachedDroneArm : MonoBehaviour, IAttemptAugmentation
{
    [SerializeField] private Projectile bulletPrefab;
    // TEST values: follow offset, fire rate and damage are not final balance.
    private readonly Vector2 offset = new Vector2(1.3f, 1f);
    private const float FollowSpeed = 10f, FireInterval = .7f, BulletSpeed = 18f;
    private const int Damage = 6;
    private PlayerHealth health;
    private PlayerInputReader input;
    private SpriteRenderer arm;
    private bool armWasEnabled;
    private bool equipped;
    private Enemy target;
    private Collider2D targetCollider;
    private float readyAt;
    public Transform Unit { get; private set; }
    public int ShotsFired { get; private set; }
    public Vector2 AimDirection { get; private set; }
    public bool IsEquipped => equipped;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        input = GetComponent<PlayerInputReader>();
    }

    public void ApplyDeployment(DeploymentSnapshot snapshot)
    {
        CleanupAttempt();
        equipped = snapshot.HasDetachedDrone;
        ShotsFired = 0;
        if (!equipped) return;
        arm = GetComponent<OfficerVisual>()?.Part(LoadoutSlot.LeftArm);
        armWasEnabled = arm != null && arm.enabled;
        var unit = new GameObject("DetachedDrone_TEST");
        unit.layer = gameObject.layer;
        Unit = unit.transform;
        Unit.SetParent(transform.parent, true); // sibling of the officer, owned by this attempt
        Unit.position = (Vector2)transform.position + offset;
        var visual = unit.AddComponent<SpriteRenderer>();
        var source = arm != null ? arm : GetComponent<SpriteRenderer>();
        visual.sprite = source.sprite;
        visual.sharedMaterial = source.sharedMaterial;
        visual.sortingLayerID = source.sortingLayerID;
        visual.sortingOrder = source.sortingOrder + 1;
        visual.color = OfficerVisual.SlotColor(LoadoutSlot.LeftArm);
        Unit.localScale = new Vector3(.8f, .22f, 1);
        if (arm != null) arm.enabled = false;
        readyAt = 0;
    }

    // The existing fight binds its sole Enemy once. Shared code does not know about level Boss logic.
    public void BindTarget(Enemy boss)
    {
        target = boss;
        targetCollider = boss != null ? boss.GetComponent<Collider2D>() : null;
    }

    private void Update()
    {
        if (!equipped || Unit == null || health.IsDead) return;
        Unit.position = Vector2.MoveTowards(Unit.position, (Vector2)transform.position + offset, FollowSpeed * Time.deltaTime);
        if (!input.GameplayInputEnabled || target == null || target.IsDead || !target.isActiveAndEnabled) return;
        Vector2 point = targetCollider != null ? targetCollider.bounds.center : target.transform.position;
        Vector2 delta = point - (Vector2)Unit.position;
        if (delta.sqrMagnitude < .001f) return;
        AimDirection = delta.normalized;
        Unit.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        if (Time.time < readyAt || bulletPrefab == null) return;
        readyAt = Time.time + FireInterval;
        var bullet = Instantiate(bulletPrefab, (Vector2)Unit.position + AimDirection * .5f, Unit.rotation);
        // Player ownership supplies the existing team/lifetime/attempt cleanup rules.
        bullet.Launch(AimDirection, BulletSpeed, Damage, gameObject);
        var tint = bullet.GetComponent<SpriteRenderer>();
        if (tint != null) tint.color = OfficerVisual.SlotColor(LoadoutSlot.LeftArm);
        ShotsFired++;
    }

    public void CleanupAttempt()
    {
        equipped = false;
        target = null;
        targetCollider = null;
        readyAt = 0;
        AimDirection = Vector2.zero;
        if (Unit != null)
        {
            Unit.gameObject.SetActive(false);
            Destroy(Unit.gameObject);
            Unit = null;
        }
        if (arm != null) arm.enabled = armWasEnabled;
        arm = null;
    }

    private void OnDisable() => CleanupAttempt();
}
