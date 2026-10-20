using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Per-fight controller. Emits input only; the retained executors accept actions and spend resources.
    public sealed partial class ConditionalController : IDisposable
    {
        public struct TriggerContext
        {
            public long Occurrence;
            public int Candidates, Source;
            public bool HasPosition, EnemyBound, IsThreat;
            public int ThreatKind, PreparationSource, PreparationId;
            public Vector2 Position;
            public float ExpiresAt;
        }
        sealed class RuleState
        {
            public bool WasTrue, Consumed;
            public long Occurrence;
            public readonly HashSet<long> ConsumedEvents = new HashSet<long>();
            public int RangeMode;
            public bool CadencePending;
            public TriggerContext CadenceContext;
            public bool PlatformFailed;
            public Vector2 PlatformFailurePosition;
            public int PlatformFailureVersion;
            public readonly Dictionary<int,int> Preparations = new Dictionary<int,int>();
        }
        public readonly struct ExecutionFeedback
        {
            public readonly bool Executing;
            public readonly float LastExecutedAt;
            public readonly ConditionalVisualFeedback Visual;
            public ExecutionFeedback(bool executing,float at,ConditionalVisualFeedback visual=default) {Executing=executing;LastExecutedAt=at;Visual=visual;}
            public float Glow => Executing?1:Mathf.Clamp01((LastExecutedAt+.5f-Time.time)/.15f);
        }
        sealed class Running
        {
            public int Row=-1, Target=-1, Shots, ShotLimit;
            public float Started, Deadline, Direction, Destination, PreviousX, Stuck;
            public Vector2 Aim;
            public bool Accepted, Completed;
            public TriggerContext Context, EventContext;
            public PlatformTransition Platform;
        }
        struct Notice
        {
            public int Condition;
            public float At;
            public TriggerContext Context;
        }
        readonly PlayerHealth player;
        readonly PlayerMovement movement;
        readonly PlayerShooting gun;
        readonly PlayerDodge dodge;
        readonly PlayerInputReader input;
        readonly Collider2D shape;
        readonly Rigidbody2D body;
        readonly IReadOnlyList<BossAgent> actors;
        readonly Collider2D[] platforms;
        readonly Bounds arena;
        readonly LegacyPlayerAI geometry;
        readonly ConditionalLoadout loadout;
        readonly RuleState[,] states=new RuleState[5,ConditionalCatalog.MaxRulesTotal];
        readonly bool[,] matches=new bool[5,ConditionalCatalog.MaxRulesTotal];
        readonly bool[,] visualMatches=new bool[5,ConditionalCatalog.MaxRulesTotal];
        readonly float[,] acceptedAt=new float[5,ConditionalCatalog.MaxRulesTotal];
        readonly int[,] acceptedFrame=new int[5,ConditionalCatalog.MaxRulesTotal];
        readonly ExecutionFeedback[,] pausedFeedback=new ExecutionFeedback[5,ConditionalCatalog.MaxRulesTotal];
        readonly ExecutionFeedback[,] displayedFeedback=new ExecutionFeedback[5,ConditionalCatalog.MaxRulesTotal];
        bool feedbackPaused;
        readonly TriggerContext[,] contexts=new TriggerContext[5,ConditionalCatalog.MaxRulesTotal];
        struct Fact
        {
            public bool Match;
            public TriggerContext Context;
        }
        readonly Dictionary<int,Fact> facts=new Dictionary<int,Fact>(24);
        readonly Running[] running={new Running(),new Running(),new Running(),new Running(),new Running()};
        readonly float[,] executedAt=new float[5,ConditionalCatalog.MaxRulesTotal];
        readonly int[,] executedFrame=new int[5,ConditionalCatalog.MaxRulesTotal];
        readonly List<Notice> events=new List<Notice>(16);
        readonly HashSet<long> liveEvents=new HashSet<long>();
        readonly Predicate<long> expiredEvent;
        readonly BossContact[] contacts;
        readonly bool[] visible,seen,hidden,lowEnemy,close,far,cursorNear;
        readonly RaycastHit2D[] hits=new RaycastHit2D[24];
        RawPlayerInput raw;
        ConditionalControlFrame output;
        long sequence, observedSequence;
        float lastResolvedAt=float.NegativeInfinity,previousResolvedAt;
        int lastResolvedFrame=-2,previousResolvedFrame;
        int allVisible,previousTarget=-1,currentTarget=-1;
        bool initialized,grounded,lowHealth,disposed;
        Vector2 previousTargetPosition;
        public ConditionalControlFrame Ownership => output;
        public int Starts { get; private set; }
        public int AcceptedJumps {get;private set;}
        public int AcceptedDodges {get;private set;}
        public int AutomaticShots {get;private set;}
        public int CurrentTargetId => currentTarget>=0?actors[currentTarget].ActorId:0;
        public bool IsLowHealth => lowHealth;
        public int RuleCount(ControlOperation operation)=>loadout.Operations[(int)operation].Rules.Count;
        public ConditionalRule RuleAt(ControlOperation operation,int row)=>loadout.Operations[(int)operation].Rules[row].Copy();
        public ExecutionFeedback Feedback(ControlOperation operation,int row)
        {
            if(disposed)return new ExecutionFeedback(false,float.NegativeInfinity,default);
            if(feedbackPaused)return pausedFeedback[(int)operation,row];
            int op=(int)operation;var r=running[op];
            bool active=!disposed&&r.Row==row&&r.Accepted;
            if(op==0)active&=input.ConditionalFrame.MovementOwned&&!dodge.IsDodging&&!movement.HitPushActive&&Time.time-executedAt[op,row]<.1f;
            else if(op==2)active&=input.ConditionalFrame.AimOwned&&executedFrame[op,row]==Time.frameCount;
            else if(op==3)active&=input.ConditionalFrame.FireOwned&&gun.isActiveAndEnabled
                &&(loadout.Operations[op].Rules[row].Action=="F06"||!gun.IsReloading&&gun.IsFiring);
            else if(op==4)active&=dodge.IsDodging;
            else if(op==1)active&=r.Platform!=null&&platformOwnsMovement&&!dodge.IsDodging&&!movement.HitPushActive;
            else active=false;
            var visual=new ConditionalVisualFeedback {Matched=visualMatches[op,row],Executing=active,
                AcceptedThisFrame=acceptedFrame[op,row]==Time.frameCount,
                Pulse=Mathf.Clamp01(1-(Time.time-acceptedAt[op,row])/.24f),
                Afterglow=active?0:Mathf.Clamp01(1-(Time.time-executedAt[op,row])/.3f)};
            var result=new ExecutionFeedback(active,executedAt[op,row],visual);
            displayedFeedback[op,row]=result;
            return result;
        }
        public ConditionalVisualFeedback VisualFeedback(ControlOperation operation)
        {
            var combined=new ConditionalVisualFeedback();
            for(int row=0;row<RuleCount(operation);row++)
            {
                var f=Feedback(operation,row).Visual;
                combined.Matched|=f.Matched;combined.Executing|=f.Executing;combined.AcceptedThisFrame|=f.AcceptedThisFrame;
                combined.Pulse=Mathf.Max(combined.Pulse,f.Pulse);combined.Afterglow=Mathf.Max(combined.Afterglow,f.Afterglow);
            }
            return combined;
        }
        public void SetFeedbackPaused(bool value)
        {
            if(value&&!feedbackPaused)for(int op=0;op<5;op++)for(int row=0;row<RuleCount((ControlOperation)op);row++)
                pausedFeedback[op,row]=displayedFeedback[op,row];
            feedbackPaused=value;
        }
        void Executed(int op,Running r)
        {
            executedAt[op,r.Row]=Time.time;executedFrame[op,r.Row]=Time.frameCount;
            r.Accepted=true;
        }
        void Consume(int op,Running r)
        {
            var rule=loadout.Operations[op].Rules[r.Row];var state=states[op,r.Row];
            if(r.Context.PreparationSource>=0&&r.Context.PreparationId!=0&&ConditionalCatalog.Response(rule.Action).Finite)
                state.Preparations[r.Context.PreparationSource]=r.Context.PreparationId;
            if(ConditionalCatalog.Response(rule.Action).Finite)state.Consumed=true;
            if(ConditionalCatalog.Condition(rule.Condition).Event)state.ConsumedEvents.Add(r.Context.Occurrence);
        }
        void Accept(int op,Running r)
        {
            if(!r.Accepted){acceptedAt[op,r.Row]=Time.time;acceptedFrame[op,r.Row]=Time.frameCount;}
            Executed(op,r);Consume(op,r);
        }
        public ConditionalController(PlayerHealth player,IReadOnlyList<BossAgent> actors,Collider2D[] platforms,Bounds arena,ConditionalLoadout snapshot)
        {
            if(!ConditionalCatalog.IsValid(snapshot))throw new ArgumentException("Invalid conditional loadout",nameof(snapshot));
            this.player=player;this.actors=actors;this.platforms=platforms;this.arena=arena;loadout=snapshot.Copy();
            expiredEvent=IsExpiredEvent;
            movement=player.GetComponent<PlayerMovement>();gun=player.GetComponent<PlayerShooting>();dodge=player.GetComponent<PlayerDodge>();
            input=player.GetComponent<PlayerInputReader>();shape=player.GetComponent<Collider2D>();body=player.GetComponent<Rigidbody2D>();
            geometry=new LegacyPlayerAI(player,actors,platforms);
            contacts=new BossContact[actors.Count];visible=new bool[actors.Count];seen=new bool[actors.Count];hidden=new bool[actors.Count];
            lowEnemy=new bool[actors.Count];close=new bool[actors.Count];far=new bool[actors.Count];cursorNear=new bool[actors.Count];
            for(int op=0;op<5;op++)for(int row=0;row<ConditionalCatalog.MaxRulesTotal;row++)
            {states[op,row]=new RuleState();acceptedAt[op,row]=executedAt[op,row]=float.NegativeInfinity;acceptedFrame[op,row]=executedFrame[op,row]=-1;
                displayedFeedback[op,row]=new ExecutionFeedback(false,float.NegativeInfinity,default);}
            gun.ReloadStarted+=ReloadStarted;gun.ReloadFinished+=ReloadFinished;gun.EnemyHit+=EnemyHit;gun.ShotFired+=Shot;
            player.DamageTaken+=Damaged;dodge.DodgeFinished+=DodgeFinished;input.ConditionalActionResolved+=ActionResolved;
            movement.JumpStarted+=JumpStarted;movement.JumpApexReached+=JumpApexReached;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            gun.ReloadStarted-=ReloadStarted;gun.ReloadFinished-=ReloadFinished;gun.EnemyHit-=EnemyHit;gun.ShotFired-=Shot;
            player.DamageTaken-=Damaged;dodge.DodgeFinished-=DodgeFinished;input.ConditionalActionResolved-=ActionResolved;
            movement.JumpStarted-=JumpStarted;movement.JumpApexReached-=JumpApexReached;
            events.Clear();facts.Clear();foreach(var response in running)response.Row=-1;output=default;
        }
        void ReloadStarted()
        {
            // Final-shot completion is recorded before the gun begins its automatic reload.
            // An unfinished burst must never resume from a later magazine, even after a fast reload.
            var r=running[(int)ControlOperation.Fire];
            if(r.Row>=0&&!r.Completed&&loadout.Operations[(int)ControlOperation.Fire].Rules[r.Row].Action=="F02")r.Row=-1;
        }
        void ReloadFinished()=>Record(11);
        void DodgeFinished()=>Record(7);
        void JumpStarted()=>Record(57);
        void JumpApexReached()=>Record(58);
        void Shot(Vector2 origin,Vector2 direction)
        {
            // The public fact includes manual shots; only the owning response receives auto credit.
            Record(59);
            var r=running[3];
            if(disposed||!input.ConditionalFrame.FireOwned||!input.ConditionalFrame.Fire||r.Row<0)return;
            string action=loadout.Operations[3].Rules[r.Row].Action;
            if(action=="F06"||r.Completed)return;
            if(action=="F02"&&!r.Accepted)
            {
                // ShotFired follows the real ammo spend: capture this magazine at actual acceptance.
                r.ShotLimit=gun.AmmoInMagazine+1;
                r.Deadline=Time.time+r.ShotLimit*gun.FireInterval+.5f;
            }
            AutomaticShots++;r.Shots++;acceptedAt[3,r.Row]=Time.time;acceptedFrame[3,r.Row]=Time.frameCount;Accept(3,r);
            if(action=="F02"&&r.Shots>=r.ShotLimit)
            {r.Completed=true;Record(60);}
        }
        int Index(GameObject source)
        {
            var actor=source!=null?source.GetComponentInParent<BossAgent>():null;
            if(actor==null)return -1;
            for(int i=0;i<actors.Count;i++)if(actors[i]==actor)return i;
            return -1;
        }
        void Damaged(int amount,GameObject source) {if(amount>0)Record(6,Index(source));}
        void EnemyHit(Enemy enemy,bool defeated)
        {
            if(enemy!=null)Record(13,Index(enemy.gameObject));
        }
        void Record(int condition,int source=-1,Vector2? position=null)
        {
            if(!initialized||disposed||player.IsDead||!input.GameplayInputEnabled||input.NarrativePaused)return;
            var ctx=new TriggerContext {Occurrence=++sequence,Source=source,Candidates=source>=0?1<<source:allVisible,EnemyBound=source>=0,PreparationSource=-1,
                ExpiresAt=Time.time+(condition==6?ConditionalDefaults.ResponseWindow:ConditionalDefaults.EventWindow)};
            if(position.HasValue){ctx.HasPosition=true;ctx.Position=position.Value;}
            // Damage/death identifies its source; only retain an already observed static body position.
            else if(source>=0&&visible[source]){ctx.HasPosition=true;ctx.Position=contacts[source].Bounds.center;}
            events.Add(new Notice {Condition=condition,At=Time.time,Context=ctx});
        }
        void Snapshot()
        {
            allVisible=0;
            for(int i=0;i<actors.Count;i++)
            {
                BossContact c=default;
                bool now=actors[i]!=null&&actors[i].TryGetContact(out c)&&c.Visible;
                if(now)
                {
                    contacts[i]=c;allVisible|=1<<i;

                    seen[i]=true;hidden[i]=false;
                    lowEnemy[i]=HysteresisLow(c.HealthFraction,lowEnemy[i]);
                    float distance=Mathf.Abs(c.Bounds.center.x-shape.bounds.center.x);
                    close[i]=distance<=(close[i]?ConditionalDefaults.Close+ConditionalDefaults.Hysteresis:ConditionalDefaults.Close);
                    far[i]=distance>=(far[i]?ConditionalDefaults.Far-ConditionalDefaults.Hysteresis:ConditionalDefaults.Far);
                    cursorNear[i]=MouseAngle(i)<=(cursorNear[i]?ConditionalDefaults.MouseExit:ConditionalDefaults.MouseEnter);
                }
                else if(seen[i])hidden[i]=true;
                visible[i]=now;
                if(!now)cursorNear[i]=false;
            }
            bool low=HysteresisLow((float)player.CurrentHealth/player.MaxHealth,lowHealth);
            if(initialized)
            {
                if(movement.IsGrounded&&!grounded)Record(3);
            }
            grounded=movement.IsGrounded;lowHealth=low;
            initialized=true;
            events.RemoveAll(n=>Time.time>n.Context.ExpiresAt);
            liveEvents.Clear();
            foreach(var notice in events)liveEvents.Add(notice.Context.Occurrence);
            for(int op=0;op<5;op++)for(int row=0;row<loadout.Operations[op].Rules.Count;row++)
                states[op,row].ConsumedEvents.RemoveWhere(expiredEvent);
        }
        bool IsExpiredEvent(long occurrence)=>!liveEvents.Contains(occurrence);
        static bool HysteresisLow(float ratio,bool wasLow)=>wasLow?ratio<ConditionalDefaults.LowExit:ratio<=ConditionalDefaults.LowEnter;
        public ConditionalControlFrame Resolve(RawPlayerInput frame)
        {
            if(disposed||player.IsDead)return default;
            previousResolvedAt=lastResolvedAt;previousResolvedFrame=lastResolvedFrame;
            lastResolvedAt=Time.time;lastResolvedFrame=Time.frameCount;
            raw=frame;output=default;platformOwnsMovement=false;facts.Clear();Snapshot();
            // Results raised while resolving are observed on the next pass, never synchronously recursed.
            observedSequence=sequence;
            CompleteStepBack();
            output.Move=raw.Move;output.Aim=raw.Aim;
            // Data dependencies only: Fire sees final Aim and D03 sees resolved Move.
            // Each operation arbitrates its own priority list independently.
            for(int op=0;op<5;op++)ResolveOperation(op);
            if(!output.AimOwned)currentTarget=AlignedTarget(raw.Aim,allVisible);
            previousTarget=currentTarget;
            if(previousTarget>=0)previousTargetPosition=contacts[previousTarget].Bounds.center;
            return output;
        }
        void ResolveOperation(int op)
        {
            var config=loadout.Operations[op];var active=running[op];
            // Observe every row independently, including rows hidden by a higher executable response.
            for(int row=0;row<config.Rules.Count;row++)
            {
                var rule=config.Rules[row];var state=states[op,row];
                bool match=Evaluate(rule,op,row,out var context,out bool observed);
                visualMatches[op,row]=observed;
                if(!ConditionalCatalog.Condition(rule.Condition).Event)
                {
                    if(observed&&!state.WasTrue){state.Consumed=false;state.Occurrence=++sequence;}
                    state.WasTrue=observed;context.Occurrence=state.Occurrence;
                    if(!observed)state.PlatformFailed=false;
                }
                matches[op,row]=match;contexts[op,row]=context;
            }
            // Physical Dodge is already committed. A completed Jump never gets canceled here.
            if(op==4&&active.Row>=0&&active.Accepted&&dodge.IsDodging)
            {output.DodgeOwned=true;Executed(op,active);return;}
            if(active.Accepted&&op==4)active.Row=-1;
            for(int row=0;row<config.Rules.Count;row++)
            {
                var rule=config.Rules[row];var state=states[op,row];var response=ConditionalCatalog.Response(rule.Action);
                bool isEvent=ConditionalCatalog.Condition(rule.Condition).Event;
                bool match=matches[op,row];var context=contexts[op,row];
                bool existing=active.Row==row;
                if(existing)
                {
                    // An accepted finite event response owns its real process. Continuous responses
                    // retain their bounded response window and must still satisfy the optional state.
                    if(isEvent)
                    {
                        context=response.Finite&&active.Accepted?active.Context:active.EventContext;
                        match=active.Accepted||Time.time<=context.ExpiresAt;
                        if(!response.Finite||!active.Accepted)
                            match&=AdditionalMatches(rule,op,ref context);
                    }
                    else
                    {
                        match=StateRuleMatches(rule,op,out context);
                    }
                    // A selected transition is one bounded process, including its physical landing.
                    if(rule.Action=="J04"&&active.Platform!=null&&(active.Accepted||active.Platform.LeftGround))
                    {match=true;context=active.Context;}
                    if(match&&active.Target>=0&&op!=2&&context.EnemyBound)
                        match=(context.Candidates&(1<<active.Target))!=0;
                    // A finite state response retains its commitment only after current eligibility.
                    if(match&&!isEvent&&response.Finite)context=active.Context;
                    if(match)
                    {
                        active.Context=context;
                        if(Continue(op,rule,active))return;
                    }
                    active.Row=-1;
                    // A failed or completed attempt does not mint another state occurrence.
                    match=matches[op,row];context=contexts[op,row];
                }
                if(!match||(int)response.Operation!=op)continue;
                bool preparation=HasPreparation(rule)&&!isEvent;
                if(isEvent?state.ConsumedEvents.Contains(context.Occurrence):response.Finite&&!preparation&&state.Consumed)continue;
                var candidate=new Running {Row=row,Context=context,Started=Time.time,PreviousX=body.position.x,
                    Deadline=isEvent?Time.time+ConditionalDefaults.ResponseWindow:float.PositiveInfinity};
                if(isEvent)
                {
                    candidate.EventContext=context;
                    foreach(var notice in events)if(notice.Context.Occurrence==context.Occurrence)
                    {candidate.EventContext=notice.Context;break;}
                }
                if(!Start(op,rule,candidate))continue;
                // Failed higher rows leave the existing lower response untouched.
                if(op==3)for(int other=0;other<config.Rules.Count;other++)states[op,other].CadencePending=false;
                running[op]=candidate;Starts++;return;
            }
            running[op].Row=-1;
        }
        static bool HasPreparation(ConditionalRule rule)=>rule.Condition==25||rule.AdditionalCondition==25;
        TriggerContext EmptyContext()=>new TriggerContext {Candidates=allVisible,Source=-1,PreparationSource=-1};
        bool Evaluate(ConditionalRule rule,int op,int row,out TriggerContext ctx,out bool observed)
        {
            var state=states[op,row];ctx=EmptyContext();observed=false;
            if(ConditionalCatalog.Condition(rule.Condition).Event)
            {
                // At equal event/cadence boundaries, adjacent frame samples can straddle both deadlines.
                // Admit only the first crossing sample of an already valid cadence-only attempt;
                // this is not an expired-event response queue and never extends its original deadline.
                if(state.CadencePending)
                {
                    var pending=state.CadenceContext;
                    bool inside=Time.time<=pending.ExpiresAt;
                    bool boundary=!inside&&Time.frameCount==previousResolvedFrame+1&&previousResolvedAt<=pending.ExpiresAt
                        &&gun.NextShotReadyAt<=pending.ExpiresAt+.0001f&&Time.time>=gun.NextShotReadyAt;
                    if((inside||boundary)&&!state.ConsumedEvents.Contains(pending.Occurrence)
                        &&gun.isActiveAndEnabled&&!gun.IsReloading&&gun.AmmoInMagazine>AmmoFloor(rule)
                        &&AdditionalMatches(rule,op,ref pending))
                    {ctx=pending;observed=inside;return true;}
                    state.CadencePending=false;
                }
                bool found=false;
                foreach(var notice in events)
                {
                    if(notice.Condition!=rule.Condition||notice.Context.Occurrence>observedSequence)continue;
                    var candidate=notice.Context;
                    if(!AdditionalMatches(rule,op,ref candidate))continue;
                    observed=true;
                    if(!found&&!state.ConsumedEvents.Contains(candidate.Occurrence)){ctx=candidate;found=true;}
                }
                return found;
            }
            if(!StateRuleMatches(rule,op,out ctx))return false;
            observed=true;
            if(HasPreparation(rule)&&ConditionalCatalog.Response(rule.Action).Finite)
            {
                int available=0;
                for(int i=0;i<actors.Count;i++)if((ctx.Candidates&(1<<i))!=0&&visible[i]&&contacts[i].Preparing
                    &&(!state.Preparations.TryGetValue(i,out int used)||used!=contacts[i].PreparationId))available|=1<<i;
                if(available==0)return false;
                ctx.Candidates=available;SelectSource(ref ctx);
            }
            return true;
        }
        bool StateRuleMatches(ConditionalRule rule,int op,out TriggerContext ctx)
        {
            if(!StateFact(rule,false,op,out ctx))return false;
            return AdditionalMatches(rule,op,ref ctx);
        }
        bool AdditionalMatches(ConditionalRule rule,int op,ref TriggerContext ctx)
        {
            if(rule.AdditionalCondition==0)return true;
            if(!StateFact(rule,true,op,out var extra))return false;
            if(ctx.EnemyBound&&extra.EnemyBound)
            {
                ctx.Candidates&=extra.Candidates;
                if(ctx.Candidates==0)return false;
            }
            else if(extra.EnemyBound)
            {ctx.Candidates=extra.Candidates;ctx.EnemyBound=true;}
            if(!ctx.IsThreat&&extra.IsThreat&&(ctx.Occurrence==0||ctx.Source<0))
            {ctx.IsThreat=true;ctx.ThreatKind=extra.ThreatKind;ctx.HasPosition=false;}
            if(extra.PreparationSource>=0)ctx.PreparationSource=extra.PreparationSource;
            SelectSource(ref ctx);
            return true;
        }
        void SelectSource(ref TriggerContext ctx)
        {
            if(!ctx.EnemyBound)return;
            if(ctx.Source<0||(ctx.Candidates&(1<<ctx.Source))==0)
            {
                ctx.Source=Choose(ctx.Candidates,"Q01");
                // A public projectile can retain a source whose body is currently concealed.
                if(ctx.Source<0)for(int i=0;i<actors.Count;i++)if((ctx.Candidates&(1<<i))!=0){ctx.Source=i;break;}
                ctx.HasPosition=false;
            }
            if(ctx.Source>=0&&!ctx.HasPosition)
            {
                if(ctx.IsThreat)
                {
                    if(geometry.ConditionalThreat(ctx.ThreatKind,movement.MoveInput.x,false,out var origin,out _,actors[ctx.Source].ActorId))
                    {ctx.HasPosition=true;ctx.Position=origin;}
                }
                else if(visible[ctx.Source]){ctx.HasPosition=true;ctx.Position=contacts[ctx.Source].Bounds.center;}
            }
            if(ctx.PreparationSource>=0&&ctx.Source>=0&&visible[ctx.Source]&&contacts[ctx.Source].Preparing)
            {ctx.PreparationSource=ctx.Source;ctx.PreparationId=contacts[ctx.Source].PreparationId;}
        }
        bool StateFact(ConditionalRule rule,bool additional,int op,out TriggerContext ctx)
        {
            int id=additional?rule.AdditionalCondition:rule.Condition;
            int option=id==51?(int)(additional?rule.AdditionalGroundState:rule.GroundState)
                :id==52?(int)(additional?rule.AdditionalDistance:rule.Distance)
                :id==53?(int)(additional?rule.AdditionalIncomingAttack:rule.IncomingAttack):0;
            int key=id*32+option*2+(id==40&&op==3?1:0);
            if(!facts.TryGetValue(key,out var fact))
            {
                bool match=EvaluateState(id,option,op,out ctx);
                facts[key]=fact=new Fact {Match=match,Context=ctx};
            }
            ctx=fact.Context;return fact.Match;
        }
        bool EvaluateState(int id,int option,int op,out TriggerContext ctx)
        {
            ctx=EmptyContext();
            switch(id)
            {
                case 51:return option==(int)GroundStateOption.Grounded?movement.IsGrounded:!movement.IsGrounded;
                case 4:return lowHealth;
                case 9:return gun.IsReloading;
                case 12:return !gun.IsReloading&&gun.AmmoInMagazine>0&&gun.AmmoInMagazine<=2;
                case 17:return raw.Fire;
                case 54:return movement.IsOrdinaryMoving;
                case 55:return dodge.IsDodging;
                case 56:return gun.IsFiring;
                case 39:
                    bool left=false,right=false;
                    for(int i=0;i<actors.Count;i++)if(visible[i])
                    {left|=contacts[i].Bounds.center.x<shape.bounds.center.x;right|=contacts[i].Bounds.center.x>shape.bounds.center.x;}
                    if(left&&right){ctx.Source=Choose(ctx.Candidates,"Q01");ctx.HasPosition=true;ctx.Position=contacts[ctx.Source].Bounds.center;}
                    return left&&right;
                case 53:
                    ctx.EnemyBound=true;ctx.IsThreat=true;
                    ctx.ThreatKind=option==(int)IncomingAttackOption.Bullet?41:option==(int)IncomingAttackOption.Melee?42:option==(int)IncomingAttackOption.Low?43:0;
                    ctx.Candidates=0;
                    for(int i=0;i<actors.Count;i++)if(actors[i]!=null&&geometry.ConditionalThreat(ctx.ThreatKind,movement.MoveInput.x,false,out _,out _,actors[i].ActorId))
                        ctx.Candidates|=1<<i;
                    if(ctx.Candidates==0)
                    {
                        if(!geometry.ConditionalThreat(ctx.ThreatKind,movement.MoveInput.x,false,out var unknown,out int actorId))return false;
                        // Public geometry without a body identity is usable but cannot match a named enemy filter.
                        ctx.HasPosition=true;ctx.Position=unknown;return true;
                    }
                    SelectSource(ref ctx);return true;
                case 45:
                    float margin=shape.bounds.size.x;
                    return shape.bounds.min.x-arena.min.x<margin||arena.max.x-shape.bounds.max.x<margin;
                case 18:case 25:case 35:case 40:case 52:
                    ctx.EnemyBound=true;ctx.Candidates=0;
                    for(int i=0;i<actors.Count;i++)if(visible[i]&&EnemyMatches(id,option,i,op))ctx.Candidates|=1<<i;
                    if(ctx.Candidates==0)return false;
                    if(id==25)ctx.PreparationSource=0;
                    SelectSource(ref ctx);return true;
                default:throw new InvalidOperationException("Unhandled V2 state A"+id);
            }
        }
        bool EnemyMatches(int id,int option,int i,int op)
        {
            var c=contacts[i];Vector2 delta=(Vector2)c.Bounds.center-(Vector2)shape.bounds.center;
            switch(id)
            {
                case 18:return cursorNear[i];
                case 25:return c.Preparing;
                case 52:return option==(int)EnemyDistanceOption.Close?close[i]:option==(int)EnemyDistanceOption.Far?far[i]:!close[i]&&!far[i];
                case 35:return c.VelocityKnown&&delta.sqrMagnitude>=.01f
                    &&Vector2.Dot(c.Velocity-body.linearVelocity,delta.normalized)<-ConditionalDefaults.VelocityDeadzone;
                case 40:return RayAligned(i,op==3?output.Aim:raw.CommittedAim);
                default:throw new InvalidOperationException("Unhandled enemy condition");
            }
        }
        float MouseAngle(int target)=>Vector2.Angle(raw.Aim,(Vector2)contacts[target].Bounds.center-gun.AimOrigin);
        bool NearSupportEdge()
        {
            if(!movement.IsGrounded||platforms==null)return false;
            var bounds=shape.bounds;
            foreach(var support in platforms)
            {
                if(support==null||!support.enabled)continue;
                var b=support.bounds;
                if(Mathf.Abs(bounds.min.y-b.max.y)>.13f||bounds.center.x<b.min.x||bounds.center.x>b.max.x)continue;
                return bounds.center.x-b.min.x<bounds.size.x||b.max.x-bounds.center.x<bounds.size.x;
            }
            return false;
        }
        int Choose(int mask,string response)
        {
            if(response=="Q07")return currentTarget>=0&&visible[currentTarget]&&(mask&(1<<currentTarget))!=0?currentTarget:-1;
            int best=-1;float bestScore=float.PositiveInfinity;
            for(int i=0;i<actors.Count;i++)
            {
                if(!visible[i]||(mask&(1<<i))==0)continue;
                if((response=="Q06"||response=="Q09")&&MouseAngle(i)>ConditionalDefaults.MouseEnter)continue;
                float score=((Vector2)contacts[i].Bounds.center-(Vector2)shape.bounds.center).sqrMagnitude;
                if(response=="Q02"&&best>=0)
                {
                    if(contacts[i].HealthFraction>contacts[best].HealthFraction)continue;
                    if(contacts[i].HealthFraction<contacts[best].HealthFraction){best=i;bestScore=score;continue;}
                }
                if(response=="Q04"&&actors[i].Profile.Kind==BossKind.Refraction||response=="Q05"&&actors[i].Profile.Kind==BossKind.Severance)score-=100000;
                if(score<bestScore-.0001f||Mathf.Abs(score-bestScore)<.0001f&&(best<0||actors[i].ActorId<actors[best].ActorId)) {best=i;bestScore=score;}
            }
            return best;
        }
        int AlignedTarget(Vector2 aim,int mask)
        {
            int target=Choose(mask,"Q01");
            if(target>=0&&RayAligned(target,aim))return target;
            for(int i=0;i<actors.Count;i++)if(visible[i]&&(mask&(1<<i))!=0&&RayAligned(i,aim))return i;
            return -1;
        }
        bool RayAligned(int i,Vector2 aim)
        {
            if(!visible[i]||aim.sqrMagnitude<.01f)return false;
            Vector2 origin=gun.GetMuzzlePosition(aim.normalized);
            var ray=new Ray(origin,aim.normalized);var b=contacts[i].Bounds;b.Expand(gun.BulletRadius*2);
            if(!b.IntersectRay(ray,out float distance)||distance>50)return false;
            // Bullets pass terrain; the nearest real opponent body is the actual hit candidate.
            for(int j=0;j<actors.Count;j++)
                if(j!=i&&visible[j]) {var other=contacts[j].Bounds;other.Expand(gun.BulletRadius*2);if(other.IntersectRay(ray,out float d)&&d<distance)return false;}
            return !actors[i].GuardBlocksRay(origin,origin+aim.normalized*(distance+.01f));
        }
        Vector2 AimAt(int target)
        {
            var c=contacts[target];Vector2 point=c.Bounds.center;
            for(int k=0;k<3;k++)
            {
                float flight=Vector2.Distance(gun.GetMuzzlePosition((point-gun.AimOrigin).normalized),point)/Mathf.Max(.01f,gun.BulletSpeed);
                flight=Mathf.Min(.35f,flight);
                point=(Vector2)c.Bounds.center+(c.VelocityKnown?c.Velocity*flight:Vector2.zero);
                if(c.GroundedKnown&&!c.Grounded)point.y-=.5f*c.Gravity*flight*flight;
                if(actors[target].Navigation!=null&&!actors[target].Navigation.SweepTraversal(c.Bounds.center,point,c.Bounds.size,out _,out var safe))point=safe;
            }
            return (point-gun.AimOrigin).normalized;
        }
        int AmmoFloor(ConditionalRule rule)=>ConditionalCatalog.UsesAmmoOption(rule.Action)&&rule.AmmoUse==AmmoUseOption.KeepTwoRounds?ConditionalDefaults.Reserve:0;
        bool Start(int op,ConditionalRule rule,Running r)
        {
            string action=rule.Action;
            bool targetAction=op==2||action=="M01"||action=="M02"||action=="M03"||action=="D02";
            // Explicit Nearest/Lowest-Health/Fine-Tune strategies retain their own visible target policy.
            r.Target=targetAction?Choose(op==2||!r.Context.EnemyBound?allVisible:r.Context.Candidates,action):-1;
            if(action=="M04"&&!r.Context.HasPosition)r.Target=Choose(r.Context.EnemyBound?r.Context.Candidates:allVisible,"Q01");
            if(action=="Q03")r.Target=r.Context.Source>=0&&visible[r.Context.Source]?r.Context.Source:-1;
            r.Aim=raw.CommittedAim;
            switch((ControlOperation)op)
            {
                case ControlOperation.Movement:
                    if((action=="M01"||action=="M02"||action=="M03")&&r.Target<0)return false;
                    if(action=="M04")
                    {
                        if(!SourcePoint(r,out var threat))return false;
                        r.Direction=Away(threat.x);r.Destination=body.position.x+r.Direction*ConditionalDefaults.StepBack;
                        r.Deadline=Time.time+ConditionalDefaults.StepBack/Mathf.Max(.01f,movement.MoveSpeed)+.5f;
                    }
                    if(action=="M11")r.Direction=rule.MoveDirection==MoveDirectionOption.Left?-1:1;
                    return WriteMovement(action,r);
                case ControlOperation.Jump:
                    if(action=="J04")return StartPlatform(rule,r);
                    if(!CanJump())return false;
                    output.JumpOwned=true;output.Jump=true;return true;
                case ControlOperation.Aim:
                    if(r.Target<0)return false;
                    return WriteAim(action,r);
                case ControlOperation.Fire:
                    if(action=="F06")
                    {output.FireOwned=true;output.Fire=false;Accept(op,r);return true;}
                    if(!gun.isActiveAndEnabled||gun.IsReloading||gun.AmmoInMagazine<=AmmoFloor(rule))return false;
                    if(!gun.CanFireNow)
                    {
                        var state=states[op,r.Row];
                        if((action=="F01"||action=="F02")&&ConditionalCatalog.Condition(rule.Condition).Event
                            &&!state.CadencePending&&Time.time<=r.Context.ExpiresAt
                            &&gun.NextShotReadyAt<=r.Context.ExpiresAt+.0001f)
                        {state.CadencePending=true;state.CadenceContext=r.Context;}
                        return false;
                    }
                    r.Shots=0;
                    r.ShotLimit=action=="F01"?1:action=="F02"?gun.AmmoInMagazine:int.MaxValue;
                    if(action!="F03")r.Deadline=Time.time+r.ShotLimit*gun.FireInterval+.5f;
                    output.FireOwned=true;output.Fire=true;return true;
                case ControlOperation.Dodge:
                    if(!dodge.CanDodge)return false;
                    if(action=="D01") {if(!SourcePoint(r,out var source))return false;r.Direction=Away(source.x);}
                    else if(action=="D02") {if(r.Target<0)return false;r.Direction=-Away(contacts[r.Target].Bounds.center.x);}
                    else if(action=="D03")r.Direction=Mathf.Abs(output.Move)>.01f?Mathf.Sign(output.Move):movement.MovementFacingDirection;
                    else if(action=="D04") {if(Mathf.Abs(raw.Cursor.x-body.position.x)<.01f)return false;r.Direction=Mathf.Sign(raw.Cursor.x-body.position.x);}
                    else throw new InvalidOperationException("Unknown Dodge response");
                    if(Travel(r.Direction,dodge.Distance)<.08f)return false;
                    output.DodgeOwned=true;output.Dodge=true;output.DodgeDirection=r.Direction;return true;
                default:throw new InvalidOperationException("Unknown operation");
            }
        }
        bool Continue(int op,ConditionalRule rule,Running r)
        {
            string action=rule.Action;
            if(action=="J04")return ContinuePlatform(rule,r);
            if(r.Completed||op==1&&r.Accepted)return false;
            if(op!=1&&Time.time>=r.Deadline)return false;
            if(r.Target>=0&&(!visible[r.Target]||!actors[r.Target].IsAlive))return false;
            switch((ControlOperation)op)
            {
                case ControlOperation.Movement:return WriteMovement(action,r);
                case ControlOperation.Aim:return WriteAim(action,r);
                case ControlOperation.Fire:
                    if(action=="F06"){output.FireOwned=true;output.Fire=false;Accept(op,r);return true;}
                    if(!gun.isActiveAndEnabled||gun.IsReloading||gun.AmmoInMagazine<=AmmoFloor(rule))return false;
                    if(!r.Accepted&&!gun.CanFireNow)return false;
                    if(action!="F03"&&r.Shots>=r.ShotLimit)return false;
                    output.FireOwned=true;output.Fire=true;return true;
                case ControlOperation.Jump:
                    if(!CanJump())return false;
                    output.JumpOwned=true;output.Jump=true;return true;
                case ControlOperation.Dodge:return false;
                default:throw new InvalidOperationException("Unknown running operation");
            }
        }
        void CompleteStepBack()
        {
            var r=running[0];
            if(r.Row<0||!r.Accepted||r.Completed||loadout.Operations[0].Rules[r.Row].Action!="M04"
                ||Time.time>=r.Deadline||dodge.IsDodging||movement.HitPushActive)return;
            if((body.position.x-r.Destination)*r.Direction<-.005f)return;
            r.Completed=true;Record(61);
        }
        bool CanJump()=>movement.IsGrounded&&!dodge.IsDodging&&input.GameplayInputEnabled&&movement.isActiveAndEnabled;
        bool SourcePoint(Running r,out Vector2 point)
        {
            if(r.Context.HasPosition){point=r.Context.Position;return true;}
            if(r.Target>=0&&visible[r.Target]){point=contacts[r.Target].Bounds.center;return true;}
            point=default;return false;
        }
        float Away(float x)=>Mathf.Abs(x-body.position.x)>.01f?(x>body.position.x?-1:1):movement.MovementFacingDirection;
        bool WriteMovement(string action,Running r)
        {
            if(dodge.IsDodging||movement.HitPushActive||!movement.isActiveAndEnabled||!body.simulated||action!="M10"&&movement.MoveSpeed<=.01f)return false;
            float move=0,x=body.position.x;
            float distance=r.Target>=0?Mathf.Abs(contacts[r.Target].Bounds.center.x-shape.bounds.center.x):0;
            float toward=r.Target>=0?-Away(contacts[r.Target].Bounds.center.x):0;
            switch(action)
            {
                case "M01":move=toward;break;
                case "M02":move=-toward;break;
                case "M03":
                    var state=states[0,r.Row];
                    if(state.RangeMode<0&&distance>=ConditionalDefaults.Close+ConditionalDefaults.Hysteresis
                        ||state.RangeMode>0&&distance<=ConditionalDefaults.Far-ConditionalDefaults.Hysteresis)state.RangeMode=0;
                    if(state.RangeMode==0)state.RangeMode=distance<ConditionalDefaults.Close?-1:distance>ConditionalDefaults.Far?1:0;
                    if(state.RangeMode==0)return false;
                    move=state.RangeMode*toward;break;
                case "M04":
                    if(r.Completed)return false;move=r.Direction;break;
                case "M11":move=r.Direction;break;
                case "M09":if(Mathf.Abs(arena.center.x-x)<=.15f)return false;move=Mathf.Sign(arena.center.x-x);break;
                case "M10":move=0;break;
                default:throw new InvalidOperationException("Unknown movement response");
            }
            if(Mathf.Abs(move)>.01f)
            {
                if(Travel(move,.18f)<.04f)return false;
                r.Stuck=Mathf.Abs(x-r.PreviousX)<.005f?r.Stuck+Time.deltaTime:0;
                if(r.Stuck>ConditionalDefaults.StuckWindow)return false;
            }
            r.PreviousX=x;output.MovementOwned=true;output.Move=move;return true;
        }
        bool WriteAim(string action,Running r)
        {
            // A current public candidate set, never a hidden target lock.
            if(action=="Q01"||action=="Q02")
            {r.Target=Choose(allVisible,action);if(r.Target<0)return false;}
            if((action=="Q06"||action=="Q09")&&MouseAngle(r.Target)>ConditionalDefaults.MouseExit)return false;
            output.AimOwned=true;output.Aim=action=="Q08"?r.Aim:AimAt(r.Target);
            currentTarget=action=="Q08"?AlignedTarget(r.Aim,allVisible):r.Target;return true;
        }
        float Travel(float direction,float distance)
        {
            float available=distance;
            var filter=new ContactFilter2D {useTriggers=false};
            int count=shape.Cast(Vector2.right*Mathf.Sign(direction),filter,hits,distance);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];var other=hit.collider;
                if(other==null||other.attachedRigidbody==body||other.isTrigger||Physics2D.GetIgnoreCollision(shape,other)
                    ||Vector2.Dot(hit.normal,Vector2.right*Mathf.Sign(direction))>=-.2f)continue;
                if(other.usedByEffector&&other.GetComponent<PlatformEffector2D>()!=null)continue;
                available=Mathf.Min(available,Mathf.Max(0,hit.distance-.015f));
            }
            return available;
        }
        void ActionResolved(ControlOperation operation,bool accepted)
        {
            if(disposed)return;
            if(platformOwnsMovement&&operation==ControlOperation.Movement)
            {
                var transition=running[1];
                if(transition.Row>=0&&transition.Platform!=null&&accepted)Accept(1,transition);
                return;
            }
            int op=(int)operation;var r=running[op];if(r.Row<0)return;
            if(accepted)
            {
                Accept(op,r);
                if(op==1){AcceptedJumps++;if(r.Platform!=null)r.Platform.Launched=true;}
                if(op==4)AcceptedDodges++;return;
            }
            // Losing a physical attempt never refunds an already consumed occurrence.
            r.Accepted=false;
            if(op==0||op==2)return;
            r.Row=-1;
        }
    }
}
