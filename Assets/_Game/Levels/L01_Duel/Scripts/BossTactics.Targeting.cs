using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Delayed public target prediction and ally blocking. Prediction caches live only within synchronous queries.
    public sealed partial class BossTactics
    {
        readonly PublicPrediction gunPrediction=new PublicPrediction(),swordPrediction=new PublicPrediction();
        int predictionDepth;
        sealed class PublicPrediction
        {
            public readonly List<Vector2> Points=new List<Vector2>(65);
            public BossObservation Original,Facts;
            public Vector2 Point,Velocity;
            public float Time;
        }
        void BeginPredictions()
        {
            if(predictionDepth++==0)
            {gunPrediction.Points.Clear();swordPrediction.Points.Clear();actor.Navigation.BeginRouteBatch();}
        }
        void EndPredictions(){if(--predictionDepth==0)actor.Navigation.EndRouteBatch();}
        public Vector2 PredictBody(BossObservation facts,float horizon)
            =>PredictBodyLimited(facts,horizon,.65f);
        public Vector2 PredictSwordBody(BossObservation facts,float horizon)
            =>PredictBodyLimited(facts,horizon+Mathf.Clamp(Time.time-facts.ObservedAt,0,.25f),1.25f,true);
        public Vector2 SwordTarget(BossAbility ability,Vector2 from,BossObservation facts,float remainingTell=-1,int cut=0)
        {
            if(ability==BossAbility.Guard)return facts.Position;
            var settings=actor.Profile.Settings(ability);
            float delay=remainingTell>=0?remainingTell:ability==BossAbility.Rhythm
                ?actor.Profile.RhythmPrepare[cut]+actor.Profile.RhythmLock[cut]:settings.Prepare+settings.Lock;
            if(ability==BossAbility.Rhythm||ability==BossAbility.DashFollowup||ability==BossAbility.DropFollowup
                ||ability==BossAbility.LeapFollowup||ability==BossAbility.FlankCut)delay+=actor.Profile.RhythmActive[cut]*.5f;
            Vector2 target=PredictSwordBody(facts,delay);
            if(ability==BossAbility.DrawDash||ability==BossAbility.ReturnCut)
            {
                float speed=ability==BossAbility.DrawDash?actor.Profile.DashSpeed:actor.Profile.ReturnSpeed;
                for(int i=0;i<3;i++)target=PredictSwordBody(facts,delay+Vector2.Distance(from,target)/speed);
            }
            return target;
        }
        bool TrySwordDash(Vector2 from,BossObservation facts,out Vector2 target,out Vector2 end)
        {
            target=SwordTarget(BossAbility.DrawDash,from,facts);end=from;
            float distance=Vector2.Distance(from,target);
            if(distance<actor.Motor.Size.x*2||Vector2.Distance(from,facts.Position)>actor.Navigation.SameLevelSpacing
                ||!actor.Navigation.TryDirectionalAttack(from,target,actor.Motor.Size,out end))return false;
            Vector2 direction=(end-from).normalized;
            float extent=Mathf.Abs(direction.x)*facts.Body.extents.x+Mathf.Abs(direction.y)*facts.Body.extents.y;
            if(Vector2.Distance(from,end)>=distance-extent-actor.Profile.BladeRadius-.2f)return true;
            // A retreat can outlast direct contact. Still accept a supported closing dash when
            // its actual finite exit gains distance and creates a legal next-cut opportunity.
            float arrival=actor.Profile.DrawDash.Prepare+actor.Profile.DrawDash.Lock+Vector2.Distance(from,end)/actor.Profile.DashSpeed;
            Vector2 atExit=PredictSwordBody(facts,arrival);
            float gap=Vector2.Distance(end,atExit);
            return actor.Navigation.CanStand(end,actor.Motor.Size)&&Mathf.Abs(end.y-atExit.y)<1.5f
                &&gap<=actor.Profile.RhythmReach[0]+actor.Profile.RhythmStep[0]+facts.Body.extents.x
                &&gap+actor.Motor.Size.x<Vector2.Distance(from,PredictSwordBody(facts,0));
        }
        public bool GuardOpportunity(Vector2 point,BossObservation facts,out float urgency)
        {
            urgency=0;
            if(!facts.Visible||facts.Confidence<.3f)return false;
            float facing=Mathf.Sign(facts.Position.x-point.x);
            float begins=actor.Profile.Guard.Prepare+actor.Profile.Guard.Lock;
            foreach(var shot in Projectile.Active)
            {
                if(shot==null||shot.OwnerLayer==actor.gameObject.layer)continue;
                Vector2 position=shot.transform.position,velocity=shot.GetComponent<Rigidbody2D>().linearVelocity;
                float speed=velocity.magnitude;if(speed<.01f||velocity.x*facing>=0)continue;
                Vector2 direction=velocity/speed,to=point-position;
                float along=Vector2.Dot(to,direction),radius=actor.Profile.Guard.Range+shot.Radius;
                float perpendicular=to.sqrMagnitude-along*along;
                if(along<=0||perpendicular>radius*radius)continue;
                float enter=along-Mathf.Sqrt(Mathf.Max(0,radius*radius-perpendicular));
                float arrival=enter/speed;
                if(arrival<begins+Time.fixedDeltaTime||arrival>begins+actor.Profile.Guard.Active)continue;
                Vector2 contact=position+direction*enter-point;
                if(Vector2.Dot(contact.normalized,Vector2.right*facing)<=Mathf.Cos(actor.Profile.GuardArc*.5f*Mathf.Deg2Rad))continue;
                // Reflect shots which would actually strike our body, not harmless fly-bys.
                var body=new Bounds(point,actor.Motor.Size+Vector2.one*shot.Radius*2);
                if(!body.IntersectRay(new Ray(position,direction),out _))continue;
                urgency=Mathf.Max(urgency,1-Mathf.Clamp01((arrival-begins)/actor.Profile.Guard.Active));
            }
            return urgency>0;
        }
        Vector2 PredictBodyLimited(BossObservation facts,float horizon,float limit,bool sword=false)
        {
            if(predictionDepth>0)
            {
                var cache=sword?swordPrediction:gunPrediction;
                var original=cache.Original;
                // Only reuse prefixes during this synchronous query. Changed observations, even
                // within that query, start a fresh trajectory; nothing survives to a later frame.
                if(cache.Points.Count==0||!original.Position.Equals(facts.Position)||!original.Velocity.Equals(facts.Velocity)
                    ||original.Grounded!=facts.Grounded||original.Dodging!=facts.Dodging
                    ||original.ObservedAt!=facts.ObservedAt||!original.Body.size.Equals(facts.Body.size))
                {
                    cache.Points.Clear();cache.Original=cache.Facts=facts;
                    cache.Point=facts.Position;cache.Velocity=facts.Velocity;cache.Time=0;
                    cache.Points.Add(cache.Point);
                }
                // Keep float accumulation identical to the original fixed-step predictor.
                int count=0;float step=Time.fixedDeltaTime;
                for(float t=0;t<Mathf.Min(limit,horizon);t+=step)count++;
                while(cache.Points.Count<=count)
                {
                    StepPublicPrediction(ref cache.Facts,ref cache.Point,ref cache.Velocity,cache.Time,step,sword);
                    cache.Time+=step;cache.Points.Add(cache.Point);
                }
                return cache.Points[count];
            }
            Vector2 point=facts.Position,velocity=facts.Velocity;
            float dt=Time.fixedDeltaTime;
            for(float t=0;t<Mathf.Min(limit,horizon);t+=dt)
                StepPublicPrediction(ref facts,ref point,ref velocity,t,dt,sword);
            return point;
        }
        // A synchronous sword candidate search needs many horizons of the same public trajectory.
        // Build its prefixes once; the planner discards them before the next search.
        internal void PredictSwordPath(BossObservation facts,List<Vector2> points)
        {
            points.Clear();Vector2 point=facts.Position,velocity=facts.Velocity;points.Add(point);
            float dt=Time.fixedDeltaTime;
            for(float t=0;t<1.25f;t+=dt)
            {
                StepPublicPrediction(ref facts,ref point,ref velocity,t,dt,true);
                points.Add(point);
            }
        }
        void StepPublicPrediction(ref BossObservation facts,ref Vector2 point,ref Vector2 velocity,float t,float dt,bool sword)
        {
            // A public dodge is finite. Do not extrapolate its burst speed through an entire leap.
            if(sword&&facts.Dodging&&facts.ObservedAt+t>=actor.Perception.DodgeStartedAt+.18f)
                velocity.x=actor.Perception.BeforeDodgeVelocityX;
            if(facts.Grounded&&!actor.Navigation.TrySupport(point,facts.Body.size,out _))facts.Grounded=false;
            if(!facts.Grounded)velocity.y+=Physics2D.gravity.y*3*dt;
            Vector2 next=point+velocity*dt;
            if(!actor.Navigation.SweepTraversal(point,next,facts.Body.size,out var hit,out var safe))
            {
                point=safe;
                if(hit.normal.y>.5f&&velocity.y<0){velocity.y=0;facts.Grounded=true;}
                else if(Mathf.Abs(hit.normal.x)>.5f)velocity.x=0;
                else velocity.y=0;
            }
            else point=next;
        }
        public bool TryGunTarget(BossAbility ability,Vector2 position,BossObservation facts,out Vector2 target)
            =>TryGunTarget(ability,position,facts,out target,-1);
        public bool TryGunTarget(BossAbility ability,Vector2 position,BossObservation facts,out Vector2 target,float remainingPreparation)
        {
            float flight=ability==BossAbility.Piercing?0:Vector2.Distance(position,facts.Position)/actor.Profile.BulletSpeed;
            float delay=remainingPreparation>=0?remainingPreparation:ability==BossAbility.Piercing
                ?actor.Profile.Piercing.Prepare+actor.Profile.Piercing.Lock:actor.Profile.BurstShotLock;
            float age=Mathf.Clamp(Time.time-facts.ObservedAt,0,actor.Profile.SightMemory);
            Vector2 centre=PredictBody(facts,age+delay+flight),origin=position+Vector2.up*.28f;
            target=centre;
            // All points remain on the observed body. An exposed head can be shot over a teammate.
            for(int i=0;i<3;i++)
            {
                Vector2 point=centre+Vector2.up*facts.Body.extents.y*(i==0?0:i==1?.72f:-.72f);
                if(AllyBlocks(origin,point))continue;
                target=point;return true;
            }
            return false;
        }
        public bool ClearGunLine(Vector2 position,Vector2 target)=>!AllyBlocks(position+Vector2.up*.28f,target);
        public bool AllyBlocks(Vector2 from,Vector2 target)
        {
            if(actor.Coordinator==null)return false;
            Vector2 delta=target-from;
            var filter=new ContactFilter2D().NoFilter();filter.SetLayerMask(1<<actor.gameObject.layer);filter.useTriggers=Physics2D.queriesHitTriggers;
            Physics2D.CircleCast(from,actor.ProjectilePrefab.Radius,delta.normalized,filter,allyHits,delta.magnitude);
            foreach(var hit in allyHits)
                foreach(var other in actor.Coordinator.Actors)if(other!=actor&&other.IsAlive&&hit.collider==other.Body)return true;
            return false;
        }
    }
}
