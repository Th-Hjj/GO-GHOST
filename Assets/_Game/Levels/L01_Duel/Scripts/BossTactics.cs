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
    public sealed partial class BossTactics
    {
        readonly BossAgent actor;
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
        public float EngagementDistance=>PlatformPressureEnabled?actor.Profile.DesiredDistance:
            actor.Profile.Kind==BossKind.Gunner?actor.Navigation.SameLevelSpacing*.8f:actor.Navigation.PlatformWidth*.4f;
        public BossTactics(BossAgent actor){this.actor=actor;}
        public void Tick()
        {
            if (!Enabled)
                return;
            // The current sword policy owns solo phrases before general exposure/recovery planning.
            if (actor.Profile.Kind == BossKind.Swordsman && dropPlan == null
                && (actor.Coordinator == null || !actor.Coordinator.HasAssignment(actor)))
            {
                actor.Actions.TickSwordDecision();
                return;
            }

            var action = actor.Actions;
            var facts = actor.Perception.Facts;
            UpdateExposure(facts);
            if (action.Busy)
            {
                PreviewRecoveryPlan(action, facts);
                return;
            }
            if (dropPlan != null)
            {
                TickDropPlan();
                return;
            }
            if (actor.Coordinator != null && actor.Coordinator.HasAssignment(actor))
            {
                YieldMovementToCoordinator();
                return;
            }
            // Complete the accepted physical air connection before replanning.
            if (!actor.Motor.Grounded)
                return;
            if (TickCamoFollowup(facts))
                return;
            if (PlatformPressureEnabled)
            {
                TickPlatformPressure(facts);
                return;
            }
            if (facts.Visible && facts.Confidence > .3f)
            {
                searchedSupports.Clear();
                searchSupport = null;
            }
            if (TryGunReposition(facts))
            {
                recoveryPlan = null;
                return;
            }
            if (TryUseRecoveryPlan(facts))
                return;
            if (CurrentGunRepositionIsUseful(facts))
                return;
            if (Time.time >= nextTick && facts.Confidence > .3f && TryStartOpportunisticAction(action, facts))
                return;

            InvalidateStalePursuit(facts);
            if (ContinueCurrentPlan(action, facts))
                return;
            if (Time.time < nextTick)
                return;
            SelectNextPlan(action, facts);
        }

        void UpdateExposure(BossObservation facts)
        {
            bool exposed = facts.Visible && Time.time - facts.LastShotAt < .55f
                && Vector2.Dot(facts.Aim, (actor.Position - facts.Position).normalized) > .9f;
            if (!exposed)
                exposedSince = -1;
            else if (exposedSince < 0)
                exposedSince = Time.time;
        }

        void PreviewRecoveryPlan(BossActionExecutor action, BossObservation facts)
        {
            if (!PlatformPressureEnabled && action.SafeRecovery && actor.Motor.Grounded && Time.time >= nextTick
                && (actor.Coordinator == null || !actor.Coordinator.HasAssignment(actor)))
            {
                nextTick = Time.time + actor.Profile.TacticTick;
                if (actor.Profile.Kind == BossKind.Swordsman)
                    recoveryPlan = SelectSwordPressure(facts);
                else
                {
                    var next = BuildCandidates();
                    recoveryPlan = next.Count > 0 ? next[0] : null;
                }
            }
        }

        void YieldMovementToCoordinator()
        {
            if (camoPlan != null)
            {
                if (plan == camoPlan)
                    plan = null;
                camoPlan = null;
            }
            // Do not resume a stale solo perch plan after the coordinator releases movement.
            if (plan != null && plan.Intent == "PlatformRelocate")
                plan = null;
        }

        bool TryUseRecoveryPlan(BossObservation facts)
        {
            // Re-evaluate a preview against fresh public facts and the actual recovered body.
            // It never cancels recovery or an air route.
            if (actor.Profile.Kind == BossKind.Swordsman && (Time.time >= nextTick || recoveryPlan != null))
            {
                bool previewed = recoveryPlan != null;
                recoveryPlan = null;
                var pressure = SelectSwordPressure(facts);
                if (pressure != null && StartSwordPressure(pressure, facts))
                {
                    if (previewed)
                        RecoveryPlansUsed++;
                    return true;
                }
            }
            if (recoveryPlan != null)
            {
                var ready = recoveryPlan;
                recoveryPlan = null;
                if (ready.TerrainVersion == actor.Navigation.Version && facts.Visible && Time.time - ready.CreatedAt < .5f
                    && LegalAt(ready.Ability, actor.Position, facts, out _) && TryImmediate(ready.Ability, facts))
                {
                    RecoveryPlansUsed++;
                    return true;
                }
                // Rebuild from the actual recovered position; never reuse an old takeoff.
                nextTick = Time.time;
            }
            return false;
        }

        bool CurrentGunRepositionIsUseful(BossObservation facts)
        {
            return plan != null && plan.Intent == "GunReposition" && !actor.Motor.RouteFailed
                && plan.TerrainVersion == actor.Navigation.Version && Time.time < plan.CreatedAt + plan.MaxPlanTime
                && actor.Motor.HasRoute && facts.Confidence > .3f;
        }

        bool TryStartOpportunisticAction(BossActionExecutor action, BossObservation facts)
        {
            // Preserve acceptance order: reload punish, Camo, then jump shot.
            if (actor.Profile.Kind == BossKind.Gunner && facts.Visible && facts.ReloadVisible
                && TryImmediate(BossAbility.Piercing, facts))
                return true;
            if (TryStartCamo())
            {
                nextTick = Time.time + actor.Profile.TacticTick;
                return true;
            }
            if (actor.Profile.Kind == BossKind.Gunner && action.Ready(BossAbility.TacticalJumpShot)
                && TryPlanMobility(BossAbility.TacticalJumpShot, facts, out var jump) && action.TryStartMobility(jump))
            {
                plan = null;
                previousAbility = BossAbility.TacticalJumpShot;
                lastAttackAt = Time.time;
                nextTick = Time.time + actor.Profile.TacticTick;
                return true;
            }
            return false;
        }

        void InvalidateStalePursuit(BossObservation facts)
        {
            // A newly observed jump can invalidate pursuit before the previous hold expires.
            if (plan != null && actor.Profile.Kind == BossKind.Swordsman && plan.Intent == "Approach/Intercept"
                && facts.Visible && Time.time >= nextTick && Time.time - plan.CreatedAt > .25f
                && Vector2.Distance(PredictSwordBody(facts, plan.TravelSeconds), plan.TargetHypothesis)
                    > actor.Navigation.PlatformWidth * .45f)
            {
                plan = null;
                actor.Motor.ClearRoute();
                nextTick = Time.time;
            }
        }

        // True ends this decision tick; false permits same-tick replacement after invalidation.
        bool ContinueCurrentPlan(BossActionExecutor action, BossObservation facts)
        {
            if (plan == null)
                return false;
            // Recover only failed routes this way; successful flanks retain their destination.
            if (actor.Motor.RouteFailed && plan.TerrainVersion == actor.Navigation.Version
                && plan.Ability != BossAbility.None && LegalAt(plan.Ability, actor.Position, facts, out _)
                && (actor.Profile.Kind != BossKind.Swordsman || SwordImmediateScore(plan.Ability, actor.Position, facts) > 0)
                && action.TryStart(plan.Ability, actor.Profile.Kind == BossKind.Swordsman
                    ? SwordTarget(plan.Ability, actor.Position, facts) : plan.TargetHypothesis))
            {
                actor.Motor.Brake();
                previousAbility = plan.Ability;
                lastAttackAt = Time.time;
                plan = null;
                return true;
            }
            bool invalid = plan.TerrainVersion != actor.Navigation.Version || actor.Motor.RouteFailed
                || Time.time > plan.CreatedAt + plan.MaxPlanTime || facts.Confidence <= 0;
            if (invalid)
            {
                if (actor.Motor.RouteFailed)
                {
                    failedDestination = plan.Destination;
                    avoidFailedUntil = Time.time + 2;
                }
                plan = null;
                actor.Motor.ClearRoute();
            }
            else if (Vector2.Distance(actor.Position, plan.Destination) < .38f && actor.Motor.Grounded
                && (plan.Intent != "AerialPressure" && plan.Intent != "OpenFireLane" || actor.Motor.RouteCompleted))
            {
                actor.Motor.Brake();
                if (plan.Ability != BossAbility.None && LegalAt(plan.Ability, actor.Position, facts, out _)
                    && (actor.Profile.Kind != BossKind.Swordsman || SwordImmediateScore(plan.Ability, actor.Position, facts) > 0)
                    && action.TryStart(plan.Ability, actor.Profile.Kind == BossKind.Swordsman
                        ? SwordTarget(plan.Ability, actor.Position, facts) : plan.TargetHypothesis))
                {
                    previousAbility = plan.Ability;
                    lastAttackAt = Time.time;
                    plan = null;
                    return true;
                }
                if (plan.Ability == BossAbility.None || Time.time - plan.CreatedAt > .8f)
                    plan = null;
            }
            else if (Time.time < nextTick)
                return true;
            else if (plan.Intent == "ResetSpacing" || plan.Intent == "OpenFireLane" || plan.Intent == "AerialPressure")
                return true;
            return false;
        }

        void SelectNextPlan(BossActionExecutor action, BossObservation facts)
        {
            nextTick = Time.time + actor.Profile.TacticTick;
            if (facts.Confidence <= 0)
            {
                SearchVisibleAngle();
                return;
            }
            if (plan == null && TryBreakStalemate(facts))
                return;
            var options = BuildCandidates();
            if (options.Count == 0)
            {
                SearchVisibleAngle();
                return;
            }
            BossPlan selected = options[0];
            bool chasing = actor.Profile.Kind == BossKind.Swordsman && plan?.Intent == "Approach/Intercept";
            if (plan != null && (Time.time - plan.CreatedAt < (chasing ? Mathf.Min(.35f, actor.Profile.PlanHold) : actor.Profile.PlanHold)
                || selected.Score < (actor.Profile.Kind == BossKind.Swordsman
                    ? plan.Score + Mathf.Max(.08f, Mathf.Abs(plan.Score) * (chasing ? .05f : .15f)) : plan.Score * 1.15f)))
                return;
            plan = selected;
            plan.CreatedAt = Time.time;
            if (plan.Intent == "Approach/Intercept")
                InterceptPlansUsed++;
            actor.Trace($"plan {plan.Intent}/{plan.Ability} to={plan.Destination} score={plan.Score:0.00}");
            actor.Motor.SetRoute(plan.Route);
            if (plan.Route == null)
                actor.Motor.Brake(); // Retire the previous route for an in-place attack.
            if (actor.Profile.Kind == BossKind.Gunner && !action.CanAfford(plan.Ability))
                action.RequestReload();
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
            if(actor.Profile.Kind==BossKind.Swordsman&&(ability==BossAbility.Rhythm||ability==BossAbility.DrawDash||ability==BossAbility.Guard)
                &&SwordImmediateScore(ability,actor.Position,facts)<=0)return false;
            Vector2 target=actor.Profile.Kind==BossKind.Swordsman?SwordTarget(ability,actor.Position,facts):PredictBody(facts,.16f);
            if(actor.Profile.Kind==BossKind.Gunner&&!TryGunTarget(ability,actor.Position,facts,out target))return false;
            if(!actor.Actions.TryStart(ability,target))return false;
            plan=null;previousAbility=ability;lastAttackAt=Time.time;nextTick=Time.time+actor.Profile.TacticTick;return true;
        }

        public bool TrySwordRecoveryStep(out float displacement)
        {
            displacement=0;var facts=actor.Perception.Facts;
            if(!Enabled||actor.Profile.Kind!=BossKind.Swordsman||!actor.Motor.Grounded
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
            if(actor.Profile.Kind!=BossKind.Gunner||!facts.Visible||Time.time<nextGunMove||plan!=null
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
            if(actor.Profile.Kind!=BossKind.Gunner||action.Busy||!actor.Motor.Grounded
                ||!action.Ready(BossAbility.Camo)
                ||actor.Coordinator!=null&&actor.Coordinator.HasAssignment(actor))return false;
            if(PlatformPressureEnabled)return facts.Body.size.sqrMagnitude>.01f&&BeginPlatformBlink(facts);
            if(!facts.Visible||facts.Confidence<.3f)return false;
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
            if(PlatformBlinkActive){EndPlatformBlink();return;}
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
            if(actor.Profile.Kind!=BossKind.Swordsman||facts.Confidence<.3f)return false;
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
    }
}
