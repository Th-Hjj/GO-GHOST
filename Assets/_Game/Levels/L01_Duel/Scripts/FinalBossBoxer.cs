using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // The body and the axe have independent commitments. Only a finished punch or a
    // new hand order gets another sighting; a travelling fist or axe can genuinely miss.
    public sealed partial class FinalBossBoxer
    {
        public enum Punch { None, Jab, Uppercut, SlipHook, Haymaker, Meteor, Backfist = SlipHook, Leap = Haymaker }
        public enum Stage { Idle, Windup, Slip, Active, Recovery }
        static readonly BossMoveTuning[] tuning =
        {
            // Wind-ups are long enough to read and answer (Thomas, 2026-11-15); they were .18 to .29.
            default, Tune(.30f,.23f,.60f,13), Tune(.42f,.40f,1.85f,24),
            Tune(.40f,.38f,1.75f,23), Tune(.38f,.54f,2.05f,29), Tune(.45f,.74f,3.55f,36)
        };
        // After three strokes in a row he rests: no punch may start for a while. A stroke belongs to the
        // run when it starts soon after the last one ended. Walking and axe orders are not held.
        const int ChainLimit = 3;
        const float ChainRest = .8f, ChainGap = .9f;
        int chain;
        float lastPunchEnd = -100, restUntil;
        internal bool Resting => Time.time < restUntil;
        static BossMoveTuning Tune(float windup,float recovery,float cooldown,int baseDamage)
            => new BossMoveTuning(windup,recovery,cooldown,BossProfile.AttackDamage(baseDamage));
        internal static BossMoveTuning DefaultTuning(Punch punch) => tuning[(int)punch];

        readonly DuelGame game;
        readonly BossAgent actor;
        readonly Collider2D playerBody;
        readonly float[] readyAt = new float[tuning.Length];
        public readonly int[] Started = new int[tuning.Length], Hits = new int[tuning.Length];
        internal Punch PreviewPunch { get; set; }
        public Punch Current { get; private set; }
        public Stage Phase { get; private set; }
        public float LockedFacing { get; private set; } = 1;
        public float PhaseProgress => Mathf.InverseLerp(phaseAt, phaseUntil, Time.time);
        public System.Action<Punch,Vector2> Thrown;
        internal bool Cross { get; private set; }
        internal float ImpactAt { get; private set; } = float.NegativeInfinity;
        Collider2D impactSupport;
        float impactX;
        bool impactSpent;
        internal Vector2 ImpactPoint => new Vector2(impactX,impactSupport!=null ? impactSupport.bounds.max.y : actor.Position.y);
        internal float ImpactRadius => 2.5f*Mathf.SmoothStep(0,1,(Time.time-ImpactAt)/.18f);
        internal Vector2 StrokeStart { get; private set; }
        internal int Stroke { get; private set; }
        internal const float FistRadius = .23f;
        BossMoveTuning accepted;
        JumpPlan jump;
        float phaseAt, phaseUntil, nextThink, punchTime, lastSample, airStart;
        Vector2 lastFist, lastPlayer, diveTarget, recoveryHand;
        bool hit, airborne, signatureLeap;
        int observedSide;
        float crossedAt = -100;
        Punch lastPunch;
        public FinalBossBoxer(BossAgent owner, PlayerHealth player)
        {
            actor=owner;
            game=owner.GetComponentInParent<DuelGame>();
            playerBody=player != null ? player.GetComponent<Collider2D>() : null;
            ReadPlatforms();
        }

        public bool Ready(Punch punch) => punch > Punch.None && (int)punch < readyAt.Length && Time.time >= readyAt[(int)punch];
        public bool CanStart(Punch punch) => Ready(punch) && !Resting && Current == Punch.None && actor.Running
            && actor.IsAlive && (actor.Motor.Grounded || punch != Punch.Leap) && !Gesturing;

        public void Tick()
        {
            if (Retired || playerBody == null || actor.Player.IsDead) return;
            TickWeapon();
            SyncPerch();
            if (!actor.Running || !actor.IsAlive) return;
            TickImpact();
            if (!actor.Running || actor.Player.IsDead) return;
            var facts=actor.Perception.Facts;
            if (facts.Visible)
            {
                int side=(int)Mathf.Sign(facts.Position.x-actor.Position.x);
                if (observedSide != 0 && side != observedSide) crossedAt=Time.time;
                observedSide=side;
            }
            // Only the opening throw still occupies the weapon hand. Subsequent orders
            // release the body as soon as the summoning gesture finishes.
            if (!detached && weapon==WeaponState.Armed)
            {
                if (facts.Visible && actor.Motor.Grounded)
                    actor.Motor.PursueOnGround(Mathf.Sign(Destination.x-actor.Position.x),3.5f);
                return;
            }
            if (Cover != CoverStage.None) { TickCover(); return; }
            if (Current != Punch.None) { TickPunch(facts); return; }
            if (combination!=null && !Gesturing && ContinueCombination(facts)) return;
            if (Time.time < nextThink || Gesturing) return;
            nextThink=Time.time + actor.Profile.ReactionSeconds;
            if (facts.Confidence <= .1f) { SeekLastSighting(); return; }
            if (PreviewPunch != Punch.None && TryStart(PreviewPunch)) { PreviewPunch=Punch.None; return; }
            Decide(facts);
        }

        public bool TryStart(Punch punch)
        {
            if (!CanStart(punch)) return false;
            var facts=actor.Perception.Facts;
            // Aim where the opponent will be when the fist arrives: this punch's own wind-up and the start of its stroke.
            Vector2 target=OpponentAt(facts,game.GetBossMoveTuning(true,(int)punch).Windup+.08f);
            if (punch == Punch.Leap && !FindJump(target,0,out jump)) return false;
            if (punch == Punch.Meteor)
            {
                if (actor.Motor.Grounded && !FindJump(target,.8f,out jump)) return false;
                if (!actor.Motor.Grounded && !FindDive(target,out diveTarget)) return false;
            }
            // An opponent standing above him turns the Uppercut into a jump at them.
            bool rise=punch==Punch.Uppercut && (requireRising || RisingReady) && PlanRising(target);
            if (requireRising && !rise) return false;
            Rising=rise;
            if (Rising) risingReadyAt=Time.time+RisingCooldown;
            counter=false;
            if (Time.time-lastPunchEnd > ChainGap) chain=0;
            Current=punch; Cross=false; signatureLeap=punch==Punch.Leap && ShoulderWindow;
            LockedFacing=Mathf.Abs(target.x-actor.Position.x) > .12f ? Mathf.Sign(target.x-actor.Position.x) : actor.Facing;
            actor.Face(Vector2.right*LockedFacing);
            accepted=game.GetBossMoveTuning(true,(int)punch);
            Started[(int)punch]++;
            if (actor.Motor.Grounded || punch==Punch.Meteor) actor.Motor.EndAttackMotion();
            // Every punch is wound up standing still (Thomas, 2026-11-15): he plants, coils into the
            // punch's own pose, holds it, and only then moves. The lunge belongs to the release.
            // An airborne straight or hook keeps the steering of its existing jump.
            actor.Perception.ExpressedPressure(actor.Position);
            SetPhase(Stage.Windup,accepted.Windup);
            actor.Trace($"boxer {punch} starts");
            return true;
        }

        void SetPhase(Stage stage,float duration)
        {
            Phase=stage; phaseAt=Time.time; phaseUntil=phaseAt+duration;
        }

        void TickPunch(BossObservation facts)
        {
            if (Phase == Stage.Windup && Time.time >= phaseUntil)
            {
                if (Current == Punch.Leap)
                {
                    if (!LaunchPunchJump(facts)) { Recover(); return; }
                    airborne=false; airStart=Time.time;
                    SetPhase(Stage.Slip,Mathf.Max(.04f,jump.StrikeAt));
                    return;
                }
                if (Current == Punch.Meteor)
                {
                    if (actor.Motor.Grounded)
                    {
                        if (!LaunchPunchJump(facts)) { Recover(); return; }
                        airborne=false; airStart=Time.time;
                        SetPhase(Stage.Slip,jump.Motion.JumpSpeed / (Mathf.Abs(Physics2D.gravity.y)*jump.Motion.GravityScale));
                        return;
                    }
                    if (!LockDive(facts)) Recover();
                    return;
                }
                if (Rising)
                {
                    // The platforms have moved during the wind-up; the place he jumps at has not.
                    if (FindJump(risingTarget,0,out jump) && TakeOff(jump))
                    {
                        // The fist leads from the ground up: the stroke starts with the take-off and
                        // lasts to the top of the jump, so the whole rise is the punch.
                        airborne=false; airStart=Time.time;
                        StartStroke();
                        SetPhase(Stage.Active,Mathf.Clamp(jump.Motion.JumpSpeed/(Mathf.Abs(Physics2D.gravity.y)*jump.Motion.GravityScale),.25f,.7f));
                        return;
                    }
                    Rising=false;
                }
                ReleaseLunge(facts);
                StartStroke();
            }
            if (Phase == Stage.Slip)
            {
                airborne |= !actor.Motor.Grounded;
                if (diveLocked)
                {
                    if (Time.time < phaseUntil) return;
                    BeginDive();
                }
                else
                {
                    if (Time.time < phaseUntil && !(airborne && actor.Motor.Grounded)) return;
                    if (Current==Punch.Meteor) { if (!LockDive(facts)) Recover(); return; }
                }
                StartStroke();
            }
            if (Phase == Stage.Active)
            {
                SampleFist();
                if (Current == Punch.None) return;
                if (Current == Punch.Meteor)
                {
                    if (actor.Motor.DashActive && !actor.Motor.DashBlocked && Time.time < phaseUntil) return;
                    // A body may block the dive in midair; only terrain releases the impact.
                    if (actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var support))
                    {
                        ImpactAt=Time.time;
                        impactX=actor.Position.x; impactSupport=support; impactSpent=hit;
                        TickImpact();
                        actor.Sounds?.PlayPanned(Sfx.MeteorGround,actor.Axe.PanAt(impactX),actor.ActorId,200000+Stroke);
                    }
                    else if (Time.time<phaseUntil) return;
                    if (Current != Punch.None) Recover();
                    return;
                }
                // The counter's dash strikes with the whole body, not the fist alone.
                if (counter && !hit)
                {
                    var reach=actor.Body.bounds; reach.Expand(.2f);
                    if (reach.Intersects(playerBody.bounds)) DealPunch();
                }
                if (Time.time < phaseUntil) return;
                if (counter && chain < ChainLimit && facts.Visible && actor.Motor.Grounded)
                {
                    // He arrives throwing: the dash runs straight into a Cross.
                    var after=OpponentAt(facts,.15f)-actor.Position;
                    if (Mathf.Abs(after.x) < 3.2f && Mathf.Abs(after.y) < 1.3f)
                    {
                        counter=false;
                        Current=Punch.Jab; Cross=true;
                        accepted=game.GetBossMoveTuning(true,(int)Punch.Jab);
                        if (Mathf.Abs(after.x) > .12f) { LockedFacing=Mathf.Sign(after.x); actor.Face(Vector2.right*LockedFacing); }
                        actor.Motor.EndAttackMotion();
                        DriveTo(actor.Position+after,.16f,16f);
                        SetPhase(Stage.Windup,.16f);
                        return;
                    }
                }
                if (Current == Punch.Jab && !Cross && chain < ChainLimit && facts.Visible)
                {
                    var next=OpponentAt(facts,.15f)-actor.Position;
                    // A second advancing straight needs a useful lane, not a predicted hit.
                    float laneValue=Vector2.Dot(next.normalized,Vector2.right*LockedFacing);
                    if (laneValue>.34f || (lastPunch!=Punch.Jab && next.sqrMagnitude<.36f))
                    {
                        Cross=true;
                        DriveTo(actor.Position+next,.16f,16f);
                        SetPhase(Stage.Windup,.16f);
                        return;
                    }
                }
                Recover();
            }
            if (Phase != Stage.Recovery) return;
            if (Rising && !actor.Motor.Grounded && Time.time-airStart<1.8f) { phaseUntil=Time.time+RisingRecovery; return; }
            if (Current==Punch.Leap && !actor.Motor.Grounded && Time.time-airStart<1.8f) return;
            if (Time.time < phaseUntil) return;
            readyAt[(int)Current]=Time.time+accepted.Cooldown;
            lastPunchEnd=Time.time;
            Rising=counter=false;
            if (chain >= ChainLimit) { restUntil=Time.time+ChainRest; chain=0; actor.Trace("boxer rests"); }
            lastPunch=Current;
            Current=Punch.None;
            Phase=Stage.Idle;
            nextThink=Time.time;
        }

        bool LaunchPunchJump(BossObservation facts)
        {
            // The takeoff and the deck may both have moved during the wind-up.
            Vector2 sight=facts.Visible ? OpponentAt(facts,.24f) : jump.Landing;
            return FindJump(sight,Current==Punch.Meteor ? .8f : 0,out jump) && TakeOff(jump);
        }

        // At the top of the rise the Meteor stops in the air and locks where it will land (Thomas, 2026-11-15):
        // the dive line and landing outline show for the whole hold, then the dive follows that line without
        // tracking. It was .2 to .3 s of warning before, only during the dive itself.
        internal const float DiveHold = .30f;
        bool diveLocked;
        int diveSurface;
        internal bool DiveLocked => diveLocked;
        bool LockDive(BossObservation facts)
        {
            var sight=facts.Visible ? OpponentAt(facts,DiveHold+.20f) : jump.Landing;
            if (!FindDive(sight,out diveTarget,out diveSurface,DiveHold)) return false;
            if (facts.Visible)
            {
                // Sight once more for the hold and the time the dive itself takes.
                float flight=Mathf.Min(.6f,Vector2.Distance(diveTarget,actor.Position)/24f);
                if (FindDive(OpponentAt(facts,DiveHold+flight),out var led,out int ledSurface,DiveHold)) { diveTarget=led; diveSurface=ledSurface; }
            }
            actor.Face(diveTarget-actor.Position); LockedFacing=actor.Facing;
            actor.Motor.HoldInAir();
            diveLocked=true;
            SetPhase(Stage.Slip,DiveHold);
            return true;
        }
        void BeginDive()
        {
            diveLocked=false;
            OpenDiveLane(diveSurface);
            var delta=diveTarget-actor.Position;
            actor.Motor.EndAttackMotion();
            actor.Motor.StartDash(delta,delta.magnitude+.45f,24f);
        }

        // The dive lands on the surface it chose. Every other platform lets it through, so a step of the
        // tower between him and the target no longer catches him (Thomas, 2026-11-15). The floor always stops him.
        readonly List<Collider2D> divePassed = new List<Collider2D>();
        void OpenDiveLane(int landing)
        {
            CloseDiveLane();
            for (int i=1;i<surfaces.Length;i++)
            {
                var platform=surfaces[i].Collider;
                if (i==landing || platform==null) continue;
                divePassed.Add(platform);
                actor.Navigation.Passable.Add(platform);
                if (actor.Body!=null) Physics2D.IgnoreCollision(actor.Body,platform,true);
            }
        }
        void CloseDiveLane()
        {
            foreach (var platform in divePassed)
            {
                if (platform==null) continue;
                actor.Navigation.Passable.Remove(platform);
                if (actor.Body!=null) Physics2D.IgnoreCollision(actor.Body,platform,false);
            }
            divePassed.Clear();
        }

        static float ActiveSeconds(Punch punch) => punch switch { Punch.Uppercut=>.21f, Punch.Backfist=>.22f, Punch.Leap=>.27f, _=>.14f };
        // How far a grounded punch can carry him in its release, with the fist's own reach.
        internal static float LungeReach(Punch punch) => ActiveSeconds(punch)*(punch==Punch.Jab ? 22f : 16f)+.82f;
        // The burst forward as the held punch is let go, along the facing fixed at its start.
        void ReleaseLunge(BossObservation facts)
        {
            if (Cross || !actor.Motor.Grounded) return;
            if (Current!=Punch.Jab && Current!=Punch.Backfist && Current!=Punch.Uppercut) return;
            Vector2 to=facts.Visible ? OpponentAt(facts,.08f) : actor.Position+Vector2.right*(LockedFacing*2f);
            DriveTo(to,ActiveSeconds(Current),Current==Punch.Jab ? 22f : 16f);
        }

        void DriveTo(Vector2 target,float seconds,float speed)
        {
            if (!actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var support)) return;
            float x=Mathf.Clamp(target.x-LockedFacing*.82f,
                support.bounds.min.x+actor.Motor.Size.x*.5f,support.bounds.max.x-actor.Motor.Size.x*.5f);
            float step=Mathf.Min((x-actor.Position.x)*LockedFacing,seconds*speed);
            if (step > .12f) actor.Motor.StartDash(Vector2.right*LockedFacing,step,speed);
        }

        void StartStroke()
        {
            hit=false; Stroke++; chain++;
            float active=Current switch { Punch.Uppercut=>Rising ? .30f : .21f, Punch.Backfist=>.22f, Punch.Leap=>.27f,
                Punch.Meteor=>Mathf.Clamp(Vector2.Distance(diveTarget,actor.Position)/24f+.18f,.20f,.85f), _=>.14f };
            SetPhase(Stage.Active,active);
            punchTime=Time.time;
            lastSample=Time.time; lastFist=StrokeStart=Fist(); lastPlayer=playerBody.bounds.center;
            actor.Sounds?.PlayPanned(Current==Punch.Jab && !Cross ? Sfx.JabAir : Sfx.HeavyFist,
                actor.Axe.PanAt(actor.Position.x),actor.ActorId,200000+Stroke);
            Thrown?.Invoke(Current,lastFist);
        }

        void Recover()
        {
            recoveryHand=HandLocal;
            diveLocked=false;
            CloseDiveLane();
            if (actor.Motor.Grounded || Current==Punch.Meteor) actor.Motor.EndAttackMotion();
            float recovery=signatureLeap ? Mathf.Max(.76f,accepted.Recovery) : accepted.Recovery;
            // Inside a combination the next punch follows on a shortened recovery.
            if (CombinationContinues) recovery*=.5f;
            SetPhase(Stage.Recovery,recovery);
        }

        internal static readonly Vector2 Guard = new Vector2(.29f,.31f);
        Vector2 Chamber => Current switch
        {
            Punch.Uppercut=>Rising ? new Vector2(.06f,-.60f) : new Vector2(.10f,-.50f), Punch.Backfist=>new Vector2(-.44f,.25f),
            Punch.Leap=>new Vector2(-.21f,.44f), Punch.Meteor=>new Vector2(-.12f,.71f),
            _=>Cross ? new Vector2(-.25f,.20f) : new Vector2(.17f,.27f)
        };
        Vector2 StrokePoint(float t)
        {
            float extend=Mathf.SmoothStep(0,1,t);
            // Rising: the fist is over his head within the first third and stays there, leading the body up.
            if (Rising) return Vector2.Lerp(Chamber,new Vector2(.30f,1.05f),Mathf.Clamp01(t*3.2f));
            switch(Current)
            {
                case Punch.Uppercut:
                    return Vector2.Lerp(Chamber,new Vector2(.40f,1.02f),extend)+Vector2.right*(.37f*Mathf.Sin(t*Mathf.PI));
                case Punch.Backfist:
                    return Vector2.Lerp(Chamber,new Vector2(.89f,.22f),extend)+Vector2.up*(.32f*Mathf.Sin(t*Mathf.PI));
                case Punch.Meteor:
                    return Vector2.Lerp(Chamber,new Vector2(.60f,-.70f),Mathf.Clamp01(t*2));
                default:
                    return Vector2.Lerp(Chamber,new Vector2(Current==Punch.Leap || Cross ? 1.04f : .82f,
                        Current==Punch.Leap ? .45f : .30f),Mathf.Clamp01(t*1.55f));
            }
        }
        internal Vector2 HandLocal => Phase switch
        {
            Stage.Windup=>Vector2.Lerp(Guard,Chamber,CoilProgress), Stage.Slip=>Chamber,
            Stage.Active=>StrokePoint(PhaseProgress), Stage.Recovery=>Vector2.Lerp(recoveryHand,Guard,PhaseProgress),
            _=>Guard
        };
        // The coil is reached in the first 40% of a wind-up and then held, so there is a still pose to read.
        internal float CoilProgress => Phase==Stage.Windup ? Mathf.SmoothStep(0,1,Mathf.Clamp01(PhaseProgress/.4f)) : 1;
        public Vector2 Fist() { var p=HandLocal; return actor.Position+new Vector2(LockedFacing*p.x,p.y); }
        // Read by the view to warn of the punch being wound up and of the Meteor's landing.
        internal Vector2 StrokeWorld(float t) { var p=StrokePoint(t); return actor.Position+new Vector2(LockedFacing*p.x,p.y); }
        internal bool Diving => Current==Punch.Meteor && (diveLocked || Phase==Stage.Active) && Time.time>=ImpactAt+.5f;
        internal Vector2 DiveLanding => diveTarget-Vector2.up*(actor.Motor.Size.y*.5f);
        public Bounds PunchArea() => new Bounds(Fist(),Vector3.one*.48f);
        void SampleFist()
        {
            var target=playerBody.bounds;
            Vector2 point=Fist();
            actor.View?.RecordFistSweep(this, point);
            if (!hit && SweptContact(lastFist,point,lastPlayer,target,FistRadius,lastSample,Time.time)) DealPunch();
            lastSample=Time.time; lastFist=point; lastPlayer=target.center;
        }
        static bool SweptContact(Vector2 a,Vector2 b,Vector2 previousTarget,Bounds target,float radius,float from,float to)
        {
            var stroke=new BossThreat {Valid=true,Phase=BossThreatPhase.Active,Shape=BossThreatShape.SweptCapsule,
                Origin=a,End=b,Radius=radius,StartsAt=from,EndsAt=to};
            return stroke.IntersectsMotion(previousTarget,target.center,target.extents,from,to);
        }
        void TickImpact()
        {
            if (impactSpent || impactSupport==null || Time.time-ImpactAt>.28f) return;
            var wave=new Bounds(ImpactPoint+Vector2.up*.32f,new Vector3(ImpactRadius*2,.9f,1));
            if (!wave.Intersects(playerBody.bounds)) return;
            impactSpent=true;
            int before=actor.Player.CurrentHealth;
            actor.HitPlayer(accepted.Damage);
            if (actor.Player.CurrentHealth<before) Hits[(int)Punch.Meteor]++;
        }
        void DealPunch()
        {
            hit=true;
            var move=Current;
            int before=actor.Player.CurrentHealth;
            actor.HitPlayer(Cross ? BossProfile.ScaleDamage(accepted.Damage,1.3f) : accepted.Damage);
            if (actor.Player.CurrentHealth<before) Hits[(int)move]++;
        }
        public void Publish(List<BossThreat> output)
        {
            if (Phase==Stage.Active)
                output.Add(new BossThreat {ActorId=actor.ActorId,AttackId=200000+Stroke,Valid=true,Phase=BossThreatPhase.Active,
                    Shape=BossThreatShape.Box,Area=PunchArea(),Origin=Fist(),StartsAt=punchTime,EndsAt=phaseUntil});
        }
        public void Cancel()
        {
            CloseDiveLane();
            RemovePerch();
            Cover=CoverStage.None; Rising=counter=false; combination=null;
            diveLocked=false;
            if (InFlight) actor.Sounds?.StopAttack(actor.ActorId,300000+Orders);
            if (Current != Punch.None) actor.Motor?.EndAttackMotion();
            Current=Punch.None; Phase=Stage.Idle; PreviewPunch=Punch.None;
            weapon=WeaponState.Retired; detached=false; impactSupport=null;
            ImpactAt=float.NegativeInfinity;
        }
    }
}
