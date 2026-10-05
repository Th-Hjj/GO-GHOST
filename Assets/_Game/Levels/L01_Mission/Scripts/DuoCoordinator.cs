using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public enum DuoPhase { Idle, Assemble, Ready, FirstCommitted, FollowupCommitted, Recover }
    [DefaultExecutionOrder(-30)] // Offer a due shared plan before the actors start local actions.
    public sealed class DuoCoordinator : MonoBehaviour
    {
        [SerializeField] float decisionSeconds=.25f,assemblyLimit=2.5f,comboCooldown=8f,abortDelay=.8f,reorganizeSeconds=.65f;
        public IReadOnlyList<BossAgent> Actors=>actors;
        public DuoPhase Phase {get;private set;}
        public int CombosStarted {get;private set;}
        public int CombosCompleted {get;private set;}
        public int CombosAborted {get;private set;}
        BossAgent[] actors=System.Array.Empty<BossAgent>();
        BossAgent gunner,sword,majorOwner;
        BossPlan gunPlan,swordPlan;
        ArenaNavigation2D nav;
        float nextDecision,nextCombo,deadline,followupAt,recoverUntil;
        bool running,paused,gunStarted,swordStarted;
        int terrainVersion;
        float occludedSince=-1;
        public bool NeedsFireLane(BossAgent actor) => actor==sword && Phase==DuoPhase.Idle
            && gunner!=null && gunner.IsAlive && occludedSince>=0 && Time.time-occludedSince>.6f;
        public void Configure(BossAgent[] actors,ArenaNavigation2D nav)
        {this.actors=actors;this.nav=nav;foreach(var a in actors)if(a.Profile.Kind==BossKind.Refraction)gunner=a;else sword=a;nextCombo=Time.time+2;}
        public void BeginCombat(){running=true;}
        public void SetPaused(bool value){paused=value;}
        public void StopCombat(){running=false;majorOwner=null;Phase=DuoPhase.Idle;gunPlan=swordPlan=null;}
        public bool HasAssignment(BossAgent actor)=>Phase==DuoPhase.Assemble||Phase==DuoPhase.Ready||Phase==DuoPhase.FirstCommitted||Phase==DuoPhase.FollowupCommitted;
        public bool Occupied(BossAgent self,Vector2 point)
        {
            foreach(var other in actors)
            {
                if(other==self||!other.IsAlive)continue;
                if(Mathf.Abs(point.y-other.Position.y)<self.Motor.Size.y*.6f&&Mathf.Abs(point.x-other.Position.x)<self.Motor.Size.x+.2f)return true;
                var planned=other.Tactics.Current;
                if(planned!=null&&Vector2.Distance(point,planned.Destination)<self.Motor.Size.x+.2f)return true;
            }
            return false;
        }
        public float RoleScore(BossAgent self,BossAbility ability,Vector2 point)
        {
            if(actors.Length<2)return 0;
            var other=self==gunner?sword:gunner;
            if(other==null||!other.IsAlive)return 0;
            bool covering=other.Actions.Phase==BossActionPhase.Recovery||other.Actions.Reloading;
            if(covering)return ability==BossAbility.Burst||ability==BossAbility.Rhythm?.2f:0;
            if(other.Actions.HighThreat)return ability==BossAbility.Burst||ability==BossAbility.Guard?.25f:-.6f;
            var observation=self.Perception.Facts;
            return Mathf.Sign(point.x-observation.Position.x)!=Mathf.Sign(other.Position.x-observation.Position.x)?.15f:0;
        }
        public bool TryReserve(BossAgent actor,BossAbility ability,bool cooperative)
        {
            if(Time.time<recoverUntil)return false;
            if(cooperative&&(Phase==DuoPhase.Ready||Phase==DuoPhase.FirstCommitted))return true;
            if(Phase!=DuoPhase.Idle||majorOwner!=null&&majorOwner!=actor&&majorOwner.IsAlive&&majorOwner.Actions.Busy)return false;
            majorOwner=actor;return true;
        }
        public void Release(BossAgent actor)
        {
            if(majorOwner==actor)majorOwner=null;
            if(!actor.IsAlive&&Phase!=DuoPhase.Idle)Abort("actor defeated");
        }
        void Update()
        {
            if(!running||paused||Time.deltaTime<=0)return;
            if(Phase==DuoPhase.Recover){if(Time.time>=recoverUntil)Phase=DuoPhase.Idle;return;}
            if(gunner==null||sword==null||!gunner.IsAlive||!sword.IsAlive){if(Phase!=DuoPhase.Idle)Abort("single survivor");return;}
            var gunFacts=gunner.Perception.Facts;
            bool blocked=gunFacts.Confidence>.3f && !gunner.Actions.Busy && !gunner.Actions.Reloading
                && gunner.Tactics.AllyBlocks(gunner.Position+Vector2.up*.28f,gunFacts.Position)
                && !gunner.Tactics.TryGunTarget(BossAbility.Burst,gunner.Position,gunFacts,out _)
                && !gunner.Tactics.TryGunTarget(BossAbility.Piercing,gunner.Position,gunFacts,out _);
            if(!blocked)occludedSince=-1;else if(occludedSince<0)occludedSince=Time.time;
            if(Phase==DuoPhase.Assemble)
            {
                if(nav.Version!=terrainVersion||Time.time>deadline||gunner.Motor.RouteFailed||sword.Motor.RouteFailed||gunner.Perception.Facts.Confidence<.25f){Abort("assembly invalid/timeout");return;}
                bool g=Vector2.Distance(gunner.Position,gunPlan.Destination)<.45f&&gunner.Motor.Grounded;
                bool s=Vector2.Distance(sword.Position,swordPlan.Destination)<.45f&&sword.Motor.Grounded;
                if(g)gunner.Motor.Brake();if(s)sword.Motor.Brake();
                if(g&&s)
                {
                    var facts=sword.Perception.Facts;
                    if(!sword.Tactics.LegalAt(BossAbility.DrawDash,sword.Position,facts,out _)||gunner.Tactics.AllyBlocks(gunner.Position,gunPlan.TargetHypothesis)){Abort("lines changed");return;}
                    Phase=DuoPhase.Ready;deadline=Time.time+3;
                    // Plan the second line now, before any dodge response. The normal lock duration is retained.
                    float travel=Mathf.Abs(facts.Position.x-sword.Position.x)/sword.Profile.DashSpeed;
                    float delay=Mathf.Clamp(travel+.25f, .18f,.5f);
                    followupAt=Time.time+delay;
                    gunStarted=gunner.Actions.TryStart(BossAbility.Piercing,gunPlan.TargetHypothesis,true);
                    if(!gunStarted){Abort("gun start rejected");return;}
                    CombosStarted++;gunner.Trace("cross execution ready");
                }
            }
            else if(Phase==DuoPhase.Ready)
            {
                if(!gunner.Actions.Busy||gunner.Actions.Phase==BossActionPhase.Recovery||Time.time>deadline){Abort("first preparation interrupted");return;}
                if(Time.time>=followupAt)
                {
                    swordStarted=sword.Actions.TryStart(BossAbility.DrawDash,swordPlan.TargetHypothesis,true);
                    if(!swordStarted){Abort("dash start invalid");return;}
                    Phase=DuoPhase.FirstCommitted;
                }
            }
            else if(Phase==DuoPhase.FirstCommitted)
            {
                if(gunner.Actions.InterruptedCurrent){Abort("beam interrupted");return;}
                if(gunner.Actions.Phase==BossActionPhase.Active||gunner.Actions.Phase==BossActionPhase.Recovery)Phase=DuoPhase.FollowupCommitted;
                if(Time.time>deadline)Abort("execution timeout");
            }
            else if(Phase==DuoPhase.FollowupCommitted)
            {
                if(Time.time>deadline+2){Abort("recovery timeout");return;}
                if(!gunner.Actions.Busy&&!sword.Actions.Busy)
                {CombosCompleted++;Phase=DuoPhase.Recover;recoverUntil=Time.time+reorganizeSeconds;gunner.Trace("cross execution complete");}
            }
            else if(Time.time>=nextDecision)
            {
                if(Time.time>=nextCombo&&!gunner.Actions.Busy&&!sword.Actions.Busy&&gunner.Actions.Ready(BossAbility.Piercing)&&sword.Actions.Ready(BossAbility.DrawDash)
                    &&gunner.Actions.CanAfford(BossAbility.Piercing)&&gunner.Perception.Facts.Confidence>.6f&&sword.Perception.Facts.Confidence>.6f)
                {nextDecision=Time.time+decisionSeconds;Plan();}
            }
        }
        void Plan()
        {
            var guns=new List<BossPlan>(gunner.Tactics.BuildCandidates(BossAbility.Piercing));
            var swords=new List<BossPlan>(sword.Tactics.BuildCandidates(BossAbility.DrawDash));
            float best=float.NegativeInfinity;BossPlan gBest=null,sBest=null;
            var observed=gunner.Perception.Facts;
            foreach(var g in guns)foreach(var s in swords)
            {
                if(g.TravelSeconds>assemblyLimit-.3f||s.TravelSeconds>assemblyLimit-.3f||Vector2.Distance(g.Destination,s.Destination)<2)continue;
                Vector2 exit=observed.Predict(.35f)+Vector2.right*(gunner.Perception.LearnedRetreat!=0?gunner.Perception.LearnedRetreat:Mathf.Sign(observed.Velocity.x))*1.2f;
                Vector2 beam=(exit-g.Destination).normalized;
                if(!HasEscapeSample(g.Destination,s.Destination,exit,observed))continue;
                Vector2 swordTo=s.Destination-g.Destination;
                float cross=Mathf.Abs(Vector3.Cross(beam,swordTo).z);
                float along=Vector2.Dot(beam,swordTo);
                float allyHalfAlong=Mathf.Abs(beam.x)*sword.Body.bounds.extents.x+Mathf.Abs(beam.y)*sword.Body.bounds.extents.y;
                // A teammate beyond the planned coverage point may stop the beam afterwards;
                // that is a legal same-level pincer, not a blocked route to the coverage point.
                if(cross<sword.Body.bounds.extents.x+.2f&&along>0
                    &&along-allyHalfAlong<Vector2.Distance(g.Destination,exit))continue;
                float pair=g.Score+s.Score-Mathf.Abs(g.TravelSeconds-s.TravelSeconds)*.2f;
                if(pair>best){best=pair;gBest=g;sBest=s;gBest.TargetHypothesis=exit;}
            }
            if(gBest==null){nextCombo=Time.time+abortDelay;return;}
            gunPlan=gBest;swordPlan=sBest;terrainVersion=nav.Version;deadline=Time.time+assemblyLimit;
            Phase=DuoPhase.Assemble;nextCombo=Time.time+comboCooldown;gunStarted=swordStarted=false;
            gunner.Motor.SetRoute(gunPlan.Route);sword.Motor.SetRoute(swordPlan.Route);
            gunner.Trace($"cross assemble gun={gunPlan.Destination} sword={swordPlan.Destination}");
        }
        void Abort(string reason)
        {
            CombosAborted++;gunner?.Trace("cross abort "+reason);
            // Locked actions keep their commitments; only preparations may be cancelled.
            if(gunner!=null&&gunner.Actions.Phase==BossActionPhase.Prepare)gunner.Actions.AbortPreparation();
            if(sword!=null&&sword.Actions.Phase==BossActionPhase.Prepare)sword.Actions.AbortPreparation();
            gunner?.Motor.ClearRoute();sword?.Motor.ClearRoute();
            gunPlan=swordPlan=null;majorOwner=null;Phase=DuoPhase.Recover;
            recoverUntil=Time.time+reorganizeSeconds;nextCombo=Mathf.Max(nextCombo,Time.time+abortDelay);
        }
        bool HasEscapeSample(Vector2 gun,Vector2 blade,Vector2 beamTarget,BossObservation observation)
        {
            // Finite public capability samples, never player permissions, input or planned AI actions.
            // Fixed player run/jump/gravity are the retained combat contract. Test actual full-body clearance.
            Vector2 half=observation.Body.extents;
            Vector2 beam=(beamTarget-gun).normalized;
            for(int sample=0;sample<5;sample++)
            {
                bool jump=sample>=3;
                if(jump&&!observation.Grounded)continue;
                float run=sample==0?observation.Velocity.x:(sample==1||sample==3?-7:7);
                Vector2 position=observation.Position,velocity=new Vector2(run,jump?12:observation.Velocity.y);
                bool feasible=true;
                for(float t=0;t<.98f;t+=Time.fixedDeltaTime)
                {
                    velocity.y+=Physics2D.gravity.y*3*Time.fixedDeltaTime;
                    Vector2 next=position+velocity*Time.fixedDeltaTime;
                    if(!nav.SweepTraversal(position,next,half*2,out var hit,out _))
                    {
                        if(hit.normal.y>.6f&&velocity.y<0){next.y=position.y;velocity.y=0;}
                        else {feasible=false;break;}
                    }
                    position=next;
                    if(t>.60f&&t<.85f&&Mathf.Abs(position.y-blade.y)<half.y+.42f&&Mathf.Abs(position.x-blade.x)<sword.Profile.DrawDash.Range)
                    {feasible=false;break;}
                }
                if(feasible&&Mathf.Abs(Vector3.Cross(beam,position-gun).z)>half.magnitude+gunner.Profile.BeamRadius)return true;
            }
            return false;
        }
    }
}
