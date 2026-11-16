using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Two additions to phase two (Thomas, 2026-11-15), both first played as a browser prototype.
    // Rising Uppercut: the Uppercut as a real jump at an opponent standing above him.
    // Counter Guard: a one-second guard that answers a blocked shot with a warned dash punch.
    public sealed partial class FinalBossBoxer
    {
        // ---- Rising Uppercut ----
        // The jump is chosen and fixed when the wind-up starts, and its path is the warning.
        // He lands on the level he jumped at and recovers there.
        const float RisingReach = 4.5f, RisingMinimum = 1.2f, RisingRecovery = .7f;
        internal bool Rising { get; private set; }
        Vector2 risingTarget;
        readonly List<Vector2> risingArc = new List<Vector2>(64);
        List<Vector2> traceInto;
        internal IReadOnlyList<Vector2> RisingArc => risingArc;

        bool RisingOpening(Vector2 target) => actor.Motor.Grounded && target.y-actor.Position.y > RisingMinimum
            && Mathf.Abs(target.x-actor.Position.x) <= RisingReach;

        bool PlanRising(Vector2 target)
        {
            risingArc.Clear();
            if (!RisingOpening(target) || !FindJump(target,0,out jump) || jump.Landing.y-actor.Position.y < .6f) return false;
            risingTarget=target;
            traceInto=risingArc;
            TraceJump(jump.AimX,target,jump.Motion,out _);
            traceInto=null;
            return risingArc.Count > 2;
        }

        // ---- Counter Guard ----
        public enum CoverStage { None, Raise, Hold, Warn, Open }
        internal const float CoverRaise = .20f, CoverHold = 1.0f, CoverWarn = .20f, CoverOpen = .60f, CoverCooldown = 7f;
        const float CoverMinimumGap = 2.5f, CounterReach = 8f, CounterSpeed = 18f;
        internal CoverStage Cover { get; private set; }
        internal bool Covering => Cover==CoverStage.Raise || Cover==CoverStage.Hold || Cover==CoverStage.Warn;
        internal float CoverProgress => Mathf.InverseLerp(coverAt,coverUntil,Time.time);
        internal Vector2 CounterTarget { get; private set; }
        public int Covers { get; private set; }
        public int Counters { get; private set; }
        float coverAt, coverUntil, coverReadyAt;
        int blocks;
        bool counter;

        void SetCover(CoverStage stage,float seconds) { Cover=stage; coverAt=Time.time; coverUntil=coverAt+seconds; }

        // He covers up against a player who has just been shooting from range and is in front of him.
        bool TryCover(BossObservation facts,Vector2 target)
        {
            if (Time.time < coverReadyAt || Resting || !actor.Motor.Grounded || !facts.Visible || weapon==WeaponState.Armed
                || Time.time-facts.LastShotAt > 1.5f) return false;
            Vector2 delta=target-actor.Position;
            if (Mathf.Abs(delta.x) < CoverMinimumGap || Mathf.Abs(delta.y) > 3f) return false;
            actor.Motor.EndAttackMotion();
            actor.Motor.HoldHorizontal();
            LockedFacing=Mathf.Sign(delta.x);
            actor.Face(Vector2.right*LockedFacing);
            coverReadyAt=Time.time+CoverCooldown;
            Covers++;
            SetCover(CoverStage.Raise,CoverRaise);
            actor.Trace("boxer covers");
            return true;
        }

        void TickCover()
        {
            if (actor.Motor.Grounded) actor.Motor.HoldHorizontal();
            if (Time.time < coverUntil) return;
            switch (Cover)
            {
                case CoverStage.Raise: SetCover(CoverStage.Hold,CoverHold); break;
                // Nothing touched the guard: the hands come down and he is open.
                case CoverStage.Hold: SetCover(CoverStage.Open,CoverOpen); break;
                case CoverStage.Warn: Cover=CoverStage.None; LaunchCounter(); break;
                default: Cover=CoverStage.None; nextThink=Time.time; break;
            }
        }

        // Called for every bullet that reaches his body. The fists stop what comes from the front
        // third of a circle; the first one stopped fixes where the counter will go.
        internal bool BlockShot(Vector2 velocity,Vector2 point)
        {
            if (Cover!=CoverStage.Hold && Cover!=CoverStage.Warn) return false;
            if (velocity.sqrMagnitude < .01f || Vector2.Dot(-velocity.normalized,Vector2.right*LockedFacing) < .5f) return false;
            actor.Sounds?.PlayPanned(Sfx.BulletHit,actor.Axe.PanAt(point.x),actor.ActorId,400000+blocks++);
            if (Cover==CoverStage.Hold)
            {
                var facts=actor.Perception.Facts;
                CounterTarget=facts.Visible ? (Vector2)facts.Body.center : (Vector2)playerBody.bounds.center;
                actor.View?.Hit();
                SetCover(CoverStage.Warn,CoverWarn);
                actor.Trace("boxer counter warned");
            }
            return true;
        }

        // The counter is an Overhand carried by a straight dash instead of a jump: the same fist,
        // damage, trail and recovery, along the line the warning showed.
        void LaunchCounter()
        {
            Vector2 delta=CounterTarget-actor.Position;
            float distance=Mathf.Min(CounterReach,delta.magnitude+.4f);
            if (Time.time-lastPunchEnd > ChainGap) chain=0;
            Current=Punch.Leap; Cross=false; signatureLeap=false; Rising=false; counter=true;
            if (Mathf.Abs(delta.x) > .12f) LockedFacing=Mathf.Sign(delta.x);
            actor.Face(Vector2.right*LockedFacing);
            accepted=game.GetBossMoveTuning(true,(int)Punch.Leap);
            Started[(int)Punch.Leap]++; Counters++;
            actor.Motor.EndAttackMotion();
            actor.Motor.StartDash(delta,distance,CounterSpeed);
            airStart=Time.time;
            StartStroke();
            SetPhase(Stage.Active,distance/CounterSpeed+.05f);
            actor.Perception.ExpressedPressure(actor.Position);
        }

        // How soon the flying axe passes the point, or a negative number when it will not come near.
        float AxePassesIn(Vector2 point)
        {
            if (weapon!=WeaponState.Flying) return -1;
            Vector2 line=Destination-AxeEdge;
            float length=line.magnitude;
            if (length < .2f) return -1;
            Vector2 direction=line/length;
            float along=Vector2.Dot(point-AxeEdge,direction);
            if (along < 0 || along > length) return -1;
            return (point-(AxeEdge+direction*along)).magnitude > 1.5f ? -1 : along/FlightSpeed;
        }
    }
}
