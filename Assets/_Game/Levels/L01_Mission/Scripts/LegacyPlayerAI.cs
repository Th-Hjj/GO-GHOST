using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The same player policy in every wave. It requests the existing abilities through PlayerInputReader;
/// it never writes a body, cooldown, permission, hit or opponent plan.
/// </summary>
public sealed class LegacyPlayerAI
{
    private const float BulletSpeed = 18f, JumpSpeed = 12f, DodgeDistance = 3f;
    private const float DodgeDuration = .18f, Invulnerability = .25f, Horizon = .8f, Step = .04f;
    private readonly PlayerHealth player;
    private readonly PlayerMovement movement;
    private readonly PlayerDodge dodge;
    private readonly PlayerCounterArm blade;
    private readonly PlayerShooting gun;
    private readonly Rigidbody2D body, targetBody;
    private readonly Collider2D shape, targetShape;
    private readonly CyberpsychoBoss boss;
    private readonly Transform combatRoot;
    private readonly List<Collider2D> supports = new List<Collider2D>();
    private readonly List<VisibleBullet> bullets = new List<VisibleBullet>();
    // Only an already-expressed Crosscut needs the longer two-pass forecast. Ordinary motion stays at .8s.
    private readonly Vector2[] path = new Vector2[81];
    private int pathCount = 21;
    private LegacyBossThreat threat;
    private Bounds selfBounds, targetBounds;
    private Vector2 targetVelocity;
    private float gravity, leftEdge = -15f, rightEdge = 15f;
    private int sensedFrame = -1, automaticAimFrame = -1;
    private float rememberedMove, moveHeldUntil;
    private Collider2D routePlatform;
    private float routeUntil;

    private struct VisibleBullet
    {
        public Vector2 Position, Velocity;
        public float Radius;
        public bool Counterable;
    }

    private struct Motion
    {
        public float Move;
        public bool Jump, Dodge;
        public Motion(float move, bool jump = false, bool dash = false)
        { Move = move; Jump = jump; Dodge = dash; }
    }

    public LegacyPlayerAI(PlayerHealth player, CyberpsychoBoss boss, Collider2D[] platforms)
    {
        this.player = player; this.boss = boss;
        movement = player.GetComponent<PlayerMovement>();
        dodge = player.GetComponent<PlayerDodge>();
        blade = player.GetComponent<PlayerCounterArm>();
        gun = player.GetComponent<PlayerShooting>();
        body = player.GetComponent<Rigidbody2D>(); shape = player.GetComponent<Collider2D>();
        targetBody = boss.GetComponent<Rigidbody2D>(); targetShape = boss.GetComponent<Collider2D>();
        combatRoot = player.transform.parent;
        // Current fixed arena only. Use real colliders, including the floor, rather than invented footholds.
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
        threat = boss.PublicThreat; // This struct contains only the warnings actually displayed in the scene.
        bullets.Clear();
        if (combatRoot == null) return;
        foreach (var projectile in combatRoot.GetComponentsInChildren<Projectile>())
        {
            if (projectile.OwnerLayer == player.gameObject.layer) continue;
            var projectileBody = projectile.GetComponent<Rigidbody2D>();
            var projectileShape = projectile.GetComponent<Collider2D>();
            if (projectileBody == null || !projectileBody.simulated || projectileShape == null || !projectileShape.enabled) continue;
            bullets.Add(new VisibleBullet { Position = projectileBody.position, Velocity = projectileBody.linearVelocity,
                Radius = Mathf.Max(projectileShape.bounds.extents.x, projectileShape.bounds.extents.y),
                // Arm-Blade turns around ordinary live bullets. CanBeCountered belongs only to legacy
                // Counter Arm's on-body cancellation and must not narrow the restored blade's coverage.
                Counterable = !projectile.Deflected });
        }
    }

