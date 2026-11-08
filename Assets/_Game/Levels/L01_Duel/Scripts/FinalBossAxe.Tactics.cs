using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        // Positions are body centres, except Turn, which is the flying axe's brake point.
        sealed class AxePlan
        {
            public Move Move;
            public float Facing, FirstThreat, FinishFacing;
            public Cut Swing;
            public Vector2 Start, Target, Landing, Anchor, Turn, Retreat;
            public ArenaNavigation2D.Route JumpRoute;
            public Vector2[] JumpPath;
            public BossMotionProfile JumpMotion;
        }

        static readonly Move[] PhaseOneMoves =
        {
            Move.TripleCut, Move.LeapingCrosscut, Move.AnchorVault, Move.UnderpassReturn, Move.PalmDeflection
        };
        struct Site
        {
            public Vector2 Point;
            public int Surface, Side;
            public float Cost;
        }

        readonly List<Site> approachSites = new List<Site>(16);
        readonly List<Site> attackSites = new List<Site>(48);
        readonly List<Site> searchSites = new List<Site>(16);
        readonly List<Vector2> searchedPoints = new List<Vector2>(6);
        readonly List<Vector2> publicTrajectory = new List<Vector2>(65);
        int trajectoryFrame = -1;
        BossMotionProfile lowLeap, oneTierLeap, twoTierLeap;
        float nextTacticsTick, routeHoldUntil, failedRouteUntil, relocateUntil, lastSeenAt;
        Vector2 routeGoal, failedRouteGoal, lastSeenPoint, missedFrom;
        bool movingToSite, searching, hasLastSeen;
        readonly AxePlan[] attacksHere = new AxePlan[Timings.Length];
        float pressUntil;
        Move lastAttack, pendingResult;
        Vector2 lastStartup;
        int hitsAtStartup, sameSpotMisses;
        int observedTravelSign;
        float turnedAt=-100, travelSeenAt=-100;
        readonly float[] missedUntil=new float[Timings.Length];
        readonly Vector2[] missedOrigin=new Vector2[Timings.Length];
        Vector2 missedPalmAim;

        void TickPhaseOneTactics()
        {
            var motor = actor.Motor;
            if (Time.time < nextTacticsTick) return;
            nextTacticsTick = Time.time + actor.Profile.ReactionSeconds;
            if (!motor.Grounded)
            {
                if (!motor.HasRoute) RecoverFooting();
                return;
            }
            if (movingToSite && motor.RouteFailed)
            {
                failedRouteGoal = routeGoal;
                failedRouteUntil = Time.time + 1.1f;
                movingToSite = false;
            }
            if (movingToSite && !motor.HasRoute)
            {
                if (searching) RememberSearch(routeGoal);
                movingToSite = false;
            }
            var facts = actor.Perception.Facts;
            if (facts.Confidence <= .12f || facts.Body.size.x < .01f)
            {
                ClearAttackPosition();
                if (!movingToSite || !motor.HasRoute) SeekContact();
                return;
            }
            hasLastSeen = true;
            lastSeenPoint = facts.Position;
            lastSeenAt = facts.ObservedAt;
            searchedPoints.Clear();
            if(Mathf.Abs(facts.Velocity.x)>1.5f)
            {
                int sign=facts.Velocity.x>0?1:-1;
                if(sign!=observedTravelSign && facts.ObservedAt-travelSeenAt<.65f)turnedAt=facts.ObservedAt;
                observedTravelSign=sign;
                travelSeenAt=facts.ObservedAt;
            }
            bool probe = PreviewMove != Move.None;
            // Current spacing and a move's future destination answer different questions.
            // Both are reconstructed from the same delayed visible observation.
            Vector2 target = PublicPosition(facts, 0);
            AxePlan best = null;
            float first = float.PositiveInfinity, next = float.PositiveInfinity;
            foreach (var move in PhaseOneMoves)
            {
                attacksHere[(int)move]=null;
                if (probe && move != PreviewMove || !CanStart(move)
                    || !PlanFrom(move, probe, facts, actor.Position, game.GetBossMoveTuning(false,(int)move).Windup, out var candidate)) continue;
                if(move==Move.TripleCut && CommitTacticalAttack(candidate,probe))return;
                attacksHere[(int)move]=candidate;
                float cost=probe?candidate.FirstThreat:AttackCost(candidate,facts);
                if(cost>=first)continue;
                best=candidate; first=cost;
            }

            if (probe) ClearAttackPosition();
            else if (ChooseFootwork(facts,target,best,first)) return;
            if (best != null && CommitTacticalAttack(best, probe)) return;
            if (!(movingToSite && motor.HasRoute && !searching && Time.time < routeHoldUntil))
            {
                Vector2 intercept = PublicPosition(facts, .40f);
                Move approach=probe?PreviewMove:ApproachFor(facts,target);
                if (FindApproach(intercept,approach,ref first,ref next,out var route))
                {
                    FollowRoute(route, false);
                    return;
                }
            }
            if (motor.HasRoute) return;
            // A failed graph shortlist should still allow progress along the present
            // ledge toward the target or toward the next launch site.
            if (!ApproachOnCurrentSupport(target))
            {
                if (Mathf.Abs(target.y-actor.Position.y) > 1.45f) SeekContact();
                else motor.Brake();
            }
        }

        // Compare time to an offensive beat and the purpose of the move. Whether it
        // damages anything is resolved only by the active blade.
        float AttackCost(AxePlan plan,BossObservation facts)
        {
            float gap=Mathf.Abs(plan.Target.x-plan.Start.x);
            float away=facts.Velocity.x*plan.Facing;
            bool crossTier=Mathf.Abs(plan.Target.y-plan.Start.y)>1.3f;
            float cost=plan.FirstThreat;
            float exitGap=Mathf.Abs(plan.Target.x-plan.Landing.x);
            bool open=facts.ReloadVisible||!facts.Dodging&&Time.time-actor.Perception.LastDodgeObservedAt<.48f;
            switch(plan.Move)
            {
                case Move.TripleCut:
                    cost+=Mathf.Min(.50f,Followup(plan.Landing,plan.Target)*.26f);
                    if(open || gap<2f)cost-=.22f;
                    if(Time.time<pressUntil)cost-=.30f;
                    break;
                case Move.LeapingCrosscut:
                    cost-=(crossTier || !facts.Grounded) ? .40f : .20f;
                    break;
                case Move.AnchorVault:
                    cost-=Mathf.InverseLerp(7f,13f,gap)*.48f;
                    if(away>2f)cost-=.22f;
                    break;
                case Move.UnderpassReturn:
                    cost-=.8f*Mathf.InverseLerp(1f,5f,-away);
                    cost+=Mathf.Clamp(exitGap-6f,0,4f)*.10f;
                    break;
                case Move.PalmDeflection:
                    if(Time.time-turnedAt<.85f)cost-=.48f;
                    if(crossTier)cost-=.37f;
                    break;
            }
            if(plan.Move==lastAttack)cost+=.28f;
            int i=(int)plan.Move;
            if((plan.Move==Move.PalmDeflection || plan.Move==Move.UnderpassReturn) && Time.time<missedUntil[i])
            {
                bool sameAngle=plan.Move==Move.PalmDeflection && Mathf.Abs(plan.Start.y-missedOrigin[i].y)<.8f &&
                    Vector2.Dot((plan.Target-plan.Start).normalized,missedPalmAim)>.93f;
                if(sameAngle || Vector2.Distance(plan.Start,missedOrigin[i])<2f)cost+=.65f;
            }
            return Mathf.Max(.05f,cost);
        }

        Move ApproachFor(BossObservation facts,Vector2 target)
        {
            Vector2 delta=target-actor.Position;
            if(Mathf.Abs(delta.y)>1.35f && Ready(Move.LeapingCrosscut))return Move.LeapingCrosscut;
            if(Mathf.Abs(delta.x)>7f && facts.Velocity.x*delta.x>0 && Ready(Move.AnchorVault))return Move.AnchorVault;
            if((NeedNewOrigin(actor.Position)||Mathf.Abs(delta.y)>2f) && Ready(Move.PalmDeflection))return Move.PalmDeflection;
            return Move.None;
        }

        bool CommitTacticalAttack(AxePlan plan, bool probe)
        {
            if (!StartPhaseOne(plan)) return false;
            movingToSite = searching = false;
            positionIntent=default;
            pendingResult = lastAttack = plan.Move;
            lastStartup = plan.Start;
            hitsAtStartup = Hits[(int)plan.Move];
            if (probe) PreviewMove = Move.None;
            return true;
        }

        void RecoverFooting()
        {
            // An ordinary route owns its airborne steering. This is only the free-body
            // case, e.g. landing on the player's head after collision interrupted a move.
            // Grounded deliberately excludes actor bodies, so waiting here can last forever.
            var motor=actor.Motor;
            Vector2 start=actor.Position;
            var facts=actor.Perception.Facts;
            Vector2 observed=facts.Confidence>.12f?PublicPosition(facts,0):start;
            bool avoidBody=facts.Confidence>.12f;
            float clearance=(motor.Size.x+facts.Body.size.x)*.5f+.1f;
            Vector2 best=start;
            float bestCost=float.PositiveInfinity;
            foreach(var node in actor.Navigation.Nodes)
            {
                var p=node.Center(motor.Size);
                if(p.y>start.y+.05f || avoidBody && Mathf.Abs(p.x-observed.x)<clearance &&
                    Mathf.Abs(p.y-observed.y)<motor.Size.y)continue;
                float cost=Mathf.Abs(p.x-start.x)+Mathf.Max(0,start.y-p.y)*.30f;
                if(cost>=bestCost || !actor.Navigation.CanStand(p,motor.Size))continue;
                // Check the immediate lateral exit. Actual descent and landing remain physical.
                float dx=Mathf.Clamp(p.x-start.x,-.65f,.65f);
                if(Mathf.Abs(dx)>.05f && !actor.Navigation.TryDash(start,start+Vector2.right*dx,motor.Size,out _))continue;
                bestCost=cost; best=p;
            }
            motor.SetMove(Mathf.Clamp((best.x-start.x)*2f,-1f,1f));
        }

        struct GroundPressure
        {
            public float Direction, Speed, FirstThreat;
        }

        bool TryGroundPressure(BossObservation facts, Vector2 target, out GroundPressure pursuit)
        {
            pursuit = default;
            var motor = actor.Motor;
            var nav = actor.Navigation;
            Vector2 start = actor.Position;
            if (!nav.TrySupport(start, motor.Size, out var support)) return false;
            if (!nav.TrySupport(target, facts.Body.size, out var theirs) || theirs != support) return false;

            float side = Mathf.Sign(target.x - start.x);
            float retreat = facts.Velocity.x * side;
            // Lead a running player on this surface, but do not run through them or
            // off the end of this support when they turn back or jump away.
            var ahead = PublicPosition(facts, .38f);
            if (!nav.TrySupport(ahead, facts.Body.size, out var aheadSupport) || aheadSupport != support)
                ahead = target;
            float aimX = ahead.x - side * 1.25f;
            float min = support.bounds.min.x + motor.Size.x * .5f + .05f;
            float max = support.bounds.max.x - motor.Size.x * .5f - .05f;
            if (min >= max) return false;
            aimX = Mathf.Clamp(aimX, min, max);
            float travel = (aimX - start.x) * side;
            // At contact a legal attack should beat waiting at an ideal spacing point.
            float wind = game.GetBossMoveTuning(false, (int)Move.TripleCut).Windup + .16f;
            float reachDelay = Mathf.Max(0, readyAt[(int)Move.TripleCut] - Time.time);
            if (travel <= .12f)
            {
                pursuit.FirstThreat = wind + reachDelay;
                return true;
            }
            if (!SameSupportWalk(start, new Vector2(aimX, start.y))) return false;
            float speed = motor.Profile.Speed;
            if (retreat > 1.5f && travel > .7f)
                speed = Mathf.Lerp(speed, 10.5f, Mathf.InverseLerp(1.5f, 5.5f, retreat));
            pursuit.Direction = side;
            pursuit.Speed = Mathf.Min(speed, travel / Mathf.Max(.12f, actor.Profile.ReactionSeconds));
            float closingSpeed = Mathf.Max(1f, speed - Mathf.Clamp(retreat, -7f, 7f));
            float closeTime = Mathf.Max(0, Mathf.Abs(target.x - start.x) - 2.45f) / closingSpeed;
            pursuit.FirstThreat = Mathf.Max(reachDelay, closeTime) + wind;
            return true;
        }


        bool ApproachOnCurrentSupport(Vector2 target)
        {
            var motor = actor.Motor;
            var nav = actor.Navigation;
            Vector2 start = actor.Position;
            if (!nav.TrySupport(start, motor.Size, out var support)) return false;
            float margin = motor.Size.x * .5f + .08f;
            float min = support.bounds.min.x + margin, max = support.bounds.max.x - margin;
            if (min >= max) return false;
            float x = Mathf.Clamp(target.x, min, max);
            float travel = x - start.x;
            if (Mathf.Abs(travel) < .18f) return false;
            float step = Mathf.Clamp(travel, -.95f, .95f);
            if (!SameSupportWalk(start, start + Vector2.right * step)) return false;
            motor.PursueOnGround(Mathf.Sign(travel), Mathf.Min(motor.Profile.Speed, Mathf.Abs(step) / .14f));
            return true;
        }

        static bool Better(float first, float next, float bestFirst, float bestNext)
        {
            if (Mathf.Abs(first-bestFirst) > .25f) return first < bestFirst;
            return next + .025f < bestNext || Mathf.Abs(next-bestNext) <= .025f && first < bestFirst;
        }

        void ObserveLastAttack()
        {
            if (pendingResult == Move.None) return;
            int move=(int)pendingResult;
            bool hit=Hits[move]>hitsAtStartup;
            if(!hit)
            {
                missedUntil[move]=Mathf.Max(Time.time+5f,readyAt[move]+1.25f);
                missedOrigin[move]=lastStartup;
                if(pendingResult==Move.PalmDeflection)missedPalmAim=(acceptedPlan.Target-lastStartup).normalized;
            }
            else missedUntil[move]=0;
            if(pendingResult==Move.AnchorVault && reboundAt>=0 ||
               pendingResult==Move.UnderpassReturn && caught && !abortAfterCatch)pressUntil=Time.time+1.1f;
            if (pendingResult == Move.UnderpassReturn || pendingResult == Move.PalmDeflection)
            {
                if (hit) sameSpotMisses = 0;
                else
                {
                    if (Vector2.Distance(missedFrom,lastStartup) > 2f) sameSpotMisses = 0;
                    missedFrom = lastStartup;
                    sameSpotMisses++;
                    if (pendingResult==Move.PalmDeflection || sameSpotMisses>=2)relocateUntil=Time.time+2.4f;
                }
            }
            pendingResult = Move.None;
        }

        Vector2 PublicPosition(BossObservation facts, float after)
        {
            if (trajectoryFrame != Time.frameCount)
            {
                trajectoryFrame = Time.frameCount;
                actor.Tactics.PredictSwordPath(facts, publicTrajectory);
            }
            // The shared bounded predictor processes gravity, platforms and the end of
            // an observed dodge. Never extrapolate its burst velocity indefinitely.
            float age = Mathf.Clamp(Time.time - facts.ObservedAt, 0, .25f);
            float sample = Mathf.Clamp((age + after) / Time.fixedDeltaTime, 0, publicTrajectory.Count - 1);
            int index = Mathf.FloorToInt(sample);
            return Vector2.Lerp(publicTrajectory[index], publicTrajectory[Mathf.Min(index+1,publicTrajectory.Count-1)], sample-index);
        }

        bool TryPlanPhaseOne(Move move, bool preview, out AxePlan plan)
        {
            plan = null;
            var facts = actor.Perception.Facts;
            return CanStart(move) && facts.Confidence > .12f && facts.Body.size.x > .01f
                && PlanFrom(move, preview, facts, actor.Position, game.GetBossMoveTuning(false,(int)move).Windup, out plan);
        }

        bool LockPhaseOneAim()
        {
            // The first cut keeps the facing that started its advancing pose. Other
            // moves take a final sighting without re-running the decision to attack.
            if(Current==Move.TripleCut)return true;
            var facts=actor.Perception.Facts;
            if(!facts.Visible || facts.Confidence<=.12f || facts.Body.size.x<=0)return true;
            var plan=AimAt(Current,facts,actor.Position,PublicPosition(facts,0),0,0);
            if(!PlanMotion(plan,facts,0))return false;
            acceptedPlan=plan;
            actor.Face(Vector2.right*plan.Facing);
            lockedFacing=actor.Facing;
            BuildFlight(plan,flight);
            return true;
        }

        // Eligibility depends on the situation the Boss can see. Once a move is
        // accepted, only its physical execution can fail. Damage is resolved live.
        AxePlan ReadOpening(Move move,bool preview,BossObservation facts,Vector2 start,float travel,float wind)
        {
            Vector2 seen=PublicPosition(facts,travel);
            Vector2 delta=seen-start;
            float gap=Mathf.Abs(delta.x), height=Mathf.Abs(delta.y);
            switch(move)
            {
                case Move.TripleCut:
                    if(gap>4.2f || height>=1.55f ||
                        Projectile.TerrainHit(start+Vector2.up*.15f,seen,.08f,out _,false))return null;
                    break;
                case Move.LeapingCrosscut:
                    if(gap>8f || height>=5f || gap<1.55f && height<1.25f)return null;
                    break;
                case Move.AnchorVault:
                    if(gap<6.5f || gap>16f || height>5f)return null;
                    break;
                case Move.UnderpassReturn:
                    if(gap<3.45f || gap>9f || height>=1.55f ||
                        !preview && (facts.Velocity.x*Mathf.Sign(delta.x)>-1.25f || !facts.Grounded && facts.Velocity.y>0))
                        return null;
                    break;
                case Move.PalmDeflection:
                    if(gap>11f || height>4.8f || gap<3.3f && height<1.65f ||
                        !preview && (height<1.65f && gap<8.5f || NeedNewOrigin(start)))return null;
                    break;
                default:return null;
            }
            return AimAt(move,facts,start,seen,travel,wind);
        }

        AxePlan AimAt(Move move,BossObservation facts,Vector2 start,Vector2 seen,float travel,float wind)
        {
            float dx=seen.x-start.x;
            float face=Mathf.Abs(dx)>.10f?Mathf.Sign(dx):actor.Facing;
            float lead=move==Move.TripleCut?.08f:.20f;
            Vector2 aim=Vector2.MoveTowards(seen,PublicPosition(facts,travel+lead),1.1f);
            if((aim.x-start.x)*face<0)aim.x=seen.x;
            float firstBeat=move switch
            {
                Move.TripleCut => 0, Move.LeapingCrosscut => .25f, Move.AnchorVault => .40f,
                Move.UnderpassReturn => .45f, _ => .36f
            };
            return new AxePlan { Move=move, Start=start, Target=aim, Facing=face, FinishFacing=face,
                Landing=start, Retreat=start, FirstThreat=wind+Timings[(int)move].Lock+firstBeat };
        }

        bool PlanFrom(Move move,bool preview,BossObservation facts,Vector2 start,float wind,out AxePlan plan)
        {
            plan=ReadOpening(move,preview,facts,start,0,wind);
            return plan!=null && PlanMotion(plan,facts,wind);
        }

        bool PlanMotion(AxePlan plan,BossObservation facts,float wind)
        {
            switch(plan.Move)
            {
                case Move.TripleCut: return true;
                case Move.LeapingCrosscut: return PlanCrosscut(plan,facts,wind);
                case Move.AnchorVault: return PlanVault(plan);
                case Move.UnderpassReturn: return PlanUnderpass(plan);
                case Move.PalmDeflection: return PlanDeflection(plan);
                default:return false;
            }
        }

        bool NeedNewOrigin(Vector2 point) => Time.time < relocateUntil && Vector2.Distance(point,missedFrom) < 1.9f;
        float Followup(Vector2 point, Vector2 target)
        {
            Vector2 delta = target-point;
            return Mathf.Max(0,Mathf.Abs(delta.x)-2f)/actor.Motor.Profile.Speed + Mathf.Abs(delta.y)*.25f;
        }

        BossMotionProfile LeapMotion(float rise)
        {
            if (rise > 2.7f) return twoTierLeap ??= CreateLeapMotion(17.2f);
            if (rise > .6f) return oneTierLeap ??= CreateLeapMotion(14.2f);
            return lowLeap ??= CreateLeapMotion(8.4f);
        }

        BossMotionProfile CreateLeapMotion(float jumpSpeed) => new BossMotionProfile {
            Speed=14.5f, Acceleration=120f, Braking=95f, JumpSpeed=jumpSpeed,
            GravityScale=actor.Motor.Profile.GravityScale, AirControl=1, ArrivalBraking=true
        };

        bool PlanCrosscut(AxePlan plan, BossObservation facts, float wind)
        {
            var motor = actor.Motor;
            Vector2 launch=plan.Start;
            float advance=wind>0?CrosscutRunIn((plan.Target.x-plan.Start.x)*plan.Facing):0;
            Vector2 stepped=launch+Vector2.right*(advance*plan.Facing);
            if (advance>0 && SameSupportWalk(launch,stepped)) launch=stepped;
            float release=wind+Timings[(int)Move.LeapingCrosscut].Lock;
            bool intercept=TryReadLanding(facts,release,out var landingTarget);
            RankCrosscutLandings(plan,intercept,landingTarget);
            foreach (var site in attackSites)
            {
                float shift=(site.Point.x-launch.x)*plan.Facing;
                if (shift<-.15f || shift>9f) continue;
                var motion=LeapMotion(site.Point.y-launch.y);
                if (!actor.Navigation.TryJumpTrajectory(launch,site.Point,motion,motor.Size,out var route,out var path)
                    || !PathInBounds(path)) continue;

                // The destination is ranked by where the opponent was seen. The blade
                // passes through that lane even when the opponent subsequently leaves.
                Vector2 aim=intercept?landingTarget:plan.Target;
                float touchdown=(path.Length-1)*Time.fixedDeltaTime;
                float latest=Mathf.Max(.06f,touchdown-.21f);
                float at=ClosestCutStart(path,aim,plan.Facing,Mathf.Min(.08f,latest),latest,.11f);
                plan.Swing=CrosscutForPath(path,at);
                plan.FirstThreat=release+at;
                plan.Landing=site.Point;
                plan.JumpMotion=motion;
                plan.JumpRoute=route;
                plan.JumpPath=path;
                return true;
            }
            return false;
        }

        // Choose a swing time along an already feasible jump, never a condition for
        // starting it. The target is a snapshot, including during a real whiff.
        static float ClosestCutStart(Vector2[] path,Vector2 target,float facing,float earliest,float latest,float toEdge)
        {
            float dt=Time.fixedDeltaTime;
            float best=earliest, score=float.PositiveInfinity;
            for(float at=earliest;;at=Mathf.Min(latest,at+dt))
            {
                int k=Mathf.Clamp(Mathf.RoundToInt((at+toEdge)/dt),0,path.Length-1);
                Vector2 delta=target-path[k];
                float cost=Mathf.Abs(delta.x*facing-1.3f)+Mathf.Abs(delta.y+.25f)*.70f;
                if(cost<score){best=at;score=cost;}
                if(at>=latest)return best;
            }
        }

        static Cut CrosscutForPath(Vector2[] path,float start)
        {
            int first=Mathf.Clamp(Mathf.RoundToInt(start/Time.fixedDeltaTime),0,path.Length-1);
            int last=Mathf.Clamp(Mathf.RoundToInt((start+.21f)/Time.fixedDeltaTime),0,path.Length-1);
            return CrosscutAt(start,path[last].y-path[first].y);
        }


        bool TryReadLanding(BossObservation facts,float release,out Vector2 landing)
        {
            float start=Mathf.Min(release,1.2f);
            Vector2 previous=PublicPosition(facts,Mathf.Max(0,start-.10f));
            bool descending=!facts.Grounded && facts.Velocity.y<0;
            for(float t=start;t<=1.25f;t+=.10f)
            {
                var point=PublicPosition(facts,t);
                descending|=point.y<previous.y-.02f;
                if(descending && point.y<=previous.y+.02f && actor.Navigation.TrySupport(point,facts.Body.size,out _))
                {
                    landing=point;
                    return true;
                }
                previous=point;
            }
            landing=default;
            return false;
        }

        void RankCrosscutLandings(AxePlan plan,bool intercept,Vector2 landingTarget)
        {
            attackSites.Clear();
            var size=actor.Motor.Size;
            foreach (var node in actor.Navigation.Nodes)
            {
                var p=node.Center(size);
                float advance=(p.x-plan.Start.x)*plan.Facing;
                if(advance<0 || advance>9f || Mathf.Abs(p.y-plan.Start.y)>4.8f)continue;
                bool nearNow=Mathf.Abs(p.x-plan.Target.x)<=3.2f && Mathf.Abs(p.y-plan.Target.y)<=2.45f;
                bool nearLanding=intercept && Mathf.Abs(p.x-landingTarget.x)<=3.2f && Mathf.Abs(p.y-landingTarget.y)<=2.45f;
                if(!nearNow && !nearLanding && (advance<1f ||
                    Vector2.Distance(p,plan.Target)+.8f>=Vector2.Distance(plan.Start,plan.Target)))continue;
                if(advance<.5f && Mathf.Abs(p.y-plan.Start.y)<.5f && Mathf.Abs(plan.Target.y-plan.Start.y)<1.25f)continue;
                var ideal=(nearLanding?landingTarget:plan.Target)-Vector2.right*plan.Facing*.85f;
                attackSites.Add(new Site { Point=p,Cost=(p-ideal).sqrMagnitude });
            }
            attackSites.Sort(SiteOrder);
        }

        static int SiteOrder(Site a, Site b) => a.Cost.CompareTo(b.Cost);
        bool WithinArena(Vector2 center, Vector2 size)
        {
            var b=actor.Navigation.Bounds;
            var h=size*.5f;
            return center.x-h.x>=b.min.x && center.x+h.x<=b.max.x &&
                center.y-h.y>=b.min.y-.04f && center.y+h.y<=b.max.y-.015f;
        }
        bool PathInBounds(Vector2[] path)
        {
            foreach(var p in path) if(!WithinArena(p,actor.Motor.Size)) return false;
            return true;
        }

        bool PlanUnderpass(AxePlan plan)
        {
            if(!actor.Navigation.TrySupport(plan.Start,actor.Motor.Size,out var support))return false;
            float dx=Mathf.Abs(plan.Target.x-plan.Start.x);
            float stride=Mathf.Clamp(2.25f-dx*.12f,.85f,1.75f);
            float margin=actor.Motor.Size.x*.5f+.06f;
            float limit=plan.Facing>0?support.bounds.min.x+margin:support.bounds.max.x-margin;
            stride=Mathf.Min(stride,(plan.Start.x-limit)*plan.Facing);
            if(stride<.75f)return false;
            var retreat=plan.Start - Vector2.right*plan.Facing*stride;
            if (!SameSupportWalk(plan.Start,retreat)) return false;
            if (!actor.Navigation.TryJumpTrajectory(retreat,retreat,actor.Motor,out var route,out var path)
                || !PathInBounds(path)) return false;
            plan.Retreat=plan.Landing=retreat;
            plan.JumpRoute=route; plan.JumpPath=path; plan.JumpMotion=actor.Motor.Profile;
            plan.Turn=BrakePoint(plan,1.0f,.40f,3.6f,9.2f);
            return (plan.Turn.x-plan.Start.x)*plan.Facing>=3.6f && FlightCorridorClear(plan);
        }
        bool PlanDeflection(AxePlan plan)
        {
            float minimumLane=Mathf.Abs(plan.Target.y-plan.Start.y)>1.5f?1.0f:3.0f;
            plan.Turn=BrakePoint(plan,1.60f,.38f,minimumLane,10.6f);
            if ((plan.Turn.x-plan.Start.x)*plan.Facing<minimumLane) return false;
            for (int step=3;step>=0;step--)
            {
                var catchPoint=plan.Start+Vector2.right*plan.Facing*(step*.65f);
                if(!SameSupportWalk(plan.Start,catchPoint))continue;
                plan.Landing=catchPoint;
                if (FlightCorridorClear(plan))return true;
            }
            return false;
        }
        Vector2 BrakePoint(AxePlan plan,float overrun,float height,float minTravel,float maxTravel)
        {
            var b=actor.Navigation.Bounds;
            var p=plan.Target+new Vector2(plan.Facing*overrun,height);
            float forward=Mathf.Clamp((p.x-plan.Start.x)*plan.Facing,minTravel,maxTravel);
            p.x=plan.Start.x+plan.Facing*forward;
            return ArenaBoundary2D.ClampPoint(b,p,SpinningClearance);
        }
        bool SameSupportWalk(Vector2 start,Vector2 end)
        {
            var nav=actor.Navigation;var size=actor.Motor.Size;
            if (!nav.TrySupport(start,size,out var support)||!nav.TryDash(start,end,size,out _))return false;
            int count=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(start,end)/.30f));
            for(int i=1;i<=count;i++)
            {
                var p=Vector2.Lerp(start,end,(float)i/count);
                if(!nav.CanStand(p,size)||!nav.TrySupport(p,size,out var at)||at!=support)return false;
            }
            return true;
        }

        // Shortlist actual support samples on either side of the target. The graph is
        // consulted for those destinations only; ordinary same-support walks bypass A*.
        bool FindApproach(Vector2 target, Move desiredMove, ref float bestFirst, ref float bestNext,
            out ArenaNavigation2D.Route chosen)
        {
            chosen=null;
            var nav=actor.Navigation;var motor=actor.Motor;
            Vector2 start=actor.Position;
            float ideal=ApproachRange(desiredMove);
            approachSites.Clear();
            foreach(var node in nav.Nodes)
            {
                var point=node.Center(motor.Size);
                float travel=Vector2.Distance(point,start);
                if(travel<.55f||RecentlyFailed(point))continue;
                var delta=point-target;
                float gap=Mathf.Abs(delta.x), height=Mathf.Abs(delta.y);
                if(height<1.45f && gap<1.05f)continue;
                if(desiredMove==Move.None && !NeedNewOrigin(start) && Mathf.Abs(start.y-target.y)<1.2f
                    && Mathf.Abs(start.x-target.x)<=2.65f && height<1.2f
                    && Mathf.Abs(gap-ideal)+.2f>=Mathf.Abs(Mathf.Abs(start.x-target.x)-ideal))continue;
                // Never route through the target's solid body to stand on its far side.
                if(height<1.2f && Mathf.Abs(start.y-target.y)<1.2f && delta.x*(start.x-target.x)<0)continue;
                if(NeedNewOrigin(start)&&Vector2.Distance(missedFrom,point)<1.9f)continue;
                int side=delta.x<0?-1:1;
                float cost=Mathf.Abs(gap-ideal)*.85f+height*1.55f+travel*.18f;
                KeepSite(approachSites,new Site{Point=point,Surface=node.SurfaceId,Side=side,Cost=cost});
            }
            approachSites.Sort(SiteOrder);
            int graphAttempts=0;
            nav.BeginRouteBatch();
            try
            {
                foreach(var site in approachSites)
                {
                    Vector2 point=site.Point;
                    float remaining=Followup(point,target);
                    float residual=desiredMove==Move.None?remaining
                        :Mathf.Abs(Mathf.Abs(point.x-target.x)-ideal)/motor.Profile.Speed+Mathf.Abs(point.y-target.y)*.25f;
                    float travelLowerBound=Vector2.Distance(start,point)/motor.Profile.Speed;
                    if(travelLowerBound+.48f+residual>bestFirst+.25f)continue;
                    if(!RouteTo(point,ref graphAttempts,out var path))continue;
                    float first=path.Duration + .48f + residual;
                    if(desiredMove==Move.None && remaining<.16f)
                        first=Mathf.Max(path.Duration,readyAt[(int)Move.TripleCut]-Time.time)+.48f+residual;
                    if(!Better(first,remaining,bestFirst,bestNext))continue;
                    bestFirst=first;bestNext=remaining;chosen=path;
                }
            }
            finally {nav.EndRouteBatch();}
            return chosen!=null;
        }

        static float ApproachRange(Move desired)
        {
            switch(desired)
            {
                case Move.LeapingCrosscut:return 3.8f;
                case Move.AnchorVault:return 8.0f;
                case Move.PalmDeflection:case Move.UnderpassReturn:return 6.2f;
                default:return 1.75f;
            }
        }

        void KeepSite(List<Site> list, Site item)
        {
            for(int i=0;i<list.Count;i++)
            {
                if(list[i].Surface!=item.Surface||list[i].Side!=item.Side)continue;
                if(item.Cost<list[i].Cost && actor.Navigation.CanStand(item.Point,actor.Motor.Size))list[i]=item;
                return;
            }
            if(actor.Navigation.CanStand(item.Point,actor.Motor.Size))list.Add(item);
        }
        bool RecentlyFailed(Vector2 point) => Time.time<failedRouteUntil && Vector2.Distance(point,failedRouteGoal)<.85f;

        readonly Dictionary<Vector2,float> unreachableUntil = new Dictionary<Vector2,float>();
        int reachabilityVersion=-1;
        bool RouteTo(Vector2 goal,ref int graphAttempts,out ArenaNavigation2D.Route route)
        {
            var nav=actor.Navigation;var motor=actor.Motor;
            route=null;
            if(SameSupportWalk(actor.Position,goal)&&nav.TrySupport(goal,motor.Size,out var support))
            {
                float duration=Vector2.Distance(actor.Position,goal)/motor.Profile.Speed+.10f;
                route=new ArenaNavigation2D.Route{Version=nav.Version,Duration=duration};
                route.Steps.Add(new ArenaNavigation2D.RouteStep{Kind=ArenaNavigation2D.TravelKind.Walk,
                    Start=actor.Position,Launch=actor.Position,End=goal,Support=support,Duration=duration});
                return true;
            }
            if(reachabilityVersion!=nav.Version)
            { unreachableUntil.Clear();reachabilityVersion=nav.Version; }
            if(unreachableUntil.TryGetValue(goal,out float until)&&Time.time<until)return false;
            if(graphAttempts>=3)return false;
            graphAttempts++;
            if(nav.TryRoute(actor.Position,goal,motor,out route)&&route.Steps.Count>0)return true;
            unreachableUntil[goal]=Time.time+1.25f;
            return false;
        }
        void FollowRoute(ArenaNavigation2D.Route route,bool isSearch,bool exact=false)
        {
            var motor=actor.Motor;
            var end=route.Steps[route.Steps.Count-1].End;
            if(movingToSite && motor.HasRoute && searching==isSearch && Vector2.Distance(routeGoal,end)<(exact?.08f:.65f))return;
            routeGoal=end;routeHoldUntil=Time.time+.45f;movingToSite=true;searching=isSearch;
            motor.SetRoute(route);
        }

        void RememberSearch(Vector2 reached)
        {
            if(searchedPoints.Count==6)searchedPoints.RemoveAt(0);
            searchedPoints.Add(reached);
        }
        void SeekContact()
        {
            var nav=actor.Navigation;var motor=actor.Motor;
            searchSites.Clear();
            // Go to the last public sighting first, then search different reachable
            // viewing positions. Never query the hidden player's current support or pose.
            foreach(var node in nav.Nodes)
            {
                var p=node.Center(motor.Size);float distance=Vector2.Distance(p,actor.Position);
                if(distance<1.35f||RecentlyFailed(p))continue;
                float cost=hasLastSeen && Time.time-lastSeenAt<2.4f?Vector2.Distance(p,lastSeenPoint):Mathf.Abs(distance-6f);
                foreach(var seen in searchedPoints)
                    cost+=Mathf.Max(0,4f-Vector2.Distance(p,seen))*2f;
                cost+=distance*.10f;
                KeepSite(searchSites,new Site{Surface=node.SurfaceId,Side=p.x<actor.Position.x?-1:1,Point=p,Cost=cost});
            }
            searchSites.Sort(SiteOrder);
            int graphAttempts=0;
            nav.BeginRouteBatch();
            try
            {
                foreach(var site in searchSites)
                {
                    if(!RouteTo(site.Point,ref graphAttempts,out var route))continue;
                    FollowRoute(route,true);return;
                }
            }
            finally{nav.EndRouteBatch();}
            motor.Brake();
        }
    }
}
