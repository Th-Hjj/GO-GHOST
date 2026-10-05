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
        public int Ammo {get;private set;}
        public bool Reloading=>reloadUntil>0;
        public bool Busy=>Ability!=BossAbility.None;
        public bool Camouflaged=>Ability==BossAbility.Camo&&Phase!=BossActionPhase.Recovery;
        public bool Committed=>Phase==BossActionPhase.Locked||Phase==BossActionPhase.Active||Phase==BossActionPhase.Recovery;
        public bool HighThreat=>Ability==BossAbility.Piercing||Ability==BossAbility.Sweep||Ability==BossAbility.Rhythm||Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut||Ability==BossAbility.DropCut;
        public float PhaseProgress=>Mathf.Clamp01((Time.time-phaseAt)/Mathf.Max(.001f,phaseUntil-phaseAt));
        public Vector2 Aim {get;private set;}=Vector2.left;
        public Vector2 Origin {get;private set;}
        public Vector2 End {get;private set;}
        public int RhythmIndex {get;private set;}
        public readonly List<BossThreat> PublicThreats=new List<BossThreat>(6);
        public readonly int[] Started=new int[12],Activated=new int[12];
        public int Guarded {get;private set;}
        public int BeamAllyBlocks {get;private set;}
        public int Interruptions {get;private set;}
        public bool InterruptedCurrent {get;private set;}
        public float LastActiveAt {get;private set;}
        public int ConnectedHits {get;private set;}
        public int ConsecutiveMeleeMisses {get;private set;}
        BossAgent actor;
        BossProfile p;
        BossAbilitySettings settings;
        readonly float[] readyAt=new float[12];
        float phaseAt,phaseUntil,activeStarts,activeEnds,reloadUntil,nextShot,sweepStart,sweepEnd,lastCamoEnded=-100,returnWindowUntil;
        int shotsRemaining,shotIndex,prepareHits,hitsAtStart;
        bool hitInWindow,comboTarget,hasReturned;
        Vector2 targetPoint,dashStart;
        CircleCollider2D guardZone;
        public void Configure(BossAgent owner)
        {
            actor=owner;p=owner.Profile;Ammo=p.Magazine;
            foreach(var c in GetComponentsInChildren<CircleCollider2D>())if(c.isTrigger){guardZone=c;guardZone.enabled=false;}
        }
        public bool Ready(BossAbility ability)=>Time.time>=readyAt[(int)ability] && !(ability==BossAbility.Camo&&Time.time<lastCamoEnded+1.2f);
        public bool CanAfford(BossAbility ability)=>ability==BossAbility.Burst?Ammo>0:Ammo>=p.Settings(ability).Cost;
        public void RequestReload()
        {
            if(reloadUntil>0||Ammo==p.Magazine)return;
            reloadUntil=Time.time+p.ReloadSeconds;actor.Sounds?.Play(Sfx.Reload,actor.ActorId);
            actor.Trace("reload begin");
        }
        public bool TryStart(BossAbility ability,Vector2 hypothesis,bool cooperative=false)
        {
            if(!actor.Running||!actor.IsAlive||Busy||!Ready(ability))return false;
            if(ability!=BossAbility.Camo&&p.Kind==BossKind.Refraction&&(!actor.Motor.Grounded||Reloading))return false;
            if(ability==BossAbility.ReturnCut)return false;
            if(!CanAfford(ability)){RequestReload();return false;}
            if((ability==BossAbility.DrawDash||ability==BossAbility.Guard||ability==BossAbility.Rhythm)&&!actor.Motor.Grounded)return false;
            if(ability==BossAbility.DropCut&&(actor.Motor.Grounded||actor.Velocity.y<-.5f))return false;
            if(IsHigh(ability)&&actor.Coordinator!=null&&!actor.Coordinator.TryReserve(actor,ability,cooperative))return false;
            Ability=ability;settings=p.Settings(ability);AttackId++;Started[(int)ability]++;
            hitsAtStart=ConnectedHits;
            targetPoint=hypothesis;comboTarget=cooperative;RhythmIndex=0;prepareHits=0;hasReturned=false;Guarded=0;InterruptedCurrent=false;
            readyAt[(int)ability]=Time.time+settings.Cooldown;
            Aim=(hypothesis-actor.Position).normalized;if(Aim.sqrMagnitude<.1f)Aim=Vector2.right*actor.Facing;
            actor.Face(Aim);SetPhase(BossActionPhase.Prepare,settings.Prepare);
            if(ability!=BossAbility.Camo&&ability!=BossAbility.Burst)actor.Motor.Brake();
            var prepareCue=PrepareCue(ability);
            if(prepareCue.HasValue)actor.Sounds?.Play(prepareCue.Value,actor.ActorId,AttackId);
            actor.Perception.ExpressedPressure(actor.Position);
            actor.Trace($"prepare {ability} attack={AttackId}");
            UpdateGeometry();Publish();return true;
        }
        static bool IsHigh(BossAbility a)=>a==BossAbility.Piercing||a==BossAbility.Sweep||a==BossAbility.Rhythm||a==BossAbility.DrawDash||a==BossAbility.ReturnCut||a==BossAbility.DropCut;
        Sfx? PrepareCue(BossAbility ability)
        {
            switch(ability){case BossAbility.Piercing:return Sfx.BeamCharge;case BossAbility.Camo:return Sfx.CamoEnter;
                case BossAbility.DrawDash:return Sfx.DrawPrepare;case BossAbility.ReturnCut:return Sfx.ReturnPrepare;
                default:return null;}
        }
        void SetPhase(BossActionPhase phase,float seconds)
        {Phase=phase;phaseAt=Time.time;phaseUntil=Time.time+Mathf.Max(0,seconds);}
        void Update()
        {
            if(actor==null||!actor.Running||Time.deltaTime<=0)return;
            if(reloadUntil>0&&Time.time>=reloadUntil){reloadUntil=0;Ammo=p.Magazine;actor.Trace("reload complete");}
            if(!Busy)return;
            if(Phase==BossActionPhase.Prepare)
            {
                if(!comboTarget && actor.Perception.Facts.Confidence>.1f)
                {
                    if(p.Kind==BossKind.Refraction && Ability!=BossAbility.Camo)
                    {
                        if(actor.Tactics.TryGunTarget(Ability,actor.Position,actor.Perception.Facts,out var shotPoint)) targetPoint=shotPoint;
                        else { AbortPreparation(); return; }
                    }
                    else targetPoint=actor.Tactics.PredictBody(actor.Perception.Facts,.16f);
                }
                UpdateGeometry();
                if(Time.time>=phaseUntil)Lock();
            }
            else if(Phase==BossActionPhase.Locked&&Time.time>=phaseUntil)Activate();
            else if(Phase==BossActionPhase.Active)
            {
                if(Ability==BossAbility.Burst&&!actor.Motor.Grounded){Recover();}
                else if(Ability==BossAbility.Burst||Ability==BossAbility.Sweep)
                {
                    if(shotsRemaining>0&&Time.time>=nextShot)FireBullet();
                    if(shotsRemaining==0&&Time.time>=phaseUntil)Recover();
                }
                else if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                {if(!actor.Motor.DashActive&&Time.time-phaseAt>.025f)FinishDash();}
                else if(Ability==BossAbility.DropCut)
                {if(actor.Motor.Grounded||Time.time>=phaseUntil)Recover();}
                else if(Time.time>=phaseUntil)
                {
                    if(Ability==BossAbility.Rhythm)NextRhythm();else Recover();
                }
            }
            else if(Phase==BossActionPhase.Recovery)
            {
                if(Ability==BossAbility.DrawDash&&Time.time<=returnWindowUntil&&TryReturn())return;
                if(Time.time>=phaseUntil)Finish();
            }
            if(Ability==BossAbility.Guard&&Phase==BossActionPhase.Active)actor.Motor.SetMove(actor.Facing,p.GuardWalkScale);
            if(Ability==BossAbility.Burst&&Phase!=BossActionPhase.Recovery&&actor.Motor.Grounded)
            {
                var observed=actor.Perception.Facts;
                float direction=Mathf.Abs(observed.Position.x-actor.Position.x)<p.DesiredDistance*.6f?-Mathf.Sign(observed.Position.x-actor.Position.x):0;
                if(direction!=0&&!actor.Navigation.CanStand(actor.Position+Vector2.right*direction*.45f,actor.Motor.Size))direction=0;
                actor.Motor.SetMove(direction,.5f);
            }
            if(Camouflaged&&Ammo==0)RequestReload();
            Publish();
        }
        void Lock()
        {
            UpdateGeometry();
            if((Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)&&Vector2.Distance(Origin,End)<.65f){Recover();return;}
            if(Ability==BossAbility.DropCut&&actor.Motor.Grounded){Recover();return;}
            if(Ability==BossAbility.Guard)Aim=Vector2.right*Mathf.Sign(Aim.x);
            actor.Face(Aim);SetPhase(BossActionPhase.Locked,settings.Lock);
            if(Ability==BossAbility.Piercing)actor.Sounds?.Play(Sfx.BeamLock,actor.ActorId,AttackId);
            activeStarts=Time.time+settings.Lock;activeEnds=activeStarts+(Ability==BossAbility.Piercing?.0001f:settings.Active);
            actor.Trace($"lock {Ability} origin={Origin} end={End}");
            if(settings.Lock<=0)Activate();
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
                float distance=Mathf.Min(settings.Range,Mathf.Abs(targetPoint.x-centre.x)+.75f);
                actor.Navigation.TryDash(centre,centre+Aim*distance,actor.Motor.Size,out var safeEnd);End=safeEnd;
            }
            else if(Ability==BossAbility.Rhythm)
            {
                // Each cut may cover a visible airborne body, with a finite arc locked before impact.
                Vector2 delta=targetPoint-centre;
                float facing=Mathf.Abs(delta.x)>.05f?Mathf.Sign(delta.x):actor.Facing;
                float angle=Mathf.Clamp(Mathf.Atan2(delta.y,Mathf.Abs(delta.x))*Mathf.Rad2Deg,-55,55)*Mathf.Deg2Rad;
                Aim=new Vector2(facing*Mathf.Cos(angle),Mathf.Sin(angle));Origin=centre;End=centre+Aim*settings.Range;
            }
            else if(Ability==BossAbility.Piercing)End=BeamEnd(Origin,Aim);
            else if(Ability==BossAbility.Sweep)
            {
                Vector2 movement=actor.Perception.Facts.Velocity;
                float side=actor.Perception.LearnedRetreat!=0?actor.Perception.LearnedRetreat:Mathf.Abs(movement.x)>.5f?Mathf.Sign(movement.x):Mathf.Sign(targetPoint.x-centre.x);
                Vector2 a=targetPoint+Vector2.left*.6f,b=targetPoint+Vector2.right*(side*2.6f);
                sweepStart=Mathf.Atan2(a.y-Origin.y,a.x-Origin.x)*Mathf.Rad2Deg;
                sweepEnd=sweepStart+Mathf.DeltaAngle(sweepStart,Mathf.Atan2(b.y-Origin.y,b.x-Origin.x)*Mathf.Rad2Deg);
            }
            else if(Ability==BossAbility.DropCut)
            {Origin=centre;Aim=new Vector2(actor.Velocity.x,-Mathf.Max(5,Mathf.Abs(actor.Velocity.y))).normalized;End=centre+Aim*settings.Range;}
            activeStarts=phaseUntil+settings.Lock;activeEnds=activeStarts+(Ability==BossAbility.Piercing?.0001f:settings.Active);
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
            SetPhase(BossActionPhase.Active,Ability==BossAbility.Camo?p.CamoDuration:Ability==BossAbility.Rhythm&&RhythmIndex==2?p.RhythmThirdActive:settings.Active);
            Activated[(int)Ability]++;LastActiveAt=Time.time;hitInWindow=false;activeStarts=Time.time;
            activeEnds=Ability==BossAbility.Piercing?Time.time+.0001f:phaseUntil;actor.Trace($"active {Ability} attack={AttackId}");
            if(Ability==BossAbility.Burst||Ability==BossAbility.Sweep)
            {shotsRemaining=Ability==BossAbility.Burst?Mathf.Min(3,Ammo):6;shotIndex=0;nextShot=Time.time;FireBullet();}
            else if(Ability==BossAbility.Piercing)
            {
                Ammo-=settings.Cost;
                // A moving ally can still enter and block an already locked shot.
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
                actor.Sounds?.Play(Sfx.DashCut,actor.ActorId,AttackId);actor.View?.Slash(Aim,settings.Range,true);
            }
            else if(Ability==BossAbility.Rhythm||Ability==BossAbility.DropCut)
            {actor.Sounds?.Play(RhythmIndex==2||Ability==BossAbility.DropCut?Sfx.BladeHeavy:Sfx.BladeLight,actor.ActorId,AttackId,RhythmIndex);actor.View?.Slash(Aim,settings.Range,false);}
            else if(Ability==BossAbility.Guard&&guardZone!=null)guardZone.enabled=true;
            if(Ammo==0)RequestReload();Publish();
        }
        void FireBullet()
        {
            if(Ammo<=0||shotsRemaining<=0)return;
            Vector2 direction=Aim;
            if(Ability==BossAbility.Sweep)
            {float angle=Mathf.Lerp(sweepStart,sweepEnd,shotIndex/5f)*Mathf.Deg2Rad;direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));}
            Vector2 muzzle=actor.Position+Vector2.up*.28f+direction*.52f;
            // Check barrel as well: a muzzle cannot originate on the far side of cover.
            if(!Projectile.TerrainHit(actor.Position+Vector2.up*.28f,muzzle,.06f,out _))
            {
                var shot=Instantiate(actor.ProjectilePrefab,muzzle,Quaternion.identity);
                shot.Launch(direction,p.BulletSpeed,settings.Damage,actor.gameObject);
                var sprite=shot.GetComponent<SpriteRenderer>();if(sprite!=null)sprite.color=new Color(.33f,.84f,.42f);
                actor.Sounds?.Play(Sfx.BossBurst,actor.ActorId,AttackId,shotIndex);actor.View?.Muzzle(muzzle,direction);
            }
            Ammo--;shotsRemaining--;shotIndex++;nextShot=Time.time+(Ability==BossAbility.Burst?p.BurstInterval:p.SweepInterval);
            if(Ammo==0)RequestReload();
        }
        void FixedUpdate()
        {
            if(actor==null||!actor.Running||Phase!=BossActionPhase.Active||Time.timeScale<=0)return;
            if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                HitSegment(actor.Motor.PreviousPosition,actor.Position,p.BladeRadius+.2f,settings.Damage,true);
            else if(Ability==BossAbility.Rhythm)
            {Origin=actor.Position;End=Origin+Aim*settings.Range;HitSegment(Origin,End,p.BladeRadius,RhythmIndex==2?p.RhythmThirdDamage:settings.Damage,true);}
            else if(Ability==BossAbility.DropCut)
            {
                Origin=actor.Position;End=Origin+Aim*settings.Range;
                HitSegment(Origin,End,p.BladeRadius,settings.Damage,true);
            }
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
            if(actor.Player.CurrentHealth<before){ConnectedHits++;actor.View?.Impact(point);}
        }
        void NextRhythm()
        {
            var observed=actor.Perception.Facts;
            if(RhythmIndex>=2||observed.Confidence<.2f||Vector2.Distance(observed.Position,actor.Position)>settings.Range+actor.Body.bounds.extents.x){Recover();return;}
            RhythmIndex++;prepareHits=0;
            float gap=RhythmIndex==1?p.RhythmSecondGap:p.RhythmThirdGap;
            targetPoint=observed.Position;SetPhase(BossActionPhase.Prepare,Mathf.Max(0,gap-settings.Lock));
            activeStarts=phaseUntil+settings.Lock;activeEnds=activeStarts+(RhythmIndex==2?p.RhythmThirdActive:settings.Active);
        }
        void FinishDash()
        {
            // A body/wall sweep can stop the motor during its last physics step. Resolve that
            // completed displacement before Update leaves Active; the next FixedUpdate is too late.
            HitSegment(actor.Motor.PreviousPosition,actor.Position,p.BladeRadius+.2f,settings.Damage,true);
            End=actor.Position;PublicThreats.Clear();
            if(TryReturn())return;
            returnWindowUntil=Time.time+p.ReturnBranchWindow;Recover();
        }
        bool TryReturn()
        {
            var facts=actor.Perception.Facts;
            Vector2 opportunity=ObservedReturnPoint(facts);
            if(Ability==BossAbility.DrawDash&&!hasReturned&&Ready(BossAbility.ReturnCut)&&facts.Confidence>.3f&&Mathf.Sign(opportunity.x-actor.Position.x)!=Mathf.Sign(Aim.x)
                &&Mathf.Abs(opportunity.y-actor.Position.y)<.85f&&Mathf.Abs(opportunity.x-actor.Position.x)<p.ReturnCut.Range)
            {
                Vector2 to=actor.Position+Vector2.right*Mathf.Sign(opportunity.x-actor.Position.x)*Mathf.Min(p.ReturnCut.Range,Mathf.Abs(opportunity.x-actor.Position.x)+.6f);
                if(actor.Navigation.TryDash(actor.Position,to,actor.Motor.Size,out var safe)&&Vector2.Distance(safe,actor.Position)>.75f)
                {
                    hasReturned=true;Ability=BossAbility.ReturnCut;settings=p.ReturnCut;AttackId++;Started[(int)Ability]++;
                    readyAt[(int)Ability]=Time.time+settings.Cooldown;targetPoint=opportunity;comboTarget=false;
                    SetPhase(BossActionPhase.Prepare,settings.Prepare);actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,AttackId);return true;
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
            if(Ability==BossAbility.Camo){lastCamoEnded=Time.time;actor.Sounds?.Play(Sfx.CamoExit,actor.ActorId,AttackId);actor.PublishCue(.15f);}
            actor.Motor.Brake();PublicThreats.Clear();SetPhase(BossActionPhase.Recovery,settings.Recovery);
        }
        public void AbortPreparation()
        { if(Phase==BossActionPhase.Prepare){actor.Trace("cancel "+Ability);actor.Sounds?.StopAttack(actor.ActorId,AttackId);Recover();} }
        void Finish()
        {
            if(Ability==BossAbility.Rhythm||Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut||Ability==BossAbility.DropCut)
                ConsecutiveMeleeMisses=ConnectedHits>hitsAtStart?0:ConsecutiveMeleeMisses+1;
            actor.Coordinator?.Release(actor);Ability=BossAbility.None;Phase=BossActionPhase.Idle;PublicThreats.Clear();
        }
        public void StopCombat()
        {
            if(guardZone!=null)guardZone.enabled=false;reloadUntil=0;PublicThreats.Clear();
            actor?.Coordinator?.Release(actor);Ability=BossAbility.None;Phase=BossActionPhase.Idle;
        }
        public void OnDamaged(GameObject source)
        {
            if(Phase!=BossActionPhase.Prepare||source==null||source.GetComponent<PlayerShooting>()==null)return;
            if(Ability!=BossAbility.Piercing&&Ability!=BossAbility.Sweep)return;
            prepareHits++;
            if(prepareHits>=(Ability==BossAbility.Piercing?p.PiercingInterruptHits:p.SweepInterruptHits))
            {Interruptions++;InterruptedCurrent=true;actor.Sounds?.StopAttack(actor.ActorId,AttackId);Recover();phaseUntil=Time.time+p.InterruptRecovery;actor.Trace("interrupted");}
        }
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
            if(!Busy||Phase==BossActionPhase.Recovery||Ability==BossAbility.Camo||Ability==BossAbility.Guard)return;
            var phase=Phase==BossActionPhase.Prepare?BossThreatPhase.Preparation:Phase==BossActionPhase.Locked?BossThreatPhase.Locked:BossThreatPhase.Active;
            int count=Ability==BossAbility.Sweep?6:Ability==BossAbility.Burst?3:1;
            for(int i=0;i<count;i++)
            {
                Vector2 origin=Origin,end=End,direction=Aim;
                BossThreatShape shape=BossThreatShape.SweptCapsule;
                float radius=p.BladeRadius,start=activeStarts,finish=activeEnds,speed=0;
                bool terrain=true;
                if(Ability==BossAbility.Piercing){shape=BossThreatShape.Ray;radius=p.BeamRadius;terrain=false;}
                else if(Ability==BossAbility.Burst||Ability==BossAbility.Sweep)
                {
                    shape=BossThreatShape.Projectile;radius=actor.ProjectilePrefab.Radius;speed=p.BulletSpeed;
                    if(Ability==BossAbility.Burst)
                    {origin=actor.Position+Vector2.up*.28f+Aim*.52f;start+=i*p.BurstInterval;if(Phase==BossActionPhase.Active&&i<shotIndex)continue;}
                    if(Ability==BossAbility.Sweep){float angle=Mathf.Lerp(sweepStart,sweepEnd,i/5f)*Mathf.Deg2Rad;direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));start+=i*p.SweepInterval;}
                    end=origin+direction*settings.Range;finish=start+settings.Range/speed;
                }
                else if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                {radius=p.BladeRadius+.2f;finish=start+Vector2.Distance(origin,end)/(Ability==BossAbility.DrawDash?p.DashSpeed:p.ReturnSpeed);}
                else if(Ability==BossAbility.Rhythm){shape=BossThreatShape.Ray;}
                else if(Ability==BossAbility.DropCut){shape=BossThreatShape.Ray;if(Phase==BossActionPhase.Active){origin=actor.Position;end=origin+Aim*settings.Range;}}
                PublicThreats.Add(new BossThreat {ActorId=actor.ActorId,AttackId=AttackId,Phase=phase,Shape=shape,Origin=origin,End=end,Direction=direction,
                    Radius=radius,StartsAt=start,EndsAt=finish,ProjectileSpeed=speed,TerrainBlocked=terrain,Valid=true});
            }
        }
    }
}
