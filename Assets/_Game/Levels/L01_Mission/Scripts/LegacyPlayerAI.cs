using System.Collections.Generic;
using GoGhost.Duel;
using UnityEngine;

/// <summary>One fixed-arena policy in every wave. Legs writes Move/Jump, Arms Aim/Fire, Spine Dodge only.</summary>
public sealed class LegacyPlayerAI
{
    // Prototype tuning lives here. Weapon, movement and dodge resources come from their real executors.
    public static readonly Vector2 MidRange = new Vector2(5.3f, 8.5f);
    public const float SpineThreatWindow = .20f;
    public const float DistanceHysteresis = .35f, MoveHold = .16f, ShotPositionHold = .65f;
    private const float Step = .02f, WalkLookahead = .3f, EndpointMargin = .12f;
    private static readonly Vector2[] AimOffsets = { Vector2.zero, new Vector2(0,.65f), new Vector2(0,-.65f), new Vector2(-.55f,0), new Vector2(.55f,0) };
    private static readonly float[] Moves = { 0, -1, 1 };
    private readonly PlayerHealth player;
    private readonly PlayerMovement movement;
    private readonly PlayerDodge dodge;
    private readonly PlayerShooting gun;
    private readonly Rigidbody2D body;
    private Rigidbody2D targetBody;
    private readonly Collider2D shape;
    private Collider2D targetShape;
    private readonly CyberpsychoBoss boss;
    private readonly IReadOnlyList<BossAgent> targets;
    private readonly ArenaNavigation2D navigation;
    private readonly List<ObservedTarget> contacts = new List<ObservedTarget>(2);
    private readonly List<BossThreat> threats = new List<BossThreat>(4);
    private BossAgent focus;
    private BossContact targetContact;
    private bool hasContact;
    private float focusSince;
    private Bounds lastSeenBounds;
    private bool hasSearchMemory, searchingContact;
    private float searchX, searchUntil, nextContactSearch;
    private readonly Transform combatRoot;
    private readonly List<Collider2D> supports = new List<Collider2D>();
    private readonly List<VisibleBullet> bullets = new List<VisibleBullet>();
    private readonly Vector2 range;
    private readonly Vector2[] path = new Vector2[64];
    private LegacyBossThreat threat;
    private Bounds selfBounds, targetBounds;
    private Vector2 targetVelocity;
    private float gravity, leftEdge = -15f, rightEdge = 15f;
    private int sensedFrame = -1, aimIndex;
    private float rememberedMove, moveHeldUntil, rangeMove;
    private Collider2D routePlatform;
    private float routeX, routeUntil;
    private bool routeJump, airborneRoute;
    private float cornerExitUntil, nextCornerSearch;
    private const float PlatformHold = 2.5f, RelocationCooldown = 6f, PressureHold = 1f;
    private Collider2D currentSupport, previousSupport, relocationFrom;
    private float supportSince, pressureSince = -1, nextRelocation, nextPlatformSearch, platformHeldUntil, jumpRequestedAt;
    private bool relocationRoute, routeLeftGround;
    public int PlatformRelocations { get; private set; }
    public int PlatformLandings { get; private set; }
    public int PlatformRouteFailures { get; private set; }
    public Collider2D CommittedPlatform => relocationRoute ? routePlatform : null;
    public Collider2D LastPlatformLanding { get; private set; }
    private Vector2 lastAim = Vector2.right;

    private struct ObservedTarget
    {
        public BossAgent Actor;
        public BossContact Contact;
        public Bounds Bounds;
    }

    private struct VisibleBullet
    {
        public Vector2 Position, Velocity;
        public float Radius;
    }
    private struct Motion
    {
        public float Move, DodgeDirection, LandingX;
        public bool SteerLanding => !float.IsNaN(LandingX);
        public bool Jump;
        public Motion(float move, bool jump = false, float dodgeDirection = 0, float landingX = float.NaN)
        { Move = move; Jump = jump; DodgeDirection = dodgeDirection; LandingX = landingX; }
    }

    public LegacyPlayerAI(PlayerHealth player, CyberpsychoBoss boss, Collider2D[] platforms)
        : this(player, platforms)
    {
        this.player = player; this.boss = boss;
        targetBody = boss.GetComponent<Rigidbody2D>(); targetShape = boss.GetComponent<Collider2D>();
    }

    public LegacyPlayerAI(PlayerHealth player, IReadOnlyList<BossAgent> targets, Collider2D[] platforms)
        : this(player, platforms)
    {
        this.targets = targets;
        navigation = targets.Count > 0 ? targets[0].Navigation : null;
        // Threat prediction follows the active room, including rooms away from world origin.
        // The formal arena retains its existing -15 / 15 physical boundaries.
        if (navigation != null)
        { leftEdge = navigation.ArenaBounds.min.x; rightEdge = navigation.ArenaBounds.max.x; }
    }

    private LegacyPlayerAI(PlayerHealth player, Collider2D[] platforms)
    {
        this.player = player;
        movement = player.GetComponent<PlayerMovement>(); dodge = player.GetComponent<PlayerDodge>();
        gun = player.GetComponent<PlayerShooting>();
        body = player.GetComponent<Rigidbody2D>(); shape = player.GetComponent<Collider2D>();
        combatRoot = player.transform.parent;
        range = MidRange;
        foreach (var collider in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            if (!collider.enabled || collider.isTrigger || collider.gameObject.layer != LayerMask.NameToLayer("Ground")) continue;
            var bounds = collider.bounds;
            if (bounds.size.x > bounds.size.y && bounds.size.x > 1f) supports.Add(collider);
            if (bounds.size.y > 6f && bounds.size.x < 2f)
            {
                if (bounds.center.x < 0) leftEdge = Mathf.Max(leftEdge, bounds.max.x);
                else rightEdge = Mathf.Min(rightEdge, bounds.min.x);
            }
        }
        if (platforms != null)
            foreach (var platform in platforms)
                if (platform != null && !supports.Contains(platform)) supports.Add(platform);
    }
    private bool Active => player != null && !player.IsDead && shape.enabled;

