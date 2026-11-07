using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // The Final Boss's fixed trial rule list (Thomas, 2026-11-06). Conditions reuse the player
    // catalog's wording and thresholds, read from the Boss's side through public observations.
    // Top to bottom, the first matching rule that can execute runs before the Boss's own AI;
    // cooldowns and an axe already in use let lower rows and then the AI act instead.
    public sealed class FinalBossRules
    {
        public enum BossAction { Jump, Chop, Spin, Throw, Boomerang }

        public sealed class Row
        {
            public ConditionalRule Condition;
            public BossAction Action;
            public string ActionLabel;
            public bool Matched, Armed = true;
            public long ConsumedOccurrence;
            public int Executed;
        }

        const float EventWindow = ConditionalDefaults.EventWindow, IncomingHorizon = ConditionalDefaults.ThreatWindow;
        readonly BossAgent actor;
        readonly List<Row> rows = new List<Row>(5);
        bool close, far, wasGrounded, incoming;
        int lastHealth, lastPhase;
        long landedOccurrence, damagedOccurrence;
        float landedAt = -100, damagedAt = -100;

        public IReadOnlyList<Row> Rows => rows;
        public bool Sealed { get; set; }

        public FinalBossRules(BossAgent owner)
        {
            actor = owner;
            Add(new ConditionalRule { Condition = ConditionalCatalog.IncomingAttackCondition, IncomingAttack = IncomingAttackOption.Ranged }, BossAction.Jump, "Jump");
            Add(new ConditionalRule { Condition = 3 }, BossAction.Chop, "Chop");
            Add(new ConditionalRule { Condition = ConditionalCatalog.EnemyDistanceCondition, Distance = EnemyDistanceOption.Close }, BossAction.Spin, "Spin");
            Add(new ConditionalRule { Condition = 6 }, BossAction.Throw, "Throw & Recall");
            Add(new ConditionalRule { Condition = ConditionalCatalog.EnemyDistanceCondition, Distance = EnemyDistanceOption.Far }, BossAction.Boomerang, "Boomerang");
        }

        void Add(ConditionalRule condition, BossAction action, string label)
        {
            condition.DisplayOrder = rows.Count;
            rows.Add(new Row { Condition = condition, Action = action, ActionLabel = label });
        }

        public static ControlOperation DisplayOperation(BossAction action)
            => action == BossAction.Jump ? ControlOperation.Jump : ControlOperation.Fire;

        // Every frame, including during moves, so the sidebar reflects what the Boss currently sees.
        public void Observe()
        {
            bool grounded = actor.Motor.Grounded;
            if (grounded && !wasGrounded) { landedOccurrence++; landedAt = Time.time; }
            wasGrounded = grounded;
            var health = actor.Health;
            if (health.PhaseIndex == lastPhase && health.CurrentHealth < lastHealth) { damagedOccurrence++; damagedAt = Time.time; }
            lastHealth = health.CurrentHealth; lastPhase = health.PhaseIndex;
            var facts = actor.Perception.Facts;
            bool known = facts.Confidence > .1f;
            float distance = known ? Mathf.Abs(facts.Position.x - actor.Position.x) : 0;
            close = known && distance <= (close ? ConditionalDefaults.Close + ConditionalDefaults.Hysteresis : ConditionalDefaults.Close);
            far = known && distance >= (far ? ConditionalDefaults.Far - ConditionalDefaults.Hysteresis : ConditionalDefaults.Far);
            incoming = IncomingRanged();
            foreach (var row in rows)
            {
                bool matched = !Sealed && Matches(row);
                if (!matched) row.Armed = true;
                row.Matched = matched;
            }
        }

        bool Matches(Row row)
        {
            switch (row.Condition.Condition)
            {
                case ConditionalCatalog.IncomingAttackCondition: return incoming;
                case 3: return Time.time - landedAt <= EventWindow && row.ConsumedOccurrence != landedOccurrence;
                case 6: return Time.time - damagedAt <= EventWindow && row.ConsumedOccurrence != damagedOccurrence;
                case ConditionalCatalog.EnemyDistanceCondition: return row.Condition.Distance == EnemyDistanceOption.Close ? close : far;
                default: return false;
            }
        }

        // A public player bullet that will reach the Boss's body within the incoming horizon.
        bool IncomingRanged()
        {
            var body = new Bounds(actor.Position, actor.Motor.Size);
            foreach (var shot in Projectile.Active)
            {
                if (shot == null || shot.OwnerLayer == actor.gameObject.layer || shot.Owner == actor.gameObject) continue;
                var rigidbody = shot.GetComponent<Rigidbody2D>();
                if (rigidbody == null) continue;
                Vector2 velocity = rigidbody.linearVelocity;
                float speed = velocity.magnitude;
                if (speed < .01f) continue;
                var expanded = body; expanded.Expand(shot.Radius * 2);
                if (expanded.IntersectRay(new Ray(shot.transform.position, velocity / speed), out float along)
                    && along <= speed * IncomingHorizon) return true;
            }
            return false;
        }

        // Called only while the Boss is free to act. Returns true when a rule took the decision.
        public bool TryExecute(FinalBossAxe axe)
        {
            if (Sealed) return false;
            foreach (var row in rows)
            {
                if (!row.Matched) continue;
                bool started;
                if (row.Action == BossAction.Jump)
                {
                    // Jump (Once): one jump per entry into the condition.
                    started = row.Armed && actor.Motor.TryJump();
                    if (started) row.Armed = false;
                }
                else started = axe.TryStart(MoveFor(row.Action));
                if (!started) continue;
                row.Executed++;
                if (row.Condition.Condition == 3) row.ConsumedOccurrence = landedOccurrence;
                if (row.Condition.Condition == 6) row.ConsumedOccurrence = damagedOccurrence;
                actor.Trace($"boss rule {rows.IndexOf(row) + 1} -> {row.ActionLabel}");
                return true;
            }
            return false;
        }

        static FinalBossAxe.Move MoveFor(BossAction action) => action switch
        {
            BossAction.Chop => FinalBossAxe.Move.Chop,
            BossAction.Spin => FinalBossAxe.Move.Spin,
            BossAction.Throw => FinalBossAxe.Move.Throw,
            _ => FinalBossAxe.Move.Boomerang
        };
    }
}
