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
        public float DropPrepareDelay, DropImpactDelay;
        // Start: supported arrival + resource/ray/range valid. Commit: action Locked.
        // Exit: terrain invalidation, deadline, route failure, lost contact, actor death.
    }
    public sealed class BossTactics
    {
        readonly BossAgent actor;
        readonly List<BossPlan> candidates=new List<BossPlan>(12);
        readonly List<BossPlan> ranked=new List<BossPlan>(64);
        float nextTick,lastAttackAt,nextSpacing,nextAerial,avoidFailedUntil;
        Vector2 failedDestination,lastSearchPoint;
        BossAbility previousAbility;
        BossPlan plan,dropPlan;
        bool dropCooperative,dropLeftGround;
        float dropLaunchedAt,dropDeadline;
        public bool Enabled {get;set;}=true;
        public BossPlan Current=>plan;
        public BossTactics(BossAgent actor){this.actor=actor;}
        public void Tick()
        {
            if(!Enabled)return;
            var action=actor.Actions; var facts=actor.Perception.Facts;
            if(action.Busy)return;
            if(dropPlan!=null){TickDropPlan();return;}
            if(actor.Coordinator!=null&&actor.Coordinator.HasAssignment(actor))return;
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
                if(invalid)
                {
                    if(actor.Motor.RouteFailed){failedDestination=plan.Destination;avoidFailedUntil=Time.time+2;}
                    plan=null;actor.Motor.ClearRoute();
                }
                else if(Vector2.Distance(actor.Position,plan.Destination)<.38f&&actor.Motor.Grounded
                    && (plan.Intent!="AerialPressure"&&plan.Intent!="OpenFireLane"||actor.Motor.RouteCompleted))
                {
                    actor.Motor.Brake();
                    if(plan.Ability!=BossAbility.None&&LegalAt(plan.Ability,actor.Position,facts,out _)
                        &&action.TryStart(plan.Ability,plan.TargetHypothesis))
                    {previousAbility=plan.Ability;lastAttackAt=Time.time;plan=null;return;}
                    if(plan.Ability==BossAbility.None||Time.time-plan.CreatedAt>.8f)plan=null;
                }
                else if(Time.time<nextTick)return;
                else if(plan.Intent=="ResetSpacing"||plan.Intent=="OpenFireLane"||plan.Intent=="AerialPressure")return;
            }
            if(Time.time<nextTick)return;
            nextTick=Time.time+actor.Profile.TacticTick;
            if(facts.Confidence<=0){SearchVisibleAngle();return;}
            if(plan==null && actor.Profile.Kind==BossKind.Severance && Time.time>=nextAerial
                && action.Ready(BossAbility.DropCut) && (Mathf.Abs(facts.Velocity.y)>.5f
                    || Mathf.Abs(facts.Position.y-actor.Position.y)>.4f || action.ConsecutiveMeleeMisses>0)
                && TryPlanDrop(facts,out var aerial))
            {nextAerial=Time.time+1;StartDropPlan(aerial,false);return;}
            if(plan==null && TryBreakStalemate(facts))return;
            var options=BuildCandidates();
            if(options.Count==0){SearchVisibleAngle();return;}
            BossPlan selected=options[0];
            if(plan!=null&&(Time.time-plan.CreatedAt<actor.Profile.PlanHold||selected.Score<plan.Score*1.15f))return;
            plan=selected;plan.CreatedAt=Time.time;
            actor.Trace($"plan {plan.Intent}/{plan.Ability} to={plan.Destination} score={plan.Score:0.00}");
            actor.Motor.SetRoute(plan.Route);
            if(plan.Route==null)actor.Motor.Brake(); // Retire the previous route when accepting an in-place attack.
            if(actor.Profile.Kind==BossKind.Refraction&&plan.TravelSeconds>.45f&&Vector2.Distance(plan.Destination,actor.Position)>1.5f&&selected.Score>.8f&&action.Ready(BossAbility.Camo))
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
                if(distance<1||distance>13||Vector2.Distance(point,lastSearchPoint)<1)continue;
                var facts=actor.Perception.Facts;
                float cost=Vector2.Distance(point,facts.Position)*.6f+distance*.2f;
                if(BossPerception.CanSee(point+Vector2.up*.35f,facts.Body))cost-=4;
                if(cost>=best)continue;
                if(actor.Navigation.TryRoute(actor.Position,point,actor.Motor,out var path)) {best=cost;route=path;}
            }
            if(route!=null){lastSearchPoint=route.Steps[route.Steps.Count-1].End;actor.Motor.SetRoute(route);}
        }
        bool TryBreakStalemate(BossObservation facts)
        {
            if(actor.Profile.Kind!=BossKind.Severance||facts.Confidence<.3f)return false;
            float distance=Vector2.Distance(facts.Position,actor.Position);
            bool yield=actor.Coordinator!=null&&actor.Coordinator.NeedsFireLane(actor);
            if(Time.time>=nextAerial && (yield || actor.Actions.ConsecutiveMeleeMisses>0
                && distance<2.6f && actor.Actions.Ready(BossAbility.DropCut)))
            {
                // A validated jump opens a temporary lane and can lead to the existing aerial cut.
                if(actor.Navigation.TryJumpRoute(actor.Position,actor.Position,actor.Motor,out var jump))
                { nextAerial=Time.time+3;AcceptMovement(yield?"OpenFireLane":"AerialPressure",actor.Position,jump,facts);return true; }
            }
            if(Time.time<nextSpacing || distance>3 || (Mathf.Abs(facts.Position.x-actor.Position.x)>1.15f
                && actor.Actions.ConsecutiveMeleeMisses<2))return false;
            float away=actor.Position.x<facts.Position.x?-1:1;
            float best=float.PositiveInfinity;Vector2 destination=default;ArenaNavigation2D.Route route=null;
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);
                float separation=Mathf.Abs(point.x-facts.Position.x),travel=Vector2.Distance(point,actor.Position);
                if(Mathf.Sign(point.x-actor.Position.x)!=away || separation<2.4f || separation>4
                    || Mathf.Abs(point.y-actor.Position.y)>.15f || travel<.8f || travel>3.5f
                    || actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))continue;
                float cost=Mathf.Abs(separation-3)+travel*.2f;
                if(cost>=best || !actor.Navigation.TryRoute(actor.Position,point,actor.Motor,out var candidate))continue;
                best=cost;destination=point;route=candidate;
            }
            if(route==null)return false;
            nextSpacing=Time.time+2.4f;AcceptMovement("ResetSpacing",destination,route,facts);return true;
        }
        void AcceptMovement(string intent,Vector2 destination,ArenaNavigation2D.Route route,BossObservation facts)
        {
            plan=new BossPlan { Intent=intent,Destination=destination,Route=route,Ability=BossAbility.None,
                TargetHypothesis=facts.Position,Score=3,CreatedAt=Time.time,TerrainVersion=actor.Navigation.Version,
                TravelSeconds=route.Duration,MaxPlanTime=route.Duration+1.2f };
            actor.Motor.SetRoute(route);actor.Trace("plan "+intent+" to="+destination);
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
            if(!current&&Time.time<avoidFailedUntil&&Vector2.Distance(point,failedDestination)<.9f)return;
            if(!current&&new Bounds(point,actor.Motor.Size).Intersects(facts.Body))return;
            if(actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))return;
            BossAbility[] abilities=actor.Profile.Kind==BossKind.Refraction
                ?new[]{BossAbility.Burst,BossAbility.Piercing,BossAbility.Sweep}:new[]{BossAbility.Rhythm,BossAbility.DrawDash,BossAbility.Guard};
            foreach(var ability in abilities)
            {
                if(forced!=BossAbility.None&&ability!=forced)continue;
                if(!actor.Actions.Ready(ability))continue;
                if(actor.Coordinator!=null&&!actor.Coordinator.CanStart(actor,ability,false))continue;
                if(!LegalAt(ability,point,facts,out float chance))continue;
                float distance=Vector2.Distance(point,actor.Position),targetDistance=Mathf.Abs(point.x-facts.Position.x);
                float exposed=facts.Visible&&Vector2.Dot(facts.Aim,(point-facts.Position).normalized)>.8f?1:0;
                float benefit=ability==BossAbility.Piercing?1.15f:ability==BossAbility.Sweep?1.05f:ability==BossAbility.DrawDash?1.18f:ability==BossAbility.Rhythm?1.25f:.95f;
                float risk=exposed*actor.Profile.ExposureWeight*(ability==BossAbility.Piercing?.6f:.2f);
                float preferred=Mathf.Clamp01(1-Mathf.Abs(targetDistance-actor.Profile.DesiredDistance)/(actor.Profile.Kind==BossKind.Refraction?12:4));
                float score=benefit+chance*.7f+preferred*.28f-distance*.055f-risk;
                if(ability==BossAbility.Rhythm)score-=Mathf.Min(2,actor.Actions.ConsecutiveMeleeMisses)*.2f;
                if(ability==previousAbility&&Time.time-lastAttackAt<4)score-=actor.Profile.RepetitionWeight;
                if(!actor.Actions.CanAfford(ability))score-=.45f;
                if(actor.Coordinator!=null)score+=actor.Coordinator.RoleScore(actor,ability,point);
                Vector2 hypothesis=facts.Predict(.16f);
                if(actor.Profile.Kind==BossKind.Refraction)TryGunTarget(ability,point,facts,out hypothesis);
                ranked.Add(new BossPlan {Intent=ability==BossAbility.Guard?"GuardAdvance":distance>.5f?"RepositionAngle":ability==BossAbility.Sweep?"CutExit":"Pressure",
                    Destination=point,TargetHypothesis=hypothesis,Ability=ability,
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
                if(Mathf.Abs(delta.y)>.75f||Mathf.Abs(delta.x)<1.4f||Mathf.Abs(delta.x)>actor.Profile.DrawDash.Range)return false;
                Vector2 end=point+Vector2.right*Mathf.Sign(delta.x)*Mathf.Min(actor.Profile.DrawDash.Range,Mathf.Abs(delta.x)+.7f);
                actor.Navigation.TryDash(point,end,actor.Motor.Size,out var safe);
                if(Vector2.Distance(point,safe)<Mathf.Abs(delta.x)-facts.Body.extents.x-actor.Profile.BladeRadius-.2f)return false;
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
        public Vector2 PredictBody(BossObservation facts,float horizon)
        {
            Vector2 point=facts.Position,velocity=facts.Velocity;
            float dt=Time.fixedDeltaTime;
            for(float t=0;t<Mathf.Min(.65f,horizon);t+=dt)
            {
                if(facts.Grounded&&!actor.Navigation.TrySupport(point,facts.Body.size,out _))facts.Grounded=false;
                if(!facts.Grounded)velocity.y+=Physics2D.gravity.y*3*dt;
                Vector2 next=point+velocity*dt;
                if(!actor.Navigation.SweepTraversal(point,next,facts.Body.size,out var hit,out var safe))
                {
                    point=safe;
                    if(hit.normal.y>.5f&&velocity.y<0){velocity.y=0;facts.Grounded=true;}
                    else if(Mathf.Abs(hit.normal.x)>.5f)velocity.x=0;
                    else velocity.y=0;
                }
                else point=next;
            }
            return point;
        }
        public bool TryGunTarget(BossAbility ability,Vector2 position,BossObservation facts,out Vector2 target)
        {
            float flight=ability==BossAbility.Piercing?actor.Profile.Piercing.Prepare+actor.Profile.Piercing.Lock:Vector2.Distance(position,facts.Position)/actor.Profile.BulletSpeed;
            Vector2 centre=PredictBody(facts,.16f+flight),origin=position+Vector2.up*.28f;
            target=centre;
            // All points remain on the observed body. An exposed head can be shot over a teammate.
            for(int i=0;i<3;i++)
            {
                Vector2 point=centre+Vector2.up*facts.Body.extents.y*(i==0?0:i==1?.72f:-.72f);
                if(AllyBlocks(origin,point) || ability!=BossAbility.Piercing
                    && Projectile.TerrainHit(origin,point,actor.ProjectilePrefab.Radius,out _))continue;
                target=point;return true;
            }
            return false;
        }
        public bool TrySweepBand(Vector2 position,BossObservation facts,out Vector2 from,out Vector2 to)
        {
            from=to=default;
            if(!facts.Visible)return false;
            Vector2 predicted=PredictBody(facts,.35f);
            ArenaNavigation2D.Node best=null;float cost=float.PositiveInfinity;
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 centre=node.Center(facts.Body.size);
                float d=Vector2.Distance(centre,predicted);
                if(d>5||node.Position.y>facts.Body.min.y+2.4f||d>=cost)continue;
                if(!BossPerception.CanSee(position+Vector2.up*.35f,new Bounds(centre,facts.Body.size)))continue;
                cost=d;best=node;
            }
            if(best==null)return false;
            var bounds=best.Collider.bounds;
            float left=bounds.min.x+.12f,right=bounds.max.x-.12f;
            float width=Mathf.Min(actor.Profile.SweepWidth,right-left);
            if(width<.5f)return false;
            float side=actor.Perception.LearnedRetreat!=0?actor.Perception.LearnedRetreat:Mathf.Abs(facts.Velocity.x)>.5f?Mathf.Sign(facts.Velocity.x):Mathf.Sign(facts.Position.x-position.x);
            float mid=Mathf.Clamp(predicted.x+side*width*.2f,left+width*.5f,right-width*.5f);
            from=new Vector2(mid-side*width*.5f,best.Position.y+actor.Profile.SweepHeight);
            to=new Vector2(mid+side*width*.5f,from.y);
            int clear=0;
            for(int i=0;i<actor.Profile.SweepShots;i++)
                if(ClearGunLine(position,Vector2.Lerp(from,to,i/(float)(actor.Profile.SweepShots-1))))clear++;
            return clear>=6;
        }
        public bool TryPlanDrop(BossObservation facts,out BossPlan result)
        {
            result=null;
            if(!actor.Motor.Grounded||!actor.Actions.Ready(BossAbility.DropCut)||facts.Confidence<.3f)return false;
            Vector2 predicted=PredictBody(facts,.6f);
            var choices=new List<ArenaNavigation2D.Node>();
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 centre=node.Center(actor.Motor.Size);
                if(Vector2.Distance(centre,predicted)>3 || Mathf.Abs(centre.x-actor.Position.x)>3
                    || Mathf.Abs(centre.y-actor.Position.y)>2)continue;
                choices.Add(node);
            }
            choices.Sort((a,b)=>Vector2.Distance(a.Center(actor.Motor.Size),predicted).CompareTo(Vector2.Distance(b.Center(actor.Motor.Size),predicted)));
            for(int i=0;i<Mathf.Min(3,choices.Count);i++)
            {
                Vector2 landing=choices[i].Center(actor.Motor.Size);
                if(!actor.Navigation.TryJumpRoute(actor.Position,landing,actor.Motor,out var route))continue;
                // Simulate the launch and the same .28 s braking tell at FixedUpdate resolution.
                Vector2 position=actor.Position,velocity=new Vector2(actor.Velocity.x,actor.Profile.Motion.JumpSpeed);
                float prepare=.18f,tell=actor.Profile.DropCut.Prepare+actor.Profile.DropCut.Lock,dt=Time.fixedDeltaTime;
                bool valid=true;
                for(float t=0;t<prepare+tell;t+=dt)
                {
                    float desired=t<prepare?Mathf.Clamp((landing.x-position.x)/(actor.Profile.Motion.Speed*dt),-1,1)*actor.Profile.Motion.Speed:0;
                    velocity.x=BossMotor2D.StepHorizontal(velocity.x,desired,false,actor.Profile.Motion,dt);
                    velocity.y+=Physics2D.gravity.y*actor.Profile.Motion.GravityScale*dt;
                    Vector2 next=position+velocity*dt;
                    if(!actor.Navigation.SweepTraversal(position,next,actor.Motor.Size,out _,out _)){valid=false;break;}
                    position=next;
                }
                float height=position.y-landing.y,gravity=-Physics2D.gravity.y*actor.Profile.Motion.GravityScale;
                if(!valid||height<.25f)continue;
                float fall=(Mathf.Sqrt(actor.Profile.DropSpeed*actor.Profile.DropSpeed+2*gravity*height)-actor.Profile.DropSpeed)/gravity;
                if(Mathf.Abs(landing.x-position.x)>actor.Profile.Motion.Speed*fall+.12f)continue;
                result=new BossPlan {Intent="PlannedDrop",Ability=BossAbility.DropCut,Destination=actor.Position,TargetHypothesis=landing,
                    Route=route,CreatedAt=Time.time,MaxPlanTime=1.5f,TerrainVersion=actor.Navigation.Version,
                    DropPrepareDelay=prepare,DropImpactDelay=prepare+tell+Time.fixedDeltaTime
                        +(Mathf.Sqrt(actor.Profile.DropSpeed*actor.Profile.DropSpeed+2*gravity*Mathf.Max(0,position.y-actor.Profile.DropCut.Range-landing.y-facts.Body.extents.y))-actor.Profile.DropSpeed)/gravity,Score=2};
                return true;
            }
            return false;
        }
        public bool StartDropPlan(BossPlan value,bool cooperative)
        {
            if(value==null||actor.Actions.Busy||!actor.Motor.Grounded)return false;
            if(actor.Coordinator!=null&&!actor.Coordinator.CanStart(actor,BossAbility.DropCut,cooperative))return false;
            dropPlan=value;dropCooperative=cooperative;dropLeftGround=false;dropDeadline=Time.time+1.6f;plan=null;
            actor.Motor.SetRoute(value.Route);return true;
        }
        void TickDropPlan()
        {
            if(Time.time>dropDeadline||actor.Motor.RouteFailed||dropPlan.TerrainVersion!=actor.Navigation.Version)
            {CancelDropPlan();return;}
            if(!actor.Motor.Grounded&&!dropLeftGround){dropLeftGround=true;dropLaunchedAt=Time.time;}
            if(dropLeftGround&&Time.time>=dropLaunchedAt+dropPlan.DropPrepareDelay)
            {
                Vector2 target=dropPlan.TargetHypothesis;
                if(actor.Actions.TryStart(BossAbility.DropCut,target,dropCooperative)){dropPlan=null;previousAbility=BossAbility.DropCut;lastAttackAt=Time.time;}
                else if(actor.Motor.Grounded||actor.Velocity.y<0)CancelDropPlan();
            }
        }
        public bool DropPlanPending=>dropPlan!=null;
        public void CancelDropPlan(){dropPlan=null;actor.Motor.ClearRoute();}
        public bool CanDropFromHere(Vector2 landing,out float fall,bool atLock=false)
        {
            fall=0;if(actor.Motor.Grounded)return false;
            float before=atLock?actor.Profile.DropCut.Lock:actor.Profile.DropCut.Prepare+actor.Profile.DropCut.Lock;
            if(!PredictDropActivation(before,out var point))return false;
            float height=point.y-landing.y,g=-Physics2D.gravity.y*actor.Profile.Motion.GravityScale;
            if(height<.15f)return false;
            fall=(Mathf.Sqrt(actor.Profile.DropSpeed*actor.Profile.DropSpeed+2*g*height)-actor.Profile.DropSpeed)/g;
            if(Mathf.Abs(point.x-landing.x)>actor.Profile.Motion.Speed*fall+.15f)return false;
            return actor.Navigation.CanStand(landing,actor.Motor.Size);
        }
        public bool PredictDropActivation(float before,out Vector2 point)
        {
            point=actor.Position;Vector2 velocity=actor.Velocity;
            for(float t=0;t<before;t+=Time.fixedDeltaTime)
            {
                velocity.x=BossMotor2D.StepHorizontal(velocity.x,0,false,actor.Profile.Motion,Time.fixedDeltaTime);
                velocity.y+=Physics2D.gravity.y*actor.Profile.Motion.GravityScale*Time.fixedDeltaTime;
                Vector2 next=point+velocity*Time.fixedDeltaTime;
                if(!actor.Navigation.SweepTraversal(point,next,actor.Motor.Size,out _,out _))return false;
                point=next;
            }
            return true;
        }
        public bool ClearGunLine(Vector2 position,Vector2 target)=>!Projectile.TerrainHit(position+Vector2.up*.28f,target,actor.ProjectilePrefab.Radius,out _)&&!AllyBlocks(position+Vector2.up*.28f,target);
        public bool AllyBlocks(Vector2 from,Vector2 target)
        {
            if(actor.Coordinator==null)return false;
            Vector2 delta=target-from;
            foreach(var hit in Physics2D.CircleCastAll(from,actor.ProjectilePrefab.Radius,delta.normalized,delta.magnitude,1<<actor.gameObject.layer))
                foreach(var other in actor.Coordinator.Actors)if(other!=actor&&other.IsAlive&&hit.collider==other.Body)return true;
            return false;
        }
    }
}
