using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Geometry-derived jump, flank and drop plans, including committed drop lifecycle.
    public sealed partial class BossTactics
    {
        public bool TryPlanMobility(BossAbility ability,BossObservation facts,out BossPlan result)
        {
            using(MobilitySample.Auto())
            {BeginPredictions();try{return FindMobility(ability,facts,out result);}finally{EndPredictions();}}
        }
        static readonly Unity.Profiling.ProfilerMarker MobilitySample=new Unity.Profiling.ProfilerMarker("Boss.Mobility");
        bool FindMobility(BossAbility ability,BossObservation facts,out BossPlan result)
        {
            result=null;
            if(!actor.Motor.Grounded || facts.Confidence<.3f || !actor.Actions.Ready(ability)
                || actor.Coordinator!=null&&!actor.Coordinator.CanStart(actor,ability,false))return false;
            bool sword=ability==BossAbility.LeapSlash;
            float width=actor.Navigation.PlatformWidth,spacing=actor.Navigation.SameLevelSpacing;
            float height=ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile);
            Vector2 target=sword?PredictSwordBody(facts,.7f):PredictBody(facts,.5f),delta=target-actor.Position;
            bool blocked=!TryGunTarget(BossAbility.Burst,actor.Position,facts,out _);
            if(sword)
            {
                if(SwordAvailable(BossAbility.Rhythm)&&SwordImmediateScore(BossAbility.Rhythm,actor.Position,facts)>=3.5f)return false;
            }
            else if(!blocked && Mathf.Abs(delta.y)<height*.3f && Mathf.Abs(delta.x)>width*.8f
                && (plan==null||plan.TravelSeconds<.3f))return false;
            var choices=new List<ArenaNavigation2D.Node>();
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);
                float g=Mathf.Abs(Physics2D.gravity.y*actor.Motor.Profile.GravityScale);
                float discriminant=actor.Motor.Profile.JumpSpeed*actor.Motor.Profile.JumpSpeed-2*g*(point.y-actor.Position.y);
                float flight=discriminant>=0?(actor.Motor.Profile.JumpSpeed+Mathf.Sqrt(discriminant))/g:0;
                if(Mathf.Abs(point.x-actor.Position.x)>spacing || point.y-actor.Position.y>height
                    || Mathf.Abs(point.x-actor.Position.x)>actor.Motor.Profile.Speed*flight
                    || actor.Position.y-point.y>height || Vector2.Distance(point,actor.Position)<actor.Motor.Size.x
                    || new Bounds(point,actor.Motor.Size).Intersects(facts.Body)
                    || actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))continue;
                choices.Add(node);
            }
            choices.Sort((a,b)=>MobilityScore(b.Center(actor.Motor.Size),target,sword,width,spacing)
                .CompareTo(MobilityScore(a.Center(actor.Motor.Size),target,sword,width,spacing)));
            float bestPlanScore=float.NegativeInfinity;
            for(int i=0;i<Mathf.Min(sword?8:12,choices.Count);i++)
            {
                Vector2 landing=choices[i].Center(actor.Motor.Size);
                if(sword && Vector2.Distance(landing,target)>delta.magnitude-actor.Motor.Size.x*.5f)continue;
                if(!actor.Navigation.TryJumpTrajectory(actor.Position,landing,actor.Motor,out var route,out var path))continue;
                int slash=1;float best=float.PositiveInfinity;bool gunLine=false;
                for(int j=1;j<path.Length-1;j+=sword?3:1)
                {
                    float t=j*Time.fixedDeltaTime;
                    if(t<.12f || t>(path.Length-1)*Time.fixedDeltaTime-actor.Profile.LeapSlash.Active)continue;
                    Vector2 atImpact=sword?PredictSwordBody(facts,actor.Profile.LeapSlash.Prepare+actor.Profile.LeapSlash.Lock+t):target;
                    Vector2 toBody=atImpact-path[j];
                    float distance=toBody.magnitude;
                    // The first cut is a forward arc, not a damage circle around the jump.
                    if(sword&&(Mathf.Sign(toBody.x)!=Mathf.Sign(target.x-actor.Position.x)
                        ||Mathf.Abs(toBody.y)>Mathf.Abs(toBody.x)*.55f+facts.Body.extents.y))continue;
                    if(distance<best){best=distance;slash=j;}
                    if(!sword&&TryGunTarget(BossAbility.Burst,path[j],facts,out _))gunLine=true;
                }
                if(!sword&&!gunLine)continue;
                if(sword&&best>actor.Profile.RhythmReach[0]+facts.Body.extents.x+actor.Profile.BladeRadius)continue;
                float score=sword?-best-Vector2.Distance(landing,PredictSwordBody(facts,(path.Length-1)*Time.fixedDeltaTime+.36f))*.3f:2;
                if(score<=bestPlanScore)continue;
                bestPlanScore=score;
                result=new BossPlan { Intent=sword?"LeapClose":"JumpAngle",Ability=ability,Destination=landing,
                    TargetHypothesis=target,Route=route,JumpPath=path,SlashOffset=slash*Time.fixedDeltaTime,
                    TerrainVersion=actor.Navigation.Version,CreatedAt=Time.time,MaxPlanTime=route.Duration+1,
                    TravelSeconds=route.Duration,Score=2 };
                if(!sword)return true;
            }
            return result!=null;
        }
        public bool TryPlanFlank(BossObservation facts,out BossPlan result)
        {
            result=null;
            if(actor.Profile.Kind!=BossKind.Severance||!actor.Motor.Grounded||!facts.Visible||facts.Confidence<.45f
                ||!actor.Actions.Ready(BossAbility.FlankLeap)||!actor.Actions.Ready(BossAbility.Rhythm)
                ||actor.Coordinator!=null&&!actor.Coordinator.CanStart(actor,BossAbility.FlankLeap,false))return false;
            float distance=Vector2.Distance(actor.Position,facts.Position),width=actor.Navigation.PlatformWidth;
            if(distance<width*.45f||distance>actor.Navigation.SameLevelSpacing)return false;
            // Public weapon facing defines the back. Never read input, permissions or a hidden transform.
            if(Mathf.Abs(facts.Aim.x)<.25f)return false;
            float back=-Mathf.Sign(facts.Aim.x);
            if((actor.Position.x-facts.Position.x)*back>-.35f)return false;
            var motion=actor.Profile.FlankMotion;
            Vector2 expected=PredictSwordBody(facts,.95f);
            var choices=new List<Vector2>();
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);
                float left=node.Collider.bounds.min.x+actor.Motor.Size.x*.5f+.05f;
                float right=node.Collider.bounds.max.x-actor.Motor.Size.x*.5f-.05f;
                if(left>right)continue;
                point.x=Mathf.Clamp(expected.x+back*width*.4f,left,right);
                if(Mathf.Abs(point.x-actor.Position.x)>actor.Navigation.SameLevelSpacing
                    ||Mathf.Abs(point.y-actor.Position.y)>ArenaNavigation2D.ExecutableJumpHeight(motion)
                    ||(point.x-facts.Position.x)*back<actor.Motor.Size.x
                    ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))continue;
                AddDropPoint(choices,point);
            }
            choices.Sort((a,b)=>Vector2.Distance(a,expected+Vector2.right*back*width*.4f)
                .CompareTo(Vector2.Distance(b,expected+Vector2.right*back*width*.4f)));
            float best=float.PositiveInfinity;
            for(int i=0;i<Mathf.Min(8,choices.Count);i++)
            {
                Vector2 landing=choices[i];
                if(!actor.Navigation.TryJumpTrajectory(actor.Position,landing,motion,actor.Motor.Size,out var route,out var path))continue;
                float flight=(path.Length-1)*Time.fixedDeltaTime;
                Vector2 target=PredictSwordBody(facts,actor.Profile.FlankLeap.Prepare+actor.Profile.FlankLeap.Lock+flight);
                float behind=(landing.x-target.x)*back;
                if(behind<actor.Motor.Size.x||behind>actor.Profile.RhythmReach[0]+facts.Body.extents.x
                    ||Mathf.Abs(landing.y-target.y)>1.2f)continue;
                bool blocked=false;
                for(int j=1;j<path.Length;j+=3)
                {
                    Vector2 body=PredictSwordBody(facts,actor.Profile.FlankLeap.Prepare+actor.Profile.FlankLeap.Lock+j*Time.fixedDeltaTime);
                    if(new Bounds(path[j],actor.Motor.Size-Vector2.one*.04f).Intersects(new Bounds(body,facts.Body.size))
                        ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,path[j])){blocked=true;break;}
                }
                float score=Mathf.Abs(behind-width*.4f)+flight*.25f;
                if(blocked||score>=best)continue;
                best=score;result=new BossPlan {Intent="FlankPursuit",Ability=BossAbility.FlankLeap,Destination=landing,
                    TargetHypothesis=target,Route=route,JumpPath=path,JumpMotion=motion,CreatedAt=Time.time,
                    TerrainVersion=actor.Navigation.Version,TravelSeconds=flight,MaxPlanTime=route.Duration+1};
            }
            return result!=null;
        }
        float MobilityScore(Vector2 point,Vector2 target,bool sword,float width,float spacing)
        {
            float travel=Vector2.Distance(point,actor.Position);
            if(sword)return -Vector2.Distance(point,target)-travel*.15f;
            return -Mathf.Abs(Mathf.Abs(point.x-target.x)-spacing*.8f)-travel*.2f
                + Mathf.Min(width,point.y-actor.Position.y)*.5f
                + (ClearGunLine(point,target)?width:0);
        }
        public bool TrySweepBand(Vector2 position,BossObservation facts,out Vector2 from,out Vector2 to)
        {
            from=to=default;
            if(!facts.Visible)return false;
            float horizon=actor.Profile.Sweep.Prepare+actor.Profile.Sweep.Lock
                +Vector2.Distance(position,facts.Position)/actor.Profile.BulletSpeed;
            Vector2 predicted=PredictBody(facts,horizon);
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
            Vector2 predicted=PredictSwordBody(facts,.6f);
            var choices=new List<Vector2>();
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 centre=node.Center(actor.Motor.Size);
                if(Vector2.Distance(centre,predicted)>actor.Navigation.PlatformWidth
                    || Mathf.Abs(centre.x-actor.Position.x)>actor.Navigation.PlatformWidth
                    || Mathf.Abs(centre.y-actor.Position.y)>ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile))continue;
                AddDropPoint(choices,centre);
                // Continuous support point near the predicted body, rather than a coarse graph node.
                centre.x=Mathf.Clamp(predicted.x,node.Collider.bounds.min.x+actor.Motor.Size.x*.5f+.03f,
                    node.Collider.bounds.max.x-actor.Motor.Size.x*.5f-.03f);
                if(actor.Navigation.CanStand(centre,actor.Motor.Size)
                    &&actor.Navigation.TrySupport(centre,actor.Motor.Size,out var support)&&support==node.Collider)
                    AddDropPoint(choices,centre);
            }
            choices.Sort((a,b)=>Vector2.Distance(a,predicted).CompareTo(Vector2.Distance(b,predicted)));
            float bestError=float.PositiveInfinity;
            for(int i=0;i<Mathf.Min(12,choices.Count);i++)
            {
                Vector2 landing=choices[i];
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
                if(Mathf.Abs(landing.x-position.x)>actor.Profile.Motion.Speed*fall+.03f
                    ||!DropDescentClear(position,landing))continue;
                float contactFall=(Mathf.Sqrt(actor.Profile.DropSpeed*actor.Profile.DropSpeed+2*gravity
                    *Mathf.Max(0,position.y-actor.Profile.DropCut.Range-landing.y-facts.Body.extents.y))-actor.Profile.DropSpeed)/gravity;
                Vector2 intercept=PredictSwordBody(facts,prepare+tell+contactFall);
                float error=Mathf.Abs(landing.x-intercept.x)+Mathf.Abs(landing.y-intercept.y)*.3f;
                if(error>=bestError||error>actor.Navigation.PlatformWidth*.6f)continue;
                bestError=error;
                result=new BossPlan {Intent="PlannedDrop",Ability=BossAbility.DropCut,Destination=actor.Position,TargetHypothesis=landing,
                    Route=route,CreatedAt=Time.time,MaxPlanTime=1.5f,TerrainVersion=actor.Navigation.Version,
                    DropPrepareDelay=prepare,DropImpactDelay=prepare+tell+Time.fixedDeltaTime
                        +contactFall,Score=2-error*.3f};
            }
            return result!=null;
        }
        static void AddDropPoint(List<Vector2> points,Vector2 point)
        {foreach(var existing in points)if(Vector2.Distance(existing,point)<.1f)return;points.Add(point);}
        public bool TryRefineDropTarget(BossObservation facts,Vector2 original,float before,out Vector2 target)
        {
            target=original;
            if(!facts.Visible||facts.Confidence<.3f||!PredictDropActivation(before,out var origin))return false;
            float gravity=-Physics2D.gravity.y*actor.Profile.Motion.GravityScale;
            float best=float.PositiveInfinity;
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);
                if(Mathf.Abs(point.y-original.y)>.15f)continue;
                float height=origin.y-point.y;
                if(height<.15f)continue;
                float fall=(Mathf.Sqrt(actor.Profile.DropSpeed*actor.Profile.DropSpeed+2*gravity*height)-actor.Profile.DropSpeed)/gravity;
                float hitFall=(Mathf.Sqrt(actor.Profile.DropSpeed*actor.Profile.DropSpeed+2*gravity
                    *Mathf.Max(0,height-actor.Profile.DropCut.Range-facts.Body.extents.y))-actor.Profile.DropSpeed)/gravity;
                Vector2 intercept=PredictSwordBody(facts,before+hitFall);
                float reach=actor.Profile.Motion.Speed*fall;
                float left=Mathf.Max(node.Collider.bounds.min.x+actor.Motor.Size.x*.5f+.03f,origin.x-reach,original.x-actor.Navigation.PlatformWidth*.5f);
                float right=Mathf.Min(node.Collider.bounds.max.x-actor.Motor.Size.x*.5f-.03f,origin.x+reach,original.x+actor.Navigation.PlatformWidth*.5f);
                if(left>right)continue;
                point.x=Mathf.Clamp(intercept.x,left,right);
                float error=Mathf.Abs(point.x-intercept.x);
                if(error>=best||!actor.Navigation.CanStand(point,actor.Motor.Size)
                    ||!actor.Navigation.TrySupport(point,actor.Motor.Size,out var support)||support!=node.Collider
                    ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point)||!DropDescentClear(origin,point))continue;
                best=error;target=point;
            }
            return best<float.PositiveInfinity&&Vector2.Distance(target,original)>.03f;
        }
        bool DropDescentClear(Vector2 origin,Vector2 landing)
        {
            if(!actor.Navigation.TrySupport(landing,actor.Motor.Size,out var support))return false;
            float gravity=-Physics2D.gravity.y*actor.Profile.Motion.GravityScale,height=origin.y-landing.y;
            if(height<=0)return false;
            float fall=(Mathf.Sqrt(actor.Profile.DropSpeed*actor.Profile.DropSpeed+2*gravity*height)-actor.Profile.DropSpeed)/gravity;
            float horizontal=(landing.x-origin.x)/Mathf.Max(.02f,fall);
            if(Mathf.Abs(horizontal)>actor.Profile.Motion.Speed+.05f)return false;
            Vector2 point=origin,velocity=new Vector2(horizontal,-actor.Profile.DropSpeed);
            for(float t=0;t<fall+Time.fixedDeltaTime*2;t+=Time.fixedDeltaTime)
            {
                velocity.y-=gravity*Time.fixedDeltaTime;Vector2 next=point+velocity*Time.fixedDeltaTime;
                if(!actor.Navigation.SweepTraversal(point,next,actor.Motor.Size,out var hit,out var safe))
                    return hit.collider==support&&hit.normal.y>.5f&&Mathf.Abs(safe.x-landing.x)<.3f;
                point=next;
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
            return actor.Navigation.CanStand(landing,actor.Motor.Size)&&DropDescentClear(point,landing);
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
    }
}
