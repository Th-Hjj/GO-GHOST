using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Per-fight controller. Emits input only; the retained executors accept actions and spend resources.
    public sealed class ConditionalController : IDisposable
    {
        public struct TriggerContext
        {
            public long Occurrence;
            public int Candidates, Source;
            public bool HasPosition;
            public Vector2 Position;
        }
        sealed class RuleState
        {
            public bool WasTrue, Consumed;
            public long Occurrence, LastEvent;
        }
        sealed class Running
        {
            public int Row=-1, Target=-1, Shots, ShotLimit;
            public float Started, Deadline, Direction, Destination, PreviousX, Stuck;
            public Vector2 Aim;
            public bool Accepted;
            public TriggerContext Context;
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
        readonly RuleState[,] states=new RuleState[5,2];
        readonly bool[,] matches=new bool[5,2];
        readonly TriggerContext[,] contexts=new TriggerContext[5,2];
        readonly int[,] factFrames=new int[51,2];
        readonly bool[,] factMatches=new bool[51,2],factWasTrue=new bool[51,2];
        readonly long[,] factOccurrences=new long[51,2];
        readonly TriggerContext[,] factContexts=new TriggerContext[51,2];
        readonly Running[] running={new Running(),new Running(),new Running(),new Running(),new Running()};
        readonly List<Notice> events=new List<Notice>(16);
        readonly BossContact[] contacts;
        readonly bool[] visible,seen,hidden,lowEnemy,close,far;
        readonly RaycastHit2D[] hits=new RaycastHit2D[24];
        RawPlayerInput raw;
        ConditionalControlFrame output;
        long sequence;
        int allVisible,previousTarget=-1,currentTarget=-1;
        bool initialized,grounded,lowHealth,disposed;
        Vector2 previousTargetPosition;
        public ConditionalControlFrame Ownership => output;
        public int Starts { get; private set; }
        public int AcceptedJumps {get;private set;}
        public int AcceptedDodges {get;private set;}
        public int AutomaticShots {get;private set;}
        public int CurrentTargetId => currentTarget>=0?actors[currentTarget].ActorId:0;
        public ConditionalController(PlayerHealth player,IReadOnlyList<BossAgent> actors,Collider2D[] platforms,Bounds arena,ConditionalLoadout snapshot)
        {
            this.player=player;this.actors=actors;this.platforms=platforms;this.arena=arena;loadout=snapshot.Copy();
            movement=player.GetComponent<PlayerMovement>();gun=player.GetComponent<PlayerShooting>();dodge=player.GetComponent<PlayerDodge>();
            input=player.GetComponent<PlayerInputReader>();shape=player.GetComponent<Collider2D>();body=player.GetComponent<Rigidbody2D>();
            geometry=new LegacyPlayerAI(player,actors,platforms);
            contacts=new BossContact[actors.Count];visible=new bool[actors.Count];seen=new bool[actors.Count];hidden=new bool[actors.Count];
            lowEnemy=new bool[actors.Count];close=new bool[actors.Count];far=new bool[actors.Count];
            for(int op=0;op<5;op++)for(int row=0;row<2;row++) states[op,row]=new RuleState();
            gun.ReloadStarted+=ReloadStarted;gun.ReloadFinished+=ReloadFinished;gun.EnemyHit+=EnemyHit;gun.ShotFired+=Shot;
            player.DamageTaken+=Damaged;dodge.DodgeFinished+=DodgeFinished;input.ConditionalActionResolved+=ActionResolved;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            gun.ReloadStarted-=ReloadStarted;gun.ReloadFinished-=ReloadFinished;gun.EnemyHit-=EnemyHit;gun.ShotFired-=Shot;
            player.DamageTaken-=Damaged;dodge.DodgeFinished-=DodgeFinished;input.ConditionalActionResolved-=ActionResolved;
            events.Clear();output=default;
        }
        void ReloadStarted()=>Record(10);
        void ReloadFinished()=>Record(11);
        void DodgeFinished()=>Record(7);
        void Shot(Vector2 origin,Vector2 direction) {if(input.ConditionalFrame.FireOwned) AutomaticShots++;}
        int Index(GameObject source)
        {
            var actor=source!=null?source.GetComponentInParent<BossAgent>():null;
            for(int i=0;i<actors.Count;i++)if(actors[i]==actor)return i;
            return -1;
        }
        void Damaged(int amount,GameObject source)=>Record(6,Index(source));
        void EnemyHit(Enemy enemy,bool defeated)
        {
            int target=Index(enemy.gameObject);Record(13,target);if(defeated)Record(14,target);
        }
        void Record(int condition,int source=-1,Vector2? position=null)
        {
            if(!initialized||disposed)return;
            var ctx=new TriggerContext {Occurrence=++sequence,Source=source,Candidates=source>=0?1<<source:allVisible};
            if(position.HasValue){ctx.HasPosition=true;ctx.Position=position.Value;}
            // Damage/death identifies its source; only retain an already observed static body position.
            else if(source>=0&&seen[source]){ctx.HasPosition=true;ctx.Position=contacts[source].Bounds.center;}
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
                    if(initialized&&seen[i]&&hidden[i])Record(32,i,(Vector2)c.Bounds.center);
                    seen[i]=true;hidden[i]=false;
                    lowEnemy[i]=HysteresisLow(c.HealthFraction,lowEnemy[i]);
                    float distance=Mathf.Abs(c.Bounds.center.x-shape.bounds.center.x);
                    close[i]=distance<=(close[i]?ConditionalDefaults.Close+ConditionalDefaults.Hysteresis:ConditionalDefaults.Close);
                    far[i]=distance>=(far[i]?ConditionalDefaults.Far-ConditionalDefaults.Hysteresis:ConditionalDefaults.Far);
                }
                else if(seen[i])hidden[i]=true;
                visible[i]=now;
            }
            bool low=HysteresisLow((float)player.CurrentHealth/player.MaxHealth,lowHealth);
            if(initialized)
            {
                if(movement.IsGrounded!=grounded)Record(movement.IsGrounded?3:2);
                if(low&&!lowHealth)Record(5);
                if(previousTarget>=0&&!visible[previousTarget])Record(50,previousTarget,previousTargetPosition);
            }
            grounded=movement.IsGrounded;lowHealth=low;
            if(initialized&&raw.Jump)Record(19);
            if(initialized&&raw.Dodge)Record(20);
            initialized=true;
            events.RemoveAll(n=>Time.time-n.At>ConditionalDefaults.EventWindow);
        }
        static bool HysteresisLow(float ratio,bool wasLow)=>wasLow?ratio<ConditionalDefaults.LowExit:ratio<=ConditionalDefaults.LowEnter;
        public ConditionalControlFrame Resolve(RawPlayerInput frame)
        {
            if(disposed||player.IsDead)return default;
            raw=frame;output=default;Snapshot();
            output.Move=raw.Move;output.Aim=raw.Aim;
            // Fixed order: Move, Jump, Aim, Fire(final muzzle recheck), Dodge(resolved movement).
            for(int op=0;op<5;op++)ResolveOperation(op);
            if(!output.AimOwned)currentTarget=AlignedTarget(raw.Aim,allVisible);
            previousTarget=currentTarget;
            if(previousTarget>=0)previousTargetPosition=contacts[previousTarget].Bounds.center;
            return output;
        }
        void ResolveOperation(int op)
        {
            var config=loadout.Operations[op];var active=running[op];
            if(!config.Enabled)return;
            // Observe both rows even while the higher one owns the operation, so a lower
            // condition's false/true transition cannot disappear behind priority arbitration.
            for(int row=0;row<config.Rules.Count;row++)
            {
                var rule=config.Rules[row];var state=states[op,row];
                bool match=Evaluate(rule,op,out var context);
                if(!ConditionalCatalog.Condition(rule.Condition).Event)
                {
                    if(match&&state.Occurrence!=context.Occurrence)state.Consumed=false;
                    state.WasTrue=match;state.Occurrence=context.Occurrence;
                }
                matches[op,row]=match;contexts[op,row]=context;
            }
            // An already accepted physical action cannot be replaced or canceled by a condition change.
            if(op==4&&active.Accepted&&dodge.IsDodging) {output.DodgeOwned=true;return;}
            if(active.Accepted&&op==4)active.Row=-1;
            for(int row=0;row<config.Rules.Count;row++)
            {
                var rule=config.Rules[row];var state=states[op,row];
                bool match=matches[op,row];var context=contexts[op,row];
                bool isEvent=ConditionalCatalog.Condition(rule.Condition).Event;
                // Event's short observation window doesn't shorten an already accepted response.
                bool existing=active.Row==row;
                if(existing&&isEvent)
                {
                    context=active.Context;
                    match=true;
                }
                if(!match){if(existing)active.Row=-1;continue;}
                if(existing)
                {
                    if(Continue(op,rule,active))return;
                    active.Row=-1;continue;
                }
                var response=ConditionalCatalog.Response(rule.Action);
                if((int)response.Operation!=op)continue;
                if(isEvent ? context.Occurrence<=state.LastEvent : response.Finite&&state.Consumed)continue;
                var candidate=new Running {Row=row,Context=context,Started=Time.time,PreviousX=body.position.x,
                    Deadline=isEvent?Time.time+ConditionalDefaults.ResponseWindow:float.PositiveInfinity};
                if(!Start(op,rule,candidate))continue;
                // Only an executable higher row can replace the existing lower response.
                running[op]=candidate;active=candidate;
                if(response.Finite)state.Consumed=true;
                if(isEvent)state.LastEvent=context.Occurrence;
                Starts++;return;
            }
            running[op].Row=-1;
        }
        bool Evaluate(ConditionalRule rule,int op,out TriggerContext ctx)
        {
            ctx=new TriggerContext {Candidates=allVisible,Source=-1};
            if(ConditionalCatalog.Condition(rule.Condition).Event)
            {
                foreach(var n in events)
                    if(n.Condition==rule.Condition&&n.Context.Occurrence>states[op,loadout.Operations[op].Rules.IndexOf(rule)].LastEvent)
                    {ctx=n.Context;return true;}
                return false;
            }
            // All operations share one condition fact and occurrence. A40 alone has an
            // explicitly separate final-Fire ray versus the prior committed ray.
            int id=rule.Condition,variant=id==37&&rule.Below||id==40&&op!=3?1:0;
            if(factFrames[id,variant]!=Time.frameCount)
            {
                bool match=EvaluateState(rule,op,out ctx);
                if(match&&!factWasTrue[id,variant])factOccurrences[id,variant]=++sequence;
                factWasTrue[id,variant]=match;ctx.Occurrence=factOccurrences[id,variant];
                factFrames[id,variant]=Time.frameCount;factMatches[id,variant]=match;factContexts[id,variant]=ctx;
            }
            ctx=factContexts[id,variant];return factMatches[id,variant];
        }
        bool EvaluateState(ConditionalRule rule,int op,out TriggerContext ctx)
        {
            ctx=new TriggerContext {Candidates=allVisible,Source=-1};
            switch(rule.Condition)
            {
                case 1:return !movement.IsGrounded;
                case 4:return lowHealth;
                case 9:return gun.IsReloading;
                case 12:return !gun.IsReloading&&gun.AmmoInMagazine>0&&gun.AmmoInMagazine<=2;
                case 15:return dodge.CooldownRemaining>0;
                case 17:return raw.Fire;
                case 39:
                    bool left=false,right=false;
                    for(int i=0;i<actors.Count;i++)if(visible[i]) {left|=contacts[i].Bounds.center.x<shape.bounds.center.x;right|=contacts[i].Bounds.center.x>shape.bounds.center.x;}
                    return left&&right;
                case 41:case 42:case 43:
                    if(!geometry.ConditionalThreat(rule.Condition,movement.MoveInput.x,false,out var origin,out int actorId))return false;
                    ctx.HasPosition=true;ctx.Position=origin;
                    for(int i=0;i<actors.Count;i++)if(actors[i].ActorId==actorId){ctx.Source=i;ctx.Candidates=1<<i;break;}
                    return true;
                case 45:
                    float margin=shape.bounds.size.x;
                    return shape.bounds.min.x-arena.min.x<margin||arena.max.x-shape.bounds.max.x<margin;
                case 46:return NearSupportEdge();
                case 47:
                    float move=movement.MoveInput.x;
                    return Mathf.Abs(move)>.01f&&Travel(Mathf.Sign(move),.2f)<.08f;
                case 18:case 21:case 22:case 25:case 27:case 28:case 29:case 31:
                case 33:case 34:case 35:case 36:case 37:case 40:
                    ctx.Candidates=0;
                    for(int i=0;i<actors.Count;i++)
                        if(visible[i]&&EnemyMatches(rule,i,op))ctx.Candidates|=1<<i;
                    ctx.Source=Choose(ctx.Candidates,"Q01");
                    if(ctx.Source>=0){ctx.HasPosition=true;ctx.Position=contacts[ctx.Source].Bounds.center;}
                    return ctx.Candidates!=0;
                default:throw new InvalidOperationException("Unhandled state A"+rule.Condition);
            }
        }
        bool EnemyMatches(ConditionalRule r,int i,int op)
        {
            var c=contacts[i];Vector2 delta=(Vector2)c.Bounds.center-(Vector2)shape.bounds.center;
            switch(r.Condition)
            {
                case 18:return MouseAngle(i)<=ConditionalDefaults.MouseEnter;
                case 21:return raw.Move*delta.x>.01f;
                case 22:return raw.Move*delta.x<-.01f;
                case 25:return c.Preparing;
                case 27:return c.Recovering;
                case 28:return c.Reloading;
                case 29:return c.GroundedKnown&&!c.Grounded;
                case 31:return lowEnemy[i];
                case 33:return close[i];
                case 34:return far[i];
                case 35:case 36:
                    if(!c.VelocityKnown||delta.sqrMagnitude<.01f)return false;
                    float separation=Vector2.Dot(c.Velocity-body.linearVelocity,delta.normalized);
                    return r.Condition==35?separation<-ConditionalDefaults.VelocityDeadzone:separation>ConditionalDefaults.VelocityDeadzone;
                case 37:return r.Below?delta.y<-shape.bounds.size.y:delta.y>shape.bounds.size.y;
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
                if(response=="Q02")score=contacts[i].HealthFraction*100000+score*.001f;
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
        bool Start(int op,ConditionalRule rule,Running r)
        {
            string action=rule.Action;
            bool targetAction=op==2&&action!="Q08"||action=="M01"||action=="M02"||action=="M03"||action=="D02";
            r.Target=targetAction?Choose(r.Context.Candidates,action):-1;
            if((action=="M04"||action=="D01")&&!r.Context.HasPosition)r.Target=Choose(r.Context.Candidates,"Q01");
            if(action=="Q03")r.Target=r.Context.Source>=0&&visible[r.Context.Source]&&(r.Context.Candidates&(1<<r.Context.Source))!=0?r.Context.Source:-1;
            r.Aim=raw.CommittedAim;
            switch((ControlOperation)op)
            {
                case ControlOperation.Movement:
                    if((action=="M01"||action=="M02"||action=="M03")&&r.Target<0)return false;
                    if(action=="M04")
                    {
                        if(!SourcePoint(r,out var threat))return false;
                        r.Direction=Away(threat.x);r.Destination=body.position.x+r.Direction*ConditionalDefaults.StepBack;
                        r.Deadline=Time.time+ConditionalDefaults.StepBack/movement.MoveSpeed+.5f;
                    }
                    if(action=="M05") {r.Direction=Mathf.Sign(raw.Move);if(Mathf.Abs(raw.Move)<.01f)return false;}
                    if(action=="M06") {r.Destination=Mathf.Clamp(raw.Cursor.x,arena.min.x+shape.bounds.extents.x,arena.max.x-shape.bounds.extents.x);r.Deadline=Time.time+Mathf.Abs(r.Destination-body.position.x)/movement.MoveSpeed+.5f;}
                    return WriteMovement(action,r);
                case ControlOperation.Jump:
                    if(!CanJump())return false;
                    switch(action)
                    {
                        case "J01":case "J03":output.Jump=true;break;
                        case "J02":r.Deadline=Time.time+ConditionalDefaults.JumpDelay;output.Jump=false;break;
                        default:throw new InvalidOperationException("Unknown Jump response");
                    }
                    output.JumpOwned=true;return true;
                case ControlOperation.Aim:
                    if(action!="Q08"&&r.Target<0)return false;
                    if(action=="Q09")r.Deadline=Time.time+ConditionalDefaults.Calibration;
                    return WriteAim(action,r);
                case ControlOperation.Fire:
                    if(!gun.CanFireNow)return false;
                    if(action=="F05"&&gun.AmmoInMagazine<=ConditionalDefaults.Reserve)return false;
                    r.Shots=gun.TriggerPulls;
                    switch(action)
                    {
                        case "F01":r.ShotLimit=1;break;
                        case "F02":r.ShotLimit=Mathf.Min(ConditionalDefaults.Burst,gun.AmmoInMagazine);break;
                        case "F03":case "F04":r.ShotLimit=gun.AmmoInMagazine;break;
                        case "F05":r.ShotLimit=gun.AmmoInMagazine-ConditionalDefaults.Reserve;break;
                        default:throw new InvalidOperationException("Unknown Fire response");
                    }
                    if(action!="F03")r.Deadline=Time.time+r.ShotLimit*gun.FireInterval+.5f;
                    output.FireOwned=true;output.Fire=true;return true;
                case ControlOperation.Dodge:
                    if(!dodge.CanDodge)return false;
                    if(action=="D01") {if(!SourcePoint(r,out var source))return false;r.Direction=Away(source.x);}
                    else if(action=="D02") {if(r.Target<0)return false;r.Direction=-Away(contacts[r.Target].Bounds.center.x);}
                    else if(action=="D03")r.Direction=Mathf.Abs(output.Move)>.01f?Mathf.Sign(output.Move):movement.MovementFacingDirection;
                    else if(action=="D04") {if(Mathf.Abs(raw.Cursor.x-body.position.x)<.01f)return false;r.Direction=Mathf.Sign(raw.Cursor.x-body.position.x);}
                    else if(action=="D07") {if(!geometry.ConditionalDodgeDirection(output.Move,out r.Direction))return false;}
                    else throw new InvalidOperationException("Unknown Dodge response");
                    if(Travel(r.Direction,dodge.Distance)<.08f)return false;
                    output.DodgeOwned=true;output.Dodge=true;output.DodgeDirection=r.Direction;return true;
                default:throw new InvalidOperationException("Unknown operation");
            }
        }
        bool Continue(int op,ConditionalRule rule,Running r)
        {
            string action=rule.Action;
            if(op==1&&r.Accepted)return false;
            if(op!=1&&Time.time>=r.Deadline)return false;
            if(r.Target>=0&&(!visible[r.Target]||!actors[r.Target].IsAlive))return false;
            // Enemy state filters remain bound to the selected candidate, even if another actor still matches.
            if(r.Target>=0&&!ConditionalCatalog.Condition(rule.Condition).Event
                &&IsEnemyState(rule.Condition)&&!EnemyMatches(rule,r.Target,op))return false;
            switch((ControlOperation)op)
            {
                case ControlOperation.Movement:return WriteMovement(action,r);
                case ControlOperation.Aim:return WriteAim(action,r);
                case ControlOperation.Fire:
                    if(gun.IsReloading||gun.AmmoInMagazine<=0)return false;
                    if(action!="F03"&&gun.TriggerPulls-r.Shots>=r.ShotLimit)return false;
                    if(action=="F05"&&gun.AmmoInMagazine<=ConditionalDefaults.Reserve)return false;
                    output.FireOwned=true;output.Fire=true;return true;
                case ControlOperation.Jump:
                    if(action=="J02"&&Time.time<r.Deadline)
                    {if(!CanJump())return false;output.JumpOwned=true;return true;}
                    if(!CanJump())return false;
                    output.JumpOwned=true;output.Jump=true;return true;
                case ControlOperation.Dodge:return false;
                default:throw new InvalidOperationException("Unknown running operation");
            }
        }
        static bool IsEnemyState(int id)=>id==18||id==21||id==22||id==25||id==27||id==28||id==29||id==31||id>=33&&id<=37||id==40;
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
            float move=0,x=body.position.x;
            float distance=r.Target>=0?Mathf.Abs(contacts[r.Target].Bounds.center.x-shape.bounds.center.x):0;
            float toward=r.Target>=0?-Away(contacts[r.Target].Bounds.center.x):0;
            switch(action)
            {
                case "M01":move=distance>ConditionalDefaults.Close?toward:0;break;
                case "M02":move=distance<ConditionalDefaults.Far?-toward:0;break;
                case "M03":move=distance<ConditionalDefaults.Close?-toward:distance>ConditionalDefaults.Far?toward:0;break;
                case "M04":case "M06":
                    if(Mathf.Abs(r.Destination-x)<.12f||action=="M04"&&(x-r.Destination)*r.Direction>=0)return false;
                    move=Mathf.Sign(r.Destination-x);break;
                case "M05":move=r.Direction;break;
                case "M09":move=Mathf.Abs(arena.center.x-x)>.15f?Mathf.Sign(arena.center.x-x):0;break;
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
            int op=(int)operation;var r=running[op];if(r.Row<0)return;
            r.Accepted=accepted;
            if(accepted){if(op==1)AcceptedJumps++;if(op==4)AcceptedDodges++;return;}
            // A rejected same-frame request consumes no occurrence and queues no edge.
            var state=states[op,r.Row];
            // An observed press edge is consumed even when the simultaneous Dodge wins;
            // replaying it at the end of the .25 s event window would queue swallowed input.
            if(!ConditionalCatalog.Condition(loadout.Operations[op].Rules[r.Row].Condition).Event)state.Consumed=false;
            r.Row=-1;
        }
    }
}
