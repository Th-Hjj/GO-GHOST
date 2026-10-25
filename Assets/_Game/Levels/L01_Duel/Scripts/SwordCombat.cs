using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    [Serializable]
    public sealed class SwordCombatSettings
    {
        [Range(.1f,.8f)] public float UnboundHealth=.33f;
        public float AdvanceSpeed=34, AdvanceDistance=22, TravelSpeed=15;
        public float DrawPrepare=.20f, DrawLock=.14f, WavePrepare=.30f, WaveLock=.18f;
        public float WaveSpeed=15, WaveLength=1.35f, WaveLife=2.4f;
        public float CutReach=3.0f, CutRadius=.16f, EchoDelay=.20f, EchoWarning=0, EchoActive=.12f;
        public float EchoLength=1.35f, EchoSpeed=13, EchoTravel=18, EchoStagger=.16f;
        public float CutPrepare=.30f, FinisherPrepare=.38f, CutLock=.16f, CutAdvance=1.1f, CutStepSpeed=12;
        public float JumpPrepare=.24f, JumpLock=.12f, JumpCutActive=.28f;
        public float Recovery=.65f, FinaleRecovery=1f, FinaleCooldown=10;
        public float UnboundRecovery=.50f, UnboundFinaleCooldown=9;
        public int DrawDamage=28, WaveDamage=14, CutDamage=18, FinisherDamage=26, EchoDamage=24;
        public BossMotionProfile TravelMotion(BossMotionProfile basis) => new BossMotionProfile {
            Speed=TravelSpeed, Acceleration=90, Braking=90, JumpSpeed=basis.JumpSpeed,
            GravityScale=basis.GravityScale, AirControl=.9f, ArrivalBraking=true };
    }

    public enum SwordBeat { None, Wave, Traverse, Draw, LowCut, RisingCut, DownCut, Replay, JumpCut }

    public sealed class SwordTravel
    {
        public Vector2 Destination;
        public ArenaNavigation2D.Route Route;
        public Vector2[] Path;
        public BossMotionProfile Motion;
        public bool Jump;
        public float Duration;
        public bool Walk, AttackJump, PlatformChange, Setup;
        public Collider2D SourceSupport;
        public SwordBeat Followup;
        public Vector2 Target;
        public float StrikeOffset, Facing, FirstThreatAfter, Score;
    }

    // Instance-local planning. It consumes copied public observations and actual arena geometry.
    // It neither runs another update loop nor owns Rigidbody motion.
    public sealed class SwordPlanner
    {
        readonly BossAgent actor;
        readonly List<Vector2> points=new List<Vector2>(32);
        readonly List<BossThreat> linkThreats=new List<BossThreat>(4);
        readonly List<Vector2> travelPredictions=new List<Vector2>(64);
        readonly List<RaycastHit2D> drawHits=new List<RaycastHit2D>(16);
        bool predictingTravel;
        int decisionDepth;
        bool decisionTravelValid,decisionTravelFound,decisionAttackJump,decisionAirLink,decisionPlatformSetup;
        SwordBeat decisionFollowup;
        SwordTravel decisionTravel;
        // Selection, immediate validation and first-beat setup are one synchronous decision.
        // A later decision/landing must search again against fresh public facts and geometry.
        internal void BeginDecision(){if(decisionDepth++==0)decisionTravelValid=false;}
        internal void EndDecision(){if(--decisionDepth==0){decisionTravelValid=false;decisionTravel=null;}}
        Vector2 failedPoint;
        float failedUntil;
        Collider2D previousSupport;
        float previousSupportUntil;
        BossAbility previous;
        int selection;
        float DrawTell=>actor.Profile.Sword.DrawPrepare+actor.Profile.Sword.DrawLock;
        public SwordPlanner(BossAgent owner){actor=owner;}
        public void Failed(Vector2 point){failedPoint=point;failedUntil=Time.time+1.4f;}
        public void Landed(SwordTravel travel)
        {
            if(travel==null||!travel.PlatformChange)return;
            previousSupport=travel.SourceSupport;previousSupportUntil=Time.time+3;
        }
        public void ResetTravelMemory(){previousSupport=null;previousSupportUntil=failedUntil=0;}
        public void Accepted(BossAbility ability){previous=ability;selection++;}
        public bool StillUseful(BossAbility ability)
        {
            if(actor.Perception.Facts.Confidence<.25f)return false;
            switch(ability)
            {
                case BossAbility.SwordDuel:return CloseBeat(false)!=SwordBeat.None;
                case BossAbility.SwordPursuit:case BossAbility.SwordRedline:
                    return TryDraw(actor.Position,DrawTarget(DrawTell),out _,out _);
                case BossAbility.SwordCrossing:return TryTravel(out _,true);
                case BossAbility.SwordDislodge:return true;
                default:return false;
            }
        }

        public BossAbility Select()
        {
            var a=actor.Actions;var f=actor.Perception.Facts;
            if(f.Confidence<.25f)return BossAbility.None;
            Vector2 delta=f.Position-actor.Position;
            float distance=delta.magnitude;
            bool unbound=a.SwordUnbound;
            if(unbound&&a.Ready(BossAbility.SwordRedline)&&!a.HasLivingPartner&&distance<actor.Profile.Sword.AdvanceDistance
                &&TryDraw(actor.Position,f.Position,out _,out _))return BossAbility.SwordRedline;
            if(distance<actor.Profile.Sword.CutReach+f.Body.extents.x&&Mathf.Abs(delta.y)<1.6f
                &&a.Ready(BossAbility.SwordDuel))return BossAbility.SwordDuel;
            bool retreat=Vector2.Dot(f.Velocity,delta.normalized)>2;
            SwordTravel travel=null;
            bool crossing=a.Ready(BossAbility.SwordCrossing)&&TryTravel(out travel,true);
            // A different fighting level is an arrival problem, not just a race to the first
            // damage tick. Take the useful platform route before repeating floor Draw/waves.
            if(crossing&&(travel.Setup||travel.PlatformChange&&PlatformPurpose(travel.Destination,travel.SourceSupport)))
                return BossAbility.SwordCrossing;
            bool opening=f.ReloadVisible||Time.time-actor.Perception.LastDodgeObservedAt<.65f||a.Guarded>0;
            if(opening&&a.Ready(BossAbility.SwordPursuit)&&distance>2.2f&&TryDraw(actor.Position,DrawTarget(DrawTell),out _,out _))
                return BossAbility.SwordPursuit;
            float drawTime=DrawOpportunity(actor.Position,0,out _);
            if(a.Ready(BossAbility.SwordPursuit)&&distance>actor.Profile.Sword.CutReach+1&& !float.IsInfinity(drawTime))
                return BossAbility.SwordPursuit;
            if(a.Ready(BossAbility.SwordDislodge)&&(Mathf.Abs(delta.y)>1.5f||distance>actor.Profile.Sword.AdvanceDistance
                ||retreat&&previous!=BossAbility.SwordDislodge))return BossAbility.SwordDislodge;
            // A crossing competes by time to a real blade opportunity, not by a jump preference.
            if(crossing&&travel.FirstThreatAfter+.12f<drawTime)return BossAbility.SwordCrossing;
            if(a.Ready(BossAbility.SwordPursuit)&&TryDraw(actor.Position,DrawTarget(DrawTell),out _,out _)
                &&distance>2.2f&&(previous!=BossAbility.SwordPursuit||!a.Ready(BossAbility.SwordDislodge)))
                return BossAbility.SwordPursuit;
            if(crossing)return BossAbility.SwordCrossing;
            return a.Ready(BossAbility.SwordDislodge)?BossAbility.SwordDislodge:BossAbility.None;
        }

        public Vector2 Target(float after)
        {
            var facts=actor.Perception.Facts;
            Vector2 target;
            if(predictingTravel)
            {
                // All candidates in this synchronous search use the same public observation and
                // terrain. Horizons ending on the same fixed step have identical predictions.
                // Match the predictor's float accumulation exactly (CeilToInt can differ at edges).
                float horizon=Mathf.Min(1.25f,after+Mathf.Clamp(Time.time-facts.ObservedAt,0,.25f));
                int steps=0;
                for(float t=0;t<horizon;t+=Time.fixedDeltaTime)steps++;
                if(travelPredictions.Count==0)actor.Tactics.PredictSwordPath(facts,travelPredictions);
                target=travelPredictions[steps];
            }
            else target=actor.Tactics.PredictSwordBody(facts,after);
            if(actor.Perception.LearnedRetreat!=0)target.x+=actor.Perception.LearnedRetreat*Mathf.Min(.6f,after);
            return target;
        }
        public Vector2 DrawTarget(float tell)
        {
            Vector2 point=Target(tell);
            for(int i=0;i<3;i++)point=Target(tell+Mathf.Min(actor.Profile.Sword.AdvanceDistance,Vector2.Distance(actor.Position,point))/actor.Profile.Sword.AdvanceSpeed);
            return point;
        }

        public bool TryDraw(Vector2 from,Vector2 target,out Vector2 end,out Vector2 direction)
        {
            // The player and sword have different standing collider heights. A centre-to-centre
            // line would tilt into the floor after a few metres. Aim through the overlapping body
            // band on a near-level approach; diagonal/up/down targets retain their full direction.
            if(Mathf.Abs(target.y-from.y)<.2f)target.y=from.y;
            var size=actor.Motor.Size;Vector2 delta=target-from;
            direction=delta.sqrMagnitude>.01f?delta.normalized:Vector2.right*actor.Facing;
            float length=Mathf.Min(actor.Profile.Sword.AdvanceDistance,delta.magnitude+.65f);
            actor.Navigation.TryDash(from,from+direction*length,size,out end);
            // The moving target is predicted at arrival, not frozen as a blocker at its current
            // location. The motor still sweeps the actual player every physics step and stops.
            var filter=new ContactFilter2D().NoFilter();
            filter.SetLayerMask((1<<LayerMask.NameToLayer("Player"))|(1<<LayerMask.NameToLayer("Enemy")));
            filter.useTriggers=Physics2D.queriesHitTriggers;
            Physics2D.BoxCast(from,size-Vector2.one*.025f,0,direction,filter,drawHits,Vector2.Distance(from,end));
            foreach(var hit in drawHits)
            {
                if(hit.collider==null||hit.collider.isTrigger||hit.collider.attachedRigidbody==actor.Motor.GetComponent<Rigidbody2D>())continue;
                if(hit.collider.GetComponentInParent<PlayerHealth>()!=null)continue;
                float clipped=Mathf.Max(0,hit.distance-.025f);
                if(clipped<Vector2.Distance(from,end))end=from+direction*clipped;
            }
            float gain=Vector2.Distance(from,target)-Vector2.Distance(end,target);
            return Vector2.Distance(from,end)>.35f&&gain>.3f;
        }

        // Keep the existing entry point; true now requests a close crossing with an optional
        // descending strike, never forces a jump when a walk or dash does the job sooner.
        public bool TryTravel(out SwordTravel result,bool preferJump=false)
            =>TryTravel(out result,preferJump?SwordBeat.RisingCut:SwordBeat.Draw,preferJump,false,preferJump);

        bool PlatformPurpose(Vector2 point,Collider2D current)
        {
            var f=actor.Perception.Facts;var nav=actor.Navigation;
            if(!nav.TrySupport(point,actor.Motor.Size,out var destination)||destination==current)return false;
            bool otherSupport=nav.TrySupport(f.Position,f.Body.size,out var targetSupport)&&targetSupport!=current;
            float heightGain=Mathf.Abs(actor.Position.y-f.Position.y)-Mathf.Abs(point.y-f.Position.y);
            float distanceGain=Vector2.Distance(actor.Position,f.Position)-Vector2.Distance(point,f.Position);
            bool retreat=Vector2.Dot(f.Velocity,(f.Position-actor.Position).normalized)>2;
            // Neither an arbitrary same-floor hop nor a cosmetic detour earns priority.
            return (otherSupport||Mathf.Abs(f.Position.y-actor.Position.y)>1.5f)&&heightGain>.5f
                ||otherSupport&&destination==targetSupport&&distanceGain>1
                ||retreat&&distanceGain>2&&Mathf.Abs(point.y-f.Position.y)<2;
        }

        float DrawOpportunity(Vector2 from,float arrival,out Vector2 target)
        {
            var s=actor.Profile.Sword;float tell=s.DrawPrepare+s.DrawLock;
            target=Target(arrival+tell);
            for(int i=0;i<3;i++)target=Target(arrival+tell+Mathf.Min(s.AdvanceDistance,Vector2.Distance(from,target))/s.AdvanceSpeed);
            if(!TryDraw(from,target,out var end,out _)||Vector2.Distance(end,target)>s.CutReach*.8f)return float.PositiveInfinity;
            return arrival+tell+Vector2.Distance(from,end)/s.AdvanceSpeed;
        }

        public bool TryTravel(out SwordTravel result,SwordBeat followup,bool allowAttackJump,bool requireAirLink,bool allowPlatformSetup=false)
        {
            if(decisionDepth>0&&decisionTravelValid&&decisionFollowup==followup&&decisionAttackJump==allowAttackJump
                &&decisionAirLink==requireAirLink&&decisionPlatformSetup==allowPlatformSetup)
            {result=decisionTravel;return decisionTravelFound;}
            travelPredictions.Clear();predictingTravel=true;
            UnityEngine.Profiling.Profiler.BeginSample("Sword.TravelSearch");
            try
            {
                bool found=FindTravel(out result,followup,allowAttackJump,requireAirLink,allowPlatformSetup);
                if(decisionDepth>0)
                {
                    decisionTravelValid=true;decisionTravelFound=found;decisionTravel=result;decisionFollowup=followup;
                    decisionAttackJump=allowAttackJump;decisionAirLink=requireAirLink;decisionPlatformSetup=allowPlatformSetup;
                }
                return found;
            }
            finally {predictingTravel=false;UnityEngine.Profiling.Profiler.EndSample();}
        }

        bool FindTravel(out SwordTravel result,SwordBeat followup,bool allowAttackJump,bool requireAirLink,bool allowPlatformSetup)
        {
            result=null;var nav=actor.Navigation;var motor=actor.Motor;var f=actor.Perception.Facts;
            if(!motor.Grounded||f.Confidence<.2f)return false;
            var motion=actor.Profile.Sword.TravelMotion(actor.Profile.Motion);
            float best=float.NegativeInfinity;
            points.Clear();
            foreach(var node in nav.Nodes)
            {
                var point=node.Center(motor.Size);
                if(Vector2.Distance(point,motor.Position)>15)continue;
                AddPoint(point);
                // Seed both sides, then score each executable duration against its own impact time.
                float estimate=.28f+Vector2.Distance(point,motor.Position)/motion.Speed+DrawTell;
                for(int side=-1;side<=1;side+=2)
                {
                    Vector2 target=Target(estimate);
                    var continuous=new Vector2(Mathf.Clamp(target.x+side*2.0f,
                        node.Collider.bounds.min.x+motor.Size.x*.5f+.1f,node.Collider.bounds.max.x-motor.Size.x*.5f-.1f),point.y);
                    if(nav.CanStand(continuous,motor.Size))AddPoint(continuous);
                }
            }
            nav.TrySupport(motor.Position,motor.Size,out var current);
            foreach(var point in points)
            {
                if(Vector2.Distance(point,motor.Position)<.6f||Time.time<failedUntil&&Vector2.Distance(point,failedPoint)<.8f
                    ||!nav.CanStand(point,motor.Size)||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,point))continue;
                nav.TrySupport(point,motor.Size,out var support);
                // A recent departure is not a new pursuit plan unless the public target really
                // returned there. This memory is per actor and cleared at combat teardown.
                if(support==previousSupport&&Time.time<previousSupportUntil
                    &&(!nav.TrySupport(f.Position,f.Body.size,out var targetSupport)||targetSupport!=support))continue;
                for(int mode=0;mode<3;mode++)
                {
                    bool jump=mode==2;if(requireAirLink&&!jump)continue;
                    var candidate=new SwordTravel {Destination=point,Jump=jump,Walk=mode==0,Motion=motion,Followup=followup,
                        SourceSupport=current,PlatformChange=support!=current};
                    if(jump)
                    {
                        if(!nav.TryJumpTrajectory(motor.Position,point,motion,motor.Size,out var route,out var path))continue;
                        candidate.Route=route;candidate.Path=path;candidate.Duration=(path.Length-1)*Time.fixedDeltaTime;
                    }
                    else
                    {
                        if(support!=current||Mathf.Abs(point.y-motor.Position.y)>.12f
                            ||!nav.TryDash(motor.Position,point,motor.Size,out var clear)||Vector2.Distance(clear,point)>.05f)continue;
                        float speed=mode==0?motor.Profile.Speed:motion.Speed;
                        candidate.Duration=Vector2.Distance(point,motor.Position)/speed+(mode==0?speed/motor.Profile.Acceleration:0);
                    }
                    float arrival=.28f+candidate.Duration;
                    float closeTell=actor.Profile.Sword.CutPrepare+actor.Profile.Sword.CutLock;
                    Vector2 target=Target(arrival+closeTell);
                    float reach=actor.Profile.Sword.CutReach;
                    bool close=Vector2.Distance(point,target)<reach-.2f&&Mathf.Abs(point.y-target.y)<1.5f
                        &&!Projectile.TerrainHit(point+Vector2.up*.15f,target,.12f,out _);
                    float threat=followup==SwordBeat.Draw?DrawOpportunity(point,arrival,out target):close?arrival+closeTell:float.PositiveInfinity;
                    candidate.Target=target;candidate.FirstThreatAfter=threat;
                    if(requireAirLink)
                    {
                        if(!TryAerialDraw(candidate))continue;
                        threat=candidate.FirstThreatAfter;
                    }
                    if(allowAttackJump&&jump&&TryDescendingCut(candidate))threat=candidate.FirstThreatAfter;
                    // Crossing may land into a grounded Draw when a close rising cut cannot reach.
                    if(float.IsInfinity(threat)&&allowAttackJump&&candidate.PlatformChange)
                    {
                        threat=DrawOpportunity(point,arrival,out target);
                        if(!float.IsInfinity(threat)){candidate.Followup=SwordBeat.Draw;candidate.Target=target;candidate.FirstThreatAfter=threat;}
                    }
                    // One intermediate platform can open a second physical jump. It must improve
                    // height/spacing and lead to a real strike, rather than merely change support.
                    if((float.IsInfinity(threat)||!candidate.AttackJump&&target.y-point.y>1.5f)
                        &&allowPlatformSetup&&jump&&candidate.PlatformChange
                        &&PlatformPurpose(point,current)&&TryPlatformLink(point,arrival,motion,out float linkThreat))
                    {threat=linkThreat;candidate.Setup=true;candidate.Followup=SwordBeat.None;candidate.FirstThreatAfter=threat;}
                    if(float.IsInfinity(threat))continue;
                    // Every mode must improve the actual next opportunity. A support change alone
                    // is insufficient, and an ordinary same-support hop gets no artificial bonus.
                    float currentDistance=Vector2.Distance(motor.Position,Target(threat));
                    float distance=Vector2.Distance(point,Target(threat));
                    if(!candidate.AttackJump&&!candidate.Setup&&distance>currentDistance-.5f&&!requireAirLink)continue;
                    float score=8-threat*2-Mathf.Abs(distance-2)*.25f-(jump?.45f:mode==1?.65f:0);
                    if(allowAttackJump&&candidate.PlatformChange&&PlatformPurpose(point,current))score+=4;
                    if(score<=best)continue;
                    candidate.Score=score;best=score;result=candidate;
                }
            }
            // If the lower support is outside the first jump's reach, approach a verified launch
            // point on the current floor. The same finite Crossing may then take its two jumps.
            if(allowPlatformSetup&&f.Position.y-motor.Position.y>2.5f&&(result==null||!result.PlatformChange&&!result.Setup)
                &&TryPlatformApproach(current,motion,out var approach))result=approach;
            return result!=null;
        }

        bool TryPlatformApproach(Collider2D current,BossMotionProfile motion,out SwordTravel result)
        {
            result=null;var nav=actor.Navigation;var motor=actor.Motor;var f=actor.Perception.Facts;
            float best=float.PositiveInfinity;
            foreach(var node in nav.Nodes)
            {
                Vector2 landing=node.Center(motor.Size);
                if(landing.y<motor.Position.y+.5f||landing.y>f.Position.y-.6f||Mathf.Abs(landing.x-f.Position.x)>7)continue;
                Vector2 launch=new Vector2(landing.x-Mathf.Sign(landing.x-motor.Position.x)*3,motor.Position.y);
                float distance=Vector2.Distance(launch,motor.Position);
                if(distance<.6f||distance>6||Time.time<failedUntil&&Vector2.Distance(launch,failedPoint)<.8f
                    ||!nav.CanStand(launch,motor.Size)||!nav.TrySupport(launch,motor.Size,out var support)||support!=current
                    ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,launch)
                    ||!nav.TryDash(motor.Position,launch,motor.Size,out var clear)||Vector2.Distance(clear,launch)>.05f)continue;
                if(!nav.TryJumpTrajectory(launch,landing,motion,motor.Size,out _,out var path))continue;
                float duration=distance/motion.Speed,arrival=.56f+duration+(path.Length-1)*Time.fixedDeltaTime;
                if(!TryPlatformLink(landing,arrival,motion,out float threat)||threat>=best)continue;
                best=threat;result=new SwordTravel {Destination=launch,Motion=motion,Duration=duration,Setup=true,
                    SourceSupport=current,Followup=SwordBeat.None,FirstThreatAfter=threat,Target=Target(threat)};
            }
            return result!=null;
        }

        void AddPoint(Vector2 point)
        {
            foreach(var existing in points)if((existing-point).sqrMagnitude<.0025f)return;
            points.Add(point);
        }
        bool TryPlatformLink(Vector2 from,float arrival,BossMotionProfile motion,out float threat)
        {
            threat=float.PositiveInfinity;var nav=actor.Navigation;var size=actor.Motor.Size;
            // Only inspect nodes on the observed target's level, with no recursive route search.
            foreach(var node in nav.Nodes)
            {
                Vector2 end=node.Center(size),target=Target(arrival+.8f);
                if(Mathf.Abs(end.y-target.y)>1.5f||Vector2.Distance(end,target)>actor.Profile.Sword.CutReach+1
                    ||Vector2.Distance(end,from)<.6f||Time.time<failedUntil&&Vector2.Distance(end,failedPoint)<.8f
                    ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,end))continue;
                if(!nav.TryJumpTrajectory(from,end,motion,size,out _,out var path))continue;
                float time=arrival+.28f+(path.Length-1)*Time.fixedDeltaTime;
                float next=DrawOpportunity(end,time,out target);
                if(Vector2.Distance(end,target)<actor.Profile.Sword.CutReach-.2f
                    &&!Projectile.TerrainHit(end+Vector2.up*.15f,target,.12f,out _))next=Mathf.Min(next,time+.58f);
                threat=Mathf.Min(threat,next);
            }
            return !float.IsInfinity(threat);
        }

        bool TryAerialDraw(SwordTravel travel)
        {
            // Redline's next strike occurs in flight, not at the landing. Choose the authored
            // link time together with the route, allowing enough air time for the full Draw tell.
            var s=actor.Profile.Sword;float dt=Time.fixedDeltaTime,tell=s.DrawPrepare+s.DrawLock;
            float gravity=Physics2D.gravity.y*travel.Motion.GravityScale;
            for(int i=2;i<travel.Path.Length-2;i++)
            {
                float offset=i*dt,vy=(travel.Path[i].y-travel.Path[i-1].y)/dt;
                if(offset<.12f||vy>6||offset+tell+.04f>=travel.Duration)continue;
                Vector2 from=travel.Path[i]+Vector2.up*(vy*tell+gravity*tell*tell*.5f);
                Vector2 target=Target(.28f+offset+tell);
                for(int n=0;n<2;n++)target=Target(.28f+offset+tell+Vector2.Distance(from,target)/s.AdvanceSpeed);
                if(!TryDraw(from,target,out var end,out var direction)||Vector2.Distance(end,target)>s.CutReach*.8f)continue;
                Vector2 lead=direction*(BossActionExecutor.DashBladeLead(direction,actor.Motor.Size,.48f)+.25f);
                // The final lock check still runs against then-current facts. This conservative
                // preview rejects a route whose aerial link already seals ordinary escape.
                float start=Time.time+s.DrawLock;
                linkThreats.Clear();
                linkThreats.Add(new BossThreat {Valid=true,Phase=BossThreatPhase.Locked,Shape=BossThreatShape.SweptCapsule,
                    Origin=from+lead,End=end+lead,Radius=.48f,TerrainBlocked=true,StartsAt=start,
                    EndsAt=start+Vector2.Distance(from,end)/s.AdvanceSpeed});
                if(actor.Actions.HasLivingPartner&&!SwordEscape.HasRoute(actor,linkThreats,.7f))continue;
                travel.StrikeOffset=offset;travel.Target=target;travel.FirstThreatAfter=.28f+offset+tell;return true;
            }
            return false;
        }

        bool TryDescendingCut(SwordTravel travel)
        {
            float dt=Time.fixedDeltaTime;var s=actor.Profile.Sword;
            // Prefer a swing spanning touchdown, but do not discard an earlier valid aerial
            // interception when the landing geometry cannot reach the target.
            for(int pass=0;pass<2;pass++)
            for(int i=2;i<travel.Path.Length-2;i++)
            {
                float offset=i*dt;
                if(offset<.16f||travel.Path[i].y>=travel.Path[i-1].y
                    ||pass==0&&offset<travel.Duration-s.JumpCutActive*.6f)continue;
                // The same committed swing may finish just after touchdown. Requiring its whole
                // active window to fit in descent excluded most short upward platform jumps.
                float tell=s.JumpPrepare+s.JumpLock,impact=tell+offset+s.JumpCutActive*.5f;Vector2 target=Target(impact);
                Vector2 root=travel.Path[Mathf.Min(i+Mathf.RoundToInt(s.JumpCutActive*.5f/dt),travel.Path.Length-1)]+Vector2.up*.15f;
                float facing=Mathf.Sign(target.x-root.x);if(facing==0)facing=actor.Facing;
                var arc=new BossThreat {Valid=true,Shape=BossThreatShape.BladeArc,Phase=BossThreatPhase.Active,
                    Origin=root,ArcFrom=35,ArcTo=-95,Facing=facing,Reach=s.CutReach,Radius=s.CutRadius,TerrainBlocked=true,
                    StartsAt=0,EndsAt=s.JumpCutActive};
                if(!arc.IntersectsMotion(target,target,actor.Perception.Facts.Body.extents,0,s.JumpCutActive))continue;
                travel.AttackJump=true;travel.StrikeOffset=offset;travel.Facing=facing;
                travel.Target=target;travel.FirstThreatAfter=tell+offset;return true;
            }
            return false;
        }

        public SwordBeat CloseBeat(bool finisher)
        {
            var f=actor.Perception.Facts;
            if(f.Confidence<.2f||Vector2.Distance(f.Position,actor.Position)>actor.Profile.Sword.CutReach+1.1f)return SwordBeat.None;
            return finisher?SwordBeat.DownCut:selection%2==0?SwordBeat.LowCut:SwordBeat.RisingCut;
        }
    }

    public static class SwordEscape
    {
        // Bounded public walk/jump samples reject obvious sealed combinations. This is not a
        // guarantee that every player decision is safe, or a substitute for human playtesting.
        public static bool HasRoute(BossAgent actor,IReadOnlyList<BossThreat> threats,float horizon,
            Vector2? bodyFrom=null,Vector2? bodyTo=null,float bodyDuration=0)
        {
            var f=actor.Perception.Facts;if(f.Confidence<.2f)return false;
            float dt=Time.fixedDeltaTime,now=Time.time;
            for(int sample=0;sample<6;sample++)
            {
                bool jump=sample>=3;if(jump&&!f.Grounded)continue;
                Vector2 position=f.Position,velocity=f.Velocity;
                bool safe=true,jumped=false,grounded=f.Grounded;
                float move=sample%3-1;
                for(float t=0;t<horizon;t+=dt)
                {
                    if(t>=actor.Profile.ReactionSeconds)
                    {
                        velocity.x=move*7;
                        if(jump&&!jumped&&grounded){velocity.y=12;jumped=true;grounded=false;}
                    }
                    if(grounded&&!actor.Navigation.TrySupport(position,f.Body.size,out _))grounded=false;
                    if(!grounded)velocity.y+=Physics2D.gravity.y*3*dt;
                    Vector2 next=position+velocity*dt;
                    if(!actor.Navigation.SweepTraversal(position,next,f.Body.size,out var hit,out var endpoint))
                    {
                        if(hit.normal.y>.5f&&velocity.y<0)
                        {
                            velocity.y=0;grounded=true;
                            // A landing does not erase the horizontal slide of the real Rigidbody.
                            var slide=new Vector2(next.x,endpoint.y);
                            actor.Navigation.SweepTraversal(endpoint,slide,f.Body.size,out _,out next);
                        }
                        else
                        {next=endpoint;if(Mathf.Abs(hit.normal.x)>.5f)velocity.x=0;else velocity.y=0;}
                    }
                    foreach(var threat in threats)
                        if(threat.IntersectsMotion(position,next,f.Body.extents,now+t,now+t+dt)){safe=false;break;}
                    // Residual footwork must leave an ordinary walk/jump answer even accounting
                    // for the moving solid sword body. Conservative swept boxes reject crossings.
                    if(safe&&bodyFrom.HasValue&&bodyTo.HasValue)
                    {
                        Vector2 a=Vector2.Lerp(bodyFrom.Value,bodyTo.Value,t/Mathf.Max(.01f,bodyDuration));
                        Vector2 b=Vector2.Lerp(bodyFrom.Value,bodyTo.Value,(t+dt)/Mathf.Max(.01f,bodyDuration));
                        Vector2 separation=(position+next-a-b)*.5f;
                        Vector2 half=(Vector2)f.Body.extents+actor.Motor.Size*.5f
                            +new Vector2(Mathf.Abs(next.x-position.x)+Mathf.Abs(b.x-a.x),Mathf.Abs(next.y-position.y)+Mathf.Abs(b.y-a.y))*.5f;
                        if(Mathf.Abs(separation.x)<half.x&&Mathf.Abs(separation.y)<half.y)safe=false;
                    }
                    if(!safe)break;position=next;
                }
                if(safe)return true;
            }
            return false;
        }
    }
}
