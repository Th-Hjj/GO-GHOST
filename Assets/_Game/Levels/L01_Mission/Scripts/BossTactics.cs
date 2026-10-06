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
        public float SlashOffset;
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
        readonly HashSet<Collider2D> searchedSupports=new HashSet<Collider2D>();
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
        float exposedSince=-1,nextGunMove;
        Vector2 lastGunOrigin;
        public int RecoveryPlansUsed {get;private set;}
        public int GunRepositions {get;private set;}
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
                    var next=BuildCandidates();recoveryPlan=next.Count>0?next[0]:null;
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
                if(actor.Profile.Kind==BossKind.Severance)
                {
                    // Spend a real close-range opening before choosing another closing move.
                    if(LegalAt(BossAbility.Rhythm,actor.Position,facts,out _)&&TryImmediate(BossAbility.Rhythm,facts))return;
                    if(LegalAt(BossAbility.Guard,actor.Position,facts,out float guardChance)&&guardChance>1
                        &&TryImmediate(BossAbility.Guard,facts))return;
                }
                else if(facts.Visible&&facts.ReloadVisible&&TryImmediate(BossAbility.Piercing,facts))return;
                if(TryStartCamo())
                {nextTick=Time.time+actor.Profile.TacticTick;return;}
                if(actor.Profile.Kind==BossKind.Severance && actor.Position.y-facts.Position.y>ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile)*.3f
                    && action.Ready(BossAbility.DropCut)&&TryPlanDrop(facts,out var descent)&&StartDropPlan(descent,false))
                {nextTick=Time.time+actor.Profile.TacticTick;return;}
                BossAbility mobility=actor.Profile.Kind==BossKind.Severance?BossAbility.LeapSlash:BossAbility.TacticalJumpShot;
                if(action.Ready(mobility) && TryPlanMobility(mobility,facts,out var jump) && action.TryStartMobility(jump))
                {plan=null;previousAbility=mobility;lastAttackAt=Time.time;nextTick=Time.time+actor.Profile.TacticTick;return;}
            }
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
            if(actor.Profile.Kind==BossKind.Refraction&&!action.CanAfford(plan.Ability))action.RequestReload();
        }
        bool TryImmediate(BossAbility ability,BossObservation facts)
        {
            if(ability==BossAbility.None||!actor.Actions.Ready(ability)||!actor.Actions.CanAfford(ability)
                ||!LegalAt(ability,actor.Position,facts,out _))return false;
            Vector2 target=PredictBody(facts,.16f);
            if(actor.Profile.Kind==BossKind.Refraction&&!TryGunTarget(ability,actor.Position,facts,out target))return false;
            if(!actor.Actions.TryStart(ability,target))return false;
            plan=null;previousAbility=ability;lastAttackAt=Time.time;nextTick=Time.time+actor.Profile.TacticTick;return true;
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
            float best=float.MaxValue;ArenaNavigation2D.Route route=null;
            Collider2D chosen=null;
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 point=node.Center(actor.Motor.Size);float distance=Vector2.Distance(point,actor.Position);
                if(searchedSupports.Contains(node.Collider)||distance<actor.Motor.Size.x||distance>actor.Navigation.SameLevelSpacing*1.5f||Vector2.Distance(point,lastSearchPoint)<actor.Motor.Size.x)continue;
                var facts=actor.Perception.Facts;
                float cost=Vector2.Distance(point,facts.Position)*.6f+distance*.2f;
                if(BossPerception.CanSee(point+Vector2.up*.35f,facts.Body))cost-=4;
                if(cost>=best)continue;
                if(actor.Navigation.TryRoute(actor.Position,point,actor.Motor,out var path)) {best=cost;route=path;chosen=node.Collider;}
            }
            if(route!=null)
            {lastSearchPoint=route.Steps[route.Steps.Count-1].End;searchSupport=chosen;searchDeadline=Time.time+route.Duration+2.5f;actor.Motor.SetRoute(route);}
            else searchedSupports.Clear(); // A finished finite sweep can retry, rather than wait forever.
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
                bool directCamoJump=camo&&Mathf.Abs(candidate.Destination.y-actor.Position.y)>.3f
                    &&actor.Navigation.TryJumpRoute(actor.Position,candidate.Destination,actor.Motor,out candidate.Route);
                if(!near&&!directCamoJump&&!actor.Navigation.TryRoute(actor.Position,candidate.Destination,actor.Motor,out candidate.Route))continue;
                candidate.TravelSeconds=candidate.Route!=null?candidate.Route.Duration:0;
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
                else if(ability==BossAbility.DrawDash)
                {
                    Vector2 intercept=PredictBody(facts,actor.Profile.DrawDash.Prepare+actor.Profile.DrawDash.Lock
                        +Mathf.Abs(facts.Position.x-point.x)/actor.Profile.DashSpeed);
                    if(Mathf.Abs(intercept.x-point.x)>actor.Navigation.SameLevelSpacing)score-=.8f;
                    score-=Mathf.Min(2,actor.Actions.ConsecutiveMeleeMisses)*.3f;
                }
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
                float distance=Vector2.Distance(point,actor.Position);
                Vector2 intercept=PredictBody(facts,Mathf.Min(.65f,distance/actor.Profile.Motion.Speed+.12f));
                if(actor.Perception.LearnedRetreat!=0&&Mathf.Abs(facts.Velocity.x)>.5f)
                    intercept.x+=actor.Perception.LearnedRetreat*actor.Navigation.PlatformWidth*.2f;
                float targetDistance=Vector2.Distance(point,intercept);
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
                if(Mathf.Abs(delta.y)>actor.Motor.Size.y*.4f||Mathf.Abs(delta.x)<actor.Motor.Size.x*2
                    ||Mathf.Abs(delta.x)>actor.Navigation.SameLevelSpacing)return false;
                if(!actor.Navigation.TryHorizontalAttack(point,facts.Position,actor.Motor.Size,out var safe))return false;
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
            result=null;
            if(!actor.Motor.Grounded || facts.Confidence<.3f || !actor.Actions.Ready(ability)
                || actor.Coordinator!=null&&!actor.Coordinator.CanStart(actor,ability,false))return false;
            bool sword=ability==BossAbility.LeapSlash;
            float width=actor.Navigation.PlatformWidth,spacing=actor.Navigation.SameLevelSpacing;
            float height=ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile);
            Vector2 target=PredictBody(facts,.5f),delta=target-actor.Position;
            bool blocked=!TryGunTarget(BossAbility.Burst,actor.Position,facts,out _);
            if(sword)
            {
                if(LegalAt(BossAbility.Rhythm,actor.Position,facts,out _))return false;
                if(LegalAt(BossAbility.DrawDash,actor.Position,facts,out _)&&actor.Actions.ConsecutiveMeleeMisses==0
                    &&Mathf.Abs(facts.Velocity.x)<1)return false;
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
            for(int i=0;i<Mathf.Min(12,choices.Count);i++)
            {
                Vector2 landing=choices[i].Center(actor.Motor.Size);
                if(sword && Vector2.Distance(landing,target)>delta.magnitude-actor.Motor.Size.x*.5f)continue;
                if(!actor.Navigation.TryJumpTrajectory(actor.Position,landing,actor.Motor,out var route,out var path))continue;
                int slash=1;float best=float.PositiveInfinity;bool gunLine=false;
                for(int j=1;j<path.Length-1;j++)
                {
                    float t=j*Time.fixedDeltaTime;
                    if(t<.12f || t>(path.Length-1)*Time.fixedDeltaTime-actor.Profile.LeapSlash.Active)continue;
                    Vector2 atImpact=sword?PredictBody(facts,actor.Profile.LeapSlash.Prepare+actor.Profile.LeapSlash.Lock+t):target;
                    float distance=Vector2.Distance(path[j],atImpact);
                    if(distance<best){best=distance;slash=j;}
                    if(TryGunTarget(BossAbility.Burst,path[j],facts,out _))gunLine=true;
                }
                if(!sword&&!gunLine)continue;
                result=new BossPlan { Intent=sword?"LeapClose":"JumpAngle",Ability=ability,Destination=landing,
                    TargetHypothesis=target,Route=route,JumpPath=path,SlashOffset=slash*Time.fixedDeltaTime,
                    TerrainVersion=actor.Navigation.Version,CreatedAt=Time.time,MaxPlanTime=route.Duration+1,
                    TravelSeconds=route.Duration,Score=2 };
                return true;
            }
            return false;
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
            Vector2 predicted=PredictBody(facts,.6f);
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
                Vector2 intercept=PredictBody(facts,prepare+tell+contactFall);
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
                Vector2 intercept=PredictBody(facts,Mathf.Clamp(Time.time-facts.ObservedAt,0,.25f)+before+hitFall);
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
            foreach(var hit in Physics2D.CircleCastAll(from,actor.ProjectilePrefab.Radius,delta.normalized,delta.magnitude,1<<actor.gameObject.layer))
                foreach(var other in actor.Coordinator.Actors)if(other!=actor&&other.IsAlive&&hit.collider==other.Body)return true;
            return false;
        }
    }
}