    private void Sense()
    {
        if (sensedFrame == Time.frameCount) return;
        sensedFrame = Time.frameCount;
        selfBounds = shape.bounds;
        gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
        threats.Clear(); contacts.Clear();
        hasContact = false;
        if (targets != null)
        {
            foreach (var actor in targets)
            {
                if (actor == null || !actor.IsAlive) continue;
                foreach (var expressed in actor.PublicThreats)
                    if (expressed.Valid && expressed.EndsAt >= Time.time) threats.Add(expressed);
                if (!actor.TryGetContact(out var contact) || !contact.IsFresh) continue;
                var observed = contact.Bounds;
                // Cues expose their own position only; they do not imply exact hidden body velocity.
                if (contact.VelocityKnown)
                    observed.center += (Vector3)(contact.Velocity * Mathf.Min(.35f, Mathf.Max(0, Time.time - contact.ObservedAt)));
                contacts.Add(new ObservedTarget { Actor = actor, Contact = contact, Bounds = observed });
            }
            SelectFocus();
        }
        else if (boss != null && boss.gameObject.activeInHierarchy && targetShape != null && targetShape.enabled
            && !boss.GetComponent<Enemy>().IsDead)
        {
            hasContact = true;
            targetBounds = targetShape.bounds; targetVelocity = targetBody.linearVelocity;
            threat = boss.PublicThreat; // Historical single-boss fixture compatibility.
        }
        else threat = default;
        bullets.Clear();
        if (combatRoot == null) return;
        foreach (var projectile in Projectile.Active)
        {
            if (projectile == null || !projectile.transform.IsChildOf(combatRoot)) continue;
            if (projectile.OwnerLayer == player.gameObject.layer) continue;
            var moving = projectile.GetComponent<Rigidbody2D>();
            var hitbox = projectile.GetComponent<Collider2D>();
            if (moving == null || !moving.simulated || hitbox == null || !hitbox.enabled) continue;
            bullets.Add(new VisibleBullet { Position = moving.position, Velocity = moving.linearVelocity, Radius = projectile.Radius });
        }
    }

    private void UseContact(ObservedTarget observed)
    {
        targetBounds = observed.Bounds;
        targetContact = observed.Contact;
        targetVelocity = observed.Contact.VelocityKnown ? observed.Contact.Velocity : Vector2.zero;
        targetShape = observed.Actor.Body;
        // Never retain a hidden actor's live Rigidbody as an aim/prediction input.
        targetBody = null;
        focus = observed.Actor;
        hasContact = true;
    }

    private void SelectFocus()
    {
        var previous = focus;
        int current = -1, best = -1, latest = -1;
        bool bestVisible = false;
        float bestDistance = float.PositiveInfinity, newest = float.NegativeInfinity;
        for (int i = 0; i < contacts.Count; i++)
        {
            var observed = contacts[i];
            if (observed.Actor == previous) current = i;
            if (observed.Contact.ObservedAt > newest)
            { newest = observed.Contact.ObservedAt; latest = i; }
            UseContact(observed);
            if (!CanShootFrom(selfBounds.center)) continue;
            if (bestVisible && !observed.Contact.Visible) continue;
            float distance = ((Vector2)targetBounds.center - (Vector2)selfBounds.center).sqrMagnitude;
            if (observed.Contact.Visible && !bestVisible || distance < bestDistance - .001f || Mathf.Abs(distance - bestDistance) <= .001f
                && (best < 0 || observed.Contact.ActorId < contacts[best].Contact.ActorId))
            { best = i; bestDistance = distance; bestVisible = observed.Contact.Visible; }
        }
        // Keep a useful focus. Only a materially better target after the minimum hold can replace it.
        if (current >= 0)
        {
            UseContact(contacts[current]);
            if (CanShootFrom(selfBounds.center) && (targetContact.Visible || !bestVisible))
            {
                float currentDistance = ((Vector2)targetBounds.center - (Vector2)selfBounds.center).sqrMagnitude;
                if (Time.time - focusSince < .6f || best < 0 || bestDistance >= currentDistance * .65f)
                    best = current;
            }
        }
        int selected = best >= 0 ? best : latest;
        if (selected < 0) { focus = null; hasContact = false; return; }
        UseContact(contacts[selected]);
        if(targetContact.Visible)
        {lastSeenBounds=targetBounds;hasSearchMemory=true;searchingContact=false;}
        if (focus != previous)
        { focusSince = Time.time; aimIndex = 0; rangeMove = 0; }
    }

    public LegacyCombatIntent Arms()
    {
        var result = new LegacyCombatIntent { Aim = lastAim };
        if (!Active) return result;
        Sense();
        if (!hasContact) return result;
        // Keep a valid body point. Reconsider the finite centre-first list only when that line fails.
        if (AimAt(selfBounds.center, aimIndex, out var direction))
        { result.Aim = lastAim = direction; result.Fire = true; return result; }
        for (int i = 0; i < AimOffsets.Length; i++)
            if (i != aimIndex && AimAt(selfBounds.center, i, out direction))
            { aimIndex = i; result.Aim = lastAim = direction; result.Fire = true; return result; }
        return result;
    }

