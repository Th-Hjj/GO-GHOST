using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossActionExecutor
    {
        SwordPlanner swordPlanner;
        SwordHazards swordHazards;
        SwordTravel swordTravel;
        readonly List<SwordBeat> swordPhrase=new List<SwordBeat>(7);
        readonly List<BossThreat> swordSafety=new List<BossThreat>(8);
        int swordIndex,swordBeatId,swordPhraseEchoStart;
        float swordDecisionAt,swordDeadline,swordTravelDeadline,swordArcFrom,swordArcTo,swordFacing;
        float swordRecoveryStarted=-1,swordLastProgressAt;
        Vector2 swordLastProgress,swordDrawStart,swordBladePrevious;
        Vector2 swordPreviousPlayer;
        Vector2 swordSettleTarget;
        float swordSettleUntil;
        float swordJumpLaunch=-1,swordStrikeAt,swordPreviewAt;
        bool swordJumpStruck,swordResidualStepTried,swordResidualMoving;
        Vector2 swordResidualEnd;
        BossAbility swordPreview;
        Vector2 swordPreviewOrigin,swordPreviewTarget;
        float swordPreviewedAt;
        bool swordAirLink,swordAirPermission,swordUnbound;
        bool swordTravelJumpPlanned;
        int swordTravelBeats;
        bool swordChaseLinked,swordCutSeeded;
        Vector2 swordCutEnd;
        float swordLandingFlowUntil;
        BossThreat swordThreat;
        public SwordBeat CurrentSwordBeat {get;private set;}
        public bool SwordUnbound=>swordUnbound;
        public bool PendingSwordThreats=>swordHazards!=null&&swordHazards.Pending;
        public IReadOnlyList<SwordHazards.Trace> SwordTraces=>swordHazards?.Traces;
        public int SwordPhrasesCompleted {get;private set;}
        public int SwordAbortedTravels {get;private set;}
        public int SwordAirLinks {get;private set;}
        public int SwordSupportSettles {get;private set;}
        public int SwordJumpCuts {get;private set;}
        public int SwordResidualSteps {get;private set;}
        public int SwordWalks {get;private set;}
        public int SwordTravelDashes {get;private set;}
        public int SwordPlatformJumps {get;private set;}
        public int SwordCutSteps {get;private set;}
        public int SwordChaseLinks {get;private set;}
        public bool SwordResidualWaiting=>IsSwordPhrase(Ability)&&Phase==BossActionPhase.Recovery&&swordRecoveryStarted<0&&PendingSwordThreats;
        public bool SwordTerminalRecovery=>IsSwordPhrase(Ability)&&Phase==BossActionPhase.Recovery&&swordRecoveryStarted>=0;
        public SwordTravel SwordTravelPlan=>swordTravel;
        public float SwordStrikeAt=>swordStrikeAt;
        public float SwordCutProgress=>CurrentSwordBeat==SwordBeat.JumpCut
            ?(Phase==BossActionPhase.Active?Mathf.Clamp01((Time.time-swordStrikeAt)/p.Sword.JumpCutActive):0):PhaseProgress;
        public int SwordWaveCount=>swordHazards?.Emitted??0;
        public int SwordEchoCount=>swordHazards?.Echoes??0;
        public int SwordHazardHits=>swordHazards?.Hits??0;
        public int SwordAllyBlocks=>swordHazards?.AllyBlocks??0;
        public Vector2[] SwordTravelPath=>swordTravel?.Path;
        public Vector2 SwordBladeRoot=>actor.Position+(CurrentSwordBeat==SwordBeat.LowCut?Vector2.down*.5f:Vector2.up*.15f);
        public Vector2 SwordBladeTip=>SwordBladeRoot+SwordCutDirection(Phase==BossActionPhase.Active?SwordCutProgress:Phase==BossActionPhase.Recovery?1:0)*p.Sword.CutReach;
        // The held blade leads the body's solid contact face. Its expressed and damaging capsule
        // includes that real forward reach, including the final step against a moving opponent.
        Vector2 SwordDrawLead=>Aim*(DashBladeLead(Aim,actor.Motor.Size,.48f)+.25f);
        public bool HasLivingPartner
        {
            get {if(actor.Coordinator!=null)foreach(var other in actor.Coordinator.Actors)if(other!=actor&&other.IsAlive)return true;return false;}
        }
        public static bool IsSwordPhrase(BossAbility value)=>value>=BossAbility.SwordPursuit&&value<=BossAbility.SwordRedline;
        void ConfigureSword()
        {
            if(p.Sword==null)p.Sword=new SwordCombatSettings();
            swordPlanner=new SwordPlanner(actor);swordHazards=new SwordHazards(actor);
            swordLastProgress=actor.Position;swordLastProgressAt=Time.time;
        }
        public void TickSwordDecision()
        {
            if(!actor.Running||!actor.IsAlive||Busy||Time.deltaTime<=0)return;
            if(!swordUnbound&&(float)actor.Health.CurrentHealth/actor.Health.MaxHealth<=p.Sword.UnboundHealth)
            {swordUnbound=true;actor.View?.Impact(actor.Position);actor.Trace("sword unbound");}
            if(swordSettleUntil>Time.time&&actor.Motor.Grounded&&Mathf.Abs(swordSettleTarget.x-actor.Position.x)>.08f)
            {actor.Motor.SetMove(Mathf.Sign(swordSettleTarget.x-actor.Position.x),.65f);return;}
            swordSettleUntil=0;
            // A sliver of lower-platform support can report Grounded while the upper platform
            // overlaps the torso. Settle onto the actual support before requiring target vision.
            // Otherwise HoldHorizontal + failed attack routes form an endless idle state.
            if(actor.Motor.Grounded&&!actor.Navigation.CanStand(actor.Position,actor.Motor.Size)
                &&actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var support))
            {
                float margin=actor.Motor.Size.x*.5f+.12f;
                Vector2 settle=new Vector2(Mathf.Clamp(actor.Position.x,support.bounds.min.x+margin,support.bounds.max.x-margin),actor.Position.y);
                if(Vector2.Distance(settle,actor.Position)>.05f&&actor.Navigation.CanStand(settle,actor.Motor.Size)
                    &&actor.Navigation.TryDash(actor.Position,settle,actor.Motor.Size,out _))
                {
                    SwordSupportSettles++;swordSettleTarget=settle;swordSettleUntil=Time.time+.7f;
                    actor.Motor.BeginStep(Vector2.Distance(settle,actor.Position));
                    actor.Motor.SetMove(Mathf.Sign(settle.x-actor.Position.x),.65f);return;
                }
            }
            // A platform is held until a complete useful departure has been selected.
            if(!actor.Motor.Grounded)
            {
                if(Vector2.Distance(actor.Position,swordLastProgress)>.08f)
                {swordLastProgress=actor.Position;swordLastProgressAt=Time.time;}
                // A wedged airborne body may have neither support nor downward progress. Use real
                // horizontal movement toward a standable nearby ledge, never teleport or jump-reset.
                if(Time.time-swordLastProgressAt>.4f)
                {
                    float best=float.PositiveInfinity;Vector2 exit=actor.Position;
                    foreach(var node in actor.Navigation.Nodes)
                    {
                        var point=node.Center(actor.Motor.Size);float distance=Vector2.Distance(point,actor.Position);
                        if(distance<best&&point.y<=actor.Position.y+.3f&&actor.Navigation.CanStand(point,actor.Motor.Size))
                        {best=distance;exit=point;}
                    }
                    actor.Motor.SetMove(Mathf.Sign(exit.x-actor.Position.x));
                }
                return;
            }
            if(actor.Perception.Facts.Confidence<.2f){actor.Tactics.SearchSwordContact();return;}
            actor.Tactics.SwordContactRecovered();
            swordLastProgress=actor.Position;swordLastProgressAt=Time.time;actor.Motor.HoldHorizontal();
            if(Time.time<swordDecisionAt)return;swordDecisionAt=Time.time+.13f;
            var facts=actor.Perception.Facts;
            if(facts.Confidence<.2f)return;
            if(Ready(BossAbility.Guard)&&actor.Tactics.GuardOpportunity(actor.Position,facts,out _)
                &&TryStart(BossAbility.Guard,facts.Position))return;
            // Only cache an intent, not a stale route. TryStartSword builds its movement anew.
            bool previewFresh=swordPreview!=BossAbility.None&&Time.time-swordPreviewedAt<.4f
                &&Vector2.Distance(swordPreviewOrigin,actor.Position)<.25f
                &&Vector2.Distance(swordPreviewTarget,facts.Position)<.4f&&Ready(swordPreview)&&swordPlanner.StillUseful(swordPreview);
            BossAbility choice=previewFresh?swordPreview:swordPlanner.Select();
            swordPreview=BossAbility.None;
            if(choice!=BossAbility.None)TryStartSword(choice);
        }
        public bool TryStartSword(BossAbility ability)
        {
            if(!IsSwordPhrase(ability)||p.Kind!=BossKind.Severance||!actor.Running||!actor.IsAlive||Busy
                ||!Ready(ability)||!actor.Motor.Grounded||actor.Perception.Facts.Confidence<.2f)return false;
            if(ability==BossAbility.SwordRedline&&(!SwordUnbound||HasLivingPartner))return false;
            if(!swordPlanner.StillUseful(ability))return false;
            if(actor.Coordinator!=null&&!actor.Coordinator.TryReserve(actor,ability,false))return false;
            Ability=ability;settings=p.DrawDash;Started[(int)ability]++;AttackId++;
            hitsAtStart=ConnectedHits;comboTarget=false;RecoveryStepping=false;InterruptedCurrent=false;
            swordPhrase.Clear();swordIndex=-1;swordAirLink=swordAirPermission=false;swordRecoveryStarted=-1;
            swordResidualMoving=swordResidualStepTried=false;swordPreview=BossAbility.None;
            swordTravelJumpPlanned=false;swordTravelBeats=0;
            swordChaseLinked=false;swordLandingFlowUntil=0;
            swordPhraseEchoStart=swordHazards.Echoes;
            swordDeadline=Time.time+(ability==BossAbility.SwordRedline?9:7);
            switch(ability)
            {
                case BossAbility.SwordPursuit:swordPhrase.Add(SwordBeat.Draw);swordPhrase.Add(SwordBeat.RisingCut);swordPhrase.Add(SwordBeat.DownCut);break;
                case BossAbility.SwordDislodge:swordPhrase.Add(SwordBeat.Wave);swordPhrase.Add(SwordBeat.Traverse);swordPhrase.Add(SwordBeat.Draw);break;
                case BossAbility.SwordDuel:swordPhrase.Add(SwordBeat.LowCut);swordPhrase.Add(SwordBeat.RisingCut);if(SwordUnbound)swordPhrase.Add(SwordBeat.LowCut);swordPhrase.Add(SwordBeat.DownCut);break;
                case BossAbility.SwordCrossing:swordPhrase.Add(SwordBeat.Traverse);swordPhrase.Add(SwordBeat.RisingCut);swordPhrase.Add(SwordBeat.DownCut);break;
                case BossAbility.SwordRedline:swordPhrase.Add(SwordBeat.Draw);swordPhrase.Add(SwordBeat.Traverse);swordPhrase.Add(SwordBeat.Draw);swordPhrase.Add(SwordBeat.DownCut);swordPhrase.Add(SwordBeat.Replay);break;
            }
            readyAt[(int)ability]=Time.time+(ability==BossAbility.SwordRedline?p.Sword.FinaleCooldown:ability==BossAbility.SwordDislodge?3.2f:1.4f);
            swordPlanner.Accepted(ability);Guarded=0;actor.Perception.ExpressedPressure(actor.Position);
            swordPreviousPlayer=actor.Player.GetComponent<Collider2D>().bounds.center;
            actor.Motor.HoldHorizontal();NextSwordBeat();return true;
        }
        void NextSwordBeat()
        {
            actor.Motor.EndAttackMotion();
            // Skip illegal authored links, never loop or search a new phrase inside this one.
            while(++swordIndex<swordPhrase.Count)
            {
                var beat=swordPhrase[swordIndex];swordTravel=null;
                // The lane wave already asks for a jump. A visibly airborne response changes the
                // close follow-up to a descending cut, which is answered by lateral movement.
                // If that cut is out of reach the player earned separation; retire this phrase.
                if(beat==SwordBeat.Draw&&Ability==BossAbility.SwordDislodge&&!actor.Perception.Facts.Grounded)
                    beat=SwordBeat.DownCut;
                if(beat==SwordBeat.Traverse)
                {
                    SwordBeat followup=Ability==BossAbility.SwordCrossing?SwordBeat.RisingCut:SwordBeat.Draw;
                    if(!actor.Motor.Grounded||!swordPlanner.TryTravel(out swordTravel,followup,
                        Ability==BossAbility.SwordCrossing,Ability==BossAbility.SwordRedline,
                        Ability==BossAbility.SwordCrossing&&swordTravelBeats<2&&!swordTravelJumpPlanned))continue;
                    swordTravelBeats++;swordTravelJumpPlanned|=swordTravel.Jump;
                    if(swordTravel.Setup)
                        swordPhrase.Insert(swordIndex+1,SwordBeat.Traverse);
                    else if(Ability==BossAbility.SwordCrossing&&swordTravel.Followup==SwordBeat.Draw)
                        swordPhrase[swordIndex+1]=SwordBeat.Draw;
                    if(swordTravel.AttackJump){beat=SwordBeat.JumpCut;swordIndex++;}
                }
                else if(beat==SwordBeat.Draw)
                {
                    if(!actor.Motor.Grounded&&!swordAirPermission)continue;
                    if(!swordPlanner.TryDraw(actor.Position,swordPlanner.DrawTarget(p.Sword.DrawPrepare+p.Sword.DrawLock),out _,out _))continue;
                }
                else if(beat==SwordBeat.Replay)
                {swordHazards.Replay();continue;}
                else if(beat!=SwordBeat.Wave&&(!actor.Motor.Grounded||swordPlanner.CloseBeat(beat==SwordBeat.DownCut)==SwordBeat.None))
                {
                    // One paid chase link replaces a now-out-of-range cut. Do not retire every
                    // landing follow-up merely because the player kept running during the jump.
                    if(swordChaseLinked||!actor.Motor.Grounded||actor.Perception.Facts.Confidence<.2f
                        ||!swordPlanner.TryDraw(actor.Position,swordPlanner.DrawTarget(p.Sword.DrawPrepare+p.Sword.DrawLock),out _,out _))continue;
                    beat=SwordBeat.Draw;swordChaseLinked=true;SwordChaseLinks++;
                }
                CurrentSwordBeat=beat;hitInWindow=false;previousCutProgress=0;swordBeatId=++AttackId;
                swordJumpLaunch=-1;swordJumpStruck=false;swordCutSeeded=false;swordCutEnd=actor.Position;
                swordFacing=actor.Facing;swordDrawStart=actor.Position;
                swordArcFrom=beat==SwordBeat.JumpCut?35:beat==SwordBeat.DownCut?95:beat==SwordBeat.RisingCut?-75:-12;
                swordArcTo=beat==SwordBeat.JumpCut?-95:beat==SwordBeat.DownCut?-75:beat==SwordBeat.RisingCut?65:18;
                float preparation=beat==SwordBeat.Wave?p.Sword.WavePrepare:beat==SwordBeat.Traverse?.18f:
                    beat==SwordBeat.JumpCut?p.Sword.JumpPrepare:beat==SwordBeat.DownCut?p.Sword.FinisherPrepare:
                    beat==SwordBeat.Draw?p.Sword.DrawPrepare:p.Sword.CutPrepare;
                if(Time.time<swordLandingFlowUntil&&GroundSwordCut(beat))preparation=Mathf.Min(preparation,.12f);
                SetPhase(BossActionPhase.Prepare,preparation);
                actor.Sounds?.Play(Sfx.DrawPrepare,actor.ActorId,AttackId);UpdateSwordGeometry();PublishSword();return;
            }
            RecoverSword();
        }
        static bool GroundSwordCut(SwordBeat beat)=>beat==SwordBeat.LowCut||beat==SwordBeat.RisingCut||beat==SwordBeat.DownCut;
        float SwordLock=>CurrentSwordBeat==SwordBeat.Wave?p.Sword.WaveLock:CurrentSwordBeat==SwordBeat.Traverse?.1f:
            CurrentSwordBeat==SwordBeat.JumpCut?p.Sword.JumpLock:GroundSwordCut(CurrentSwordBeat)?p.Sword.CutLock:p.Sword.DrawLock;
        void TickSwordPhrase()
        {
            if(Time.time>swordDeadline&&Phase!=BossActionPhase.Recovery)
            {SwordAbortedTravels++;swordHazards.Replay();RecoverSword();}
            if(Phase==BossActionPhase.Prepare)
            {
                UpdateSwordGeometry();
                if(Time.time>=phaseUntil)
                {
                    // This is the final targeting boundary. Later movement cannot turn the attack.
                    actor.Face(Aim);swordFacing=CurrentSwordBeat==SwordBeat.JumpCut?swordTravel.Facing:actor.Facing;SetPhase(BossActionPhase.Locked,SwordLock);
                    if(GroundSwordCut(CurrentSwordBeat))swordCutEnd=PlanSwordCutStep();
                    UpdateSwordThreat(Time.time+SwordLock);
                    if(HasLivingPartner&&(PendingSwordThreats||CurrentSwordBeat==SwordBeat.JumpCut)&&CurrentSwordBeat!=SwordBeat.Traverse)
                    {
                        swordSafety.Clear();swordHazards.AppendScheduled(swordSafety);swordSafety.Add(swordThreat);
                        if(!SwordEscape.HasRoute(actor,swordSafety,Mathf.Min(2.5f,swordThreat.EndsAt-Time.time+.2f)))
                        {if(CurrentSwordBeat==SwordBeat.JumpCut)swordPlanner.Failed(swordTravel.Destination);NextSwordBeat();}
                    }
                }
            }
            else if(Phase==BossActionPhase.Locked&&Time.time>=phaseUntil)ActivateSwordBeat();
            else if(Phase==BossActionPhase.Active)
            {
                if(CurrentSwordBeat==SwordBeat.JumpCut)
                {
                    if(swordJumpLaunch<0&&actor.Motor.JumpStartedAt>=phaseAt)
                    {swordJumpLaunch=actor.Motor.JumpStartedAt;swordStrikeAt=swordJumpLaunch+swordTravel.StrikeOffset;}
                    if(!swordJumpStruck&&swordJumpLaunch>=0&&Time.time>=swordStrikeAt
                        &&Time.time<=swordStrikeAt+p.Sword.JumpCutActive&&(actor.Motor.Grounded||actor.Velocity.y<0))
                    {swordJumpStruck=true;SwordJumpCuts++;actor.Sounds?.Play(Sfx.BladeHeavy,actor.ActorId,AttackId);}
                    if(swordJumpStruck&&Time.time>=swordStrikeAt+p.Sword.JumpCutActive)SeedSwordCut();
                    if(actor.Motor.RouteFailed||Time.time>swordTravelDeadline
                        ||actor.Motor.RouteCompleted&&!actor.Motor.HasRoute&&swordJumpLaunch>=0&&Time.time>=swordStrikeAt+p.Sword.JumpCutActive)
                    {
                        if(actor.Motor.RouteFailed||Time.time>swordTravelDeadline)SwordAbortedTravels++;
                        else swordPlanner.Landed(swordTravel);
                        if(!hitInWindow)swordPlanner.Failed(swordTravel.Destination);
                        if(actor.Motor.Grounded)swordLandingFlowUntil=Time.time+.35f;
                        NextSwordBeat();
                    }
                }
                else if(CurrentSwordBeat==SwordBeat.Traverse)
                {
                    if(Ability==BossAbility.SwordRedline&&!swordAirLink&&swordTravel.Jump&&!actor.Motor.Grounded
                        &&actor.Velocity.y<=6&&actor.Motor.JumpStartedAt>=phaseAt
                        &&Time.time-actor.Motor.JumpStartedAt>=swordTravel.StrikeOffset
                        &&swordPlanner.TryDraw(actor.Position,swordPlanner.DrawTarget(p.Sword.DrawPrepare+p.Sword.DrawLock),out _,out _))
                    {swordAirLink=swordAirPermission=true;NextSwordBeat();}
                    else if(actor.Motor.RouteFailed||Time.time>swordTravelDeadline)
                    {SwordAbortedTravels++;swordPlanner.Failed(swordTravel.Destination);NextSwordBeat();}
                    else if(swordTravel.Jump?actor.Motor.RouteCompleted&&!actor.Motor.HasRoute:swordTravel.Walk?
                        Mathf.Abs(actor.Position.x-swordTravel.Destination.x)<.1f:!actor.Motor.DashActive)
                    {swordPlanner.Landed(swordTravel);if(swordTravel.Jump)swordLandingFlowUntil=Time.time+.35f;NextSwordBeat();}
                    else if(swordTravel.Walk)actor.Motor.SetMove(Mathf.Sign(swordTravel.Destination.x-actor.Position.x));
                }
                else if(CurrentSwordBeat==SwordBeat.Draw)
                {
                    if(!actor.Motor.DashActive||Time.time>=phaseUntil)
                    {
                        Vector2 lead=SwordDrawLead;
                        if(swordHazards.Echoes==swordPhraseEchoStart)
                            swordHazards.Echo(swordDrawStart+lead,actor.Position+lead,Ability==BossAbility.SwordRedline);
                        NextSwordBeat();
                    }
                }
                else if(Time.time>=phaseUntil){if(GroundSwordCut(CurrentSwordBeat))SeedSwordCut();NextSwordBeat();}
            }
            else if(Phase==BossActionPhase.Recovery)
            {
                PreviewSwordIntent();
                // The actor's bounded recovery runs alongside the delayed blades. Waiting for
                // every blade's expiry before starting recovery made each attack stop the chase.
                if(Time.time>=phaseUntil){SwordPhrasesCompleted++;CurrentSwordBeat=SwordBeat.None;swordDecisionAt=Time.time+.04f;Finish();return;}
            }
            PublishSword();
        }
        void UpdateSwordGeometry()
        {
            float delay=Mathf.Max(0,phaseUntil-Time.time)+SwordLock;
            if(CurrentSwordBeat==SwordBeat.JumpCut)
            {
                // Full takeoff tell reveals the already-selected landing, swing side and time.
                targetPoint=swordTravel.Target;swordFacing=swordTravel.Facing;
                Aim=Vector2.right*swordFacing;actor.Face(Aim);Origin=actor.Position;End=swordTravel.Destination;
                UpdateSwordThreat(Time.time+delay);return;
            }
            targetPoint=swordPlanner.Target(delay);
            if(CurrentSwordBeat==SwordBeat.Wave)
            {
                Origin=actor.Position+Vector2.up*(p.Sword.WaveLength*.5f+.08f-actor.Motor.Size.y*.5f);
                targetPoint+=Vector2.up*(p.Sword.WaveLength*.5f+.08f-actor.Perception.Facts.Body.extents.y);
                Aim=(targetPoint-Origin).normalized;End=Origin+Aim*p.Sword.WaveSpeed*p.Sword.WaveLife;
            }
            else if(CurrentSwordBeat==SwordBeat.Draw)
            {
                targetPoint=swordPlanner.DrawTarget(delay);
                Origin=actor.Position;swordPlanner.TryDraw(Origin,targetPoint,out var end,out var direction);End=end;Aim=direction;
            }
            else {Aim=(targetPoint-actor.Position).normalized;Origin=SwordBladeRoot;End=SwordBladeTip;}
            if(Aim.sqrMagnitude<.01f)Aim=Vector2.right*actor.Facing;
            actor.Face(Aim);swordFacing=actor.Facing;
            UpdateSwordThreat(Time.time+delay);
        }
        void UpdateSwordThreat(float starts)
        {
            swordThreat=new BossThreat {ActorId=actor.ActorId,AttackId=swordBeatId,Valid=true,
                Phase=Phase==BossActionPhase.Prepare?BossThreatPhase.Preparation:BossThreatPhase.Locked,
                Origin=Origin,End=End,Direction=Aim,Radius=p.Sword.CutRadius,StartsAt=starts,EndsAt=starts+.22f,TerrainBlocked=true};
            if(CurrentSwordBeat==SwordBeat.Draw)
            {
                swordThreat.Shape=BossThreatShape.SweptCapsule;swordThreat.Radius=.48f;
                Vector2 lead=SwordDrawLead;
                swordThreat.Origin+=lead;swordThreat.End+=lead;
                swordThreat.EndsAt=starts+Vector2.Distance(Origin,End)/p.Sword.AdvanceSpeed;
            }
            else if(CurrentSwordBeat==SwordBeat.Wave)
            {
                swordThreat.Shape=BossThreatShape.MovingBlade;swordThreat.TerrainBlocked=false;
                Vector2 side=new Vector2(-Aim.y,Aim.x)*p.Sword.WaveLength*.5f;
                swordThreat.Origin=Origin-side;swordThreat.End=Origin+side;swordThreat.Radius=.09f;
                swordThreat.ProjectileSpeed=p.Sword.WaveSpeed;swordThreat.EndsAt=starts+p.Sword.WaveLife;
            }
            else if(CurrentSwordBeat==SwordBeat.JumpCut)
            {
                float launch=Phase==BossActionPhase.Active?(swordJumpLaunch>=0?swordJumpLaunch:phaseAt+Time.fixedDeltaTime):starts;
                swordThreat.Shape=BossThreatShape.BladeArc;swordThreat.ArcFrom=swordArcFrom;swordThreat.ArcTo=swordArcTo;
                swordThreat.Facing=swordFacing;swordThreat.Reach=p.Sword.CutReach;
                swordThreat.StartsAt=launch+swordTravel.StrikeOffset;swordThreat.EndsAt=swordThreat.StartsAt+p.Sword.JumpCutActive;
                var path=swordTravel.Path;swordThreat.PathStartsAt=launch;swordThreat.PathStep=Time.fixedDeltaTime;
                if(Phase==BossActionPhase.Active&&swordJumpLaunch>=0)
                {
                    swordThreat.PathStartsAt=Time.time;
                    path=actor.Navigation.PredictAirPath(actor.Position,actor.Velocity,swordTravel.Destination,
                        swordTravel.Motion,actor.Motor.Size,Mathf.Max(0,swordThreat.EndsAt-Time.time));
                }
                // The path contains body centres; the blade is rooted slightly above them.
                swordThreat.OriginPath=System.Array.ConvertAll(path,v=>v+Vector2.up*.15f);
                swordThreat.Origin=swordThreat.OriginAt(swordThreat.StartsAt);swordThreat.End=swordThreat.Origin+SwordCutDirection(0)*p.Sword.CutReach;
            }
            else if(CurrentSwordBeat==SwordBeat.Traverse)swordThreat.Valid=false;
            else
            {
                swordThreat.Shape=BossThreatShape.BladeArc;swordThreat.Origin=SwordBladeRoot;
                swordThreat.ArcFrom=swordArcFrom;swordThreat.ArcTo=swordArcTo;swordThreat.Facing=swordFacing;
                swordThreat.Reach=p.Sword.CutReach;swordThreat.EndsAt=starts+(CurrentSwordBeat==SwordBeat.DownCut?.24f:.18f);
                Vector2 end=Phase==BossActionPhase.Prepare?PlanSwordCutStep():swordCutEnd;
                swordThreat.OriginPath=new[]{SwordBladeRoot,SwordBladeRoot+end-actor.Position};
                swordThreat.PathStartsAt=starts;swordThreat.PathStep=Mathf.Max(Time.fixedDeltaTime,Vector2.Distance(end,actor.Position)/p.Sword.CutStepSpeed);
            }
        }
        Vector2 PlanSwordCutStep()
        {
            Vector2 from=actor.Position;if(!actor.Motor.Grounded)return from;
            float gap=(targetPoint.x-from.x)*swordFacing;
            float distance=Mathf.Clamp(gap-p.Sword.CutReach*.55f,0,p.Sword.CutAdvance);
            Vector2 end=from+Vector2.right*swordFacing*distance;
            if(!actor.Navigation.TrySupport(from,actor.Motor.Size,out var source)
                ||!actor.Navigation.TrySupport(end,actor.Motor.Size,out var destination)||source!=destination
                ||!actor.Navigation.CanStand(end,actor.Motor.Size))return from;
            actor.Navigation.TryDash(from,end,actor.Motor.Size,out var clear);return clear;
        }
        void SeedSwordCut()
        {
            if(swordCutSeeded)return;swordCutSeeded=true;
            // Count successful emissions for the whole phrase, not currently surviving blades.
            // Expiry or an ally block never grants another seed within this phrase.
            if(swordHazards.Echoes!=swordPhraseEchoStart)return;
            Vector2 direction=new Vector2(swordFacing*Mathf.Max(.6f,Mathf.Abs(Aim.x)),Aim.y*.55f).normalized;
            swordHazards.Cut(SwordBladeRoot,direction,Ability==BossAbility.SwordRedline);
        }
        void ActivateSwordBeat()
        {
            Activated[(int)Ability]++;LastActiveAt=Time.time;hitInWindow=false;previousCutProgress=0;
            if(CurrentSwordBeat==SwordBeat.Traverse||CurrentSwordBeat==SwordBeat.JumpCut)
            {
                bool started;
                if(!swordTravel.Jump)actor.Motor.SetRoute(null);
                if(swordTravel.Jump)started=actor.Motor.StartCommittedJump(swordTravel.Route,swordTravel.Motion);
                else if(swordTravel.Walk)
                {actor.Motor.BeginStep(Mathf.Abs(swordTravel.Destination.x-actor.Position.x));actor.Motor.SetMove(Mathf.Sign(swordTravel.Destination.x-actor.Position.x));SwordWalks++;started=true;}
                else {actor.Motor.StartDash(swordTravel.Destination-actor.Position,Vector2.Distance(swordTravel.Destination,actor.Position),p.Sword.TravelSpeed);started=actor.Motor.DashActive;if(started)SwordTravelDashes++;}
                if(!started){SwordAbortedTravels++;swordPlanner.Failed(swordTravel.Destination);NextSwordBeat();return;}
                if(swordTravel.Jump&&swordTravel.PlatformChange)SwordPlatformJumps++;
                swordTravelDeadline=Time.time+swordTravel.Duration+.65f;SetPhase(BossActionPhase.Active,swordTravel.Duration+.65f);
                if(CurrentSwordBeat==SwordBeat.JumpCut){swordStrikeAt=Time.time+Time.fixedDeltaTime+swordTravel.StrikeOffset;UpdateSwordThreat(Time.time+Time.fixedDeltaTime);}
                return;
            }
            if(CurrentSwordBeat==SwordBeat.Draw)
            {
                if(!actor.Motor.Grounded&&!swordAirPermission){NextSwordBeat();return;}
                bool aerial=swordAirPermission&&!actor.Motor.Grounded;swordAirPermission=false;Origin=actor.Position;swordDrawStart=Origin;
                // Clip the locked corridor from the physical launch point; never reacquire the target.
                float length=Mathf.Max(0,Vector2.Dot(End-Origin,Aim));
                actor.Navigation.TryDash(Origin,Origin+Aim*length,actor.Motor.Size,out var end);End=end;
                if(Vector2.Distance(Origin,End)<.2f){NextSwordBeat();return;}
                if(aerial)SwordAirLinks++;
                actor.Motor.StartDash(Aim,Vector2.Distance(Origin,End),p.Sword.AdvanceSpeed);
                SetPhase(BossActionPhase.Active,Vector2.Distance(Origin,End)/p.Sword.AdvanceSpeed+.24f);
            }
            else if(CurrentSwordBeat==SwordBeat.Wave)
            {swordHazards.Wave(Origin,Aim);SetPhase(BossActionPhase.Active,.12f);}
            else
            {
                if(!actor.Motor.Grounded){NextSwordBeat();return;}
                actor.Navigation.TryDash(actor.Position,swordCutEnd,actor.Motor.Size,out swordCutEnd);
                if(Vector2.Distance(actor.Position,swordCutEnd)>.05f)
                {actor.Motor.StartDash(swordCutEnd-actor.Position,Vector2.Distance(actor.Position,swordCutEnd),p.Sword.CutStepSpeed);SwordCutSteps++;}
                SetPhase(BossActionPhase.Active,CurrentSwordBeat==SwordBeat.DownCut?.24f:.18f);
            }
            UpdateSwordThreat(Time.time);swordThreat.Phase=BossThreatPhase.Active;
            swordBladePrevious=SwordBladeTip;
            actor.Sounds?.Play(CurrentSwordBeat==SwordBeat.Draw?Sfx.DashCut:CurrentSwordBeat==SwordBeat.DownCut?Sfx.BladeHeavy:Sfx.BladeLight,actor.ActorId,AttackId);
        }
        Vector2 SwordCutDirection(float progress)
        {float angle=Mathf.Lerp(swordArcFrom,swordArcTo,progress)*Mathf.Deg2Rad;return new Vector2(swordFacing*Mathf.Cos(angle),Mathf.Sin(angle));}
        void SweepSwordBeat()
        {
            if(actor.Player==null)return;
            var playerBody=actor.Player.GetComponent<Collider2D>();
            Vector2 currentPlayer=playerBody.bounds.center,previousPlayer=swordPreviousPlayer;swordPreviousPlayer=currentPlayer;
            if(Phase!=BossActionPhase.Active)return;
            if(CurrentSwordBeat==SwordBeat.JumpCut&&(swordJumpLaunch<0||Time.time<swordStrikeAt||Time.time>swordStrikeAt+p.Sword.JumpCutActive
                ||!actor.Motor.Grounded&&actor.Velocity.y>=0))return;
            if(CurrentSwordBeat==SwordBeat.Draw)
            {
                Vector2 lead=SwordDrawLead;
                // Both bodies moved during the completed physics step. Testing only the player's
                // final centre misses a retreating body which the solid motor just stopped behind.
                var sweep=new BossThreat {Valid=true,Phase=BossThreatPhase.Active,Shape=BossThreatShape.SweptCapsule,
                    Origin=actor.Motor.PreviousPosition+lead,End=actor.Position+lead,Radius=.48f,TerrainBlocked=true,
                    StartsAt=Time.time-Time.fixedDeltaTime,EndsAt=Time.time};
                if(!hitInWindow&&sweep.IntersectsMotion(previousPlayer,currentPlayer,playerBody.bounds.extents,sweep.StartsAt,sweep.EndsAt))
                {
                    hitInWindow=true;int before=actor.Player.CurrentHealth;actor.Player.TakeDamage(p.Sword.DrawDamage,actor.gameObject);
                    if(actor.Player.CurrentHealth<before){ConnectedHits++;actor.View?.Impact(playerBody.ClosestPoint(actor.Position+lead));}
                }
            }
            else if(CurrentSwordBeat==SwordBeat.LowCut||CurrentSwordBeat==SwordBeat.RisingCut||CurrentSwordBeat==SwordBeat.DownCut||CurrentSwordBeat==SwordBeat.JumpCut)
            {
                int damage=CurrentSwordBeat==SwordBeat.DownCut||CurrentSwordBeat==SwordBeat.JumpCut?p.Sword.FinisherDamage:p.Sword.CutDamage;
                if(CurrentSwordBeat==SwordBeat.JumpCut&&previousCutProgress==0)swordBladePrevious=SwordBladeRoot+SwordCutDirection(0)*p.Sword.CutReach;
                for(int i=0;i<=4;i++)
                {
                    Vector2 tip=SwordBladeRoot+SwordCutDirection(Mathf.Lerp(previousCutProgress,SwordCutProgress,i/4f))*p.Sword.CutReach;
                    HitSegment(SwordBladeRoot,tip,p.Sword.CutRadius,damage,true);
                    HitSegment(swordBladePrevious,tip,p.Sword.CutRadius,damage,true);swordBladePrevious=tip;
                }
                previousCutProgress=SwordCutProgress;
            }
        }
        void RecoverSword()
        {
            swordHazards.Replay();actor.Motor.EndAttackMotion();CurrentSwordBeat=SwordBeat.None;
            SetPhase(BossActionPhase.Recovery,Ability==BossAbility.SwordRedline?p.Sword.FinaleRecovery:p.Sword.Recovery);
            swordRecoveryStarted=Time.time;swordThreat.Valid=false;
            swordResidualStepTried=swordResidualMoving=false;swordPreviewAt=0;
        }
        void PublishSword()
        {
            PublicThreats.Clear();swordHazards.Publish(PublicThreats);
            if(CurrentSwordBeat==SwordBeat.JumpCut&&Phase==BossActionPhase.Active)
            {
                UpdateSwordThreat(phaseAt);
                swordThreat.Phase=Time.time>=swordThreat.StartsAt?BossThreatPhase.Active:BossThreatPhase.Locked;
                if(Time.time>swordThreat.EndsAt)swordThreat.Valid=false;
            }
            if(Phase!=BossActionPhase.Recovery&&swordThreat.Valid&&!(CurrentSwordBeat==SwordBeat.Wave&&Phase==BossActionPhase.Active))
                PublicThreats.Add(swordThreat);
        }
        void StopSword()
        {
            swordHazards?.Clear();swordPhrase.Clear();swordTravel=null;CurrentSwordBeat=SwordBeat.None;
            swordThreat.Valid=false;swordAirLink=swordAirPermission=false;swordRecoveryStarted=-1;
            swordSettleUntil=0;
            swordPreview=BossAbility.None;swordResidualMoving=swordResidualStepTried=false;swordJumpLaunch=-1;
            swordTravelJumpPlanned=false;swordTravelBeats=0;swordPlanner?.ResetTravelMemory();
            swordChaseLinked=swordCutSeeded=false;swordLandingFlowUntil=0;
            swordPhraseEchoStart=0;
            actor?.Motor?.EndAttackMotion();
        }

        void PreviewSwordIntent()
        {
            if(Time.time<swordPreviewAt)return;swordPreviewAt=Time.time+.18f;
            swordPreview=swordPlanner.Select();
            swordPreviewOrigin=actor.Position;swordPreviewTarget=actor.Perception.Facts.Position;swordPreviewedAt=Time.time;
        }
        void TickSwordResidualStep()
        {
            if(swordResidualMoving)
            {
                if(!actor.Motor.Grounded||Mathf.Abs(actor.Position.x-swordResidualEnd.x)<.08f)
                {actor.Motor.HoldHorizontal();swordResidualMoving=false;return;}
                actor.Motor.SetMove(Mathf.Sign(swordResidualEnd.x-actor.Position.x),.35f);return;
            }
            if(swordResidualStepTried||!actor.Motor.Grounded)return;swordResidualStepTried=true;
            if(!actor.Navigation.TrySupport(actor.Position,actor.Motor.Size,out var support))return;
            Vector2 target=swordPlanner.Target(.65f),from=actor.Position;
            float side=Mathf.Sign(target.x-from.x);if(side==0)side=actor.Facing;
            float desired=target.x-side*3.1f,margin=actor.Motor.Size.x*.5f+.12f;
            var end=new Vector2(Mathf.Clamp(from.x+Mathf.Clamp(desired-from.x,-.85f,.85f),support.bounds.min.x+margin,support.bounds.max.x-margin),from.y);
            float distance=Vector2.Distance(from,end);
            if(distance<.18f||!actor.Navigation.CanStand(end,actor.Motor.Size)
                ||!actor.Navigation.TryDash(from,end,actor.Motor.Size,out var clear)||Vector2.Distance(clear,end)>.05f
                ||actor.Coordinator!=null&&actor.Coordinator.Occupied(actor,end))return;
            swordSafety.Clear();swordHazards.AppendScheduled(swordSafety);
            float duration=distance/(actor.Profile.Motion.Speed*.35f)+.12f;
            if(!SwordEscape.HasRoute(actor,swordSafety,2.5f,from,end,duration))return;
            swordResidualEnd=end;swordResidualMoving=true;SwordResidualSteps++;
            actor.Motor.BeginStep(distance);actor.Motor.SetMove(Mathf.Sign(end.x-from.x),.35f);
        }
    }
}
