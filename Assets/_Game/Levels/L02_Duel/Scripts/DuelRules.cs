using UnityEngine;

namespace GoGhost.Duel
{
    // All distances are world units. H is the physical standing collider height.
    public static class DuelRules
    {
        public const float H = 2f, HalfArena = 14f, Width = 1.1f;
        public const float GroundSpeed = 6f, AirSpeed = 4.5f, AirAcceleration = 12f;
        public const float Gravity = 20.8f, JumpSpeed = 10.4f;
        public const float DodgeDuration = .62f, DodgeFastStart = .08f, DodgeFastEnd = .32f;
        public const float DodgeDistance = 4.8f, DodgeImmuneStart = .16f, DodgeImmuneEnd = .30f;
        public const float DodgeRefillDelay = .8f, DodgeRefillInterval = 2f;
        public const float ShotSpeed = 40f, WaveSpeed = 60f, Range = 22f;
        public static float Windup(Strike a) => a == Strike.Shot ? .12f : a == Strike.Intercept ? .24f : .56f;
        public static float Active(Strike a) => a == Strike.Intercept ? .10f : 0f;
        public static float Recovery(Strike a) => a == Strike.Shot ? .30f : a == Strike.Intercept ? .42f : .64f;
        public static float Lock(Strike a) => a == Strike.Shot ? .06f : a == Strike.Intercept ? .14f : .30f;
        public static float CancelLimit(Strike a) => a == Strike.Intercept ? .18f : a == Strike.Wave ? .52f : -1f;
        public static float CancelRecovery(Strike a) => a == Strike.Intercept ? .16f : .10f;
        public static float MoveScale(Strike a) => a == Strike.Shot ? .75f : a == Strike.Intercept ? .5f : .35f;
        public static int Damage(Strike a) => a == Strike.Shot ? 10 : a == Strike.Intercept ? 22 : 34;
        public static float Stun(Strike a) => a == Strike.Shot ? 0f : a == Strike.Intercept ? .16f : .20f;
        public static float DodgeTravel(float t)
        {
            float start = Mathf.Min(t, .08f) * .6f;
            return start + Mathf.Clamp(t - .08f, 0f, .24f) * 19.8f;
        }
    }
    public enum Strike { None, Shot, Intercept, Wave }
    public enum BodyAction { Idle, Attack, CancelRecovery, Dodge, Dead }
    public struct ControlIntent
    {
        public float Move;
        public int Aim, DodgeDirection;
        public bool Jump, Dodge;
        public Strike Attack;
    }
    public readonly struct FighterView
    {
        public readonly Vector2 Position, Velocity;
        public readonly int Facing, Hp, Charges, ActionId, DodgeDirection;
        public readonly float Elapsed, Stun, JumpAt, DodgeAt;
        public readonly BodyAction Action;
        public readonly Strike Attack;
        public readonly bool Grounded, Locked;
        public FighterView(DuelFighter f)
        {
            Position = f.Position; Velocity = f.Velocity; Facing = f.Facing; Hp = f.Health;
            Charges = f.Charges; ActionId = f.ActionId; DodgeDirection = f.DodgeDirection;
            Elapsed = f.ActionTime; Stun = f.StunRemaining; JumpAt = f.JumpAt; DodgeAt = f.DodgeAt;
            Action = f.Action; Attack = f.Attack; Grounded = f.Grounded; Locked = f.DirectionLocked;
        }
        public float Feet => Position.y - DuelRules.H * .5f;
        public bool Free => Action == BodyAction.Idle && Stun <= 0f && Hp > 0;
        public bool Windup => Action == BodyAction.Attack && Elapsed < DuelRules.Windup(Attack);
        public float Remaining => Action == BodyAction.Dodge ? Mathf.Max(0, .62f - Elapsed) :
            Action == BodyAction.Attack ? Mathf.Max(0, DuelRules.Windup(Attack) + DuelRules.Active(Attack) + DuelRules.Recovery(Attack) - Elapsed) : 0f;
        public float BackSpace(int away) => DuelRules.HalfArena - DuelRules.Width * .5f - Position.x * away;
    }
}
