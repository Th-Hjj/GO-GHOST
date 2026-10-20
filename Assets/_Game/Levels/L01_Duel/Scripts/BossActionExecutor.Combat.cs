using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Real shots, physical hit sweeps, reflection and accepted follow-up connections.
    public sealed partial class BossActionExecutor
    {
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
            Aim=actor.IsTrainingEnemy ? Vector2.right*actor.Facing
                : (targetPoint-(actor.Position+Vector2.up*.28f)).normalized;
            Origin=actor.Position+Vector2.up*.28f+Aim*.52f;
            End=Origin+Aim*(actor.IsTrainingEnemy||BurstAction?p.Burst.Range:settings.Range);
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
                actor.View?.Bullet(shot,direction);
                actor.Sounds?.Play(Sfx.BossBurst,actor.ActorId,AttackId,shotIndex);actor.View?.Muzzle(muzzle,direction);
            }
            Ammo--;shotsRemaining--;shotIndex++;bulletLocked=false;
            // The next round now has its own visible preparation/lock, even while the
            // enclosing Burst is Active. Keep that identity stable until this round fires.
            if(BurstAction&&shotsRemaining>0)PreparationId++;
            nextShot=Time.time+(BurstAction?(StaggeredBurst&&shotIndex==1?p.BurstStaggerPause:p.BurstInterval):p.SweepInterval);
            if(shotsRemaining==0){phaseUntil=Time.time+(BurstAction?p.BurstTail:p.SweepTail);activeEnds=phaseUntil;}
            if(Ammo==0)RequestReload();
        }
        void FixedUpdate()
        {
            if(actor!=null&&actor.Running&&Time.deltaTime>0)
            {
                swordHazards?.Tick();
                if(IsSwordPhrase(Ability)){SweepSwordBeat();return;}
            }
            if(actor!=null&&actor.Running&&Ability==BossAbility.Guard&&Time.timeScale>0)actor.Motor.HoldHorizontal();
            if(actor==null||!actor.Running||Phase!=BossActionPhase.Active||Time.timeScale<=0)return;
            if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                SweepDashBlade();
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
                Vector2 offset=Vector2.down*(BladeReach*i/4f);
                // A translated blade tip may already lie below a platform. It cannot originate damage there.
                if(actor.Player!=null&&!Projectile.TerrainHit(now,actor.Player.GetComponent<Collider2D>().ClosestPoint(now+offset),.015f,out _))
                    HitSegment(previousBladeOrigin+offset,now+offset,p.BladeRadius,settings.Damage,true);
            }
            HitSegment(now,now+Vector2.down*BladeReach,p.BladeRadius,settings.Damage,true);
            Origin=now;Aim=Vector2.down;End=now+Aim*BladeReach;previousBladeOrigin=now;
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
            Vector2 nextTarget=next<3?actor.Tactics.SwordTarget(BossAbility.Rhythm,actor.Position,observed,-1,next):observed.Position;
            if(next>=3||observed.Confidence<.2f
                || Vector2.Distance(nextTarget,actor.Position)>p.RhythmReach[next]+p.RhythmStep[next]+observed.Body.extents.x
                || Mathf.Abs(nextTarget.y-actor.Position.y)>p.RhythmReach[next]+observed.Body.extents.y)
            {Recover();return;}
            RhythmIndex=next;targetPoint=nextTarget;
            actor.Motor.BeginStep(p.RhythmStep[next]);
            SetPhase(BossActionPhase.Prepare,p.RhythmPrepare[next]);UpdateGeometry();
        }
        void FinishDash()
        {
            // A body/wall sweep can stop the motor during its last physics step. Resolve that
            // completed displacement before Update leaves Active; the next FixedUpdate is too late.
            SweepDashBlade();
            End=actor.Position;PublicThreats.Clear();
            if(TryDashFollowup())return;
            returnWindowUntil=Time.time+p.ReturnBranchWindow;Recover();
        }
        void SweepDashBlade()
        {
            Vector2 offset=DashBladeOffset;
            // A leading blade must not originate damage through a wall/platform.
            if(offset.sqrMagnitude>0&&Projectile.TerrainHit(actor.Position,actor.Position+offset,.015f,out _))return;
            HitSegment(actor.Motor.PreviousPosition+offset,actor.Position+offset,p.BladeRadius+.2f,settings.Damage,true);
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
            if(Ability==BossAbility.DrawDash&&!comboTarget&&!hasReturned&&actor.Motor.Grounded&&Ready(BossAbility.ReturnCut)&&facts.Confidence>.3f&&Mathf.Sign(opportunity.x-actor.Position.x)!=Mathf.Sign(Aim.x)
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
            if(!shot.Deflect(gameObject,ProjectileActor.Environment))return false;
            if(shot.TryGetComponent<SpriteRenderer>(out var sprite))sprite.color=NeuralPalette.Severance;
            Guarded++;actor.Sounds?.Play(Sfx.GuardBlock,actor.ActorId,AttackId,Guarded);actor.View?.Impact(shot.transform.position);
            if(Guarded>=p.GuardCapacity)Recover();return true;
        }
    }
}