    public Vector2 Aim(bool bladeRequested)
    {
        if (!Active) return Vector2.right;
        Sense();
        automaticAimFrame = Time.frameCount;
        Vector2 origin = selfBounds.center;
        if (bladeRequested)
        {
            if (BladeThreat(out Vector2 counterDirection)) return counterDirection;
            return ((Vector2)targetBounds.center - origin).normalized;
        }
        // Iterative intercept from the real muzzle and current visible velocity. No homing after Launch.
        Vector2 point = targetBounds.center;
        float flight = Vector2.Distance(origin, point) / BulletSpeed;
        for (int i = 0; i < 3; i++)
        {
            point = PredictTarget(Mathf.Min(flight, 1.4f));
            Vector2 direction = (point - origin).normalized;
            flight = Vector2.Distance(gun.GetMuzzlePosition(direction), point) / BulletSpeed;
        }
        return (point - origin).sqrMagnitude > .001f ? (point - origin).normalized : Vector2.right;
    }

    public LegacyCombatIntent Brain(Vector2 resolvedAim)
    {
        var result = new LegacyCombatIntent();
        if (!Active) return result;
        Sense();
        Vector2 aim = resolvedAim.sqrMagnitude > .001f ? resolvedAim.normalized : Vector2.right;
        result.Fire = FireOpportunity(aim);
        if (blade == null || blade.CooldownRemaining > 0 || blade.IsWindowOpen) return result;
        // Brain evaluates the actual resolved direction, including manual Arms. It cannot substitute an AI aim.
        if (InsideBlade(targetBounds.center, aim, targetBounds.extents.magnitude * .55f)) result.Blade = true;
        foreach (var bullet in bullets)
        {
            if (!bullet.Counterable) continue;
            float arrival = ClosestTime(bullet.Position - (Vector2)selfBounds.center,
                bullet.Velocity - body.linearVelocity, .18f);
            float catchTime = Mathf.Max(0,arrival-.055f);
            Vector2 future = bullet.Position + bullet.Velocity * catchTime;
            Vector2 futureOrigin = (Vector2)selfBounds.center+body.linearVelocity*catchTime;
            if (InsideBlade(future, aim, bullet.Radius, futureOrigin) && Vector2.Distance(future, futureOrigin) < 2.3f)
                result.Blade = true;
        }
        // The reader has actually run the authorised automatic Arms callback this frame. A legal E request
        // can therefore receive its blade direction in the reader's following Aim(true) step. With manual
        // Arms this branch is unavailable: only the already-resolved direction above can authorise E.
        bool automaticBladeAim = automaticAimFrame == Time.frameCount && BladeThreat(out _);
        if (automaticBladeAim) result.Blade = true;
        // Only the original compatible ordinary blade contact can parry; Drop is deliberately excluded.
        if (threat.Visible && (threat.Kind == LegacyThreatKind.GhostSlash || threat.Kind == LegacyThreatKind.Crosscut)
            && threat.TimeToImpact <= .28f && threat.DangerArea.Intersects(selfBounds)
            && Vector2.Dot(aim, (Vector2)targetBounds.center - (Vector2)selfBounds.center) > 0)
            result.Blade = true;
        return result;
    }

    private bool FireOpportunity(Vector2 aim)
    {
        Vector2 muzzle = gun.GetMuzzlePosition(aim);
        float flight = Vector2.Distance(muzzle, targetBounds.center) / BulletSpeed;
        Vector2 expected = PredictTarget(Mathf.Min(flight, 1.4f));
        float along = Vector2.Dot(expected-muzzle,aim), across = Mathf.Abs(Cross(expected-muzzle,aim));
        float radius = Mathf.Abs(aim.y)*targetBounds.extents.x+Mathf.Abs(aim.x)*targetBounds.extents.y;
        return along>0 && along<BulletSpeed*2.9f && across<radius+.12f && !ProjectileBlocked(muzzle,aim,along);
    }

