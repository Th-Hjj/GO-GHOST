using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        const float PivotOffset=.736f, FlightEdgeRadius=.20f, SpinningClearance=.84f;
        const float UnderTurn=.59f, UnderReturn=UnderTurn+.18f, UnderHopAt=.82f, UnderPassAt=1.42f, UnderCatch=1.96f;
        const float PalmAt=1.24f, PalmRelease=PalmAt+.22f, PalmBank=PalmRelease+.55f, PalmCatch=PalmBank+.57f;
        readonly List<FlightLeg> flight=new List<FlightLeg>(6), candidateFlight=new List<FlightLeg>(6);
        bool redirected;
        public float PalmAccent=>Current==Move.PalmDeflection&&Phase==Stage.Active&&redirected&&!abortAfterCatch ?
            1-Mathf.Clamp01((ActiveTime-PalmAt)/(PalmRelease-PalmAt)):0;
        public bool FlightWarning=>path.Count>1;
        public float FlightFacing=>acceptedPlan.Facing;
        public Vector2 FlyingEdge => AxePosition+Shaft(1,AxeSpin-FlightFacing*EdgeOffset)*PhysicalEdgeReach-Shaft(1,AxeSpin)*PivotOffset;
        public bool FlightLive
        {
            get
            {
                if(!AxeFlying || retrieveAt>=0 || abortAfterCatch)return false;
                foreach(var leg in flight)
                    if(leg.Group>=0 && ActiveTime>=leg.Start && ActiveTime<leg.End)return true;
                return false;
            }
        }

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
        Vector2 LimitFlightPoint(Vector2 point)=>ArenaBoundary2D.ClampPoint(actor.Navigation.ArenaBounds,point,SpinningClearance);

        void BuildFlight(AxePlan p,List<FlightLeg> legs)
        {
            legs.Clear();
            float f=p.Facing;
            if(p.Move==Move.UnderpassReturn)
            {
                var start=Pivot(p.Start,f,new Vector2(.39f,.24f),18);
                var turn=p.Turn;
                var pass=p.Retreat+Offset(f,.04f,-actor.Motor.Size.y*.5f+SpinningClearance);
                var caught=Pivot(p.Retreat,f,new Vector2(-.37f,.32f),145);
                float dx=Mathf.Abs(turn.x-start.x);
                legs.Add(new FlightLeg(start,LimitFlightPoint(start+Offset(f,dx*.32f,1.85f)),
                    LimitFlightPoint(turn+Offset(f,-dx*.20f,1.9f)),turn,0,UnderTurn,0,16,18,-340));
                legs.Add(new FlightLeg(turn,LimitFlightPoint(new Vector2(turn.x+f*.26f,pass.y+.04f)),
                    LimitFlightPoint(new Vector2(Mathf.Lerp(turn.x,pass.x,.55f),pass.y)),pass,UnderReturn,UnderPassAt,1,22,-340,-540));
                legs.Add(new FlightLeg(pass,LimitFlightPoint(pass+Offset(f,-1.65f,.58f)),
                    LimitFlightPoint(caught+Offset(f,-1.3f,.72f)),caught,UnderPassAt,UnderCatch,-1,0,-540,145-1080));
            }
            else if(p.Move==Move.AnchorVault)
            {
                float angle=VaultGripAngle(p);
                var start=Pivot(p.Start,f,new Vector2(.39f,.31f),angle);
                var anchored=Pivot(p.Anchor,f,new Vector2(.38f,.34f),angle);
                legs.Add(new FlightLeg(start,LimitFlightPoint(Vector2.Lerp(start,anchored,.36f)+Vector2.down*.44f),
                    LimitFlightPoint(Vector2.Lerp(start,anchored,.78f)+Vector2.down*.60f),anchored,0,VaultCastTime(p),0,16,angle,angle-360));
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
            float side=p.FinishFacing;
            var caught=Pivot(p.Landing,side,new Vector2(.18f,.43f),127);
            var bank=LimitFlightPoint(new Vector2(target.x+side*1.25f,target.y+.45f));
            float dx=Mathf.Abs(bank.x-palm.x);
            float catchAngle=side==p.Facing?127:180-127;
            legs.Add(new FlightLeg(palm,LimitFlightPoint(palm+Offset(side,dx*.28f,1.90f)),
                LimitFlightPoint(bank+Offset(side,-dx*.14f,1.85f)),bank,PalmRelease,PalmBank,1,15,38,-324));
            legs.Add(new FlightLeg(bank,LimitFlightPoint(bank+Offset(side,.30f,-.93f)),
                LimitFlightPoint(Vector2.Lerp(bank,caught,.66f)+Vector2.down*.85f),caught,PalmBank,PalmCatch,1,15,-324,catchAngle-720));
        }
        bool FlightCorridorClear(AxePlan plan)
        {
            BuildFlight(plan,candidateFlight);
            return FlightIsClear(candidateFlight,0,plan.Facing);
        }
        bool FlightIsClear(List<FlightLeg> legs,int first,float facing)
        {
            var arena=actor.Navigation.ArenaBounds;
            for(int i=first;i<legs.Count;i++)
            {
                var leg=legs[i];var from=leg.A;
                Vector2 lastEdge=leg.Edge(0,facing);
                for(int j=0;j<=leg.Samples;j++)
                {
                    var to=leg.Sample(j);
                    if(to.x<arena.min.x+AxeRadius-.01f||to.x>arena.max.x-AxeRadius+.01f||
                       to.y<arena.min.y+AxeRadius-.01f||to.y>arena.max.y-AxeRadius+.01f||
                       ((to-from).sqrMagnitude>.000001f && Projectile.TerrainHit(from,to,AxeRadius,out _,true)))return false;
                    if(leg.Group>=0)
                    {
                        Vector2 edge=leg.Edge(j,facing);
                        if(edge.x<arena.min.x+FlightEdgeRadius || edge.x>arena.max.x-FlightEdgeRadius ||
                           edge.y<arena.min.y+FlightEdgeRadius || edge.y>arena.max.y-FlightEdgeRadius ||
                           j>0 && Projectile.TerrainHit(lastEdge,edge,FlightEdgeRadius,out _,true))return false;
                        lastEdge=edge;
                    }
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
            if(!TryRedirectPalm(palm)&&!FlightIsClear(flight,2,acceptedPlan.Facing))
            {abortAfterCatch=true;BeginRetrieval();return;}
            actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,attackId);
        }
        bool TryRedirectPalm(Vector2 palm)
        {
            var seen=actor.Perception.Facts;
            if(seen.Confidence<=.12f||seen.Body.size.x<=.01f)return false;

            Vector2 target=PublicPosition(seen,.62f);
            float delta=target.x-palm.x;
            float side=Mathf.Abs(delta)>.55f?Mathf.Sign(delta):acceptedPlan.Facing;
            target.x=palm.x+Mathf.Clamp(Mathf.Abs(delta),.5f,8.5f)*side;
            target.y=Mathf.Clamp(target.y,palm.y-4.8f,palm.y+4.8f);
            Vector2 start=actor.Position;
            float advance=Mathf.Clamp((target.x-start.x)*side-1.4f,0f,3.25f);
            // Try the useful forward catch first. If terrain blocks the stride,
            // a shorter catch can still redirect the blade into the same lane.
            for(int step=2;step>=0;step--)
            {
                Vector2 landing=start+Vector2.right*(side*advance*step*.5f);
                if(!SameSupportWalk(start,landing))continue;
                var redirect=new AxePlan{Facing=acceptedPlan.Facing,FinishFacing=side,Landing=landing};
                candidateFlight.Clear();
                AddPalmOuter(redirect,candidateFlight,palm,target);
                if(!FlightIsClear(candidateFlight,0,acceptedPlan.Facing))continue;
                acceptedPlan.Landing=landing;
                acceptedPlan.FinishFacing=side;
                flight.RemoveRange(2,flight.Count-2);
                flight.AddRange(candidateFlight);
                return true;
            }
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
            if(flight.Count==0||Phase==Stage.Prepare||Phase==Stage.Recovery||retrieveAt>=0||abortAfterCatch)return;
            float age=Time.time-activeAt;
            foreach(var leg in flight)
            {
                bool firstLock=Phase==Stage.Locked && leg.Start==0;
                if(leg.Group<0 || age>=leg.Start || !firstLock && age<leg.Start-.24f)continue;
                if(Current==Move.PalmDeflection && (leg.Start>=PalmAt&&!redirected || leg.Start==PalmBank))continue;
                // Each separately committed throw/return has one short directional tell.
                // The continuous outer bank has no extra lock at its apex.
                path.Add(leg.A);
                float remaining=2.4f;
                for(int j=1;j<=leg.Samples;j++)
                {
                    var previous=path[path.Count-1];
                    var point=leg.Sample(j);
                    float length=Vector2.Distance(previous,point);
                    if(length>=remaining){path.Add(Vector2.MoveTowards(previous,point,remaining));break;}
                    path.Add(point);remaining-=length;
                }
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
