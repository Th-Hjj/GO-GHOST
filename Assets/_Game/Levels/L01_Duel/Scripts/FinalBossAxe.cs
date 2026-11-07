using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase-one axe kit (Thomas, 2026-11-06 trial; tuning values are provisional).
    // The boss's own operating AI chooses between four telegraphed moves. Boss Rules (stage 4)
    // call TryStart before this AI. One clock drives pose, tells, damage and the public threats
    // that player rules observe. Preparation tracks the target; Lock freezes the attack.
    public sealed class FinalBossAxe
    {
        public enum Move { None, Chop, Spin, Throw, Boomerang }
        public enum Stage { Idle, Prepare, Locked, Active, Recovery }

        struct Timing
        {
            public float Prepare, Lock, Active, Recovery, Cooldown; public int Damage;
            public Timing(float prepare, float locked, float active, float recovery, float cooldown, int damage)
            { Prepare = prepare; Lock = locked; Active = active; Recovery = recovery; Cooldown = cooldown; Damage = damage; }
        }
        static readonly Timing[] Timings =
        {
            default,
            new Timing(.42f, .14f, .12f, .50f, 1.8f, 26),  // Chop
            new Timing(.45f, .15f, 1.0f, .65f, 5.0f, 20),  // Spin
            new Timing(.40f, .15f, 0, .35f, 4.5f, 18),     // Throw & Recall (active from the path)
            new Timing(.45f, .15f, 1.6f, .40f, 5.5f, 20),  // Boomerang
        };
        public const float ChopReach = 2.3f, ChopHeight = 2.1f, SpinRadius = 1.65f, AxeRadius = .38f;
        const float ThrowSpeed = 15, RecallSpeed = 17, ThrowHold = .25f, ThrowRange = 12, SpinMove = .45f;
        const int BoomerangSamples = 25;
        const int AttackIdBase = 100000;
        // Three invasions at these phase-one health fractions; the rule lands near the end of the animation.
        static readonly float[] InvasionThresholds = { .75f, .50f, .25f };
        public const float InvasionDuration = 1.8f, InvasionInject = 1.4f;

        readonly BossAgent actor;
        readonly System.Action onPhaseTwo;
        readonly System.Func<ConditionalRule> onInvasion;
        float invasionAt;
        bool invasionInjected;
        readonly Collider2D playerBody;
        readonly float[] readyAt = new float[5];
        readonly List<Vector2> path = new List<Vector2>(BoomerangSamples);
        float phaseAt, phaseUntil, activeAt, activeEnds, nextDecisionAt, transitionUntil, nextImpact;
        float throwOut, throwBack, lockedFacing = 1;
        Vector2 previousAxe, spinCentre;
        bool hitDone, outHit, backHit;
        int attackId = AttackIdBase;

        public Move Current { get; private set; }
        public Stage Phase { get; private set; }
        public float PhaseProgress => Mathf.Clamp01((Time.time - phaseAt) / Mathf.Max(.001f, phaseUntil - phaseAt));
        public float ActiveProgress => Mathf.Clamp01((Time.time - activeAt) / Mathf.Max(.001f, activeEnds - activeAt));
        public bool Committed => Current != Move.None || Transitioning || Invading;
        public bool Invading { get; private set; }
        public int Invasions { get; private set; }
        public float InvasionProgress => Mathf.Clamp01((Time.time - invasionAt) / InvasionDuration);
        public ConditionalRule LastInvasion { get; private set; }
        public bool Transitioning { get; private set; }
        public bool PhaseTwo { get; private set; }
        // The scripted phase change drops the axe; the Boss fights bare-handed afterwards.
        public bool Unarmed { get; private set; }
        public FinalBossBoxer Boxer { get; private set; }
        bool transitionHeld;
        public bool AxeFlying { get; private set; }
        public Vector2 AxePosition { get; private set; }
        public float AxeSpin { get; private set; }
        public float LockedFacing => lockedFacing;
        public IReadOnlyList<Vector2> Path => path;
        public readonly int[] Started = new int[5], Hits = new int[5];
        public FinalBossRules Rules { get; }

        public FinalBossAxe(BossAgent owner, PlayerHealth player, System.Action phaseTwo, System.Func<ConditionalRule> invasion)
        {
            actor = owner; onPhaseTwo = phaseTwo; onInvasion = invasion;
            playerBody = player != null ? player.GetComponent<Collider2D>() : null;
            for (int i = 0; i < readyAt.Length; i++) readyAt[i] = 0;
            Rules = new FinalBossRules(owner);
            nextDecisionAt = Time.time + 1.0f;
        }

        public bool Ready(Move move) => move != Move.None && Time.time >= readyAt[(int)move];
        public bool CanStart(Move move) => move != Move.None && Current == Move.None && !Transitioning
            && actor.Running && actor.IsAlive && !actor.Health.AwaitingSecondPhase && actor.Motor.Grounded && Ready(move);

        public void Tick()
        {
            // Phase two seals the Boss's rules together with the player's.
            Rules.Sealed = PhaseTwo || Transitioning || actor.Health.AwaitingSecondPhase;
            Rules.Observe();
            if (TickTransition()) return;
            if (Unarmed) { TickUnarmed(); return; }
            if (TickInvasion()) return;
            if (Current != Move.None) { TickMove(); return; }
            var player = actor.Player;
            if (player == null || player.IsDead) { actor.Motor.Brake(); return; }
            if (Rules.TryExecute(this)) return;
            var facts = actor.Perception.Facts;
            if (facts.Confidence <= .1f) { actor.Motor.Brake(); return; }
            Vector2 target = facts.Position, position = actor.Position;
            float dx = target.x - position.x, distance = Mathf.Abs(dx);
            bool level = Mathf.Abs(target.y - position.y) < 1.4f;
            if (Time.time >= nextDecisionAt && Decide(distance, level)) return;
            // Close in on a level target; keep a throwing lane on a raised one.
            if (level) actor.Motor.SetMove(distance > 2.0f ? Mathf.Sign(dx) : 0);
            else actor.Motor.SetMove(distance < 3f ? -Mathf.Sign(dx) : distance > 6f ? Mathf.Sign(dx) : 0, .7f);
        }

        bool Decide(float distance, bool level)
        {
            if (level && distance < 2.5f && TryStart(Move.Chop)) return true;
            if (level && distance < 2.2f && TryStart(Move.Spin)) return true;
            if (distance >= 6.5f && TryStart(Move.Boomerang)) return true;
            if ((distance >= 3.5f || !level) && TryStart(Move.Throw)) return true;
            if (!level && TryStart(Move.Boomerang)) return true;
            return false;
        }

        public bool TryStart(Move move)
        {
            if (!CanStart(move)) return false;
            var timing = Timings[(int)move];
            Current = move; attackId++; Started[(int)move]++;
            hitDone = outHit = backHit = false; AxeFlying = false;
            readyAt[(int)move] = Time.time + timing.Cooldown;
            actor.Motor.HoldHorizontal();
            TrackTarget();
            SetPhase(Stage.Prepare, timing.Prepare);
            PlanActiveWindow();
            actor.Sounds?.Play(Sfx.DrawPrepare, actor.ActorId, attackId);
            actor.Perception.ExpressedPressure(actor.Position);
            actor.Trace($"axe prepare {move} attack={attackId}");
            return true;
        }

        void SetPhase(Stage stage, float seconds) { Phase = stage; phaseAt = Time.time; phaseUntil = Time.time + Mathf.Max(0, seconds); }

        void TickMove()
        {
            var timing = Timings[(int)Current];
            if (Phase == Stage.Prepare)
            {
                TrackTarget(); PlanActiveWindow();
                if (Time.time >= phaseUntil) { SetPhase(Stage.Locked, timing.Lock); actor.Sounds?.Play(Sfx.ReturnPrepare, actor.ActorId, attackId); }
                actor.Motor.HoldHorizontal();
                return;
            }
            if (Phase == Stage.Locked)
            {
                actor.Motor.HoldHorizontal();
                if (Time.time >= phaseUntil) Activate();
                return;
            }
            if (Phase == Stage.Active) { TickActive(); return; }
            actor.Motor.HoldHorizontal();
            if (Time.time >= phaseUntil) Finish();
        }

        void Activate()
        {
            Phase = Stage.Active; activeAt = Time.time; PlanActiveWindow();
            SetPhase(Stage.Active, activeEnds - activeAt);
            spinCentre = actor.Position;
            previousAxe = path.Count > 0 ? path[0] : actor.Position;
            actor.Sounds?.Play(Current == Move.Chop ? Sfx.BladeHeavy : Sfx.BladeLight, actor.ActorId, attackId);
        }

        void TickActive()
        {
            int damage = Timings[(int)Current].Damage;
            switch (Current)
            {
                case Move.Chop:
                    actor.Motor.HoldHorizontal();
                    if (!hitDone && ChopArea().Intersects(PlayerBounds())) hitDone = Strike(damage);
                    break;
                case Move.Spin:
                    float toward = actor.Player != null ? Mathf.Sign(actor.Player.transform.position.x - actor.Position.x) : 0;
                    actor.Motor.SetMove(toward, SpinMove);
                    AxeSpin = ActiveProgress * 360f * 3f;
                    if (!hitDone && PlayerBounds().SqrDistance(actor.Position) <= SpinRadius * SpinRadius) hitDone = Strike(damage);
                    break;
                case Move.Throw:
                case Move.Boomerang:
                    actor.Motor.HoldHorizontal();
                    AxeFlying = true;
                    Vector2 axe = AxeAt(Time.time, out bool returning);
                    AxePosition = axe; AxeSpin -= Time.deltaTime * 900f * lockedFacing;
                    bool alreadyHit = returning ? backHit : outHit;
                    if (!alreadyHit && SweptHit(previousAxe, axe))
                    { if (Strike(damage)) { if (returning) backHit = true; else outHit = true; } }
                    previousAxe = axe;
                    break;
            }
            if (Time.time >= activeEnds)
            {
                AxeFlying = false; AxeSpin = 0;
                if (Current == Move.Spin) actor.Motor.HoldHorizontal();
                SetPhase(Stage.Recovery, Timings[(int)Current].Recovery);
            }
        }

        void Finish()
        {
            actor.Trace($"axe finish {Current} attack={attackId}");
            Current = Move.None; Phase = Stage.Idle; AxeFlying = false; path.Clear();
            nextDecisionAt = Time.time + .25f;
        }

        public void Cancel()
        {
            Boxer?.Cancel();
            if (Invading) { Invading = false; actor.Health.Invulnerable = false; }
            Current = Move.None; Phase = Stage.Idle; AxeFlying = false; AxeSpin = 0; path.Clear();
            nextDecisionAt = Time.time + .5f;
        }

        // At each threshold the Boss finishes his current move, lands, then stands invulnerable and
        // writes one random legal rule into the player's fight rules. Thresholds crossed together queue.
        bool TickInvasion()
        {
            if (!Invading)
            {
                var health = actor.Health;
                if (Invasions >= InvasionThresholds.Length || Current != Move.None || PhaseTwo || health.PhaseIndex != 1
                    || health.AwaitingSecondPhase || !actor.Motor.Grounded) return false;
                if ((float)health.CurrentHealth / health.MaxHealth > InvasionThresholds[Invasions]) return false;
                Invading = true; invasionInjected = false; invasionAt = Time.time;
                health.Invulnerable = true;
                actor.Motor.HoldHorizontal();
                if (actor.Player != null) actor.Face((Vector2)actor.Player.transform.position - actor.Position);
                lockedFacing = actor.Facing;
                actor.Sounds?.Play(Sfx.Glitch, actor.ActorId);
                actor.Trace($"invasion {Invasions + 1} begins");
            }
            actor.Motor.HoldHorizontal();
            if (!invasionInjected && Time.time - invasionAt >= InvasionInject)
            {
                invasionInjected = true;
                LastInvasion = onInvasion?.Invoke();
                actor.Sounds?.Play(Sfx.Glitch, actor.ActorId);
            }
            if (Time.time - invasionAt < InvasionDuration) return true;
            Invading = false; Invasions++;
            actor.Health.Invulnerable = false;
            nextDecisionAt = Time.time + .3f;
            return true;
        }

        // Placeholder until the scripted transition (stage 7): hold, strip the player's rules,
        // refill the second pool and continue as a white stickman with the same kit.
        bool TickTransition()
        {
            if (!Transitioning && !PhaseTwo && actor.Health.AwaitingSecondPhase)
            {
                Cancel(); Transitioning = true; transitionUntil = Time.time + 2.2f; nextImpact = 0;
                // The game's scripted transition holds the Boss until its countdown ends.
                transitionHeld = onPhaseTwo != null;
                actor.Motor.HoldHorizontal();
                onPhaseTwo?.Invoke();
                actor.Trace("final boss phase 2: transition");
            }
            if (!Transitioning) return false;
            actor.Motor.HoldHorizontal();
            if (!transitionHeld && Time.time >= nextImpact) { nextImpact = Time.time + .45f; actor.View?.Impact(actor.Position); }
            if (transitionHeld || Time.time < transitionUntil) return true;
            actor.Health.BeginSecondPhase();
            Transitioning = false; PhaseTwo = true;
            actor.View?.SetFinalBossPhaseTwo();
            nextDecisionAt = Time.time + .6f;
            return true;
        }

        public void DropAxe()
        {
            if (Unarmed) return;
            Unarmed = true;
            Boxer = new FinalBossBoxer(actor, actor.Player);
            actor.View?.DropAxe();
            actor.View?.BindBoxer(Boxer);
        }

        public void ReleaseTransition() => transitionHeld = false;

        // Phase-two retry: no transition replay; the White Boxer starts on his full second pool.
        public void StartInPhaseTwo()
        {
            Cancel();
            actor.Health.SkipToSecondPhase();
            PhaseTwo = true; Transitioning = false; transitionHeld = false;
            Unarmed = true;
            Boxer = new FinalBossBoxer(actor, actor.Player);
            actor.View?.SetFinalBossPhaseTwo();
            actor.View?.SetEyesRed(true);
            actor.View?.BindBoxer(Boxer);
            actor.View?.DropAxe();
        }

        // Phase two: the White Boxer kit fights bare-handed.
        void TickUnarmed()
        {
            if (PhaseTwo) Boxer?.Tick();
            else actor.Motor.HoldHorizontal();
        }

        void TrackTarget()
        {
            var facts = actor.Perception.Facts;
            Vector2 target = facts.Confidence > .1f ? facts.Position : actor.Position + Vector2.right * actor.Facing;
            actor.Face(target - actor.Position);
            lockedFacing = actor.Facing;
            if (Current != Move.Throw && Current != Move.Boomerang) return;
            var arena = actor.Navigation.ArenaBounds;
            Vector2 hand = Hand();
            path.Clear(); path.Add(hand);
            if (Current == Move.Throw)
            {
                Vector2 direction = target - hand;
                if (direction.sqrMagnitude < .01f) direction = Vector2.right * lockedFacing;
                direction.Normalize();
                float reach = Mathf.Min(ThrowRange, ArenaBoundary2D.ExitDistance(arena, hand, direction, AxeRadius));
                path.Add(hand + direction * Mathf.Max(1, reach));
                return;
            }
            // Boomerang: out along a high arc to the target, back along a low arc to the hand.
            Vector2 end = ArenaBoundary2D.ClampPoint(arena, target, AxeRadius + .1f);
            Vector2 mid = (hand + end) * .5f;
            Vector2 high = new Vector2(mid.x, Mathf.Min(Mathf.Max(hand.y, end.y) + 2.8f, arena.max.y - AxeRadius - .2f));
            Vector2 low = new Vector2(mid.x, Mathf.Max(Mathf.Min(hand.y, end.y) - .8f, arena.min.y + AxeRadius + .45f));
            int half = BoomerangSamples / 2;
            for (int i = 1; i <= half; i++) path.Add(Bezier(hand, high, end, i / (float)half));
            for (int i = 1; i < BoomerangSamples - half; i++) path.Add(Bezier(end, low, hand, i / (float)(BoomerangSamples - half - 1)));
        }

        void PlanActiveWindow()
        {
            // Activate sets activeAt itself; earlier phases predict it from the remaining tell.
            if (Phase == Stage.Prepare) activeAt = phaseUntil + Timings[(int)Current].Lock;
            else if (Phase == Stage.Locked) activeAt = phaseUntil;
            float active = Timings[(int)Current].Active;
            if (Current == Move.Throw && path.Count == 2)
            {
                float length = Vector2.Distance(path[0], path[1]);
                throwOut = length / ThrowSpeed; throwBack = length / RecallSpeed;
                active = throwOut + ThrowHold + throwBack;
            }
            activeEnds = activeAt + active;
        }

        Vector2 AxeAt(float time, out bool returning)
        {
            float t = time - activeAt; returning = false;
            if (Current == Move.Throw && path.Count == 2)
            {
                if (t <= throwOut) return Vector2.Lerp(path[0], path[1], t / Mathf.Max(.001f, throwOut));
                if (t <= throwOut + ThrowHold) return path[1];
                returning = true;
                return Vector2.Lerp(path[1], path[0], (t - throwOut - ThrowHold) / Mathf.Max(.001f, throwBack));
            }
            if (path.Count < 2) return Hand();
            float f = Mathf.Clamp01(t / Mathf.Max(.001f, activeEnds - activeAt)) * (path.Count - 1);
            int i = Mathf.Min(path.Count - 2, Mathf.FloorToInt(f));
            returning = i >= BoomerangSamples / 2;
            return Vector2.Lerp(path[i], path[i + 1], f - i);
        }

        public Vector2 Hand() => actor.Position + new Vector2(lockedFacing * .35f, .25f);
        public Bounds ChopArea() => new Bounds(actor.Position + new Vector2(lockedFacing * ChopReach * .5f, .05f), new Vector3(ChopReach, ChopHeight, 1));
        Bounds SpinArea()
        {
            float travel = actor.Motor.Profile.Speed * SpinMove * Timings[(int)Move.Spin].Active;
            var area = new Bounds(actor.Position, new Vector3(SpinRadius * 2, SpinRadius * 2, 1));
            area.Encapsulate(new Bounds(actor.Position + Vector2.right * lockedFacing * travel, area.size));
            return area;
        }
        Bounds PlayerBounds() => playerBody != null ? playerBody.bounds : new Bounds(Vector3.one * 9999, Vector3.zero);
        bool SweptHit(Vector2 from, Vector2 to)
        {
            var bounds = PlayerBounds();
            for (int i = 0; i <= 4; i++)
                if (bounds.SqrDistance(Vector2.Lerp(from, to, i / 4f)) <= AxeRadius * AxeRadius) return true;
            return false;
        }
        bool Strike(int damage)
        {
            var player = actor.Player;
            if (player == null || player.IsDead) return false;
            int before = player.CurrentHealth;
            player.TakeDamage(damage, actor.gameObject);
            if (player.CurrentHealth < before) Hits[(int)Current]++;
            return true;
        }
        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t) => Vector2.Lerp(Vector2.Lerp(a, b, t), Vector2.Lerp(b, c, t), t);

        // Expressed geometry for player rules (Incoming Attack). Chop and Spin are melee boxes;
        // the flying axe is a ranged projectile split into its actual path segments.
        public void Publish(List<BossThreat> output)
        {
            if (Unarmed) { Boxer?.Publish(output); return; }
            if (Current == Move.None || Phase == Stage.Recovery || Phase == Stage.Idle) return;
            var phase = Phase == Stage.Prepare ? BossThreatPhase.Preparation : Phase == Stage.Locked ? BossThreatPhase.Locked : BossThreatPhase.Active;
            var threat = new BossThreat { ActorId = actor.ActorId, AttackId = attackId, Phase = phase, Origin = actor.Position,
                Facing = lockedFacing, Valid = true, TerrainBlocked = false, StartsAt = activeAt, EndsAt = activeEnds };
            if (Current == Move.Chop || Current == Move.Spin)
            {
                threat.Shape = BossThreatShape.Box;
                threat.Area = Current == Move.Chop ? ChopArea() : SpinArea();
                output.Add(threat);
                return;
            }
            if (path.Count < 2) return;
            threat.Shape = BossThreatShape.Projectile; threat.Radius = AxeRadius;
            if (Current == Move.Throw)
            {
                AddSegment(output, threat, path[0], path[1], activeAt, activeAt + throwOut);
                AddSegment(output, threat, path[1], path[0], activeAt + throwOut + ThrowHold, activeEnds);
                return;
            }
            float step = (activeEnds - activeAt) / (path.Count - 1);
            for (int i = 0; i < path.Count - 1; i++)
            {
                float end = activeAt + step * (i + 1);
                if (Phase == Stage.Active && end < Time.time) continue;
                AddSegment(output, threat, path[i], path[i + 1], activeAt + step * i, end);
            }
        }
        static void AddSegment(List<BossThreat> output, BossThreat threat, Vector2 from, Vector2 to, float starts, float ends)
        {
            if (ends < Time.time) return;
            Vector2 delta = to - from;
            threat.Origin = from; threat.End = to; threat.Direction = delta.sqrMagnitude > .0001f ? delta.normalized : Vector2.right;
            threat.ProjectileSpeed = delta.magnitude / Mathf.Max(.001f, ends - starts);
            threat.StartsAt = starts; threat.EndsAt = ends;
            output.Add(threat);
        }
    }
}
