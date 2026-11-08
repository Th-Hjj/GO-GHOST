using UnityEngine;

namespace GoGhost.Duel
{
    // The axe wielder feeds his public observations into the same rule controller as the player.
    // The motor accepts ordinary movement/jump requests; axe attacks have no rule action IDs.
    public sealed partial class ConditionalController
    {
        readonly BossAgent bossOwner;

        static ConditionalLoadout AxeLoadout()
        {
            var rules = new ConditionalLoadout();
            rules.TryAddRule(ControlOperation.Jump, new ConditionalRule {
                Condition = ConditionalCatalog.IncomingAttackCondition, IncomingAttack = IncomingAttackOption.Ranged,
                Action = "J01" });
            rules.TryAddRule(ControlOperation.Movement, new ConditionalRule {
                Condition = ConditionalCatalog.EnemyDistanceCondition, Distance = EnemyDistanceOption.Far,
                Action = "M11", MoveDirection = MoveDirectionOption.TowardEnemy });
            return rules;
        }

        internal ConditionalController(BossAgent owner) : this(AxeLoadout(), 1)
        {
            bossOwner = owner;
            shape = owner.GetComponent<Collider2D>();
            body = owner.GetComponent<Rigidbody2D>();
        }

        public System.Collections.Generic.IReadOnlyList<(int Op, int Row)> OrderedRows => priorityRows;
        public bool RowMatches(int op, int row) => visualMatches[op, row];

        bool BossIncomingAttack(IncomingAttackOption option)
        {
            if (option != IncomingAttackOption.Ranged && option != IncomingAttackOption.Any) return false;
            foreach (var shot in Projectile.Active)
            {
                if (shot == null || shot.OwnerLayer == bossOwner.gameObject.layer || shot.Owner == bossOwner.gameObject) continue;
                var motion = shot.GetComponent<Rigidbody2D>();
                if (motion == null) continue;
                Vector2 velocity = motion.linearVelocity - body.linearVelocity;
                float speed = velocity.magnitude;
                if (speed < .01f) continue;
                var bounds = shape.bounds;
                bounds.Expand(shot.Radius * 2);
                if (bounds.IntersectRay(new Ray(shot.transform.position, velocity / speed), out float along)
                    && along <= speed * ConditionalDefaults.ThreatWindow) return true;
            }
            return false;
        }

        internal void ObserveBossRules()
        {
            var observed = bossOwner.Perception.Facts;
            visible[0] = observed.Visible && observed.Confidence > .1f;
            allVisible = visible[0] ? 1 : 0;
            contacts[0].Bounds = observed.Body;
            if (visible[0]) UpdateDistance(0, Mathf.Abs(observed.Position.x - shape.bounds.center.x));
            else close[0] = far[0] = false;
            facts.Clear();
            output = default;
            ObserveOperationRules((int)ControlOperation.Jump);
            ObserveOperationRules((int)ControlOperation.Movement);
        }

        internal ConditionalControlFrame ResolveBossLocomotion()
        {
            if (TryResolvePriorityJump() && output.Jump)
            {
                bool jumped = bossOwner.Motor.TryJump();
                ActionResolved(ControlOperation.Jump, jumped);
                output.Jump = output.JumpOwned = jumped;
            }
            foreach (var entry in priorityRows)
            {
                if (entry.Op != (int)ControlOperation.Movement) continue;
                if (!TryResolveRule(entry.Op, entry.Row, loadout.Operations[entry.Op].Rules[entry.Row])) continue;
                bossOwner.Motor.SetMove(output.Move);
                ActionResolved(ControlOperation.Movement, true);
                break;
            }
            if (!output.MovementOwned) running[(int)ControlOperation.Movement].Row = -1;
            return output;
        }
    }
}