    private bool AimAt(Vector2 centre, int index, out Vector2 direction)
    {
        if (targets != null && !targetContact.Visible && Time.time - targetContact.ObservedAt > .35f)
        { direction = lastAim; return false; }
        Vector2 offset = Vector2.Scale(AimOffsets[index], targetBounds.extents);
        Vector2 displacement = centre - (Vector2)selfBounds.center;
        Vector2 origin = gun.AimOrigin + displacement;
        Vector2 point = (Vector2)targetBounds.center + offset;
        float flight = Vector2.Distance(origin, point) / gun.BulletSpeed;
        for (int i = 0; i < 3; i++)
        {
            point = PredictTarget(Mathf.Min(flight, 1.4f)) + offset;
            direction = (point - origin).normalized;
            Vector2 muzzle = gun.GetMuzzlePosition(direction) + displacement;
            flight = Vector2.Distance(muzzle, point) / gun.BulletSpeed;
        }
        direction = (point - origin).normalized;
        Vector2 start = gun.GetMuzzlePosition(direction) + displacement;
        // Terrain does not block bullets; known body points still respect active guards.
        return direction.sqrMagnitude > .9f && !GuardBlocksShot(start, point);
    }
    private bool GuardBlocksShot(Vector2 origin, Vector2 point)
    {
        if (focus != null && focus.GuardBlocksRay(origin, point)) return true;
        foreach (var observed in contacts)
        {
            if (observed.Actor == focus || !observed.Contact.Visible) continue;
            if (SegmentBox(origin - (Vector2)observed.Bounds.center, point - (Vector2)observed.Bounds.center,
                (Vector2)observed.Bounds.extents + Vector2.one * gun.BulletRadius)
                && observed.Actor.GuardBlocksRay(origin, point)) return true;
        }
        return false;
    }
    private bool CanShootFrom(Vector2 centre)
    {
        for (int i = 0; i < AimOffsets.Length; i++) if (AimAt(centre, i, out _)) return true;
        return false;
    }

    public LegacyCombatIntent Legs()
    {
        var result = new LegacyCombatIntent();
        if (!Active) return result;
        Sense();
        TrackSupportPressure();
        if (dodge.IsDodging) return result;
        // A jump has a landing target; minor enemy motion does not replace it in midair.
        if (airborneRoute && (!movement.IsGrounded || relocationRoute && routeLeftGround && currentSupport==null))
        {
            if(!routeLeftGround && relocationRoute) { PlatformRelocations++; TracePlatform("takeoff",routePlatform); }
            routeLeftGround=true;
            if(!relocationRoute || Time.time<routeUntil && routePlatform!=null && routePlatform.enabled)
            { result.Move = relocationRoute ? LandingSteer(routeX,selfBounds.center.x) : Towards(routeX); return result; }
        }
        if (airborneRoute)
        {
            // Spine can accept a Dodge and suppress the requested Jump in the same frame.
            if(!routeLeftGround && Time.time-jumpRequestedAt<.12f)return result;
            if(relocationRoute)
            {
                bool landed=routeLeftGround&&movement.IsGrounded&&currentSupport==routePlatform;
                if(landed){PlatformLandings++;LastPlatformLanding=currentSupport;previousSupport=relocationFrom;platformHeldUntil=Time.time+PlatformHold;}
                else PlatformRouteFailures++;
                nextRelocation=Time.time+(landed?RelocationCooldown:PressureHold);
                TracePlatform(landed?"landed":"interrupted",routePlatform);
            }
            airborneRoute=relocationRoute=routeLeftGround=false;routePlatform=null;
        }
        float desire = hasContact ? RangeDirection() : 0;
        if(!hasContact&&movement.IsGrounded&&routePlatform==null)desire=SearchContactDirection();
        bool sight = hasContact && CanShootFrom(selfBounds.center);
        if (movement.IsGrounded && hasContact) FindCornerExit();
        bool leavingCorner = Time.time < cornerExitUntil;
        if (!leavingCorner && hasContact && !sight && movement.IsGrounded && (routePlatform == null || Time.time >= routeUntil)) FindShotPosition();
        if(!leavingCorner && routePlatform==null && movement.IsGrounded && Time.time>=nextRelocation
            && Time.time>=platformHeldUntil && Risk(new Motion(desire),SpineThreatWindow)<=0)
            FindPlatformRelocation();
        bool jump = false;
        if (routePlatform != null)
        {
            if (!leavingCorner && sight && Time.time >= routeUntil) { routePlatform = null; relocationRoute=false; }
            else
            {
                desire = relocationRoute ? LandingSteer(routeX,selfBounds.center.x) : Towards(routeX);
                jump = routeJump && movement.IsGrounded && JumpHasClearHeadroom();
                if (!jump && Mathf.Abs(routeX-selfBounds.center.x)<.2f && Time.time>=routeUntil) { routePlatform=null; relocationRoute=false; }
            }
        }
        if (!jump && SolidTravel(desire * movement.MoveSpeed * .12f) < .06f) desire = 0;
        var best = new Motion(desire, jump, landingX:relocationRoute?routeX:float.NaN);
        float risk = Risk(best, WalkLookahead), bestRisk = risk;
        // Avoid imminent harm with ordinary walking first; distance is a preference, never an override.
        if (risk > 0)
        {
            foreach (float move in Moves)
            {
                if (move != 0 && SolidTravel(move * movement.MoveSpeed * WalkLookahead) < .1f) continue;
                var candidate = new Motion(move);
                float candidateRisk = Risk(candidate, WalkLookahead);
                if (candidateRisk < bestRisk) { best = candidate; bestRisk = candidateRisk; }
            }
            if (bestRisk > 0 && movement.IsGrounded && JumpHasClearHeadroom())
                foreach (float move in Moves)
                {
                    if (!LandingFor(move, out var support, out float landingX)) continue;
                    var candidate = new Motion(move, true);
                    float candidateRisk = Risk(candidate, WalkLookahead);
                    if (candidateRisk > bestRisk || candidateRisk == bestRisk
                        && (!best.Jump || WallClearance(landingX) <= WallClearance(routeX) + .2f)) continue;
                    best = candidate; bestRisk = candidateRisk;
                    routePlatform = support; routeX = landingX; routeJump = true;
                }
        }
        if(relocationRoute&&!best.SteerLanding)
        { relocationRoute=false;if(!best.Jump)routePlatform=null;nextRelocation=Time.time+PressureHold; }
        if (!leavingCorner && bestRisk <= 0 && Time.time < moveHeldUntil && best.Move != rememberedMove && !best.Jump
            && Risk(new Motion(rememberedMove), WalkLookahead) <= 0 && SolidTravel(rememberedMove*.3f)>.1f)
            best.Move = rememberedMove;
        // Stay to use an earned platform briefly; safety and a committed corner exit can still leave it.
        if(!leavingCorner&&!best.Jump&&bestRisk<=0&&movement.IsGrounded&&Time.time<platformHeldUntil
            &&currentSupport!=null&&!SupportedBy(currentSupport,(Vector2)selfBounds.center+Vector2.right*best.Move*movement.MoveSpeed*WalkLookahead)
            &&Risk(new Motion(0),WalkLookahead)<=0)best.Move=0;
        if (best.Move != rememberedMove) { rememberedMove=best.Move; moveHeldUntil=Time.time+MoveHold; }
        if (best.Jump)
        {
            airborneRoute=true;routeLeftGround=false;jumpRequestedAt=Time.time;
            if(!relocationRoute)routeUntil=Time.time+ShotPositionHold;
        }
        result.Move=best.Move; result.Jump=best.Jump;
        return result;
    }

