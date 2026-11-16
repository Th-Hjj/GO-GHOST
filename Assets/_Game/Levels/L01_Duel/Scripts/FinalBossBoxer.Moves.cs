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
        const float RisingReach = 6f, RisingMinimum = 1.2f, RisingRecovery = .7f, RisingCooldown = 3f;
        float risingReadyAt;
        bool requireRising;
        internal Vector2 RisingLanding => jump.Landing-Vector2.up*(actor.Motor.Size.y*.5f);
        internal float PhaseStartedAt => phaseAt;
        bool RisingReady => Time.time >= risingReadyAt;

        // The rising form has its own cooldown and does not wait for the ordinary Uppercut's.
        bool TryRising()
        {
            float held=readyAt[(int)Punch.Uppercut];
            readyAt[(int)Punch.Uppercut]=0; requireRising=true;
            bool started=TryStart(Punch.Uppercut);
            requireRising=false;
            if (!started) readyAt[(int)Punch.Uppercut]=held;
            return started;
        }
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
        internal const float CoverRaise = .20f, CoverHold = 1.0f, CoverWarn = .20f, CoverOpen = .60f, CoverCooldown = 4.5f;
        // The guard and its counter have no distance limits (Thomas, 2026-11-15): he covers up at any range
        // and the dash goes all the way to where the player will be.
        const float CounterSpeed = 22f, CoverWalk = 2.6f;
        internal CoverStage Cover { get; private set; }
        internal bool Covering => Cover==CoverStage.Raise || Cover==CoverStage.Hold || Cover==CoverStage.Warn;
        internal float CoverProgress => Mathf.InverseLerp(coverAt,coverUntil,Time.time);
        internal Vector2 CounterTarget { get; private set; }
        internal Vector2 CounterEnd
        {
            get
            {
                Vector2 delta=CounterDelta;
                return actor.Position+delta.normalized*(delta.magnitude+.4f);
            }
        }
        // From where he stands to the target. A target level with him or lower is reached along the
        // surface he stands on: aimed even slightly downward, the dash met that surface at once and he
        // punched on the spot.
        Vector2 CounterDelta
        {
            get
            {
                Vector2 delta=CounterTarget-actor.Position;
                if (actor.Motor.Grounded && delta.y < .35f) delta.y=0;
                if (delta.sqrMagnitude < .04f) delta=Vector2.right*(LockedFacing*.2f);
                return delta;
            }
        }
        // When the guard was last set off; the view gathers a ring on the fists from this moment.
        internal float CounterChargedAt { get; private set; } = float.NegativeInfinity;
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
            // No bullets come from a player who is reloading, so the guard is not spent on them.
            if (facts.ReloadVisible) return false;
            Vector2 delta=target-actor.Position;
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
            // Behind the guard he comes on slowly and never steps off what he stands on (Thomas, 2026-11-15).
            // Once a shot is stopped he stands still and charges for the length of the warning.
            if (actor.Motor.Grounded)
            {
                bool advance=false; float side=LockedFacing;
                if (Cover==CoverStage.Hold)
                {
                    var seen=actor.Perception.Facts;
                    if (seen.Visible && actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var footing))
                    {
                        float gap=seen.Position.x-actor.Position.x;
                        side=Mathf.Abs(gap) > .12f ? Mathf.Sign(gap) : LockedFacing;
                        float edge=side > 0 ? footing.bounds.max.x-.6f : footing.bounds.min.x+.6f;
                        advance=Mathf.Abs(gap) > 1.6f && (edge-actor.Position.x)*side > .15f;
                        LockedFacing=side; actor.Face(Vector2.right*side);
                    }
                }
                if (advance) actor.Motor.PursueOnGround(side,CoverWalk); else actor.Motor.HoldHorizontal();
            }
            if (Cover==CoverStage.Warn)
            {
                // The line follows the player through the warning and is fixed when the dash leaves,
                // aimed where they will be when it arrives.
                var facts=actor.Perception.Facts;
                if (facts.Visible)
                {
                    float travel=Vector2.Distance(CounterTarget,actor.Position)/CounterSpeed;
                    CounterTarget=OpponentAt(facts,Mathf.Max(0,coverUntil-Time.time)+travel);
                    if (Mathf.Abs(CounterTarget.x-actor.Position.x) > .12f)
                    { LockedFacing=Mathf.Sign(CounterTarget.x-actor.Position.x); actor.Face(Vector2.right*LockedFacing); }
                }
            }
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
                CounterChargedAt=Time.time;
                SetCover(CoverStage.Warn,CoverWarn);
                actor.Trace("boxer counter warned");
            }
            return true;
        }

        // The counter is an Overhand carried by a straight dash instead of a jump: the same fist,
        // damage, trail and recovery, along the line the warning showed.
        void LaunchCounter()
        {
            Vector2 delta=CounterDelta;
            float distance=delta.magnitude+.4f;
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

        // ---- Slip: a jump over a shot that is about to land ----
        const float SlipCooldown = 2.5f, SlipChance = .6f;
        float slipReadyAt;
        int slipJudged;
        public int Slips { get; private set; }
        bool TrySlip(BossObservation facts)
        {
            if (Time.time < slipReadyAt || !actor.Motor.Grounded || !facts.Visible) return false;
            Vector2 centre=actor.Position;
            var body=new Bounds(centre,actor.Motor.Size+Vector2.one*.3f);
            foreach (var shot in Projectile.Active)
            {
                if (shot==null || shot.OwnerLayer==actor.gameObject.layer) continue;
                Vector2 position=shot.transform.position, velocity=shot.GetComponent<Rigidbody2D>().linearVelocity;
                float speed=velocity.magnitude;
                if (speed < .01f || Mathf.Abs(velocity.y) > Mathf.Abs(velocity.x)) continue;
                if (!body.IntersectRay(new Ray(position,velocity/speed),out float distance)) continue;
                float arrival=distance/speed;
                if (arrival < .14f || arrival > .45f) continue;
                // Each shot is judged once, so he does not slip every one.
                int id=shot.GetEntityId().GetHashCode();
                if (id==slipJudged) continue;
                slipJudged=id;
                if (Random.value > SlipChance || !actor.Motor.TryJump()) continue;
                slipReadyAt=Time.time+SlipCooldown; Slips++;
                actor.Trace("boxer slips");
                return true;
            }
            return false;
        }

        // ---- Combinations: authored runs of punches, ended by the three-stroke rest ----
        static readonly Punch[][] Combinations =
        {
            new[] { Punch.Jab, Punch.Uppercut },
            new[] { Punch.Backfist, Punch.Jab },
            new[] { Punch.Jab, Punch.Backfist, Punch.Leap },
        };
        Punch[] combination;
        int combinationStep;
        void BeginCombination(Punch first,float gap)
        {
            combination=null;
            if (Rising || chain > 0 || gap > 3.5f) return;
            int from=Random.Range(0,Combinations.Length);
            for (int i=0;i<Combinations.Length;i++)
            {
                var candidate=Combinations[(from+i)%Combinations.Length];
                if (candidate[0]!=first) continue;
                combination=candidate; combinationStep=1;
                return;
            }
        }
        bool CombinationContinues => combination!=null && combinationStep < combination.Length && chain < ChainLimit;
        bool ContinueCombination(BossObservation facts)
        {
            if (!CombinationContinues || Resting || !facts.Visible) { combination=null; return false; }
            var next=combination[combinationStep++];
            Vector2 delta=OpponentAt(facts,.2f)-actor.Position;
            if (Mathf.Abs(delta.x) > 3.8f || (Mathf.Abs(delta.y) > 1.3f && next!=Punch.Leap)) { combination=null; return false; }
            float held=readyAt[(int)next];
            readyAt[(int)next]=0;
            if (TryStart(next)) return true;
            readyAt[(int)next]=held; combination=null;
            return false;
        }
    }
}
