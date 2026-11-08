using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        const float VaultMinCast=5.5f, VaultMaxCast=11f, VaultReboundSpan=7f;
        const float VaultCastSpeed=18f, VaultPullSpeed=22f, VaultLatchTime=.16f, VaultBrace=.20f;
        const float VaultKick=11.6f;
        float gripAt=-1;

        static Cut VaultCutAt(float start) => new Cut(start,.26f,112,-78,1,34,2.9f);
        static float VaultCastTime(AxePlan plan) => Vector2.Distance(plan.Start,plan.Anchor)/VaultCastSpeed;
        static float VaultGripAngle(AxePlan plan)
        {
            Vector2 delta=plan.Anchor-plan.Start;
            return Mathf.Clamp(Mathf.Atan2(delta.y,Mathf.Abs(delta.x))*Mathf.Rad2Deg,8f,42f);
        }

        // First commitment: pick only a fixed grip with clearance for the axe and
        // the pull. No landing, outgoing slash direction or predicted hit is booked.
        bool PlanVault(AxePlan plan)
        {
            var motor=actor.Motor;
            float gap=Mathf.Abs(plan.Target.x-plan.Start.x);
            float ceiling=actor.Navigation.ArenaBounds.max.y-motor.Size.y*.5f-.30f;
            attackSites.Clear();
            for(int i=0;i<=5;i++)
            {
                float span=Mathf.Lerp(VaultMinCast,VaultMaxCast,i/5f);
                for(int level=0;level<3;level++)
                {
                    float rise=1.15f+level*.85f;
                    float y=Mathf.Max(plan.Start.y+.25f,Mathf.Lerp(plan.Start.y,plan.Target.y,.55f)+rise);
                    var grip=new Vector2(plan.Start.x+plan.Facing*span,Mathf.Min(y,ceiling));
                    if(!WithinArena(grip,motor.Size))continue;
                    float cost=Mathf.Abs(Mathf.Abs(span-gap)-4.8f)+level*.12f;
                    if(span>gap)
                    {
                        float crossHeight=Mathf.Lerp(plan.Start.y,grip.y,gap/span)-plan.Target.y;
                        cost+=Mathf.Max(0,motor.Size.y+.12f-crossHeight)*2f;
                    }
                    attackSites.Add(new Site { Point=grip, Cost=cost });
                }
            }
            attackSites.Sort(SiteOrder);
            foreach(var site in attackSites)
            {
                plan.Anchor=site.Point;
                if(actor.Navigation.TryDash(plan.Start,plan.Anchor,motor.Size,out _) && FlightCorridorClear(plan))
                    return true;
            }
            return false;
        }

        // Second commitment: read the opponent once at actual grip contact. The
        // launch may cover less ground when the opponent is close or a ledge intervenes.
        bool PlanGripLeap(AxePlan plan)
        {
            var facts=actor.Perception.Facts;
            if(!facts.Visible || facts.Confidence<=.12f || facts.Body.size.x<=0)return false;
            Vector2 from=actor.Position;
            Vector2 seen=PublicPosition(facts,0);
            Vector2 aim=Vector2.MoveTowards(seen,PublicPosition(facts,.16f),.85f);
            float dx=seen.x-from.x;
            float side=Mathf.Abs(dx)>.10f ? Mathf.Sign(dx) : (Mathf.Abs(aim.x-from.x)>.05f?Mathf.Sign(aim.x-from.x):plan.Facing);
            float gap=Mathf.Max(0,(aim.x-from.x)*side);
            float desired=Mathf.Clamp(gap-.6f,gap>=4f?4f:.25f,VaultReboundSpan);
            plan.Target=aim;
            plan.FinishFacing=side;
            FaceFinisher();

            attackSites.Clear();
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 land=node.Center(actor.Motor.Size);
                float travel=(land.x-from.x)*side;
                if(travel<0 || travel>VaultReboundSpan || travel>Mathf.Max(1.5f,gap+.75f))continue;
                if(travel<.5f && gap>2f)continue;
                float cost=Mathf.Abs(travel-desired)+1.15f*Mathf.Abs(land.y-aim.y);
                if(gap>=4f && travel<4f)cost+=1.75f;
                attackSites.Add(new Site { Point=land, Cost=cost });
            }
            attackSites.Sort(SiteOrder);
            foreach(var site in attackSites)
            {
                if(TryRebound(plan,from,site.Point,aim,VaultKick) ||
                    TryRebound(plan,from,site.Point,aim,7.4f))return true;
            }
            return false;
        }

        bool TryRebound(AxePlan plan,Vector2 from,Vector2 landing,Vector2 aim,float impulse)
        {
            float gravity=-Physics2D.gravity.y*actor.Motor.Profile.GravityScale;
            float room=actor.Navigation.ArenaBounds.max.y-actor.Motor.Size.y*.5f-from.y-.05f;
            if(gravity<=0 || room<=0)return false;
            float kick=Mathf.Min(impulse,Mathf.Sqrt(2f*gravity*room));
            float discriminant=kick*kick+2f*gravity*(from.y-landing.y);
            if(discriminant<0)return false;
            float airtime=(kick+Mathf.Sqrt(discriminant))/gravity;
            var motion=CreateLeapMotion(kick);
            motion.Speed=Mathf.Clamp(Mathf.Abs(landing.x-from.x)/Mathf.Max(.10f,airtime-.10f),1.4f,14.5f);
            if(!actor.Navigation.TryJumpTrajectory(from,landing,motion,actor.Motor.Size,out var route,out var path) ||
                !PathInBounds(path))return false;

            int apex=0;
            while(apex+1<path.Length && path[apex+1].y>path[apex].y)apex++;
            float latest=Mathf.Max(.04f,(path.Length-1)*Time.fixedDeltaTime-.26f);
            float earliest=Mathf.Min(latest,Mathf.Max(.04f,apex*Time.fixedDeltaTime-.04f));
            float cut=ClosestCutStart(path,aim,plan.FinishFacing,earliest,latest,.18f);
            plan.Landing=landing;
            plan.JumpMotion=motion;
            plan.JumpRoute=route;
            plan.JumpPath=path;
            plan.Swing=VaultCutAt(cut);
            return true;
        }

        float PullAt => flight[0].End+VaultLatchTime;
        public bool AnchorLatched => Current==Move.AnchorVault && Phase==Stage.Active &&
            AxeFlying && retrieveAt<0 && flight.Count>0 && ActiveTime>=flight[0].End;
        public bool AnchorPulling => AnchorLatched && pulled;
        public float AnchorLatchProgress => AnchorLatched ?
            Mathf.Clamp01((ActiveTime-flight[0].End)/VaultLatchTime) : 0;
        public bool BracedOnAxe => Current==Move.AnchorVault && Phase==Stage.Active &&
            gripAt>=0 && reboundAt<0 && !abortAfterCatch;
        float GripRaise(float age) => Mathf.Clamp01((age-gripAt)/VaultBrace);

        void DriveAnchor(float age)
        {
            var motor=actor.Motor;
            if(abortAfterCatch || retrieveAt>=0 || reboundAt>=0)return;
            if(gripAt>=0)
            {
                if(age<gripAt+VaultBrace)return;
                // Recheck only physical clearance. The observed target, facing and
                // destination from the catch remain fixed throughout takeoff and flight.
                if(!motor.ReboundFromGrip(acceptedPlan.Anchor,acceptedPlan.JumpRoute,acceptedPlan.JumpMotion))
                {EndVaultEarly();return;}
                reboundAt=age;
                anchorCutAt=age+acceptedPlan.Swing.Start;
                float touchdown=(acceptedPlan.JumpPath.Length-1)*Time.fixedDeltaTime;
                activeEnds=phaseUntil=activeAt+age+Mathf.Max(touchdown,acceptedPlan.Swing.Start+acceptedPlan.Swing.Duration);
                actor.Sounds?.Play(Sfx.BladeHeavy,actor.ActorId,attackId);
                return;
            }

            if(!AxeFlying || age<PullAt)return;
            if(!pulled)
            {
                pulled=true;
                Vector2 delta=acceptedPlan.Anchor-actor.Position;
                if(!actor.Navigation.TryDash(actor.Position,acceptedPlan.Anchor,motor.Size,out _))
                {EndVaultEarly();return;}
                motor.StartDash(delta,delta.magnitude,VaultPullSpeed);
            }
            if(motor.CatchAxeGrip(acceptedPlan.Anchor))
            {
                gripAt=age;
                anchorLatchOffset=flight[0].D-Shaft(acceptedPlan.Facing,VaultGripAngle(acceptedPlan))*PivotOffset-actor.Position;
                AxeFlying=false;
                caught=true;
                if(!PlanGripLeap(acceptedPlan)){EndVaultEarly();return;}
                activeEnds=phaseUntil=activeAt+gripAt+VaultBrace+(acceptedPlan.JumpPath.Length-1)*Time.fixedDeltaTime;
                actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,attackId);
                return;
            }
            float pullTime=Vector2.Distance(acceptedPlan.Start,acceptedPlan.Anchor)/VaultPullSpeed;
            if(motor.DashBlocked || age>PullAt+pullTime+.22f)EndVaultEarly();
        }

        void EndVaultEarly()
        {
            actor.Motor.EndAttackMotion();
            abortAfterCatch=true;
            if(AxeFlying)BeginRetrieval();
            else activeEnds=phaseUntil=Time.time;
        }
    }
}
