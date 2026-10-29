using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Perches come from the active layout; solo and duo use the same physical route solver.
    public sealed partial class BossTactics
    {
        const float PlatformDwell = 3.2f, PlatformMoveGap = 1.2f, BlinkHiddenSeconds = .45f;
        static readonly BossAbility[] PlatformAttackChoices = { BossAbility.Burst, BossAbility.Piercing, BossAbility.Sweep };
        readonly List<Collider2D> gunPlatforms = new List<Collider2D>(7);
        readonly Dictionary<Collider2D, float> platformVisits = new Dictionary<Collider2D, float>();
        readonly List<BossPlan> platformOptions = new List<BossPlan>(21);
        Collider2D gunSupport, previousGunSupport, blinkSupport, lastBlinkSupport;
        float supportSince, nextPlatformMove;
        int supportAttacks, platformFinishedAttacks;
        bool blinkMoved;
        BossMotionProfile platformJumpMotion;
        public bool PlatformPressureEnabled => gunPlatforms.Count > 0;
        public bool PlatformBlinkActive { get; private set; }
        public int PlatformMoves { get; private set; }
        public int PlatformBlinks { get; private set; }
        public int PlatformAttacks { get; private set; }

        public void ConfigurePlatformPressure(Collider2D[] supports)
        {
            if (actor.Profile.Kind != BossKind.Gunner || actor.IsTrainingEnemy) return;
            gunPlatforms.Clear(); platformVisits.Clear();
            float floor = float.PositiveInfinity;
            foreach (var support in supports)
                if (support != null) floor = Mathf.Min(floor, support.bounds.max.y);
            foreach (var support in supports)
                if (support != null && !support.isTrigger && support.bounds.max.y > floor + .2f
                    && !gunPlatforms.Contains(support))
                    gunPlatforms.Add(support);
            var motion = actor.Profile.Motion;
            // Reuse the approved air steering on every layout; ground speed and jump height stay unchanged.
            platformJumpMotion = new BossMotionProfile { Speed = 9.5f, Acceleration = 65f, AirControl = 1,
                Braking = motion.Braking, JumpSpeed = motion.JumpSpeed, GravityScale = motion.GravityScale, ArrivalBraking = true };
            supportSince = Time.time;
        }

        void TrackGunSupport()
        {
            if (!actor.Motor.Grounded || !actor.Navigation.TrySupport(actor.Position, actor.Motor.Size, out var support)
                || support == gunSupport) return;
            previousGunSupport = gunSupport;
            gunSupport = support; supportSince = Time.time; supportAttacks = 0;
            platformVisits[support] = Time.time;
        }

        public void PlatformAttackFinished(BossAbility ability, bool completed)
        {
            if (!PlatformPressureEnabled || !completed || ability != BossAbility.Burst && ability != BossAbility.Piercing
                && ability != BossAbility.Sweep && ability != BossAbility.TacticalJumpShot) return;
            TrackGunSupport(); supportAttacks++; platformFinishedAttacks++;
            if (gunPlatforms.Contains(gunSupport)) PlatformAttacks++;
        }

        public void CancelPlatformPressure()
        {
            if (!PlatformPressureEnabled) return;
            PlatformBlinkActive = blinkMoved = false; blinkSupport = null;
            plan = camoPlan = null; camoFollowupUntil = 0;
        }

        void TickPlatformPressure(BossObservation facts)
        {
            TrackGunSupport();
            if (plan != null && plan.Intent == "PlatformRelocate")
            {
                if (plan.TerrainVersion != actor.Navigation.Version || actor.Motor.RouteFailed
                    || Time.time > plan.CreatedAt + plan.MaxPlanTime)
                {
                    failedDestination = plan.Destination; avoidFailedUntil = Time.time + 2;
                    actor.Trace("platform route retired: " + actor.Motor.RouteFailure);
                    actor.Motor.ClearRoute(); plan = null; nextPlatformMove = Time.time + .6f;
                }
                else if (actor.Motor.RouteCompleted && Vector2.Distance(actor.Position, plan.Destination) < .6f)
                {
                    actor.Motor.HoldHorizontal(); plan = null;
                    actor.Trace("platform arrived; attack opportunity");
                }
                else return;
            }
            if (Time.time < nextTick) return;
            nextTick = Time.time + actor.Profile.TacticTick;
            if (!facts.Visible || facts.Confidence < .3f)
            {
                // Wait for the opening delayed observation. Later searches use only the last public body.
                if (facts.Body.size.sqrMagnitude > .01f && Time.time >= nextPlatformMove)
                {
                    if (TryPlatformCrossing(facts)) return;
                    // Reacquire from a new angle using the frozen last-seen body, never the hidden Transform.
                    if (Time.time - facts.ObservedAt > .6f && TryStartCamo()) return;
                    if (TryPlatformJump(facts)) return;
                    nextPlatformMove = Time.time + .6f;
                }
                actor.Motor.HoldHorizontal(); return;
            }

            bool elevated = gunPlatforms.Contains(gunSupport);
            bool close = Mathf.Abs(actor.Position.x - facts.Position.x) < 4
                && Mathf.Abs(actor.Position.y - facts.Position.y) < 2.4f;
            bool exposed = exposedSince >= 0 && Time.time - exposedSince >= .65f;
            // A new perch earns an attack before another relocation. The opening earns one attack on the floor.
            bool move = supportAttacks > 0 && (!elevated || supportAttacks >= 2
                || Time.time - supportSince >= PlatformDwell || close || exposed);
            if (move && Time.time >= nextPlatformMove)
            {
                if (TryPlatformCrossing(facts)) return;
                if (elevated && platformFinishedAttacks >= 2 && TryStartCamo()) return;
                if (TryPlatformJump(facts)) return;
                nextPlatformMove = Time.time + .6f;
            }
            var ability = PickPlatformAttack(actor.Position, facts);
            if (ability != BossAbility.None && TryImmediate(ability, facts)) return;
            if (actor.Actions.Ammo < actor.Profile.Magazine) actor.Actions.RequestReload();
            // No legal shot for a bounded interval can also justify movement, without waiting for player pressure.
            if (Time.time - supportSince > PlatformDwell && Time.time >= nextPlatformMove)
            {
                if (TryPlatformJump(facts)) return;
                if (elevated && TryStartCamo()) return;
                nextPlatformMove = Time.time + .6f;
            }
            actor.Motor.HoldHorizontal();
        }

        BossAbility PickPlatformAttack(Vector2 point, BossObservation facts)
        {
            BossAbility best = BossAbility.None; float score = float.NegativeInfinity;
            foreach (var ability in PlatformAttackChoices)
            {
                if (!actor.Actions.Ready(ability) || !actor.Actions.CanAfford(ability)
                    || actor.Coordinator != null && !actor.Coordinator.CanStart(actor, ability, false)
                    || !LegalAt(ability, point, facts, out float chance)) continue;
                float value = chance * .7f + (ability == BossAbility.Burst ? 1 : ability == BossAbility.Piercing ? 1.1f : 1.05f);
                if (ability == previousAbility) value -= .45f;
                if (facts.ReloadVisible && ability == BossAbility.Piercing) value += .65f;
                if (ability == BossAbility.Sweep && actor.Actions.Ammo - actor.Profile.Sweep.Cost < 3) value -= .6f;
                if (value > score) { score = value; best = ability; }
            }
            return best;
        }

        Vector2 PlatformPoint(Collider2D support, float offset = 0)
            => new Vector2(support.bounds.center.x + offset, support.bounds.max.y + actor.Motor.Size.y * .5f + .025f);

        bool PlatformPointValid(Collider2D support, Vector2 point, BossObservation facts)
        {
            return support != null && support.enabled && support.gameObject.activeInHierarchy
                && !new Bounds(point, actor.Motor.Size + Vector2.one * .5f).Intersects(facts.Body)
                && (actor.Coordinator == null || !actor.Coordinator.Occupied(actor, point))
                && actor.Navigation.CanStand(point, actor.Motor.Size)
                && actor.Navigation.TrySupport(point, actor.Motor.Size, out var actual) && actual == support;
        }

        float PlatformScore(Collider2D support, Vector2 point, BossObservation facts)
        {
            float separation = Mathf.Abs(point.x - facts.Position.x);
            float angle = Vector2.Angle(actor.Position - facts.Position, point - facts.Position);
            float score = 2 - Mathf.Abs(separation - EngagementDistance) * .12f
                + Mathf.Min(angle / 45, 1) * .8f - Mathf.Abs(point.y - facts.Position.y - 1.7f) * .13f;
            if (Mathf.Abs(point.y - actor.Position.y) > .6f) score += .5f;
            if (BossPerception.CanSee(point + Vector2.up * .35f, facts.Body)) score += 3;
            if (separation < 3.2f) score -= 1.2f;
            if (support == previousGunSupport) score -= .8f;
            if (platformVisits.TryGetValue(support, out float visited)) score -= Mathf.Max(0, 1 - (Time.time - visited) / 10) * 1.3f;
            return score;
        }

        bool TryPlatformJump(BossObservation facts)
        {
            platformOptions.Clear();
            float height = ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile);
            foreach (var support in gunPlatforms)
            {
                if (support == gunSupport) continue;
                // Centre first, then two safe offsets; route solving remains bounded.
                for (int i = 0; i < 3; i++)
                {
                    Vector2 point = PlatformPoint(support, i == 0 ? 0 : (i == 1 ? -1 : 1) * support.bounds.size.x * .25f);
                    if (Mathf.Abs(point.y - actor.Position.y) > height
                        || Vector2.Distance(point, actor.Position) > 10
                        || Time.time < avoidFailedUntil && Vector2.Distance(point, failedDestination) < .9f
                        || !PlatformPointValid(support, point, facts)) continue;
                    platformOptions.Add(new BossPlan { Destination = point, Score = PlatformScore(support, point, facts)
                        - Vector2.Distance(point, actor.Position) * .12f });
                }
            }
            platformOptions.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < Mathf.Min(6, platformOptions.Count); i++)
            {
                var choice = platformOptions[i];
                if (!TryPlatformRoute(choice.Destination, out var route, out var path)) continue;
                bool direct = route.Steps.Count == 1;
                choice.Route = route; choice.JumpPath = path; choice.TerrainVersion = actor.Navigation.Version;
                choice.JumpMotion = platformJumpMotion;
                choice.CreatedAt = Time.time; choice.TravelSeconds = route.Duration; choice.MaxPlanTime = route.Duration + .65f;
                choice.TargetHypothesis = PredictBody(facts, .5f); choice.Ability = BossAbility.TacticalJumpShot;
                if (direct && facts.Visible && actor.Actions.Ready(BossAbility.TacticalJumpShot)
                    && actor.Actions.CanAfford(BossAbility.TacticalJumpShot) && !actor.Actions.Reloading
                    && actor.Actions.TryStartMobility(choice))
                {
                    previousAbility = BossAbility.TacticalJumpShot; lastAttackAt = Time.time; plan = null;
                }
                else
                {
                    choice.Intent = "PlatformRelocate"; choice.Ability = BossAbility.None;
                    plan = choice; actor.Motor.SetRoute(route, platformJumpMotion);
                }
                nextPlatformMove = Time.time + PlatformMoveGap;
                PlatformMoves++; GunRepositions++;
                actor.Trace("platform jump to=" + choice.Destination + " attack=" + actor.Actions.Ability);
                return true;
            }
            return false;
        }

        bool TryPlatformRoute(Vector2 destination, out ArenaNavigation2D.Route route, out Vector2[] path)
        {
            var nav = actor.Navigation; var motor = actor.Motor;
            if (nav.TryJumpTrajectory(actor.Position, destination, platformJumpMotion, motor.Size, out route, out path)) return true;
            if (!nav.TrySupport(actor.Position, motor.Size, out var source)) return false;
            float inset = motor.Size.x * .5f + .15f;
            var launch = new Vector2(Mathf.Clamp(destination.x, source.bounds.min.x + inset, source.bounds.max.x - inset),
                source.bounds.max.y + motor.Size.y * .5f + .025f);
            launch.x = Mathf.MoveTowards(actor.Position.x, launch.x, 3);
            if (!nav.TryRoute(actor.Position, launch, motor, out var approach) || approach.Duration > 1.2f) return false;
            foreach (var step in approach.Steps) if (step.Kind != ArenaNavigation2D.TravelKind.Walk) return false;
            if (!nav.TryJumpTrajectory(launch, destination, platformJumpMotion, motor.Size, out var jump, out path)) return false;
            route = approach; route.Steps.AddRange(jump.Steps); route.Duration += jump.Duration;
            return true;
        }

        // Advance through reachable supports toward the last public target. No origin, bank or
        // platform index assumptions: W1 naturally uses its middle bridge; W3 uses its staggered perches.
        bool TryPlatformCrossing(BossObservation facts)
        {
            float gap = facts.Position.x - actor.Position.x;
            if (!gunPlatforms.Contains(gunSupport) || Mathf.Abs(gap) < EngagementDistance * .7f) return false;
            float direction = Mathf.Sign(gap);
            platformOptions.Clear();
            foreach (var support in gunPlatforms)
            {
                if (support == gunSupport) continue;
                float inset = actor.Motor.Size.x * .5f + .15f;
                if (support.bounds.size.x < inset * 2) continue;
                Vector2 point = PlatformPoint(support);
                point.x = Mathf.Clamp(actor.Position.x, support.bounds.min.x + inset, support.bounds.max.x - inset);
                float progress = (point.x - actor.Position.x) * direction;
                float rise = Mathf.Abs(point.y - actor.Position.y);
                if (progress < 1 || progress > Mathf.Abs(gap) + .5f || progress > 10
                    || rise > ArenaNavigation2D.ExecutableJumpHeight(platformJumpMotion)
                    || Time.time < avoidFailedUntil && Vector2.Distance(point, failedDestination) < .9f
                    || !PlatformPointValid(support, point, facts)) continue;
                platformOptions.Add(new BossPlan { Destination = point, Score = progress - rise * 2
                    + PlatformScore(support, point, facts) * .2f });
            }
            platformOptions.Sort((a, b) => b.Score.CompareTo(a.Score));
            for (int i = 0; i < Mathf.Min(6, platformOptions.Count); i++)
            {
                var point = platformOptions[i].Destination;
                if (!TryPlatformRoute(point, out var route, out var path)) continue;
                plan = new BossPlan { Intent = "PlatformRelocate", Destination = point, Route = route, JumpPath = path,
                    JumpMotion = platformJumpMotion, TerrainVersion = actor.Navigation.Version, CreatedAt = Time.time,
                    MaxPlanTime = route.Duration + .65f };
                actor.Motor.SetRoute(route, platformJumpMotion);
                nextPlatformMove = Time.time + PlatformMoveGap; PlatformMoves++; GunRepositions++;
                actor.Trace("platform crossing via=" + point); return true;
            }
            return false;
        }

        bool BeginPlatformBlink(BossObservation facts)
        {
            TrackGunSupport(); platformOptions.Clear(); float total = 0;
            float centre = actor.Navigation.ArenaBounds.center.x;
            foreach (var support in gunPlatforms)
            {
                if (support == gunSupport || support == lastBlinkSupport) continue;
                Vector2 point = PlatformPoint(support);
                if (!PlatformPointValid(support, point, facts) || Vector2.Distance(point, facts.Position) < 3.2f
                    || !BossPerception.CanSee(point + Vector2.up * .35f, facts.Body)) continue;
                float weight = Mathf.Max(.15f, PlatformScore(support, point, facts));
                if ((point.x - centre) * (actor.Position.x - centre) < 0) weight *= 2.5f;
                if (Mathf.Abs(point.y - actor.Position.y) > .6f) weight *= 1.3f;
                platformOptions.Add(new BossPlan { Destination = point, Score = weight }); total += weight;
            }
            if (platformOptions.Count == 0) return false;
            float pick = Random.value * total; var chosen = platformOptions[platformOptions.Count - 1];
            foreach (var option in platformOptions) { pick -= option.Score; if (pick <= 0) { chosen = option; break; } }
            if (!actor.Navigation.TrySupport(chosen.Destination, actor.Motor.Size, out var supportAtTarget)) return false;
            chosen.Ability = PickPlatformAttack(chosen.Destination, facts);
            if (chosen.Ability == BossAbility.None) chosen.Ability = BossAbility.Burst;
            chosen.TargetHypothesis = facts.Position; chosen.TerrainVersion = actor.Navigation.Version;
            if (!actor.Actions.TryStart(BossAbility.Camo, chosen.TargetHypothesis)) return false;
            actor.Motor.HoldHorizontal(); plan = null; camoPlan = chosen;
            blinkSupport = supportAtTarget; blinkMoved = false; PlatformBlinkActive = true;
            actor.Trace("platform camo committed to=" + chosen.Destination);
            return true;
        }

        // Called only by Camo's real Active phase. Exactly one fixed destination, with no mid-hide retargeting.
        public bool TickPlatformBlink(float hiddenSeconds)
        {
            if (!PlatformBlinkActive) return false;
            if (hiddenSeconds < BlinkHiddenSeconds) return false;
            if (camoPlan != null && camoPlan.TerrainVersion == actor.Navigation.Version
                && blinkSupport != null && blinkSupport.enabled && blinkSupport.gameObject.activeInHierarchy
                && Vector2.Distance(PlatformPoint(blinkSupport), camoPlan.Destination) < .05f
                && (actor.Coordinator == null || !actor.Coordinator.Occupied(actor, camoPlan.Destination))
                && actor.Motor.TryCamoRelocate(camoPlan.Destination))
            {
                blinkMoved = true; PlatformBlinks++; lastBlinkSupport = blinkSupport;
                TrackGunSupport(); actor.PublishCue(.12f);
                actor.Trace("platform camo relocated=" + actor.Position);
            }
            else actor.Trace("platform camo destination invalid; reveal at current position");
            return true;
        }

        void EndPlatformBlink()
        {
            PlatformBlinkActive = false;
            if (blinkMoved)
            {
                camoFollowupUntil = Time.time + actor.Profile.Camo.Recovery + .45f;
                nextPlatformMove = Time.time + PlatformMoveGap;
            }
            else camoPlan = null;
            blinkSupport = null; blinkMoved = false;
        }
    }
}
