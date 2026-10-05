using System.Collections.Generic;
using UnityEngine;

public enum LegacyDistancePreference { Near, Mid, Far }

/// <summary>One fixed-arena policy in every wave. Legs writes Move/Jump, Arms Aim/Fire, Spine Dodge only.</summary>
public sealed class LegacyPlayerAI
{
    // Prototype tuning lives here. Weapon, movement and dodge resources come from their real executors.
    public static readonly Vector2 NearRange = new Vector2(2.5f, 4f);
    public static readonly Vector2 MidRange = new Vector2(5.3f, 8.5f);
    public static readonly Vector2 FarRange = new Vector2(10f, 13f);
    public const float SpineThreatWindow = .20f;
    public const float DistanceHysteresis = .35f, MoveHold = .16f, ShotPositionHold = .65f;
    private const float Step = .02f, WalkLookahead = .3f, EndpointMargin = .12f;
    private static readonly Vector2[] AimOffsets = { Vector2.zero, new Vector2(0,.65f), new Vector2(0,-.65f), new Vector2(-.55f,0), new Vector2(.55f,0) };
    private static readonly float[] Moves = { 0, -1, 1 };
    private readonly PlayerHealth player;
    private readonly PlayerMovement movement;
    private readonly PlayerDodge dodge;
    private readonly PlayerShooting gun;
    private readonly Rigidbody2D body, targetBody;
    private readonly Collider2D shape, targetShape;
    private readonly CyberpsychoBoss boss;
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
    private Vector2 lastAim = Vector2.right;

    private struct VisibleBullet
    {
        public Vector2 Position, Velocity;
        public float Radius;
    }
    private struct Motion
    {
        public float Move, DodgeDirection;
        public bool Jump;
        public Motion(float move, bool jump = false, float dodgeDirection = 0)
        { Move = move; Jump = jump; DodgeDirection = dodgeDirection; }
    }

