using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class ConditionalController
    {
        // One transition only. Existing navigation supplies collision queries, never a graph route.
        sealed class PlatformTransition
        {
            public Collider2D Source, Destination;
            public Bounds SourceBounds, DestinationBounds;
            public float TakeoffX, LandingX, Speed, JumpSpeed, Gravity, Deadline;
            public bool Higher, Launched, LeftGround;
        }
        bool platformOwnsMovement;
        readonly Collider2D[] platformOccupants=new Collider2D[32];
        public Collider2D PlatformDestination => running[1].Row>=0?running[1].Platform?.Destination:null;
        public int PlatformTransitionsCompleted {get;private set;}
        ArenaNavigation2D PlatformGeometry
        {
            get {foreach(var actor in actors)if(actor!=null&&actor.Navigation!=null)return actor.Navigation;return null;}
        }
        static bool ActiveSupport(Collider2D support)=>support!=null&&support.enabled&&support.gameObject.activeInHierarchy;
        float PlatformSteer(float target)
            =>Mathf.Clamp((target-shape.bounds.center.x)/(Mathf.Max(.01f,movement.MoveSpeed)*Time.fixedDeltaTime),-1,1);

        bool PlatformHasMovementPriority(ConditionalRule rule)
        {
            if(!output.MovementOwned||running[0].Row<0)return true;
            var moveRule=loadout.Operations[0].Rules[running[0].Row];
            // Global displayed order is used only for this action's two-operation claim.
            // Older unnumbered snapshots retain their normal operation order (Move before Jump).
            return rule.DisplayOrder>=0&&moveRule.DisplayOrder>=0&&rule.DisplayOrder<moveRule.DisplayOrder;
        }
        bool StartPlatform(ConditionalRule rule,Running r)
        {
            var nav=PlatformGeometry;
            if(!CanJump()||movement.HitPushActive||!body.simulated||movement.MoveSpeed<=.01f||nav==null
                ||!PlatformHasMovementPriority(rule))return false;
            var state=states[1,r.Row];
            if(state.PlatformFailed&&state.PlatformFailureVersion==nav.Version
                &&Vector2.Distance(state.PlatformFailurePosition,body.position)<.25f)return false;
            if(!SelectPlatform(rule.PlatformDirection==PlatformDirectionOption.Higher,nav,out var plan))return false;
            state.PlatformFailed=false;r.Platform=plan;
            return ContinuePlatform(rule,r);
        }
        bool FailPlatform(Running r)
        {
            var state=states[1,r.Row];
            state.PlatformFailed=true;state.PlatformFailurePosition=body.position;
            state.PlatformFailureVersion=PlatformGeometry!=null?PlatformGeometry.Version:-1;
            return false;
        }
        bool ContinuePlatform(ConditionalRule rule,Running r)
        {
            var p=r.Platform;var nav=PlatformGeometry;
            if(p==null||nav==null||dodge.IsDodging||movement.HitPushActive||!movement.isActiveAndEnabled
                ||!body.simulated||!input.GameplayInputEnabled||!PlatformHasMovementPriority(rule))return false;
            if(!ActiveSupport(p.Source)||!ActiveSupport(p.Destination)
                ||p.Source.bounds!=p.SourceBounds||p.Destination.bounds!=p.DestinationBounds
                ||Mathf.Abs(movement.MoveSpeed-p.Speed)>.001f||Mathf.Abs(movement.JumpSpeed-p.JumpSpeed)>.001f
                ||Mathf.Abs(-Physics2D.gravity.y*body.gravityScale-p.Gravity)>.001f)return false;
            if(Time.time>p.Deadline)return FailPlatform(r);
            if(!movement.IsGrounded)p.LeftGround=true;
            if(p.LeftGround&&movement.IsGrounded)
            {
                bool arrived=nav.TrySupport(shape.bounds.center,shape.bounds.size,out var landed)&&landed==p.Destination;
                if(!arrived)return FailPlatform(r);
                PlatformTransitionsCompleted++;r.Completed=true;return false;
            }
            float target=p.LandingX;
            bool jump=false;
            if(!p.LeftGround&&!p.Launched)
            {
                if(!nav.TrySupport(shape.bounds.center,shape.bounds.size,out var support)||support!=p.Source)return FailPlatform(r);
                target=p.TakeoffX;
                if(p.Higher&&Mathf.Abs(shape.bounds.center.x-p.TakeoffX)<=.035f)
                {
                    // Recheck the frozen path at takeoff; do not choose a different destination.
                    if(!PlatformFlight(nav,shape.bounds.center,p.LandingX,p.Destination,true,out _))return FailPlatform(r);
                    jump=true;target=p.LandingX;
                }
            }
            else if(!p.Higher&&shape.bounds.min.y>=p.SourceBounds.max.y-.12f)
                target=p.TakeoffX; // Clear the real body before steering back beneath the source.

            float move=PlatformSteer(target);
            if(Mathf.Abs(move)>.01f)
            {
                // A transient body obstruction is unavailable now, not a permanent failed route.
                if(Travel(move,.12f)<.025f)return false;
                r.Stuck=Mathf.Abs(body.position.x-r.PreviousX)<.003f?r.Stuck+Time.deltaTime:0;
                if(r.Stuck>ConditionalDefaults.StuckWindow)return FailPlatform(r);
            }
            else r.Stuck=0;
            r.PreviousX=body.position.x;
            // Only revoke the lower movement row once the transition can actually emit input.
            running[0].Row=-1;
            platformOwnsMovement=true;output.MovementOwned=true;output.Move=move;
            output.JumpOwned=true;output.Jump=jump;
            return true;
        }

        bool SelectPlatform(bool higher,ArenaNavigation2D nav,out PlatformTransition best)
        {
            best=null;
            Vector2 size=shape.bounds.size,origin=shape.bounds.center;
            if(!nav.TrySupport(origin,size,out var source)||!ActiveSupport(source))return false;
            float speed=movement.MoveSpeed,gravity=-Physics2D.gravity.y*body.gravityScale;
            if(gravity<=.01f||platforms==null)return false;
            float half=size.x*.5f,margin=half+.10f;
            var from=source.bounds;
            float sourceMin=from.min.x+margin,sourceMax=from.max.x-margin;
            if(sourceMin>sourceMax)return false;
            float maxRise=movement.JumpSpeed*movement.JumpSpeed/(2*gravity)-.12f;
            float localReach=speed*2*movement.JumpSpeed/gravity;
            float bestRise=float.PositiveInfinity,bestCost=float.PositiveInfinity;
            foreach(var destination in platforms)
            {
                if(!ActiveSupport(destination)||destination==source)continue;
                var to=destination.bounds;
                float rise=to.max.y-from.max.y,level=Mathf.Abs(rise);
                if(higher?rise<=.12f||rise>maxRise:rise>=-.12f)continue;
                if(level>bestRise+.05f)continue;
                float min=to.min.x+margin,max=to.max.x-margin;
                if(min>max)continue;
                // Closest safe point, centre, then both insets; stable order breaks equal costs.
                for(int point=0;point<4;point++)
                {
                    float land=point==0?Mathf.Clamp(origin.x,min,max):point==1?(min+max)*.5f:point==2?min:max;
                    Vector2 landing=new Vector2(land,to.max.y+size.y*.5f+.025f);
                    if(!nav.CanStand(landing,size)||!PlatformLandingFree(landing,size))continue;
                    for(int side=0;side<(higher?3:2);side++)
                    {
                        float launch=higher
                            ?side==0?Mathf.Clamp(origin.x,sourceMin,sourceMax):side==1?Mathf.Clamp(land,sourceMin,sourceMax):Mathf.Clamp((origin.x+land)*.5f,sourceMin,sourceMax)
                            :side==0?from.min.x-half-.10f:from.max.x+half+.10f;
                        float walk=Mathf.Abs(launch-origin.x);
                        if(walk>localReach||Mathf.Abs(land-launch)>localReach&&higher)continue;
                        var takeoff=new Vector2(launch,from.max.y+size.y*.5f+.025f);
                        if(!nav.SweepTraversal(origin,takeoff,size,out _,out _))continue;
                        if(!PlatformFlight(nav,takeoff,land,destination,higher,out float flight))continue;
                        float cost=walk+Mathf.Abs(land-launch);
                        if(level>=bestRise-.05f&&cost>=bestCost-.001f)continue;
                        bestRise=level;bestCost=cost;
                        best=new PlatformTransition {Source=source,Destination=destination,SourceBounds=from,DestinationBounds=to,
                            TakeoffX=launch,LandingX=land,Higher=higher,Speed=speed,JumpSpeed=movement.JumpSpeed,Gravity=gravity,
                            Deadline=Time.time+walk/speed+flight+.75f};
                    }
                }
            }
            return best!=null;
        }
        bool PlatformLandingFree(Vector2 center,Vector2 size)
        {
            var filter=new ContactFilter2D {useTriggers=false};
            int count=Physics2D.OverlapBox(center,size-Vector2.one*.06f,0,filter,platformOccupants);
            if(count==platformOccupants.Length)return false;
            for(int i=0;i<count;i++)
            {
                var other=platformOccupants[i];
                if(other==null||other.attachedRigidbody==body||Physics2D.GetIgnoreCollision(shape,other))continue;
                if(other.GetComponentInParent<Enemy>()!=null)return false;
            }
            return true;
        }
        bool PlatformFlight(ArenaNavigation2D nav,Vector2 start,float land,Collider2D destination,bool jump,out float flight)
        {
            flight=0;
            float step=Time.fixedDeltaTime,gravity=-Physics2D.gravity.y*body.gravityScale;
            float vy=jump?movement.JumpSpeed:0;
            Vector2 size=shape.bounds.size,position=start;
            // This is the retained player's immediate horizontal velocity and semi-implicit gravity.
            // Stop at the first physical landing, including intervening one-way platforms.
            float horizon=(jump?2*movement.JumpSpeed/gravity:0)
                +Mathf.Sqrt(2*Mathf.Max(0,start.y-size.y*.5f-destination.bounds.max.y)/gravity)+.5f;
            for(float t=step;t<=horizon;t+=step)
            {
                Vector2 next=position;
                next.x=Mathf.MoveTowards(next.x,land,movement.MoveSpeed*step);
                vy-=gravity*step;next.y+=vy*step;
                if(!nav.SweepTraversal(position,next,size,out var hit,out _))
                {
                    flight=t;
                    return vy<=0&&hit.normal.y>.5f&&hit.collider==destination
                        &&next.x-size.x*.5f>=destination.bounds.min.x+.035f
                        &&next.x+size.x*.5f<=destination.bounds.max.x-.035f;
                }
                if(next.x-size.x*.5f<arena.min.x+.02f||next.x+size.x*.5f>arena.max.x-.02f
                    ||next.y+size.y*.5f>arena.max.y-.02f)return false;
                position=next;
            }
            return false;
        }
    }
}
