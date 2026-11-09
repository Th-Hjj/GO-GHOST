using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        // Chop and Spin remain the independent, untold phase-two axe attacks.
        public enum Move { None, Chop, Spin, TripleCut, LeapingCrosscut, AnchorVault, UnderpassReturn, PalmDeflection }
        public enum Stage { Idle, Prepare, Locked, Active, Recovery }
        const float CastLock=.25f;
        readonly struct Timing
        {
            public readonly float Prepare, Lock, Active, Recovery, Cooldown;
            public readonly int Damage;
            public Timing(float prepare,float locked,float active,float recovery,float cooldown,int damage)
            { Prepare=prepare; Lock=locked; Active=active; Recovery=recovery; Cooldown=cooldown; Damage=damage; }
        }
        static readonly Timing[] Timings =
        {
            default,
            new Timing(.42f,.14f,.12f,.50f,1.8f,26), new Timing(.45f,.15f,1f,.65f,5f,20),
            new Timing(.32f,.16f,1.12f,.50f,2.1f,30), new Timing(.26f,.14f,.40f,.34f,2.4f,28),
            new Timing(.50f,CastLock,2.50f,.62f,5.6f,34), new Timing(.43f,CastLock,UnderCatch,.45f,4.8f,22),
            new Timing(.53f,CastLock,PalmCatch+.44f,.68f,7.2f,28)
        };
        internal static BossMoveTuning DefaultTuning(Move move)
        {
            var t=Timings[(int)move]; return new BossMoveTuning(t.Prepare,t.Recovery,t.Cooldown,BossProfile.AttackDamage(t.Damage));
        }
        public const float ChopReach=2.3f, ChopHeight=2.1f, SpinRadius=1.65f, AxeRadius=.32f;
        const float SpinMove=.45f;
        internal static readonly int[] InvasionPercents={66,33};
        internal const float ChargeAt=.90f, PinchAt=3.50f, BurstAt=3.70f;

        readonly DuelGame game;
        readonly BossAgent actor;
        readonly Collider2D playerBody;
        readonly System.Action onPhaseTwo;
        readonly System.Func<ConditionalRule> onInvasion;
        readonly float[] readyAt=new float[Timings.Length];
        readonly List<Vector2> path=new List<Vector2>(12);
        int attackId=100000;
        bool waveReleased, waveTouched, transitionHeld,hitDone;
        float invasionRecoverAt;
        internal Vector2 WaveOrigin { get; private set; }
        internal float WaveRadius { get; private set; }
        internal Vector2 WaveContact { get; private set; }
        float phaseAt,phaseUntil,activeAt,activeEnds,nextDecisionAt,transitionUntil,invasionAt,lockedFacing=1;
        Timing acceptedTiming;
        internal Move PreviewMove {get;set;}
        public Move Current {get;private set;}
        public Stage Phase {get;private set;}
        public float PhaseProgress=>Mathf.InverseLerp(phaseAt,phaseUntil,Time.time);
        public float ActiveProgress=>Mathf.InverseLerp(activeAt,activeEnds,Time.time);
        public float ActiveTime=>Mathf.Max(0,Time.time-activeAt);
        public bool Committed=>Current!=Move.None||Transitioning||Invading;
        public bool Invading {get;private set;}
        public int Invasions {get;private set;}
        internal float InvasionAge=>Mathf.Max(0,Time.time-invasionAt);
        internal float InvasionRecovery=>invasionRecoverAt<0?0:Mathf.Clamp01((Time.time-invasionRecoverAt)/.42f);
        public ConditionalRule LastInvasion {get;private set;}
        public bool Transitioning {get;private set;}
        public bool PhaseTwo {get;private set;}
        public bool AxeFlying {get;private set;}
        public bool WindingCast => (Phase==Stage.Prepare || Phase==Stage.Locked) &&
            (Current==Move.AnchorVault || Current==Move.UnderpassReturn || Current==Move.PalmDeflection);
        public float CastProgress => !WindingCast?0:(Phase==Stage.Prepare?acceptedTiming.Prepare*PhaseProgress:
            acceptedTiming.Prepare+acceptedTiming.Lock*PhaseProgress)/(acceptedTiming.Prepare+acceptedTiming.Lock);
        public Vector2 AxePosition {get;private set;}
        public float AxeSpin {get;private set;}
        public float LockedFacing=>lockedFacing;
        public IReadOnlyList<Vector2> Path=>path;
        public readonly int[] Started=new int[Timings.Length],Hits=new int[Timings.Length];

        public FinalBossAxe(BossAgent owner,PlayerHealth player,System.Action phaseTwo,System.Func<ConditionalRule> invasion)
        {
            game=owner.GetComponentInParent<DuelGame>(); actor=owner; onPhaseTwo=phaseTwo; onInvasion=invasion;
            playerBody=player!=null?player.GetComponent<Collider2D>():null;
            nextDecisionAt=Time.time+1f;
        }
        public bool Ready(Move move)=>move>Move.None&&(int)move<readyAt.Length&&Time.time>=readyAt[(int)move];
        public bool CanStart(Move move)=>Ready(move)&&Current==Move.None&&!Invading&&!Transitioning&&
            (PhaseTwo?(move==Move.Chop||move==Move.Spin):move>=Move.TripleCut)&&actor.Running&&actor.IsAlive&&
            !actor.Health.AwaitingSecondPhase&&actor.Motor.Grounded;

        public void Tick()
        {
            if(TickTransition())return;
            if(TickInvasion())return;
            if(Current!=Move.None) { TickMove(); return; }
            if(actor.Player==null||actor.Player.IsDead){actor.Motor.Brake();return;}
            if(!PhaseTwo) { if(Time.time>=nextDecisionAt)TickPhaseOneTactics(); return; }
            if(PreviewMove!=Move.None&&TryStart(PreviewMove)){PreviewMove=Move.None;return;}
            var seen=actor.Perception.Facts;
            if(seen.Confidence<=.1f){actor.Motor.Brake();return;}
            Vector2 delta=seen.Position-actor.Position;float gap=Mathf.Abs(delta.x);
            if(Mathf.Abs(delta.y)<1.4f&&Time.time>=nextDecisionAt&&
                (gap<2.5f&&TryStart(Move.Chop)||gap<2.2f&&TryStart(Move.Spin)))return;
            actor.Motor.SetMove(gap>1.1f?Mathf.Sign(delta.x):0);
            if(delta.y>1.4f&&gap<2.6f&&actor.Motor.Grounded)actor.Motor.TryJump();
        }
        bool StartPhaseOne(AxePlan plan)
        {
            if(!CanStart(plan.Move))return false;
            acceptedPlan=plan;
            actor.Face(Vector2.right*plan.Facing); lockedFacing=actor.Facing;
            // Melee keeps the momentum of its approach until the wind-up ends.
            // A throw instead winds up from a planted stance.
            if (AdvancesDuringWindup(plan.Move)) actor.Motor.ClearRoute();
            else actor.Motor.EndAttackMotion();
            ResetPhaseOneState();
            BuildFlight(plan,flight);
            Begin(plan.Move);
            return true;
        }
        public bool TryStart(Move move)
        {
            if(!CanStart(move))return false;
            if(!PhaseTwo)return TryPlanPhaseOne(move,true,out var plan)&&StartPhaseOne(plan);
            TrackPhaseTwoTarget();
            Begin(move);
            readyAt[(int)move]=Time.time+acceptedTiming.Cooldown;
            return true;
        }
        void Begin(Move move)
        {
            var tuning=game.GetBossMoveTuning(PhaseTwo,(int)move);
            var def=Timings[(int)move];
            acceptedTiming=new Timing(tuning.Windup,def.Lock,def.Active,tuning.Recovery,tuning.Cooldown,tuning.Damage);
            Current=move;attackId++;Started[(int)move]++;hitDone=false;AxeFlying=false;
            if (!AdvancesDuringWindup(move) || !StepIntoWindup(move)) actor.Motor.HoldHorizontal();
            SetPhase(Stage.Prepare,acceptedTiming.Prepare);
            activeAt=phaseUntil+acceptedTiming.Lock;
            activeEnds=activeAt+acceptedTiming.Active;
            actor.Sounds?.Play(Sfx.DrawPrepare,actor.ActorId,attackId);
            actor.Perception.ExpressedPressure(actor.Position);
            actor.Trace($"axe prepare {move} attack={attackId}");
        }
        void SetPhase(Stage stage,float seconds){Phase=stage;phaseAt=Time.time;phaseUntil=phaseAt+Mathf.Max(0,seconds);}
        void TickMove()
        {
            if(!PhaseTwo)UpdateFlightWarning();
            if(Phase==Stage.Prepare)
            {
                if(PhaseTwo)TrackPhaseTwoTarget();
                if(!AdvancesDuringWindup(Current))actor.Motor.HoldHorizontal();
                if(Time.time>=phaseUntil)
                {
                    if(!PhaseTwo && !LockPhaseOneAim()){RecoverPhaseOne();return;}
                    SetPhase(Stage.Locked,acceptedTiming.Lock);activeAt=phaseUntil;activeEnds=activeAt+acceptedTiming.Active;
                    if(Current==Move.TripleCut&&!SafeStep(TripleStrides[0]*lockedFacing))actor.Motor.HoldHorizontal();
                    if(Current==Move.LeapingCrosscut)actor.Motor.HoldHorizontal();
                    if(!PhaseTwo)UpdateFlightWarning();
                    actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,attackId);
                }
            }
            else if(Phase==Stage.Locked)
            {
                if(Current!=Move.TripleCut)actor.Motor.HoldHorizontal();
                if(Time.time>=phaseUntil)
                {
                    activeAt=Time.time;activeEnds=activeAt+acceptedTiming.Active;
                    SetPhase(Stage.Active,acceptedTiming.Active);
                    if(!PhaseTwo)ActivatePhaseOne();
                    if(Phase==Stage.Active)
                        actor.Sounds?.Play(Current==Move.Chop?Sfx.BladeHeavy:Sfx.BladeLight,actor.ActorId,attackId);
                }
            }
            else if(Phase==Stage.Active)
            {
                if(PhaseTwo)TickPhaseTwoAttack();else TickPhaseOneAttack();
            }
            else
            {
                actor.Motor.HoldHorizontal();
                if(Time.time>=phaseUntil)
                {
                    if(!PhaseTwo)
                    {
                        readyAt[(int)Current]=Time.time+acceptedTiming.Cooldown;
                        ObserveLastAttack();
                    }
                    actor.Trace($"axe finish {Current} attack={attackId}");
                    Current=Move.None;Phase=Stage.Idle;AxeFlying=false;path.Clear();
                    nextDecisionAt=Time.time+(PhaseTwo?.25f:0);
                }
            }
        }
        void TrackPhaseTwoTarget()
        {
            var facts=actor.Perception.Facts;
            if(facts.Confidence>.1f)actor.Face(facts.Position-actor.Position);
            lockedFacing=actor.Facing;
        }
        void TickPhaseTwoAttack()
        {
            if(Current==Move.Chop)
            {
                actor.Motor.HoldHorizontal();
                if(!hitDone&&ChopArea().Intersects(PlayerBounds()))hitDone=Strike(acceptedTiming.Damage);
            }
            else
            {
                float toward=actor.Player!=null?Mathf.Sign(actor.Player.transform.position.x-actor.Position.x):0;
                actor.Motor.SetMove(toward,SpinMove);
                AxeSpin=ActiveProgress*1080f;
                if(!hitDone&&PlayerBounds().SqrDistance(actor.Position)<=SpinRadius*SpinRadius)hitDone=Strike(acceptedTiming.Damage);
            }
            if(Current!=Move.None&&Time.time>=activeEnds)
            {
                AxeSpin=0;actor.Motor.HoldHorizontal();SetPhase(Stage.Recovery,acceptedTiming.Recovery);
            }
        }
        Bounds PlayerBounds()=>playerBody!=null?playerBody.bounds:new Bounds(Vector3.one*9999,Vector3.zero);
        bool Strike(int damage)
        {
            var player=actor.Player;if(player==null||player.IsDead)return false;
            var move=Current;int before=player.CurrentHealth;
            player.TakeDamage(damage,actor.gameObject);
            if(player.CurrentHealth<before)Hits[(int)move]++;
            return true;
        }
        public Bounds ChopArea()=>new Bounds(actor.Position+new Vector2(lockedFacing*ChopReach*.5f,.05f),new Vector3(ChopReach,ChopHeight,1));
        Bounds SpinArea()
        {
            var area=new Bounds(actor.Position,new Vector3(SpinRadius*2,SpinRadius*2,1));
            float travel=actor.Motor.Profile.Speed*SpinMove*Timings[(int)Move.Spin].Active;
            area.Encapsulate(new Bounds(actor.Position+Vector2.right*lockedFacing*travel,area.size));return area;
        }
        public void Publish(List<BossThreat> output)
        {
            if(Current==Move.None||Phase==Stage.Recovery||Phase==Stage.Idle)return;
            if(!PhaseTwo){PublishPhaseOne(output);return;}
            output.Add(new BossThreat{ActorId=actor.ActorId,AttackId=attackId,Valid=true,Facing=lockedFacing,
                Phase=ThreatPhase,Shape=BossThreatShape.Box,Area=Current==Move.Chop?ChopArea():SpinArea(),
                Origin=actor.Position,StartsAt=activeAt,EndsAt=activeEnds});
        }
        BossThreatPhase ThreatPhase=>Phase==Stage.Prepare?BossThreatPhase.Preparation:Phase==Stage.Locked?BossThreatPhase.Locked:BossThreatPhase.Active;
        public void Cancel()
        {
            PreviewMove=Move.None;
            ClearAttackPosition();
            pressUntil=0;
            pendingResult=Move.None;
            if(Invading){Invading=false;actor.Health.Invulnerable=false;}
            if(!PhaseTwo&&Current!=Move.None)actor.Motor?.EndAttackMotion();
            Current=Move.None;Phase=Stage.Idle;AxeFlying=false;AxeSpin=0;path.Clear();flight.Clear();
            nextDecisionAt=Time.time+.5f;
        }

        internal void SetNextInvasionFloor()
        {
            actor.Health.DamageFloor = Invasions < InvasionPercents.Length
                ? actor.Health.MaxHealth * InvasionPercents[Invasions] / 100 : 0;
        }

        // Damage stops at the mark even while the Boss finishes his move and lands.
        bool TickInvasion()
        {
            if (!Invading)
            {
                var health = actor.Health;
                if (health.DamageFloor <= 0 || health.CurrentHealth > health.DamageFloor ||
                    Current != Move.None || PhaseTwo || health.PhaseIndex != 1) return false;
                if (!actor.Motor.Grounded) { if(!actor.Motor.HasRoute)RecoverFooting(); return true; }
                ClearAttackPosition();
                Invading = true; waveReleased = waveTouched = false; invasionAt = Time.time;
                WaveRadius = 0; LastInvasion = null; invasionRecoverAt = -1;
                health.Invulnerable = true;
                actor.Motor.HoldHorizontal();
                var observed = actor.Perception.Facts;
                if (observed.Visible) actor.Face(observed.Position - actor.Position);
                lockedFacing = actor.Facing;
                actor.Trace($"invasion {Invasions + 1} begins");
            }
            actor.Motor.HoldHorizontal();
            if (InvasionAge < BurstAt) return true;
            if (!waveReleased)
            {
                waveReleased = true;
                WaveOrigin = actor.View.InvasionChargePoint;
                actor.Sounds?.Play(Sfx.EmpBurst, actor.ActorId);
            }
            WaveRadius = BossSilhouetteView.ShockwaveStartRadius + (InvasionAge - BurstAt) * 22f;
            if (!waveTouched && playerBody != null && actor.Player != null && !actor.Player.IsDead)
            {
                WaveContact = playerBody.ClosestPoint(WaveOrigin);
                float distance = Vector2.Distance(WaveOrigin,WaveContact);
                if (WaveRadius < distance) return true;
                WaveRadius = distance;
                waveTouched = true;
                actor.View.DrawInvasionContact(this);
                game.HoldInvasion(this);
                return true;
            }
            if (invasionRecoverAt < 0) invasionRecoverAt = Time.time;
            if (InvasionRecovery < 1) return true;
            Invading = false; Invasions++;
            SetNextInvasionFloor();
            actor.Health.Invulnerable = false;
            nextDecisionAt = Time.time + .3f;
            return true;
        }

        internal ConditionalRule WriteInvasion() => LastInvasion ??= onInvasion?.Invoke();

        // Hold the spent first pool until the scripted transition releases combat.
        bool TickTransition()
        {
            if (!Transitioning && !PhaseTwo && actor.Health.AwaitingSecondPhase)
            {
                Cancel(); Transitioning = true; transitionUntil = Time.time + 2.2f;
                // The game's scripted transition holds the Boss until its countdown ends.
                transitionHeld = onPhaseTwo != null;
                actor.Motor.HoldHorizontal();
                onPhaseTwo?.Invoke();
                actor.Trace("final boss phase 2: transition");
            }
            if (!Transitioning) return false;
            actor.Motor.HoldHorizontal();
            if (transitionHeld || Time.time < transitionUntil) return true;
            actor.Health.BeginSecondPhase();
            Transitioning = false; PhaseTwo = true;
            actor.RestoreSecondPhaseMotion();
            actor.View?.SetFinalBossPhaseTwo();
            nextDecisionAt = Time.time + .6f;
            return true;
        }

        public void ReleaseTransition() => transitionHeld = false;

        // Direct retry starts with a flowing rainbow and the charged combat axe.
        public void StartInPhaseTwo()
        {
            Cancel();
            actor.Health.SkipToSecondPhase();
            PhaseTwo = true; actor.RestoreSecondPhaseMotion(); Transitioning = false; transitionHeld = false;
            actor.View?.SetFinalBossPhaseTwo();
        }

    }
}
