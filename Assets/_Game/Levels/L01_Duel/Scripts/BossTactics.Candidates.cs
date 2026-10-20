using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Candidate scoring and bounded route admission; enumeration and tie order are intentional.
    public sealed partial class BossTactics
    {
        public List<BossPlan> BuildCandidates(BossAbility forced=BossAbility.None)=>BuildCandidates(forced,false);
        List<BossPlan> BuildCandidates(BossAbility forced,bool camo)
        {
            using(CandidatesSample.Auto())
            {BeginPredictions();try{return FindCandidates(forced,camo);}finally{EndPredictions();}}
        }
        static readonly Unity.Profiling.ProfilerMarker CandidatesSample=new Unity.Profiling.ProfilerMarker("Boss.Candidates");
        List<BossPlan> FindCandidates(BossAbility forced,bool camo)
        {
            ranked.Clear();candidates.Clear();var facts=actor.Perception.Facts;
            if(facts.Confidence<=0)return candidates;
            AddAt(actor.Position,facts,forced,true);
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);
                if(Vector2.Distance(point,actor.Position)>actor.Navigation.SameLevelSpacing*1.5f)continue;
                if(forced!=BossAbility.None && (Mathf.Abs(point.x-actor.Position.x)>actor.Navigation.PlatformWidth
                    || Mathf.Abs(point.y-actor.Position.y)>ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile)))continue;
                AddAt(point,facts,forced,false);
                if(actor.Profile.Kind==BossKind.Severance&&forced==BossAbility.None&&facts.Visible)
                {
                    // Sample a continuous interception point on this real support, not only its graph node.
                    Vector2 intercept=PredictSwordBody(facts,Vector2.Distance(point,actor.Position)/actor.Profile.Motion.Speed+.28f);
                    float side=Mathf.Abs(facts.Velocity.x)>1?Mathf.Sign(facts.Velocity.x):Mathf.Sign(actor.Position.x-intercept.x);
                    float left=node.Collider.bounds.min.x+actor.Motor.Size.x*.5f+.03f;
                    float right=node.Collider.bounds.max.x-actor.Motor.Size.x*.5f-.03f;
                    if(left<=right)
                    {
                        Vector2 cutOff=new Vector2(Mathf.Clamp(intercept.x+side*EngagementDistance,left,right),point.y);
                        if(Vector2.Distance(cutOff,point)>.4f&&actor.Navigation.TrySupport(cutOff,actor.Motor.Size,out var support)
                            &&support==node.Collider)AddAt(cutOff,facts,forced,false);
                    }
                }
            }
            if(camo)
            {
                for(int i=ranked.Count-1;i>=0;i--)
                {
                    var candidate=ranked[i];float benefit=CamoAngleBenefit(candidate.Destination,facts);
                    if(benefit<=0||!actor.Actions.CanAfford(candidate.Ability))ranked.RemoveAt(i);
                    else candidate.Score+=benefit;
                }
            }
            ranked.Sort((a,b)=>forced==BossAbility.None?b.Score.CompareTo(a.Score)
                : Vector2.Distance(a.Destination,actor.Position).CompareTo(Vector2.Distance(b.Destination,actor.Position)));
            int attempts=0;
            candidateRoutes.Clear();
            var perSupport=new Dictionary<Collider2D,int>();
            foreach(var candidate in ranked)
            {
                if(candidates.Count>=8||attempts>=12)break;
                Collider2D support=null;
                if(forced!=BossAbility.None)
                {
                    if(!actor.Navigation.TrySupport(candidate.Destination,actor.Motor.Size,out support))continue;
                    if(perSupport.TryGetValue(support,out int used)&&used>=2)continue;
                }
                attempts++;
                bool near=Vector2.Distance(candidate.Destination,actor.Position)<.25f;
                // Camo needs a short executable relocation, not a graph launch point at the jump limit.
                // Reuse the physical-position jump solver before walking to an approximate graph node.
                if(!near)
                {
                    // Several abilities can rank the same destination. Reachability is identical
                    // within this synchronous search, including a failed route; preserve attempts/order.
                    if(!candidateRoutes.TryGetValue(candidate.Destination,out candidate.Route))
                    {
                        bool directCamoJump=camo&&Mathf.Abs(candidate.Destination.y-actor.Position.y)>.3f
                            &&actor.Navigation.TryJumpRoute(actor.Position,candidate.Destination,actor.Motor,out candidate.Route);
                        if(!directCamoJump)actor.Navigation.TryRoute(actor.Position,candidate.Destination,actor.Motor,out candidate.Route);
                        candidateRoutes.Add(candidate.Destination,candidate.Route);
                    }
                    if(candidate.Route==null)continue;
                }
                candidate.TravelSeconds=candidate.Route!=null?candidate.Route.Duration:0;
                if(actor.Profile.Kind==BossKind.Severance&&candidate.Ability==BossAbility.None)
                {
                    // Compare at our actual route arrival, including the next blade's full tell.
                    Vector2 intercept=PredictSwordBody(facts,candidate.TravelSeconds+actor.Profile.Rhythm.Prepare+actor.Profile.Rhythm.Lock);
                    float oldError=Vector2.Distance(candidate.Destination,PredictSwordBody(facts,.35f));
                    candidate.Score+=(oldError-Vector2.Distance(candidate.Destination,intercept))*.22f;
                    candidate.Score+=SwordExitScore(candidate.Destination,facts,candidate.TravelSeconds);
                    candidate.TargetHypothesis=intercept;
                }
                if(camo)
                {
                    if(candidate.Route==null||candidate.TravelSeconds>actor.Profile.CamoDuration-.15f)continue;
                    int transitions=0;
                    foreach(var step in candidate.Route.Steps)if(step.Kind!=ArenaNavigation2D.TravelKind.Walk)transitions++;
                    if(transitions>1)continue;
                }
                candidate.Score-=candidate.TravelSeconds*.22f;
                candidate.MaxPlanTime=Mathf.Max(actor.Profile.PlanTimeout,candidate.TravelSeconds+1.2f);
                if(support!=null){perSupport.TryGetValue(support,out int used);perSupport[support]=used+1;}
                candidates.Add(candidate);
            }
            candidates.Sort((a,b)=>b.Score.CompareTo(a.Score));return candidates;
        }
        void AddAt(Vector2 point,BossObservation facts,BossAbility forced,bool current)
        {
            if(!current&&!actor.Navigation.CanStand(point,actor.Motor.Size))return;
            if(!current&&Time.time<avoidFailedUntil&&Vector2.Distance(point,failedDestination)<.9f)return;
            if(!current&&new Bounds(point,actor.Motor.Size).Intersects(facts.Body))return;
            if(actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))return;
            BossAbility[] abilities=actor.Profile.Kind==BossKind.Refraction
                ?new[]{BossAbility.Burst,BossAbility.Piercing,BossAbility.Sweep}:new[]{BossAbility.Rhythm,BossAbility.DrawDash,BossAbility.Guard};
            foreach(var ability in abilities)
            {
                if(actor.Profile.Kind==BossKind.Severance&&ability==BossAbility.Guard&&!current)continue;
                if(forced!=BossAbility.None&&ability!=forced)continue;
                if(!actor.Actions.Ready(ability))continue;
                if(actor.Coordinator!=null&&!actor.Coordinator.CanStart(actor,ability,false))continue;
                if(!LegalAt(ability,point,facts,out float chance))continue;
                float distance=Vector2.Distance(point,actor.Position),targetDistance=Mathf.Abs(point.x-facts.Position.x);
                float exposed=facts.Visible&&Vector2.Dot(facts.Aim,(point-facts.Position).normalized)>.8f?1:0;
                float benefit=ability==BossAbility.Piercing?1.15f:ability==BossAbility.Sweep?1.05f:ability==BossAbility.DrawDash?1.18f:ability==BossAbility.Rhythm?1.25f:.95f;
                float risk=exposed*actor.Profile.ExposureWeight*(ability==BossAbility.Piercing?.6f:.2f);
                float preferred=Mathf.Clamp01(1-Mathf.Abs(targetDistance-EngagementDistance)/(actor.Profile.Kind==BossKind.Refraction?actor.Navigation.SameLevelSpacing*1.4f:actor.Navigation.PlatformWidth));
                float score=benefit+chance*.7f+preferred*.28f-distance*.055f-risk;
                if(ability==BossAbility.Rhythm)score-=Mathf.Min(2,actor.Actions.ConsecutiveMeleeMisses)*.2f;
                if(actor.Profile.Kind==BossKind.Refraction)
                {
                    if(facts.ReloadVisible)score+=ability==BossAbility.Piercing?.6f:ability==BossAbility.Burst?.3f:-.25f;
                    // Eight-round sweep must earn the loss of immediate follow-up ammunition.
                    if(ability==BossAbility.Sweep&&actor.Actions.Ammo-actor.Profile.Sweep.Cost<3)score-=.6f;
                }
                else
                {
                    score=SwordImmediateScore(ability,point,facts,current)-distance*.12f;
                    if(float.IsNegativeInfinity(score))continue;
                }
                if(ability==previousAbility&&Time.time-lastAttackAt<4)score-=actor.Profile.RepetitionWeight;
                if(!actor.Actions.CanAfford(ability))score-=.45f;
                if(actor.Coordinator!=null)score+=actor.Coordinator.RoleScore(actor,ability,point);
                Vector2 hypothesis=facts.Predict(.16f);
                if(actor.Profile.Kind==BossKind.Refraction)TryGunTarget(ability,point,facts,out hypothesis);
                else hypothesis=SwordTarget(ability,point,facts);
                ranked.Add(new BossPlan {Intent=ability==BossAbility.Guard?"GuardAdvance":distance>.5f?"RepositionAngle":ability==BossAbility.Sweep?"CutExit":"Pressure",
                    Destination=point,TargetHypothesis=hypothesis,Ability=ability,
                    Score=score,EstimatedRisk=risk,CreatedAt=Time.time,TerrainVersion=actor.Navigation.Version});
            }
            if(forced==BossAbility.None&&actor.Profile.Kind==BossKind.Severance)
            {
                float distance=Vector2.Distance(point,actor.Position);
                Vector2 intercept=PredictSwordBody(facts,distance/actor.Profile.Motion.Speed+.28f);
                if(actor.Perception.LearnedRetreat!=0&&Mathf.Abs(facts.Velocity.x)>.5f)
                    intercept.x+=actor.Perception.LearnedRetreat*actor.Navigation.PlatformWidth*.2f;
                float targetDistance=Vector2.Distance(point,intercept);
                float score=1.0f-Mathf.Min(targetDistance,15)*.12f-distance*.035f;
                if(point.y>actor.Position.y+.5f&&facts.Position.y>actor.Position.y+.3f)score+=.3f;
                ranked.Add(new BossPlan{Intent="Approach/Intercept",Destination=point,TargetHypothesis=intercept,Ability=BossAbility.None,
                    Score=score,CreatedAt=Time.time,TerrainVersion=actor.Navigation.Version});
            }
        }
        public bool LegalAt(BossAbility ability,Vector2 point,BossObservation facts,out float chance)
        {
            chance=facts.Confidence;Vector2 delta=facts.Position-point;float distance=delta.magnitude;
            if(facts.Confidence<=0)return false;
            if(ability==BossAbility.Sweep)
            {
                if(distance>actor.Profile.Sweep.Range||!TrySweepBand(point,facts,out _,out _))return false;
                chance+=Mathf.Clamp01(Mathf.Abs(facts.Velocity.x)/7)*.25f;
            }
            else if(ability==BossAbility.Burst)
            {
                if(distance>actor.Profile.Settings(ability).Range||!TryGunTarget(ability,point,facts,out _))return false;
                if(ability==BossAbility.Sweep)chance+=Mathf.Clamp01(Mathf.Abs(facts.Velocity.x)/7)*.25f;
            }
            else if(ability==BossAbility.Piercing)
            {if(distance<3||distance>actor.Profile.Piercing.Range||!TryGunTarget(ability,point,facts,out _))return false;chance+=facts.ReloadVisible?.35f:0;}
            else if(ability==BossAbility.Rhythm)
            {if(distance>actor.Profile.RhythmReach[0]+actor.Profile.RhythmStep[0]+facts.Body.extents.x||Mathf.Abs(delta.y)>1.5f||Projectile.TerrainHit(point,facts.Position,0,out _))return false;}
            else if(ability==BossAbility.DrawDash)
            {
                if(!TrySwordDash(point,facts,out _,out _))return false;
                chance+=facts.Velocity.magnitude<1?.15f:0;
            }
            else if(ability==BossAbility.Guard)
            {
                if(!GuardOpportunity(point,facts,out float urgency))return false;
                chance=1+urgency;
            }
            return true;
        }
    }
}
