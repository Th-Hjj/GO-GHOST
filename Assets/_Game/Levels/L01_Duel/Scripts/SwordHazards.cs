using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Owned by one executor. No autonomous GameObjects, static registry, or independent clock.
    public sealed class SwordHazards
    {
        public sealed class Trace
        {
            public BossThreat Threat;
            public float WarningAt,BornAt;
            public int Damage;
            public bool Wave,Deferred,Hit,Warned,Sounded,Retired;
            public Vector2 A(float time)=>Threat.Origin+(!Deferred&&Threat.Shape==BossThreatShape.MovingBlade?Threat.Direction*Threat.ProjectileSpeed*Mathf.Max(0,time-Threat.StartsAt):Vector2.zero);
            public Vector2 B(float time)=>Threat.End+(!Deferred&&Threat.Shape==BossThreatShape.MovingBlade?Threat.Direction*Threat.ProjectileSpeed*Mathf.Max(0,time-Threat.StartsAt):Vector2.zero);
        }
        readonly BossAgent actor;
        readonly List<Trace> traces=new List<Trace>(6);
        readonly List<BossThreat> candidate=new List<BossThreat>(8);
        Vector2 previousPlayer;
        float previousTime;
        int nextId=100000;
        public IReadOnlyList<Trace> Traces=>traces;
        public bool Pending=>traces.Count>0;
        public int Emitted {get;private set;}
        public int Echoes {get;private set;}
        public int Hits {get;private set;}
        public int AllyBlocks {get;private set;}
        public SwordHazards(BossAgent owner){actor=owner;ResetSample();}
        void ResetSample(){previousPlayer=actor.Player!=null?(Vector2)actor.Player.transform.position:Vector2.zero;previousTime=Time.time;}
        public void Clear(){traces.Clear();ResetSample();}
        // EndsAt is a prediction for public threat sampling, derived from physical boundary contact.
        // Runtime removal below checks the moving segment, never a configured lifetime/travel budget.
        float BoundaryEnd(BossThreat threat)
        {
            var bounds=actor.Navigation.ArenaBounds;
            float distance=Mathf.Min(ArenaBoundary2D.ExitDistance(bounds,threat.Origin,threat.Direction,threat.Radius),
                ArenaBoundary2D.ExitDistance(bounds,threat.End,threat.Direction,threat.Radius));
            return threat.StartsAt+distance/threat.ProjectileSpeed;
        }
        bool AtBoundary(Trace trace,float time)
        {
            var bounds=actor.Navigation.ArenaBounds;
            return !ArenaBoundary2D.Contains(bounds,trace.A(time),trace.Threat.Radius)
                ||!ArenaBoundary2D.Contains(bounds,trace.B(time),trace.Threat.Radius);
        }
        public void Wave(Vector2 centre,Vector2 direction)
        {
            if(traces.Count>=6)return;
            direction=direction.sqrMagnitude>.01f?direction.normalized:Vector2.right*actor.Facing;
            var s=actor.Profile.Sword;Vector2 side=new Vector2(-direction.y,direction.x)*s.WaveLength*.5f;
            Vector2 a=centre-side,b=centre+side;
            if(!ArenaBoundary2D.ClipSegment(actor.Navigation.ArenaBounds,ref a,ref b,.09f))return;
            var threat=new BossThreat {ActorId=actor.ActorId,AttackId=nextId++,Valid=true,Phase=BossThreatPhase.Active,
                Shape=BossThreatShape.MovingBlade,Origin=a,End=b,Direction=direction,
                Radius=.09f,ProjectileSpeed=s.WaveSpeed,StartsAt=Time.time};
            threat.EndsAt=BoundaryEnd(threat);
            traces.Add(new Trace {Wave=true,Damage=s.WaveDamage,WarningAt=Time.time,BornAt=Time.time,
                Threat=threat});
            Emitted++;
        }
        public void Echo(Vector2 from,Vector2 to,bool deferred)
        {
            Vector2 delta=to-from;if(delta.magnitude<.4f)return;
            // Each actual Draw leaves at most one suspended blade along its traveled cut.
            // It later launches forward; direction is authored now, never homing.
            Cut(Vector2.Lerp(from,to,.75f),delta.normalized,deferred);
        }
        public void Cut(Vector2 centre,Vector2 direction,bool deferred)
        {
            int count=0;foreach(var trace in traces)if(!trace.Wave)count++;
            if(count>=(actor.Actions.SwordUnbound?5:3)||traces.Count>=6)return;
            var s=actor.Profile.Sword;direction=direction.sqrMagnitude>.01f?direction.normalized:Vector2.right*actor.Facing;
            Vector2 side=new Vector2(-direction.y,direction.x)*s.EchoLength*.5f;
            centre+=Vector2.up*.35f;
            Vector2 a=centre-side,b=centre+side;var bounds=actor.Navigation.ArenaBounds;
            if(!ArenaBoundary2D.ClipSegment(bounds,ref a,ref b,.18f))return;
            float start=Time.time+s.EchoDelay+s.EchoWarning;
            foreach(var trace in traces)if(!trace.Wave&&!trace.Deferred)start=Mathf.Max(start,trace.Threat.StartsAt+s.EchoStagger);
            var threat=new BossThreat {ActorId=actor.ActorId,AttackId=nextId++,Valid=true,Phase=BossThreatPhase.Locked,
                Shape=BossThreatShape.MovingBlade,Origin=a,End=b,Radius=.18f,Direction=direction,ProjectileSpeed=s.EchoSpeed,
                StartsAt=start};
            threat.EndsAt=BoundaryEnd(threat);
            candidate.Clear();foreach(var trace in traces)if(!trace.Deferred)candidate.Add(trace.Threat);
            candidate.Add(threat);
            if(actor.Actions.HasLivingPartner&&!deferred&&!SwordEscape.HasRoute(actor,candidate,Mathf.Min(2.5f,threat.EndsAt-Time.time)))return;
            // With no separate warning stage, expose the harmless .2 s residue immediately.
            traces.Add(new Trace {Threat=threat,Damage=s.EchoDamage,BornAt=Time.time,WarningAt=s.EchoWarning>0?start-s.EchoWarning:Time.time,Deferred=deferred});Echoes++;
        }
        public void Replay()
        {
            var s=actor.Profile.Sword;float start=Time.time+s.EchoWarning;
            foreach(var trace in traces)
            {
                if(!trace.Deferred)continue;
                trace.Deferred=false;trace.WarningAt=start-s.EchoWarning;
                var t=trace.Threat;t.StartsAt=start;t.EndsAt=BoundaryEnd(t);trace.Threat=t;
                start+=s.EchoStagger;
            }
            candidate.Clear();foreach(var trace in traces)candidate.Add(trace.Threat);
            float lastEnd=start;foreach(var threat in candidate)lastEnd=Mathf.Max(lastEnd,threat.EndsAt);
            if(actor.Actions.HasLivingPartner&&!SwordEscape.HasRoute(actor,candidate,Mathf.Min(2.5f,lastEnd-Time.time)))
            {
                // Retire the second mark rather than create an unavoidable crossing.
                for(int i=traces.Count-1;i>=0;i--)if(!traces[i].Wave){traces.RemoveAt(i);break;}
            }
        }
        public void Publish(List<BossThreat> output)
        {
            foreach(var trace in traces)
            {
                if(trace.Deferred||Time.time<trace.WarningAt)continue;
                var threat=trace.Threat;
                threat.Phase=Time.time>=threat.StartsAt?BossThreatPhase.Active:BossThreatPhase.Locked;
                output.Add(threat);
            }
        }
        public void AppendScheduled(List<BossThreat> output)
        {foreach(var trace in traces)if(!trace.Deferred)output.Add(trace.Threat);}
        public void Tick()
        {
            if(actor.Player==null)return;
            var body=actor.Player.GetComponent<Collider2D>();Vector2 player=body.bounds.center;
            float now=Time.time,from=Mathf.Max(previousTime,now-Time.fixedDeltaTime);
            foreach(var trace in traces)
            {
                if(!trace.Wave&&!trace.Deferred&&!trace.Warned&&now>=trace.WarningAt)
                {trace.Warned=true;actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,trace.Threat.AttackId);}
                if(trace.Deferred||now<trace.Threat.StartsAt)continue;
                if(!trace.Wave&&!trace.Sounded)
                {trace.Sounded=true;actor.Sounds?.Play(Sfx.BladeHeavy,actor.ActorId,trace.Threat.AttackId);}
                var threat=trace.Threat;threat.Phase=BossThreatPhase.Active;
                bool blocked=false;
                if(threat.Shape==BossThreatShape.MovingBlade&&actor.Coordinator!=null)
                {
                    foreach(var other in actor.Coordinator.Actors)
                    {
                        if(other==actor||!other.IsAlive)continue;
                        if(threat.IntersectsMotion(other.Position,other.Position,other.Body.bounds.extents,from,now))
                        {
                            // Ally bodies are solid to emitted waves, even when the player is behind them.
                            blocked=true;AllyBlocks++;break;
                        }
                    }
                }
                if(blocked){trace.Retired=true;continue;}
                if(!trace.Hit&&threat.IntersectsMotion(previousPlayer,player,body.bounds.extents,from,now))
                {
                    trace.Hit=true;int before=actor.Player.CurrentHealth;
                    actor.Player.TakeDamage(trace.Damage,actor.gameObject);
                    if(actor.Player.CurrentHealth<before){Hits++;actor.View?.Impact(body.ClosestPoint(trace.A(now)));}
                    if(trace.Wave)trace.Retired=true;
                }
            }
            traces.RemoveAll(t=>t.Retired||!t.Deferred&&now>=t.Threat.StartsAt&&AtBoundary(t,now));
            previousPlayer=player;previousTime=now;
        }
    }
}
