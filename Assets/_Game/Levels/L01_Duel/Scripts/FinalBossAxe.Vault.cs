using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        const float VaultMinCast = 5.5f, VaultMaxCast = 11f;
        const float VaultCastSpeed = 18f, VaultPullSpeed = 22f, VaultLatchTime = .16f, VaultKick = 5.8f;
        static readonly float[] VaultSpans = { 3.5f, 2.6f, 4.4f };
        static readonly float[] VaultHeights = { 2.1f, 3.1f };
        static Cut VaultCutAt(float start) => new Cut(start,.26f,112,-78,1,34,2.9f);
        static float VaultCastTime(AxePlan plan) => Vector2.Distance(plan.Start,plan.Anchor)/VaultCastSpeed;
        static float VaultGripAngle(AxePlan plan)
        {
            Vector2 delta=plan.Anchor-plan.Start;
            return Mathf.Clamp(Mathf.Atan2(delta.y,Mathf.Abs(delta.x))*Mathf.Rad2Deg,8f,42f);
        }
        static float VaultPullTime(AxePlan plan) => Vector2.Distance(plan.Start,plan.Anchor)/VaultPullSpeed;

        struct VaultSite
        {
            public Vector2 Anchor, Landing;
            public float Facing, Cost;
        }
        readonly List<VaultSite> vaultSites=new List<VaultSite>(64);

        // Start, aerial grip and supported landing are three different points. The grip
        // can be short of the player (continue forward) or beyond them (cut back).
        bool PlanVault(ref AxePlan plan,BossObservation facts,float wind)
        {
            Vector2 target=PublicPosition(facts,plan.TravelTime+1.2f);
            vaultSites.Clear();
            foreach(var node in actor.Navigation.Nodes)
            {
                Vector2 land=node.Center(actor.Motor.Size);
                if(Mathf.Abs(land.y-target.y)>2.4f || Mathf.Abs(land.x-target.x)>3.1f)continue;
                for(int side=-1;side<=1;side+=2)
                {
                    float gap=(target.x-land.x)*side;
                    if(gap<-.35f||gap>3.1f)continue;
                    foreach(float span in VaultSpans)
                    foreach(float height in VaultHeights)
                    {
                        var grip=new Vector2(land.x-side*span,Mathf.Max(plan.Start.y+.45f,land.y+height));
                        float cast=(grip.x-plan.Start.x)*plan.Facing;
                        if(cast<VaultMinCast||cast>VaultMaxCast||!WithinArena(grip,actor.Motor.Size))continue;
                        float cost=Mathf.Abs(gap-1.05f)+Mathf.Abs(land.y-target.y)*.8f
                            +Mathf.Abs(span-3.5f)*.12f+(height-2.1f)*.09f;
                        // Reward a real far-side grip with a chop into the runner's
                        // lane. At a wall retain the inside landing after the rebound.
                        float lead=(land.x-target.x)*plan.Facing;
                        if(facts.Velocity.x*plan.Facing>2f && lead>.35f && side==-plan.Facing)cost-=.55f;
                        var bounds=actor.Navigation.ArenaBounds;
                        float room=plan.Facing>0?bounds.max.x-target.x:target.x-bounds.min.x;
                        if(room<2.3f && lead>0)cost+=1.5f;
                        vaultSites.Add(new VaultSite{Anchor=grip,Landing=land,Facing=side,Cost=cost});
                    }
                }
            }
            vaultSites.Sort((a,b)=>a.Cost.CompareTo(b.Cost));
            AxePlan fallback=null;
            int simulated=0;
            float release=wind+Timings[(int)Move.AnchorVault].Lock;
            foreach(var site in vaultSites)
            {
                var trial=new AxePlan{Move=Move.AnchorVault,Facing=plan.Facing,Start=plan.Start,
                    Target=target,Anchor=site.Anchor,Landing=site.Landing,FinishFacing=site.Facing,TravelTime=plan.TravelTime};
                if(!VaultPullClear(trial,facts,release) || !actor.Navigation.CanStand(site.Landing,actor.Motor.Size))continue;
                if(++simulated>18)break;
                if(!PlanRebound(trial) || !FlightCorridorClear(trial))continue;
                float untilGrip=VaultCastTime(trial)+VaultLatchTime+VaultPullTime(trial);
                float chop=PathCutContact(site.Facing,trial.JumpPath,facts,trial.TravelTime+release+untilGrip,trial.Swing);
                trial.DirectCut=!float.IsPositiveInfinity(chop);
                float contact=FlightOpportunity(trial,facts,release,untilGrip+chop);
                if(trial.DirectCut)
                {
                    trial.FirstThreat=release+contact;
                    plan=trial;
                    return true;
                }
                // Prefer a connected chop. A throw-only hit or relocation still has
                // to leave the Boss close enough to attack from his landing.
                if(Followup(site.Landing,target)>.23f ||
                    Vector2.Distance(trial.Start,target)-Vector2.Distance(site.Landing,target)<3.5f)continue;
                trial.FirstThreat=float.IsPositiveInfinity(contact)
                    ? release+untilGrip+trial.Swing.Start+trial.Swing.Duration+game.GetBossMoveTuning(false,(int)Move.AnchorVault).Recovery
                    : release+contact;
                if(fallback==null||trial.FirstThreat<fallback.FirstThreat)fallback=trial;
            }
            if(fallback==null)return false;
            plan=fallback;
            return true;
        }

        bool VaultPullClear(AxePlan plan,BossObservation facts,float release)
        {
            if(!actor.Navigation.TryDash(plan.Start,plan.Anchor,actor.Motor.Size,out _))return false;
            float pullStart=plan.TravelTime+release+VaultCastTime(plan)+VaultLatchTime;
            float seconds=VaultPullTime(plan);
            Vector2 half=(actor.Motor.Size+(Vector2)facts.Body.size)*.5f;
            int steps=Mathf.CeilToInt(Vector2.Distance(plan.Start,plan.Anchor)/.40f);
            for(int i=1;i<=steps;i++)
            {
                float u=i/(float)steps;
                Vector2 delta=Vector2.Lerp(plan.Start,plan.Anchor,u)-PublicPosition(facts,pullStart+u*seconds);
                if(Mathf.Abs(delta.x)<half.x && Mathf.Abs(delta.y)<half.y)return false;
            }
            return true;
        }

        bool PlanRebound(AxePlan plan)
        {
            float gravity=-Physics2D.gravity.y*actor.Motor.Profile.GravityScale;
            if(gravity<=0)return false;
            float height=plan.Anchor.y-plan.Landing.y;
            float airtime=(VaultKick+Mathf.Sqrt(VaultKick*VaultKick+2*gravity*height))/gravity;
            // Set horizontal speed from both spans so the body is still traveling
            // across the target on the downstroke, even on the shorter bank.
            float speed=Mathf.Abs(plan.Anchor.x-plan.Landing.x)/(airtime-.10f);
            var motion=CreateLeapMotion(VaultKick);
            motion.Speed=speed;
            if(!actor.Navigation.TryJumpTrajectory(plan.Anchor,plan.Landing,motion,actor.Motor.Size,
                out var route,out var path)||!PathInBounds(path))return false;
            plan.JumpMotion=motion;
            plan.JumpPath=path;
            plan.JumpRoute=route;
            float flightTime=(path.Length-1)*Time.fixedDeltaTime;
            plan.Swing=VaultCutAt(Mathf.Max(.16f,flightTime-.33f));
            return true;
        }

        float PullAt => flight[0].End+VaultLatchTime;
        public bool AnchorLatched => Current==Move.AnchorVault && Phase==Stage.Active &&
            AxeFlying && retrieveAt<0 && flight.Count>0 && ActiveTime>=flight[0].End;
        public bool AnchorPulling => AnchorLatched && pulled;
        public float AnchorLatchProgress => AnchorLatched ?
            Mathf.Clamp01((ActiveTime-flight[0].End)/VaultLatchTime) : 0;

        void DriveAnchor(float age)
        {
            var motor=actor.Motor;
            if(!AxeFlying||retrieveAt>=0||age<PullAt)return;
            if(!pulled)
            {
                pulled=true;
                Vector2 delta=acceptedPlan.Anchor-actor.Position;
                if(!actor.Navigation.TryDash(actor.Position,acceptedPlan.Anchor,motor.Size,out _))
                {
                    abortAfterCatch=true;
                    BeginRetrieval();
                    return;
                }
                motor.StartDash(delta,delta.magnitude,VaultPullSpeed);
            }
            if(Vector2.Distance(actor.Position,acceptedPlan.Anchor)<=.15f)
            {
                motor.EndAttackMotion();
                anchorLatchOffset=flight[0].D-Shaft(acceptedPlan.Facing,VaultGripAngle(acceptedPlan))*PivotOffset-actor.Position;
                if(!motor.ReboundFromGrip(acceptedPlan.Anchor,acceptedPlan.JumpRoute,acceptedPlan.JumpMotion))
                {
                    abortAfterCatch=true;
                    BeginRetrieval();
                    return;
                }
                AxeFlying=false;
                caught=true;
                FaceFinisher();
                reboundAt=age;
                anchorCutAt=age+acceptedPlan.Swing.Start;
                float touchdown=(acceptedPlan.JumpPath.Length-1)*Time.fixedDeltaTime;
                activeEnds=phaseUntil=activeAt+age+Mathf.Max(touchdown,acceptedPlan.Swing.Start+acceptedPlan.Swing.Duration);
                actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,attackId);
                return;
            }
            if(motor.DashBlocked||age>PullAt+VaultPullTime(acceptedPlan)+.22f)
            {
                motor.EndAttackMotion();abortAfterCatch=true;BeginRetrieval();
            }
        }
    }
}
