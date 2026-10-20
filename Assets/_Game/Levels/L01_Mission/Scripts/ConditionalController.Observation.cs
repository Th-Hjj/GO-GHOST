using System;
using UnityEngine;

namespace GoGhost.Duel
{
    // Public observations, bounded occurrences and same-enemy condition evaluation.
    public sealed partial class ConditionalController
    {
        void Snapshot()
        {
            allVisible=0;
            for(int i=0;i<actors.Count;i++)
            {
                BossContact c=default;
                bool now=actors[i]!=null&&actors[i].TryGetPlayerRuleContact(out c)&&c.Visible;
                if(now)
                {
                    contacts[i]=c;allVisible|=1<<i;

                    seen[i]=true;hidden[i]=false;
                    lowEnemy[i]=HysteresisLow(c.HealthFraction,lowEnemy[i]);
                    float distance=Mathf.Abs(c.Bounds.center.x-shape.bounds.center.x);
                    close[i]=distance<=(close[i]?ConditionalDefaults.Close+ConditionalDefaults.Hysteresis:ConditionalDefaults.Close);
                    far[i]=distance>=(far[i]?ConditionalDefaults.Far-ConditionalDefaults.Hysteresis:ConditionalDefaults.Far);
                    cursorNear[i]=MouseAngle(i)<=(cursorNear[i]?ConditionalDefaults.MouseExit:ConditionalDefaults.MouseEnter);
                }
                else if(seen[i])hidden[i]=true;
                visible[i]=now;
                if(!now)cursorNear[i]=false;
            }
            bool low=HysteresisLow((float)player.CurrentHealth/player.MaxHealth,lowHealth);
            if(initialized)
            {
                if(movement.IsGrounded&&!grounded)Record(3);
            }
            grounded=movement.IsGrounded;lowHealth=low;
            initialized=true;
            events.RemoveAll(n=>Time.time>n.Context.ExpiresAt);
            liveEvents.Clear();
            foreach(var notice in events)liveEvents.Add(notice.Context.Occurrence);
            for(int op=0;op<5;op++)for(int row=0;row<loadout.Operations[op].Rules.Count;row++)
                states[op,row].ConsumedEvents.RemoveWhere(expiredEvent);
        }
        bool IsExpiredEvent(long occurrence)=>!liveEvents.Contains(occurrence);
        static bool HysteresisLow(float ratio,bool wasLow)=>wasLow?ratio<ConditionalDefaults.LowExit:ratio<=ConditionalDefaults.LowEnter;
        static bool HasPreparation(ConditionalRule rule)=>rule.Condition==25||rule.AdditionalCondition==25;
        TriggerContext EmptyContext()=>new TriggerContext {Candidates=allVisible,Source=-1,PreparationSource=-1};
        bool Evaluate(ConditionalRule rule,int op,int row,out TriggerContext ctx,out bool observed)
        {
            var state=states[op,row];ctx=EmptyContext();observed=false;
            if(ConditionalCatalog.Condition(rule.Condition).Event)
            {
                // A cadence wait may use only the original occurrence's live window.
                if(state.CadencePending)
                {
                    var pending=state.CadenceContext;
                    bool inside=Time.time<=pending.ExpiresAt;
                    if(inside&&!state.ConsumedEvents.Contains(pending.Occurrence)
                        &&gun.isActiveAndEnabled&&!gun.IsReloading&&gun.AmmoInMagazine>AmmoFloor(rule)
                        &&AdditionalMatches(rule,op,ref pending))
                    {ctx=pending;observed=inside;return true;}
                    state.CadencePending=false;
                }
                bool found=false;
                foreach(var notice in events)
                {
                    if(notice.Condition!=rule.Condition||notice.Context.Occurrence>observedSequence)continue;
                    var candidate=notice.Context;
                    if(!AdditionalMatches(rule,op,ref candidate))continue;
                    observed=true;
                    if(!found&&!state.ConsumedEvents.Contains(candidate.Occurrence)){ctx=candidate;found=true;}
                }
                return found;
            }
            if(!StateRuleMatches(rule,op,out ctx))return false;
            observed=true;
            if(HasPreparation(rule)&&ConditionalCatalog.Response(rule.Action).Finite)
            {
                int available=0;
                for(int i=0;i<actors.Count;i++)if((ctx.Candidates&(1<<i))!=0&&visible[i]&&contacts[i].Preparing
                    &&(!state.Preparations.TryGetValue(i,out int used)||used!=contacts[i].PreparationId))available|=1<<i;
                if(available==0)return false;
                ctx.Candidates=available;SelectSource(ref ctx);
            }
            return true;
        }
        bool StateRuleMatches(ConditionalRule rule,int op,out TriggerContext ctx)
        {
            if(!StateFact(rule,false,op,out ctx))return false;
            return AdditionalMatches(rule,op,ref ctx);
        }
        bool AdditionalMatches(ConditionalRule rule,int op,ref TriggerContext ctx)
        {
            if(rule.AdditionalCondition==0)return true;
            if(!StateFact(rule,true,op,out var extra))return false;
            if(ctx.EnemyBound&&extra.EnemyBound)
            {
                ctx.Candidates&=extra.Candidates;
                if(ctx.Candidates==0)return false;
            }
            else if(extra.EnemyBound)
            {ctx.Candidates=extra.Candidates;ctx.EnemyBound=true;}
            if(!ctx.IsThreat&&extra.IsThreat&&(ctx.Occurrence==0||ctx.Source<0))
            {ctx.IsThreat=true;ctx.ThreatKind=extra.ThreatKind;ctx.HasPosition=false;}
            if(extra.PreparationSource>=0)ctx.PreparationSource=extra.PreparationSource;
            SelectSource(ref ctx);
            return true;
        }
        void SelectSource(ref TriggerContext ctx)
        {
            if(!ctx.EnemyBound)return;
            if(ctx.Source<0||(ctx.Candidates&(1<<ctx.Source))==0)
            {
                ctx.Source=Choose(ctx.Candidates,"Q01");
                // A public projectile can retain a source whose body is currently concealed.
                if(ctx.Source<0)for(int i=0;i<actors.Count;i++)if((ctx.Candidates&(1<<i))!=0){ctx.Source=i;break;}
                ctx.HasPosition=false;
            }
            if(ctx.Source>=0&&!ctx.HasPosition)
            {
                if(ctx.IsThreat)
                {
                    if(geometry.ConditionalThreat(ctx.ThreatKind,movement.MoveInput.x,false,out var origin,out _,actors[ctx.Source].ActorId))
                    {ctx.HasPosition=true;ctx.Position=origin;}
                }
                else if(visible[ctx.Source]){ctx.HasPosition=true;ctx.Position=contacts[ctx.Source].Bounds.center;}
            }
            if(ctx.PreparationSource>=0&&ctx.Source>=0&&visible[ctx.Source]&&contacts[ctx.Source].Preparing)
            {ctx.PreparationSource=ctx.Source;ctx.PreparationId=contacts[ctx.Source].PreparationId;}
        }
        bool StateFact(ConditionalRule rule,bool additional,int op,out TriggerContext ctx)
        {
            int id=additional?rule.AdditionalCondition:rule.Condition;
            int option=id==51?(int)(additional?rule.AdditionalGroundState:rule.GroundState)
                :id==52?(int)(additional?rule.AdditionalDistance:rule.Distance)
                :id==53?(int)(additional?rule.AdditionalIncomingAttack:rule.IncomingAttack):0;
            int key=id*32+option*2;
            if(!facts.TryGetValue(key,out var fact))
            {
                bool match=EvaluateState(id,option,op,out ctx);
                facts[key]=fact=new Fact {Match=match,Context=ctx};
            }
            ctx=fact.Context;return fact.Match;
        }
        bool EvaluateState(int id,int option,int op,out TriggerContext ctx)
        {
            ctx=EmptyContext();
            switch(id)
            {
                case 51:return option==(int)GroundStateOption.Grounded?movement.IsGrounded:!movement.IsGrounded;
                case 4:return lowHealth;
                case 9:return gun.IsReloading;
                case 12:return !gun.IsReloading&&gun.AmmoInMagazine>0&&gun.AmmoInMagazine<=2;
                case 17:return raw.Fire;
                case 54:return movement.IsOrdinaryMoving;
                case 55:return dodge.IsDodging;
                case 56:return gun.IsFiring;
                case 39:
                    bool left=false,right=false;
                    for(int i=0;i<actors.Count;i++)if(visible[i])
                    {left|=contacts[i].Bounds.center.x<shape.bounds.center.x;right|=contacts[i].Bounds.center.x>shape.bounds.center.x;}
                    if(left&&right){ctx.Source=Choose(ctx.Candidates,"Q01");ctx.HasPosition=true;ctx.Position=contacts[ctx.Source].Bounds.center;}
                    return left&&right;
                case 53:
                    ctx.EnemyBound=true;ctx.IsThreat=true;
                    if(option==(int)IncomingAttackOption.Low)return false;
                    ctx.ThreatKind=option==(int)IncomingAttackOption.Ranged?41:option==(int)IncomingAttackOption.Melee?42:0;
                    ctx.Candidates=0;
                    for(int i=0;i<actors.Count;i++)if(actors[i]!=null&&geometry.ConditionalThreat(ctx.ThreatKind,movement.MoveInput.x,false,out _,out _,actors[i].ActorId))
                        ctx.Candidates|=1<<i;
                    if(ctx.Candidates==0)
                    {
                        if(!geometry.ConditionalThreat(ctx.ThreatKind,movement.MoveInput.x,false,out var unknown,out int actorId))return false;
                        // Public geometry without a body identity is usable but cannot match a named enemy filter.
                        ctx.HasPosition=true;ctx.Position=unknown;return true;
                    }
                    SelectSource(ref ctx);return true;
                case 45:
                    float margin=shape.bounds.size.x;
                    return shape.bounds.min.x-arena.min.x<margin||arena.max.x-shape.bounds.max.x<margin;
                case 18:case 25:case 35:case 40:case 52:
                    ctx.EnemyBound=true;ctx.Candidates=0;
                    for(int i=0;i<actors.Count;i++)if(visible[i]&&EnemyMatches(id,option,i,op))ctx.Candidates|=1<<i;
                    if(ctx.Candidates==0)return false;
                    if(id==25)ctx.PreparationSource=0;
                    SelectSource(ref ctx);return true;
                default:throw new InvalidOperationException("Unhandled V2 state A"+id);
            }
        }
        bool EnemyMatches(int id,int option,int i,int op)
        {
            var c=contacts[i];Vector2 delta=(Vector2)c.Bounds.center-(Vector2)shape.bounds.center;
            switch(id)
            {
                case 18:return cursorNear[i];
                case 25:return c.Preparing;
                case 52:return option==(int)EnemyDistanceOption.Close?close[i]:option==(int)EnemyDistanceOption.Far?far[i]:!close[i]&&!far[i];
                case 35:return c.VelocityKnown&&delta.sqrMagnitude>=.01f
                    &&Vector2.Dot(c.Velocity-body.linearVelocity,delta.normalized)<-ConditionalDefaults.VelocityDeadzone;
                case 40:return RayAligned(i,raw.CommittedAim);
                default:throw new InvalidOperationException("Unhandled enemy condition");
            }
        }
        float MouseAngle(int target)=>Vector2.Angle(raw.Aim,(Vector2)contacts[target].Bounds.center-gun.AimOrigin);
        bool NearSupportEdge()
        {
            if(!movement.IsGrounded||platforms==null)return false;
            var bounds=shape.bounds;
            foreach(var support in platforms)
            {
                if(support==null||!support.enabled)continue;
                var b=support.bounds;
                if(Mathf.Abs(bounds.min.y-b.max.y)>.13f||bounds.center.x<b.min.x||bounds.center.x>b.max.x)continue;
                return bounds.center.x-b.min.x<bounds.size.x||b.max.x-bounds.center.x<bounds.size.x;
            }
            return false;
        }
        int Choose(int mask,string response)
        {
            if(response=="Q07")return currentTarget>=0&&visible[currentTarget]&&(mask&(1<<currentTarget))!=0?currentTarget:-1;
            int best=-1;float bestScore=float.PositiveInfinity;
            for(int i=0;i<actors.Count;i++)
            {
                if(!visible[i]||(mask&(1<<i))==0)continue;
                if((response=="Q06"||response=="Q09")&&MouseAngle(i)>ConditionalDefaults.MouseEnter)continue;
                float score=((Vector2)contacts[i].Bounds.center-(Vector2)shape.bounds.center).sqrMagnitude;
                if(response=="Q06"||response=="Q09")score=MouseAngle(i);
                if(response=="Q02"&&best>=0)
                {
                    if(contacts[i].HealthFraction>contacts[best].HealthFraction)continue;
                    if(contacts[i].HealthFraction<contacts[best].HealthFraction){best=i;bestScore=score;continue;}
                }
                if(response=="Q04"&&actors[i].Profile.Kind==BossKind.Refraction||response=="Q05"&&actors[i].Profile.Kind==BossKind.Severance)score-=100000;
                if(score<bestScore-.0001f||Mathf.Abs(score-bestScore)<.0001f&&(best<0||actors[i].ActorId<actors[best].ActorId)) {best=i;bestScore=score;}
            }
            return best;
        }
        int AlignedTarget(Vector2 aim,int mask)
        {
            int target=Choose(mask,"Q01");
            if(target>=0&&RayAligned(target,aim))return target;
            for(int i=0;i<actors.Count;i++)if(visible[i]&&(mask&(1<<i))!=0&&RayAligned(i,aim))return i;
            return -1;
        }
        bool RayAligned(int i,Vector2 aim)
        {
            if(!visible[i]||aim.sqrMagnitude<.01f)return false;
            Vector2 origin=gun.GetMuzzlePosition(aim.normalized);
            // Current body geometry only: no bullet radius, prediction, guard or damage policy.
            var bounds=contacts[i].Bounds;
            if(!bounds.IntersectRay(new Ray(origin,aim.normalized),out float distance)||distance<0)return false;
            var target=actors[i].Body;
            if(target.OverlapPoint(origin))return true;
            int count=Physics2D.Raycast(origin,aim.normalized,new ContactFilter2D {useTriggers=false},
                hits,distance+bounds.size.magnitude+.01f);
            for(int h=0;h<count;h++)if(hits[h].collider==target)return true;
            return false;
        }
        Vector2 AimAt(int target)
        {
            var c=contacts[target];Vector2 point=c.Bounds.center;
            for(int k=0;k<3;k++)
            {
                float flight=Vector2.Distance(gun.GetMuzzlePosition((point-gun.AimOrigin).normalized),point)/Mathf.Max(.01f,gun.BulletSpeed);
                flight=Mathf.Min(.35f,flight);
                point=(Vector2)c.Bounds.center+(c.VelocityKnown?c.Velocity*flight:Vector2.zero);
                if(c.GroundedKnown&&!c.Grounded)point.y-=.5f*c.Gravity*flight*flight;
                if(actors[target].Navigation!=null&&!actors[target].Navigation.SweepTraversal(c.Bounds.center,point,c.Bounds.size,out _,out var safe))point=safe;
            }
            return (point-gun.AimOrigin).normalized;
        }
    }
}