    private bool BladeThreat(out Vector2 direction)
    {
        direction = Vector2.zero;
        float bestTime = .3f;
        Vector2 origin = selfBounds.center;
        foreach (var bullet in bullets)
        {
            if (!bullet.Counterable) continue;
            Vector2 relativeVelocity = bullet.Velocity-body.linearVelocity;
            Vector2 offset = bullet.Position-origin;
            if (Vector2.Dot(offset,relativeVelocity)>=0) continue;
            float arrival = ClosestTime(offset,relativeVelocity,.22f);
            if ((offset+relativeVelocity*arrival).magnitude>selfBounds.extents.magnitude+bullet.Radius+.12f) continue;
            // Face the bullet before it reaches the body, rather than aiming at a zero-length contact vector.
            float catchTime = Mathf.Max(0,arrival-.055f);
            Vector2 catchOffset = offset+relativeVelocity*catchTime;
            if (arrival>=bestTime || catchOffset.magnitude>2.1f || catchOffset.sqrMagnitude<.01f) continue;
            bestTime=arrival; direction=catchOffset.normalized;
        }
        return direction.sqrMagnitude>.01f;
    }

    public LegacyCombatIntent Legs()
    {
        var result = new LegacyCombatIntent();
        if (!Active) return result;
        Sense();
        float desire = PositionDirection();
        if (dodge.IsDodging) { result.Move = Mathf.Sign(body.linearVelocity.x); return result; }
        Motion best = new Motion(desire);
        float bestScore = Score(best, desire, out float walkingRisk);
        foreach (float move in new[] { 0f, -1f, 1f })
        {
            var candidate = new Motion(move);
            float score = Score(candidate, desire, out _);
            if (score < bestScore) { bestScore = score; best = candidate; }
        }
        bool grounded = movement.IsGrounded;
        // Jump only for a real visible threat, an edge escape, or a reachable chosen platform.
        bool needsPlatform = grounded && ChoosePlatform(desire);
        bool nearEdge = selfBounds.center.x < leftEdge + 2.6f || selfBounds.center.x > rightEdge - 2.6f;
        if (grounded && (walkingRisk > .1f || needsPlatform || nearEdge && Mathf.Abs(targetBounds.center.x-selfBounds.center.x)<4f))
        {
            foreach (float move in new[] { desire, -desire, 0f })
            {
                var candidate = new Motion(move, true);
                if (!JumpHasClearHeadroom()) continue;
                float score = Score(candidate, desire, out _);
                if (needsPlatform && routePlatform != null && move == Mathf.Sign(routePlatform.bounds.center.x-selfBounds.center.x)) score -= 1.5f;
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
        }
        bool canDodge = grounded && !player.IsInvulnerable && Time.time - dodge.LastDodgeStartedAt >= dodge.DodgeStartInterval;
        if (canDodge && walkingRisk > .1f)
        {
            // Check the entire visible stream and the endpoint after immunity, not only the closest bullet.
            foreach (float move in new[] { -1f, 1f })
            {
                if (SolidTravel(move * DodgeDistance) < 1f) continue;
                var candidate = new Motion(move, false, true);
                float score = Score(candidate, desire, out _);
                if (score < bestScore) { bestScore = score; best = candidate; }
            }
        }
        result.Move = best.Move; result.Jump = best.Jump; result.Dodge = best.Dodge;
        if (best.Move != 0f && best.Move != rememberedMove)
        { rememberedMove = best.Move; moveHeldUntil = Time.time + .16f; }
        return result;
    }

    private float PositionDirection()
    {
        float separation = targetBounds.center.x - selfBounds.center.x;
        float distance = Mathf.Abs(separation), toward = Mathf.Sign(separation);
        if (selfBounds.center.x < leftEdge + 2f) return 1f;
        if (selfBounds.center.x > rightEdge - 2f) return -1f;
        if (routePlatform != null && Time.time < routeUntil)
        {
            var destination=routePlatform.bounds;
            if (movement.IsGrounded && Mathf.Abs(selfBounds.min.y-destination.max.y)<.14f)
                routePlatform=null;
            else
            {
                float delta=destination.center.x-selfBounds.center.x;
                return Mathf.Abs(delta)<.35f?0f:Mathf.Sign(delta);
            }
        }
        if (distance < 5.3f) return -toward;
        if (distance > 8.5f) return toward;
        return 0f;
    }

    private float Score(Motion motion, float desire, out float risk)
    {
        PredictPath(motion);
        risk = 0f;
        float immunity = motion.Dodge ? Invulnerability : Mathf.Max(0, Invulnerability-(Time.time-dodge.LastDodgeStartedAt));
        for (int i = 1; i < pathCount; i++)
        {
            float t = i * Step;
            if (t < immunity) continue;
            Vector2 centre = path[i];
            foreach (var bullet in bullets)
            {
                Vector2 a = bullet.Position + bullet.Velocity * (t-Step) - path[i-1];
                Vector2 b = bullet.Position + bullet.Velocity * t - centre;
                if (!SegmentBox(a, b, (Vector2)selfBounds.extents + Vector2.one*(bullet.Radius+.04f))) continue;
                // Coordination is based on an already-open real slash, never a proposed E request.
                if (blade != null && blade.IsWindowOpen && bullet.Counterable && t < blade.WindowRemaining
                    && InsideBlade(bullet.Position + bullet.Velocity*t, blade.ActiveBladeDirection, bullet.Radius, centre)) continue;
                risk += 8f * Mathf.Max(.25f,1.3f-t);
            }
            risk += WarningRisk(centre, t);
        }
        // A later declared follow-up extends threat evaluation, not the preferred-distance lookahead.
        Vector2 end = PathAt(Horizon);
        float distance = Mathf.Abs(targetBounds.center.x + targetVelocity.x*.25f-end.x);
        float positionCost = distance < 4.2f ? (4.2f-distance)*1.3f : distance>9f ? (distance-9f)*.3f : 0f;
        float edgeSpace = Mathf.Min(end.x-leftEdge,rightEdge-end.x);
        positionCost += Mathf.Max(0,2.2f-edgeSpace)*1.8f;
        float change = motion.Move != desire ? .12f : 0f;
        if (Time.time < moveHeldUntil && motion.Move != rememberedMove) change += .18f;
        return risk + positionCost + change + (motion.Jump ? .8f : 0f) + (motion.Dodge ? 1.6f : 0f);
    }

    private float WarningRisk(Vector2 centre, float t)
    {
        if (!threat.Visible) return 0f;
        float start = Mathf.Max(0, threat.TimeToImpact);
        float active = Mathf.Max(.08f, threat.ActiveRemaining);
        var projected = new Bounds(centre, selfBounds.size);
        switch (threat.Kind)
        {
            case LegacyThreatKind.GhostSlash:
            case LegacyThreatKind.PredatorDrop:
                if (t < start-.03f || t > start+active+.06f) return 0f;
                if (!threat.DangerArea.Intersects(projected)) return 0f;
                return threat.Kind == LegacyThreatKind.PredatorDrop ? 3.5f : 2.7f;
            case LegacyThreatKind.Crosscut:
                float currentPass = t >= start-.03f && t <= start+active+.06f
                    && threat.DangerArea.Intersects(projected) ? (threat.FollowupExpected ? 3.1f : 2.7f) : 0f;
                return Mathf.Max(currentPass, CrosscutFollowupRisk(projected,t));
            case LegacyThreatKind.MarkedLocked:
            case LegacyThreatKind.SweepLow:
            case LegacyThreatKind.SweepHigh:
            case LegacyThreatKind.Burst:
                if (!threat.Locked && threat.Kind != LegacyThreatKind.SweepLow && threat.Kind != LegacyThreatKind.SweepHigh) return 0f;
                Vector2 relative = centre-threat.Origin;
                float distance = Vector2.Dot(relative, threat.Direction);
                if (distance < 0 || threat.ProjectileSpeed <= 0) return 0f;
                float arrival = start + distance/threat.ProjectileSpeed;
                // The displayed sweep is a stream; reserve an exit through its whole visible active segment.
                float width = threat.Kind == LegacyThreatKind.MarkedLocked ? .08f : active;
                if (t < arrival-.045f || t > arrival+width) return 0f;
                float cross = Mathf.Abs(Cross(relative,threat.Direction));
                float half = Mathf.Abs(threat.Direction.y)*selfBounds.extents.x+Mathf.Abs(threat.Direction.x)*selfBounds.extents.y;
                return cross < half+.13f ? 2.2f : 0f;
            default: return 0f; // A still-tracking mark does not reveal its eventual locked line.
        }
    }

    private void PredictPath(Motion motion)
    {
        float horizon=Horizon;
        if (threat.Visible && threat.Kind==LegacyThreatKind.Crosscut && threat.FollowupExpected
            && threat.FollowupSpeed>0f)
        {
            float firstEnds=threat.TimeToImpact+threat.ActiveRemaining;
            float endpoint=CommittedCrosscutEndX();
            // Bound the second pass using own possible movement before its re-sample. No future police target is read.
            float returnDistance=Mathf.Abs(endpoint-selfBounds.center.x)+movement.MoveSpeed*firstEnds+2f;
            horizon=Mathf.Clamp(firstEnds+threat.FollowupGap+returnDistance/threat.FollowupSpeed+.1f,Horizon,3.2f);
        }
        pathCount=Mathf.Min(path.Length,Mathf.CeilToInt(horizon/Step)+1);
        Vector2 position = selfBounds.center;
        float vy = motion.Jump ? JumpSpeed : body.linearVelocity.y;
        bool grounded = movement.IsGrounded && !motion.Jump;
        path[0] = position;
        for (int i = 1; i < pathCount; i++)
        {
            float t = i*Step;
            float vx = motion.Move*(motion.Dodge && t<=DodgeDuration ? DodgeDistance/DodgeDuration : movement.MoveSpeed);
            Vector2 previous = position;
            position.x = Mathf.Clamp(position.x+vx*Step,leftEdge+selfBounds.extents.x+.02f,rightEdge-selfBounds.extents.x-.02f);
            // Ordinary body collision can stop a dash; immunity is not permission to phase through the police.
            var opponent = targetBounds;
            opponent.center += (Vector3)(targetVelocity*Mathf.Min(t,.2f));
            bool verticalOverlap = position.y-selfBounds.extents.y < opponent.max.y && position.y+selfBounds.extents.y > opponent.min.y;
            if (verticalOverlap && !Physics2D.GetIgnoreCollision(shape,targetShape))
            {
                if (previous.x+selfBounds.extents.x <= opponent.min.x && position.x+selfBounds.extents.x > opponent.min.x)
                    position.x = opponent.min.x-selfBounds.extents.x-.01f;
                if (previous.x-selfBounds.extents.x >= opponent.max.x && position.x-selfBounds.extents.x < opponent.max.x)
                    position.x = opponent.max.x+selfBounds.extents.x+.01f;
            }
            if (grounded && !SupportedAt(position)) grounded = false;
            if (!grounded)
            {
                vy -= gravity*Step; position.y += vy*Step;
                if (vy<=0)
                    foreach (var support in supports)
                    {
                        if (support == null || !support.enabled || !support.gameObject.activeInHierarchy) continue;
                        var bounds = support.bounds;
                        if (position.x+selfBounds.extents.x*.5f<bounds.min.x || position.x-selfBounds.extents.x*.5f>bounds.max.x) continue;
                        float previousFoot=previous.y-selfBounds.extents.y, foot=position.y-selfBounds.extents.y;
                        if (previousFoot>=bounds.max.y-.03f && foot<=bounds.max.y)
                        { position.y=bounds.max.y+selfBounds.extents.y; vy=0; grounded=true; }
                    }
            }
            path[i]=position;
        }
    }

    private float CommittedCrosscutEndX()
    {
        // The current lane's far end is already displayed. Its .85 offset / 1.8 blade width are the original hitbox.
        float direction=Mathf.Sign(threat.Direction.x);
        return threat.DangerArea.center.x-direction*.85f
            +direction*Mathf.Max(0,threat.DangerArea.size.x-1.8f)*.5f;
    }

    private float CrosscutFollowupRisk(Bounds projected,float time)
    {
        if (!threat.FollowupExpected || threat.FollowupSpeed<=0f) return 0f;
        float firstEnds=threat.TimeToImpact+threat.ActiveRemaining;
        float secondStarts=firstEnds+threat.FollowupGap;
        if (time<secondStarts-.03f) return 0f;
        float fromX=CommittedCrosscutEndX();
        // Crosscut publicly re-samples only after its first pass. Predict that sample from this candidate's own
        // trajectory; it can be wrong. At CrosscutGap the actual second lock replaces this forecast automatically.
        float expectedSelfX=PathAt(firstEnds).x;
        float direction=expectedSelfX<fromX?-1f:1f;
        float toX=Mathf.Clamp(expectedSelfX+direction*2f,leftEdge+.9f,rightEdge-.9f);
        float duration=Mathf.Max(.1f,Mathf.Abs(toX-fromX)/threat.FollowupSpeed);
        if (time>secondStarts+duration+.04f) return 0f;
        float progress=Mathf.Clamp(time-secondStarts,0,duration);
        float previousProgress=Mathf.Clamp(time-Step-secondStarts,0,duration);
        float currentX=Mathf.MoveTowards(fromX,toX,threat.FollowupSpeed*progress);
        float previousX=Mathf.MoveTowards(fromX,toX,threat.FollowupSpeed*previousProgress);
        // Preserve the original swept blade geometry and the visible body's gravity/support prediction.
        float height=PredictTarget(firstEnds).y;
        var returnBlade=new Bounds(new Vector2((currentX+previousX)*.5f+direction*.85f,height),
            new Vector3(1.8f+Mathf.Abs(currentX-previousX),1.3f,1f));
        return returnBlade.Intersects(projected)?2.7f:0f;
    }

    private Vector2 PathAt(float seconds)
    {
        float sample=Mathf.Clamp(seconds/Step,0,pathCount-1);
        int index=Mathf.FloorToInt(sample);
        return Vector2.Lerp(path[index],path[Mathf.Min(pathCount-1,index+1)],sample-index);
    }

    private bool SupportedAt(Vector2 centre)
    {
        float foot=centre.y-selfBounds.extents.y;
        foreach (var support in supports)
        {
            if (support==null || !support.enabled) continue;
            var bounds=support.bounds;
            if (Mathf.Abs(foot-bounds.max.y)<.12f && centre.x>bounds.min.x-selfBounds.extents.x*.5f
                && centre.x<bounds.max.x+selfBounds.extents.x*.5f) return true;
        }
        return false;
    }

    private bool ChoosePlatform(float desire)
    {
        if (routePlatform!=null && Time.time<routeUntil)
            return selfBounds.min.y<routePlatform.bounds.max.y-.15f;
        routePlatform=null;
        bool pressure=threat.Visible && (threat.Kind==LegacyThreatKind.SweepLow || threat.Kind==LegacyThreatKind.GhostSlash
            || threat.Kind==LegacyThreatKind.Crosscut);
        bool edge=Mathf.Min(selfBounds.center.x-leftEdge,rightEdge-selfBounds.center.x)<3.3f;
        if (!pressure && !edge) return false;
        float best=float.PositiveInfinity;
        foreach(var platform in supports)
        {
            var bounds=platform.bounds;
            float rise=bounds.max.y-selfBounds.min.y;
            if(rise<.4f || rise>JumpSpeed*JumpSpeed/(2f*gravity)-.08f || bounds.size.x>10f) continue;
            float landing=(JumpSpeed+Mathf.Sqrt(JumpSpeed*JumpSpeed-2f*gravity*rise))/gravity;
            float landingX=Mathf.Clamp(selfBounds.center.x+desire*movement.MoveSpeed*landing,bounds.min.x+.5f,bounds.max.x-.5f);
            float reach=Mathf.Abs(landingX-selfBounds.center.x);
            if(reach>movement.MoveSpeed*landing-.1f) continue;
            float cost=reach+Mathf.Abs(Mathf.Abs(landingX-targetBounds.center.x)-6f)*.3f;
            if(cost<best) {best=cost;routePlatform=platform;}
        }
        routeUntil=Time.time+1.25f;
        return routePlatform!=null;
    }

    private bool JumpHasClearHeadroom()
    {
        foreach(var hit in Physics2D.BoxCastAll(selfBounds.center,selfBounds.size*.9f,0,Vector2.up,.3f,LayerMask.GetMask("Ground")))
            if(hit.collider!=null && !hit.collider.isTrigger && hit.collider.GetComponent<PlatformEffector2D>()==null && hit.distance>.01f) return false;
        return true;
    }

    private float SolidTravel(float distance)
    {
        float available=Mathf.Abs(distance),direction=Mathf.Sign(distance);
        available=Mathf.Min(available,direction>0?rightEdge-selfBounds.max.x:selfBounds.min.x-leftEdge);
        if(selfBounds.max.y>targetBounds.min.y && selfBounds.min.y<targetBounds.max.y && !Physics2D.GetIgnoreCollision(shape,targetShape))
        {
            float gap=direction>0?targetBounds.min.x-selfBounds.max.x:selfBounds.min.x-targetBounds.max.x;
            if(gap>=0) available=Mathf.Min(available,gap);
        }
        return available;
    }

    private Vector2 PredictTarget(float seconds)
    {
        Vector2 point=(Vector2)targetBounds.center+targetVelocity*seconds;
        if(targetBody.bodyType==RigidbodyType2D.Dynamic && Mathf.Abs(targetVelocity.y)>.1f)
        {
            float g=Mathf.Abs(Physics2D.gravity.y*targetBody.gravityScale);
            point.y-=.5f*g*seconds*seconds;
            float floor=-2.5f+targetBounds.extents.y;
            foreach(var support in supports)
                if(support!=null && support.enabled && support.bounds.max.y<=targetBounds.min.y+.12f
                    && point.x>=support.bounds.min.x && point.x<=support.bounds.max.x)
                    floor=Mathf.Max(floor,support.bounds.max.y+targetBounds.extents.y);
            point.y=Mathf.Max(point.y,floor);
        }
        point.x=Mathf.Clamp(point.x,leftEdge+targetBounds.extents.x,rightEdge-targetBounds.extents.x);
        return point;
    }

    private bool ProjectileBlocked(Vector2 origin,Vector2 direction,float distance)
    {
        foreach(var hit in Physics2D.RaycastAll(origin,direction,distance))
        {
            if(hit.collider==null || hit.collider==shape || hit.collider==targetShape) continue;
            // Ground alone passes old bullets. Only a real receiver in the current arena changes that rule.
            if(hit.collider.GetComponentInParent<IProjectileReceiver>()!=null) return true;
        }
        return false;
    }

    private bool InsideBlade(Vector2 point,Vector2 direction,float radius,Vector2? origin=null)
    {
        Vector2 offset=point-(origin??(Vector2)selfBounds.center);
        float along=Vector2.Dot(offset,direction),across=Mathf.Abs(Cross(offset,direction));
        return along>.2f-radius && along<1.9f+radius && across<.85f+radius;
    }

    private static float ClosestTime(Vector2 offset,Vector2 velocity,float maximum)
        => velocity.sqrMagnitude<.001f?0f:Mathf.Clamp(-Vector2.Dot(offset,velocity)/velocity.sqrMagnitude,0,maximum);
    private static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;

    private static bool SegmentBox(Vector2 from,Vector2 to,Vector2 half)
    {
        Vector2 delta=to-from;
        float first=0,last=1;
        for(int axis=0;axis<2;axis++)
        {
            if(Mathf.Abs(delta[axis])<.0001f) {if(Mathf.Abs(from[axis])>half[axis]) return false;continue;}
            float a=(-half[axis]-from[axis])/delta[axis],b=(half[axis]-from[axis])/delta[axis];
            if(a>b) {float swap=a;a=b;b=swap;}
            first=Mathf.Max(first,a);last=Mathf.Min(last,b);
            if(first>last) return false;
        }
        return true;
    }
}
