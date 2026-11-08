using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        const float PivotOffset=.736f, FlightEdgeRadius=.20f, PullAt=.81f, PalmAt=1.24f, PalmCatch=2.43f, UnderCatch=2.02f;
        readonly List<FlightLeg> flight=new List<FlightLeg>(6), candidateFlight=new List<FlightLeg>(6);
        bool redirected;
        public bool AnchorPulling=>Current==Move.AnchorVault&&Phase==Stage.Active&&AxeFlying&&ActiveTime>=PullAt&&retrieveAt<0;
        public float PalmAccent=>Current==Move.PalmDeflection&&Phase==Stage.Active&&redirected ? 1-Mathf.Clamp01((ActiveTime-PalmAt)/.16f):0;
        public bool FlightWarning=>path.Count>1;

        readonly struct FlightLeg
        {
            public readonly Vector2 A,B,C,D;
            public readonly float Start,End,AngleA,AngleD;
            public readonly int Group,Damage;
            public int Samples=>Mathf.Max(Mathf.CeilToInt(Mathf.Abs(AngleD-AngleA)/15f),
                Mathf.Max(2,Mathf.CeilToInt((End-Start)/.045f)));
            public FlightLeg(Vector2 a,Vector2 b,Vector2 c,Vector2 d,float start,float end,int group,int damage,float angleA,float angleD)
            {A=a;B=b;C=c;D=d;Start=start;End=end;Group=group;Damage=damage;AngleA=angleA;AngleD=angleD;}
            public Vector2 Sample(int i)
            {
                float u=i/(float)Samples,k=1-u;
                return k*k*k*A+3*k*k*u*B+3*k*u*u*C+u*u*u*D;
            }
            public Vector2 At(float age)
            {
                float f=Mathf.InverseLerp(Start,End,age)*Samples;
                int j=Mathf.Min(Mathf.FloorToInt(f),Samples-1);
                return Vector2.Lerp(Sample(j),Sample(j+1),f-j);
            }
            public float Angle(float age)
            {
                float t=Mathf.InverseLerp(Start,End,age);
                return Mathf.Lerp(AngleA,AngleD,Group<0?Mathf.SmoothStep(0,1,t):t);
            }
            public Vector2 Edge(int sample,float side)
            {
                float age=Mathf.Lerp(Start,End,sample/(float)Samples);
                return Sample(sample)+Shaft(side,Angle(age)-EdgeOffset)*PhysicalEdgeReach-Shaft(side,Angle(age))*PivotOffset;
            }
        }
        static Vector2 Offset(float facing,float x,float y)=>new Vector2(facing*x,y);
        static Vector2 Shaft(float side,float angle)
        {
            float r=angle*Mathf.Deg2Rad;
            return new Vector2(side*Mathf.Cos(r),Mathf.Sin(r));
        }
        static float WorldAngle(float side,float local)=>side>0?local:180f-local;
        static Vector2 Pivot(Vector2 body,float side,Vector2 handLocal,float angle)=>
            body+Offset(side,handLocal.x,handLocal.y)+Shaft(side,angle)*PivotOffset;
        Vector2 LimitFlightPoint(Vector2 point)=>ArenaBoundary2D.ClampPoint(actor.Navigation.ArenaBounds,point,AxeRadius+.025f);

        void BuildFlight(AxePlan p,List<FlightLeg> legs)
        {
            legs.Clear();
            float f=p.Facing;
            if(p.Move==Move.UnderpassReturn)
            {
                var start=Pivot(p.Start,f,new Vector2(.39f,.24f),18);
                var turn=p.Turn;
                var pass=p.Retreat+Offset(f,.04f,-actor.Motor.Size.y*.5f+.60f);
                var caught=Pivot(p.Retreat,f,new Vector2(-.37f,.32f),145);
                float dx=Mathf.Abs(turn.x-start.x);
                legs.Add(new FlightLeg(start,LimitFlightPoint(start+Offset(f,dx*.32f,1.85f)),
                    LimitFlightPoint(turn+Offset(f,-dx*.20f,1.9f)),turn,0,.66f,0,16,18,-340));
                legs.Add(new FlightLeg(turn,LimitFlightPoint(turn+Offset(f,.48f,-.62f)),
                    LimitFlightPoint(Vector2.Lerp(turn,pass,.56f)+Vector2.down*.16f),pass,.66f,1.36f,1,22,-340,-540));
                legs.Add(new FlightLeg(pass,LimitFlightPoint(pass+Offset(f,-1.65f,.58f)),
                    LimitFlightPoint(caught+Offset(f,-1.3f,.72f)),caught,1.36f,UnderCatch,-1,0,-540,145-1080));
            }
            else if(p.Move==Move.AnchorVault)
            {
                var start=Pivot(p.Start,f,new Vector2(.39f,.31f),32);
                var anchored=Pivot(p.Anchor,f,new Vector2(.38f,.34f),32);
                legs.Add(new FlightLeg(start,LimitFlightPoint(Vector2.Lerp(start,anchored,.36f)+Vector2.down*.44f),
                    LimitFlightPoint(anchored+Offset(f,-.24f,-.85f)),anchored,0,.56f,0,16,32,-328));
            }
            else if(p.Move==Move.PalmDeflection)
            {
                var start=Pivot(p.Start,f,new Vector2(.39f,.31f),10);
                var turn=p.Turn;
                var palm=Pivot(p.Start,f,new Vector2(.46f,.27f),38);
                legs.Add(new FlightLeg(start,LimitFlightPoint(Vector2.Lerp(start,turn,.25f)+Vector2.up*.55f),
                    LimitFlightPoint(Vector2.Lerp(start,turn,.75f)+Vector2.up*.48f),turn,0,.52f,0,12,10,-348));
                legs.Add(new FlightLeg(turn,LimitFlightPoint(Vector2.Lerp(turn,palm,.27f)+Vector2.down*.35f),
                    LimitFlightPoint(Vector2.Lerp(turn,palm,.78f)+Vector2.down*.23f),palm,.74f,PalmAt,0,12,-348,38));
                AddPalmOuter(p,legs,palm,p.Target);
            }
        }
        void AddPalmOuter(AxePlan p,List<FlightLeg> legs,Vector2 palm,Vector2 target)
        {
            var caught=Pivot(p.Landing,p.Facing,new Vector2(.18f,.43f),127);
            var bank=LimitFlightPoint(new Vector2(target.x+p.Facing*1.25f,target.y+.45f));
            float dx=Mathf.Abs(bank.x-palm.x);
            legs.Add(new FlightLeg(palm,LimitFlightPoint(palm+Offset(p.Facing,dx*.10f,1.90f)),
                LimitFlightPoint(bank+Offset(p.Facing,-dx*.14f,1.85f)),bank,PalmAt,1.85f,1,15,38,-324));
            legs.Add(new FlightLeg(bank,LimitFlightPoint(bank+Offset(p.Facing,.30f,-.93f)),
                LimitFlightPoint(Vector2.Lerp(bank,caught,.66f)+Vector2.down*.85f),caught,1.85f,PalmCatch,1,15,-324,127-720));
        }
        bool FlightCorridorClear(AxePlan plan)
        {
            BuildFlight(plan,candidateFlight);
            return FlightIsClear(candidateFlight,0);
        }
        bool FlightIsClear(List<FlightLeg> legs,int first)
        {
            var arena=actor.Navigation.ArenaBounds;
            for(int i=first;i<legs.Count;i++)
            {
                var leg=legs[i];var from=leg.A;
                for(int j=0;j<=leg.Samples;j++)
                {
                    var to=leg.Sample(j);
                    if(to.x<arena.min.x+AxeRadius-.01f||to.x>arena.max.x-AxeRadius+.01f||
                       to.y<arena.min.y+AxeRadius-.01f||to.y>arena.max.y-AxeRadius+.01f||
                       ((to-from).sqrMagnitude>.000001f && Projectile.TerrainHit(from,to,AxeRadius,out _,true)))return false;
                    from=to;
                }
            }
            return true;
        }
        void RedirectPalm()
        {
            redirected=true;
            var palm=flight[1].D;
            var physicalPalm=actor.Position+OrientedGrip(PalmAt)+Shaft(acceptedPlan.Facing,38)*PivotOffset;
            if(Vector2.Distance(physicalPalm,palm)>.30f)
            {abortAfterCatch=true;BeginRetrieval();return;}
            // The palm strike is the one deliberate redirection after release. The
            // target may have crossed several units since the first throw. Use the
            // new public intercept and a reachable catch instead of inheriting its old aim.
            if(!TryRedirectPalm(palm)&&!FlightIsClear(flight,2))
            {abortAfterCatch=true;BeginRetrieval();return;}
            actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,attackId);
        }
        bool TryRedirectPalm(Vector2 palm)
        {
            var seen=actor.Perception.Facts;
            if(seen.Confidence<=.12f||seen.Body.size.x<=.01f)return false;

            Vector2 target=PublicPosition(seen,.48f);
            float side=acceptedPlan.Facing;
            float front=Mathf.Clamp((target.x-palm.x)*side,.5f,8.5f);
            target.x=palm.x+side*front;
            target.y=Mathf.Clamp(target.y,palm.y-4.8f,palm.y+4.8f);
            Vector2 start=actor.Position,initialLanding=acceptedPlan.Landing;
            float advance=Mathf.Clamp((target.x-start.x)*side-1.4f,0f,3.25f);
            // Try the useful forward catch first. If terrain blocks the stride,
            // a shorter catch can still redirect the blade into the same lane.
            for(int step=2;step>=0;step--)
            {
                Vector2 landing=start+Vector2.right*(side*advance*step*.5f);
                if(!SameSupportWalk(start,landing))continue;
                acceptedPlan.Landing=landing;
                candidateFlight.Clear();
                AddPalmOuter(acceptedPlan,candidateFlight,palm,target);
                if(!FlightIsClear(candidateFlight,0))continue;
                flight.RemoveRange(2,flight.Count-2);
                flight.AddRange(candidateFlight);
                return true;
            }
            acceptedPlan.Landing=initialLanding;
            return false;
        }
        void FlightPose(float age)
        {
            if(flight.Count==0)return;
            var pose=flight[flight.Count-1];
            foreach(var leg in flight)
                if(age<=leg.End){pose=leg;break;}
            // The pier/anchor holds between damaging stages.
            if(age<pose.Start)
                for(int i=1;i<flight.Count;i++)if(age>=flight[i-1].End&&age<flight[i].Start){pose=flight[i-1];break;}
            AxePosition=pose.At(age);AxeSpin=WorldAngle(acceptedPlan.Facing,pose.Angle(age));
        }
        void UpdateFlightWarning()
        {
            path.Clear();
            if(flight.Count==0||Phase==Stage.Recovery||retrieveAt>=0)return;
            float age=Time.time-activeAt;
            foreach(var leg in flight)
            {
                if(leg.Group<0||age>=leg.Start||age<leg.Start-.24f&&leg.Start>0)continue;
                if(Current==Move.PalmDeflection&&leg.Start>=PalmAt&&!redirected)continue;
                for(int j=0;j<=Mathf.Max(2,leg.Samples/2);j++)path.Add(leg.Sample(j));
                break;
            }
        }
        BossThreat FlightThreat(FlightLeg leg,int segment)
        {
            var a=leg.Edge(segment,acceptedPlan.Facing);var b=leg.Edge(segment+1,acceptedPlan.Facing);
            Vector2 delta=b-a;float interval=(leg.End-leg.Start)/leg.Samples;
            float starts=activeAt+leg.Start+segment*interval;
            return new BossThreat{ActorId=actor.ActorId,AttackId=attackId,Phase=ThreatPhase,Valid=true,
                Shape=BossThreatShape.Projectile,Origin=a,End=b,Direction=delta.normalized,Radius=FlightEdgeRadius,TerrainBlocked=true,ThroughOneWay=true,
                ProjectileSpeed=delta.magnitude/interval,StartsAt=starts,EndsAt=starts+interval};
        }
    }
}
