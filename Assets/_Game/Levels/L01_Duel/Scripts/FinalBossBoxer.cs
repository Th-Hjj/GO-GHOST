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
            default, Tune(.18f,.23f,.60f,13), Tune(.29f,.40f,1.85f,24),
            Tune(.26f,.38f,1.75f,23), Tune(.22f,.54f,2.05f,29), Tune(.26f,.74f,3.55f,36)
        };
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
        public bool CanStart(Punch punch) => Ready(punch) && Current == Punch.None && actor.Running
            && actor.IsAlive && (actor.Motor.Grounded || punch != Punch.Leap) && !Gesturing;

        public void Tick()
        {
            if (Retired || playerBody == null || actor.Player.IsDead) return;
            TickWeapon();
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
            if (Current != Punch.None) { TickPunch(facts); return; }
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
            Vector2 target=OpponentAt(facts,.26f);
            if (punch == Punch.Leap && !FindJump(target,0,out jump)) return false;
            if (punch == Punch.Meteor)
            {
                if (actor.Motor.Grounded && !FindJump(target,.8f,out jump)) return false;
                if (!actor.Motor.Grounded && !FindDive(target,out diveTarget)) return false;
            }
            Current=punch; Cross=false; signatureLeap=punch==Punch.Leap && ShoulderWindow;
            LockedFacing=Mathf.Abs(target.x-actor.Position.x) > .12f ? Mathf.Sign(target.x-actor.Position.x) : actor.Facing;
            actor.Face(Vector2.right*LockedFacing);
            accepted=game.GetBossMoveTuning(true,(int)punch);
            Started[(int)punch]++;
            if (actor.Motor.Grounded || punch==Punch.Meteor) actor.Motor.EndAttackMotion();
            // An airborne straight or hook keeps the steering of its existing jump.
            // The first beat can cover ground even when the target is outside fist reach.
            if (punch==Punch.Jab || punch==Punch.Backfist || punch==Punch.Uppercut)
                DriveTo(target,accepted.Windup,punch==Punch.Jab ? 18f : 13f);
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
                    if (!BeginDive(facts)) { Recover(); return; }
                }
                StartStroke();
            }
            if (Phase == Stage.Slip)
            {
                airborne |= !actor.Motor.Grounded;
                if (Time.time < phaseUntil && !(airborne && actor.Motor.Grounded)) return;
                if (Current==Punch.Meteor && !BeginDive(facts)) { Recover(); return; }
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
                if (Time.time < phaseUntil) return;
                if (Current == Punch.Jab && !Cross && facts.Visible)
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
            if (Current==Punch.Leap && !actor.Motor.Grounded && Time.time-airStart<1.8f) return;
            if (Time.time < phaseUntil) return;
            readyAt[(int)Current]=Time.time+accepted.Cooldown;
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

        bool BeginDive(BossObservation facts)
        {
            var sight=facts.Visible ? OpponentAt(facts,.20f) : jump.Landing;
            if (!FindDive(sight,out diveTarget)) return false;
            var delta=diveTarget-actor.Position;
            actor.Motor.EndAttackMotion();
            actor.Face(delta); LockedFacing=actor.Facing;
            actor.Motor.StartDash(delta,delta.magnitude+.45f,24f);
            return true;
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
            hit=false; Stroke++;
            float active=Current switch { Punch.Uppercut=>.21f, Punch.Backfist=>.22f, Punch.Leap=>.27f,
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
            if (actor.Motor.Grounded || Current==Punch.Meteor) actor.Motor.EndAttackMotion();
            SetPhase(Stage.Recovery,signatureLeap ? Mathf.Max(.76f,accepted.Recovery) : accepted.Recovery);
        }

        internal static readonly Vector2 Guard = new Vector2(.29f,.31f);
        Vector2 Chamber => Current switch
        {
            Punch.Uppercut=>new Vector2(.13f,-.36f), Punch.Backfist=>new Vector2(-.44f,.25f),
            Punch.Leap=>new Vector2(-.21f,.44f), Punch.Meteor=>new Vector2(-.12f,.71f),
            _=>Cross ? new Vector2(-.25f,.20f) : new Vector2(.17f,.27f)
        };
        Vector2 StrokePoint(float t)
        {
            float extend=Mathf.SmoothStep(0,1,t);
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
            Stage.Windup=>Vector2.Lerp(Guard,Chamber,PhaseProgress), Stage.Slip=>Chamber,
            Stage.Active=>StrokePoint(PhaseProgress), Stage.Recovery=>Vector2.Lerp(recoveryHand,Guard,PhaseProgress),
            _=>Guard
        };
        public Vector2 Fist() { var p=HandLocal; return actor.Position+new Vector2(LockedFacing*p.x,p.y); }
        public Bounds PunchArea() => new Bounds(Fist(),Vector3.one*.48f);
        void SampleFist()
        {
            var target=playerBody.bounds;
            Vector2 point=Fist();
            if (!hit && SweptContact(lastFist,point,lastPlayer,target,.23f,lastSample,Time.time)) DealPunch();
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
            actor.Player.TakeDamage(accepted.Damage,actor.gameObject);
            if (actor.Player.CurrentHealth<before) Hits[(int)Punch.Meteor]++;
        }
        void DealPunch()
        {
            hit=true;
            var move=Current;
            int before=actor.Player.CurrentHealth;
            actor.Player.TakeDamage(Cross ? Mathf.RoundToInt(accepted.Damage*1.3f) : accepted.Damage,actor.gameObject);
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
            if (InFlight) actor.Sounds?.StopAttack(actor.ActorId,300000+Orders);
            if (Current != Punch.None) actor.Motor?.EndAttackMotion();
            Current=Punch.None; Phase=Stage.Idle; PreviewPunch=Punch.None;
            weapon=WeaponState.Retired; detached=false; impactSupport=null;
            ImpactAt=float.NegativeInfinity;
        }
    }
}
