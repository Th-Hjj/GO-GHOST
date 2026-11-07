using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase two, the White Boxer (Thomas, 2026-11-06 trial; tuning is provisional).
    // No AI assistance and no tell lines: each punch is read from its wind-up pose, weight and footwork.
    // Jab steps in; the low uppercut rises from a crouch; the slip dashes through to the other side
    // before a hook; the rage haymaker twists far back before lunging. One hit per punch.
    public sealed class FinalBossBoxer
    {
        public enum Punch { None, Jab, Uppercut, SlipHook, Haymaker }
        public enum Stage { Idle, Windup, Slip, Active, Recovery }

        struct Timing
        {
            public float Windup, Active, Recovery, Cooldown, Reach, Height, Lift; public int Damage;
            public Timing(float windup, float active, float recovery, float cooldown, int damage, float reach, float height, float lift)
            { Windup = windup; Active = active; Recovery = recovery; Cooldown = cooldown; Damage = damage; Reach = reach; Height = height; Lift = lift; }
        }
        static readonly Timing[] Timings =
        {
            default,
            new Timing(.22f, .10f, .25f, .8f, 12, 1.35f, .8f, .15f),   // Jab: step in
            new Timing(.38f, .12f, .45f, 2.2f, 20, 1.1f, 2.4f, .55f),  // Low uppercut: rises from the floor
            new Timing(.30f, .14f, .40f, 4.0f, 22, 1.6f, 1.1f, .20f),  // Hook after the slip
            new Timing(.75f, .14f, .80f, 5.0f, 34, 1.8f, 1.2f, .20f),  // Rage haymaker
        };
        const float SlipSpeed = 16, SlipBeyond = 1.3f, SlipSeconds = .25f, LungeDistance = 1.8f, LungeSpeed = 12, JabStep = .9f;
        const int AttackIdBase = 200000;

        readonly BossAgent actor;
        readonly Collider2D playerBody;
        readonly float[] readyAt = new float[5];
        float phaseAt, phaseUntil, nextDecisionAt, activeAt, activeEnds;
        bool hitDone, slipIgnoring;
        int attackId = AttackIdBase;

        public Punch Current { get; private set; }
        public Stage Phase { get; private set; }
        public float PhaseProgress => Mathf.Clamp01((Time.time - phaseAt) / Mathf.Max(.001f, phaseUntil - phaseAt));
        public float LockedFacing { get; private set; } = 1;
        public readonly int[] Started = new int[5], Hits = new int[5];
        public System.Action<Punch, Vector2> Thrown;   // presentation: gold burst at the fist

        public FinalBossBoxer(BossAgent owner, PlayerHealth player)
        {
            actor = owner;
            playerBody = player != null ? player.GetComponent<Collider2D>() : null;
            nextDecisionAt = Time.time + .4f;
        }

        public bool Ready(Punch punch) => punch != Punch.None && Time.time >= readyAt[(int)punch];
        public bool CanStart(Punch punch) => Current == Punch.None && actor.Running && actor.IsAlive && actor.Motor.Grounded && Ready(punch);

        public void Tick()
        {
            if (Current != Punch.None) { TickPunch(); return; }
            var player = actor.Player;
            var facts = actor.Perception.Facts;
            if (player == null || player.IsDead || facts.Confidence <= .1f) { actor.Motor.Brake(); return; }
            Vector2 target = facts.Position;
            float dx = target.x - actor.Position.x, dy = target.y - actor.Position.y, distance = Mathf.Abs(dx);
            actor.Face(new Vector2(dx, 0));
            // Keep pressing: a raised target is chased with a jump once roughly underneath it.
            if (dy > 1.4f && distance < 2.6f && actor.Motor.Grounded) { actor.Motor.SetMove(Mathf.Sign(dx)); actor.Motor.TryJump(); return; }
            if (Time.time >= nextDecisionAt && Decide(distance, dy)) return;
            actor.Motor.SetMove(distance > 1.1f ? Mathf.Sign(dx) : 0);
        }

        bool Decide(float distance, float dy)
        {
            if (Mathf.Abs(dy) > 1.6f || distance > 3.2f) return false;
            if (dy > .7f && distance < 1.8f && TryStart(Punch.Uppercut)) return true;
            // Weighted choice among the punches in range; the jab is the bread and butter.
            var choices = new List<(Punch, float)>(4);
            if (distance < 2.4f && Ready(Punch.Jab)) choices.Add((Punch.Jab, 3));
            if (distance < 2.6f && Ready(Punch.SlipHook)) choices.Add((Punch.SlipHook, 2));
            if (distance < 1.8f && Ready(Punch.Uppercut)) choices.Add((Punch.Uppercut, 1.5f));
            if (distance < 3.0f && Ready(Punch.Haymaker)) choices.Add((Punch.Haymaker, 1));
            if (choices.Count == 0) return false;
            float total = 0; foreach (var c in choices) total += c.Item2;
            float pick = Random.value * total;
            foreach (var (punch, weight) in choices) { if ((pick -= weight) <= 0) return TryStart(punch); }
            return TryStart(choices[choices.Count - 1].Item1);
        }

        public bool TryStart(Punch punch)
        {
            if (!CanStart(punch)) return false;
            var timing = Timings[(int)punch];
            Current = punch; attackId++; Started[(int)punch]++; hitDone = false;
            readyAt[(int)punch] = Time.time + timing.Cooldown;
            if (actor.Player != null) actor.Face((Vector2)actor.Player.transform.position - actor.Position);
            LockedFacing = actor.Facing;
            actor.Motor.HoldHorizontal();
            if (punch == Punch.SlipHook) BeginSlip();
            else SetPhase(Stage.Windup, timing.Windup);
            actor.Perception.ExpressedPressure(actor.Position);
            actor.Trace($"boxer {punch} attack={attackId}");
            return true;
        }

        void SetPhase(Stage stage, float seconds) { Phase = stage; phaseAt = Time.time; phaseUntil = Time.time + Mathf.Max(0, seconds); }

        // Slip through the player to the far side, then turn for the hook.
        void BeginSlip()
        {
            float dx = actor.Player.transform.position.x - actor.Position.x;
            float direction = Mathf.Sign(dx == 0 ? LockedFacing : dx);
            float distance = Mathf.Abs(dx) + SlipBeyond;
            var arena = actor.Navigation.ArenaBounds;
            float limit = direction > 0 ? arena.max.x - actor.Motor.Size.x : arena.min.x + actor.Motor.Size.x;
            distance = Mathf.Clamp(distance, .5f, Mathf.Abs(limit - actor.Position.x));
            SetIgnorePlayer(true);
            actor.Motor.StartDash(new Vector2(direction, 0), distance, SlipSpeed);
            SetPhase(Stage.Slip, Mathf.Max(SlipSeconds, distance / SlipSpeed));
        }

        void SetIgnorePlayer(bool value)
        {
            if (playerBody == null || actor.Body == null || slipIgnoring == value) return;
            Physics2D.IgnoreCollision(actor.Body, playerBody, value);
            slipIgnoring = value;
        }

        void TickPunch()
        {
            var timing = Timings[(int)Current];
            switch (Phase)
            {
                case Stage.Slip:
                    if (Time.time < phaseUntil && actor.Motor.DashActive) return;
                    actor.Motor.EndAttackMotion();
                    SetIgnorePlayer(false);
                    if (actor.Player != null) actor.Face((Vector2)actor.Player.transform.position - actor.Position);
                    LockedFacing = actor.Facing;
                    SetPhase(Stage.Windup, timing.Windup);
                    return;
                case Stage.Windup:
                    actor.Motor.HoldHorizontal();
                    if (Time.time < phaseUntil) return;
                    activeAt = Time.time; activeEnds = activeAt + timing.Active;
                    SetPhase(Stage.Active, timing.Active);
                    if (Current == Punch.Jab) { actor.Motor.BeginStep(JabStep); actor.Motor.SetMove(LockedFacing); }
                    if (Current == Punch.Haymaker) actor.Motor.StartDash(new Vector2(LockedFacing, 0), LungeDistance, LungeSpeed);
                    actor.Sounds?.Play(Current == Punch.Haymaker || Current == Punch.Uppercut ? Sfx.BladeHeavy : Sfx.BladeLight, actor.ActorId, attackId);
                    Thrown?.Invoke(Current, Fist());
                    return;
                case Stage.Active:
                    if (!hitDone && PunchArea().Intersects(PlayerBounds())) hitDone = Strike(timing.Damage);
                    if (Time.time < phaseUntil) return;
                    actor.Motor.EndAttackMotion();
                    SetPhase(Stage.Recovery, timing.Recovery);
                    return;
                default:
                    actor.Motor.HoldHorizontal();
                    if (Time.time < phaseUntil) return;
                    Current = Punch.None; Phase = Stage.Idle;
                    nextDecisionAt = Time.time + Random.Range(.20f, .40f);
                    return;
            }
        }

        public void Cancel()
        {
            SetIgnorePlayer(false);
            if (Current != Punch.None) actor.Motor.EndAttackMotion();
            Current = Punch.None; Phase = Stage.Idle;
        }

        public Vector2 Fist() => actor.Position + new Vector2(LockedFacing * Timings[(int)Current].Reach * .7f, .4f);
        public Bounds PunchArea()
        {
            var timing = Timings[(int)Current];
            // The box starts at the chest and reaches forward; the uppercut also rises above the head.
            return new Bounds(actor.Position + new Vector2(LockedFacing * timing.Reach * .5f, timing.Lift),
                new Vector3(timing.Reach + .4f, timing.Height, 1));
        }
        Bounds PlayerBounds() => playerBody != null ? playerBody.bounds : new Bounds(Vector3.one * 9999, Vector3.zero);
        bool Strike(int damage)
        {
            var player = actor.Player;
            if (player == null || player.IsDead) return false;
            int before = player.CurrentHealth;
            player.TakeDamage(damage, actor.gameObject);
            if (player.CurrentHealth < before) Hits[(int)Current]++;
            return true;
        }

        // Melee boxes for the public threat model; the wind-up itself draws no lines.
        public void Publish(List<BossThreat> output)
        {
            if (Current == Punch.None || Phase == Stage.Recovery || Phase == Stage.Slip) return;
            var phase = Phase == Stage.Windup ? BossThreatPhase.Preparation : BossThreatPhase.Active;
            float starts = Phase == Stage.Windup ? phaseUntil : activeAt, ends = starts + Timings[(int)Current].Active;
            output.Add(new BossThreat { ActorId = actor.ActorId, AttackId = attackId, Phase = phase, Shape = BossThreatShape.Box,
                Origin = actor.Position, Area = PunchArea(), Facing = LockedFacing, Valid = true, StartsAt = starts, EndsAt = ends });
        }
    }
}
