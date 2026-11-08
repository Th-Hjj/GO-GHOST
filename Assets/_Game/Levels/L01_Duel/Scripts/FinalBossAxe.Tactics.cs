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
            public float Facing, FirstThreat;
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
        BossMotionProfile crosscutMotion;
        float nextTacticsTick, routeHoldUntil, failedRouteUntil, relocateUntil, lastSeenAt;
        Vector2 routeGoal, failedRouteGoal, lastSeenPoint, missedFrom;
        bool movingToSite, searching, hasLastSeen;
        float laneUntil, laneRetryAt, laneEndX;
        Move lastAttack, pendingResult;
        Vector2 lastStartup;
        int hitsAtStartup, sameSpotMisses;

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
            ObserveLastAttack();

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
                if (!movingToSite || !motor.HasRoute) SeekContact();
                return;
            }
            hasLastSeen = true;
            lastSeenPoint = facts.Position;
            lastSeenAt = facts.ObservedAt;
            searchedPoints.Clear();
            bool probe = PreviewMove != Move.None;
            // Current spacing and a move's future destination answer different questions.
            // Both are reconstructed from the same delayed visible observation.
            Vector2 target = PublicPosition(facts, 0);
            AxePlan best = null;
            float first = float.PositiveInfinity, next = float.PositiveInfinity;
            foreach (var move in PhaseOneMoves)
            {
                if (probe && move != PreviewMove || !CanStart(move)
                    || !PlanForTarget(move, probe, facts, target, game.GetBossMoveTuning(false,(int)move).Windup, out var candidate)) continue;
                float arrival = candidate.FirstThreat;
                // The player saw the previous action. Prefer a different good answer when available.
                if (!probe && move == lastAttack) arrival += .29f;
                float follow = Followup(candidate.Landing, candidate.Target);
                bool opening = facts.ReloadVisible || (!facts.Dodging && Time.time-actor.Perception.LastDodgeObservedAt < .48f);
                if (!probe && opening && (move == Move.TripleCut || move == Move.LeapingCrosscut)
                    && candidate.FirstThreat < 1f) arrival -= .20f;
                if(move==Move.UnderpassReturn)
                {
                    // Meeting a fast approach with a low returning pass both threatens the
                    // lead lane and avoids landing in the opponent's body. Value that exit.
                    float incoming=-facts.Velocity.x*candidate.Facing;
                    float timeToClose=(Mathf.Abs(facts.Position.x-candidate.Start.x)-2f)/Mathf.Max(1f,incoming);
                    float intercept=Mathf.InverseLerp(1.35f,.35f,timeToClose);
                    float disengage=game.GetBossMoveTuning(false,(int)move).Windup+.16f;
                    arrival=Mathf.Lerp(arrival,disengage,intercept);
                    follow-=.90f*intercept;
                }
                if (best != null && !Better(arrival, follow, first, next)) continue;
                best = candidate; first = arrival; next = follow;
            }

            // Chase on connected ground with persistent motor input. A running target can
            // move several body widths during a held graph route. There is no reason to
            // brake at a sampled node, or wait for a route hold to start a legal attack.
            GroundPressure pursuit = default;
            bool direct = !probe && TryGroundPressure(facts, target, out pursuit);
            if (direct)
            {
                if (best == null && TryOpenAttackLane(facts,target)) return;
                if (best == null || (Time.time >= laneUntil && pursuit.Direction != 0 && pursuit.FirstThreat + .18f < first))
                {
                    movingToSite = searching = false;
                    motor.PursueOnGround(pursuit.Direction, pursuit.Speed);
                    return;
                }
            }
            else if ((!probe || best == null) &&
                     !(movingToSite && motor.HasRoute && !searching && Time.time < routeHoldUntil))
            {
                Vector2 intercept = PublicPosition(facts, .40f);
                if (FindApproach(intercept, probe ? PreviewMove : Move.None, ref first, ref next, out var route))
                {
                    FollowRoute(route, false);
                    return;
                }
            }
            if (best != null && StartPhaseOne(best))
            {
                movingToSite = searching = false;
                laneUntil = 0;
                pendingResult = lastAttack = best.Move;
                lastStartup = best.Start;
                hitsAtStartup = Hits[(int)best.Move];
                if (probe) PreviewMove = Move.None;
                return;
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

        bool TryOpenAttackLane(BossObservation facts, Vector2 target)
        {
            var motor = actor.Motor;
            Vector2 start = actor.Position;
            float dx = target.x-start.x;
            float side = Mathf.Sign(dx);
            float tripleWait = readyAt[(int)Move.TripleCut]-Time.time;
            if (tripleWait < .50f || facts.Velocity.x*side > 1.5f)
            {
                laneUntil = 0;
                return false;
            }
            if (Time.time >= laneUntil)
            {
                if (Time.time < laneRetryAt || Mathf.Abs(dx) > 2.0f ||
                    readyAt[(int)Move.LeapingCrosscut] > Time.time+.55f) return false;
                if (!actor.Navigation.TrySupport(start,motor.Size,out var support)) return false;
                float margin=motor.Size.x*.5f+.08f;
                float min=support.bounds.min.x+margin, max=support.bounds.max.x-margin;
                if (min >= max) return false;
                float goal=Mathf.Clamp(target.x-side*3.65f,min,max);
                if ((target.x-goal)*side < 2.7f || !SameSupportWalk(start,new Vector2(goal,start.y))) return false;
                laneEndX=goal;
                laneUntil=Time.time+Mathf.Abs(goal-start.x)/motor.Profile.Speed+.22f;
                // Commit this setup once during the cooldown. A fresh legal attack or
                // an observed retreat ends it early, with no frame-to-frame direction flips.
                laneRetryAt=Time.time+tripleWait;
            }
            float travel=laneEndX-start.x;
            if (travel*side >= -.12f || !SameSupportWalk(start,new Vector2(laneEndX,start.y)))
            {
                laneUntil=0;
                return false;
            }
            movingToSite=searching=false;
            motor.PursueOnGround(Mathf.Sign(travel),Mathf.Min(motor.Profile.Speed,Mathf.Abs(travel)/.13f));
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
            if (pendingResult == Move.UnderpassReturn || pendingResult == Move.PalmDeflection)
            {
                if (Hits[(int)pendingResult] > hitsAtStartup) sameSpotMisses = 0;
                else
                {
                    if (Vector2.Distance(missedFrom,lastStartup) > 2f) sameSpotMisses = 0;
                    missedFrom = lastStartup;
                    if (++sameSpotMisses >= 2) relocateUntil = Time.time + 2.4f;
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
                && PlanForTarget(move, preview, facts, PublicPosition(facts,0), game.GetBossMoveTuning(false,(int)move).Windup, out plan);
        }

        bool TryRefreshPhaseOnePlan(Move move, out AxePlan plan)
        {
            var facts = actor.Perception.Facts;
            plan = null;
            return facts.Confidence > .12f && facts.Body.size.x > .01f
                && PlanForTarget(move, true, facts, PublicPosition(facts,0), 0, out plan);
        }

        bool PlanForTarget(Move move, bool preview, BossObservation facts, Vector2 present, float wind, out AxePlan plan)
        {
            Vector2 target = ProjectedAttackTarget(move, facts, wind);
            Vector2 start = actor.Position, delta = present-start;
            float dx = Mathf.Abs(delta.x), dy = Mathf.Abs(delta.y);
            float face = Mathf.Abs(target.x-start.x) > .05f ? Mathf.Sign(target.x-start.x) : actor.Facing;
            float lead = (target.x-start.x)*face;
            bool leaving = facts.Velocity.x*face > 2f && delta.x*face >= -.1f;
            plan = new AxePlan { Move=move, Start=start, Target=target, Facing=face, Landing=start, Retreat=start };
            switch (move)
            {
                case Move.TripleCut:
                    plan.FirstThreat = wind + .16f;
                    return dx <= 2.85f && dy < 1.25f && lead <= (wind>0 ? 3.55f : 2.45f)
                        && !Projectile.TerrainHit(start+Vector2.up*.15f,target,.10f,out _);
                case Move.LeapingCrosscut:
                    return (dx>=1.8f || leaving && lead>=2.3f) && dx<=6.6f && dy<1.35f && PlanCrosscut(plan,facts,wind);
                case Move.AnchorVault:
                    // A runner beside the Boss now can already be in range when the axe
                    // arrives. Such an intercept is useful even from shoulder to shoulder.
                    if (delta.magnitude<3.4f && !(leaving && lead>=3.4f) || delta.magnitude>8.25f || !PlanVault(plan)) return false;
                    plan.FirstThreat = wind + .18f + FlightOpportunity(plan, facts, wind+.18f, 1.55f);
                    return true;
                case Move.UnderpassReturn:
                    if (!preview && (facts.Velocity.x * face > -1.15f || NeedNewOrigin(start))) return false;
                    if(dx < 3.45f || dx > 8.6f || dy >= 1.45f || !PlanUnderpass(plan))return false;
                    plan.FirstThreat = wind + .16f + FlightOpportunity(plan, facts, wind+.16f, 1.36f);
                    return true;
                case Move.PalmDeflection:
                    if(!preview && NeedNewOrigin(start) || dx<3.9f && !(leaving && lead>=3.9f) ||
                        dx>9.1f || dy>2.7f || !PlanDeflection(plan))return false;
                    float first=FlightOpportunity(plan,facts,wind+.18f,2.5f);
                    if(!preview && first>=2.5f && Followup(plan.Landing,target)>.18f)return false;
                    plan.FirstThreat=wind+.18f+first;
                    return true;
                default: return false;
            }
        }

        bool NeedNewOrigin(Vector2 point) => Time.time < relocateUntil && Vector2.Distance(point,missedFrom) < 1.9f;
        float Followup(Vector2 point, Vector2 target)
        {
            Vector2 delta = target-point;
            return Mathf.Max(0,Mathf.Abs(delta.x)-2f)/actor.Motor.Profile.Speed
                + Mathf.Abs(delta.y)*.25f;
        }

        // FlightCorridorClear has just built the candidate's actual public flight segments.
        // A high arc that misses the observed body does not earn an instant hit opportunity.
        float FlightOpportunity(AxePlan plan,BossObservation facts,float releaseAfter,float otherContact)
        {
            Vector3 size=facts.Body.size;
            var window=new Bounds(Vector3.zero,new Vector3(size.x+2*FlightEdgeRadius,size.y+2*FlightEdgeRadius,1f));
            foreach(var leg in candidateFlight)
            {
                if(leg.Group<0)continue;
                var from=leg.Edge(0,plan.Facing) - PublicPosition(facts,releaseAfter+leg.Start);
                for(int i=1;i<=leg.Samples;i++)
                {
                    float segment=(leg.End-leg.Start)/leg.Samples;
                    var to=leg.Edge(i,plan.Facing) - PublicPosition(facts,releaseAfter+leg.Start+i*segment);
                    var delta=to-from;
                    if(window.Contains(from))return leg.Start+(i-1)*segment;
                    if(delta.sqrMagnitude>.000001f && window.IntersectRay(new Ray(from,delta.normalized),out float distance)
                        && distance<=delta.magnitude)return leg.Start+(i-1+distance/delta.magnitude)*segment;
                    from=to;
                }
            }
            return otherContact;
        }

        bool PlanCrosscut(AxePlan plan, BossObservation facts, float wind)
        {
            var motor = actor.Motor;
            crosscutMotion ??= new BossMotionProfile {
                Speed=12.5f, Acceleration=115f, Braking=95f, JumpSpeed=7.3f,
                GravityScale=motor.Profile.GravityScale, AirControl=1, ArrivalBraking=true
            };
            Vector2 launch=plan.Start;
            float advance=wind>0?CrosscutRunIn((plan.Target.x-plan.Start.x)*plan.Facing):0;
            Vector2 stepped=launch+Vector2.right*(advance*plan.Facing);
            if (advance>0 && SameSupportWalk(launch,stepped)) launch=stepped;
            var ideal = plan.Target - Vector2.right*plan.Facing*.85f;
            RankLandings(plan, ideal, false);
            bool found=false;
            float best=float.PositiveInfinity;
            foreach (var site in attackSites)
            {
                float shift=(site.Point.x-launch.x)*plan.Facing;
                if (shift<1.35f||shift>5.1f) continue;
                if (!actor.Navigation.TryJumpTrajectory(launch,site.Point,crosscutMotion,motor.Size,out var route,out var path)) continue;
                if (!PathInBounds(path)) continue;
                float touchdown=(path.Length-1)*Time.fixedDeltaTime;
                float release=wind+.14f;
                float threat=CrosscutContact(plan.Facing,path,facts,release);
                if (float.IsPositiveInfinity(threat))
                {
                    // A gap-closing leap can be useful, but its landing alone is not a hit.
                    if ((site.Point-plan.Target).sqrMagnitude+1f >= (plan.Start-plan.Target).sqrMagnitude) continue;
                    float remaining=Followup(site.Point,PublicPosition(facts,release+touchdown));
                    threat=touchdown+game.GetBossMoveTuning(false,(int)Move.LeapingCrosscut).Recovery+remaining;
                }
                if (threat>=best) continue;
                found=true; best=threat;
                plan.FirstThreat=release+threat;
                plan.Landing=site.Point; plan.JumpMotion=crosscutMotion; plan.JumpRoute=route; plan.JumpPath=path;
            }
            return found;
        }

        float CrosscutContact(float facing, Vector2[] path, BossObservation facts, float release)
        {
            float dt=Time.fixedDeltaTime;
            for (float age=Crosscut.Start; age<=Crosscut.Start+Crosscut.Duration; age+=dt)
            {
                int k=Mathf.Clamp(Mathf.RoundToInt(age/dt),0,path.Length-1);
                Vector2 hand=path[k]+Offset(facing,.45f,.20f);
                float angle=Mathf.Lerp(Crosscut.From,Crosscut.To,(age-Crosscut.Start)/Crosscut.Duration);
                Vector2 dir=Shaft(facing,angle);
                var body=new Bounds(PublicPosition(facts,release+age),facts.Body.size+Vector3.one*.32f);
                if (body.Contains(hand) || body.IntersectRay(new Ray(hand,dir),out float dist) && dist<=BladeReach &&
                    !Projectile.TerrainHit(hand,hand+dir*dist,.16f,out _)) return age;
            }
            return float.PositiveInfinity;
        }

        bool PlanVault(AxePlan plan)
        {
            var motor=actor.Motor;
            RankLandings(plan,plan.Target+Vector2.right*plan.Facing*1.50f,true);
            foreach (var site in attackSites)
            {
                // Catch beyond the observed opponent at a point with a real, clear descent.
                // Finish descending into the target-facing side of the landing instead of
                // choosing a point from which an ordinary attack needs another full tier.
                var land=site.Point;
                if (!actor.Navigation.CanStand(land,motor.Size)) continue;
                foreach (float rise in vaultRises)
                {
                    var anchor=land + Vector2.up*rise;
                    if (!WithinArena(anchor,motor.Size) || !actor.Navigation.TryDash(plan.Start,anchor,motor.Size,out _)
                        || PlannedBodyContact(plan.Start,anchor,plan.Target) || !ClearDescent(anchor,land)) continue;
                    plan.Anchor=anchor; plan.Landing=land;
                    if (FlightCorridorClear(plan)) return true;
                }
            }
            return false;
        }
        static readonly float[] vaultRises = {1.65f, 2.25f, 2.85f, 3.35f, 1.1f};

        bool PlannedBodyContact(Vector2 from,Vector2 to,Vector2 publicTarget)
        {
            // The motor will stop on physical body contact. Check the *observed*
            // silhouette here so a straight pull does not routinely jam on a player
            // standing between the launcher and the far-side catch.
            var extents=(Vector2)actor.Perception.Facts.Body.size+actor.Motor.Size;
            var area=new Bounds(publicTarget,new Vector3(extents.x,extents.y,.5f));
            var delta=to-from;
            return area.IntersectRay(new Ray(from,delta.normalized),out var distance) && distance<=delta.magnitude;
        }

        void RankLandings(AxePlan plan, Vector2 ideal, bool vault)
        {
            attackSites.Clear();
            var size=actor.Motor.Size;
            foreach (var node in actor.Navigation.Nodes)
            {
                var p=node.Center(size);
                float advance=(p.x-plan.Start.x)*plan.Facing;
                if (vault)
                {
                    float beyond=(p.x-plan.Target.x)*plan.Facing;
                    if (advance < 1.2f || advance > 9.3f || beyond < .80f || beyond > 2.85f
                        || Mathf.Abs(p.y-plan.Target.y)>1.45f) continue;
                }
                else if (advance<1.35f || advance>6.75f || (p.x-plan.Target.x)*plan.Facing>-.55f
                    || Mathf.Abs(p.y-plan.Start.y)>.65f) continue;
                attackSites.Add(new Site { Point=p,Cost=(p-ideal).sqrMagnitude });
            }
            attackSites.Sort(SiteOrder);
        }

        static int SiteOrder(Site a, Site b) => a.Cost.CompareTo(b.Cost);
        bool ClearDescent(Vector2 anchor, Vector2 landing)
        {
            var nav=actor.Navigation; var size=actor.Motor.Size;
            if (!nav.TrySupport(landing,size,out var support)) return false;
            return nav.SweepTraversal(anchor,landing,size,out var hit,out var safe)
                || hit.collider==support && hit.normal.y>.7f && Vector2.Distance(safe,landing)<.12f;
        }
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
            var retreat=plan.Start - Vector2.right*plan.Facing*1.4f;
            if (!SameSupportWalk(plan.Start,retreat)) return false;
            if (!actor.Navigation.TryJumpTrajectory(retreat,retreat,actor.Motor,out var route,out var path)
                || !PathInBounds(path)) return false;
            plan.Retreat=plan.Landing=retreat;
            plan.JumpRoute=route; plan.JumpPath=path; plan.JumpMotion=actor.Motor.Profile;
            plan.Turn=BrakePoint(plan,1.0f,.40f);
            return (plan.Turn.x-plan.Start.x)*plan.Facing>2.8f && FlightCorridorClear(plan);
        }
        bool PlanDeflection(AxePlan plan)
        {
            plan.Turn=BrakePoint(plan,1.60f,.38f);
            if ((plan.Turn.x-plan.Start.x)*plan.Facing<3.0f) return false;
            for (int step=2;step>=0;step--)
            {
                var catchPoint=plan.Start+Vector2.right*plan.Facing*(.85f+step*.50f);
                if(!SameSupportWalk(plan.Start,catchPoint))continue;
                plan.Landing=catchPoint;
                if (FlightCorridorClear(plan))return true;
            }
            return false;
        }
        Vector2 BrakePoint(AxePlan plan,float overrun,float height)
        {
            var b=actor.Navigation.Bounds;
            var p=plan.Target+new Vector2(plan.Facing*overrun,height);
            p.x=Mathf.Clamp(p.x,b.min.x+AxeRadius+.08f,b.max.x-AxeRadius-.08f);
            p.y=Mathf.Clamp(p.y,b.min.y+AxeRadius+.08f,b.max.y-AxeRadius-.08f);
            return p;
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
                case Move.AnchorVault:return 5.0f;
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
        void FollowRoute(ArenaNavigation2D.Route route,bool isSearch)
        {
            var motor=actor.Motor;
            var end=route.Steps[route.Steps.Count-1].End;
            if(movingToSite && motor.HasRoute && searching==isSearch && Vector2.Distance(routeGoal,end)<.65f)return;
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
