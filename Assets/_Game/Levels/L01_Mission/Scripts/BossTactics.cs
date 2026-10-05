using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed class BossPlan
    {
        public string Intent;
        public Vector2 Destination, TargetHypothesis;
        public ArenaNavigation2D.Route Route;
        public BossAbility Ability;
        public float Score, EstimatedRisk, CreatedAt, MaxPlanTime, TravelSeconds;
        public int TerrainVersion;
        // Start: supported arrival + resource/ray/range valid. Commit: action Locked.
        // Exit: terrain invalidation, deadline, route failure, lost contact, actor death.
    }
    public sealed class BossTactics
    {
        readonly BossAgent actor;
        readonly List<BossPlan> candidates=new List<BossPlan>(12);
        readonly List<BossPlan> ranked=new List<BossPlan>(64);
        float nextTick,lastAttackAt;
        BossAbility previousAbility;
        BossPlan plan;
        public bool Enabled {get;set;}=true;
        public BossPlan Current=>plan;
        public BossTactics(BossAgent actor){this.actor=actor;}
        public void Tick()
        {
            if(!Enabled)return;
            var action=actor.Actions; var facts=actor.Perception.Facts;
            if(action.Busy)return;
            if(actor.Coordinator!=null&&actor.Coordinator.HasAssignment(actor))return;
            // A falling attack is only offered during the end of a real upward jump.
            if(actor.Profile.Kind==BossKind.Severance&&!actor.Motor.Grounded&&actor.Velocity.y>0&&actor.Velocity.y<4
                &&facts.Confidence>.3f&&facts.Position.y<actor.Position.y&&Mathf.Abs(facts.Position.x-actor.Position.x)<1.3f)
            {if(action.TryStart(BossAbility.DropCut,facts.Predict(.25f))){plan=null;return;}}
            if(!actor.Motor.Grounded)return; // Finish the accepted physical air connection before replanning.
            if(plan!=null)
            {
                // A solid body can stop arrival while the accepted attack is already legal here.
                // Only recover a failed route this way; successful flanking routes keep their destination.
                if(actor.Motor.RouteFailed&&plan.TerrainVersion==actor.Navigation.Version
                    &&plan.Ability!=BossAbility.None&&LegalAt(plan.Ability,actor.Position,facts,out _)
                    &&action.TryStart(plan.Ability,plan.TargetHypothesis))
                {
                    actor.Motor.Brake();previousAbility=plan.Ability;lastAttackAt=Time.time;plan=null;return;
                }
                bool invalid=plan.TerrainVersion!=actor.Navigation.Version||actor.Motor.RouteFailed||Time.time>plan.CreatedAt+plan.MaxPlanTime||facts.Confidence<=0;
                if(invalid){plan=null;actor.Motor.ClearRoute();}
                else if(Vector2.Distance(actor.Position,plan.Destination)<.38f&&actor.Motor.Grounded)
                {
                    actor.Motor.Brake();
                    if(plan.Ability!=BossAbility.None&&LegalAt(plan.Ability,actor.Position,facts,out _)
                        &&action.TryStart(plan.Ability,plan.TargetHypothesis))
                    {previousAbility=plan.Ability;lastAttackAt=Time.time;plan=null;return;}
                    if(plan.Ability==BossAbility.None||Time.time-plan.CreatedAt>.8f)plan=null;
                }
                else if(Time.time<nextTick)return;
            }
            if(Time.time<nextTick)return;
            nextTick=Time.time+actor.Profile.TacticTick;
            if(facts.Confidence<=0){SearchVisibleAngle();return;}
            var options=BuildCandidates();
            if(options.Count==0){SearchVisibleAngle();return;}
            BossPlan selected=options[0];
            if(plan!=null&&(Time.time-plan.CreatedAt<actor.Profile.PlanHold||selected.Score<plan.Score*1.15f))return;
            plan=selected;plan.CreatedAt=Time.time;
            actor.Trace($"plan {plan.Intent}/{plan.Ability} to={plan.Destination} score={plan.Score:0.00}");
            if(plan.Route!=null)actor.Motor.SetRoute(plan.Route);
            if(actor.Profile.Kind==BossKind.Refraction&&plan.TravelSeconds>.45f&&selected.Score>.8f&&action.Ready(BossAbility.Camo))
                action.TryStart(BossAbility.Camo,plan.TargetHypothesis);
            if(actor.Profile.Kind==BossKind.Refraction&&!action.CanAfford(plan.Ability))action.RequestReload();
        }
        void SearchVisibleAngle()
        {
            // Keep seeking a real visible angle after memory expires, without reading the player's Transform.
            if(actor.Motor.HasRoute)return;
            float best=float.MaxValue;ArenaNavigation2D.Route route=null;
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);float distance=Vector2.Distance(point,actor.Position);
                if(distance<1||distance>7||distance>=best)continue;
                if(actor.Navigation.TryRoute(actor.Position,point,actor.Motor,out var path)) {best=distance;route=path;}
            }
            if(route!=null)actor.Motor.SetRoute(route);
        }
        public List<BossPlan> BuildCandidates(BossAbility forced=BossAbility.None)
        {
            ranked.Clear();candidates.Clear();var facts=actor.Perception.Facts;
            if(facts.Confidence<=0)return candidates;
            AddAt(actor.Position,facts,forced,true);
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);
                if(Vector2.Distance(point,actor.Position)>13)continue;
                AddAt(point,facts,forced,false);
            }
            ranked.Sort((a,b)=>b.Score.CompareTo(a.Score));
            int attempts=0;
            foreach(var candidate in ranked)
            {
                if(candidates.Count>=8||attempts++>=12)break;
                bool near=Vector2.Distance(candidate.Destination,actor.Position)<.25f;
                if(!near&&!actor.Navigation.TryRoute(actor.Position,candidate.Destination,actor.Motor,out candidate.Route))continue;
                candidate.TravelSeconds=candidate.Route!=null?candidate.Route.Duration:0;
                candidate.Score-=candidate.TravelSeconds*.22f;
                candidate.MaxPlanTime=Mathf.Max(actor.Profile.PlanTimeout,candidate.TravelSeconds+1.2f);
                candidates.Add(candidate);
            }
            candidates.Sort((a,b)=>b.Score.CompareTo(a.Score));return candidates;
        }
        void AddAt(Vector2 point,BossObservation facts,BossAbility forced,bool current)
        {
            if(!current&&!actor.Navigation.CanStand(point,actor.Motor.Size))return;
            if(actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))return;
            BossAbility[] abilities=actor.Profile.Kind==BossKind.Refraction
                ?new[]{BossAbility.Burst,BossAbility.Piercing,BossAbility.Sweep}:new[]{BossAbility.Rhythm,BossAbility.DrawDash,BossAbility.Guard};
            foreach(var ability in abilities)
            {
                if(forced!=BossAbility.None&&ability!=forced)continue;
                if(!actor.Actions.Ready(ability))continue;
                if(!LegalAt(ability,point,facts,out float chance))continue;
                float distance=Vector2.Distance(point,actor.Position),targetDistance=Mathf.Abs(point.x-facts.Position.x);
                float exposed=facts.Visible&&Vector2.Dot(facts.Aim,(point-facts.Position).normalized)>.8f?1:0;
                float benefit=ability==BossAbility.Piercing?1.15f:ability==BossAbility.Sweep?1.05f:ability==BossAbility.DrawDash?1.18f:ability==BossAbility.Rhythm?1.25f:.95f;
                float risk=exposed*actor.Profile.ExposureWeight*(ability==BossAbility.Piercing?.6f:.2f);
                float preferred=actor.Profile.Kind==BossKind.Refraction?Mathf.Clamp01(1-Mathf.Abs(targetDistance-actor.Profile.DesiredDistance)/12):Mathf.Clamp01(1-targetDistance/8);
                float score=benefit+chance*.7f+preferred*.28f-distance*.055f-risk;
                if(ability==previousAbility&&Time.time-lastAttackAt<4)score-=actor.Profile.RepetitionWeight;
                if(!actor.Actions.CanAfford(ability))score-=.45f;
                if(actor.Coordinator!=null)score+=actor.Coordinator.RoleScore(actor,ability,point);
                ranked.Add(new BossPlan {Intent=ability==BossAbility.Guard?"GuardAdvance":distance>.5f?"RepositionAngle":ability==BossAbility.Sweep?"CutExit":"Pressure",
                    Destination=point,TargetHypothesis=facts.Predict(ability==BossAbility.Piercing?.5f:.16f),Ability=ability,
                    Score=score,EstimatedRisk=risk,CreatedAt=Time.time,TerrainVersion=actor.Navigation.Version});
            }
            if(forced==BossAbility.None&&actor.Profile.Kind==BossKind.Severance)
            {
                float distance=Vector2.Distance(point,actor.Position),targetDistance=Vector2.Distance(point,facts.Predict(.35f));
                float score=1.0f-Mathf.Min(targetDistance,15)*.12f-distance*.035f;
                if(point.y>actor.Position.y+.5f&&facts.Position.y>actor.Position.y+.3f)score+=.3f;
                ranked.Add(new BossPlan{Intent="Approach/Intercept",Destination=point,TargetHypothesis=facts.Position,Ability=BossAbility.None,
                    Score=score,CreatedAt=Time.time,TerrainVersion=actor.Navigation.Version});
            }
        }
        public bool LegalAt(BossAbility ability,Vector2 point,BossObservation facts,out float chance)
        {
            chance=facts.Confidence;Vector2 delta=facts.Position-point;float distance=delta.magnitude;
            if(facts.Confidence<=0)return false;
            if(ability==BossAbility.Burst||ability==BossAbility.Sweep)
            {
                if(distance>actor.Profile.Settings(ability).Range||!ClearGunLine(point,facts.Position))return false;
                if(ability==BossAbility.Sweep)chance+=Mathf.Clamp01(Mathf.Abs(facts.Velocity.x)/7)*.25f;
            }
            else if(ability==BossAbility.Piercing)
            {if(distance<3||distance>actor.Profile.Piercing.Range||AllyBlocks(point,facts.Position))return false;chance+=facts.ReloadVisible?.35f:0;}
            else if(ability==BossAbility.Rhythm)
            {if(distance>actor.Profile.Rhythm.Range+.1f||Mathf.Abs(delta.y)>.65f||Projectile.TerrainHit(point,facts.Position,0,out _))return false;}
            else if(ability==BossAbility.DrawDash)
            {
                if(Mathf.Abs(delta.y)>.65f||Mathf.Abs(delta.x)<1.1f||Mathf.Abs(delta.x)>actor.Profile.DrawDash.Range)return false;
                Vector2 end=point+Vector2.right*Mathf.Sign(delta.x)*Mathf.Min(actor.Profile.DrawDash.Range,Mathf.Abs(delta.x)+.7f);
                if(!actor.Navigation.TryDash(point,end,actor.Motor.Size,out var safe)||Vector2.Distance(point,safe)<1)return false;
                chance+=Mathf.Abs(facts.Velocity.x)<1?.15f:0;
            }
            else if(ability==BossAbility.Guard)
            {
                if(!facts.Visible||Time.time-facts.LastShotAt>.8f||distance>9||Mathf.Abs(delta.y)>1.5f)return false;
                chance=.3f;
                foreach(var shot in Projectile.Active)
                {
                    if(shot==null||shot.OwnerLayer==actor.gameObject.layer)continue;
                    var velocity=shot.GetComponent<Rigidbody2D>().linearVelocity;
                    if(Vector2.Dot(velocity,point-(Vector2)shot.transform.position)>0&&Vector2.Distance(point,shot.transform.position)<5){chance=1.15f;break;}
                }
            }
            return true;
        }
        public bool ClearGunLine(Vector2 position,Vector2 target)=>!Projectile.TerrainHit(position+Vector2.up*.28f,target,.08f,out _)&&!AllyBlocks(position,target);
        public bool AllyBlocks(Vector2 from,Vector2 target)
        {
            if(actor.Coordinator==null)return false;
            Vector2 delta=target-from;
            foreach(var hit in Physics2D.RaycastAll(from,delta.normalized,delta.magnitude,1<<actor.gameObject.layer))
                foreach(var other in actor.Coordinator.Actors)if(other!=actor&&other.IsAlive&&hit.collider==other.Body)return true;
            return false;
        }
    }
}
