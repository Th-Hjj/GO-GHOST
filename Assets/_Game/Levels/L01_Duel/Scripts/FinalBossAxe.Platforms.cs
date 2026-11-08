using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        struct AttackPosition
        {
            public Move Attack;
            public Vector2 Point, TargetAtArrival;
            public Collider2D Support;
            public ArenaNavigation2D.Route Route;
            public float Travel, Cost, GroundSpeed;
            public bool Backstep;
        }

        struct PlatformSite
        {
            public Vector2 Point;
            public Collider2D Support;
            public Move Attack;
            public float Rank;
        }

        readonly List<PlatformSite> platformSites = new List<PlatformSite>(16);
        readonly Dictionary<Vector2,ArenaNavigation2D.Route> platformRoutes = new Dictionary<Vector2,ArenaNavigation2D.Route>();
        AttackPosition positionIntent;
        Collider2D lastAttackPlatform;
        int intentTargetDirection;
        float arriveAt, intentDeadline, nextPlatformChoice, nextBackstep, lastPlatformAt;

        // Reuse the actual attack planners at a possible launch position. A setup earns
        // no guaranteed attack: on arrival the Boss must still see a legal opening.
        bool ChooseFootwork(BossObservation facts,Vector2 target,AxePlan attack,float attackCost)
        {
            var motor=actor.Motor;
            if(!actor.Navigation.TrySupport(actor.Position,motor.Size,out var support))return false;
            float gap=Mathf.Abs(target.x-actor.Position.x);
            bool connected=TryGroundPressure(facts,target,out var pursuit);
            bool holdsExit=connected && gap<4.5f && HoldsWallSide(target,facts);
            var melee=attacksHere[(int)Move.TripleCut];
            if(melee!=null && (holdsExit || Time.time<pressUntil || melee.FirstThreat<.78f))
                return CommitTacticalAttack(melee,false);
            if(holdsExit)return GuardInside(facts,target,pursuit);
            if(ContinueAttackPosition(facts,target,support))return true;

            float chaseCost=pursuit.FirstThreat+.15f;
            if(Time.time<pressUntil)chaseCost-=.30f;
            bool chase=connected && pursuit.Direction!=0 && chaseCost<attackCost;
            var choice=new AttackPosition { Cost=chase?chaseCost:attackCost };
            ConsiderGroundSetups(facts,target,support,ref choice);
            if(Time.time>=nextPlatformChoice)
            {
                nextPlatformChoice=Time.time+.35f;
                ConsiderPlatforms(facts,target,support,ref choice);
            }
            if(choice.Attack!=Move.None)
            {
                positionIntent=choice;
                arriveAt=Time.time+choice.Travel;
                intentDeadline=arriveAt+(choice.Route==null?.35f:.65f);
                intentTargetDirection=TravelDirection(facts);
                if(choice.Route!=null)FollowRoute(choice.Route,false,true);
                else
                {
                    if(choice.Backstep)nextBackstep=Time.time+1.65f;
                    DriveGroundSetup(choice);
                }
                return true;
            }
            if(chase)
            {
                movingToSite=searching=false;
                motor.PursueOnGround(pursuit.Direction,pursuit.Speed);
                return true;
            }
            if(connected && attack==null && pursuit.Direction==0)
            {
                movingToSite=searching=false;
                motor.HoldHorizontal();
                return true;
            }
            return false;
        }

        bool HoldsWallSide(Vector2 target,BossObservation facts)
        {
            float side=Mathf.Sign(target.x-actor.Position.x);
            var end=target+Vector2.right*(side*2.5f);
            return !actor.Navigation.TryDash(target,end,facts.Body.size,out var safe)
                && Mathf.Abs(safe.x-target.x)<2.3f;
        }

        bool GuardInside(BossObservation facts,Vector2 target,GroundPressure pursuit)
        {
            ClearAttackPosition();
            float side=Mathf.Sign(target.x-actor.Position.x);
            AxePlan strike=null;
            float cost=pursuit.Direction==0 ? float.PositiveInfinity : pursuit.FirstThreat+.15f;
            foreach(var move in PhaseOneMoves)
            {
                var plan=attacksHere[(int)move];
                if(plan==null || (plan.Landing.x-target.x)*side>.1f ||
                    (plan.Landing.x-actor.Position.x)*side<-.4f)continue;
                float score=AttackCost(plan,facts);
                if(score>=cost)continue;
                strike=plan; cost=score;
            }
            if(strike!=null)return CommitTacticalAttack(strike,false);
            movingToSite=searching=false;
            actor.Motor.PursueOnGround(pursuit.Direction,pursuit.Speed);
            return true;
        }

        static int TravelDirection(BossObservation facts) =>
            Mathf.Abs(facts.Velocity.x)<1.5f ? 0 : (int)Mathf.Sign(facts.Velocity.x);

        bool ContinueAttackPosition(BossObservation facts,Vector2 target,Collider2D here)
        {
            var intent=positionIntent;
            if(intent.Attack==Move.None)return false;
            var motor=actor.Motor;
            int direction=TravelDirection(facts);
            bool reversed=intentTargetDirection!=0 && direction==-intentTargetDirection;
            float gap=target.x-actor.Position.x;
            bool lostLane=intent.Backstep && gap*facts.Velocity.x>Mathf.Abs(gap)*1.25f;
            var revised=PublicPosition(facts,Mathf.Max(0,arriveAt-Time.time));
            bool movedAway=Vector2.Distance(intent.TargetAtArrival,revised)>3.2f;
            bool close=here==intent.Support && Mathf.Abs(intent.Point.x-actor.Position.x)<.40f;
            if(intent.Support==null || !intent.Support.enabled || !intent.Support.gameObject.activeInHierarchy ||
                reversed || lostLane || movedAway || Time.time>intentDeadline)
            {
                ClearAttackPosition();
                return false;
            }
            var opening=attacksHere[(int)intent.Attack];
            if(close || (here==intent.Support && opening!=null && opening.FirstThreat<.80f))
            {
                if(intent.Route!=null && close)
                {
                    lastAttackPlatform=here;
                    lastPlatformAt=Time.time;
                }
                ClearAttackPosition();
                return opening!=null && CommitTacticalAttack(opening,false);
            }
            if(intent.Route!=null)
            {
                if(motor.HasRoute && !motor.RouteFailed && intent.Route.Version==actor.Navigation.Version &&
                    Mathf.Abs(intent.Point.x-target.x)<12f)return true;
            }
            else if(here==intent.Support && SameSupportWalk(actor.Position,intent.Point))
            {
                DriveGroundSetup(intent);
                return true;
            }
            ClearAttackPosition();
            return false;
        }

        void ClearAttackPosition()
        {
            if(positionIntent.Attack==Move.None)return;
            positionIntent=default;
            movingToSite=false;
            actor.Motor.Brake();
        }

        void DriveGroundSetup(AttackPosition intent)
        {
            movingToSite=searching=false;
            float dx=intent.Point.x-actor.Position.x;
            float speed=Mathf.Min(intent.GroundSpeed,Mathf.Abs(dx)/Mathf.Max(.08f,actor.Profile.ReactionSeconds));
            actor.Motor.PursueOnGround(Mathf.Sign(dx),speed);
        }

        void ConsiderGroundSetups(BossObservation facts,Vector2 target,Collider2D here,ref AttackPosition choice)
        {
            Vector2 start=actor.Position;
            float gap=Mathf.Abs(target.x-start.x);
            float height=Mathf.Abs(target.y-start.y);
            if(gap>11f || height>4.8f)return;
            float side=Mathf.Sign(target.x-start.x);
            float away=facts.Velocity.x*side;
            float speed=actor.Motor.Profile.Speed;
            // Move to where the first cut can meet the runner, then check the cut
            // at its own wind-up and swing time. This also works on a bare floor.
            float advance=(PublicPosition(facts,.45f).x-start.x)*side-1.65f;
            if(advance>.55f && height<2.7f)
            {
                float chaseSpeed=away>1.5f?Mathf.Max(speed,9f):speed;
                ConsiderGroundStep(facts,here,Move.TripleCut,side*Mathf.Min(advance,3.6f),
                    chaseSpeed,Time.time<pressUntil?.43f:.23f,ref choice);
            }
            // An existing perch can also open a diagonal cast with a short forward
            // step along its edge; a different platform is not always necessary.
            if(height>1.3f && attacksHere[(int)Move.PalmDeflection]==null)
            {
                float toAngle=(PublicPosition(facts,.45f).x-start.x)*side-5.2f;
                if(toAngle>.55f)ConsiderGroundStep(facts,here,Move.PalmDeflection,
                    side*Mathf.Min(2.6f,toAngle),speed,.38f,ref choice);
            }
            if(Time.time<pressUntil || Time.time<nextBackstep || away>1.25f || gap>5f)return;
            // Underpass supplies part of the separation itself. Palm needs a little
            // more room before release. A usable cast from here needs no setup step.
            float arrivalGap=(PublicPosition(facts,.30f).x-start.x)*side;
            float retreatSpeed=Mathf.Max(speed,7.8f);
            if(attacksHere[(int)Move.UnderpassReturn]==null)
                ConsiderGroundStep(facts,here,Move.UnderpassReturn,-side*Mathf.Clamp(4.15f-arrivalGap,0,2.6f),
                    retreatSpeed,.42f,ref choice);
            if(attacksHere[(int)Move.PalmDeflection]==null)
                ConsiderGroundStep(facts,here,Move.PalmDeflection,-side*Mathf.Clamp(5.4f-arrivalGap,0,2.6f),
                    retreatSpeed,.24f,ref choice);
        }

        void ConsiderGroundStep(BossObservation facts,Collider2D here,Move move,float distance,float speed,
            float benefit,ref AttackPosition choice)
        {
            if(Mathf.Abs(distance)<.55f)return;
            Vector2 start=actor.Position;
            Vector2 point=start+Vector2.right*SupportedStride(start,distance);
            float span=Mathf.Abs(point.x-start.x);
            float travel=span/speed+.08f;
            if(span<.55f || readyAt[(int)move]>Time.time+travel || !SameSupportWalk(start,point))return;
            float side=Mathf.Sign(PublicPosition(facts,0).x-start.x);
            // A step never plans to cross the player's solid body on the same tier.
            Vector2 atArrival=PublicPosition(facts,travel);
            if(Mathf.Abs(atArrival.y-point.y)<actor.Motor.Size.y && (atArrival.x-point.x)*side<.70f)return;
            ConsiderOpening(facts,new AttackPosition { Attack=move, Point=point, Support=here,
                GroundSpeed=speed, Travel=travel, Backstep=distance*side<0 },benefit,ref choice);
        }

        void ConsiderOpening(BossObservation facts,AttackPosition site,float benefit,ref AttackPosition choice)
        {
            float travel=site.Travel;
            if(readyAt[(int)site.Attack]>Time.time+travel)return;
            float wind=game.GetBossMoveTuning(false,(int)site.Attack).Windup;
            if(!PlanFrom(site.Attack,false,facts,site.Point,travel,wind,out var plan))return;
            if(site.Attack==Move.LeapingCrosscut && !plan.DirectCut)return;
            site.TargetAtArrival=PublicPosition(facts,travel);
            if(site.Attack==Move.UnderpassReturn)
            {
                // The outward step and the throw's own retreat share a spacing budget.
                float spacing=Mathf.Abs(site.TargetAtArrival.x-plan.Retreat.x);
                if(spacing<4f || spacing>6.15f)return;
            }
            site.Cost=travel+AttackCost(plan,facts)-benefit;
            if(site.Cost<choice.Cost)choice=site;
        }

        void ConsiderPlatforms(BossObservation facts,Vector2 target,Collider2D here,ref AttackPosition choice)
        {
            var nav=actor.Navigation;
            Vector2 start=actor.Position;
            Vector2 intercept=PublicPosition(facts,.85f);
            bool changeAngle=lastAttack==Move.PalmDeflection && Time.time<relocateUntil;
            int direction=TravelDirection(facts);
            platformSites.Clear();
            foreach(var node in nav.Nodes)
            {
                if(node.Collider==here)continue;
                Vector2 point=node.Center(actor.Motor.Size);
                float lane=Mathf.Abs(point.x-intercept.x);
                float height=point.y-intercept.y;
                if(lane>10f || Mathf.Abs(height)>4.8f || Mathf.Abs(point.x-start.x)>12f || RecentlyFailed(point))continue;
                float progress=(point.x-start.x)*direction;
                float rank=Vector2.Distance(start,point)*.15f+Mathf.Abs(height)*.20f;
                if((point.x-target.x)*direction>0 && progress>1f)rank-=.75f;
                if(node.Collider==lastAttackPlatform && Time.time-lastPlatformAt<3f)rank+=1.25f;
                if(Mathf.Abs(height)<1.45f)RankPlatform(node.Collider,point,Move.TripleCut,rank+Mathf.Abs(lane-1.65f)*.32f);
                if(lane<7.5f && (Mathf.Abs(height)>1.2f || !facts.Grounded))
                    RankPlatform(node.Collider,point,Move.LeapingCrosscut,rank+Mathf.Abs(lane-2.7f)*.34f);
                if(Mathf.Abs(height)>.7f || changeAngle)
                    RankPlatform(node.Collider,point,Move.PalmDeflection,rank+Mathf.Abs(lane-5.2f)*.33f);
            }
            platformSites.Sort((a,b)=>a.Rank.CompareTo(b.Rank));
            platformRoutes.Clear();
            int paths=0, forecasts=0;
            nav.BeginRouteBatch();
            try
            {
                foreach(var site in platformSites)
                {
                    if(!platformRoutes.TryGetValue(site.Point,out var route))
                    {
                        if(!RouteTo(site.Point,ref paths,out route))route=null;
                        platformRoutes[site.Point]=route;
                    }
                    if(route==null || route.Duration>2.8f || readyAt[(int)site.Attack]>Time.time+route.Duration)continue;
                    float travel=route.Duration;
                    Vector2 arrival=PublicPosition(facts,travel);
                    float ahead=(site.Point.x-arrival.x)*direction;
                    bool cutsOff=direction!=0 && (site.Point.x-target.x)*direction>1f &&
                        (site.Point.x-start.x)*direction>1f && ahead>-.8f && ahead<5f;
                    bool changesTier=Mathf.Abs(target.y-start.y)>1.3f && Mathf.Abs(arrival.y-site.Point.y)<1.3f;
                    float benefit=cutsOff ? 1.05f : changesTier ? .72f : .28f;
                    if(changeAngle && Mathf.Abs(site.Point.y-missedFrom.y)>.9f)benefit+=.65f;
                    ConsiderOpening(facts,new AttackPosition { Attack=site.Attack, Support=site.Support,
                        Point=site.Point, Route=route, Travel=travel },benefit,ref choice);
                    if(++forecasts>=4)break;
                }
            }
            finally {nav.EndRouteBatch();}
        }

        void RankPlatform(Collider2D support,Vector2 point,Move move,float rank)
        {
            if(readyAt[(int)move]>Time.time+2.8f)return;
            int index=-1;
            for(int i=0;i<platformSites.Count;i++)
                if(platformSites[i].Support==support && platformSites[i].Attack==move){index=i;break;}
            if(index>=0 && platformSites[index].Rank<=rank)return;
            if(!actor.Navigation.CanStand(point,actor.Motor.Size))return;
            var candidate=new PlatformSite { Support=support, Point=point, Attack=move, Rank=rank };
            if(index<0)platformSites.Add(candidate);
            else platformSites[index]=candidate;
        }
    }
}
