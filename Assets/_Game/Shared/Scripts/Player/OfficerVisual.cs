using UnityEngine;

/// <summary>Lightweight visual-only stickman poses. Movement and weapon execution own all gameplay.</summary>
public sealed class OfficerVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer leftArm;
    [SerializeField] private SpriteRenderer rightArm;
    [SerializeField] private SpriteRenderer leftLeg;
    [SerializeField] private SpriteRenderer rightLeg;
    [SerializeField] private SpriteRenderer torso;
    [SerializeField] private SpriteRenderer head;
    [SerializeField] private SpriteRenderer weapon;
    [SerializeField] private SpriteRenderer weaponGrip;
    private bool reloading;
    private float reloadVisualStartedAt;
    private bool animate, wasGrounded = true;
    private PlayerMovement movement;
    private PlayerInputReader input;
    private PlayerShooting shooting;
    private PlayerDodge dodge;
    private PlayerHealth health;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private SpriteRenderer leftForearm, rightForearm, leftCalf, rightCalf;
    private SpriteRenderer cylinder, cylinderHalo, triggerPalm, supportPalm;
    private float clock, gait, stride, landing, recoil;
    private int lastShots;
    private SpriteRenderer[] glowParts;
    private Material[] originalMaterials;
    private MaterialPropertyBlock glowBlock;
    private System.Func<ControlOperation, ConditionalVisualFeedback> feedback;
    private SpriteRenderer spineSignal;
    private bool glowBound;
    private static readonly int GlowStrength=Shader.PropertyToID("_AIStrength");
    private static readonly int GlowColor=Shader.PropertyToID("_AIColor");
    public void ConfigureConditionalGlow(Material material, System.Func<ControlOperation, ConditionalVisualFeedback> source = null)
    {
        if(!animate||material==null)return;
        ClearConditionalGlow();
        if(spineSignal==null){spineSignal=AddSegment(torso,"SpineSignal");spineSignal.sortingOrder=torso.sortingOrder+1;}
        spineSignal.enabled=true;
        // Keep the approved gun silhouette gray-blue; weapon signals live only in its core.
        glowParts=new[]{leftLeg,rightLeg,leftCalf,rightCalf,leftArm,rightArm,leftForearm,rightForearm,cylinderHalo,cylinder,spineSignal,supportPalm,triggerPalm};
        originalMaterials=new Material[glowParts.Length];glowBlock=new MaterialPropertyBlock();
        for(int i=0;i<glowParts.Length;i++)if(glowParts[i]!=null){originalMaterials[i]=glowParts[i].sharedMaterial;glowParts[i].sharedMaterial=material;}
        feedback=source;health.OnDeath+=ClearConditionalGlow;glowBound=true;
    }
    public void ClearConditionalGlow()
    {
        if(glowBound&&health!=null)health.OnDeath-=ClearConditionalGlow;
        glowBound=false;feedback=null;
        if(glowParts!=null)for(int i=0;i<glowParts.Length;i++)if(glowParts[i]!=null){glowParts[i].SetPropertyBlock(null);glowParts[i].sharedMaterial=originalMaterials[i];}
        if(spineSignal!=null)spineSignal.enabled=false;
        glowParts=null;originalMaterials=null;
        if (health != null && health.IsDead && cylinderHalo != null) cylinderHalo.enabled = false;
    }
    void OnDestroy()=>ClearConditionalGlow();
    void OnDisable(){SetReloading(false);if(cylinderHalo!=null)cylinderHalo.enabled=false;}
    void UpdateConditionalGlow()
    {
        if(!glowBound||feedback==null)return;
        var move=feedback(ControlOperation.Movement);var jump=feedback(ControlOperation.Jump);
        var aim=feedback(ControlOperation.Aim);var fire=feedback(ControlOperation.Fire);var spine=feedback(ControlOperation.Dodge);
        float legs=Mathf.Max(Mathf.Max(move.Signal,jump.Matched?.2f:0),Mathf.Max(move.Afterglow*.22f,Mathf.Max(jump.Pulse*.95f,jump.Afterglow*.22f)));
        float arms=Mathf.Max(Mathf.Max(aim.Signal,fire.Signal),Mathf.Max(aim.Afterglow*.22f,fire.Afterglow*.22f));
        float dodgeSignal=Mathf.Max(spine.Matched?.2f:0,Mathf.Max(spine.Pulse,spine.Afterglow*.22f));
        for(int i=0;i<glowParts.Length;i++)
        {
            float strength=i<4?legs:i<8||i>10?arms:i<10?Mathf.Max(fire.Signal*.7f,Mathf.Max(fire.Pulse,fire.Afterglow*.22f)):dodgeSignal;
            // Keep the gun's reload signal red while arm feedback continues independently.
            if(reloading&&(i==8||i==9))strength=0;
            var renderer=glowParts[i];if(renderer==null)continue;
            if(i==10)renderer.enabled=strength>.001f;
            renderer.GetPropertyBlock(glowBlock);glowBlock.SetFloat(GlowStrength,Mathf.Clamp01(strength));
            glowBlock.SetColor(GlowColor,i<4?NeuralPalette.Legs:i==10?NeuralPalette.Spine:NeuralPalette.Arms);renderer.SetPropertyBlock(glowBlock);
        }
    }
    public bool ReloadVisible => reloading && weapon != null && weapon.enabled && weapon.gameObject.activeInHierarchy;

    static Sprite neutralHeadSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetNeutralHeadSprite()
    {
        if (neutralHeadSprite != null) { Destroy(neutralHeadSprite.texture); Destroy(neutralHeadSprite); }
        neutralHeadSprite = null;
    }

    static Sprite NeutralHeadMask(Sprite source)
    {
        if (neutralHeadSprite != null) return neutralHeadSprite;
        if (source == null || !source.texture.isReadable) return source;
        // The built-in Knob contains gray RGB. Keep its exact alpha and source rectangle, so the
        // shared palette alone controls head/hand color without changing their silhouette.
        var pixels = source.texture.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, pixels[i].a);
        var texture = new Texture2D(source.texture.width, source.texture.height, TextureFormat.RGBA32, false);
        texture.name = "Player Neutral Head Mask";
        texture.filterMode = source.texture.filterMode;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels); texture.Apply(false, true);
        neutralHeadSprite = Sprite.Create(texture, source.rect, source.pivot / source.rect.size,
            source.pixelsPerUnit, 0, SpriteMeshType.FullRect, source.border);
        neutralHeadSprite.name = "Player Neutral Head";
        return neutralHeadSprite;
    }

    public void ConfigureLegacyCombat()
    {
        foreach (var part in new[] { leftArm, rightArm, leftLeg, rightLeg, torso, head, weapon, weaponGrip })
            if (part != null) part.color = NeuralPalette.Primary;
        SetReloading(false);
        movement = GetComponent<PlayerMovement>();
        input = GetComponent<PlayerInputReader>();
        shooting = GetComponent<PlayerShooting>();
        dodge = GetComponent<PlayerDodge>();
        health = GetComponent<PlayerHealth>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        animate = movement != null && input != null && shooting != null && body != null
            && leftArm != null && rightArm != null && leftLeg != null && rightLeg != null
            && torso != null && head != null && weapon != null && weaponGrip != null;
        if (!animate) return;
        head.sprite = NeutralHeadMask(head.sprite);
        if (leftForearm == null) leftForearm = AddSegment(leftArm, "LeftForearm");
        if (rightForearm == null) rightForearm = AddSegment(rightArm, "RightForearm");
        if (leftCalf == null) leftCalf = AddSegment(leftLeg, "LeftCalf");
        if (rightCalf == null) rightCalf = AddSegment(rightLeg, "RightCalf");
        if (cylinder == null)
        {
            cylinder = AddSegment(head, "RevolverCylinder");
            cylinder.transform.SetParent(weapon.transform.parent, false);
            cylinder.sortingOrder = weapon.sortingOrder + 1;
        }
        weapon.sprite = RevolverPresentationGeometry.Body;
        weaponGrip.enabled = false; // The connected silhouette includes the complete grip.
        cylinder.sprite = RevolverPresentationGeometry.Core;
        cylinder.sortingOrder = weapon.sortingOrder + 2;
        if (cylinderHalo == null) {
            cylinderHalo = AddSegment(cylinder, "RevolverCoreHalo");
            cylinderHalo.sortingOrder = weapon.sortingOrder + 1;
        }
        cylinderHalo.sprite = RevolverPresentationGeometry.Halo;
        foreach (var arm in new[] { leftArm, leftForearm, rightArm, rightForearm })
            arm.sprite = RevolverPresentationGeometry.ArmSegment;
        leftArm.flipX = rightArm.flipX = false;
        leftForearm.flipX = rightForearm.flipX = true;
        if (supportPalm == null) supportPalm = AddSegment(head, "RevolverSupportHand");
        if (triggerPalm == null) triggerPalm = AddSegment(head, "RevolverTriggerHand");
        foreach (var palm in new[] { supportPalm, triggerPalm }) {
            palm.transform.SetParent(weapon.transform.parent, false);
            palm.sortingOrder = weapon.sortingOrder + 3;
            palm.color = NeuralPalette.Primary;
        }
        UpdateReloadVisual();
        clock = gait = stride = landing = recoil = 0;
        wasGrounded = true;
        lastShots = shooting.ShotsFired;
        Pose(true, Vector2.zero, 0);
    }

    // Physical weapon feedback, shared by the player and the observer. It never changes muzzle aim.
    public void SetReloading(bool value)
    {
        if (value && !reloading) reloadVisualStartedAt = Time.time;
        reloading = value;
        UpdateReloadVisual();
        UpdateConditionalGlow();
    }

    private void LateUpdate()
    {
        if (animate)
        {
            if (Time.deltaTime <= 0 || input.NarrativePaused || (health != null && health.IsDead)) return;
            if (!movement.isActiveAndEnabled) return;
            // Preparation has frozen physics, but the weapon anchor still updates before this view.
            if (!body.simulated) { Pose(true, Vector2.zero, 0); return; }
            float dt = Time.deltaTime;
            clock += dt;
            Vector2 velocity = body.linearVelocity;
            bool grounded = movement.IsGrounded;
            landing = Mathf.MoveTowards(landing, 0, dt / .18f);
            if (grounded && !wasGrounded) landing = 1;
            wasGrounded = grounded;
            bool dodging = dodge != null && dodge.IsDodging;
            stride = Mathf.MoveTowards(stride, grounded && !dodging ? Mathf.Clamp01(Mathf.Abs(velocity.x) / 7f) : 0, dt * 12);
            if (grounded && !dodging) gait += velocity.x * dt * 2.8f;
            recoil = Mathf.MoveTowards(recoil, 0, dt / .12f);
            if (shooting.ShotsFired != lastShots) { recoil = 1; lastShots = shooting.ShotsFired; }
            Pose(grounded, velocity, dodging ? Mathf.Sign(velocity.x) : 0);
        }
        if (reloading) UpdateReloadVisual();
        UpdateConditionalGlow();
    }

    private SpriteRenderer AddSegment(SpriteRenderer source, string label)
    {
        var part = new GameObject(label).AddComponent<SpriteRenderer>();
        part.transform.SetParent(source.transform.parent, false);
        part.sprite = source.sprite;
        part.sharedMaterial = source.sharedMaterial;
        part.sortingLayerID = source.sortingLayerID;
        part.sortingOrder = source.sortingOrder;
        part.color = source.color;
        return part;
    }

    private void Pose(bool grounded, Vector2 velocity, float dashDirection)
    {
        Vector2 aim = shooting.CurrentBarrelDirection;
        int facing = movement.FacingDirection;
        // Arms stay attached to the independently aimed Revolver when the legs face the other way.
        int aimFacing = shooting.CurrentWeaponFacing;
        float wave = Mathf.Sin(gait), lift = Mathf.Cos(gait);
        float crouch = landing * .12f + (dashDirection != 0 ? .17f : 0);
        float breath = Mathf.Sin(clock * 2.4f) * .009f;
        float lean = Mathf.Clamp(velocity.x * .009f, -.07f, .07f) + dashDirection * .15f;
        Vector2 hip = new Vector2(0, -.12f - crouch + Mathf.Abs(wave) * stride * .025f);
        Vector2 chest = new Vector2(lean, .34f - crouch + breath) - aim * (recoil * .035f);
        Vector2 headPoint = new Vector2(lean * .8f, .57f - crouch + breath) - aim * (recoil * .02f);
        float floor = bodyCollider != null ? transform.InverseTransformPoint(bodyCollider.bounds.min).y + (grounded ? .025f : .075f) : -.87f;
        Vector2 front = new Vector2(.15f * facing + wave * .30f * stride, floor + Mathf.Max(0, lift) * .15f * stride);
        Vector2 back = new Vector2(-.15f * facing - wave * .30f * stride, floor + Mathf.Max(0, -lift) * .15f * stride);
        if (!grounded)
        {
            float rising = Mathf.InverseLerp(-2, 3, velocity.y);
            front = new Vector2(.25f * facing, floor + Mathf.Lerp(.10f, .34f, rising));
            back = new Vector2(-.22f * facing, floor + Mathf.Lerp(.26f, .14f, rising));
        }
        if (dashDirection != 0)
        {
            front.x = dashDirection * .40f;
            back.x = -dashDirection * .42f;
            if (!grounded) { front.y = floor + .28f; back.y = floor + .18f; }
        }
        Segment(torso, hip, chest + Vector2.up * .035f, .17f);
        if(spineSignal!=null)Segment(spineSignal,hip,chest+Vector2.up*.035f,.055f);
        Place(head, headPoint, new Vector2(.46f, .46f), 0);
        Limb(leftLeg, leftCalf, hip + Vector2.left * (.075f * facing), back, new Vector2(.12f * facing, 0), .17f);
        Limb(rightLeg, rightCalf, hip + Vector2.right * (.075f * facing), front, new Vector2(.18f * facing, 0), .17f);

        // FirePoint remains entirely owned by PlayerShooting. The barrel ends at its real muzzle.
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = new Vector3(1, aimFacing, 1);
        Vector2 corePoint = RevolverPresentationGeometry.CorePosition;
        cylinder.transform.localPosition = new Vector3(corePoint.x, corePoint.y * aimFacing, 0);
        cylinder.transform.localRotation = Quaternion.identity;
        cylinder.transform.localScale = Vector3.one;
        cylinderHalo.transform.localPosition = cylinder.transform.localPosition;
        cylinderHalo.transform.localRotation = Quaternion.identity;
        cylinderHalo.transform.localScale = Vector3.one;
        cylinderHalo.enabled = isActiveAndEnabled && (health == null || !health.IsDead);
        var anchor = weapon.transform.parent;
        Vector2 grip = RevolverPresentationGeometry.GripPosition;
        Vector2 support = RevolverPresentationGeometry.SupportPosition;
        Vector2 triggerHand = transform.InverseTransformPoint(anchor.TransformPoint(new Vector3(grip.x, grip.y * aimFacing, 0)));
        Vector2 supportHand = transform.InverseTransformPoint(anchor.TransformPoint(new Vector3(support.x, support.y * aimFacing, 0)));
        PositionPalm(triggerPalm, grip, aimFacing, new Vector2(.10f,.075f));
        PositionPalm(supportPalm, support, aimFacing, new Vector2(.095f,.065f));
        Arm(leftArm, leftForearm, chest + new Vector2(-.10f * aimFacing, -.04f), supportHand, aimFacing);
        Arm(rightArm, rightForearm, chest + new Vector2(.08f * aimFacing, -.04f), triggerHand, aimFacing);
    }

    private static void PositionPalm(SpriteRenderer palm, Vector2 point, int facing, Vector2 size)
    {
        palm.transform.localPosition = new Vector3(point.x, point.y * facing, 0);
        palm.transform.localRotation = Quaternion.Euler(0, 0, 12 * facing);
        Vector2 source = palm.sprite.bounds.size;
        palm.transform.localScale = new Vector3(size.x/source.x,size.y/source.y,1);
    }

    private void Arm(SpriteRenderer upper, SpriteRenderer lower, Vector2 shoulder, Vector2 hand, int facing)
    {
        // Two .30-unit bones: aiming/reloading bends the elbow instead of scaling a midpoint offset.
        const float length = .30f;
        Vector2 delta = hand - shoulder;
        float distance = delta.magnitude;
        Vector2 direction = distance > .0001f ? delta / distance : Vector2.right * facing;
        float half = distance * .5f;
        float bend = Mathf.Sqrt(Mathf.Max(0, length * length - half * half));
        Vector2 normal = new Vector2(-direction.y, direction.x);
        // Stable lower/outside elbow solution for each handedness, including vertical aim.
        Vector2 elbow = (shoulder + hand) * .5f - normal * (bend * facing);
        RoundedArmSegment(upper, shoulder, elbow);
        RoundedArmSegment(lower, elbow, hand);
    }

    private void RoundedArmSegment(SpriteRenderer part, Vector2 start, Vector2 end)
    {
        const float width = .16f;
        Vector2 delta = end - start;
        Place(part, (start + end) * .5f, new Vector2(delta.magnitude + .0125f + width*.5f, width),
            Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void Limb(SpriteRenderer upper, SpriteRenderer lower, Vector2 start, Vector2 end, Vector2 bend, float width)
    {
        Vector2 joint = (start + end) * .5f + bend;
        Segment(upper, start, joint, width);
        Segment(lower, joint, end, width);
    }

    private void Segment(SpriteRenderer part, Vector2 start, Vector2 end, float width)
    {
        Vector2 delta = end - start;
        Place(part, (start + end) * .5f, new Vector2(delta.magnitude + .025f, width), Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    private void Place(SpriteRenderer part, Vector2 point, Vector2 size, float angle)
    {
        part.transform.position = transform.TransformPoint(point);
        part.transform.rotation = transform.rotation * Quaternion.Euler(0, 0, angle);
        Vector2 source = part.sprite.bounds.size;
        part.transform.localScale = new Vector3(size.x / source.x, size.y / source.y, 1);
    }

    private void UpdateReloadVisual()
    {
        float pulse = reloading ? .5f + .5f * Mathf.Cos((Time.time - reloadVisualStartedAt) * Mathf.PI * 6f) : 0;
        Color tint = Color.Lerp(animate ? RevolverPresentationGeometry.BodyColor : NeuralPalette.Primary, NeuralPalette.Danger, pulse);
        if (weapon != null) weapon.color = tint;
        if (weaponGrip != null) weaponGrip.color = tint;
        if (cylinder != null) cylinder.color = Color.Lerp(RevolverPresentationGeometry.CoreColor, NeuralPalette.Danger, pulse);
        if (cylinderHalo != null) cylinderHalo.color = Color.Lerp(new Color(.35f,.78f,1f,1f), NeuralPalette.Danger, pulse);
    }
    // The UI and actor share this palette. Brightened indigo stays readable against black.
    public static Color SlotColor(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm => new Color32(255, 90, 95, 255),
        LoadoutSlot.RightArm => new Color32(255, 172, 74, 255),
        LoadoutSlot.LeftLeg => new Color32(255, 228, 94, 255),
        LoadoutSlot.RightLeg => new Color32(88, 222, 131, 255),
        LoadoutSlot.Torso => new Color32(72, 174, 255, 255),
        LoadoutSlot.Brain => new Color32(132, 130, 255, 255),
        LoadoutSlot.Weapon => new Color32(209, 132, 255, 255),
        _ => Color.white
    };
    public static Color EquipmentColor(LoadoutSlot slot, string itemId) =>
        string.IsNullOrEmpty(itemId) || slot == LoadoutSlot.Weapon // guns keep the plain white placeholder
            ? Color.white : SlotColor(slot);
    public static Color AugmentedColor => SlotColor(LoadoutSlot.LeftArm); // historical developer fixture compatibility

    public SpriteRenderer Part(LoadoutSlot slot) => slot switch
    {
        LoadoutSlot.LeftArm => leftArm, LoadoutSlot.RightArm => rightArm,
        LoadoutSlot.LeftLeg => leftLeg, LoadoutSlot.RightLeg => rightLeg,
        LoadoutSlot.Torso => torso, LoadoutSlot.Brain => head, LoadoutSlot.Weapon => weapon, _ => null
    };

    public void Apply(DeploymentSnapshot snapshot)
    {
        for (int i = 0; i < LoadoutRules.SlotCount; i++)
        {
            var part = Part((LoadoutSlot)i);
            if (part != null) part.color = EquipmentColor((LoadoutSlot)i, snapshot.GetSlot((LoadoutSlot)i).ItemId);
        }
        if (weaponGrip != null) weaponGrip.color = EquipmentColor(LoadoutSlot.Weapon, snapshot.WeaponId);
    }
}
