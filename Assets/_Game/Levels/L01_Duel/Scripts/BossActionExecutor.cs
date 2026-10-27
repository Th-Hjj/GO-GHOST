using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // One action clock publishes the geometry used by damage, tells and public threat prediction.
    public sealed partial class BossActionExecutor : MonoBehaviour
    {
        public BossAbility Ability {get;private set;}
        public BossActionPhase Phase {get;private set;}
        public int AttackId {get;private set;}
        public int PreparationId {get;private set;}
        public int Ammo {get;private set;}
        public bool Reloading=>reloadUntil>0;
        public bool Busy=>Ability!=BossAbility.None;
        public bool Camouflaged=>Ability==BossAbility.Camo&&Phase!=BossActionPhase.Recovery;
        public bool Committed=>Phase==BossActionPhase.Locked||Phase==BossActionPhase.Active||Phase==BossActionPhase.Recovery;
        public bool HighThreat=>IsHigh(Ability)||PendingSwordThreats;
        public bool OccupiesThreatBudget=>PendingSwordThreats||(HighThreat&&Busy&&!SafeRecovery);
        public bool SafeRecovery=>Phase==BossActionPhase.Recovery&&!PendingSwordThreats
            && !((Ability==BossAbility.DrawDash||Ability==BossAbility.DropCut)&&!comboTarget&&!hasReturned&&Time.time<=returnWindowUntil);
        public float RecoveryRemaining=>Phase==BossActionPhase.Recovery?Mathf.Max(0,phaseUntil-Time.time):0;
        bool SingleCut=>Ability==BossAbility.DashFollowup||Ability==BossAbility.DropFollowup||Ability==BossAbility.FlankCut||Ability==BossAbility.LeapFollowup;
        public bool UsesBladeArc=>Ability==BossAbility.Rhythm||SingleCut||Ability==BossAbility.LeapSlash;
        public float AimDelay=>Phase==BossActionPhase.Prepare?Mathf.Max(0,phaseUntil-Time.time)+LockSeconds
            :BurstAction&&Phase==BossActionPhase.Active?Mathf.Max(p.BurstShotLock,nextShot-Time.time):LockSeconds;
        public bool StaggeredBurst {get;private set;}
        public int DropCorrections {get;private set;}
        bool BurstAction=>Ability==BossAbility.Burst||Ability==BossAbility.TacticalJumpShot;
        bool JumpAction=>Ability==BossAbility.LeapSlash||Ability==BossAbility.TacticalJumpShot||Ability==BossAbility.FlankLeap;
        public bool FlankTravelling=>Ability==BossAbility.FlankLeap&&Phase==BossActionPhase.Active;
        public Vector2[] CommittedJumpPath=>jumpPlan?.JumpPath;
        public float CutProgress=>Ability==BossAbility.LeapSlash?Mathf.Clamp01((Time.time-slashAt)/p.LeapSlash.Active):PhaseProgress;
        public Vector2 JumpDestination=>jumpPlan!=null?jumpPlan.Destination:actor.Position;
        public float PhaseProgress=>Mathf.Clamp01((Time.time-phaseAt)/Mathf.Max(.001f,phaseUntil-phaseAt));
        public Vector2 Aim {get;private set;}=Vector2.left;
        // Steep dashes meet a target with the tall body before its centre reaches the blade sweep.
        // Move the same-radius blade to that leading edge; horizontal/Return hit geometry is unchanged.
        public Vector2 DashBladeOffset => Ability==BossAbility.DrawDash
            ? Aim*DashBladeLead(Aim,actor.Motor.Size,p.BladeRadius+.2f)
            : Vector2.zero;
        public static float DashBladeLead(Vector2 direction,Vector2 bodySize,float radius)
            => Mathf.Max(0,Mathf.Abs(direction.x)*bodySize.x*.5f+Mathf.Abs(direction.y)*bodySize.y*.5f-radius+.025f);
        public Vector2 Origin {get;private set;}
        public Vector2 End {get;private set;}
        public int RhythmIndex {get;private set;}
        public readonly List<BossThreat> PublicThreats=new List<BossThreat>(6);
        public readonly int[] Started=new int[System.Enum.GetValues(typeof(BossAbility)).Length],Activated=new int[System.Enum.GetValues(typeof(BossAbility)).Length];
        public int Guarded {get;private set;}
        public int BeamAllyBlocks {get;private set;}
        public int Interruptions {get;private set;}
        public bool InterruptedCurrent {get;private set;}
        public float LastActiveAt {get;private set;}
        public int ConnectedHits {get;private set;}
        public int ConsecutiveMeleeMisses {get;private set;}
        public int RecoverySteps {get;private set;}
        public bool RecoveryStepping {get;private set;}
        BossAgent actor;
        BossProfile p;
        BossAbilitySettings settings;
        readonly float[] readyAt=new float[System.Enum.GetValues(typeof(BossAbility)).Length];
        BossPlan jumpPlan;
        bool jumpAirborne,slashStarted,slashFinished;
        float jumpDeadline,slashAt;
        float phaseAt,phaseUntil,activeStarts,activeEnds,reloadUntil,nextShot,sweepStart,sweepEnd,lastCamoEnded=-100,returnWindowUntil;
        int shotsRemaining,shotIndex,hitsAtStart,platformActiveBefore;
        bool hitInWindow,comboTarget,hasReturned,bulletLocked;
        float burstStepX;
        bool burstStep;
        float dropBodyWaitUntil;
        Vector2 targetPoint,dashStart,sweepFrom,sweepTo;
        float cutFacing=1,previousCutProgress,camoMovingSeconds;
        Vector2 previousBladeOrigin;
        public Vector2 DropTarget => targetPoint;
        public Vector2 SweepFrom => sweepFrom;
        public Vector2 SweepTo => sweepTo;
        public float BladeReach => UsesBladeArc||Ability==BossAbility.DropCut?p.Sword.CutReach:settings!=null?settings.Range:1.5f;
        public float BladeAngle => UsesBladeArc
            ? Mathf.Lerp(p.RhythmArcFrom[RhythmIndex],p.RhythmArcTo[RhythmIndex],Phase==BossActionPhase.Active?PhaseProgress:Phase==BossActionPhase.Recovery?1:0)
            : -90;
        public Vector2 BladeDirection(float progress)
        {
            float angle=Mathf.Lerp(p.RhythmArcFrom[RhythmIndex],p.RhythmArcTo[RhythmIndex],progress)*Mathf.Deg2Rad;
            return new Vector2(cutFacing*Mathf.Cos(angle),Mathf.Sin(angle));
        }
        float LockSeconds => Ability==BossAbility.Rhythm||SingleCut?p.RhythmLock[RhythmIndex]:Ability==BossAbility.Burst?p.BurstShotLock:settings.Lock;
        float ActiveSeconds => Ability==BossAbility.Rhythm||SingleCut?p.RhythmActive[RhythmIndex]:settings.Active;
        CircleCollider2D guardZone;
        public void Configure(BossAgent owner)
        {
            actor=owner;p=owner.Profile;Ammo=p.Magazine;
            ConfigureSword();
            foreach(var c in GetComponentsInChildren<CircleCollider2D>())if(c.isTrigger){guardZone=c;guardZone.enabled=false;}
        }
        public bool Ready(BossAbility ability)=>Time.time>=readyAt[(int)ability] && !(ability==BossAbility.Camo&&Time.time<lastCamoEnded+1.2f);
        public bool CanAfford(BossAbility ability)=>ability==BossAbility.Burst||ability==BossAbility.TacticalJumpShot?Ammo>0:Ammo>=p.Settings(ability).Cost;
        public bool TryStartMobility(BossPlan value)
        {
            if(value==null||Busy || value.TerrainVersion!=actor.Navigation.Version
                || (p.Kind==BossKind.Severance?value.Ability!=BossAbility.LeapSlash&&value.Ability!=BossAbility.FlankLeap
                    :value.Ability!=BossAbility.TacticalJumpShot))return false;
            jumpPlan=value;
            if(TryStart(value.Ability,value.TargetHypothesis))return true;
            jumpPlan=null;return false;
        }
        public void RequestReload()
        {
            if(reloadUntil>0||Ammo==p.Magazine)return;
            reloadUntil=Time.time+p.ReloadSeconds;actor.Sounds?.Play(Sfx.Reload,actor.ActorId);
            actor.Trace("reload begin");
        }
        public bool TryStart(BossAbility ability,Vector2 hypothesis,bool cooperative=false)
        {
            if(IsSwordPhrase(ability))return !cooperative&&TryStartSword(ability);
            if(!actor.Running||!actor.IsAlive||Busy||!Ready(ability))return false;
            if(actor.Coordinator!=null&&!actor.Coordinator.CanStart(actor,ability,cooperative))return false;
            if(ability!=BossAbility.Camo&&p.Kind==BossKind.Refraction&&(!actor.Motor.Grounded||Reloading))return false;
            if(ability==BossAbility.ReturnCut||ability==BossAbility.DashFollowup||ability==BossAbility.DropFollowup||ability==BossAbility.FlankCut||ability==BossAbility.LeapFollowup)return false;
            if((ability==BossAbility.LeapSlash||ability==BossAbility.TacticalJumpShot||ability==BossAbility.FlankLeap)
                && (jumpPlan==null||!actor.Motor.Grounded))return false;
            if(!CanAfford(ability)){RequestReload();return false;}
            if((ability==BossAbility.DrawDash||ability==BossAbility.Guard||ability==BossAbility.Rhythm)&&!actor.Motor.Grounded)return false;
            if(ability==BossAbility.DropCut&&!actor.Tactics.CanDropFromHere(hypothesis,out _))return false;
            if(ability==BossAbility.Sweep&&!actor.Tactics.TrySweepBand(actor.Position,actor.Perception.Facts,out sweepFrom,out sweepTo))return false;
            if(ability==BossAbility.Burst&&!actor.IsTrainingEnemy&&!actor.Tactics.TryGunTarget(ability,actor.Position,actor.Perception.Facts,out hypothesis))return false;
            if(ability==BossAbility.DrawDash&&!actor.Tactics.LegalAt(ability,actor.Position,actor.Perception.Facts,out _))return false;
            if(IsHigh(ability)&&actor.Coordinator!=null&&!actor.Coordinator.TryReserve(actor,ability,cooperative))return false;
            Ability=ability;settings=p.Settings(ability);AttackId++;Started[(int)ability]++;
            platformActiveBefore=Activated[(int)ability];
            hitsAtStart=ConnectedHits;
            targetPoint=hypothesis;comboTarget=cooperative;RhythmIndex=0;hasReturned=false;bulletLocked=false;Guarded=0;InterruptedCurrent=false;
            returnWindowUntil=-1;
            dropBodyWaitUntil=0;
            StaggeredBurst=!actor.IsTrainingEnemy && ability==BossAbility.Burst && Started[(int)BossAbility.Burst]%2==0
                && (actor.Coordinator==null||actor.Coordinator.Phase==DuoPhase.Idle);
            burstStep=!actor.IsTrainingEnemy&&ability==BossAbility.Burst&&actor.Tactics.TryBurstStep(out burstStepX);
            readyAt[(int)ability]=Time.time+settings.Cooldown;
            Aim=(hypothesis-actor.Position).normalized;if(Aim.sqrMagnitude<.1f)Aim=Vector2.right*actor.Facing;
            actor.Face(Aim);SetPhase(BossActionPhase.Prepare,ability==BossAbility.Burst?settings.Prepare-p.BurstShotLock:settings.Prepare);
            cutFacing=actor.Facing;
            if(ability==BossAbility.Camo){dashStart=actor.Position;camoMovingSeconds=0;actor.BeginCamo();}
            if(ability!=BossAbility.Camo&&!actor.IsTrainingEnemy)actor.Motor.Brake();
            if(ability==BossAbility.Guard)actor.Motor.HoldHorizontal();
            if(ability==BossAbility.Rhythm)actor.Motor.BeginStep(p.RhythmStep[0]);
            var prepareCue=PrepareCue(ability);
            if(prepareCue.HasValue)actor.Sounds?.Play(prepareCue.Value,actor.ActorId,AttackId);
            actor.Perception.ExpressedPressure(actor.Position);
            actor.Trace($"prepare {ability} attack={AttackId}");
            UpdateGeometry();Publish();return true;
        }
        public static bool IsHigh(BossAbility a)=>IsSwordPhrase(a)||a==BossAbility.Piercing||a==BossAbility.Sweep||a==BossAbility.Rhythm||a==BossAbility.DrawDash||a==BossAbility.ReturnCut||a==BossAbility.DropCut||a==BossAbility.LeapSlash||a==BossAbility.DashFollowup||a==BossAbility.DropFollowup||a==BossAbility.FlankLeap||a==BossAbility.FlankCut||a==BossAbility.LeapFollowup;
        public bool TryStartSweep(Vector2 from,Vector2 to)
        {
            int clear=0;
            for(int i=0;i<p.SweepShots;i++)if(actor.Tactics.ClearGunLine(actor.Position,Vector2.Lerp(from,to,i/(float)(p.SweepShots-1))))clear++;
            if(clear<6||!TryStart(BossAbility.Sweep,(from+to)*.5f,true))return false;
            sweepFrom=from;sweepTo=to;UpdateGeometry();Publish();return true;
        }
        Sfx? PrepareCue(BossAbility ability)
        {
            switch(ability){case BossAbility.Piercing:return Sfx.BeamCharge;case BossAbility.Camo:return Sfx.CamoEnter;
                case BossAbility.DrawDash:case BossAbility.LeapSlash:case BossAbility.FlankLeap:return Sfx.DrawPrepare;case BossAbility.ReturnCut:return Sfx.ReturnPrepare;
                default:return null;}
        }
        void SetPhase(BossActionPhase phase,float seconds)
        {if(phase==BossActionPhase.Prepare)PreparationId++;Phase=phase;phaseAt=Time.time;phaseUntil=Time.time+Mathf.Max(0,seconds);}
        void Update()
        {
            if (actor == null || !actor.Running || Time.deltaTime <= 0)
                return;
            if (IsSwordPhrase(Ability))
            {
                TickSwordPhrase();
                return;
            }
            CompleteReloadIfReady();
            if (actor.IsTrainingEnemy)
                UpdateGunAim();
            if (!Busy)
            {
                Publish();
                return;
            }

            // Dispatch once: changing Phase never ticks the next phase in this same Update.
            // Preparation cancellation and accepted recovery follow-ups also skip the tail.
            if (Phase == BossActionPhase.Prepare)
            {
                if (!TickPreparation())
                    return;
            }
            else if (Phase == BossActionPhase.Locked && Time.time >= phaseUntil)
                Activate();
            else if (Phase == BossActionPhase.Active)
                TickActive();
            else if (Phase == BossActionPhase.Recovery && !TickRecovery())
                return;

            ApplyOngoingMovement();
            if (Camouflaged && Ammo == 0)
                RequestReload();
            Publish();
        }

        void CompleteReloadIfReady()
        {
            if (reloadUntil > 0 && Time.time >= reloadUntil)
            {
                reloadUntil = 0;
                Ammo = p.Magazine;
                actor.Trace("reload complete");
            }
        }

        bool TickPreparation()
        {
            if (!actor.IsTrainingEnemy && !comboTarget && !JumpAction
                && Ability != BossAbility.DropCut && Ability != BossAbility.Sweep
                && actor.Perception.Facts.Confidence > .1f)
            {
                if (p.Kind == BossKind.Refraction && Ability != BossAbility.Camo)
                {
                    if (actor.Tactics.TryGunTarget(Ability, actor.Position, actor.Perception.Facts, out var shotPoint, AimDelay))
                        targetPoint = shotPoint;
                    else
                    {
                        AbortPreparation();
                        return false;
                    }
                }
                else
                    targetPoint = p.Kind == BossKind.Severance
                        ? actor.Tactics.SwordTarget(Ability, actor.Position, actor.Perception.Facts, AimDelay, RhythmIndex)
                        : actor.Tactics.PredictBody(actor.Perception.Facts, .16f);
            }
            if (Ability == BossAbility.Rhythm || SingleCut)
            {
                float delta = targetPoint.x - actor.Position.x;
                cutFacing = Mathf.Abs(delta) > .05f ? Mathf.Sign(delta) : cutFacing;
                actor.Face(Vector2.right * cutFacing);
                actor.Motor.SetMove(Mathf.Abs(delta) > BladeReach * .72f ? cutFacing : 0);
            }
            UpdateGeometry();
            if (Time.time >= phaseUntil)
                Lock();
            return true;
        }

        void TickActive()
        {
            if (JumpAction)
                TickJump();
            else if (Ability == BossAbility.Burst && !actor.Motor.Grounded)
                Recover();
            else if (Ability == BossAbility.Burst || Ability == BossAbility.Sweep)
            {
                if (!actor.IsTrainingEnemy && Ability == BossAbility.Burst && shotsRemaining > 0 && !bulletLocked)
                {
                    if (actor.Tactics.TryGunTarget(Ability, actor.Position, actor.Perception.Facts, out var point, AimDelay))
                    {
                        targetPoint = point;
                        UpdateGunAim();
                    }
                    if (Time.time >= nextShot - p.BurstShotLock)
                    {
                        bulletLocked = true;
                        actor.Face(Aim);
                    }
                }
                if (shotsRemaining > 0 && Time.time >= nextShot)
                    FireBullet();
                if (shotsRemaining == 0 && Time.time >= phaseUntil)
                    Recover();
            }
            else if (Ability == BossAbility.DrawDash || Ability == BossAbility.ReturnCut)
            {
                if (!actor.Motor.DashActive && Time.time - phaseAt > .025f)
                    FinishDash();
            }
            else if (Ability == BossAbility.DropCut)
            {
                if (actor.Motor.DropBlocked && dropBodyWaitUntil == 0)
                    dropBodyWaitUntil = Time.time + .3f;
                if (actor.Motor.Grounded || actor.Motor.DropBlocked && Time.time >= dropBodyWaitUntil)
                {
                    SweepDrop();
                    actor.Motor.EndDrop();
                    if (!TryDropFollowup())
                    {
                        returnWindowUntil = !comboTarget && Ready(BossAbility.Rhythm) ? Time.time + .45f : -1;
                        Recover();
                    }
                }
                // No arbitrary active cutoff: one descent window ends on real support.
            }
            else if (Ability == BossAbility.Camo)
            {
                if (actor.Tactics.PlatformBlinkActive)
                {
                    if (actor.Tactics.TickPlatformBlink(Time.time - phaseAt))
                        Recover();
                }
                else
                {
                    if (actor.Velocity.sqrMagnitude > .1f)
                        camoMovingSeconds += Time.deltaTime;
                    if (actor.Tactics.CamoRouteInvalid || Time.time >= phaseUntil
                        || camoMovingSeconds >= p.CamoMinimumMove
                        && Vector2.Distance(actor.Position, dashStart) > .4f && actor.Motor.RouteCompleted)
                        Recover();
                }
            }
            else if (Ability == BossAbility.Guard)
            {
                if (Time.time >= phaseUntil)
                    Recover();
            }
            else if (Time.time >= phaseUntil)
            {
                if (UsesBladeArc)
                {
                    SweepBlade(1);
                    NextRhythm();
                }
                else
                    Recover();
            }
        }

        bool TickRecovery()
        {
            if (Ability == BossAbility.DropCut && Time.time <= returnWindowUntil && TryDropFollowup())
                return false;
            if (Ability == BossAbility.DrawDash && Time.time <= returnWindowUntil && TryDashFollowup())
                return false;
            if (Time.time >= phaseUntil)
                Finish();
            return true;
        }

        void ApplyOngoingMovement()
        {
            if (Ability == BossAbility.Guard)
                actor.Motor.HoldHorizontal();
            if (Ability == BossAbility.Burst && Phase != BossActionPhase.Recovery && actor.Motor.Grounded)
            {
                float direction = burstStep && Mathf.Abs(burstStepX - actor.Position.x) > .12f
                    ? Mathf.Sign(burstStepX - actor.Position.x) : 0;
                if (direction != 0 && !actor.Navigation.CanStand(actor.Position + Vector2.right * direction * .45f, actor.Motor.Size))
                    direction = 0;
                if (direction != 0 && actor.Coordinator != null
                    && actor.Coordinator.Occupied(actor, actor.Position + Vector2.right * direction * .6f))
                    direction = 0;
                actor.Motor.SetMove(direction, .5f);
            }
        }

        void Lock()
        {
            // One visible correction at the end of preparation. The subsequent full lock never homes.
            if(Ability==BossAbility.DropCut&&!comboTarget
                &&actor.Tactics.TryRefineDropTarget(actor.Perception.Facts,targetPoint,LockSeconds,out var corrected))
            {targetPoint=corrected;DropCorrections++;actor.Trace("drop correction to="+corrected);}
            if(JumpAction)
            {
                if(jumpPlan.TerrainVersion!=actor.Navigation.Version || !actor.Motor.Grounded
                    || !actor.Navigation.TryJumpTrajectory(actor.Position,jumpPlan.Destination,jumpPlan.JumpMotion??p.Motion,actor.Motor.Size,out var route,out var path))
                {Recover();return;}
                jumpPlan.Route=route;jumpPlan.JumpPath=path;
                jumpPlan.SlashOffset=Mathf.Min(jumpPlan.SlashOffset,Mathf.Max(.12f,(path.Length-1)*Time.fixedDeltaTime-p.LeapSlash.Active));
                if(Ability==BossAbility.LeapSlash)
                {
                    float best=float.PositiveInfinity;
                    for(int i=6;i<path.Length-Mathf.CeilToInt(p.LeapSlash.Active/Time.fixedDeltaTime);i+=3)
                    {
                        Vector2 predicted=actor.Tactics.PredictSwordBody(actor.Perception.Facts,LockSeconds+i*Time.fixedDeltaTime);
                        Vector2 delta=predicted-path[i];
                        if(Mathf.Sign(delta.x)!=Mathf.Sign(jumpPlan.TargetHypothesis.x-actor.Position.x)
                            ||Mathf.Abs(delta.y)>Mathf.Abs(delta.x)*.55f+actor.Perception.Facts.Body.extents.y)continue;
                        float distance=Vector2.Distance(path[i],predicted);
                        if(distance<best){best=distance;jumpPlan.SlashOffset=i*Time.fixedDeltaTime;targetPoint=predicted;}
                    }
                }
                cutFacing=Mathf.Sign(targetPoint.x-actor.Position.x);
            }
            UpdateGeometry();
            if((Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)&&Vector2.Distance(Origin,End)<.65f){Recover();return;}
            if(Ability==BossAbility.DropCut&&actor.Motor.Grounded){Recover();return;}
            if(Ability==BossAbility.Guard)Aim=Vector2.right*Mathf.Sign(Aim.x);
            if(Ability==BossAbility.Burst)UpdateGunAim();
            if(Ability==BossAbility.Rhythm||SingleCut){actor.Motor.Brake();cutFacing=actor.Facing;}
            if(Ability==BossAbility.DropCut&&!actor.Tactics.CanDropFromHere(targetPoint,out _,true)){Recover();return;}
            actor.Face(Aim);SetPhase(BossActionPhase.Locked,LockSeconds);
            bulletLocked=Ability==BossAbility.Burst;
            if(Ability==BossAbility.Piercing)actor.Sounds?.Play(Sfx.BeamLock,actor.ActorId,AttackId);
            activeStarts=Time.time+LockSeconds;activeEnds=activeStarts+(Ability==BossAbility.Piercing?.0001f:ActiveSeconds);
            actor.Trace($"lock {Ability} origin={Origin} end={End}");
            if(LockSeconds<=0)Activate();
        }
        void Activate()
        {
            if(JumpAction && !actor.Motor.StartCommittedJump(jumpPlan.Route,jumpPlan.JumpMotion)){Recover();return;}
            SetPhase(BossActionPhase.Active,Ability==BossAbility.Camo?p.CamoDuration:ActiveSeconds);
            Activated[(int)Ability]++;LastActiveAt=Time.time;hitInWindow=false;bossesHitInWindow.Clear();activeStarts=Time.time;
            activeEnds=Ability==BossAbility.Piercing?Time.time+.0001f:phaseUntil;actor.Trace($"active {Ability} attack={AttackId}");
            if(JumpAction)
            {
                jumpAirborne=slashStarted=slashFinished=false;
                jumpDeadline=Time.time+jumpPlan.Route.Duration+.6f;
                slashAt=Time.time+Time.fixedDeltaTime+jumpPlan.SlashOffset;
                if(BurstAction){shotsRemaining=Mathf.Min(3,Ammo);shotIndex=0;bulletLocked=false;nextShot=Time.time+Time.fixedDeltaTime+p.BurstShotLock;}
            }
            else if(Ability==BossAbility.Burst||Ability==BossAbility.Sweep)
            {shotsRemaining=Ability==BossAbility.Burst?Mathf.Min(3,Ammo):p.SweepShots;shotIndex=0;nextShot=Time.time;FireBullet();}
            else if(Ability==BossAbility.Piercing)
            {
                Ammo-=settings.Cost;
                Vector2 actualEnd=BeamEnd(Origin,Aim);
                if((actualEnd-Origin).sqrMagnitude<(End-Origin).sqrMagnitude){End=actualEnd;BeamAllyBlocks++;}
                actor.Sounds?.Play(Sfx.BeamFire,actor.ActorId,AttackId);actor.View?.Beam(Origin,End);
                HitSegment(Origin,End,p.BeamRadius,settings.Damage,false);
            }
            else if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
            {
                dashStart=actor.Position;
                actor.Motor.StartDash(Aim,Vector2.Distance(dashStart,End),Ability==BossAbility.DrawDash?p.DashSpeed:p.ReturnSpeed);
                activeEnds=Time.time+Vector2.Distance(dashStart,End)/(Ability==BossAbility.DrawDash?p.DashSpeed:p.ReturnSpeed);
                phaseUntil=activeEnds;
                actor.Sounds?.Play(Sfx.DashCut,actor.ActorId,AttackId);actor.View?.Slash(Aim,settings.Range,true);
            }
            else if(UsesBladeArc)
            {
                previousBladeOrigin=actor.Position;previousCutProgress=0;
                actor.Sounds?.Play(RhythmIndex==2?Sfx.BladeHeavy:Sfx.BladeLight,actor.ActorId,AttackId,RhythmIndex);
            }
            else if(Ability==BossAbility.DropCut)
            {
                float height=Mathf.Max(.01f,actor.Position.y-targetPoint.y);
                float gravity=-Physics2D.gravity.y*p.Motion.GravityScale;
                float fall=(Mathf.Sqrt(p.DropSpeed*p.DropSpeed+2*gravity*height)-p.DropSpeed)/gravity;
                actor.Motor.StartDrop((targetPoint.x-actor.Position.x)/Mathf.Max(.02f,fall),targetPoint.x);
                previousBladeOrigin=actor.Position;
                activeEnds=Time.time+fall+Time.fixedDeltaTime;
                actor.Sounds?.Play(Sfx.BladeHeavy,actor.ActorId,AttackId);
            }
            else if(Ability==BossAbility.Guard&&guardZone!=null)guardZone.enabled=true;
            if(Ammo==0)RequestReload();Publish();
        }
        void Recover()
        {
            if(guardZone!=null)guardZone.enabled=false;
            if(Ability==BossAbility.DropCut)actor.Motor.EndDrop();
            if(Ability==BossAbility.Camo)
            {
                actor.Tactics.EndCamoRoute();lastCamoEnded=Time.time;
                actor.Sounds?.Play(Sfx.CamoExit,actor.ActorId,AttackId);actor.PublishCue(.15f);
            }
            if(!actor.IsTrainingEnemy)actor.Motor.Brake();
            PublicThreats.Clear();SetPhase(BossActionPhase.Recovery,settings.Recovery);
            RecoveryStepping=false;
            if(Ability!=BossAbility.Guard&&SafeRecovery&&!comboTarget&&actor.Tactics.TrySwordRecoveryStep(out float step))
            {
                RecoveryStepping=true;RecoverySteps++;
                actor.Motor.BeginStep(Mathf.Abs(step));actor.Motor.SetMove(Mathf.Sign(step),.65f);
            }
        }
        public void AbortPreparation()
        { if(Phase==BossActionPhase.Prepare){actor.Trace("cancel "+Ability);actor.Sounds?.StopAttack(actor.ActorId,AttackId);Recover();} }
        void Finish()
        {
            actor.Tactics.PlatformAttackFinished(Ability, !InterruptedCurrent && Activated[(int)Ability] > platformActiveBefore);
            if(RecoveryStepping){actor.Motor.Brake();RecoveryStepping=false;}
            if(UsesBladeArc||Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut||Ability==BossAbility.DropCut)
                ConsecutiveMeleeMisses=ConnectedHits>hitsAtStart?0:ConsecutiveMeleeMisses+1;
            jumpPlan=null;
            actor.Coordinator?.Release(actor);Ability=BossAbility.None;Phase=BossActionPhase.Idle;PublicThreats.Clear();
            swordHazards?.Publish(PublicThreats);
        }
        public void StopCombat()
        {
            actor?.Tactics?.CancelPlatformPressure();
            StopSword();
            if(RecoveryStepping){actor.Motor.Brake();RecoveryStepping=false;}
            if(guardZone!=null)guardZone.enabled=false;reloadUntil=0;PublicThreats.Clear();jumpPlan=null;
            actor?.Coordinator?.Release(actor);Ability=BossAbility.None;Phase=BossActionPhase.Idle;
        }
        // Ordinary hits still damage health, but do not cancel G3/G4 preparation.
        public void OnDamaged(GameObject source) { }
    }
}
