using System;
using UnityEngine;

namespace GoGhost.Duel
{
    [Serializable]
    public sealed class BossMotionProfile
    {
        public float Speed = 5.8f, Acceleration = 45f, Braking = 55f;
        public float JumpSpeed = 12f, GravityScale = 3f, AirControl = .45f;
        [NonSerialized] public bool ArrivalBraking;
        public int Fingerprint => Speed.GetHashCode() ^ Acceleration.GetHashCode() * 31
            ^ Braking.GetHashCode() * 47 ^ JumpSpeed.GetHashCode() * 71
            ^ GravityScale.GetHashCode() * 101 ^ AirControl.GetHashCode() * 137 ^ (ArrivalBraking?173:0);
    }

    /// <summary>The sole writer of a Boss Rigidbody. Visuals and skills submit requests.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DefaultExecutionOrder(100)] // Damage readers see the completed last step before this writer advances its origin.
    public sealed class BossMotor2D : MonoBehaviour
    {
        public BossMotionProfile Profile { get; private set; }
        BossMotionProfile routeMotion;
        public BossMotionProfile TraversalProfile => routeMotion ?? Profile;
        public Vector2 Position => body != null ? body.position + centreOffset : (Vector2)transform.position;
        public Vector2 PreviousPosition { get; private set; }
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public Vector2 Size => shape != null ? (Vector2)shape.bounds.size : new Vector2(.7f, 1.65f);
        public bool Grounded { get; private set; }
        public bool DashActive { get; private set; }
        public bool DashBlocked { get; private set; }
        public bool DropBlocked { get; private set; }
        public bool RouteFailed { get; private set; }
        public string RouteFailure { get; private set; }
        public bool RouteCompleted { get; private set; }
        public bool HasRoute => route != null;
        public ArenaNavigation2D Navigation => navigation;
        public float JumpStartedAt { get; private set; } = -1;
        Rigidbody2D body;
        BoxCollider2D shape;
        Vector2 centreOffset, dashDirection, progressPosition;
        ArenaNavigation2D navigation;
        ArenaNavigation2D.Route route;
        int stepIndex;
        bool enabledCombat, launched, leftGround, jumpRequested, dashStopPending;
        float move, speedScale = 1, dashRemaining, dashSpeed, routeAge, progressAge;
        bool dropping, dropRequested, stepping;
        float dropHorizontal, stepRemaining,dropLandingX;
        bool fixedDropLanding;
        Vector2 stepPosition;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>(); shape = GetComponent<BoxCollider2D>();
            centreOffset = (Vector2)shape.bounds.center - body.position;
            PreviousPosition = Position;
        }
        public void Configure(BossMotionProfile profile, ArenaNavigation2D nav)
        {
            if (body == null) Awake();
            Profile = profile ?? new BossMotionProfile(); navigation = nav;
            body.gravityScale = Profile.GravityScale;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Stop();
            // Preparation freezes physics before the first FixedUpdate; initialize the supported pose now.
            Grounded = navigation != null && navigation.TrySupport(Position, Size, out _);
        }
        public void SetCombatEnabled(bool value)
        {
            enabledCombat = value;
            if (!value) Stop();
        }
        public void SetMove(float direction, float speedScale = 1)
        { move = Mathf.Clamp(direction, -1, 1); this.speedScale = Mathf.Clamp01(speedScale); }
        public void Brake() { route = null; routeMotion = null; stepping = false; BrakeMotion(); }
        public void HoldHorizontal()
        {
            Brake();
            if (body != null) body.linearVelocity = new Vector2(0, body.linearVelocity.y);
        }
        public void ClearRoute() { route = null; routeMotion = null; launched = leftGround = false; BrakeMotion(); }
        void BrakeMotion() { move = 0; speedScale = 1; }
        public bool TryJump()
        {
            if (!enabledCombat || !Grounded || DashActive || jumpRequested || Profile == null) return false;
            jumpRequested = true;
            return true;
        }
        public void StartDash(Vector2 direction, float distance, float speed)
        {
            if (!enabledCombat || direction.sqrMagnitude < .01f || distance <= 0 || speed <= 0) return;
            route = null; jumpRequested = false; Brake();
            dashDirection = direction.normalized; dashRemaining = distance; dashSpeed = speed;
            PreviousPosition = Position;
            DashActive = true; DashBlocked = dashStopPending = false;
            dashElapsed = dashStalled = 0;
            dashDeadline = distance / speed + .3f;
        }
        float dashElapsed, dashStalled, dashDeadline;
        public int StalledDrives { get; private set; }
        // Retire attack motion without disabling terrain/body collision or normal gravity.
        public void EndAttackMotion()
        {
            bool driven=DashActive;
            DashActive=dashStopPending=dropping=dropRequested=stepping=jumpRequested=false;
            fixedDropLanding=false;ClearRoute();
            if(body!=null)body.linearVelocity=new Vector2(0,driven?0:body.linearVelocity.y);
        }
        public bool StartCommittedJump(ArenaNavigation2D.Route value, BossMotionProfile motion=null)
        {
            if(!enabledCombat || !Grounded || DashActive || value==null || value.Version!=navigation.Version
                || value.Steps.Count!=1 || value.Steps[0].Kind!=ArenaNavigation2D.TravelKind.Jump
                || Mathf.Abs(Velocity.x)>.15f)return false;
            if(motion==null ? !navigation.ValidateStep(value.Steps[0],this)
                : !navigation.TryJumpTrajectory(Position,value.Steps[0].End,motion,Size,out _,out _))return false;
            SetRoute(value); launched=true; jumpRequested=true; JumpStartedAt=-1;
            routeMotion=motion;
            return true;
        }
        public void BeginStep(float distance)
        { Brake(); stepping = true; stepRemaining = distance; stepPosition = Position; }
        public void StartDrop(float horizontal)
        {
            if (!enabledCombat || Grounded) return;
            Brake(); jumpRequested = false; dropping = dropRequested = true; DropBlocked = false;
            dropHorizontal = Mathf.Clamp(horizontal, -Profile.Speed, Profile.Speed);
            fixedDropLanding=false;
        }
        public void StartDrop(float horizontal,float landingX)
        {StartDrop(horizontal);fixedDropLanding=true;dropLandingX=landingX;}
        public void EndDrop() { dropping = dropRequested = DropBlocked = fixedDropLanding = false; Brake(); }
        public void SetRoute(ArenaNavigation2D.Route value)
        {
            route = value; routeMotion=null; stepIndex = 0; launched = leftGround = false;
            RouteFailed = false; RouteFailure = null; RouteCompleted = value == null || value.Steps.Count == 0;
            routeAge = progressAge = 0; progressPosition = Position;
            if (RouteCompleted) route = null;
        }
        public void Stop()
        {
            JumpStartedAt=-1;
            dropping = dropRequested = stepping = false;
            DropBlocked = false;
            route = null; DashActive = false; DashBlocked = dashStopPending = false; jumpRequested = false;
            RouteFailed = RouteCompleted = false; RouteFailure = null; launched = leftGround = false;
            Brake();
            if (body != null) body.linearVelocity = Vector2.zero;
        }
        void FixedUpdate()
        {
            if (body == null || Profile == null || !enabledCombat) return;
            float dt = Time.fixedDeltaTime;
            var here = Position;
            Grounded = body.linearVelocity.y <= .1f && navigation != null
                && navigation.TrySupport(here, Size, out _);
            if (dropping && !dropRequested && body.linearVelocity.y > -.2f)
            {
                DropBlocked=false;
                foreach (var contact in Physics2D.BoxCastAll(here, Size-Vector2.one*.025f, 0, Vector2.down, .065f, LayerMask.GetMask("Player","Enemy")))
                    if (contact.collider != null && !contact.collider.isTrigger && contact.collider.attachedRigidbody != body
                        && contact.normal.y > .5f) { DropBlocked=true; break; }
            }
            if (DashActive)
            {
                float progress=Mathf.Max(0,Vector2.Dot(here-PreviousPosition,dashDirection));
                dashElapsed+=dt;
                dashStalled=progress<.001f?dashStalled+dt:0;
                // Rigidbody contact can disagree with the planning cast at a platform corner.
                // Distance alone cannot finish a drive when that contact prevents displacement.
                if(!dashStopPending&&(dashStalled>.18f||dashElapsed>dashDeadline))
                {DashBlocked=dashStopPending=true;StalledDrives++;}
                dashRemaining -= progress;
                if (dashStopPending || dashRemaining <= .025f)
                {
                    // The earlier damage reader has consumed the last completed dash segment.
                    // End the finite drive without turning an upward dash into a second ballistic launch.
                    DashActive = dashStopPending = false; Brake();
                    body.linearVelocity = Vector2.zero;
                    PreviousPosition = here;
                    return;
                }
                else
                {
                    float travel = Mathf.Min(dashRemaining, dashSpeed * dt);
                    Vector2 desired = here + dashDirection * travel;
                    // A dash drives a straight swept Rigidbody path in its committed direction.
                    // Counter gravity only during that finite drive; ordinary gravity resumes on exit.
                    bool clear = navigation == null || navigation.TryDash(here, desired, Size, out _);
                    Vector2 end = desired;
                    if (!clear)
                    {
                        navigation.TryDash(here, desired, Size, out end);
                        DashBlocked = dashStopPending = true;
                    }
                    if (!SweepBodies(here, end, out var bodyEnd))
                    {
                        end = bodyEnd; DashBlocked = dashStopPending = true;
                    }
                    Vector2 velocity = (end - here) / dt;
                    // Unity integrates gravity after FixedUpdate; the sweep used that same next velocity.
                    velocity.y -= Physics2D.gravity.y * Profile.GravityScale * dt;
                    body.linearVelocity = velocity;
                    PreviousPosition = here;
                    return;
                }
            }
            if (route != null) FollowRoute(dt);
            float vertical = body.linearVelocity.y;
            if (dropRequested) { vertical = -GetComponent<BossAgent>().Profile.DropSpeed; dropRequested = false; }
            else if(dropping&&!DropBlocked)vertical=Mathf.Min(vertical,-GetComponent<BossAgent>().Profile.DropSpeed);
            if (jumpRequested)
            {
                vertical = Profile.JumpSpeed;
                JumpStartedAt = Time.fixedTime;
                jumpRequested = false; Grounded = false;
            }
            var motion=TraversalProfile;
            float horizontal = dropping ? dropHorizontal : StepHorizontal(body.linearVelocity.x, move * motion.Speed * speedScale, Grounded, motion, dt);
            if(dropping&&fixedDropLanding)
            {
                float remaining=dropLandingX-here.x;
                horizontal=Mathf.Sign(remaining)==Mathf.Sign(dropHorizontal)
                    ?Mathf.Sign(dropHorizontal)*Mathf.Min(Mathf.Abs(dropHorizontal),Mathf.Abs(remaining)/dt):0;
            }
            if (stepping)
            {
                stepRemaining = Mathf.Max(0, stepRemaining - Mathf.Abs(here.x - stepPosition.x));
                stepPosition = here;
                horizontal = Mathf.Clamp(horizontal, -stepRemaining / dt, stepRemaining / dt);
            }
            // Sweep uses the same full box and one-way rule as navigation, and preserves ordinary gravity.
            Vector2 next = here + new Vector2(horizontal, vertical + Physics2D.gravity.y * Profile.GravityScale * dt) * dt;
            if (navigation != null && !navigation.SweepTraversal(here, next, Size, out var hit, out _)
                && Mathf.Abs(hit.normal.x) > .5f)
                horizontal = Mathf.Sign(horizontal) * Mathf.Min(Mathf.Abs(horizontal), Mathf.Max(0, hit.distance - .015f) / dt);
            // Actor contacts stay solid. Stop our own drive before contact instead of pushing a teammate
            // or a player through thin terrain; a dash's blade sweep still reaches the actual front contact.
            if (!SweepBodies(here, here + Vector2.right * (horizontal * dt), out var clearBodyEnd))
                horizontal = (clearBodyEnd.x - here.x) / dt;
            body.linearVelocity = new Vector2(horizontal, vertical);
            PreviousPosition = here;
        }
        void FollowRoute(float dt)
        {
            if (navigation == null || route.Version != navigation.Version || stepIndex >= route.Steps.Count)
            { FailRoute("terrain version or missing step"); return; }
            var step = route.Steps[stepIndex];
            routeAge += dt;
            if (step.Support == null || !step.Support.enabled || !step.Support.gameObject.activeInHierarchy)
            { FailRoute("destination support removed"); return; }
            if (!launched)
            {
                float delta = step.Start.x - Position.x;
                if (Mathf.Abs(delta) > .07f)
                    SetMove(SteerTo(step.Start.x));
                else if (step.Kind == ArenaNavigation2D.TravelKind.Walk)
                    launched = true;
                else if (!Grounded) { FailRoute("launch lost support"); return; }
                else
                {
                    BrakeMotion();
                    if (Mathf.Abs(Velocity.x) < .15f)
                    {
                        if (!navigation.ValidateStep(step, this)) { FailRoute("launch revalidation rejected"); return; }
                        launched = true;
                        if (step.Kind == ArenaNavigation2D.TravelKind.Jump) TryJump();
                    }
                }
            }
            if (launched)
            {
                if (step.Kind == ArenaNavigation2D.TravelKind.Drop && !leftGround)
                    SetMove(SteerTo(step.Launch.x));
                else SetMove(SteerTo(step.End.x));
                if (!Grounded || jumpRequested) leftGround = true;
                bool landed = Grounded && !jumpRequested && (step.Kind == ArenaNavigation2D.TravelKind.Walk || leftGround)
                    && Mathf.Abs(Position.y - step.End.y) < .16f;
                if (landed && Mathf.Abs(Position.x - step.End.x) < .18f)
                {
                    stepIndex++; launched = leftGround = false; routeAge = progressAge = 0;
                    progressPosition = Position;
                    if (stepIndex >= route.Steps.Count) { route = null; RouteCompleted = true; Brake(); }
                    return;
                }
            }
            if (Vector2.Distance(Position, progressPosition) > .10f)
            { progressPosition = Position; progressAge = 0; }
            else progressAge += dt;
            if (progressAge > .6f) FailRoute("no physical progress");
            else if (routeAge > step.Duration + 2.5f) FailRoute("step deadline");
        }
        float SteerTo(float x) => JumpHorizontalTarget(x-Position.x,TraversalProfile,Time.fixedDeltaTime)/TraversalProfile.Speed;
        public static float JumpHorizontalTarget(float delta,BossMotionProfile motion,float dt)
        {
            float speed=Mathf.Min(motion.Speed,Mathf.Abs(delta)/dt);
            if(motion.ArrivalBraking)
                speed=Mathf.Min(speed,Mathf.Sqrt(2*motion.Acceleration*motion.AirControl*Mathf.Max(0,Mathf.Abs(delta)-.04f)));
            return Mathf.Sign(delta)*speed;
        }
        bool SweepBodies(Vector2 from, Vector2 to, out Vector2 safeEnd)
        {
            safeEnd = to;
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .000001f) return true;
            int actors = LayerMask.GetMask("Player", "Enemy");
            foreach (var hit in Physics2D.BoxCastAll(from, Size - Vector2.one * .035f, 0, delta.normalized, delta.magnitude, actors))
            {
                var other = hit.collider;
                if (other == null || other.isTrigger || other.attachedRigidbody == body
                    || other.transform.IsChildOf(transform) || Physics2D.GetIgnoreCollision(shape, other)) continue;
                if (hit.distance < .003f && Vector2.Dot(delta, from - (Vector2)other.bounds.center) > 0) continue;
                if (hit.normal.y > .5f && Mathf.Abs(delta.y) < .015f) continue;
                safeEnd = from + delta.normalized * Mathf.Max(0, hit.distance - .015f);
                return false;
            }
            return true;
        }
        void FailRoute(string reason)
        {
            RouteFailure = reason;
            if (route != null && stepIndex < route.Steps.Count)
            {
                var step = route.Steps[stepIndex];
                RouteFailure += $" {step.Kind} from={step.Start} to={step.End} at={Position} velocity={Velocity}";
            }
            if (route != null && stepIndex < route.Steps.Count) navigation?.BlockTemporarily(route.Steps[stepIndex]);
            route = null; RouteFailed = true; Brake();
        }
        public static float StepHorizontal(float current, float desired, bool grounded, BossMotionProfile profile, float dt)
        {
            float acceleration = Mathf.Abs(desired) < .01f ? profile.Braking : profile.Acceleration;
            if (!grounded) acceleration *= profile.AirControl;
            return Mathf.MoveTowards(current, desired, acceleration * dt);
        }
    }
}
