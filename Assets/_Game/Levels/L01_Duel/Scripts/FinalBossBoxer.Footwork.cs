using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossBoxer
    {
        struct Surface
        {
            public Collider2D Collider;
            public PlatformOscillator Motion;
            public Bounds Bounds;
            public float Top(float after) => Bounds.max.y + (Motion != null ? Motion.DisplacementAfter(after) : 0);
        }
        struct JumpPlan
        {
            public Collider2D Support;
            public Vector2 Landing;
            public BossMotionProfile Motion;
            public float Duration, StrikeAt, AimX;
        }
        Surface[] surfaces;
        float playerGravity;
        float lastSurfaceRead = -1, nextBurst;
        Vector2 lastSeen;
        bool hasSeen;
        readonly float[] desirability = new float[tuning.Length];

        void ReadPlatforms()
        {
            playerGravity=Physics2D.gravity.y*actor.Player.GetComponent<Rigidbody2D>().gravityScale;
            var active=game.ActivePlatforms;
            surfaces=new Surface[active.Length];
            for (int i=0;i<active.Length;i++) surfaces[i].Collider=active[i];
            SampleSurfaces();
        }
        void SampleSurfaces()
        {
            if (lastSurfaceRead==Time.time) return;
            lastSurfaceRead=Time.time;
            for (int i=0;i<surfaces.Length;i++)
            {
                ref var s=ref surfaces[i];
                s.Bounds=s.Collider.bounds;
                if (i>0 && s.Motion==null) s.Motion=s.Collider.GetComponent<PlatformOscillator>();
            }
        }
        Vector2 OpponentAt(BossObservation facts,float after)
        {
            SampleSurfaces();
            // Use the same delayed horizontal observations as phase one. Vertically,
            // continue a ride or a fall until the first actual moving deck will meet it.
            var p=actor.Axe.PublicPosition(facts,0);
            float half=facts.Body.extents.y;
            int rider=-1;
            if (facts.Grounded)
                for(int i=0;i<surfaces.Length;i++)
                {
                    var s=surfaces[i];
                    if (Mathf.Abs(facts.Body.min.y-s.Top(0))<.43f && facts.Position.x+facts.Body.extents.x>s.Bounds.min.x
                        && facts.Position.x-facts.Body.extents.x<s.Bounds.max.x) { rider=i; break; }
                }
            float vertical=facts.Velocity.y, elapsed=0;
            while (elapsed<after)
            {
                float dt=Mathf.Min(.04f,after-elapsed);
                var next=actor.Axe.PublicPosition(facts,elapsed+dt);
                float foot=p.y-half;
                if (rider>=0)
                {
                    var deck=surfaces[rider];
                    if (next.x+facts.Body.extents.x>deck.Bounds.min.x && next.x-facts.Body.extents.x<deck.Bounds.max.x)
                    {
                        next.y=deck.Top(elapsed+dt)+half;
                        p=next; elapsed+=dt; continue;
                    }
                    vertical=(deck.Top(elapsed+dt)-deck.Top(elapsed))/dt;
                    rider=-1;
                }
                vertical+=playerGravity*dt;
                if (p.y>=ceiling-half-.01f && vertical>0) vertical=0;
                next.y=Mathf.Min(ceiling-half,p.y+vertical*dt);
                float highest=float.NegativeInfinity;
                for(int i=0;i<surfaces.Length;i++)
                {
                    var s=surfaces[i]; float top=s.Top(elapsed+dt);
                    if (foot<s.Top(elapsed)-.035f || next.y-half>top || next.x+facts.Body.extents.x<=s.Bounds.min.x
                        || next.x-facts.Body.extents.x>=s.Bounds.max.x || top<=highest) continue;
                    highest=top; rider=i;
                }
                if (rider>=0) { next.y=highest+half; vertical=0; }
                p=next; elapsed+=dt;
            }
            return p;
        }

        void Decide(BossObservation facts)
        {
            Vector2 target=OpponentAt(facts,.20f);
            lastSeen=target; hasSeen=true;
            Vector2 delta=target-actor.Position;
            float gap=Mathf.Abs(delta.x);
            float closing=facts.Velocity.x*Mathf.Sign(delta.x);
            float opening=facts.ReloadVisible ? .38f : 0;
            if (TryOrder(facts,target)) return;
            if (actor.Motor.DashActive) return;
            if (Ready(Punch.Leap) && actor.Motor.Grounded && ShoulderLead>-.30f && ShoulderLead<1f)
            {
                if (ShoulderWindow && TryStart(Punch.Leap)) return;
                if (actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var ground))
                {
                    float x=Mathf.Clamp(target.x,ground.bounds.min.x+.4f,ground.bounds.max.x-.4f);
                    actor.Motor.PursueOnGround(Mathf.Sign(x-actor.Position.x),Mathf.Min(9f,Mathf.Abs(x-actor.Position.x)*5f));
                    return;
                }
            }

            // Scores describe the value of a commitment. Physical reach determines where
            // it ends; none of these scores requires the forecast to intersect a hitbox.
            desirability[(int)Punch.Jab]=1.7f-gap*.26f-Mathf.Abs(delta.y)*.38f+opening;
            desirability[(int)Punch.Uppercut]=1.12f-gap*.30f + (facts.Grounded ? 0 : .8f)
                + Mathf.Clamp(delta.y,-1.5f,1.5f)*.30f;
            desirability[(int)Punch.Backfist]=.64f-gap*.23f+(Time.time-crossedAt<.85f ? 1.95f : 0);
            desirability[(int)Punch.Leap]=.87f+Mathf.Min(gap,9)*.07f-Mathf.Max(0,gap-10)*.12f
                +Mathf.Min(2f,Mathf.Abs(delta.y))*.14f + Mathf.Max(0,closing)*.05f + (ShoulderWindow ? 2.0f : 0);
            desirability[(int)Punch.Meteor]=.37f+Mathf.Clamp(-delta.y,-2f,4f)*.34f
                + (actor.Motor.Grounded ? 0 : .95f) + opening - gap*.065f;
            float footwork=.73f+Mathf.Min(gap*.045f,.38f);
            if (Time.time<nextBurst && actor.Motor.HasRoute) footwork+=.13f;
            for (int i=1;i<desirability.Length;i++)
            {
                var move=(Punch)i;
                if (!CanStart(move)) desirability[i]=float.NegativeInfinity;
                if (lastPunch==move) desirability[i]-=.27f;
            }
            for (int i=1;i<desirability.Length;i++)
            {
                int best=1;
                for (int k=2;k<desirability.Length;k++) if (desirability[k]>desirability[best]) best=k;
                if (desirability[best]<footwork) break;
                if (TryStart((Punch)best)) return;
                desirability[best]=float.NegativeInfinity;
            }
            Pursue(target,facts);
        }

        void SeekLastSighting()
        {
            if (actor.Motor.HasRoute) return;
            if (!actor.Motor.Grounded) { actor.Axe.RecoverFooting(); return; }
            if (hasSeen && Mathf.Abs(actor.Position.x-lastSeen.x)>.45f) actor.Motor.SetMove(Mathf.Sign(lastSeen.x-actor.Position.x));
            else actor.Motor.Brake();
        }
        void Pursue(Vector2 target,BossObservation facts)
        {
            var motor=actor.Motor;
            if (motor.HasRoute || motor.DashActive) return;
            if (!motor.Grounded)
            {
                // A fighter can come to rest on the other body without acquiring terrain
                // footing. Steer clear of that body before planning another takeoff.
                float sideGap=actor.Position.x-facts.Position.x;
                float clearance=(motor.Size.x+facts.Body.size.x)*.5f+.14f;
                if (Mathf.Abs(sideGap)<clearance && actor.Position.y>facts.Position.y && Mathf.Abs(motor.Velocity.y)<1f)
                {
                    float escape=Mathf.Abs(sideGap)>.04f ? Mathf.Sign(sideGap) : Mathf.Sign(actor.Navigation.ArenaBounds.center.x-actor.Position.x);
                    motor.SetMove(escape);
                }
                else actor.Axe.RecoverFooting();
                return;
            }
            Vector2 delta=target-actor.Position;
            float side=Mathf.Sign(delta.x);
            // A lower opponent makes an upper perch an offensive option for the dive.
            float takeHeight=Ready(Punch.Meteor) && Mathf.Abs(delta.x)>2 ? .65f : 0;
            if (FindJump(target,takeHeight,out var hop))
            {
                float groundValue=Mathf.Abs(delta.y)*1.25f+Mathf.Abs(delta.x)*.48f;
                float landingValue=Mathf.Abs(hop.Landing.y-target.y)*.70f+Mathf.Abs(hop.Landing.x-target.x)*.50f
                    +hop.Duration*2f-takeHeight*Mathf.Clamp(hop.Landing.y-actor.Position.y,0,4);
                if (landingValue<groundValue && TakeOff(hop)) return;
            }
            float goal=target.x-side*.74f;
            if (actor.Navigation.TrySupport(actor.Position,motor.Size,out var support) && delta.y<-.8f && support!=surfaces[0].Collider)
            {
                // Walk off the useful edge instead of pacing directly above the target.
                float left=support.bounds.min.x,right=support.bounds.max.x;
                goal=Mathf.Abs(actor.Position.x-left)+Mathf.Abs(target.x-left)<Mathf.Abs(actor.Position.x-right)+Mathf.Abs(target.x-right)
                    ? left-motor.Size.x : right+motor.Size.x;
            }
            float dx=goal-actor.Position.x;
            if (Mathf.Abs(dx)<.15f) { motor.HoldHorizontal(); return; }
            float speed=9.2f+Mathf.Min(3.2f,Mathf.Max(0,facts.Velocity.x*side)*.5f);
            if (Time.time>=nextBurst && Mathf.Abs(dx)/speed>.32f && actor.Navigation.TrySupport(actor.Position,motor.Size,out support))
            {
                float edge=Mathf.Sign(dx)>0?support.bounds.max.x:support.bounds.min.x;
                float travel=Mathf.Min(Mathf.Abs(dx)*.72f,Mathf.Abs(edge-actor.Position.x)-motor.Size.x*.5f,4.6f);
                if (travel>.35f)
                {
                    motor.StartDash(Vector2.right*Mathf.Sign(dx),travel,19);
                    nextBurst=Time.time+.88f;
                    return;
                }
            }
            motor.PursueOnGround(Mathf.Sign(dx),speed);
        }

        bool FindJump(Vector2 target,float highGround,out JumpPlan best)
        {
            SampleSurfaces();
            best=default; float bestCost=float.PositiveInfinity;
            var from=actor.Position;
            for (int i=0;i<surfaces.Length;i++)
            {
                var surface=surfaces[i];
                float margin=actor.Motor.Size.x*.5f;
                float x=Mathf.Clamp(target.x-Mathf.Sign(target.x-from.x)*.55f,surface.Bounds.min.x+margin,surface.Bounds.max.x-margin);
                float height=surface.Top(.38f)+actor.Motor.Size.y*.5f-from.y;
                var motion=actor.Axe.LeapMotion(height);
                if (!TraceJump(x,target,motion,out var plan)) continue;
                var diff=plan.Landing-target;
                float cost=Mathf.Abs(diff.x)*.82f+Mathf.Abs(diff.y)*.72f+plan.Duration*.65f
                    - highGround*Mathf.Clamp(plan.Landing.y-from.y,0,4);
                if (cost >= bestCost) continue;
                bestCost=cost; best=plan;
            }
            return best.Support!=null;
        }

        // Mirror the motor's discrete acceleration and gravity. Only the body lands on
        // one-way surfaces; each crossing uses the surface height at both ends of the step.
        bool TraceJump(float aimX,Vector2 target,BossMotionProfile motion,out JumpPlan plan)
        {
            plan=default;
            Vector2 p=actor.Position, velocity=new Vector2(0,motion.JumpSpeed), half=actor.Motor.Size*.5f;
            var arena=actor.Navigation.ArenaBounds;
            float ceiling=arena.max.y-.25f-half.y;
            float dt=Time.fixedDeltaTime, bestDistance=float.PositiveInfinity, strike=0;
            for (float t=dt;t<1.9f;t+=dt)
            {
                float wish=BossMotor2D.JumpHorizontalTarget(aimX-p.x,motion,dt);
                velocity.x=BossMotor2D.StepHorizontal(velocity.x,wish,false,motion,dt);
                velocity.y+=Physics2D.gravity.y*motion.GravityScale*dt;
                var next=p+velocity*dt;
                next.x=Mathf.Clamp(next.x,arena.min.x+.25f+half.x,arena.max.x-.25f-half.x);
                if (next.y>ceiling) { next.y=ceiling; velocity.y=0; }
                float distance=(next+Vector2.right*Mathf.Sign(aimX-actor.Position.x)*.85f-target).sqrMagnitude;
                if (distance<bestDistance) { bestDistance=distance; strike=t; }
                int first=-1; float crossing=float.PositiveInfinity;
                for (int k=0;k<surfaces.Length;k++)
                {
                    var s=surfaces[k];
                    float before=p.y-half.y-s.Top(t-dt), after=next.y-half.y-s.Top(t);
                    if (before<-.025f || after>0 || after>=before) continue;
                    float f=Mathf.Clamp01(before/(before-after));
                    float x=Mathf.Lerp(p.x,next.x,f);
                    if (x+half.x<=s.Bounds.min.x || x-half.x>=s.Bounds.max.x || f>=crossing) continue;
                    first=k; crossing=f;
                }
                if (first>=0)
                {
                    var surface=surfaces[first];
                    plan=new JumpPlan { Support=surface.Collider, Motion=motion, Duration=t, AimX=aimX,
                        Landing=new Vector2(next.x,surface.Top(t)+half.y+.015f), StrikeAt=Mathf.Clamp(strike-.10f,.055f,Mathf.Max(.055f,t-.16f)) };
                    return t>.12f;
                }
                p=next;
            }
            return false;
        }
        bool TakeOff(JumpPlan plan)
        {
            if (plan.Support==null) return false;
            return actor.Motor.JumpToMovingSupport(new ArenaNavigation2D.RouteStep { Kind=ArenaNavigation2D.TravelKind.Jump,
                Start=actor.Position, Launch=actor.Position, End=new Vector2(plan.AimX,plan.Landing.y), Support=plan.Support, Duration=plan.Duration+.35f },plan.Motion);
        }
        bool FindDive(Vector2 target,out Vector2 contact)
        {
            SampleSurfaces();
            contact=default; float cost=float.PositiveInfinity;
            foreach (var s in surfaces)
            {
                float x=Mathf.Clamp(target.x,s.Bounds.min.x+.15f,s.Bounds.max.x-.15f);
                float flight=Vector2.Distance(actor.Position,new Vector2(x,s.Top(0)))/24f;
                var point=new Vector2(x,s.Top(flight)+actor.Motor.Size.y*.5f);
                if (point.y>=actor.Position.y-.28f) continue;
                float score=(point-target).sqrMagnitude;
                if (score<cost) { cost=score; contact=point; }
            }
            return cost<float.PositiveInfinity;
        }
    }
}
