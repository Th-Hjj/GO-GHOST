using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class FinalBossAxe
    {
        readonly struct Cut
        {
            public readonly float Start,Duration,From,To,Reach; public readonly int Group,Damage;
            public Cut(float start,float duration,float from,float to,int group,int damage,float reach)
            {Start=start;Duration=duration;From=from;To=to;Group=group;Damage=damage;Reach=reach;}
        }
        static readonly float[] TripleStrides={.45f,.85f,1.20f};
        static readonly Cut[] TripleCuts={
            new Cut(0,.14f,136,-31,0,16,2.05f),new Cut(.39f,.16f,-32,106,1,19,2.25f),new Cut(.92f,.20f,117,-55,2,30,2.85f)};
        readonly Cut[] combo=new Cut[TripleCuts.Length];
        int comboCount;
        static Cut CrosscutAt(float start, float riseDuringSwing)
        {
            if (riseDuringSwing>.40f) return new Cut(start,.21f,-45,105,0,28,2.55f);
            if (riseDuringSwing<-.40f) return new Cut(start,.21f,108,-82,0,28,2.55f);
            return new Cut(start,.21f,125,-35,0,28,2.55f);
        }
        const float PalmSweepLock=.20f;
        static Cut PalmCutAt(float start) => new Cut(start,.24f,129,-45,2,28,2.65f);
        internal const float PhysicalEdgeReach=1.15f;
        const float EdgeOffset=28.36f;
        AxePlan acceptedPlan;
        Cut palmSwing;
        int consumed;
        int soundedCuts;
        bool flightSound;
        bool launched,caught,abortAfterCatch,palmStepped;
        float lastAge,retrieveAt,retrieveAngle;
        Vector2 lastBody,lastPlayer,retrieveOrigin;
        readonly Vector2[] actualOrigins=new Vector2[2];
        readonly Vector2[][] futureOrigins={new Vector2[16],new Vector2[16],new Vector2[16]};

        void ResetPhaseOneState()
        {
            consumed=0;launched=caught=redirected=abortAfterCatch=palmStepped=false;
            soundedCuts=0;
            palmSwing=default;
            comboCount=1;
            combo[0]=TripleCuts[0];
            retrieveAt=-1;path.Clear();AxeSpin=0;
        }
        static bool AdvancesDuringWindup(Move move) => move==Move.TripleCut || move==Move.LeapingCrosscut;
        static float CrosscutRunIn(float forwardGap) => Mathf.Clamp(forwardGap - 3f, 0, 1.65f);

        bool StepIntoWindup(Move move)
        {
            float forwardGap = (acceptedPlan.Target.x - actor.Position.x) * lockedFacing;
            float stride = move==Move.LeapingCrosscut
                ? CrosscutRunIn(forwardGap)
                : Mathf.Clamp(forwardGap - .95f, 0, 1.15f);
            return SafeStep(stride * lockedFacing);
        }

        bool SafeStep(float signedDistance)
        {
            if(!actor.Motor.Grounded)return false;
            float dx=SupportedStride(actor.Position,signedDistance);
            if(Mathf.Abs(dx)<.025f)return false;
            actor.Motor.BeginStep(Mathf.Abs(dx));actor.Motor.SetMove(Mathf.Sign(dx));
            return true;
        }

        float SupportedStride(Vector2 start,float signedDistance)
        {
            if(Mathf.Abs(signedDistance)<.025f||!actor.Navigation.TrySupport(start,actor.Motor.Size,out var support))return 0;
            float extent=actor.Motor.Size.x*.5f;
            float x=Mathf.Clamp(start.x+signedDistance,support.bounds.min.x+extent,support.bounds.max.x-extent);
            float dx=x-start.x;
            if(dx*signedDistance<=0||!actor.Navigation.TryDash(start,new Vector2(x,start.y),actor.Motor.Size,out _))return 0;
            return dx;
        }

        bool InFrontOfAxe(BossObservation facts,float facing,float reach,float height)
        {
            if(!facts.Visible || facts.Confidence<=.12f || facts.Body.size.x<=0)return false;
            Vector2 seen=PublicPosition(facts,0);
            Vector2 delta=seen-actor.Position;
            float front=delta.x*facing;
            return front>=-.08f && front<=reach && Mathf.Abs(delta.y)<=height &&
                !Projectile.TerrainHit(actor.Position+Vector2.up*.15f,seen,.08f,out _,false);
        }

        void TickTriple(float age)
        {
            if(comboCount==TripleCuts.Length)return;
            var previous=combo[comboCount-1];
            float decision=previous.Start+previous.Duration+.04f;
            if(age<decision || activeEnds<=activeAt+decision)return;
            var next=TripleCuts[comboCount];
            // Commit each link from the public position, once, on the original side.
            // The preceding stroke may have completely missed.
            if(!actor.Motor.Grounded || !InFrontOfAxe(actor.Perception.Facts,lockedFacing,
                next.Reach+TripleStrides[comboCount]+.65f,1.55f))
            {
                activeEnds=phaseUntil=activeAt+decision;
                return;
            }
            float drift=previous.Start-TripleCuts[comboCount-1].Start;
            float start=Mathf.Max(next.Start+drift,age+.16f);
            SafeStep(TripleStrides[comboCount]*lockedFacing);
            combo[comboCount++]=new Cut(start,next.Duration,next.From,next.To,next.Group,next.Damage,next.Reach);
            activeEnds=phaseUntil=activeAt+start+(acceptedTiming.Active-next.Start);
        }
        void ActivatePhaseOne()
        {
            lastAge=0;lastBody=actor.Position;lastPlayer=PlayerBounds().center;path.Clear();
            if(Current==Move.LeapingCrosscut)
            {
                // The running tell can finish slightly short at an edge or against a body.
                // Preserve the locked landing and refresh both the execution and its public forecast.
                if(Vector2.Distance(acceptedPlan.JumpPath[0],actor.Position)>.04f)
                {
                    if(!actor.Navigation.TryJumpTrajectory(actor.Position,acceptedPlan.Landing,acceptedPlan.JumpMotion,
                        actor.Motor.Size,out var route,out var samples)){RecoverPhaseOne();return;}
                    acceptedPlan.Start=actor.Position;
                    acceptedPlan.JumpRoute=route;
                    acceptedPlan.JumpPath=samples;
                }
                if(!actor.Motor.StartCommittedJump(acceptedPlan.JumpRoute,acceptedPlan.JumpMotion)){RecoverPhaseOne();return;}
                launched=true;
                var cut=GetCut(0);
                activeEnds=activeAt+Mathf.Max(cut.Start+cut.Duration,(acceptedPlan.JumpPath.Length-1)*Time.fixedDeltaTime);
                phaseUntil=activeEnds;
            }
            else if(flight.Count>0)
            {
                if(Vector2.Distance(acceptedPlan.Start,actor.Position)>.04f)
                {
                    // A body collision can displace the caster during the wind-up. Keep
                    // the locked destinations but release from the physical grip.
                    acceptedPlan.Start=actor.Position;
                    if(Current==Move.AnchorVault && !acceptedPlan.Vault.RebaseRelease(actor.Position))
                    {
                        RecoverPhaseOne();
                        return;
                    }
                    BuildFlight(acceptedPlan,flight);
                }
                if(!FlightIsClear(flight,0,acceptedPlan.Facing)){RecoverPhaseOne();return;}
                AxeFlying=true;FlightPose(0);
                flightSound=true;
                actor.Sounds?.PlayPanned(Sfx.AxeFlight,PanAt(AxePosition.x),actor.ActorId,-attackId,
                    coverSeconds:flight[flight.Count-1].End);
                if(Current==Move.UnderpassReturn)SafeStep(acceptedPlan.Retreat.x-actor.Position.x);
            }
        }
        void TickPhaseOneAttack()
        {
            float age=ActiveTime;
            if(Current==Move.TripleCut)
            {
                TickTriple(age);
            }
            else if(Current==Move.UnderpassReturn)
            {
                if(age>=UnderHopAt&&!launched)
                {
                    launched=true;actor.Motor.HoldHorizontal();
                    if(!actor.Motor.StartCommittedJump(acceptedPlan.JumpRoute,acceptedPlan.JumpMotion))
                    {abortAfterCatch=true;BeginRetrieval();}
                }
            }
            else if(Current==Move.AnchorVault)acceptedPlan.Vault.Tick(age);
            else if(Current==Move.PalmDeflection)
            {
                if(age>=PalmAt && !redirected)RedirectPalm();
                if(age>=PalmRelease && !abortAfterCatch && !palmStepped)
                {
                    palmStepped=true;
                    FaceFinisher();
                    SafeStep(acceptedPlan.Landing.x-actor.Position.x);
                }
            }
            // A contact spanning a release, end, or flight-leg boundary is still resolved once.
            for (int i=soundedCuts;i<CutCount;i++)
            {
                if (age<GetCut(i).Start) break;
                float level=Current!=Move.TripleCut || i==2 ? 1f : i==0 ? .72f : .84f;
                actor.Sounds?.PlayPanned(Sfx.AxeSwing,PanAt(actor.Position.x),actor.ActorId,attackId,i,level:level);
                soundedCuts++;
            }
            ResolvePhaseOneHits(Mathf.Min(age,activeEnds-activeAt));
            if(Current==Move.None)return; // A lethal hit can stop and clear the encounter synchronously.
            if(AxeFlying)
            {
                if(retrieveAt>=0)TickRetrieval(age);
                else
                {
                    FlightPose(age);
                    if(Current==Move.UnderpassReturn&&age>=UnderCatch||Current==Move.PalmDeflection&&age>=PalmCatch)
                        CatchOrRetrieve();
                }
            }
            if (flightSound)
            {
                if (!AxeFlying || Current==Move.AnchorVault && acceptedPlan.Vault.Latched) StopFlightSound();
                else actor.Sounds?.SetAttackPan(actor.ActorId,-attackId,PanAt(AxePosition.x));
            }
            if(Time.time<activeEnds||AxeFlying)return;
            // Finish the fall before starting the punish window. A body-blocked landing
            // has a finite fallback and releases the attack motor back to gravity.
            if(!actor.Motor.Grounded&&Time.time<activeEnds+.65f)return;
            RecoverPhaseOne();
        }
        void FaceFinisher()
        {
            actor.Face(Vector2.right*acceptedPlan.FinishFacing);
            lockedFacing=actor.Facing;
        }
        void CatchOrRetrieve()
        {
            bool palm=Current==Move.PalmDeflection;
            var local=palm?new Vector2(.18f,.43f):new Vector2(-.37f,.32f);
            var held=Pivot(actor.Position,lockedFacing,local,palm?127:145);
            if(Vector2.Distance(AxePosition,held)>.18f){abortAfterCatch=true;BeginRetrieval();return;}
            caught=true;AxeFlying=false;
            if(palm)
            {
                // The physical catch is the decision boundary. Commit to one fully
                // telegraphed sweep or end the move here with its full recovery.
                var facts=actor.Perception.Facts;
                var cut=PalmCutAt(ActiveTime+PalmSweepLock);
                if(actor.Motor.Grounded && InFrontOfAxe(facts,lockedFacing,cut.Reach+.45f,1.45f))
                {
                    palmSwing=cut;
                    activeEnds=phaseUntil=activeAt+cut.Start+cut.Duration;
                }
                else activeEnds=phaseUntil=Time.time;
            }
            actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,attackId);
        }

        void BeginRetrieval()
        {
            if(retrieveAt>=0)return;
            retrieveAt=ActiveTime;retrieveOrigin=AxePosition;retrieveAngle=AxeSpin;
            if(abortAfterCatch)activeEnds=phaseUntil=activeAt+retrieveAt+.17f;
        }
        void TickRetrieval(float age)
        {
            float t=Mathf.Clamp01((age-retrieveAt)/.16f);
            var end=Hand()+Shaft(lockedFacing,HeldShaftAngle)*PivotOffset;
            AxePosition=Vector2.Lerp(retrieveOrigin,end,t);
            AxeSpin=Mathf.LerpAngle(retrieveAngle,WorldAngle(lockedFacing,HeldShaftAngle),t);
            if(t>=1){AxeFlying=false;caught=true;retrieveAt=-1;actor.Sounds?.Play(Sfx.ReturnPrepare,actor.ActorId,attackId);}
        }
        void StopFlightSound()
        {
            if (!flightSound) return;
            actor.Sounds?.StopAttack(actor.ActorId,-attackId);
            flightSound=false;
        }
        void RecoverPhaseOne()
        {
            StopFlightSound();
            AxeFlying=false;AxeSpin=0;path.Clear();
            actor.Motor.EndAttackMotion();
            SetPhase(Stage.Recovery,acceptedTiming.Recovery);
        }

        int CutCount=>Current==Move.TripleCut?comboCount:Current==Move.LeapingCrosscut||Current==Move.PalmDeflection&&palmSwing.Duration>0||
            Current==Move.AnchorVault&&acceptedPlan.Vault.Launched?1:0;
        Cut GetCut(int index)
        {
            if(Current==Move.TripleCut)return combo[index];
            if(Current==Move.LeapingCrosscut)
                return acceptedPlan.Swing;
            if(Current==Move.PalmDeflection)return palmSwing;
            return acceptedPlan.Vault.Chop;
        }
        Vector2 GripOffset(float age)
        {
            if(Current==Move.UnderpassReturn)
                return Vector2.Lerp(new Vector2(.39f,.24f),new Vector2(-.37f,.32f),Mathf.InverseLerp(.65f,1.51f,age));
            if(Current==Move.AnchorVault)return acceptedPlan.Vault.GripOffset(age);
            if(Current==Move.PalmDeflection)
            {
                var wind=new Vector2(-.16f,.03f);var palm=new Vector2(.46f,.27f);var raised=new Vector2(.39f,.61f);
                if(age<1.02f)return Vector2.Lerp(new Vector2(.39f,.31f),wind,Mathf.InverseLerp(.75f,1.02f,age));
                if(age<=PalmAt)return Vector2.Lerp(wind,palm,Mathf.InverseLerp(1.02f,PalmAt,age));
                if(age<=PalmRelease)return palm;
                palm.x*=acceptedPlan.Facing*lockedFacing;
                if(age<PalmRelease+.14f)return Vector2.Lerp(palm,raised,Mathf.InverseLerp(PalmRelease,PalmRelease+.14f,age));
                if(age<=PalmCatch)return Vector2.Lerp(raised,new Vector2(.18f,.43f),Mathf.InverseLerp(PalmRelease+.14f,PalmCatch,age));
                if(palmSwing.Duration==0)return new Vector2(.18f,.43f);
                return Vector2.Lerp(new Vector2(.18f,.43f),new Vector2(.45f,.20f),Mathf.InverseLerp(palmSwing.Start-PalmSweepLock,palmSwing.Start,age));
            }
            return new Vector2(.45f,.20f);
        }
        public Vector2 Hand()
        {
            var local=Current==Move.None||PhaseTwo?new Vector2(.35f,.25f):GripOffset(ActiveTime);
            return actor.Position+Offset(lockedFacing,local.x,local.y);
        }
        public float HeldShaftAngle
        {
            get
            {
                float age=Time.time-activeAt;
                if(Current==Move.UnderpassReturn)return age<.02f?18:145;
                if(Current==Move.AnchorVault && acceptedPlan.Vault.TryGetWindupAngle(age,out float vaultAngle))
                    return vaultAngle;
                if(Current==Move.PalmDeflection && (palmSwing.Duration==0 || age<palmSwing.Start))
                {
                    if(age<=.01f)return 10;
                    if(age<PalmAt+.01f)return 38;
                    if(palmSwing.Duration==0)return 127;
                    return Mathf.Lerp(127,palmSwing.From+EdgeOffset,Mathf.InverseLerp(palmSwing.Start-PalmSweepLock,palmSwing.Start,age));
                }
                float angle=65;
                for(int i=0;i<CutCount;i++)
                {
                    var c=GetCut(i);
                    if(age<c.Start)
                    {
                        float from=i==0?65:GetCut(i-1).To+EdgeOffset;
                        float start=i==0?-acceptedTiming.Prepare-acceptedTiming.Lock:GetCut(i-1).Start+GetCut(i-1).Duration;
                        angle=Mathf.Lerp(from,c.From+EdgeOffset,Mathf.SmoothStep(0,1,Mathf.InverseLerp(start,c.Start,age)));break;
                    }
                    angle=Mathf.Lerp(c.From,c.To,Mathf.InverseLerp(c.Start,c.Start+c.Duration,age))+EdgeOffset;
                    if(age<c.Start+c.Duration)break;
                }
                return Phase==Stage.Recovery?Mathf.Lerp(angle,65,PhaseProgress):angle;
            }
        }
        BossThreat CutThreat(Cut cut)
        {
            return new BossThreat{ActorId=actor.ActorId,AttackId=attackId,Phase=ThreatPhase,Shape=BossThreatShape.BladeArc,
                Valid=true,TerrainBlocked=true,Facing=lockedFacing,Origin=Hand(),ArcFrom=cut.From,ArcTo=cut.To,Reach=cut.Reach,
                Radius=.16f,StartsAt=activeAt+cut.Start,EndsAt=activeAt+cut.Start+cut.Duration};
        }
        public bool TryGetCut(out BossThreat threat,out bool locked,out bool active)
        {
            threat=default;active=locked=false;
            if(PhaseTwo||abortAfterCatch||retrieveAt>=0||Phase==Stage.Recovery||Phase==Stage.Idle)return false;
            for(int i=0;i<CutCount;i++)
            {
                var cut=GetCut(i);
                threat=CutThreat(cut);if(Time.time>threat.EndsAt)continue;
                active=Time.time>=threat.StartsAt;
                locked=!active&&Time.time>=threat.StartsAt-.16f&&Current!=Move.AnchorVault&&Current!=Move.LeapingCrosscut;
                return true;
            }
            return false;
        }

        void ResolvePhaseOneHits(float age)
        {
            if(abortAfterCatch||retrieveAt>=0)
            {
                lastAge=age;lastBody=actor.Position;lastPlayer=PlayerBounds().center;
                return;
            }
            float now=activeAt+age,previous=activeAt+lastAge;
            var player=PlayerBounds();Vector2 half=player.extents;
            for(int i=0;i<CutCount;i++)
            {
                var cut=GetCut(i);if((consumed&(1<<cut.Group))!=0)continue;
                var threat=CutThreat(cut);
                actualOrigins[0]=lastBody+OrientedGrip(lastAge);actualOrigins[1]=actor.Position+OrientedGrip(age);
                threat.OriginPath=actualOrigins;threat.PathStartsAt=previous;threat.PathStep=Mathf.Max(.001f,now-previous);
                CheckContact(threat,cut.Group,cut.Damage,player.center,half,previous,now);
                if(Current==Move.None)return;
            }
            for(int k=0;k<flight.Count;k++)
            {
                var leg=flight[k];
                if(leg.Group<0||(consumed&(1<<leg.Group))!=0||age<leg.Start||lastAge>leg.End)continue;
                for(int j=0;j<leg.Samples;j++)
                    if(CheckContact(FlightThreat(leg,j),leg.Group,leg.Damage,player.center,half,previous,now))break;
                if(Current==Move.None)return;
            }
            lastAge=age;lastBody=actor.Position;lastPlayer=player.center;
        }
        Vector2 OrientedGrip(float age)
        {var p=GripOffset(age);return Offset(lockedFacing,p.x,p.y);}
        bool CheckContact(BossThreat threat,int group,int damage,Vector2 centre,Vector2 half,float previous,float now)
        {
            if(!threat.IntersectsMotion(lastPlayer,centre,half,previous,now))return false;
            consumed|=1<<group;
            int partDamage = BossProfile.AttackDamage(damage);
            int defaultPeak = BossProfile.AttackDamage(Timings[(int)Current].Damage);
            Strike(Mathf.Max(1,Mathf.RoundToInt(partDamage * acceptedTiming.Damage / (float)defaultPeak)));
            return true;
        }
        void PublishPhaseOne(List<BossThreat> output)
        {
            if(abortAfterCatch||retrieveAt>=0)return;
            float from=Time.time,to=from+ConditionalDefaults.ThreatWindow+.025f;
            for(int i=0;i<CutCount;i++)
            {
                var c=GetCut(i);
                if((consumed&(1<<c.Group))!=0)continue;
                var threat=CutThreat(c);
                if(threat.StartsAt>to||threat.EndsAt<from)continue;
                // For the committed leap, the forecast uses its physically solved motor route.
                var points=futureOrigins[i];
                float launchAge=acceptedPlan.Vault?.LaunchAge ?? 0f;
                var jump=Current==Move.LeapingCrosscut ? acceptedPlan.JumpPath : acceptedPlan.Vault?.LaunchedPath;
                bool useFlight=jump!=null;
                Vector2 offset=Vector2.zero;
                if(useFlight && Phase==Stage.Active)
                {
                    int now=Mathf.Clamp(Mathf.RoundToInt((from-activeAt-launchAge)/Time.fixedDeltaTime),0,jump.Length-1);
                    offset=actor.Position-jump[now];
                    useFlight=!actor.Motor.RouteFailed && offset.sqrMagnitude<.35f*.35f;
                }
                for(int j=0;j<points.Length;j++)
                {
                    float dt=.02f*j,age=from+dt-activeAt;
                    Vector2 centre=actor.Position;
                    if(useFlight)
                    {
                        int k=Mathf.Clamp(Mathf.RoundToInt((age-launchAge)/Time.fixedDeltaTime),0,jump.Length-1);
                        centre=jump[k]+offset;
                    }
                    else if(!actor.Motor.Grounded)centre+=actor.Velocity*dt;
                    else centre.x+=actor.Velocity.x*Mathf.Min(dt,.12f);
                    points[j]=centre+OrientedGrip(age);
                }
                threat.OriginPath=points;threat.PathStartsAt=from;threat.PathStep=.02f;
                output.Add(threat);
            }
            foreach(var leg in flight)
            {
                if(leg.Group<0||(consumed&(1<<leg.Group))!=0||activeAt+leg.Start>to||activeAt+leg.End<from)continue;
                if(Current==Move.PalmDeflection&&leg.Start>=PalmAt&&!redirected)continue;
                for(int j=0;j<leg.Samples;j++)
                {
                    var threat=FlightThreat(leg,j);
                    if(threat.StartsAt<=to&&threat.EndsAt>=from)output.Add(threat);
                }
            }
        }
    }
}
