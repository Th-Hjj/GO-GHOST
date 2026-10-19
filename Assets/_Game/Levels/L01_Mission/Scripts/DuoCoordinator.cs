using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public enum DuoCombination { CrossExecution, SweepAndDrop }
    public enum DuoPhase { Idle, Assemble, Ready, FirstCommitted, FollowupCommitted, Recover }
    [DefaultExecutionOrder(-30)] // Offer a due shared plan before the actors start local actions.
    public sealed class DuoCoordinator : MonoBehaviour
    {
        [SerializeField] float decisionSeconds=.25f,assemblyLimit=1.2f,comboCooldown=6f,abortDelay=1f,reorganizeSeconds=.75f;
        public IReadOnlyList<BossAgent> Actors=>actors;
        public DuoPhase Phase {get;private set;}
        public int CombosStarted {get;private set;}
        public int CombosCompleted {get;private set;}
        public int CombosAborted {get;private set;}
        public int RecoveryHandoffs {get;private set;}
        BossAgent[] actors=System.Array.Empty<BossAgent>();
        BossAgent gunner,sword,majorOwner;
        BossPlan gunPlan,swordPlan;
        ArenaNavigation2D nav;
        float nextDecision,nextCombo,deadline,recoverUntil,gunStartAt,swordStartAt,gunImpact,swordImpact;
        public DuoCombination Combination {get;private set;}
        public int CrossCompleted {get;private set;}
        public int SweepDropCompleted {get;private set;}
        DuoCombination lastCombination=DuoCombination.SweepAndDrop;
        Vector2 bandFrom,bandTo;
        int gunActivationBefore,swordActivationBefore;
        bool running,paused,gunStarted,swordStarted;
        int terrainVersion;
        float occludedSince=-1;
        string planFailure;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DuoCombination nextDebugCombo=DuoCombination.CrossExecution;
        public int DebugRequests {get;private set;}
        public string LastDebugResult {get;private set;}
        public DuoCombination LastDebugCombination {get;private set;}
        public void RequestNextDebugCombo()
        {
            LastDebugCombination=nextDebugCombo;
            nextDebugCombo=nextDebugCombo==DuoCombination.CrossExecution?DuoCombination.SweepAndDrop:DuoCombination.CrossExecution;
            DebugRequests++;
            string rejection=!running||paused?"combat inactive/paused":gunner==null||sword==null||!gunner.IsAlive||!sword.IsAlive?"requires two living Bosses"
                :Phase!=DuoPhase.Idle?"existing combo/respite: "+Phase
                :sword.Tactics.DropPlanPending?"sword has committed a Drop Cut approach"
                :gunner.Actions.Busy&&!gunner.Actions.SafeRecovery||sword.Actions.Busy&&!sword.Actions.SafeRecovery?"actor still in preparation/commitment"
                :!gunner.Motor.Grounded||!sword.Motor.Grounded?"actor airborne"
                :gunner.Actions.Reloading?"gunner reloading"
                :gunner.Perception.Facts.Confidence<.6f||sword.Perception.Facts.Confidence<.6f?"public contact insufficient":null;
            if(rejection==null&&!Plan(LastDebugCombination))rejection=planFailure;
            LastDebugResult=rejection==null?"accepted real assembly":"rejected: "+rejection;
            Debug.Log($"[Duo debug] {LastDebugCombination}: {LastDebugResult}");
        }
#endif
        public bool NeedsFireLane(BossAgent actor) => actor==sword && Phase==DuoPhase.Idle
            && gunner!=null && gunner.IsAlive && occludedSince>=0 && Time.time-occludedSince>.6f;
        public void Configure(BossAgent[] actors,ArenaNavigation2D nav)
        {this.actors=actors;this.nav=nav;foreach(var a in actors)if(a.Profile.Kind==BossKind.Refraction)gunner=a;else sword=a;nextCombo=Time.time+2;}
        public void BeginCombat(){running=true;nextCombo=Time.time+2;}
        public void SetPaused(bool value){paused=value;}
        public void StopCombat(){running=false;majorOwner=null;Phase=DuoPhase.Idle;gunPlan=swordPlan=null;}
        public bool HasAssignment(BossAgent actor)=>Phase==DuoPhase.Assemble||Phase==DuoPhase.Ready||Phase==DuoPhase.FirstCommitted||Phase==DuoPhase.FollowupCommitted;
        public bool Occupied(BossAgent self,Vector2 point)
        {
            foreach(var other in actors)
            {
                if(other==self||!other.IsAlive)continue;
                if(Mathf.Abs(point.y-other.Position.y)<self.Motor.Size.y*.6f&&Mathf.Abs(point.x-other.Position.x)<self.Motor.Size.x+.2f)return true;
                var planned=other.Tactics.Current;
                if(planned!=null&&Vector2.Distance(point,planned.Destination)<self.Motor.Size.x+.2f)return true;
            }
            return false;
        }
        public float RoleScore(BossAgent self,BossAbility ability,Vector2 point)
        {
            if(actors.Length<2)return 0;
            var other=self==gunner?sword:gunner;
            if(other==null||!other.IsAlive)return 0;
            bool covering=other.Actions.Phase==BossActionPhase.Recovery||other.Actions.Reloading;
            if(covering)return ability==BossAbility.Burst||ability==BossAbility.Rhythm?.2f:0;
            if(other.Actions.HighThreat)return ability==BossAbility.Burst||ability==BossAbility.Guard?.25f:-.6f;
            var observation=self.Perception.Facts;
            return Mathf.Abs(point.y-other.Position.y)>1 || Mathf.Sign(point.x-observation.Position.x)!=Mathf.Sign(other.Position.x-observation.Position.x)?.15f:0;
        }
        BossAbility GunAbility=>Combination==DuoCombination.CrossExecution?BossAbility.Piercing:BossAbility.Sweep;
        BossAbility SwordAbility=>Combination==DuoCombination.CrossExecution?BossAbility.DrawDash:BossAbility.DropCut;
        public bool CanStart(BossAgent actor,BossAbility ability,bool cooperative)
        {
            if(Time.time<recoverUntil)return false;
            if(cooperative)return (Phase==DuoPhase.Ready||Phase==DuoPhase.FirstCommitted||Phase==DuoPhase.FollowupCommitted)
                && ((actor==gunner&&ability==GunAbility)||(actor==sword&&ability==SwordAbility));
            if(Phase==DuoPhase.Assemble && actor==gunner && ability==BossAbility.Burst)
                return deadline-Time.time>.82f && gunner.Actions.Ammo>=gunner.Profile.Settings(GunAbility).Cost+3
                    && Mathf.Abs(gunner.Perception.Facts.Position.x-gunner.Position.x)>=gunner.Tactics.EngagementDistance*.6f;
            if(Phase!=DuoPhase.Idle)return ability==BossAbility.Camo;
            // A solo sword phrase already contains its own crossing threats. Minor gunfire cannot
            // fill its intended escape route; authored duo combinations keep their existing rules.
            if(ability==BossAbility.Burst||ability==BossAbility.TacticalJumpShot)
                foreach(var other in actors)
                    if(other!=actor&&other.IsAlive&&(BossActionExecutor.IsSwordPhrase(other.Actions.Ability)||other.Actions.PendingSwordThreats))return false;
            if(BossActionExecutor.IsHigh(ability))
                foreach(var other in actors)
                    if(other!=actor&&other.IsAlive&&(other.Actions.OccupiesThreatBudget
                        ||BossActionExecutor.IsSwordPhrase(ability)&&(other.Actions.Ability==BossAbility.Burst||other.Actions.Ability==BossAbility.TacticalJumpShot)))return false;
            return true;
        }
        public bool TryReserve(BossAgent actor,BossAbility ability,bool cooperative)
        {
            if(!CanStart(actor,ability,cooperative))return false;
            if(cooperative)return true;
            foreach(var other in actors)
                if(other!=actor&&other.IsAlive&&(other.Actions.OccupiesThreatBudget||other.Actions.HighThreat&&other.Actions.Busy))
                {
                    if(!other.Actions.SafeRecovery)return false;
                    RecoveryHandoffs++;actor.Trace("recovery handoff from "+other.ActorId);
                }
            majorOwner=actor;return true;
        }
        public void Release(BossAgent actor)
        {
            if(majorOwner==actor)majorOwner=null;
            if(!actor.IsAlive&&Phase!=DuoPhase.Idle)Abort("actor defeated");
        }
        void Update()
        {
            if(!running||paused||Time.deltaTime<=0)return;
            if(gunner==null||sword==null||!gunner.IsAlive||!sword.IsAlive)
            {if(Phase!=DuoPhase.Idle)Abort("single survivor");recoverUntil=0;return;}
            if(Phase==DuoPhase.Recover){if(Time.time>=recoverUntil)Phase=DuoPhase.Idle;return;}
            var gunFacts=gunner.Perception.Facts;
            bool blocked=gunFacts.Confidence>.3f && !gunner.Actions.Busy && !gunner.Actions.Reloading
                && gunner.Tactics.AllyBlocks(gunner.Position+Vector2.up*.28f,gunFacts.Position)
                && !gunner.Tactics.TryGunTarget(BossAbility.Burst,gunner.Position,gunFacts,out _)
                && !gunner.Tactics.TryGunTarget(BossAbility.Piercing,gunner.Position,gunFacts,out _);
            if(!blocked)occludedSince=-1;else if(occludedSince<0)occludedSince=Time.time;
            if(Phase==DuoPhase.Assemble)
            {
                if(nav.Version!=terrainVersion||Time.time>deadline||gunner.Motor.RouteFailed||sword.Motor.RouteFailed
                    ||gunFacts.Confidence<.25f){Abort("assembly invalid/timeout");return;}
                bool g=Vector2.Distance(gunner.Position,gunPlan.Destination)<.35f&&gunner.Motor.Grounded;
                bool s=Vector2.Distance(sword.Position,swordPlan.Destination)<.35f&&sword.Motor.Grounded;
                if(g)gunner.Motor.Brake();if(s)sword.Motor.Brake();
                if(!g&&!gunner.Actions.Busy&&!gunner.Motor.HasRoute)gunner.Motor.SetRoute(gunPlan.Route);
                if(!s&&!sword.Actions.Busy&&!sword.Motor.HasRoute)sword.Motor.SetRoute(swordPlan.Route);
                if(g&&!s&&!gunner.Actions.Busy&&gunner.Actions.Ready(BossAbility.Burst)
                    &&CanStart(gunner,BossAbility.Burst,false))gunner.Actions.TryStart(BossAbility.Burst,gunFacts.Position);
                if(g&&s&&Mathf.Abs(gunner.Velocity.x)<.15f&&Mathf.Abs(sword.Velocity.x)<.15f&&!gunner.Actions.Busy&&!sword.Actions.Busy)
                {
                    if(!PrepareTiming()){Abort("start geometry changed");return;}
                    Phase=DuoPhase.Ready;deadline=Time.time+4;
                    gunActivationBefore=gunner.Actions.Activated[(int)GunAbility];
                    swordActivationBefore=sword.Actions.Activated[(int)SwordAbility];
                    CombosStarted++;gunner.Trace("combo ready "+Combination);
                }
            }
            if(Phase==DuoPhase.Ready||Phase==DuoPhase.FirstCommitted||Phase==DuoPhase.FollowupCommitted)
            {
                if(Time.time>deadline){Abort("execution timeout");return;}
                if(!gunStarted&&Time.time>=gunStartAt)
                {
                    gunStarted=Combination==DuoCombination.CrossExecution
                        ?gunner.Actions.TryStart(GunAbility,gunPlan.TargetHypothesis,true)
                        :gunner.Actions.TryStartSweep(bandFrom,bandTo);
                    if(!gunStarted){Abort("gun start rejected");return;}
                }
                if(!swordStarted&&Time.time>=swordStartAt)
                {
                    swordStarted=Combination==DuoCombination.CrossExecution
                        ?sword.Actions.TryStart(SwordAbility,swordPlan.TargetHypothesis,true)
                        :sword.Tactics.StartDropPlan(swordPlan,true);
                    if(!swordStarted){Abort("sword start rejected");return;}
                }
                if(gunStarted||swordStarted)Phase=DuoPhase.FirstCommitted;
                if(gunStarted&&swordStarted)Phase=DuoPhase.FollowupCommitted;
                if(gunStarted&&swordStarted&&!gunner.Actions.Busy&&!sword.Actions.Busy&&!sword.Tactics.DropPlanPending)
                {
                    if(gunner.Actions.Activated[(int)GunAbility]<=gunActivationBefore
                        ||sword.Actions.Activated[(int)SwordAbility]<=swordActivationBefore){Abort("action did not activate");return;}
                    CombosCompleted++;lastCombination=Combination;
                    if(Combination==DuoCombination.CrossExecution)CrossCompleted++;else SweepDropCompleted++;
                    Phase=DuoPhase.Recover;recoverUntil=Time.time+reorganizeSeconds;nextCombo=Time.time+comboCooldown;
                    gunPlan=swordPlan=null;majorOwner=null;gunner.Trace("combo complete "+Combination);
                }
                return;
            }
            if(Phase==DuoPhase.Idle&&Time.time>=nextDecision)
            {
                nextDecision=Time.time+decisionSeconds;
                if(Time.time<nextCombo||sword.Tactics.DropPlanPending||gunner.Actions.Busy&&!gunner.Actions.SafeRecovery||sword.Actions.Busy&&!sword.Actions.SafeRecovery||gunner.Actions.Reloading
                    ||!gunner.Motor.Grounded||!sword.Motor.Grounded||gunFacts.Confidence<.6f||sword.Perception.Facts.Confidence<.6f)return;
                var preferred=lastCombination==DuoCombination.CrossExecution?DuoCombination.SweepAndDrop:DuoCombination.CrossExecution;
                if(!Plan(preferred)&&!Plan(lastCombination))nextCombo=Time.time+abortDelay;
            }
        }
        bool Plan(DuoCombination kind)
        {
            Combination=kind;
            planFailure="no nearby pair passes reachability, ally lane, timing and escape checks";
            if(!gunner.Actions.Ready(GunAbility)||!sword.Actions.Ready(SwordAbility)){planFailure="required skill cooldown";return false;}
            if(!gunner.Actions.CanAfford(GunAbility)){planFailure="insufficient gun ammunition";return false;}
            var guns=new List<BossPlan>(gunner.Tactics.BuildCandidates(GunAbility));
            var swords=new List<BossPlan>();
            if(kind==DuoCombination.CrossExecution)swords.AddRange(sword.Tactics.BuildCandidates(SwordAbility));
            else if(sword.Tactics.TryPlanDrop(sword.Perception.Facts,out var drop))swords.Add(drop);
            float best=float.NegativeInfinity,bestTravel=float.PositiveInfinity,bestBudget=assemblyLimit;
            BossPlan gBest=null,sBest=null;
            Vector2 bestTarget=default;
            var observed=gunner.Perception.Facts;
            foreach(var g in guns)foreach(var s in swords)
            {
                if(!AssemblyTime(gunner,g,false,out float gunTravel,out float gunCap)
                    ||!AssemblyTime(sword,s,kind==DuoCombination.SweepAndDrop,out float swordTravel,out float swordCap)
                    ||Vector2.Distance(g.Destination,s.Destination)<gunner.Motor.Size.x+sword.Motor.Size.x)continue;
                float travel=Mathf.Max(gunTravel,swordTravel),cap=Mathf.Max(gunCap,swordCap);
                float budget=Mathf.Max(assemblyLimit,travel+decisionSeconds+.1f);
                if(budget>cap)continue;
                Vector2 target=g.TargetHypothesis;
                if(kind==DuoCombination.CrossExecution)
                {
                    float side=gunner.Perception.LearnedRetreat!=0?gunner.Perception.LearnedRetreat:
                        Mathf.Abs(observed.Velocity.x)>.5f?Mathf.Sign(observed.Velocity.x):Mathf.Sign(observed.Position.x-s.Destination.x);
                    target=gunner.Tactics.PredictBody(observed,.5f)+Vector2.right*side*nav.PlatformWidth*.375f;
                    target.x=Mathf.Clamp(target.x,nav.Bounds.min.x+observed.Body.extents.x,nav.Bounds.max.x-observed.Body.extents.x);
                    Vector2 origin=g.Destination+Vector2.up*.28f,ray=(target-origin).normalized;
                    // Both the starting body and the committed dash endpoint must leave a usable ray.
                    if(!nav.TryHorizontalAttack(s.Destination,s.TargetHypothesis,sword.Motor.Size,out var dashEnd))continue;
                    if(BlocksLine(origin,target,s.Destination,sword.Motor.Size)||BlocksLine(origin,target,dashEnd,sword.Motor.Size))continue;
                }
                float score=g.Score+s.Score-Mathf.Max(g.TravelSeconds,s.TravelSeconds)*.5f;
                if(travel>bestTravel+.05f||Mathf.Abs(travel-bestTravel)<=.05f&&score<=best)continue;
                var original=g.TargetHypothesis;g.TargetHypothesis=target;gunPlan=g;swordPlan=s;
                bool valid=PrepareTiming();
                g.TargetHypothesis=original;
                if(!valid)continue;
                best=score;bestTravel=travel;bestBudget=budget;gBest=g;sBest=s;bestTarget=target;
            }
            if(gBest==null){gunPlan=swordPlan=null;return false;}
            gBest.TargetHypothesis=bestTarget;
            gunPlan=gBest;swordPlan=sBest;terrainVersion=nav.Version;
            // Timing and finite escape samples are rechecked at arrival using the same frozen hypotheses.
            if(!PrepareTiming()){gunPlan=swordPlan=null;return false;}
            Phase=DuoPhase.Assemble;deadline=Time.time+bestBudget;gunStarted=swordStarted=false;
            if(!gunner.Actions.Busy)gunner.Motor.SetRoute(gunPlan.Route);
            if(!sword.Actions.Busy&&kind==DuoCombination.CrossExecution)sword.Motor.SetRoute(swordPlan.Route);else sword.Motor.Brake();
            gunner.Trace($"combo assemble {Combination} budget={bestBudget:F2}s travel={bestTravel:F2}s");return true;
        }
        bool AssemblyTime(BossAgent actor,BossPlan plan,bool attackJump,out float travel,out float cap)
        {
            var motion=actor.Motor.Profile;
            float jumpSeconds=2*motion.JumpSpeed/Mathf.Abs(Physics2D.gravity.y*motion.GravityScale);
            cap=Mathf.Clamp(jumpSeconds+nav.PlatformWidth/motion.Speed+motion.Speed/motion.Braking+decisionSeconds,
                assemblyLimit,2.25f);
            travel=actor.Actions.RecoveryRemaining+(attackJump?0:plan.TravelSeconds)
                +Mathf.Abs(actor.Velocity.x)/motion.Braking;
            int transitions=0;
            if(!attackJump&&plan.Route!=null)foreach(var step in plan.Route.Steps)
                if(step.Kind!=ArenaNavigation2D.TravelKind.Walk&&++transitions>1)return false;
            return travel<=cap;
        }
        bool PrepareTiming()
        {
            var facts=sword.Perception.Facts;
            float a,b,delta;
            if(Combination==DuoCombination.CrossExecution)
            {
                if(!sword.Tactics.LegalAt(BossAbility.DrawDash,swordPlan.Destination,facts,out _)
                    ||gunner.Tactics.AllyBlocks(gunPlan.Destination+Vector2.up*.28f,gunPlan.TargetHypothesis))return false;
                Vector2 dashDelta=swordPlan.TargetHypothesis-swordPlan.Destination;
                float targetExtent=Mathf.Abs(dashDelta.normalized.x)*facts.Body.extents.x+Mathf.Abs(dashDelta.normalized.y)*facts.Body.extents.y;
                float contact=Mathf.Max(0,dashDelta.magnitude
                    -targetExtent-(sword.Profile.BladeRadius+.2f)
                    -BossActionExecutor.DashBladeLead(dashDelta.normalized,sword.Motor.Size,sword.Profile.BladeRadius+.2f))/sword.Profile.DashSpeed;
                a=sword.Profile.DrawDash.Prepare+sword.Profile.DrawDash.Lock+contact;
                b=gunner.Profile.Piercing.Prepare+gunner.Profile.Piercing.Lock;delta=.40f;
                swordStartAt=Time.time+Mathf.Max(0,b-(a+delta));
                gunStartAt=Time.time+Mathf.Max(0,a+delta-b);
            }
            else
            {
                if(!gunner.Tactics.TrySweepBand(gunPlan.Destination,gunner.Perception.Facts,out bandFrom,out bandTo))return false;
                a=swordPlan.DropImpactDelay;
                b=gunner.Profile.Sweep.Prepare+gunner.Profile.Sweep.Lock+Vector2.Distance(gunPlan.Destination+Vector2.up*.28f,bandFrom)/gunner.Profile.BulletSpeed;
                delta=.45f;
                swordStartAt=Time.time+Mathf.Max(0,b+delta-a);
                gunStartAt=Time.time+Mathf.Max(0,a-delta-b);
                if(swordStartAt-Time.time>1.2f||gunStartAt-Time.time>.6f)return false;
            }
            swordImpact=swordStartAt-Time.time+a;gunImpact=gunStartAt-Time.time+b;
            return HasEscapeSample(facts);
        }
        static bool BlocksLine(Vector2 from,Vector2 to,Vector2 centre,Vector2 size)
        {
            Vector2 ray=(to-from).normalized;float along=Vector2.Dot(centre-from,ray);
            if(along<0||along>Vector2.Distance(from,to)+size.magnitude*.5f)return false;
            float distance=Mathf.Abs(Vector3.Cross(ray,centre-from).z);
            float half=Mathf.Abs(ray.y)*size.x*.5f+Mathf.Abs(ray.x)*size.y*.5f;
            return distance<half+.1f;
        }
        bool HasEscapeSample(BossObservation observation)
        {
            // Four public walk/jump samples; this rejects obvious sealed routes, not a solvability proof.
            float dt=Time.fixedDeltaTime;
            float dashDuration=0;
            Vector2 dashDirection=Vector2.zero;
            if(Combination==DuoCombination.CrossExecution)
            {
                if(!nav.TryDirectionalAttack(swordPlan.Destination,swordPlan.TargetHypothesis,sword.Motor.Size,out var dashEnd))return false;
                dashDuration=Vector2.Distance(swordPlan.Destination,dashEnd)/sword.Profile.DashSpeed;
                dashDirection=(dashEnd-swordPlan.Destination).normalized;
            }
            for(int sample=0;sample<4;sample++)
            {
                bool jump=sample>=2;if(jump&&!observation.Grounded)continue;
                Vector2 position=observation.Position,velocity=new Vector2(sample%2==0?-7:7,jump?12:observation.Velocity.y);
                bool safe=true;
                for(float t=0;t<Mathf.Max(gunImpact,swordImpact)+.15f;t+=dt)
                {
                    velocity.y+=Physics2D.gravity.y*3*dt;Vector2 next=position+velocity*dt;
                    if(!nav.SweepTraversal(position,next,observation.Body.size,out var hit,out var endpoint))
                    {
                        next=endpoint;
                        if(hit.normal.y>.6f&&velocity.y<0)velocity.y=0;
                        else if(Mathf.Abs(hit.normal.x)>.5f)velocity.x=0;
                    }
                    if(Combination==DuoCombination.CrossExecution)
                    {
                        float dashStart=swordStartAt-Time.time+sword.Profile.DrawDash.Prepare+sword.Profile.DrawDash.Lock;
                        float dashT=t-dashStart;
                        if(dashT>=0&&dashT<dashDuration)
                        {
                            float bladeOffset=BossActionExecutor.DashBladeLead(dashDirection,sword.Motor.Size,sword.Profile.BladeRadius+.2f);
                            Vector2 at=swordPlan.Destination+dashDirection*(dashT*sword.Profile.DashSpeed+bladeOffset);
                            if(new Bounds(at,Vector2.one*(sword.Profile.BladeRadius+.2f)*2).Intersects(new Bounds(next,observation.Body.size))){safe=false;break;}
                        }
                        Vector2 origin=gunPlan.Destination+Vector2.up*.28f;
                        Vector2 beamEnd=origin+(gunPlan.TargetHypothesis-origin).normalized*gunner.Profile.Piercing.Range;
                        if(Mathf.Abs(t-gunImpact)<dt && BlocksLine(origin,beamEnd,next,observation.Body.size)){safe=false;break;}
                    }
                    else
                    {
                        if(Mathf.Abs(t-swordImpact)<.07f && Mathf.Abs(next.x-swordPlan.TargetHypothesis.x)<observation.Body.extents.x+sword.Profile.BladeRadius
                            && Mathf.Abs(next.y-swordPlan.TargetHypothesis.y)<observation.Body.size.y){safe=false;break;}
                        for(int i=0;i<gunner.Profile.SweepShots;i++)
                        {
                            Vector2 origin=gunPlan.Destination+Vector2.up*.28f,direction=(Vector2.Lerp(bandFrom,bandTo,i/(float)(gunner.Profile.SweepShots-1))-origin).normalized;
                            float emit=gunStartAt-Time.time+gunner.Profile.Sweep.Prepare+gunner.Profile.Sweep.Lock+i*gunner.Profile.SweepInterval;
                            var threat=new BossThreat{Valid=true,Shape=BossThreatShape.Projectile,Phase=BossThreatPhase.Locked,Origin=origin,
                                End=origin+direction*gunner.Profile.Sweep.Range,Direction=direction,Radius=gunner.ProjectilePrefab.Radius,
                                StartsAt=emit,EndsAt=emit+1.5f,ProjectileSpeed=gunner.Profile.BulletSpeed,TerrainBlocked=false};
                            if(threat.IntersectsMotion(position,next,observation.Body.extents,t,t+dt)){safe=false;break;}
                        }
                        if(!safe)break;
                    }
                    position=next;
                }
                if(safe)return true;
            }
            return false;
        }
        void Abort(string reason)
        {
            CombosAborted++;gunner?.Trace("combo abort "+Combination+" "+reason);
            foreach(var actor in actors)
            {
                if(actor==null)continue;
                if(actor.Actions.Phase==BossActionPhase.Prepare)actor.Actions.AbortPreparation();
                actor.Tactics.CancelDropPlan();
                if(!actor.Actions.Committed)actor.Motor.ClearRoute();
            }
            gunPlan=swordPlan=null;majorOwner=null;Phase=DuoPhase.Idle;recoverUntil=0;
            nextCombo=Time.time+abortDelay;gunStarted=swordStarted=false;
        }
    }
}