    public LegacyPlayerAI(PlayerHealth player, CyberpsychoBoss boss, Collider2D[] platforms,
        LegacyDistancePreference preference = LegacyDistancePreference.Mid)
    {
        this.player = player; this.boss = boss;
        movement = player.GetComponent<PlayerMovement>(); dodge = player.GetComponent<PlayerDodge>();
        gun = player.GetComponent<PlayerShooting>();
        body = player.GetComponent<Rigidbody2D>(); shape = player.GetComponent<Collider2D>();
        targetBody = boss.GetComponent<Rigidbody2D>(); targetShape = boss.GetComponent<Collider2D>();
        combatRoot = player.transform.parent;
        range = preference == LegacyDistancePreference.Near ? NearRange : preference == LegacyDistancePreference.Far ? FarRange : MidRange;
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
    private bool Active => player != null && boss != null && !player.IsDead
        && boss.gameObject.activeInHierarchy && shape.enabled && targetShape.enabled;

    private void Sense()
    {
        if (sensedFrame == Time.frameCount) return;
        sensedFrame = Time.frameCount;
        selfBounds = shape.bounds; targetBounds = targetShape.bounds;
        targetVelocity = targetBody.linearVelocity;
        gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
        threat = boss.PublicThreat; // Only expressed warnings, never a future combo or hidden policy.
        bullets.Clear();
        if (combatRoot == null) return;
        foreach (var projectile in combatRoot.GetComponentsInChildren<Projectile>())
        {
            if (projectile.OwnerLayer == player.gameObject.layer) continue;
            var moving = projectile.GetComponent<Rigidbody2D>();
            var hitbox = projectile.GetComponent<Collider2D>();
            if (moving == null || !moving.simulated || hitbox == null || !hitbox.enabled) continue;
            bullets.Add(new VisibleBullet { Position = moving.position, Velocity = moving.linearVelocity, Radius = projectile.Radius });
        }
    }

    public LegacyCombatIntent Arms()
    {
        var result = new LegacyCombatIntent { Aim = lastAim };
        if (!Active) return result;
        Sense();
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
        Vector2 offset = Vector2.Scale(AimOffsets[index], targetBounds.extents);
        Vector2 origin = centre + (Vector2)player.transform.position - (Vector2)selfBounds.center;
        Vector2 point = (Vector2)targetBounds.center + offset;
        float flight = Vector2.Distance(origin, point) / gun.BulletSpeed;
        for (int i = 0; i < 3; i++)
        {
            point = PredictTarget(Mathf.Min(flight, 1.4f)) + offset;
            direction = (point - origin).normalized;
            Vector2 muzzle = gun.GetMuzzlePosition(direction) + origin - (Vector2)player.transform.position;
            flight = Vector2.Distance(muzzle, point) / gun.BulletSpeed;
        }
        direction = (point - origin).normalized;
        Vector2 start = gun.GetMuzzlePosition(direction) + origin - (Vector2)player.transform.position;
        // Also check the barrel segment: the muzzle cannot silently originate beyond a platform.
        return direction.sqrMagnitude > .9f && !Projectile.TerrainHit(origin, point, gun.BulletRadius, out _)
            && !Projectile.TerrainHit(start, point, gun.BulletRadius, out _);
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
        if (dodge.IsDodging) return result;
        // A jump has a landing target; minor enemy motion does not replace it in midair.
        if (airborneRoute && !movement.IsGrounded)
        { result.Move = Towards(routeX); return result; }
        if (airborneRoute) { airborneRoute = false; routePlatform = null; }
        float desire = RangeDirection();
        bool sight = CanShootFrom(selfBounds.center);
        if (!sight && movement.IsGrounded && (routePlatform == null || Time.time >= routeUntil)) FindShotPosition();
        bool jump = false;
        if (routePlatform != null)
        {
            if (sight && Time.time >= routeUntil) routePlatform = null;
            else
            {
                desire = Towards(routeX);
                jump = routeJump && movement.IsGrounded && JumpHasClearHeadroom();
                if (!jump && Mathf.Abs(routeX-selfBounds.center.x)<.2f && Time.time>=routeUntil) routePlatform=null;
            }
        }
        if (SolidTravel(desire * movement.MoveSpeed * .12f) < .06f) desire = 0;
        var best = new Motion(desire, jump);
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
                    if (candidateRisk >= bestRisk) continue;
                    best = candidate; bestRisk = candidateRisk;
                    routePlatform = support; routeX = landingX; routeJump = true;
                }
        }
        if (bestRisk <= 0 && Time.time < moveHeldUntil && best.Move != rememberedMove && !best.Jump
            && Risk(new Motion(rememberedMove), WalkLookahead) <= 0 && SolidTravel(rememberedMove*.3f)>.1f)
            best.Move = rememberedMove;
        if (best.Move != rememberedMove) { rememberedMove=best.Move; moveHeldUntil=Time.time+MoveHold; }
        if (best.Jump) { airborneRoute=true; routeUntil=Time.time+ShotPositionHold; }
        result.Move=best.Move; result.Jump=best.Jump;
        return result;
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
                if (new Bounds(position,selfBounds.size).Intersects(targetBounds) || !CanShootFrom(position)
                    || PointDanger(position, SpineThreatWindow)) continue;
                float cost=RangeCost(x)*3+travel*.25f+(needsJump?1.8f:0)+Mathf.Abs(rise)*.2f;
                if (cost>=best) continue;
                best=cost; routePlatform=support; routeX=x; routeJump=needsJump;
            }
        }
        routeUntil=Time.time+ShotPositionHold;
    }

    private bool LandingFor(float move, out Collider2D landing, out float x)
    {
        landing=null; x=selfBounds.center.x;
        float bestTime=float.PositiveInfinity;
        foreach(var support in supports)
        {
            if(support==null || !support.enabled) continue;
            float rise=support.bounds.max.y-selfBounds.min.y;
            float discriminant=movement.JumpSpeed*movement.JumpSpeed-2*gravity*rise;
            if(discriminant<0) continue;
            float time=(movement.JumpSpeed+Mathf.Sqrt(discriminant))/gravity;
            float candidate=selfBounds.center.x+move*movement.MoveSpeed*time;
            if(candidate<support.bounds.min.x+selfBounds.extents.x || candidate>support.bounds.max.x-selfBounds.extents.x
                || candidate<leftEdge+selfBounds.extents.x || candidate>rightEdge-selfBounds.extents.x
                || SolidTravel(candidate-selfBounds.center.x)+.1f<Mathf.Abs(candidate-selfBounds.center.x)) continue;
            if(time<bestTime) { bestTime=time; landing=support; x=candidate; }
        }
        return landing!=null;
    }

    public LegacyCombatIntent Spine(float acceptedMove, bool executableJump)
    {
        var result=new LegacyCombatIntent();
        if(!Active || !dodge.CanDodge) return result;
        Sense();
        // Current horizontal authority and only a jump that can actually execute this frame.
        if(Risk(new Motion(acceptedMove, executableJump),SpineThreatWindow)<=0) return result;
        float away=selfBounds.center.x<targetBounds.center.x ? -1 : 1;
        foreach(float direction in new[]{away,-away})
        {
            if(SolidTravel(direction*dodge.Distance)<dodge.Distance-.08f) continue;
            // Trigger is short. Validate the escape through i-frame expiry and the immediate endpoint too.
            float check=Mathf.Max(SpineThreatWindow,dodge.InvulnerabilityDuration+EndpointMargin);
            if(Risk(new Motion(acceptedMove,false,direction),check)>0) continue;
            result.Dodge=true; result.DodgeDirection=direction; break;
        }
        return result;
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
                if(Projectile.TerrainHit(bullet.Position,to,bullet.Radius,out var wall))
                {
                    // A blocker after contact cannot protect the player. Limit this step to the hit point.
                    float blockedAt=wall.distance/Mathf.Max(.001f,bullet.Velocity.magnitude);
                    if(blockedAt<=t-Step) continue;
                    to=bullet.Position+bullet.Velocity*blockedAt;
                }
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
            if(!Projectile.TerrainHit(bullet.Position,to,bullet.Radius,out _)
                && SegmentBox(bullet.Position-centre,to-centre,(Vector2)selfBounds.extents+Vector2.one*bullet.Radius)) return true;
        }
        return WarningHits(centre,centre,time);
    }
    private bool WarningHits(Vector2 previous,Vector2 centre,float time)
    {
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
            var to=threat.Origin+threat.Direction*Mathf.Max(0,along);
            if(Projectile.TerrainHit(threat.Origin,to,.125f,out _)) return false;
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
            float vx=motion.DodgeDirection!=0 && t<=dodge.Duration ? motion.DodgeDirection*dodge.Distance/dodge.Duration : motion.Move*movement.MoveSpeed;
            Vector2 previous=position;
            position.x=Mathf.Clamp(position.x+vx*Step,leftEdge+selfBounds.extents.x+.01f,rightEdge-selfBounds.extents.x-.01f);
            var opponent=targetBounds; opponent.center+=(Vector3)(targetVelocity*Mathf.Min(t,SpineThreatWindow));
            if(position.y-selfBounds.extents.y<opponent.max.y && position.y+selfBounds.extents.y>opponent.min.y && !Physics2D.GetIgnoreCollision(shape,targetShape))
            {
                if(previous.x+selfBounds.extents.x<=opponent.min.x && position.x+selfBounds.extents.x>opponent.min.x) position.x=opponent.min.x-selfBounds.extents.x-.01f;
                if(previous.x-selfBounds.extents.x>=opponent.max.x && position.x-selfBounds.extents.x<opponent.max.x) position.x=opponent.max.x+selfBounds.extents.x+.01f;
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
    private float SolidTravel(float distance)
    {
        if(Mathf.Abs(distance)<.001f) return 0;
        float direction=Mathf.Sign(distance),available=Mathf.Min(Mathf.Abs(distance),direction>0?rightEdge-selfBounds.max.x:selfBounds.min.x-leftEdge);
        if(selfBounds.max.y>targetBounds.min.y && selfBounds.min.y<targetBounds.max.y && !Physics2D.GetIgnoreCollision(shape,targetShape))
        {
            float gap=direction>0 ? targetBounds.min.x-selfBounds.max.x : selfBounds.min.x-targetBounds.max.x;
            if(gap>=0) available=Mathf.Min(available,gap);
        }
        return Mathf.Max(0,available);
    }
    private Vector2 PredictTarget(float seconds)
    {
        Vector2 point=(Vector2)targetBounds.center+targetVelocity*seconds;
        if(targetBody.bodyType==RigidbodyType2D.Dynamic && Mathf.Abs(targetVelocity.y)>.1f)
        {
            point.y-=.5f*Mathf.Abs(Physics2D.gravity.y*targetBody.gravityScale)*seconds*seconds;
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
