using System;
using UnityEngine;

namespace GoGhost.Duel
{
    [Serializable]
    public sealed class BossMotionProfile
    {
        public float Speed = 5.8f, Acceleration = 45f, Braking = 55f;
        public float JumpSpeed = 12f, GravityScale = 3f, AirControl = .45f;
        public int Fingerprint => Speed.GetHashCode() ^ Acceleration.GetHashCode() * 31
            ^ Braking.GetHashCode() * 47 ^ JumpSpeed.GetHashCode() * 71
            ^ GravityScale.GetHashCode() * 101 ^ AirControl.GetHashCode() * 137;
    }

    /// <summary>The sole writer of a Boss Rigidbody. Visuals and skills submit requests.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    [DefaultExecutionOrder(100)] // Damage readers see the completed last step before this writer advances its origin.
    public sealed class BossMotor2D : MonoBehaviour
    {
        public BossMotionProfile Profile { get; private set; }
        public Vector2 Position => body != null ? body.position + centreOffset : (Vector2)transform.position;
        public Vector2 PreviousPosition { get; private set; }
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public Vector2 Size => shape != null ? (Vector2)shape.bounds.size : new Vector2(.7f, 1.65f);
        public bool Grounded { get; private set; }
        public bool DashActive { get; private set; }
        public bool DashBlocked { get; private set; }
        public bool RouteFailed { get; private set; }
        public string RouteFailure { get; private set; }
        public bool RouteCompleted { get; private set; }
        public bool HasRoute => route != null;
        public ArenaNavigation2D Navigation => navigation;
        Rigidbody2D body;
        BoxCollider2D shape;
        Vector2 centreOffset, dashDirection, progressPosition;
        ArenaNavigation2D navigation;
        ArenaNavigation2D.Route route;
        int stepIndex;
        bool enabledCombat, launched, leftGround, jumpRequested, dashStopPending;
        float move, speedScale = 1, dashRemaining, dashSpeed, routeAge, progressAge;

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
        }
        public void SetCombatEnabled(bool value)
        {
            enabledCombat = value;
            if (!value) Stop();
        }
        public void SetMove(float direction, float speedScale = 1)
        { move = Mathf.Clamp(direction, -1, 1); this.speedScale = Mathf.Clamp01(speedScale); }
        public void Brake() { route = null; BrakeMotion(); }
        public void ClearRoute() { route = null; launched = leftGround = false; BrakeMotion(); }
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
        }
        public void SetRoute(ArenaNavigation2D.Route value)
        {
            route = value; stepIndex = 0; launched = leftGround = false;
            RouteFailed = false; RouteFailure = null; RouteCompleted = value == null || value.Steps.Count == 0;
            routeAge = progressAge = 0; progressPosition = Position;
            if (RouteCompleted) route = null;
        }
        public void Stop()
        {
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
            if (DashActive)
            {
                dashRemaining -= Mathf.Max(0, Vector2.Dot(here - PreviousPosition, dashDirection));
                if (dashStopPending || dashRemaining <= .025f)
                {
                    // The earlier damage reader has consumed the last completed dash segment.
                    // End its horizontal drive now; ordinary gravity keeps the current vertical velocity.
                    DashActive = dashStopPending = false; Brake();
                    body.linearVelocity = new Vector2(0, body.linearVelocity.y);
                    PreviousPosition = here;
                    return;
                }
                else
                {
                    float travel = Mathf.Min(dashRemaining, dashSpeed * dt);
                    Vector2 desired = here + dashDirection * travel;
                    if (Mathf.Abs(dashDirection.y) < .01f)
                        desired.y += (body.linearVelocity.y + Physics2D.gravity.y * Profile.GravityScale * dt) * dt;
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
            if (jumpRequested)
            {
                vertical = Profile.JumpSpeed;
                jumpRequested = false; Grounded = false;
            }
            float horizontal = StepHorizontal(body.linearVelocity.x, move * Profile.Speed * speedScale, Grounded, Profile, dt);
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
        float SteerTo(float x) => Mathf.Clamp((x - Position.x) / Mathf.Max(.01f, Profile.Speed * Time.fixedDeltaTime), -1, 1);
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
