using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        // The view reads the accepted move. Candidate casts never change the live pose.
        bool VaultActive => Current == Move.AnchorVault && Phase == Stage.Active;
        public bool AnchorLatched => VaultActive && acceptedPlan.Vault.Latched;
        public bool AnchorPulling => AnchorLatched && acceptedPlan.Vault.Pulling;
        public float AnchorLatchProgress => AnchorLatched ? acceptedPlan.Vault.LatchProgress : 0f;
        public bool BracedOnAxe => VaultActive && acceptedPlan.Vault.Braced;

        sealed class AnchorVault
        {
            const float MinCast = 5.5f, MaxCast = 11f, MaxLeap = 7f;
            const float CastSpeed = 18f, PullSpeed = 22f, LatchTime = .16f, BraceTime = .20f;
            const float ChopDuration = .26f;

            enum Step { Outbound, Pulling, Braced, Leaping, Aborted }

            readonly FinalBossAxe axe;
            readonly Vector2 grip;
            readonly float castFacing;
            Vector2 castStart, latchOffset;
            Step step;
            float caughtAt = -1f;
            ArenaNavigation2D.Route jumpRoute;
            BossMotionProfile jumpMotion;
            Vector2[] jumpPath;
            float cutDelay;

            public FlightLeg Outbound { get; private set; }
            public float LaunchAge { get; private set; }
            public Vector2[] LaunchedPath => Launched ? jumpPath : null;
            public Cut Chop { get; private set; }
            public bool Launched => step == Step.Leaping;
            public bool Braced => step == Step.Braced;
            public bool Pulling => step == Step.Pulling;
            public bool Latched => axe.AxeFlying && axe.retrieveAt < 0 && axe.ActiveTime >= Outbound.End;
            public float LatchProgress => Mathf.Clamp01((axe.ActiveTime - Outbound.End) / LatchTime);

            AnchorVault(FinalBossAxe axe, Vector2 start, Vector2 grip, float facing, FlightLeg outbound)
            {
                this.axe = axe;
                this.grip = grip;
                castFacing = facing;
                castStart = start;
                Outbound = outbound;
            }

            // This search commits only a grip and a clear pull. The second jump does not exist yet.
            public static bool TryPlan(FinalBossAxe axe, AxePlan plan)
            {
                var motor = axe.actor.Motor;
                float gap = Mathf.Abs(plan.Target.x - plan.Start.x);
                float ceiling = axe.actor.Navigation.ArenaBounds.max.y - motor.Size.y * .5f - .30f;
                var sites = axe.attackSites;
                sites.Clear();
                for (int i = 0; i <= 5; i++)
                {
                    float span = Mathf.Lerp(MinCast, MaxCast, i / 5f);
                    for (int level = 0; level < 3; level++)
                    {
                        float rise = 1.15f + level * .85f;
                        float y = Mathf.Max(plan.Start.y + .25f, Mathf.Lerp(plan.Start.y, plan.Target.y, .55f) + rise);
                        var grip = new Vector2(plan.Start.x + plan.Facing * span, Mathf.Min(y, ceiling));
                        if (!axe.WithinArena(grip, motor.Size)) continue;
                        float cost = Mathf.Abs(Mathf.Abs(span - gap) - 4.8f) + level * .12f;
                        if (span > gap)
                        {
                            float crossHeight = Mathf.Lerp(plan.Start.y, grip.y, gap / span) - plan.Target.y;
                            cost += Mathf.Max(0, motor.Size.y + .12f - crossHeight) * 2f;
                        }
                        sites.Add(new Site { Point = grip, Cost = cost });
                    }
                }
                sites.Sort(SiteOrder);
                foreach (var site in sites)
                {
                    if (!axe.actor.Navigation.TryDash(plan.Start, site.Point, motor.Size, out _)) continue;
                    var leg = CastLeg(axe, plan.Start, site.Point, plan.Facing);
                    axe.candidateFlight.Clear();
                    axe.candidateFlight.Add(leg);
                    if (!axe.FlightIsClear(axe.candidateFlight, 0, plan.Facing)) continue;
                    plan.Vault = new AnchorVault(axe, plan.Start, site.Point, plan.Facing, leg);
                    return true;
                }
                return false;
            }

            static FlightLeg CastLeg(FinalBossAxe axe, Vector2 start, Vector2 grip, float facing)
            {
                Vector2 delta = grip - start;
                float angle = Mathf.Clamp(Mathf.Atan2(delta.y, Mathf.Abs(delta.x)) * Mathf.Rad2Deg, 8f, 42f);
                var from = Pivot(start, facing, new Vector2(.39f, .31f), angle);
                var to = Pivot(grip, facing, new Vector2(.38f, .34f), angle);
                return new FlightLeg(from, axe.LimitFlightPoint(Vector2.Lerp(from, to, .36f) + Vector2.down * .44f),
                    axe.LimitFlightPoint(Vector2.Lerp(from, to, .78f) + Vector2.down * .60f),
                    to, 0f, Vector2.Distance(start, grip) / CastSpeed, 0, 16, angle, angle - 360f);
            }

            // A body collision during wind-up can move the release point, never the locked grip.
            public bool RebaseRelease(Vector2 start)
            {
                castStart = start;
                Outbound = CastLeg(axe, start, grip, castFacing);
                float span = (grip.x - start.x) * castFacing;
                return span >= MinCast && span <= MaxCast;
            }

            public void Tick(float age)
            {
                var motor = axe.actor.Motor;
                if (step == Step.Outbound)
                {
                    if (!axe.AxeFlying || age < Outbound.End + LatchTime) return;
                    if (!axe.actor.Navigation.TryDash(axe.actor.Position, grip, motor.Size, out _))
                    {
                        Abort();
                        return;
                    }
                    step = Step.Pulling;
                    Vector2 delta = grip - axe.actor.Position;
                    motor.StartDash(delta, delta.magnitude, PullSpeed);
                }
                if (step == Step.Pulling)
                {
                    // Check contact even on the tick that started the pull.
                    if (motor.CatchAxeGrip(grip)) CatchGrip(age);
                    else if (motor.DashBlocked ||
                        age > Outbound.End + LatchTime + Vector2.Distance(castStart, grip) / PullSpeed + .22f)
                        Abort();
                }
                else if (Braced && age >= caughtAt + BraceTime) Launch(age);
            }

            void CatchGrip(float age)
            {
                caughtAt = age;
                latchOffset = Outbound.D - Shaft(castFacing, Outbound.AngleA) * PivotOffset - axe.actor.Position;
                axe.AxeFlying = false;
                if (!PlanGripLeap())
                {
                    Abort();
                    return;
                }
                step = Step.Braced;
                axe.activeEnds = axe.phaseUntil = axe.activeAt + age + BraceTime + JumpDuration;
                axe.actor.Sounds?.Play(Sfx.ReturnPrepare, axe.actor.ActorId, axe.attackId);
            }

            bool PlanGripLeap()
            {
                var facts = axe.actor.Perception.Facts;
                if (!facts.Visible || facts.Confidence <= .12f || facts.Body.size.x <= 0) return false;
                Vector2 from = axe.actor.Position;
                Vector2 seen = axe.PublicPosition(facts, 0);
                Vector2 aim = Vector2.MoveTowards(seen, axe.PublicPosition(facts, .16f), .85f);
                float dx = seen.x - from.x;
                float side = Mathf.Abs(dx) > .10f ? Mathf.Sign(dx) :
                    (Mathf.Abs(aim.x - from.x) > .05f ? Mathf.Sign(aim.x - from.x) : castFacing);
                float gap = Mathf.Max(0, (aim.x - from.x) * side);
                float desired = Mathf.Clamp(gap - .6f, gap >= 4f ? 4f : .25f, MaxLeap);
                // Even a blocked landing leaves the Boss facing the opponent he just saw.
                axe.actor.Face(Vector2.right * side);
                axe.lockedFacing = axe.actor.Facing;

                var sites = axe.attackSites;
                sites.Clear();
                foreach (var node in axe.actor.Navigation.Nodes)
                {
                    Vector2 land = node.Center(axe.actor.Motor.Size);
                    float travel = (land.x - from.x) * side;
                    if (travel < 0 || travel > MaxLeap || travel > Mathf.Max(1.5f, gap + .75f)) continue;
                    if (travel < .5f && gap > 2f) continue;
                    float cost = Mathf.Abs(travel - desired) + 1.15f * Mathf.Abs(land.y - aim.y);
                    if (gap >= 4f && travel < 4f) cost += 1.75f;
                    sites.Add(new Site { Point = land, Cost = cost });
                }
                sites.Sort(SiteOrder);
                foreach (var site in sites)
                {
                    if (TryRebound(from, site.Point, aim, side, 11.6f) ||
                        TryRebound(from, site.Point, aim, side, 7.4f)) return true;
                }
                return false;
            }

            bool TryRebound(Vector2 from, Vector2 landing, Vector2 aim, float side, float impulse)
            {
                var motor = axe.actor.Motor;
                var navigation = axe.actor.Navigation;
                float gravity = -Physics2D.gravity.y * motor.Profile.GravityScale;
                float room = navigation.ArenaBounds.max.y - motor.Size.y * .5f - from.y - .05f;
                if (gravity <= 0 || room <= 0) return false;
                float kick = Mathf.Min(impulse, Mathf.Sqrt(2f * gravity * room));
                float discriminant = kick * kick + 2f * gravity * (from.y - landing.y);
                if (discriminant < 0) return false;
                float airtime = (kick + Mathf.Sqrt(discriminant)) / gravity;
                var motion = axe.CreateLeapMotion(kick);
                motion.Speed = Mathf.Clamp(Mathf.Abs(landing.x - from.x) / Mathf.Max(.10f, airtime - .10f), 1.4f, 14.5f);
                if (!navigation.TryJumpTrajectory(from, landing, motion, motor.Size, out var route, out var path) ||
                    !axe.PathInBounds(path)) return false;

                int apex = 0;
                while (apex + 1 < path.Length && path[apex + 1].y > path[apex].y) apex++;
                float latest = Mathf.Max(.04f, (path.Length - 1) * Time.fixedDeltaTime - ChopDuration);
                float earliest = Mathf.Min(latest, Mathf.Max(.04f, apex * Time.fixedDeltaTime - .04f));
                cutDelay = ClosestCutStart(path, aim, side, earliest, latest, .18f);
                jumpMotion = motion;
                jumpRoute = route;
                jumpPath = path;
                return true;
            }

            float JumpDuration => (jumpPath.Length - 1) * Time.fixedDeltaTime;

            void Launch(float age)
            {
                // Validate the same route again; the sighting and landing are fixed at the catch.
                if (!axe.actor.Motor.ReboundFromGrip(grip, jumpRoute, jumpMotion))
                {
                    Abort();
                    return;
                }
                step = Step.Leaping;
                LaunchAge = age;
                Chop = new Cut(age + cutDelay, ChopDuration, 112f, -78f, 1, 34, 2.9f);
                axe.activeEnds = axe.phaseUntil = axe.activeAt + age + Mathf.Max(JumpDuration, cutDelay + ChopDuration);
                axe.actor.Sounds?.Play(Sfx.BladeHeavy, axe.actor.ActorId, axe.attackId);
            }

            void Abort()
            {
                step = Step.Aborted;
                axe.actor.Motor.EndAttackMotion();
                axe.abortAfterCatch = true;
                if (axe.AxeFlying) axe.BeginRetrieval();
                else axe.activeEnds = axe.phaseUntil = Time.time;
            }

            public Vector2 GripOffset(float age)
            {
                if (caughtAt < 0)
                    return Vector2.Lerp(new Vector2(.39f, .31f), new Vector2(.38f, .34f), Mathf.InverseLerp(.54f, 1.15f, age));
                var latch = new Vector2(latchOffset.x * axe.lockedFacing, latchOffset.y);
                return Vector2.Lerp(latch, new Vector2(.45f, .20f), RaiseProgress(age));
            }

            float RaiseProgress(float age) => Mathf.Clamp01((age - caughtAt) / BraceTime);

            public bool TryGetWindupAngle(float age, out float angle)
            {
                angle = Outbound.AngleA;
                // A failed pull can finish a harmless retrieval without ever touching the grip.
                if (caughtAt < 0) return step != Step.Aborted || axe.AxeFlying;
                if (Launched && age >= Chop.Start) return false;
                float catchAngle = castFacing == axe.lockedFacing ? angle : 180f - angle;
                angle = Mathf.LerpAngle(catchAngle, 112f + EdgeOffset, RaiseProgress(age));
                return true;
            }
        }
    }
}