    private float LandingSteer(float x,float from) => Mathf.Clamp((x-from)/(movement.MoveSpeed*Step),-1,1);
    private float SearchContactDirection()
    {
        // Lost sight retires Arms' target, not Legs' ability to inspect a legal nearby angle.
        // The search uses only the last copied public bounds, never the hidden actor's body.
        if(!hasSearchMemory||currentSupport==null)return 0;
        if(searchingContact)
        {
            if(Time.time<searchUntil&&Mathf.Abs(searchX-selfBounds.center.x)>.15f)
                return Towards(searchX);
            searchingContact=false;
        }
        if(Time.time<nextContactSearch)return 0;
        nextContactSearch=Time.time+.5f;
        var support=currentSupport.bounds;
        float margin=selfBounds.extents.x+.1f;
        float best=float.PositiveInfinity;
        foreach(float x in new[]{support.min.x+margin,support.max.x-margin})
        {
            float travel=Mathf.Abs(x-selfBounds.center.x);
            var point=new Vector2(x,selfBounds.center.y);
            if(travel<.2f||travel>movement.MoveSpeed||!SupportedBy(currentSupport,point)
                ||!BossPerception.CanSee(point,lastSeenBounds)||SolidTravel(x-selfBounds.center.x)<travel-.05f
                ||Risk(new Motion(Mathf.Sign(x-selfBounds.center.x)),WalkLookahead)>0)continue;
            if(navigation!=null&&!navigation.SweepTraversal(selfBounds.center,point,selfBounds.size,out _,out _))continue;
            if(travel>=best)continue;
            best=travel;searchX=x;
        }
        if(float.IsPositiveInfinity(best))return 0;
        searchingContact=true;searchUntil=Time.time+best/movement.MoveSpeed+.5f;
        return Towards(searchX);
    }
    private bool SupportedBy(Collider2D support,Vector2 centre) => support!=null&&support.enabled
        &&Mathf.Abs(centre.y-selfBounds.extents.y-support.bounds.max.y)<.15f
        &&centre.x>support.bounds.min.x+selfBounds.extents.x&&centre.x<support.bounds.max.x-selfBounds.extents.x;
    private void TrackSupportPressure()
    {
        if(movement.IsGrounded)
        {
            Collider2D support=null;
            if(navigation!=null)navigation.TrySupport(selfBounds.center,selfBounds.size,out support);
            else foreach(var item in supports)if(SupportedBy(item,selfBounds.center)){support=item;break;}
            if(support!=currentSupport){currentSupport=support;supportSince=Time.time;}
        }
        bool pressured=false;
        float width=navigation!=null?navigation.PlatformWidth:0;
        foreach(var observed in contacts)
        {
            Vector2 delta=(Vector2)observed.Bounds.center-(Vector2)selfBounds.center;
            bool closing=observed.Contact.VelocityKnown&&delta.x*observed.Contact.Velocity.x<0;
            if(Mathf.Abs(delta.y)>selfBounds.size.y+width*.5f)continue;
            if(Mathf.Abs(delta.x)<width || observed.Actor.Profile.Kind==BossKind.Severance
                &&Mathf.Abs(delta.x)<navigation.SameLevelSpacing*.8f&&(closing||Mathf.Abs(delta.y)<selfBounds.size.y))pressured=true;
        }
        if(!pressured)pressureSince=-1;else if(pressureSince<0)pressureSince=Time.time;
    }
    private float PlatformSpacingScore(Vector2 point,float width,float jumpHeight)
    {
        float score=Mathf.Min(width,WallClearance(point.x))*.15f,nearest=float.PositiveInfinity;
        foreach(var observed in contacts)
        {
            Vector2 other=observed.Bounds.center;
            float horizontal=Mathf.Abs(point.x-other.x),vertical=Mathf.Abs(point.y-other.y);
            nearest=Mathf.Min(nearest,horizontal);
            float gap=horizontal-selfBounds.extents.x-observed.Bounds.extents.x;
            if(vertical<selfBounds.size.y)score-=Mathf.Max(0,width-gap);
            if(observed.Actor.Profile.Kind==BossKind.Severance)
                score+=Mathf.Clamp01(vertical/jumpHeight)*width*.5f;
        }
        // Distance to the closest public body, independent of the gun's selected focus.
        score-=.4f*(nearest<range.x?range.x-nearest:nearest>range.y?nearest-range.y:0);
        return score;
    }
    private void FindPlatformRelocation()
    {
        if(navigation==null||currentSupport==null||contacts.Count==0||Time.time<nextPlatformSearch
            ||pressureSince<0||Time.time-pressureSince<PressureHold||Time.time-supportSince<PressureHold
            ||!JumpHasClearHeadroom())return;
        nextPlatformSearch=Time.time+.3f;
        float width=navigation.PlatformWidth;
        float height=ArenaNavigation2D.ExecutableJumpHeight(new BossMotionProfile {JumpSpeed=movement.JumpSpeed,GravityScale=body.gravityScale});
        float baseline=PlatformSpacingScore(selfBounds.center,width,height),best=baseline+width*.12f;
        Collider2D chosen=null;float chosenX=0,chosenFlight=0;
        foreach(var node in navigation.Nodes)
        {
            if(node.Collider==currentSupport||!node.Collider.enabled)continue;
            Vector2 point=node.Center(selfBounds.size);
            float rise=point.y-selfBounds.center.y,travel=Mathf.Abs(point.x-selfBounds.center.x);
            if(Mathf.Abs(rise)>height||travel>navigation.SameLevelSpacing||!navigation.CanStand(point,selfBounds.size)
                ||BodyBlocked(new Bounds(point,selfBounds.size+Vector3.one*selfBounds.extents.x*.5f)))continue;
            float score=PlatformSpacingScore(point,width,height)-travel*.12f-(node.Collider==previousSupport?width*.4f:0);
            if(score<=best)continue;
            var motion=new Motion(Towards(point.x),true,landingX:point.x);
            if(!LandingFor(motion,out var landing,out float x,out float flight)||landing!=node.Collider
                ||Mathf.Abs(x-point.x)>selfBounds.extents.x*.5f||PointDanger(point,flight+EndpointMargin)
                ||Risk(motion,flight+EndpointMargin)>0)continue;
            best=score;chosen=landing;chosenX=point.x;chosenFlight=flight;
        }
        if(chosen==null)return;
        routePlatform=chosen;routeX=chosenX;routeJump=relocationRoute=true;relocationFrom=currentSupport;
        routeUntil=Time.time+chosenFlight+.6f;
        TracePlatform("selected",chosen);
    }
    private void TracePlatform(string stage,Collider2D destination)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Legs {Time.time:F2}] relocation {stage} from={relocationFrom?.name} to={destination?.name} x={routeX:F2} at={selfBounds.center}");
