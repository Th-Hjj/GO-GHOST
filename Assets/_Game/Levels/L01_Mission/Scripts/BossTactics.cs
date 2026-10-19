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
        public Vector2[] JumpPath;
        public BossMotionProfile JumpMotion;
        public float SlashOffset;
        // Start: supported arrival + resource/ray/range valid. Commit: action Locked.
        // Exit: terrain invalidation, deadline, route failure, lost contact, actor death.
    }
    public sealed class BossTactics
    {
        readonly BossAgent actor;
        readonly PublicPrediction gunPrediction=new PublicPrediction(),swordPrediction=new PublicPrediction();
        int predictionDepth;
        sealed class PublicPrediction
        {
            public readonly List<Vector2> Points=new List<Vector2>(65);
            public BossObservation Original,Facts;
            public Vector2 Point,Velocity;
            public float Time;
        }
        void BeginPredictions()
        {
            if(predictionDepth++==0)
            {gunPrediction.Points.Clear();swordPrediction.Points.Clear();actor.Navigation.BeginRouteBatch();}
        }
        void EndPredictions(){if(--predictionDepth==0)actor.Navigation.EndRouteBatch();}
        readonly List<BossPlan> candidates=new List<BossPlan>(12);
        readonly List<BossPlan> ranked=new List<BossPlan>(64);
        readonly Dictionary<Vector2,ArenaNavigation2D.Route> candidateRoutes=new Dictionary<Vector2,ArenaNavigation2D.Route>();
        readonly List<RaycastHit2D> allyHits=new List<RaycastHit2D>(16);
        float nextTick,lastAttackAt,nextSpacing,nextAerial,avoidFailedUntil;
        Vector2 failedDestination,lastSearchPoint;
        readonly HashSet<Collider2D> searchedSupports=new HashSet<Collider2D>();
        struct SearchCandidate
        {
            public Vector2 Point;
            public Collider2D Support;
            public float Cost;
            public int Order;
        }
        readonly List<SearchCandidate> searchCandidates=new List<SearchCandidate>(64);
        Collider2D searchSupport;
        float searchDeadline;
        BossAbility previousAbility;
        BossPlan plan,dropPlan;
        BossPlan camoPlan;
        Vector2 lastCamoDestination;
        float camoFollowupUntil,lastCamoAt=-100;
        bool camoTravelling;
        bool dropCooperative,dropLeftGround;
        float dropLaunchedAt,dropDeadline;
        BossPlan recoveryPlan;
        float exposedSince=-1,nextGunMove,nextRecoveryStep;
        Vector2 lastGunOrigin;
        public int RecoveryPlansUsed {get;private set;}
        public int GunRepositions {get;private set;}
        public int InterceptPlansUsed {get;private set;}
        public int PursuitDashes {get;private set;}
        public bool Enabled {get;set;}=true;
        public BossPlan Current=>plan;
        public bool CamoRouteInvalid=>camoTravelling&&(camoPlan==null
            ||camoPlan.TerrainVersion!=actor.Navigation.Version||actor.Motor.RouteFailed
            ||!actor.Motor.HasRoute&&!actor.Motor.RouteCompleted);
        public float EngagementDistance=>actor.Profile.Kind==BossKind.Refraction?actor.Navigation.SameLevelSpacing*.8f:actor.Navigation.PlatformWidth*.4f;
        public BossTactics(BossAgent actor){this.actor=actor;}
        public void Tick()
        {
            if(!Enabled)return;
            if(actor.Profile.Kind==BossKind.Severance&&dropPlan==null
                &&(actor.Coordinator==null||!actor.Coordinator.HasAssignment(actor)))
            {actor.Actions.TickSwordDecision();return;}
            var action=actor.Actions; var facts=actor.Perception.Facts;
            bool exposed=facts.Visible&&Time.time-facts.LastShotAt<.55f
                &&Vector2.Dot(facts.Aim,(actor.Position-facts.Position).normalized)>.9f;
            if(!exposed)exposedSince=-1;else if(exposedSince<0)exposedSince=Time.time;
            if(action.Busy)
            {
                if(action.SafeRecovery&&actor.Motor.Grounded&&Time.time>=nextTick
                    &&(actor.Coordinator==null||!actor.Coordinator.HasAssignment(actor)))
                {
                    nextTick=Time.time+actor.Profile.TacticTick;
                    if(actor.Profile.Kind==BossKind.Severance)recoveryPlan=SelectSwordPressure(facts);
                    else {var next=BuildCandidates();recoveryPlan=next.Count>0?next[0]:null;}
                }
                return;
            }
            if(dropPlan!=null){TickDropPlan();return;}
            if(actor.Coordinator!=null&&actor.Coordinator.HasAssignment(actor))
            {if(camoPlan!=null){if(plan==camoPlan)plan=null;camoPlan=null;}return;}
            if(!actor.Motor.Grounded)return; // Finish the accepted physical air connection before replanning.
            if(TickCamoFollowup(facts))return;
            if(facts.Visible&&facts.Confidence>.3f)
            {searchedSupports.Clear();searchSupport=null;}
            if(TryGunReposition(facts)){recoveryPlan=null;return;}
            // Compare current opportunities together. Re-evaluate a recovery preview against fresh
            // public facts and the actual recovered body; it never cancels recovery or an air route.
            if(actor.Profile.Kind==BossKind.Severance&&(Time.time>=nextTick||recoveryPlan!=null))
            {
                bool previewed=recoveryPlan!=null;recoveryPlan=null;
                var pressure=SelectSwordPressure(facts);
                if(pressure!=null&&StartSwordPressure(pressure,facts))
                {if(previewed)RecoveryPlansUsed++;return;}
            }
            if(recoveryPlan!=null)
            {
                var ready=recoveryPlan;recoveryPlan=null;
                if(ready.TerrainVersion==actor.Navigation.Version&&facts.Visible&&Time.time-ready.CreatedAt<.5f
                    &&LegalAt(ready.Ability,actor.Position,facts,out _)&&TryImmediate(ready.Ability,facts))
                {RecoveryPlansUsed++;return;}
                // Rebuild a route from the actual recovered position; never reuse an old takeoff.
                nextTick=Time.time;
            }
            if(plan!=null&&plan.Intent=="GunReposition"&&!actor.Motor.RouteFailed
                &&plan.TerrainVersion==actor.Navigation.Version&&Time.time<plan.CreatedAt+plan.MaxPlanTime
                &&actor.Motor.HasRoute&&facts.Confidence>.3f)return;
            if(Time.time>=nextTick && facts.Confidence>.3f)
            {
                if(actor.Profile.Kind==BossKind.Refraction&&facts.Visible&&facts.ReloadVisible&&TryImmediate(BossAbility.Piercing,facts))return;
                if(TryStartCamo())
                {nextTick=Time.time+actor.Profile.TacticTick;return;}
                if(actor.Profile.Kind==BossKind.Refraction&&action.Ready(BossAbility.TacticalJumpShot)
                    &&TryPlanMobility(BossAbility.TacticalJumpShot,facts,out var jump)&&action.TryStartMobility(jump))
                {plan=null;previousAbility=BossAbility.TacticalJumpShot;lastAttackAt=Time.time;nextTick=Time.time+actor.Profile.TacticTick;return;}
            }
            if(plan!=null)
            {
                // A new expressed jump can invalidate a ground pursuit before its old hold expires.
                if(actor.Profile.Kind==BossKind.Severance&&plan.Intent=="Approach/Intercept"&&facts.Visible&&Time.time>=nextTick
                    &&Time.time-plan.CreatedAt>.25f
                    &&Vector2.Distance(PredictSwordBody(facts,plan.TravelSeconds),plan.TargetHypothesis)>actor.Navigation.PlatformWidth*.45f)
                {plan=null;actor.Motor.ClearRoute();nextTick=Time.time;}
            }
            if(plan!=null)
            {
                // A solid body can stop arrival while the accepted attack is already legal here.
                // Only recover a failed route this way; successful flanking routes keep their destination.
                if(actor.Motor.RouteFailed&&plan.TerrainVersion==actor.Navigation.Version
                    &&plan.Ability!=BossAbility.None&&LegalAt(plan.Ability,actor.Position,facts,out _)
                    &&(actor.Profile.Kind!=BossKind.Severance||SwordImmediateScore(plan.Ability,actor.Position,facts)>0)
                    &&action.TryStart(plan.Ability,actor.Profile.Kind==BossKind.Severance?SwordTarget(plan.Ability,actor.Position,facts):plan.TargetHypothesis))
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
                        &&(actor.Profile.Kind!=BossKind.Severance||SwordImmediateScore(plan.Ability,actor.Position,facts)>0)
                        &&action.TryStart(plan.Ability,actor.Profile.Kind==BossKind.Severance?SwordTarget(plan.Ability,actor.Position,facts):plan.TargetHypothesis))
                    {previousAbility=plan.Ability;lastAttackAt=Time.time;plan=null;return;}
                    if(plan.Ability==BossAbility.None||Time.time-plan.CreatedAt>.8f)plan=null;
                }
                else if(Time.time<nextTick)return;
                else if(plan.Intent=="ResetSpacing"||plan.Intent=="OpenFireLane"||plan.Intent=="AerialPressure")return;
            }
            if(Time.time<nextTick)return;
            nextTick=Time.time+actor.Profile.TacticTick;
            if(facts.Confidence<=0){SearchVisibleAngle();return;}
            if(plan==null && TryBreakStalemate(facts))return;
            var options=BuildCandidates();
            if(options.Count==0){SearchVisibleAngle();return;}
            BossPlan selected=options[0];
            bool chasing=actor.Profile.Kind==BossKind.Severance&&plan?.Intent=="Approach/Intercept";
            if(plan!=null&&(Time.time-plan.CreatedAt<(chasing?Mathf.Min(.35f,actor.Profile.PlanHold):actor.Profile.PlanHold)
                ||selected.Score<(actor.Profile.Kind==BossKind.Severance
                    ?plan.Score+Mathf.Max(.08f,Mathf.Abs(plan.Score)*(chasing?.05f:.15f)):plan.Score*1.15f)))return;
            plan=selected;plan.CreatedAt=Time.time;
            if(plan.Intent=="Approach/Intercept")InterceptPlansUsed++;
            actor.Trace($"plan {plan.Intent}/{plan.Ability} to={plan.Destination} score={plan.Score:0.00}");
            actor.Motor.SetRoute(plan.Route);
            if(plan.Route==null)actor.Motor.Brake(); // Retire the previous route when accepting an in-place attack.
            if(actor.Profile.Kind==BossKind.Refraction&&!action.CanAfford(plan.Ability))action.RequestReload();
        }
        BossPlan SelectSwordPressure(BossObservation facts)
        {
            if(!facts.Visible||facts.Confidence<.3f)return null;
            BossPlan best=null;
            foreach(var ability in new[]{BossAbility.Rhythm,BossAbility.DrawDash,BossAbility.Guard})
            {
                if(!SwordAvailable(ability)||!LegalAt(ability,actor.Position,facts,out _))continue;
                float score=SwordImmediateScore(ability,actor.Position,facts);
                if(score<=0||best!=null&&score<=best.Score)continue;
                best=new BossPlan {Ability=ability,Score=score,Destination=actor.Position,
                    TargetHypothesis=SwordTarget(ability,actor.Position,facts),CreatedAt=Time.time,TerrainVersion=actor.Navigation.Version};
            }
            // A reachable immediate cut or an imminent reflection wins over a longer maneuver.
            if(best!=null&&best.Score>=3.5f)return best;
            if(SwordAvailable(BossAbility.LeapSlash)&&TryPlanMobility(BossAbility.LeapSlash,facts,out var leap))
                ConsiderSwordMobility(ref best,leap,facts);
            if(SwordAvailable(BossAbility.FlankLeap)&&TryPlanFlank(facts,out var flank))
                ConsiderSwordMobility(ref best,flank,facts);
            if(Time.time>=nextAerial&&SwordAvailable(BossAbility.DropCut)
                &&(Mathf.Abs(facts.Position.y-actor.Position.y)>.4f||Mathf.Abs(facts.Velocity.y)>.5f||actor.Actions.ConsecutiveMeleeMisses>0)
                &&TryPlanDrop(facts,out var drop))ConsiderSwordMobility(ref best,drop,facts);
            return best;
        }
        bool SwordAvailable(BossAbility ability)=>actor.Actions.Ready(ability)&&actor.Actions.CanAfford(ability)
            &&(actor.Coordinator==null||actor.Coordinator.CanStart(actor,ability,false));
        void ConsiderSwordMobility(ref BossPlan best,BossPlan option,BossObservation facts)
        {
            var settings=actor.Profile.Settings(option.Ability);
            float arrival=option.Ability==BossAbility.DropCut?option.DropImpactDelay:settings.Prepare+settings.Lock+option.TravelSeconds;
            float impact=option.Ability==BossAbility.LeapSlash?settings.Prepare+settings.Lock+option.SlashOffset
                :option.Ability==BossAbility.DropCut?option.DropImpactDelay:arrival+.36f;
            option.Score=2.5f-.8f*impact+SwordOpening(facts,impact)
                +SwordExitScore(option.Ability==BossAbility.DropCut?option.TargetHypothesis:option.Destination,facts,arrival);
            if(option.Ability==BossAbility.FlankLeap)option.Score-=.25f;
            if(option.Ability==previousAbility&&Time.time-lastAttackAt<4)option.Score-=actor.Profile.RepetitionWeight;
            if(option.Score>0&&(best==null||option.Score>best.Score))best=option;
        }
        bool StartSwordPressure(BossPlan selected,BossObservation facts)
        {
            bool started=selected.Ability==BossAbility.DropCut?StartDropPlan(selected,false)
                :selected.JumpPath!=null?actor.Actions.TryStartMobility(selected):TryImmediate(selected.Ability,facts);
            if(!started)return false;
            plan=null;previousAbility=selected.Ability;lastAttackAt=Time.time;nextTick=Time.time+actor.Profile.TacticTick;
            if(selected.Ability==BossAbility.DrawDash)PursuitDashes++;
            if(selected.Ability==BossAbility.DropCut)nextAerial=Time.time+1;
            actor.Trace($"pressure {selected.Ability} score={selected.Score:F2}");return true;
        }
        float SwordOpening(BossObservation facts,float impact)
        {
            // Recent *observed* dodge motion is a short opportunity, not access to its cooldown.
            float opening=facts.ReloadVisible?.8f:0;
            if(Time.time-actor.Perception.LastDodgeObservedAt<.65f)opening+=.55f;
            return opening/(1+impact);
        }
        float SwordImmediateScore(BossAbility ability,Vector2 from,BossObservation facts,bool evaluateExit=true)
        {
            if(ability==BossAbility.Guard)return GuardOpportunity(from,facts,out float urgency)?4+urgency:float.NegativeInfinity;
            var p=actor.Profile;var settings=p.Settings(ability);
            float impact=settings.Prepare+settings.Lock;
            Vector2 target=SwordTarget(ability,from,facts),end=from;
            if(ability==BossAbility.Rhythm)
            {
                if(Vector2.Distance(from,target)>p.RhythmReach[0]+p.RhythmStep[0]+facts.Body.extents.x
                    ||Mathf.Abs(target.y-from.y)>1.5f||Projectile.TerrainHit(from,target,0,out _))return float.NegativeInfinity;
                return 3.8f-.8f*impact+SwordOpening(facts,impact);
            }
            if(!TrySwordDash(from,facts,out target,out end))return float.NegativeInfinity;
            impact+=Mathf.Min(Vector2.Distance(from,target),Vector2.Distance(from,end))/p.DashSpeed;
            float score=2.9f-.8f*impact+SwordOpening(facts,impact)+(evaluateExit?SwordExitScore(end,facts,impact,true):0);
            if(ability==previousAbility&&Time.time-lastAttackAt<4)score-=p.RepetitionWeight;
            return score;
        }
        float SwordExitScore(Vector2 end,BossObservation facts,float arrival,bool falling=false)
        {
            float fall=0;Vector2 velocity=Vector2.zero;
            if(falling)
                for(;fall<1.2f&&!actor.Navigation.TrySupport(end,actor.Motor.Size,out _);fall+=Time.fixedDeltaTime)
                {
                    velocity.y+=Physics2D.gravity.y*actor.Motor.Profile.GravityScale*Time.fixedDeltaTime;
                    Vector2 next=end+velocity*Time.fixedDeltaTime;
                    if(!actor.Navigation.SweepTraversal(end,next,actor.Motor.Size,out var hit,out var safe))
                    {end=safe;if(hit.normal.y>.5f)break;velocity=Vector2.zero;}
                    else end=next;
                }
            Vector2 nextTarget=PredictSwordBody(facts,arrival+fall+actor.Profile.Rhythm.Prepare+actor.Profile.Rhythm.Lock);
            float gap=Vector2.Distance(end,nextTarget);
            bool close=gap<=actor.Profile.RhythmReach[0]+facts.Body.extents.x&&Mathf.Abs(end.y-nextTarget.y)<1.5f
                &&!Projectile.TerrainHit(end,nextTarget,0,out _);
            return (close?.65f:-Mathf.Min(gap,8)*.12f)-fall*.45f;
        }
        bool TryImmediate(BossAbility ability,BossObservation facts)
        {
            if(ability==BossAbility.None||!actor.Actions.Ready(ability)||!actor.Actions.CanAfford(ability)
                ||!LegalAt(ability,actor.Position,facts,out _))return false;
            if(actor.Profile.Kind==BossKind.Severance&&(ability==BossAbility.Rhythm||ability==BossAbility.DrawDash||ability==BossAbility.Guard)
                &&SwordImmediateScore(ability,actor.Position,facts)<=0)return false;
            Vector2 target=actor.Profile.Kind==BossKind.Severance?SwordTarget(ability,actor.Position,facts):PredictBody(facts,.16f);
            if(actor.Profile.Kind==BossKind.Refraction&&!TryGunTarget(ability,actor.Position,facts,out target))return false;
            if(!actor.Actions.TryStart(ability,target))return false;
            plan=null;previousAbility=ability;lastAttackAt=Time.time;nextTick=Time.time+actor.Profile.TacticTick;return true;
        }

        public bool TrySwordRecoveryStep(out float displacement)
        {
            displacement=0;var facts=actor.Perception.Facts;
            if(!Enabled||actor.Profile.Kind!=BossKind.Severance||!actor.Motor.Grounded
                ||Time.time<nextRecoveryStep||!facts.Visible||facts.Confidence<.3f
                ||actor.Coordinator!=null&&actor.Coordinator.Phase!=DuoPhase.Idle)return false;
            Vector2 target=PredictSwordBody(facts,actor.Actions.RecoveryRemaining+actor.Profile.Rhythm.Prepare+actor.Profile.Rhythm.Lock);
            float delta=target.x-actor.Position.x,distance=Mathf.Abs(delta);
            float reach=actor.Profile.RhythmReach[0],direction=Mathf.Sign(delta);
            // Keep a useful blade distance; backing off is only useful when bodies would crowd.
            float separation=(actor.Motor.Size.x+facts.Body.size.x)*.5f+.12f;
            if(distance<separation)direction=-direction;
            else if(distance<=reach*.85f+facts.Body.extents.x)return false;
            float step=Mathf.Min(actor.Navigation.PlatformWidth*.18f,Mathf.Abs(distance-(reach*.85f+facts.Body.extents.x)));
            if(step<.05f)return false;
            Vector2 point=actor.Position+Vector2.right*direction*step;
            if(!actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var source)
                ||!actor.Navigation.CanStand(point,actor.Motor.Size)
                ||!actor.Navigation.TrySupport(point,actor.Motor.Size,out var destination)||destination!=source
                ||new Bounds(point,actor.Motor.Size+Vector2.one*.1f).Intersects(facts.Body)
                ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point)
                ||!actor.Navigation.TryDash(actor.Position,point,actor.Motor.Size,out _))return false;
            displacement=direction*step;nextRecoveryStep=Time.time+1.3f;
            // Any cached pursuit must be rebuilt from the actual recovered body position.
            plan=null;return true;
        }
        public bool TryBurstStep(out float destination)
        {
            destination=actor.Position.x;var facts=actor.Perception.Facts;
            if(!facts.Visible||actor.Coordinator!=null&&actor.Coordinator.Phase!=DuoPhase.Idle)return false;
            float away=Mathf.Sign(actor.Position.x-facts.Position.x),width=actor.Navigation.PlatformWidth;
            float distance=Mathf.Abs(actor.Position.x-facts.Position.x);
            float preferred=distance<EngagementDistance?away:-away;
            if(!actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var support))return false;
            foreach(float direction in new[]{preferred,-preferred})
            {
                Vector2 point=actor.Position+Vector2.right*direction*width*.25f;
                if(!actor.Navigation.CanStand(point,actor.Motor.Size)||!actor.Navigation.TrySupport(point,actor.Motor.Size,out var other)
                    ||other!=support||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point)
                    ||Mathf.Abs(point.x-facts.Position.x)<width*.75f
                    ||!actor.Navigation.TryDash(actor.Position,point,actor.Motor.Size,out _))continue;
                destination=point.x;return true;
            }
            return false;
        }
        bool TryGunReposition(BossObservation facts)
        {
            using(GunRepositionSample.Auto())
            {BeginPredictions();try{return FindGunReposition(facts);}finally{EndPredictions();}}
        }
        static readonly Unity.Profiling.ProfilerMarker GunRepositionSample=new Unity.Profiling.ProfilerMarker("Boss.GunReposition");
        bool FindGunReposition(BossObservation facts)
        {
            if(actor.Profile.Kind!=BossKind.Refraction||!facts.Visible||Time.time<nextGunMove||plan!=null
                ||actor.Coordinator!=null&&actor.Coordinator.HasAssignment(actor))return false;
            float width=actor.Navigation.PlatformWidth,distance=Mathf.Abs(facts.Position.x-actor.Position.x);
            bool pressure=exposedSince>=0&&Time.time-exposedSince>=.65f;
            if(!pressure&&distance>width)return false;
            nextGunMove=Time.time+.3f;
            BossPlan best=null;float bestScore=.25f;
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);float travel=Vector2.Distance(point,actor.Position);
                if(travel<width*.3f||travel>width*1.25f||!actor.Navigation.CanStand(point,actor.Motor.Size)
                    ||Mathf.Abs(point.y-actor.Position.y)>ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile)
                    ||new Bounds(point,actor.Motor.Size).Intersects(facts.Body)
                    ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))continue;
                float separation=Mathf.Abs(point.x-facts.Position.x);
                if(separation<width*.8f||!TryGunTarget(BossAbility.Burst,point,facts,out var target))continue;
                float angle=Vector2.Angle(actor.Position-facts.Position,point-facts.Position);
                float score=(Mathf.Abs(distance-EngagementDistance)-Mathf.Abs(separation-EngagementDistance))*.35f
                    +Mathf.Min(angle/30,1)*.9f-travel*.08f;
                if(Vector2.Distance(point,lastGunOrigin)<width*.4f)score-=.65f;
                if(score<=bestScore)continue;
                ArenaNavigation2D.Route route;
                bool jump=Mathf.Abs(point.y-actor.Position.y)>.3f;
                if(jump?!actor.Navigation.TryJumpRoute(actor.Position,point,actor.Motor,out route)
                    :!actor.Navigation.TryRoute(actor.Position,point,actor.Motor,out route))continue;
                int transitions=0;foreach(var step in route.Steps)if(step.Kind!=ArenaNavigation2D.TravelKind.Walk)transitions++;
                if(transitions>1||route.Duration>1.4f)continue;
                bestScore=score;best=new BossPlan {Intent="GunReposition",Ability=BossAbility.Burst,Destination=point,
                    TargetHypothesis=target,Route=route,CreatedAt=Time.time,TerrainVersion=actor.Navigation.Version,
                    TravelSeconds=route.Duration,MaxPlanTime=route.Duration+.5f,Score=score};
            }
            if(best==null)return false;
            lastGunOrigin=actor.Position;nextGunMove=Time.time+3.2f;plan=best;GunRepositions++;
            actor.Motor.SetRoute(plan.Route);actor.Trace("gun reposition to="+plan.Destination);return true;
        }
        public bool TryStartCamo()
        {
            var action=actor.Actions;var facts=actor.Perception.Facts;
            if(actor.Profile.Kind!=BossKind.Refraction||action.Busy||!actor.Motor.Grounded
                ||!facts.Visible||facts.Confidence<.3f||!action.Ready(BossAbility.Camo)
                ||actor.Coordinator!=null&&actor.Coordinator.HasAssignment(actor))return false;
            var options=BuildCandidates(BossAbility.None,true);
            if(options.Count==0)return false;
            var chosen=options[0];
            if(!action.TryStart(BossAbility.Camo,chosen.TargetHypothesis))return false;
            camoPlan=plan=chosen;camoTravelling=true;camoFollowupUntil=0;
            plan.Intent="CamoAngle";plan.CreatedAt=Time.time;
            actor.Motor.SetRoute(plan.Route);
            lastCamoDestination=plan.Destination;lastCamoAt=Time.time;
            actor.Trace($"camo route {plan.Ability} to={plan.Destination} travel={plan.TravelSeconds:0.00}");
            return true;
        }
        public void EndCamoRoute()
        {
            if(!camoTravelling)return;
            bool arrived=!CamoRouteInvalid&&actor.Motor.RouteCompleted&&actor.Motor.Grounded
                &&Vector2.Distance(actor.Position,camoPlan.Destination)<.45f;
            camoTravelling=false;
            actor.Trace(arrived?"camo arrived; prepare follow-up":$"camo route ended without attack setup pos={actor.Position} failure={actor.Motor.RouteFailure} completed={actor.Motor.RouteCompleted}");
            if(arrived)camoFollowupUntil=Time.time+actor.Profile.Camo.Recovery+.45f;
            else {if(plan==camoPlan)plan=null;camoPlan=null;}
        }
        bool TickCamoFollowup(BossObservation facts)
        {
            if(camoPlan==null||camoTravelling)return false;
            if(Time.time>camoFollowupUntil||camoPlan.TerrainVersion!=actor.Navigation.Version
                ||!facts.Visible||facts.Confidence<.3f||Vector2.Distance(actor.Position,camoPlan.Destination)>.6f)
            {if(plan==camoPlan)plan=null;camoPlan=null;return false;}
            // Full existing tells, ammunition, cooldown and shared-threat reservation apply again.
            // A blocked major may use Burst; there is no invisible shot or extra simultaneous major.
            var action=actor.Actions;
            foreach(var ability in new[]{camoPlan.Ability,BossAbility.Burst})
                if(action.Ready(ability)&&action.CanAfford(ability)&&!action.Reloading
                    &&LegalAt(ability,actor.Position,facts,out _)
                    &&TryGunTarget(ability,actor.Position,facts,out var target)&&action.TryStart(ability,target))
                {
                    actor.Trace("camo follow-up "+ability);previousAbility=ability;lastAttackAt=Time.time;
                    plan=null;camoPlan=null;return true;
                }
            actor.Motor.Brake();return true; // Bounded wait, then resume ordinary tactics.
        }
        float CamoAngleBenefit(Vector2 point,BossObservation facts)
        {
            float width=actor.Navigation.PlatformWidth;
            if(Vector2.Distance(point,actor.Position)<width*.5f)return 0;
            float angle=Vector2.Angle(actor.Position-facts.Position,point-facts.Position);
            float height=Mathf.Abs(point.y-actor.Position.y);
            bool opensLane=!TryGunTarget(BossAbility.Burst,actor.Position,facts,out _)
                &&TryGunTarget(BossAbility.Burst,point,facts,out _);
            if(angle<20&&height<ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile)*.3f&&!opensLane)return 0;
            float benefit=.65f+Mathf.Min(angle/90,1)*.5f+(opensLane?.5f:0);
            if(Time.time-lastCamoAt<20&&Vector2.Distance(point,lastCamoDestination)<width*.5f)benefit-=.5f;
            return benefit;
        }
        public void SearchSwordContact()
        {
            if(Time.time<nextTick)return;nextTick=Time.time+actor.Profile.TacticTick;
            UnityEngine.Profiling.Profiler.BeginSample("Sword.SearchContact");
            try {SearchVisibleAngle();}
            finally {UnityEngine.Profiling.Profiler.EndSample();}
        }
        public void SwordContactRecovered(){searchedSupports.Clear();searchSupport=null;}
        void SearchVisibleAngle()
        {
            // Keep seeking a real visible angle after memory expires, without reading the player's Transform.
            if(searchSupport!=null)
            {
                if(actor.Motor.HasRoute&&Time.time<searchDeadline)return;
                searchedSupports.Add(searchSupport);searchSupport=null;
                actor.Motor.ClearRoute();
            }
            else if(actor.Motor.HasRoute)return;
            if(actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var currentSupport))
                searchedSupports.Add(currentSupport);
            if(TryVisibleAngleRoute(out var route,out var chosen))
            {lastSearchPoint=route.Steps[route.Steps.Count-1].End;searchSupport=chosen;searchDeadline=Time.time+route.Duration+2.5f;actor.Motor.SetRoute(route);}
            else searchedSupports.Clear(); // A finished finite sweep can retry, rather than wait forever.
        }
        bool TryVisibleAngleRoute(out ArenaNavigation2D.Route route,out Collider2D chosen)
        {
            route=null;chosen=null;searchCandidates.Clear();
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);float distance=Vector2.Distance(point,actor.Position);
                if(searchedSupports.Contains(node.Collider)||distance<actor.Motor.Size.x||distance>actor.Navigation.SameLevelSpacing*1.5f||Vector2.Distance(point,lastSearchPoint)<actor.Motor.Size.x)continue;
                var facts=actor.Perception.Facts;
                float cost=Vector2.Distance(point,facts.Position)*.6f+distance*.2f;
                if(BossPerception.CanSee(point+Vector2.up*.35f,facts.Body))cost-=4;
                if(cost>=float.MaxValue)continue;
                searchCandidates.Add(new SearchCandidate {Point=point,Support=node.Collider,Cost=cost,Order=searchCandidates.Count});
            }
            // The old loop repeatedly solved a full graph for progressively cheaper candidates.
            // Cheapest reachable wins either way; retain node order for exact equal-cost ties.
            searchCandidates.Sort((a,b)=>{int cost=a.Cost.CompareTo(b.Cost);return cost!=0?cost:a.Order.CompareTo(b.Order);});
            foreach(var candidate in searchCandidates)
                if(actor.Navigation.TryRoute(actor.Position,candidate.Point,actor.Motor,out route))
                {chosen=candidate.Support;return true;}
            return false;
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
        public Vector2 PredictBody(BossObservation facts,float horizon)
            =>PredictBodyLimited(facts,horizon,.65f);
        public Vector2 PredictSwordBody(BossObservation facts,float horizon)
            =>PredictBodyLimited(facts,horizon+Mathf.Clamp(Time.time-facts.ObservedAt,0,.25f),1.25f,true);
        public Vector2 SwordTarget(BossAbility ability,Vector2 from,BossObservation facts,float remainingTell=-1,int cut=0)
        {
            if(ability==BossAbility.Guard)return facts.Position;
            var settings=actor.Profile.Settings(ability);
            float delay=remainingTell>=0?remainingTell:ability==BossAbility.Rhythm
                ?actor.Profile.RhythmPrepare[cut]+actor.Profile.RhythmLock[cut]:settings.Prepare+settings.Lock;
            if(ability==BossAbility.Rhythm||ability==BossAbility.DashFollowup||ability==BossAbility.DropFollowup
                ||ability==BossAbility.LeapFollowup||ability==BossAbility.FlankCut)delay+=actor.Profile.RhythmActive[cut]*.5f;
            Vector2 target=PredictSwordBody(facts,delay);
            if(ability==BossAbility.DrawDash||ability==BossAbility.ReturnCut)
            {
                float speed=ability==BossAbility.DrawDash?actor.Profile.DashSpeed:actor.Profile.ReturnSpeed;
                for(int i=0;i<3;i++)target=PredictSwordBody(facts,delay+Vector2.Distance(from,target)/speed);
            }
            return target;
        }
        bool TrySwordDash(Vector2 from,BossObservation facts,out Vector2 target,out Vector2 end)
        {
            target=SwordTarget(BossAbility.DrawDash,from,facts);end=from;
            float distance=Vector2.Distance(from,target);
            if(distance<actor.Motor.Size.x*2||Vector2.Distance(from,facts.Position)>actor.Navigation.SameLevelSpacing
                ||!actor.Navigation.TryDirectionalAttack(from,target,actor.Motor.Size,out end))return false;
            Vector2 direction=(end-from).normalized;
            float extent=Mathf.Abs(direction.x)*facts.Body.extents.x+Mathf.Abs(direction.y)*facts.Body.extents.y;
            if(Vector2.Distance(from,end)>=distance-extent-actor.Profile.BladeRadius-.2f)return true;
            // A retreat can outlast direct contact. Still accept a supported closing dash when
            // its actual finite exit gains distance and creates a legal next-cut opportunity.
            float arrival=actor.Profile.DrawDash.Prepare+actor.Profile.DrawDash.Lock+Vector2.Distance(from,end)/actor.Profile.DashSpeed;
            Vector2 atExit=PredictSwordBody(facts,arrival);
            float gap=Vector2.Distance(end,atExit);
            return actor.Navigation.CanStand(end,actor.Motor.Size)&&Mathf.Abs(end.y-atExit.y)<1.5f
                &&gap<=actor.Profile.RhythmReach[0]+actor.Profile.RhythmStep[0]+facts.Body.extents.x
                &&gap+actor.Motor.Size.x<Vector2.Distance(from,PredictSwordBody(facts,0));
        }
        public bool GuardOpportunity(Vector2 point,BossObservation facts,out float urgency)
        {
            urgency=0;
            if(!facts.Visible||facts.Confidence<.3f)return false;
            float facing=Mathf.Sign(facts.Position.x-point.x);
            float begins=actor.Profile.Guard.Prepare+actor.Profile.Guard.Lock;
            foreach(var shot in Projectile.Active)
            {
                if(shot==null||shot.OwnerLayer==actor.gameObject.layer)continue;
                Vector2 position=shot.transform.position,velocity=shot.GetComponent<Rigidbody2D>().linearVelocity;
                float speed=velocity.magnitude;if(speed<.01f||velocity.x*facing>=0)continue;
                Vector2 direction=velocity/speed,to=point-position;
                float along=Vector2.Dot(to,direction),radius=actor.Profile.Guard.Range+shot.Radius;
                float perpendicular=to.sqrMagnitude-along*along;
                if(along<=0||perpendicular>radius*radius)continue;
                float enter=along-Mathf.Sqrt(Mathf.Max(0,radius*radius-perpendicular));
                float arrival=enter/speed;
                if(arrival<begins+Time.fixedDeltaTime||arrival>begins+actor.Profile.Guard.Active)continue;
                Vector2 contact=position+direction*enter-point;
                if(Vector2.Dot(contact.normalized,Vector2.right*facing)<=Mathf.Cos(actor.Profile.GuardArc*.5f*Mathf.Deg2Rad))continue;
                // Reflect shots which would actually strike our body, not harmless fly-bys.
                var body=new Bounds(point,actor.Motor.Size+Vector2.one*shot.Radius*2);
                if(!body.IntersectRay(new Ray(position,direction),out _))continue;
                urgency=Mathf.Max(urgency,1-Mathf.Clamp01((arrival-begins)/actor.Profile.Guard.Active));
            }
            return urgency>0;
        }
        Vector2 PredictBodyLimited(BossObservation facts,float horizon,float limit,bool sword=false)
        {
            if(predictionDepth>0)
            {
                var cache=sword?swordPrediction:gunPrediction;
                var original=cache.Original;
                // Only reuse prefixes during this synchronous query. Changed observations, even
                // within that query, start a fresh trajectory; nothing survives to a later frame.
                if(cache.Points.Count==0||!original.Position.Equals(facts.Position)||!original.Velocity.Equals(facts.Velocity)
                    ||original.Grounded!=facts.Grounded||original.Dodging!=facts.Dodging
                    ||original.ObservedAt!=facts.ObservedAt||!original.Body.size.Equals(facts.Body.size))
                {
                    cache.Points.Clear();cache.Original=cache.Facts=facts;
                    cache.Point=facts.Position;cache.Velocity=facts.Velocity;cache.Time=0;
                    cache.Points.Add(cache.Point);
                }
                // Keep float accumulation identical to the original fixed-step predictor.
                int count=0;float step=Time.fixedDeltaTime;
                for(float t=0;t<Mathf.Min(limit,horizon);t+=step)count++;
                while(cache.Points.Count<=count)
                {
                    StepPublicPrediction(ref cache.Facts,ref cache.Point,ref cache.Velocity,cache.Time,step,sword);
                    cache.Time+=step;cache.Points.Add(cache.Point);
                }
                return cache.Points[count];
            }
            Vector2 point=facts.Position,velocity=facts.Velocity;
            float dt=Time.fixedDeltaTime;
            for(float t=0;t<Mathf.Min(limit,horizon);t+=dt)
                StepPublicPrediction(ref facts,ref point,ref velocity,t,dt,sword);
            return point;
        }
        // A synchronous sword candidate search needs many horizons of the same public trajectory.
        // Build its prefixes once; the planner discards them before the next search.
        internal void PredictSwordPath(BossObservation facts,List<Vector2> points)
        {
            points.Clear();Vector2 point=facts.Position,velocity=facts.Velocity;points.Add(point);
            float dt=Time.fixedDeltaTime;
            for(float t=0;t<1.25f;t+=dt)
            {
                StepPublicPrediction(ref facts,ref point,ref velocity,t,dt,true);
                points.Add(point);
            }
        }
        void StepPublicPrediction(ref BossObservation facts,ref Vector2 point,ref Vector2 velocity,float t,float dt,bool sword)
        {
            // A public dodge is finite. Do not extrapolate its burst speed through an entire leap.
            if(sword&&facts.Dodging&&facts.ObservedAt+t>=actor.Perception.DodgeStartedAt+.18f)
                velocity.x=actor.Perception.BeforeDodgeVelocityX;
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
        public bool TryGunTarget(BossAbility ability,Vector2 position,BossObservation facts,out Vector2 target)
            =>TryGunTarget(ability,position,facts,out target,-1);
        public bool TryGunTarget(BossAbility ability,Vector2 position,BossObservation facts,out Vector2 target,float remainingPreparation)
        {
            float flight=ability==BossAbility.Piercing?0:Vector2.Distance(position,facts.Position)/actor.Profile.BulletSpeed;
            float delay=remainingPreparation>=0?remainingPreparation:ability==BossAbility.Piercing
                ?actor.Profile.Piercing.Prepare+actor.Profile.Piercing.Lock:actor.Profile.BurstShotLock;
            float age=Mathf.Clamp(Time.time-facts.ObservedAt,0,actor.Profile.SightMemory);
            Vector2 centre=PredictBody(facts,age+delay+flight),origin=position+Vector2.up*.28f;
            target=centre;
            // All points remain on the observed body. An exposed head can be shot over a teammate.
            for(int i=0;i<3;i++)
            {
                Vector2 point=centre+Vector2.up*facts.Body.extents.y*(i==0?0:i==1?.72f:-.72f);
                if(AllyBlocks(origin,point))continue;
                target=point;return true;
            }
            return false;
        }
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
        public bool ClearGunLine(Vector2 position,Vector2 target)=>!AllyBlocks(position+Vector2.up*.28f,target);
        public bool AllyBlocks(Vector2 from,Vector2 target)
        {
            if(actor.Coordinator==null)return false;
            Vector2 delta=target-from;
            var filter=new ContactFilter2D().NoFilter();filter.SetLayerMask(1<<actor.gameObject.layer);filter.useTriggers=Physics2D.queriesHitTriggers;
            Physics2D.CircleCast(from,actor.ProjectilePrefab.Radius,delta.normalized,filter,allyHits,delta.magnitude);
            foreach(var hit in allyHits)
                foreach(var other in actor.Coordinator.Actors)if(other!=actor&&other.IsAlive&&hit.collider==other.Body)return true;
            return false;
        }
    }
}
