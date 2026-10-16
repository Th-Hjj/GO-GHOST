using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // One action clock publishes the geometry used by damage, tells and public threat prediction.
    public sealed class BossActionExecutor : MonoBehaviour
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
        public bool HighThreat=>IsHigh(Ability);
        public bool SafeRecovery=>Phase==BossActionPhase.Recovery
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
        int shotsRemaining,shotIndex,hitsAtStart;
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
        public float BladeReach => UsesBladeArc?actor.Navigation.PlatformWidth*(p.RhythmReach[RhythmIndex]/4f):settings!=null?settings.Range:1.2f;
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
            if(ability==BossAbility.Burst&&!actor.Tactics.TryGunTarget(ability,actor.Position,actor.Perception.Facts,out hypothesis))return false;
            if(ability==BossAbility.DrawDash&&!actor.Tactics.LegalAt(ability,actor.Position,actor.Perception.Facts,out _))return false;
            if(IsHigh(ability)&&actor.Coordinator!=null&&!actor.Coordinator.TryReserve(actor,ability,cooperative))return false;
            Ability=ability;settings=p.Settings(ability);AttackId++;Started[(int)ability]++;
            hitsAtStart=ConnectedHits;
            targetPoint=hypothesis;comboTarget=cooperative;RhythmIndex=0;hasReturned=false;bulletLocked=false;Guarded=0;InterruptedCurrent=false;
            returnWindowUntil=-1;
            dropBodyWaitUntil=0;
            StaggeredBurst=ability==BossAbility.Burst && Started[(int)BossAbility.Burst]%2==0
                && (actor.Coordinator==null||actor.Coordinator.Phase==DuoPhase.Idle);
            burstStep=ability==BossAbility.Burst&&actor.Tactics.TryBurstStep(out burstStepX);
            readyAt[(int)ability]=Time.time+settings.Cooldown;
            Aim=(hypothesis-actor.Position).normalized;if(Aim.sqrMagnitude<.1f)Aim=Vector2.right*actor.Facing;
            actor.Face(Aim);SetPhase(BossActionPhase.Prepare,ability==BossAbility.Burst?settings.Prepare-p.BurstShotLock:settings.Prepare);
            cutFacing=actor.Facing;
            if(ability==BossAbility.Camo){dashStart=actor.Position;camoMovingSeconds=0;actor.BeginCamo();}
            if(ability!=BossAbility.Camo)actor.Motor.Brake();
            if(ability==BossAbility.Rhythm)actor.Motor.BeginStep(p.RhythmStep[0]);
            var prepareCue=PrepareCue(ability);
            if(prepareCue.HasValue)actor.Sounds?.Play(prepareCue.Value,actor.ActorId,AttackId);
            actor.Perception.ExpressedPressure(actor.Position);
            actor.Trace($"prepare {ability} attack={AttackId}");
            UpdateGeometry();Publish();return true;
        }
        public static bool IsHigh(BossAbility a)=>a==BossAbility.Piercing||a==BossAbility.Sweep||a==BossAbility.Rhythm||a==BossAbility.DrawDash||a==BossAbility.ReturnCut||a==BossAbility.DropCut||a==BossAbility.LeapSlash||a==BossAbility.DashFollowup||a==BossAbility.DropFollowup||a==BossAbility.FlankLeap||a==BossAbility.FlankCut||a==BossAbility.LeapFollowup;
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
            if(actor==null||!actor.Running||Time.deltaTime<=0)return;
            if(reloadUntil>0&&Time.time>=reloadUntil){reloadUntil=0;Ammo=p.Magazine;actor.Trace("reload complete");}
            if(!Busy)return;
            if(Phase==BossActionPhase.Prepare)
            {
                if(!comboTarget && !JumpAction && Ability!=BossAbility.DropCut && Ability!=BossAbility.Sweep && actor.Perception.Facts.Confidence>.1f)
                {
                    if(p.Kind==BossKind.Refraction && Ability!=BossAbility.Camo)
                    {
                        if(actor.Tactics.TryGunTarget(Ability,actor.Position,actor.Perception.Facts,out var shotPoint,AimDelay))targetPoint=shotPoint;
                        else {AbortPreparation();return;}
                    }
                    else targetPoint=actor.Tactics.PredictBody(actor.Perception.Facts,.16f);
                }
                if(Ability==BossAbility.Rhythm||SingleCut)
                {
                    float delta=targetPoint.x-actor.Position.x;
                    cutFacing=Mathf.Abs(delta)>.05f?Mathf.Sign(delta):cutFacing;
                    actor.Face(Vector2.right*cutFacing);
                    actor.Motor.SetMove(Mathf.Abs(delta)>BladeReach*.72f?cutFacing:0);
                }
                UpdateGeometry();
                if(Time.time>=phaseUntil)Lock();
            }
            else if(Phase==BossActionPhase.Locked&&Time.time>=phaseUntil)Activate();
            else if(Phase==BossActionPhase.Active)
            {
                if(JumpAction)TickJump();
                else if(Ability==BossAbility.Burst&&!actor.Motor.Grounded)Recover();
                else if(Ability==BossAbility.Burst||Ability==BossAbility.Sweep)
                {
                    if(Ability==BossAbility.Burst && shotsRemaining>0 && !bulletLocked)
                    {
                        if(actor.Tactics.TryGunTarget(Ability,actor.Position,actor.Perception.Facts,out var point,AimDelay))
                        {targetPoint=point;UpdateGunAim();}
                        if(Time.time>=nextShot-p.BurstShotLock){bulletLocked=true;actor.Face(Aim);}
                    }
                    if(shotsRemaining>0&&Time.time>=nextShot)FireBullet();
                    if(shotsRemaining==0&&Time.time>=phaseUntil)Recover();
                }
                else if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                {if(!actor.Motor.DashActive&&Time.time-phaseAt>.025f)FinishDash();}
                else if(Ability==BossAbility.DropCut)
                {
                    if(actor.Motor.DropBlocked&&dropBodyWaitUntil==0)dropBodyWaitUntil=Time.time+.3f;
                    if(actor.Motor.Grounded||actor.Motor.DropBlocked&&Time.time>=dropBodyWaitUntil)
                    {
                        SweepDrop();actor.Motor.EndDrop();
                        if(!TryDropFollowup())
                        {returnWindowUntil=!comboTarget&&Ready(BossAbility.Rhythm)?Time.time+.45f:-1;Recover();}
                    }
                    // No arbitrary active cutoff: one descent window ends on real support.
                }
                else if(Ability==BossAbility.Camo)
                {
                    if(actor.Velocity.sqrMagnitude>.1f)camoMovingSeconds+=Time.deltaTime;
                    if(actor.Tactics.CamoRouteInvalid || Time.time>=phaseUntil || camoMovingSeconds>=p.CamoMinimumMove
                        && Vector2.Distance(actor.Position,dashStart)>.4f && actor.Motor.RouteCompleted)Recover();
                }
                else if(Ability==BossAbility.Guard)
                {
                    if(Time.time>=phaseUntil || Time.time-phaseAt>=.18f && Ready(BossAbility.Rhythm)
                        && actor.Tactics.LegalAt(BossAbility.Rhythm,actor.Position,actor.Perception.Facts,out _))Recover();
                }
                else if(Time.time>=phaseUntil)
                {
                    if(UsesBladeArc){SweepBlade(1);NextRhythm();}else Recover();
                }
            }
            else if(Phase==BossActionPhase.Recovery)
            {
                if(Ability==BossAbility.DropCut&&Time.time<=returnWindowUntil&&TryDropFollowup())return;
                if(Ability==BossAbility.DrawDash&&Time.time<=returnWindowUntil&&TryDashFollowup())return;
                if(Time.time>=phaseUntil)Finish();
            }
            if(Ability==BossAbility.Guard&&Phase==BossActionPhase.Active)actor.Motor.SetMove(Aim.x,p.GuardWalkScale);
            if(Ability==BossAbility.Burst&&Phase!=BossActionPhase.Recovery&&actor.Motor.Grounded)
            {
                float direction=burstStep&&Mathf.Abs(burstStepX-actor.Position.x)>.12f?Mathf.Sign(burstStepX-actor.Position.x):0;
                if(direction!=0&&!actor.Navigation.CanStand(actor.Position+Vector2.right*direction*.45f,actor.Motor.Size))direction=0;
                if(direction!=0&&actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,actor.Position+Vector2.right*direction*.6f))direction=0;
                actor.Motor.SetMove(direction,.5f);
            }
            if(Camouflaged&&Ammo==0)RequestReload();
            Publish();
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
        void UpdateGeometry()
        {
            Vector2 centre=actor.Position;
            Aim=(targetPoint-(centre+Vector2.up*.28f)).normalized;
            if(Aim.sqrMagnitude<.1f)Aim=Vector2.right*actor.Facing;
            Origin=centre+Vector2.up*.28f+Aim*.45f;
            End=targetPoint;
            if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
            {
                Aim=Vector2.right*Mathf.Sign(targetPoint.x-centre.x);
                Origin=centre;
                if(Ability==BossAbility.DrawDash)
                {if(!actor.Navigation.TryHorizontalAttack(centre,targetPoint,actor.Motor.Size,out var safe))End=centre;else End=safe;}
                if(Ability==BossAbility.ReturnCut){actor.Navigation.TryDash(centre,centre+Aim*Mathf.Min(p.ReturnCut.Range,Mathf.Abs(targetPoint.x-centre.x)+.75f),actor.Motor.Size,out var safe);End=safe;}
            }
            else if(UsesBladeArc)
            {Aim=BladeDirection(0);Origin=centre;End=centre+Aim*BladeReach;}
            else if(Ability==BossAbility.Piercing)End=BeamEnd(Origin,Aim);
            else if(Ability==BossAbility.Sweep)
            {
                // The support band and its scan direction were chosen at start, never after a reaction.
                Aim=(sweepFrom-(centre+Vector2.up*.28f)).normalized;
                Origin=centre+Vector2.up*.28f;
                sweepStart=Mathf.Atan2(sweepFrom.y-Origin.y,sweepFrom.x-Origin.x)*Mathf.Rad2Deg;
                sweepEnd=sweepStart+Mathf.DeltaAngle(sweepStart,Mathf.Atan2(sweepTo.y-Origin.y,sweepTo.x-Origin.x)*Mathf.Rad2Deg);
                End=sweepTo;
            }
            else if(Ability==BossAbility.DropCut)
            {Origin=centre;Aim=Vector2.down;End=centre+Aim*settings.Range;}
            activeStarts=phaseUntil+LockSeconds;activeEnds=activeStarts+(Ability==BossAbility.Piercing?.0001f:ActiveSeconds);
        }
        Vector2 BeamEnd(Vector2 origin,Vector2 direction)
        {
            var bounds=actor.Navigation.ArenaBounds;
            float distance=settings.Range;
            if(Mathf.Abs(direction.x)>.001f)distance=Mathf.Min(distance,((direction.x>0?bounds.max.x:bounds.min.x)-origin.x)/direction.x);
            if(Mathf.Abs(direction.y)>.001f)distance=Mathf.Min(distance,((direction.y>0?bounds.max.y:bounds.min.y)-origin.y)/direction.y);
            if(actor.Coordinator!=null)
                foreach(var other in actor.Coordinator.Actors)
                    if(other!=actor&&other.IsAlive)
                    {
                        var hits=Physics2D.RaycastAll(origin,direction,Mathf.Max(0,distance),1<<other.gameObject.layer);
                        foreach(var hit in hits)if(hit.collider==other.Body)distance=Mathf.Min(distance,hit.distance);
                    }
            return origin+direction*Mathf.Max(0,distance);
        }
        void Activate()
        {
            if(JumpAction && !actor.Motor.StartCommittedJump(jumpPlan.Route,jumpPlan.JumpMotion)){Recover();return;}
            SetPhase(BossActionPhase.Active,Ability==BossAbility.Camo?p.CamoDuration:ActiveSeconds);
            Activated[(int)Ability]++;LastActiveAt=Time.time;hitInWindow=false;activeStarts=Time.time;
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
        void TickJump()
        {
            if(!actor.Motor.Grounded && actor.Motor.JumpStartedAt>=0 && !jumpAirborne)
            {
                jumpAirborne=true;slashAt=actor.Motor.JumpStartedAt+jumpPlan.SlashOffset;
                if(BurstAction)nextShot=Time.time+p.BurstShotLock;
            }
            if(jumpAirborne && actor.Motor.Grounded || Time.time>jumpDeadline || actor.Motor.RouteFailed)
            {
                if(slashStarted&&!slashFinished)SweepBlade(CutProgress);
                if(jumpAirborne&&actor.Motor.Grounded&&!actor.Motor.RouteFailed
                    &&Vector2.Distance(actor.Position,jumpPlan.Destination)<.4f&&TryJumpFollowup())return;
                Recover();return;
            }
            if(!jumpAirborne)return;
            if(Ability==BossAbility.FlankLeap)return; // Movement alone never deals damage.
            if(BurstAction)
            {
                if(shotsRemaining>0 && !bulletLocked)
                {
                    if(!actor.Tactics.TryGunTarget(BossAbility.Burst,actor.Position,actor.Perception.Facts,out var point,AimDelay))return;
                    targetPoint=point;UpdateGunAim();
                    if(Time.time>=nextShot-p.BurstShotLock){bulletLocked=true;nextShot=Mathf.Max(nextShot,Time.time+p.BurstShotLock);actor.Face(Aim);}
                }
                if(shotsRemaining>0&&bulletLocked&&Time.time>=nextShot)FireBullet();
            }
            else
            {
                if(!slashStarted && Time.time>=slashAt)
                {
                    slashStarted=true;previousBladeOrigin=actor.Position;previousCutProgress=0;
                    actor.Sounds?.Play(Sfx.BladeHeavy,actor.ActorId,AttackId);
                }
                if(slashStarted&&!slashFinished&&Time.time>=slashAt+p.LeapSlash.Active)
                {SweepBlade(1);slashFinished=true;}
            }
        }
        void UpdateGunAim()
        {
            Aim=(targetPoint-(actor.Position+Vector2.up*.28f)).normalized;
            Origin=actor.Position+Vector2.up*.28f+Aim*.52f;
            End=Origin+Aim*(BurstAction?p.Burst.Range:settings.Range);
        }
        void FireBullet()
        {
            if(Ammo<=0||shotsRemaining<=0)return;
            Vector2 direction=Aim;
            if(Ability==BossAbility.Sweep)
            {
                Vector2 point=Vector2.Lerp(sweepFrom,sweepTo,shotIndex/(float)(p.SweepShots-1));
                direction=(point-(actor.Position+Vector2.up*.28f)).normalized;Aim=direction;
            }
            actor.Face(direction);
            Vector2 muzzle=actor.Position+Vector2.up*.28f+direction*.52f;
            if(!actor.Tactics.AllyBlocks(actor.Position+Vector2.up*.28f,muzzle))
            {
                var shot=Instantiate(actor.ProjectilePrefab,muzzle,Quaternion.identity);
                shot.Launch(direction,p.BulletSpeed,BurstAction?p.Burst.Damage:settings.Damage,actor.gameObject);
                var sprite=shot.GetComponent<SpriteRenderer>();if(sprite!=null)sprite.color=actor.Profile.Kind==BossKind.Refraction?NeuralPalette.Refraction:NeuralPalette.Severance;
                actor.Sounds?.Play(Sfx.BossBurst,actor.ActorId,AttackId,shotIndex);actor.View?.Muzzle(muzzle,direction);
            }
            Ammo--;shotsRemaining--;shotIndex++;bulletLocked=false;
            nextShot=Time.time+(BurstAction?(StaggeredBurst&&shotIndex==1?p.BurstStaggerPause:p.BurstInterval):p.SweepInterval);
            if(shotsRemaining==0){phaseUntil=Time.time+(BurstAction?p.BurstTail:p.SweepTail);activeEnds=phaseUntil;}
            if(Ammo==0)RequestReload();
        }
        void FixedUpdate()
        {
            if(actor==null||!actor.Running||Phase!=BossActionPhase.Active||Time.timeScale<=0)return;
            if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                HitSegment(actor.Motor.PreviousPosition,actor.Position,p.BladeRadius+.2f,settings.Damage,true);
            else if(Ability==BossAbility.LeapSlash){if(slashStarted&&!slashFinished)SweepBlade(CutProgress);}
            else if(UsesBladeArc)SweepBlade(PhaseProgress);
            else if(Ability==BossAbility.DropCut)SweepDrop();
        }
        void SweepBlade(float progress)
        {
            // Subdivide the physical step by angular travel, including the swept tip and translated root.
            int samples=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(progress-previousCutProgress)
                *Mathf.Abs(p.RhythmArcTo[RhythmIndex]-p.RhythmArcFrom[RhythmIndex])/4));
            Vector2 lastRoot=previousBladeOrigin,lastTip=lastRoot+BladeDirection(previousCutProgress)*BladeReach;
            for(int i=1;i<=samples;i++)
            {
                float t=i/(float)samples,cut=Mathf.Lerp(previousCutProgress,progress,t);
                Vector2 root=Vector2.Lerp(previousBladeOrigin,actor.Position,t),tip=root+BladeDirection(cut)*BladeReach;
                int damage=RhythmIndex==2?p.RhythmThirdDamage:settings.Damage;
                HitSegment(root,tip,p.BladeRadius,damage,true);
                if(actor.Player!=null&&!Projectile.TerrainHit(root,actor.Player.GetComponent<Collider2D>().ClosestPoint(tip),.015f,out _))
                    HitSegment(lastTip,tip,p.BladeRadius,damage,true);
                lastRoot=root;lastTip=tip;
            }
            Origin=actor.Position;Aim=BladeDirection(progress);End=Origin+Aim*BladeReach;
            previousBladeOrigin=Origin;previousCutProgress=progress;
        }
        void SweepDrop()
        {
            Vector2 now=actor.Position;
            for(int i=0;i<=4;i++)
            {
                Vector2 offset=Vector2.down*(settings.Range*i/4f);
                // A translated blade tip may already lie below a platform. It cannot originate damage there.
                if(actor.Player!=null&&!Projectile.TerrainHit(now,actor.Player.GetComponent<Collider2D>().ClosestPoint(now+offset),.015f,out _))
                    HitSegment(previousBladeOrigin+offset,now+offset,p.BladeRadius,settings.Damage,true);
            }
            HitSegment(now,now+Vector2.down*settings.Range,p.BladeRadius,settings.Damage,true);
            Origin=now;Aim=Vector2.down;End=now+Aim*settings.Range;previousBladeOrigin=now;
        }
        void HitSegment(Vector2 from,Vector2 to,float radius,int damage,bool terrain)
        {
            if(hitInWindow||actor.Player==null||actor.Player.IsDead)return;
            var body=actor.Player.GetComponent<Collider2D>();
            Vector2 point=body.ClosestPoint(to);Vector2 d=to-from;
            float t=d.sqrMagnitude>.001f?Mathf.Clamp01(Vector2.Dot((Vector2)body.bounds.center-from,d)/d.sqrMagnitude):0;
            Vector2 nearest=from+d*t;point=body.ClosestPoint(nearest);
            if(Vector2.Distance(point,nearest)>radius)return;
            if(terrain&&Projectile.TerrainHit(from,point,.015f,out _))return;
            hitInWindow=true;int before=actor.Player.CurrentHealth;
            actor.Player.TakeDamage(damage,actor.gameObject);
            if(actor.Player.CurrentHealth<before)
            {
                ConnectedHits++;actor.View?.Impact(point);
                if(Ability==BossAbility.DropCut&&!actor.Player.IsDead)
                {
                    float side=body.bounds.center.x-targetPoint.x;
                    actor.Player.GetComponent<PlayerMovement>()?.ApplyHitPush(Mathf.Abs(side)>.05f?Mathf.Sign(side):actor.Facing,
                        actor.Navigation.PlatformWidth*.35f,.14f);
                }
            }
        }
        void NextRhythm()
        {
            if(SingleCut){Recover();return;}
            var observed=actor.Perception.Facts;int next=RhythmIndex+1;
            if(next>=3||observed.Confidence<.2f
                || Vector2.Distance(observed.Position,actor.Position)>p.RhythmReach[next]+p.RhythmStep[next]+observed.Body.extents.x
                || Mathf.Abs(observed.Position.y-actor.Position.y)>p.RhythmReach[next]+observed.Body.extents.y)
            {Recover();return;}
            RhythmIndex=next;targetPoint=observed.Position;
            actor.Motor.BeginStep(p.RhythmStep[next]);
            SetPhase(BossActionPhase.Prepare,p.RhythmPrepare[next]);UpdateGeometry();
        }
        void FinishDash()
        {
            // A body/wall sweep can stop the motor during its last physics step. Resolve that
            // completed displacement before Update leaves Active; the next FixedUpdate is too late.
            HitSegment(actor.Motor.PreviousPosition,actor.Position,p.BladeRadius+.2f,settings.Damage,true);
            End=actor.Position;PublicThreats.Clear();
            if(TryDashFollowup())return;
            returnWindowUntil=Time.time+p.ReturnBranchWindow;Recover();
        }
        bool TryJumpFollowup()
        {
            var facts=actor.Perception.Facts;
            if(Ability!=BossAbility.LeapSlash&&Ability!=BossAbility.FlankLeap)return false;
            var next=Ability==BossAbility.FlankLeap?BossAbility.FlankCut:BossAbility.LeapFollowup;
            if(comboTarget||hasReturned||!facts.Visible||facts.Confidence<.3f||!Ready(BossAbility.Rhythm)
                ||!actor.Tactics.LegalAt(BossAbility.Rhythm,actor.Position,facts,out _)
                ||actor.Coordinator!=null&&!actor.Coordinator.TryReserve(actor,next,false))return false;
            hasReturned=true;Ability=next;settings=p.Rhythm;RhythmIndex=0;
            AttackId++;Started[(int)Ability]++;targetPoint=facts.Position;cutFacing=Mathf.Sign(targetPoint.x-actor.Position.x);
            actor.Face(Vector2.right*cutFacing);actor.Motor.Brake();
            readyAt[(int)BossAbility.Rhythm]=Time.time+p.Rhythm.Cooldown;
            actor.Motor.BeginStep(p.RhythmStep[0]);
            // Arrival has its own full tell; no damage on the frame a flank lands.
            SetPhase(BossActionPhase.Prepare,next==BossAbility.FlankCut?.24f:p.RhythmPrepare[0]);
            actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,AttackId);
            UpdateGeometry();Publish();actor.Trace("prepare "+Ability+" attack="+AttackId);return true;
        }
        bool TryDropFollowup()
        {
            var facts=actor.Perception.Facts;
            if(comboTarget||hasReturned||!actor.Motor.Grounded||!facts.Visible||facts.Confidence<.3f
                ||!Ready(BossAbility.Rhythm)||Mathf.Sign(facts.Position.x-actor.Position.x)!=actor.Facing
                ||!actor.Tactics.LegalAt(BossAbility.Rhythm,actor.Position,facts,out _)
                ||actor.Coordinator!=null&&!actor.Coordinator.TryReserve(actor,BossAbility.DropFollowup,false))return false;
            hasReturned=true;Ability=BossAbility.DropFollowup;settings=p.Rhythm;RhythmIndex=0;
            AttackId++;Started[(int)Ability]++;targetPoint=facts.Position;cutFacing=actor.Facing;
            readyAt[(int)BossAbility.Rhythm]=Time.time+p.Rhythm.Cooldown;
            actor.Motor.BeginStep(p.RhythmStep[0]);SetPhase(BossActionPhase.Prepare,p.RhythmPrepare[0]);
            UpdateGeometry();Publish();actor.Trace("prepare DropFollowup attack="+AttackId);return true;
        }
        bool TryDashFollowup()
        {
            if(TryReturn())return true;
            var facts=actor.Perception.Facts;
            if(Ability!=BossAbility.DrawDash||comboTarget||hasReturned||!actor.Motor.Grounded||facts.Confidence<.3f
                ||!Ready(BossAbility.Rhythm)
                || Mathf.Sign(facts.Position.x-actor.Position.x)!=Mathf.Sign(Aim.x)
                || !actor.Tactics.LegalAt(BossAbility.Rhythm,actor.Position,facts,out _)
                ||actor.Coordinator!=null&&!actor.Coordinator.TryReserve(actor,BossAbility.DashFollowup,false))return false;
            hasReturned=true;Ability=BossAbility.DashFollowup;settings=p.Rhythm;RhythmIndex=0;
            AttackId++;Started[(int)Ability]++;targetPoint=facts.Position;cutFacing=Mathf.Sign(Aim.x);
            readyAt[(int)BossAbility.Rhythm]=Time.time+p.Rhythm.Cooldown;
            actor.Motor.BeginStep(p.RhythmStep[0]);SetPhase(BossActionPhase.Prepare,p.RhythmPrepare[0]);
            UpdateGeometry();Publish();actor.Trace("prepare DashFollowup attack="+AttackId);return true;
        }
        bool TryReturn()
        {
            var facts=actor.Perception.Facts;
            Vector2 opportunity=ObservedReturnPoint(facts);
            if(Ability==BossAbility.DrawDash&&!comboTarget&&!hasReturned&&Ready(BossAbility.ReturnCut)&&facts.Confidence>.3f&&Mathf.Sign(opportunity.x-actor.Position.x)!=Mathf.Sign(Aim.x)
                &&Mathf.Abs(opportunity.y-actor.Position.y)<.85f&&Mathf.Abs(opportunity.x-actor.Position.x)<p.ReturnCut.Range)
            {
                Vector2 to=actor.Position+Vector2.right*Mathf.Sign(opportunity.x-actor.Position.x)*Mathf.Min(p.ReturnCut.Range,Mathf.Abs(opportunity.x-actor.Position.x)+.6f);
                if(actor.Navigation.TryDash(actor.Position,to,actor.Motor.Size,out var safe)&&Vector2.Distance(safe,actor.Position)>.75f)
                {
                    hasReturned=true;Ability=BossAbility.ReturnCut;settings=p.ReturnCut;AttackId++;Started[(int)Ability]++;
                    readyAt[(int)Ability]=Time.time+settings.Cooldown;targetPoint=opportunity;comboTarget=false;
                    actor.Motor.Brake();UpdateGeometry();actor.Face(Aim);SetPhase(BossActionPhase.Prepare,settings.Prepare);actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,AttackId);return true;
                }
            }
            return false;
        }
        Vector2 ObservedReturnPoint(BossObservation facts)
        {
            // A visibly descending body is a commitment. Predict only that expressed motion
            // through real terrain during the full second tell, never a new player input.
            if(facts.Velocity.y>=-.5f||facts.Position.y<=actor.Position.y+.85f)return facts.Position;
            Vector2 point=facts.Position,velocity=facts.Velocity,size=facts.Body.size;
            float horizon=Mathf.Min(.6f,p.ReturnCut.Prepare+p.ReturnCut.Lock+Mathf.Max(0,Time.time-facts.ObservedAt));
            float dt=Time.fixedDeltaTime;
            for(float t=0;t<horizon;t+=dt)
            {
                velocity.y+=Physics2D.gravity.y*3*dt; // Retained public player gravity.
                Vector2 next=point+velocity*dt;
                if(!actor.Navigation.SweepTraversal(point,next,size,out var hit,out var safe))
                {
                    point=safe;
                    if(hit.normal.y>.6f&&velocity.y<0)return point;
                    return facts.Position;
                }
                point=next;
            }
            return point;
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
            actor.Motor.Brake();PublicThreats.Clear();SetPhase(BossActionPhase.Recovery,settings.Recovery);
            RecoveryStepping=false;
            if(SafeRecovery&&!comboTarget&&actor.Tactics.TrySwordRecoveryStep(out float step))
            {
                RecoveryStepping=true;RecoverySteps++;
                actor.Motor.BeginStep(Mathf.Abs(step));actor.Motor.SetMove(Mathf.Sign(step),.65f);
            }
        }
        public void AbortPreparation()
        { if(Phase==BossActionPhase.Prepare){actor.Trace("cancel "+Ability);actor.Sounds?.StopAttack(actor.ActorId,AttackId);Recover();} }
        void Finish()
        {
            if(RecoveryStepping){actor.Motor.Brake();RecoveryStepping=false;}
            if(UsesBladeArc||Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut||Ability==BossAbility.DropCut)
                ConsecutiveMeleeMisses=ConnectedHits>hitsAtStart?0:ConsecutiveMeleeMisses+1;
            jumpPlan=null;
            actor.Coordinator?.Release(actor);Ability=BossAbility.None;Phase=BossActionPhase.Idle;PublicThreats.Clear();
        }
        public void StopCombat()
        {
            if(RecoveryStepping){actor.Motor.Brake();RecoveryStepping=false;}
            if(guardZone!=null)guardZone.enabled=false;reloadUntil=0;PublicThreats.Clear();jumpPlan=null;
            actor?.Coordinator?.Release(actor);Ability=BossAbility.None;Phase=BossActionPhase.Idle;
        }
        // Ordinary hits still damage health, but do not cancel G3/G4 preparation.
        public void OnDamaged(GameObject source) { }
        public bool GuardBlocksRay(Vector2 origin,Vector2 point)
        {
            if(Ability!=BossAbility.Guard||Phase!=BossActionPhase.Active||Guarded>=p.GuardCapacity)return false;
            Vector2 outward=(origin-actor.Position).normalized;
            return Vector2.Dot(outward,Aim)>Mathf.Cos(p.GuardArc*.5f*Mathf.Deg2Rad);
        }
        public bool ReceiveProjectile(Projectile shot,Collider2D contact)
        {
            if(shot.OwnerLayer==gameObject.layer||!GuardBlocksRay(shot.transform.position,actor.Position))return false;
            var velocity=shot.GetComponent<Rigidbody2D>().linearVelocity;
            if(Vector2.Dot(velocity,Aim)>=0 || Vector2.Distance(shot.transform.position,actor.Position)>p.Guard.Range+shot.Radius)return false;
            Guarded++;actor.Sounds?.Play(Sfx.GuardBlock,actor.ActorId,AttackId,Guarded);actor.View?.Impact(shot.transform.position);
            if(Guarded>=p.GuardCapacity)Recover();return true;
        }
        void Publish()
        {
            PublicThreats.Clear();
            if(!Busy||Phase==BossActionPhase.Recovery||Ability==BossAbility.Camo||Ability==BossAbility.Guard||Ability==BossAbility.FlankLeap)return;
            if(Ability==BossAbility.LeapSlash){PublishLeap();return;}
            var phase=Phase==BossActionPhase.Prepare?BossThreatPhase.Preparation:Phase==BossActionPhase.Locked?BossThreatPhase.Locked:BossThreatPhase.Active;
            int count=Ability==BossAbility.Sweep?p.SweepShots:1;
            if(BurstAction&&Phase==BossActionPhase.Active&&shotsRemaining==0)return;
            for(int i=0;i<count;i++)
            {
                Vector2 origin=Origin,end=End,direction=Aim;
                BossThreatShape shape=BossThreatShape.SweptCapsule;
                float radius=p.BladeRadius,start=activeStarts,finish=activeEnds,speed=0;
                bool terrain=true;
                if(Ability==BossAbility.Piercing){shape=BossThreatShape.Ray;radius=p.BeamRadius;terrain=false;}
                else if(BurstAction||Ability==BossAbility.Sweep)
                {
                    shape=BossThreatShape.Projectile;radius=actor.ProjectilePrefab.Radius;speed=p.BulletSpeed;terrain=false;
                    if(BurstAction)
                    {
                        // Only the current round has an expressed direction. Later rounds have not been selected.
                        origin=actor.Position+Vector2.up*.28f+Aim*.52f;
                        if(Phase==BossActionPhase.Active){start=nextShot;phase=bulletLocked?BossThreatPhase.Locked:BossThreatPhase.Preparation;}
                    }
                    else
                    {
                        if(Phase==BossActionPhase.Active&&i<shotIndex)continue;
                        direction=(Vector2.Lerp(sweepFrom,sweepTo,i/(float)(p.SweepShots-1))-(actor.Position+Vector2.up*.28f)).normalized;
                        origin=actor.Position+Vector2.up*.28f+direction*.52f;
                        start=Phase==BossActionPhase.Active?nextShot+(i-shotIndex)*p.SweepInterval:activeStarts+i*p.SweepInterval;
                    }
                    float range=BurstAction?p.Burst.Range:settings.Range;
                    end=origin+direction*range;finish=start+range/speed;
                }
                else if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                {radius=p.BladeRadius+.2f;finish=start+Vector2.Distance(origin,end)/(Ability==BossAbility.DrawDash?p.DashSpeed:p.ReturnSpeed);}
                else if(UsesBladeArc){shape=BossThreatShape.BladeArc;origin=actor.Position;}
                else if(Ability==BossAbility.DropCut)
                {
                    shape=BossThreatShape.FallingBlade;
                    // Gravity prediction uses the same one-time horizontal request, never a homing endpoint.
                    float before=Phase==BossActionPhase.Active?0:Mathf.Max(0,start-Time.time);
                    actor.Tactics.PredictDropActivation(before,out origin);
                    float fall=(Mathf.Sqrt(p.DropSpeed*p.DropSpeed+2*(-Physics2D.gravity.y*p.Motion.GravityScale)*Mathf.Max(.01f,origin.y-targetPoint.y))-p.DropSpeed)/(-Physics2D.gravity.y*p.Motion.GravityScale);
                    direction=Phase==BossActionPhase.Active?actor.Velocity:new Vector2(Mathf.Clamp((targetPoint.x-origin.x)/Mathf.Max(.02f,fall),-p.Motion.Speed,p.Motion.Speed),-p.DropSpeed);
                    if(Phase==BossActionPhase.Active)start=Time.time;
                    finish=start+fall+Time.fixedDeltaTime;
                }
                PublicThreats.Add(new BossThreat {ActorId=actor.ActorId,AttackId=AttackId,Phase=phase,Shape=shape,Origin=origin,End=end,Direction=direction,
                    Radius=radius,StartsAt=start,EndsAt=finish,ProjectileSpeed=speed,TerrainBlocked=terrain,Valid=true,
                    HasLandingX=Ability==BossAbility.DropCut,LandingX=targetPoint.x,
                    ArcFrom=p.RhythmArcFrom[RhythmIndex],ArcTo=p.RhythmArcTo[RhythmIndex],Facing=cutFacing,Reach=BladeReach,Gravity=Physics2D.gravity.y*p.Motion.GravityScale});
            }
        }
        void PublishLeap()
        {
            if(jumpPlan==null||Phase==BossActionPhase.Active&&slashFinished)return;
            bool flying=Phase==BossActionPhase.Active&&jumpAirborne;
            float launch=flying?actor.Motor.JumpStartedAt:Phase==BossActionPhase.Active?Time.time+Time.fixedDeltaTime
                :phaseUntil+(Phase==BossActionPhase.Prepare?LockSeconds:0);
            float start=flying?slashAt:launch+jumpPlan.SlashOffset,end=start+p.LeapSlash.Active;
            float pathStart=flying?Time.time:launch;
            Vector2 velocity=flying?actor.Velocity:Vector2.up*p.Motion.JumpSpeed;
            var path=actor.Navigation.PredictAirPath(actor.Position,velocity,jumpPlan.Destination,p.Motion,actor.Motor.Size,Mathf.Max(0,end-pathStart));
            var threat=new BossThreat { ActorId=actor.ActorId,AttackId=AttackId,Valid=true,
                Shape=BossThreatShape.BladeArc,Phase=Time.time>=start?BossThreatPhase.Active:
                    Phase==BossActionPhase.Prepare?BossThreatPhase.Preparation:BossThreatPhase.Locked,
                StartsAt=start,EndsAt=end,Origin=actor.Position,Direction=BladeDirection(0),Radius=p.BladeRadius,
                ArcFrom=p.RhythmArcFrom[0],ArcTo=p.RhythmArcTo[0],Facing=cutFacing,Reach=BladeReach,TerrainBlocked=true,
                OriginPath=path,PathStartsAt=pathStart,PathStep=Time.fixedDeltaTime };
            threat.Origin=threat.OriginAt(start);threat.End=threat.Origin+BladeDirection(0)*BladeReach;
            PublicThreats.Add(threat);
        }
    }
}