#endif
    }

    private float RangeDirection()
    {
        float separation = targetBounds.center.x-selfBounds.center.x;
        float distance = Mathf.Abs(separation), toward = separation < 0 ? -1 : 1;
        if (rangeMove != 0 && distance >= range.x && distance <= range.y) rangeMove=0;
        if (distance < range.x-DistanceHysteresis) rangeMove=-toward;
        else if (distance > range.y+DistanceHysteresis) rangeMove=toward;
        return rangeMove;
    }
    private float Towards(float x) => Mathf.Abs(x-selfBounds.center.x)<.15f ? 0 : Mathf.Sign(x-selfBounds.center.x);
    private float RangeCost(float x)
    {
        float d=Mathf.Abs(x-targetBounds.center.x);
        return d<range.x ? range.x-d : d>range.y ? d-range.y : 0;
    }

    private float WallClearance(float x) => Mathf.Min(x-leftEdge, rightEdge-x)-selfBounds.extents.x;

    private void FindCornerExit()
    {
        if (Time.time < cornerExitUntil || Time.time < nextCornerSearch
            || WallClearance(selfBounds.center.x) > 2f
            || Mathf.Abs(targetBounds.center.x-selfBounds.center.x) > 7f) return;
        nextCornerSearch = Time.time + .25f;
        float inward = selfBounds.center.x < (leftEdge+rightEdge)*.5f ? 1 : -1;
        // Prefer a real walk. If a body closes that lane, evaluate the whole jump including landing.
        if (SolidTravel(inward*3f) > 2.8f)
        {
            foreach (var support in supports)
            {
                if (support == null || !support.enabled || Mathf.Abs(selfBounds.min.y-support.bounds.max.y) > .15f) continue;
                float x = selfBounds.center.x+inward*3f;
                if (x < support.bounds.min.x+selfBounds.extents.x || x > support.bounds.max.x-selfBounds.extents.x) continue;
                routePlatform=support; routeX=x; routeJump=false;
                cornerExitUntil=routeUntil=Time.time+1.1f; return;
            }
        }
        if (!JumpHasClearHeadroom() || !LandingFor(inward,out var landing,out float landingX)
            || WallClearance(landingX) < WallClearance(selfBounds.center.x)+1.2f) return;
        routePlatform=landing; routeX=landingX; routeJump=true;
        cornerExitUntil=routeUntil=Time.time+1.25f;
    }

    private void FindShotPosition()
    {
        routePlatform=null;
        float best=float.PositiveInfinity;
        Collider2D currentSupport=null;
        foreach(var support in supports)
            if(support!=null && support.enabled && Mathf.Abs(selfBounds.min.y-support.bounds.max.y)<.15f
                && selfBounds.center.x>support.bounds.min.x && selfBounds.center.x<support.bounds.max.x)
            { currentSupport=support; break; }
        foreach (var support in supports)
        {
            if (support==null || !support.enabled) continue;
            var b=support.bounds;
            float rise=b.max.y-selfBounds.min.y;
            if (rise>movement.JumpSpeed*movement.JumpSpeed/(2*gravity)-.08f) continue;
            float min=Mathf.Max(b.min.x+selfBounds.extents.x+.1f,leftEdge+selfBounds.extents.x+.1f);
            float max=Mathf.Min(b.max.x-selfBounds.extents.x-.1f,rightEdge-selfBounds.extents.x-.1f);
            if (min>max) continue;
            float preferred=(range.x+range.y)*.5f;
            float[] candidates={ selfBounds.center.x, selfBounds.center.x-1.5f, selfBounds.center.x+1.5f,
                targetBounds.center.x-preferred, targetBounds.center.x+preferred, min, max, b.center.x };
            foreach (float requested in candidates)
            {
                float x=Mathf.Clamp(requested,min,max);
                var position=new Vector2(x,b.max.y+selfBounds.extents.y+.02f);
                float travel=Mathf.Abs(x-selfBounds.center.x);
                bool needsJump=rise>.18f || Mathf.Abs(rise)<=.18f && currentSupport!=support;
                // Descend by actually walking off the current support. A floor point underneath it
                // is not a reachable walk destination, even though the eventual shot line is clear.
                if (rise < -.18f && currentSupport!=null
                    && x>currentSupport.bounds.min.x-selfBounds.extents.x-.1f
                    && x<currentSupport.bounds.max.x+selfBounds.extents.x+.1f) continue;
                if (needsJump)
                {
                    float flight=(movement.JumpSpeed+Mathf.Sqrt(Mathf.Max(0,movement.JumpSpeed*movement.JumpSpeed-2*gravity*rise)))/gravity;
                    if (travel>movement.MoveSpeed*flight-.1f || SolidTravel(x-selfBounds.center.x)+.1f<travel) continue;
                }
                else if (SolidTravel(x-selfBounds.center.x)+.1f<travel) continue;
                if (BodyBlocked(new Bounds(position,selfBounds.size)) || !CanShootFrom(position)
                    || PointDanger(position, SpineThreatWindow)) continue;
                float cost=RangeCost(x)*3+travel*.25f+(needsJump?1.8f:0)+Mathf.Abs(rise)*.2f;
                if (cost>=best) continue;
                best=cost; routePlatform=support; routeX=x; routeJump=needsJump;
            }
        }
        routeUntil=Time.time+ShotPositionHold;
    }

    private bool LandingFor(float move, out Collider2D landing, out float x)
        => LandingFor(new Motion(move,true),out landing,out x,out _);
    private bool LandingFor(Motion motion,out Collider2D landing,out float x,out float flight)
    {
        landing=null; x=selfBounds.center.x;flight=0;
        int count=PredictPath(motion,1.24f);
        for(int i=2;i<count;i++)
        {
            if(path[i].y>path[i-1].y+.001f) continue;
            foreach(var support in supports)
            {
                if(support==null || !support.enabled) continue;
                var bounds=support.bounds;
                if(Mathf.Abs(path[i].y-selfBounds.extents.y-bounds.max.y)>.06f
                    || path[i].x<bounds.min.x+selfBounds.extents.x || path[i].x>bounds.max.x-selfBounds.extents.x
                    || BodyBlocked(new Bounds(path[i],selfBounds.size))) continue;
                landing=support; x=path[i].x;flight=i*Step; return true;
            }
        }
        return false;
    }

    public LegacyCombatIntent Spine(float acceptedMove, bool executableJump)
    {
        var result=new LegacyCombatIntent();
        if(!Active || !dodge.CanDodge) return result;
        Sense();
        // Current horizontal authority and only a jump that can actually execute this frame.
        if(Risk(new Motion(acceptedMove, executableJump),SpineThreatWindow)<=0) return result;
        float away=hasContact ? (selfBounds.center.x<targetBounds.center.x ? -1 : 1)
            : (bullets.Count>0 && selfBounds.center.x<bullets[0].Position.x ? -1 : 1);
        foreach(float direction in new[]{away,-away})
        {
            if(SolidTravel(direction*dodge.Distance,true)<dodge.Distance-.08f) continue;
            // Trigger is short. Validate the escape through i-frame expiry and the immediate endpoint too.
            float check=Mathf.Max(SpineThreatWindow,dodge.InvulnerabilityDuration+EndpointMargin);
            if(Risk(new Motion(acceptedMove,false,direction),check)>0) continue;
            result.Dodge=true; result.DodgeDirection=direction; break;
        }
        return result;
    }

    // Conditional rules reuse only executable geometry, never the old Legs/Arms/Spine decisions.
    public bool ConditionalThreat(int kind,float move,bool jump,out Vector2 source,out int actorId,int requiredActorId=0)
    {
        Sense(); source=default;actorId=0;
        int count=PredictPath(new Motion(move,jump,dodge.IsDodging?Mathf.Sign(body.linearVelocity.x):0),SpineThreatWindow);
        Vector2 half=selfBounds.extents;
        if(kind==43) half.y*=.5f;
        Vector2 offset=kind==43?Vector2.down*selfBounds.extents.y*.5f:Vector2.zero;
        for(int i=1;i<count;i++)
        {
            float t=i*Step;
            if(kind!=42)
                foreach(var shot in Projectile.Active)
                {
                    if(shot==null||shot.OwnerLayer==player.gameObject.layer||!shot.transform.IsChildOf(combatRoot))continue;
                    var rb=shot.GetComponent<Rigidbody2D>();
                    if(rb==null||!rb.simulated)continue;
                    Vector2 a=rb.position+rb.linearVelocity*(t-Step),b=rb.position+rb.linearVelocity*t;
                    if(!SegmentBox(a-path[i-1]-offset,b-path[i]-offset,half+Vector2.one*shot.Radius))continue;
                    if(kind==43&&SegmentBox(a-path[i-1]+offset,b-path[i]+offset,half+Vector2.one*shot.Radius))continue;
                    var owner=shot.Owner!=null?shot.Owner.GetComponentInParent<BossAgent>():null;
                    int candidateId=owner!=null?owner.ActorId:0;
                    if(requiredActorId!=0&&candidateId!=requiredActorId)continue;
                    source=rb.position;actorId=candidateId;return true;
                }
            foreach(var expressed in threats)
            {
                if(requiredActorId!=0&&expressed.ActorId!=requiredActorId)continue;
                // Emitted sword waves share the public projectile rule; a windup is not a bullet.
                if(kind==41&&(expressed.Shape!=BossThreatShape.MovingBlade||expressed.Phase!=BossThreatPhase.Active))continue;
                bool melee=expressed.Shape!=BossThreatShape.Projectile&&expressed.Shape!=BossThreatShape.Ray&&expressed.Shape!=BossThreatShape.MovingBlade;
                if(kind==42&&!melee)continue;
                if(!expressed.IntersectsMotion(path[i-1]+offset,path[i]+offset,half,Time.time+t-Step,Time.time+t))continue;
                if(kind==43&&expressed.IntersectsMotion(path[i-1]-offset,path[i]-offset,half,Time.time+t-Step,Time.time+t))continue;
                source=expressed.Origin;actorId=expressed.ActorId;return true;
            }
        }
        return false;
    }
    public bool ConditionalDodgeDirection(float move,out float direction)
    {
        Sense();direction=0;float best=float.PositiveInfinity;
        float preferred=Mathf.Abs(move)>.01f?Mathf.Sign(move):movement.MovementFacingDirection;
        foreach(float side in new[]{preferred,-preferred})
        {
            if(SolidTravel(side*dodge.Distance,true)<.15f)continue;
            float risk=Risk(new Motion(move,false,side),Mathf.Max(SpineThreatWindow,dodge.InvulnerabilityDuration+EndpointMargin));
            if(risk>=best)continue;
            best=risk;direction=side;
        }
        return direction!=0;
    }

    private float Risk(Motion motion,float horizon)
    {
        int count=PredictPath(motion,horizon);
        float immunity=motion.DodgeDirection!=0 ? dodge.InvulnerabilityDuration
            : Mathf.Max(0,dodge.InvulnerabilityDuration-(Time.time-dodge.LastDodgeStartedAt));
        float risk=0;
        for(int i=1;i<count;i++)
        {
            float t=i*Step;
            if(t<immunity) continue;
            foreach(var bullet in bullets)
            {
                Vector2 from=bullet.Position+bullet.Velocity*(t-Step), to=bullet.Position+bullet.Velocity*t;
                if(SegmentBox(from-path[i-1],to-path[i],(Vector2)selfBounds.extents+Vector2.one*bullet.Radius)) risk+=1;
            }
            if(WarningHits(path[i-1],path[i],t)) risk+=1;
        }
        return risk;
    }
    private bool PointDanger(Vector2 centre,float time)
    {
        foreach(var bullet in bullets)
        {
            Vector2 to=bullet.Position+bullet.Velocity*time;
            if(SegmentBox(bullet.Position-centre,to-centre,(Vector2)selfBounds.extents+Vector2.one*bullet.Radius)) return true;
        }
        return WarningHits(centre,centre,time);
    }
    private bool WarningHits(Vector2 previous,Vector2 centre,float time)
    {
        foreach (var expressed in threats)
            if (expressed.IntersectsMotion(previous, centre, selfBounds.extents,
                Time.time + Mathf.Max(0, time - Step), Time.time + time)) return true;
        if(!threat.Visible || !threat.Locked) return false;
        float start=threat.TimeToImpact;
        float active=Mathf.Max(.02f,threat.ActiveRemaining);
        if(threat.ProjectileSpeed>0)
        {
            if(time<start) return false;
            float along=Vector2.Dot(centre-threat.Origin,threat.Direction);
            float arrival=start+along/threat.ProjectileSpeed;
            float width=threat.Kind==LegacyThreatKind.MarkedLocked ? Step : active;
            if(along<0 || time<arrival-Step || time>arrival+width) return false;
            float across=Mathf.Abs(Cross(centre-threat.Origin,threat.Direction));
            float half=Mathf.Abs(threat.Direction.y)*selfBounds.extents.x+Mathf.Abs(threat.Direction.x)*selfBounds.extents.y;
            return across<half+.125f;
        }
        if(time<start || time>start+active) return false;
        if(threat.Kind==LegacyThreatKind.PredatorDrop) return threat.DangerArea.Intersects(new Bounds(centre,selfBounds.size));
        if(threat.Kind!=LegacyThreatKind.GhostSlash && threat.Kind!=LegacyThreatKind.Crosscut) return false;
        // The shown lane is swept over time, not an instantaneous arena-sized hitbox. Only this committed pass.
        float travel=Mathf.Max(0,threat.DangerArea.size.x-1.8f);
        float speed=travel/active;
        float now=Mathf.Clamp(time-start,0,active), before=Mathf.Clamp(time-Step-start,0,active);
        Vector2 a=threat.Origin+threat.Direction*(.85f+speed*before);
        Vector2 b=threat.Origin+threat.Direction*(.85f+speed*now);
        return SegmentBox(a-previous,b-centre,(Vector2)selfBounds.extents+new Vector2(.9f,.65f));
    }

    private int PredictPath(Motion motion,float horizon)
    {
        int count=Mathf.Min(path.Length,Mathf.CeilToInt(horizon/Step)+1);
        Vector2 position=selfBounds.center;
        float vy=motion.Jump ? movement.JumpSpeed : body.linearVelocity.y;
        bool grounded=movement.IsGrounded && !motion.Jump;
        path[0]=position;
        for(int i=1;i<count;i++)
        {
            float t=i*Step;
            bool dashing=motion.DodgeDirection!=0 && t<=(dodge.IsDodging?dodge.MovementRemaining:dodge.Duration);
            float vx=dashing
                ? (dodge.IsDodging?dodge.CommittedHorizontalSpeed:motion.DodgeDirection*dodge.Distance/dodge.Duration)
                : (motion.SteerLanding?LandingSteer(motion.LandingX,position.x):motion.Move)*movement.MoveSpeed;
            Vector2 previous=position;
            position.x=Mathf.Clamp(position.x+vx*Step,leftEdge+selfBounds.extents.x+.01f,rightEdge-selfBounds.extents.x-.01f);
            if (targets != null)
                foreach (var observed in contacts)
                {
                    var opponent = observed.Bounds;
                    if (observed.Contact.VelocityKnown)
                        opponent.center += (Vector3)(observed.Contact.Velocity * Mathf.Min(t, SpineThreatWindow));
                    ClampAgainstBody(ref position, previous, opponent, observed.Actor.Body, dashing);
                }
            else if (hasContact)
            {
                var opponent = targetBounds; opponent.center += (Vector3)(targetVelocity * Mathf.Min(t, SpineThreatWindow));
                ClampAgainstBody(ref position, previous, opponent, targetShape, dashing);
            }
            if(grounded && !SupportedAt(position)) grounded=false;
            if(!grounded)
            {
                vy-=gravity*Step; position.y+=vy*Step;
                if(vy<=0)
                    foreach(var support in supports)
                    {
                        if(support==null || !support.enabled) continue;
                        var b=support.bounds;
                        if(position.x+selfBounds.extents.x*.5f<b.min.x || position.x-selfBounds.extents.x*.5f>b.max.x) continue;
                        if(previous.y-selfBounds.extents.y>=b.max.y-.03f && position.y-selfBounds.extents.y<=b.max.y)
                        { position.y=b.max.y+selfBounds.extents.y; vy=0; grounded=true; }
                    }
            }
            if(navigation!=null && !navigation.SweepTraversal(previous,position,selfBounds.size,out var terrain,out var safe))
            {
                if(Mathf.Abs(terrain.normal.y)>.5f)
                { position=safe; vy=0; grounded=terrain.normal.y>.5f; }
                else position.x=safe.x;
            }
            path[i]=position;
        }
        return count;
    }
    private bool SupportedAt(Vector2 centre)
    {
        foreach(var support in supports)
            if(support!=null && support.enabled && Mathf.Abs(centre.y-selfBounds.extents.y-support.bounds.max.y)<.12f
                && centre.x>support.bounds.min.x-selfBounds.extents.x*.5f && centre.x<support.bounds.max.x+selfBounds.extents.x*.5f) return true;
        return false;
    }
    private bool JumpHasClearHeadroom()
    {
        foreach(var hit in Physics2D.BoxCastAll(selfBounds.center,selfBounds.size*.9f,0,Vector2.up,.3f,LayerMask.GetMask("Ground")))
            if(hit.collider!=null && !hit.collider.isTrigger && hit.collider.GetComponent<PlatformEffector2D>()==null && hit.distance>.01f) return false;
        return true;
    }
    private float SolidTravel(float distance, bool dashing=false)
    {
        if(Mathf.Abs(distance)<.001f) return 0;
        float direction=Mathf.Sign(distance),available=Mathf.Min(Mathf.Abs(distance),direction>0?rightEdge-selfBounds.max.x:selfBounds.min.x-leftEdge);
        if (targets != null)
            foreach (var observed in contacts)
                available = BodyTravel(available, direction, observed.Bounds, observed.Actor.Body, dashing);
        else if (hasContact) available = BodyTravel(available, direction, targetBounds, targetShape, dashing);
        return Mathf.Max(0,available);
    }

    private float BodyTravel(float available, float direction, Bounds opponent, Collider2D other, bool dashing)
    {
        if (other != null && !(dashing && dodge.CanPassThroughBody(other)) && !Physics2D.GetIgnoreCollision(shape, other)
            && selfBounds.max.y > opponent.min.y && selfBounds.min.y < opponent.max.y)
        {
            float gap = direction > 0 ? opponent.min.x - selfBounds.max.x : selfBounds.min.x - opponent.max.x;
            if (gap >= 0) available = Mathf.Min(available, gap);
        }
        return available;
    }

    private void ClampAgainstBody(ref Vector2 position, Vector2 previous, Bounds opponent, Collider2D other, bool dashing)
    {
        if (other == null || dashing && dodge.CanPassThroughBody(other) || Physics2D.GetIgnoreCollision(shape, other)
            || position.y - selfBounds.extents.y >= opponent.max.y || position.y + selfBounds.extents.y <= opponent.min.y) return;
        if (previous.x + selfBounds.extents.x <= opponent.min.x && position.x + selfBounds.extents.x > opponent.min.x)
            position.x = opponent.min.x - selfBounds.extents.x - .01f;
        if (previous.x - selfBounds.extents.x >= opponent.max.x && position.x - selfBounds.extents.x < opponent.max.x)
            position.x = opponent.max.x + selfBounds.extents.x + .01f;
    }

    private bool BodyBlocked(Bounds proposed)
    {
        if (targets != null)
        { foreach (var observed in contacts) if (proposed.Intersects(observed.Bounds)) return true; return false; }
        return hasContact && proposed.Intersects(targetBounds);
    }
    private Vector2 PredictTarget(float seconds)
    {
        Vector2 point=(Vector2)targetBounds.center+targetVelocity*seconds;
        bool dynamic = targets != null ? targetContact.Dynamic : targetBody != null && targetBody.bodyType == RigidbodyType2D.Dynamic;
        float targetGravity = targets != null ? targetContact.Gravity : targetBody != null ? Mathf.Abs(Physics2D.gravity.y * targetBody.gravityScale) : 0;
        if (targets != null && !targetContact.Visible)
            seconds = Mathf.Min(seconds, Mathf.Max(0, Mathf.Min(.35f - (Time.time - targetContact.ObservedAt), targetContact.ExpiresAt - Time.time)));
        point = (Vector2)targetBounds.center + targetVelocity * seconds;
        if(dynamic && Mathf.Abs(targetVelocity.y)>.1f)
        {
            point.y-=.5f*targetGravity*seconds*seconds;
            float floor=-2.5f+targetBounds.extents.y;
            foreach(var support in supports)
                if(support!=null && support.enabled && support.bounds.max.y<=targetBounds.min.y+.12f
                    && point.x>=support.bounds.min.x && point.x<=support.bounds.max.x) floor=Mathf.Max(floor,support.bounds.max.y+targetBounds.extents.y);
            point.y=Mathf.Max(point.y,floor);
        }
        point.x=Mathf.Clamp(point.x,leftEdge+targetBounds.extents.x,rightEdge-targetBounds.extents.x);
        return point;
    }
    private static float Cross(Vector2 a,Vector2 b) => a.x*b.y-a.y*b.x;
    private static bool SegmentBox(Vector2 from,Vector2 to,Vector2 half)
    {
        Vector2 delta=to-from;
        float first=0,last=1;
        for(int axis=0;axis<2;axis++)
        {
            if(Mathf.Abs(delta[axis])<.0001f) { if(Mathf.Abs(from[axis])>half[axis]) return false; continue; }
            float a=(-half[axis]-from[axis])/delta[axis],b=(half[axis]-from[axis])/delta[axis];
            if(a>b) { float swap=a; a=b; b=swap; }
            first=Mathf.Max(first,a); last=Mathf.Min(last,b);
            if(first>last) return false;
        }
        return true;
    }
}
