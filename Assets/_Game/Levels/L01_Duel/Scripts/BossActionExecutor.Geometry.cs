using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // One action clock supplies matching attack geometry, tells and public threats.
    public sealed partial class BossActionExecutor
    {
        // Read only expressed attack geometry, including a current sub-attack inside Active.
        // Detached residual blades do not make the actor itself prepare another attack.
        public bool TryGetPublicPreparation(out int preparationId)
        {
            preparationId=0;
            if(!Busy||Phase==BossActionPhase.Recovery)return false;
            foreach(var threat in PublicThreats)
            {
                if(!threat.Valid||threat.AttackId!=AttackId||threat.EndsAt<Time.time
                    ||(threat.Phase!=BossThreatPhase.Preparation&&threat.Phase!=BossThreatPhase.Locked))continue;
                preparationId=PreparationId;return true;
            }
            return false;
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
                {
                    if(!actor.Navigation.TryDirectionalAttack(centre,targetPoint,actor.Motor.Size,out var safe))End=centre;
                    else {End=safe;Aim=(End-centre).normalized;}
                }
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
            {Origin=centre;Aim=Vector2.down;End=centre+Aim*BladeReach;}
            if(actor.IsTrainingEnemy && Ability==BossAbility.Burst)UpdateGunAim();
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
        void Publish()
        {
            PublicThreats.Clear();
            swordHazards?.Publish(PublicThreats);
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
                    range=Mathf.Min(range,ArenaBoundary2D.ExitDistance(actor.Navigation.ArenaBounds,origin,direction,radius));
                    end=origin+direction*range;finish=start+range/speed;
                }
                else if(Ability==BossAbility.DrawDash||Ability==BossAbility.ReturnCut)
                {
                    radius=p.BladeRadius+.2f;finish=start+Vector2.Distance(origin,end)/(Ability==BossAbility.DrawDash?p.DashSpeed:p.ReturnSpeed);
                    origin+=DashBladeOffset;end+=DashBladeOffset;
                    // The full-body route was clipped by navigation, including one-way traversal.
                    // A second ray clip would incorrectly erase an upward route through a one-way support.
                    if(Ability==BossAbility.DrawDash)terrain=false;
                }
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
