using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Both sides execute the same requests through this body. No policy writes a transform.
    public sealed class DuelFighter : MonoBehaviour
    {
        public Rigidbody2D Body { get; private set; }
        public BoxCollider2D Collider { get; private set; }
        public Vector2 Position => Body.position;
        public Vector2 Velocity { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Charges { get; private set; }
        public int Facing { get; private set; }
        public int ActionId { get; private set; }
        public int DodgeDirection { get; private set; }
        public bool Grounded { get; private set; }
        public bool DirectionLocked { get; private set; }
        public BodyAction Action { get; private set; }
        public Strike Attack { get; private set; }
        public float ActionTime { get; private set; }
        public float StunRemaining { get; private set; }
        public float JumpAt { get; private set; } = -100;
        public float DodgeAt { get; private set; } = -100;
        public float HitFlash { get; private set; }
        public float RejectFlash { get; private set; }
        public float CancelFlash { get; private set; }
        public bool Immune => Action == BodyAction.Dodge && ActionTime >= .16f && ActionTime < .30f;
        public bool Free => Action == BodyAction.Idle && StunRemaining <= 0 && Health > 0;
        public DuelFighter Opponent { get; set; }
        DuelGame game;
        float recharge, cancelDuration;
        bool released, canCross;
        readonly RaycastHit2D[] casts = new RaycastHit2D[16];
        readonly HashSet<DuelFighter> struck = new HashSet<DuelFighter>();

        public void Initialize(DuelGame owner, Vector2 spawn, int hp, int facing)
        {
            game = owner;
            Body = gameObject.AddComponent<Rigidbody2D>();
            Body.bodyType = RigidbodyType2D.Kinematic;
            Body.useFullKinematicContacts = true;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.freezeRotation = true;
            Collider = gameObject.AddComponent<BoxCollider2D>();
            Collider.size = new Vector2(DuelRules.Width, DuelRules.H);
            Body.position = spawn; Health = MaxHealth = hp; Facing = facing; Charges = 2;
            Grounded = true; Action = BodyAction.Idle;
        }

        public void Step(ControlIntent input, float dt, float clock)
        {
            if (Health <= 0) return;
            HitFlash = Mathf.Max(0, HitFlash - dt); RejectFlash = Mathf.Max(0, RejectFlash - dt);
            CancelFlash = Mathf.Max(0, CancelFlash - dt);
            StunRemaining = Mathf.Max(0, StunRemaining - dt);
            if (Charges < 2)
            {
                recharge -= dt;
                if (recharge <= .0001f) { Charges++; recharge += DuelRules.DodgeRefillInterval; }
            }
            if (!DirectionLocked && input.Aim != 0) Facing = input.Aim > 0 ? 1 : -1;
            bool started = false;
            if (Action == BodyAction.Attack && input.Attack == Attack && !released &&
                ActionTime <= DuelRules.CancelLimit(Attack) + .0001f && StunRemaining <= 0)
            {
                Action = BodyAction.CancelRecovery; ActionTime = 0;
                cancelDuration = DuelRules.CancelRecovery(Attack); CancelFlash = .16f;
                game.Trace(name + " cancel " + Attack); game.Cue(DuelCue.Cancel); started = true;
            }
            else if (Free)
            {
                // A legal dodge takes priority over a truly simultaneous attack for everyone.
                if (input.Dodge && Grounded && Charges > 0)
                {
                    Action = BodyAction.Dodge; ActionTime = 0; Attack = Strike.None;
                    DodgeDirection = input.DodgeDirection != 0 ? input.DodgeDirection : Position.x < Opponent.Position.x ? -1 : 1;
                    Charges--; recharge = DuelRules.DodgeRefillDelay + DuelRules.DodgeRefillInterval;
                    DodgeAt = clock; DirectionLocked = false;
                    game.Trace($"{name} dodge x={Position.x:0.00} direction={DodgeDirection}");
                    float endpoint = Position.x + DodgeDirection * DuelRules.DodgeDistance;
                    // Passing is only allowed if this actual dash can clear the whole opposing body.
                    canCross = (Opponent.Position.x - Position.x) * DodgeDirection > 0 &&
                        (endpoint - Opponent.Position.x) * DodgeDirection > DuelRules.Width + .03f &&
                        Mathf.Abs(endpoint) < DuelRules.HalfArena - DuelRules.Width * .5f;
                    game.Cue(DuelCue.Dodge); started = true;
                }
                else
                {
                    if (input.Dodge && Charges == 0) RejectFlash = .12f;
                    if (input.Jump && Grounded)
                    {
                        Velocity = new Vector2(Velocity.x, DuelRules.JumpSpeed);
                        Grounded = false; JumpAt = clock;
                    }
                    if (input.Attack != Strike.None && (input.Attack != Strike.Wave || Grounded))
                    {
                        Attack = input.Attack; Action = BodyAction.Attack; ActionTime = 0;
                        DirectionLocked = false; released = false; struck.Clear(); ActionId++; started = true;
                        game.Cue(Attack == Strike.Shot ? DuelCue.ShotWindup : Attack == Strike.Intercept ? DuelCue.Intercept : DuelCue.WaveWindup);
                    }
                }
            }
            float oldTime = ActionTime;
            // New actions begin at t=0, including visual windup. No skipped first simulation interval.
            if (!started) ActionTime += dt;
            if (Action == BodyAction.Attack)
            {
                if (ActionTime >= DuelRules.Lock(Attack) - .0001f) DirectionLocked = true;
                if (!released && ActionTime >= DuelRules.Windup(Attack) - .0001f)
                {
                    released = true;
                    if (Attack != Strike.Intercept) game.Launch(this, Attack);
                }
                if (released && Attack == Strike.Intercept && ActionTime < .34f - .0001f)
                    game.Slash(this, struck);
                if (ActionTime >= DuelRules.Windup(Attack) + DuelRules.Active(Attack) + DuelRules.Recovery(Attack) - .0001f) Idle();
            }
            else if (Action == BodyAction.CancelRecovery && ActionTime >= cancelDuration - .0001f) Idle();
            else if (Action == BodyAction.Dodge && ActionTime >= DuelRules.DodgeDuration - .0001f) Idle();

            float cap = Grounded ? DuelRules.GroundSpeed : DuelRules.AirSpeed;
            if (Action == BodyAction.Attack || Action == BodyAction.CancelRecovery) cap *= DuelRules.MoveScale(Attack);
            float vx = Grounded ? input.Move * cap : Mathf.MoveTowards(Velocity.x, input.Move * cap, DuelRules.AirAcceleration * dt);
            if (!Grounded) vx = Mathf.Clamp(vx, -cap, cap);
            if (StunRemaining > 0) vx = 0;
            float dx = vx * dt;
            if (Action == BodyAction.Dodge)
            {
                dx = DodgeDirection * (DuelRules.DodgeTravel(ActionTime) - DuelRules.DodgeTravel(oldTime));
                if (ActionTime >= .32f) dx += vx * dt * Mathf.InverseLerp(.32f, .62f, ActionTime);
                vx = dx / dt;
            }
            float vy = Grounded ? 0 : Velocity.y - DuelRules.Gravity * dt;
            MoveAxis(Vector2.right * dx, canCross && Action == BodyAction.Dodge && ActionTime >= .08f && ActionTime <= .32f);
            float verticalTravel = MoveAxis(Vector2.up * (vy * dt), false);
            if (vy < 0 && Mathf.Abs(verticalTravel - vy * dt) > .0001f)
            {
                vy = 0;
                Grounded = Position.y <= DuelRules.H * .5f + .025f;
            }
            else if (Position.y > DuelRules.H * .5f + .025f) Grounded = false;
            Velocity = new Vector2(vx, vy);
        }

        float MoveAxis(Vector2 displacement, bool passBody)
        {
            float length = displacement.magnitude;
            if (length < .000001f) return 0;
            var filter = new ContactFilter2D { useTriggers = false };
            int n = Collider.Cast(displacement / length, filter, casts, length + .002f);
            float allowed = length;
            for (int i = 0; i < n; i++)
            {
                if (casts[i].collider == Collider) continue;
                if (passBody && casts[i].collider == Opponent.Collider) continue;
                // A moving opponent may meet a dash endpoint. Permit physical outward escape,
                // never a teleport or a shortened recovery, instead of zero-distance cast deadlock.
                if(casts[i].collider==Opponent.Collider && casts[i].distance<=.001f &&
                    Vector2.Dot(Position-Opponent.Position,displacement)>0)continue;
                if (Vector2.Dot(casts[i].normal, displacement) >= 0) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0, casts[i].distance - .002f));
            }
            Body.position += displacement / length * allowed;
            Physics2D.SyncTransforms();
            return allowed * (displacement.y < 0 ? -1 : 1);
        }

        void Idle() { Action = BodyAction.Idle; Attack = Strike.None; ActionTime = 0; DirectionLocked = false; }
        public void Receive(Strike attack, DuelFighter source)
        {
            if (Health <= 0 || Immune) return;
            Health = Mathf.Max(0, Health - DuelRules.Damage(attack)); HitFlash = .12f;
            float stun = DuelRules.Stun(attack);
            StunRemaining = Mathf.Max(StunRemaining, stun);
            if (stun > 0 && Action == BodyAction.Attack && !released) Idle();
            if (Health == 0) { Action = BodyAction.Dead; Velocity = Vector2.zero; }
            game.Trace($"hit {source.name}->{name} {attack} hp={Health} action={Action} t={ActionTime:0.00} x={Position.x:0.00}");
            game.Cue(DuelCue.Hit);
        }
    }
}
