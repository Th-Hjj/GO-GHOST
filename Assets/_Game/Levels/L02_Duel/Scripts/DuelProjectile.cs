using UnityEngine;

namespace GoGhost.Duel
{
    public sealed class DuelProjectile
    {
        public readonly DuelFighter Owner;
        public readonly Strike Attack;
        public readonly int Direction;
        public readonly float Speed, Height;
        public Vector2 Position { get; private set; }
        public bool Alive { get; private set; } = true;
        readonly Transform visual;
        readonly float width;
        float travelled;
        public DuelProjectile(DuelFighter owner, Strike attack, Transform parent)
        {
            Owner = owner; Attack = attack; Direction = owner.Facing;
            Height = attack == Strike.Wave ? 1.2f : .16f;
            width = attack == Strike.Wave ? .55f : .28f;
            Speed = attack == Strike.Wave ? 60 : 40;
            Position = new Vector2(owner.Position.x + Direction * .72f, attack == Strike.Wave ? .6f : owner.Position.y);
            visual = DuelVisual.Box(attack.ToString(), parent, Position, new Vector2(width, Height), attack == Strike.Wave ? new Color(.65f,.65f,.65f) : Color.white).transform;
        }
        public void Step(DuelGame game, float dt)
        {
            if (!Alive) return;
            float distance = Mathf.Min(Speed * dt, DuelRules.Range - travelled);
            var hits = Physics2D.BoxCastAll(Position, new Vector2(width, Height), 0, Vector2.right * Direction, distance);
            foreach (var hit in hits)
            {
                if (hit.collider == Owner.Collider || hit.collider.isTrigger) continue;
                // Floor touches a low wave; it travels on that floor, not into it.
                if (hit.collider.name == "Floor") continue;
                var target = hit.collider.GetComponent<DuelFighter>();
                if (target != null) game.QueueHit(target, Owner, Attack);
                Position += Vector2.right * Direction * hit.distance;
                Dispose(); return;
            }
            Position += Vector2.right * Direction * distance; travelled += distance;
            visual.position = Position;
            if (travelled >= DuelRules.Range - .001f) Dispose();
        }
        public void Dispose() { Alive = false; if (visual != null) Object.Destroy(visual.gameObject); }
    }
}
