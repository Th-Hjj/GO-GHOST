using System;
using UnityEngine;

namespace GoGhost.Duel
{
    // Action requests and real executor callbacks; rule consumption follows actual acceptance.
    public sealed partial class ConditionalController
    {
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
            if(!r.ResponseStarted)
            {
                r.ResponseStarted=true;r.Started=Time.time;
                var rule=loadout.Operations[op].Rules[r.Row];
                if(ConditionalCatalog.Condition(rule.Condition).Event&&!ConditionalCatalog.Response(rule.Action).Finite)
                    r.Deadline=Time.time+ConditionalDefaults.ResponseWindow;
            }
            if(!r.Accepted){acceptedAt[op,r.Row]=Time.time;acceptedFrame[op,r.Row]=Time.frameCount;}
            Executed(op,r);Consume(op,r);
        }
        void ReloadStarted()
        {
            // Final-shot completion is recorded before the gun begins its automatic reload.
            // An unfinished burst must never resume from a later magazine, even after a fast reload.
            var r=running[(int)ControlOperation.Fire];
            if(r.Row>=0&&!r.Completed&&loadout.Operations[(int)ControlOperation.Fire].Rules[r.Row].Action=="F02")r.Row=-1;
        }
        void ReloadFinished()=>Record(11);
        void BurstFinished()
        {
            Record(60);
            var r=running[(int)ControlOperation.Fire];
            if(r.Row>=0&&r.Accepted&&loadout.Operations[3].Rules[r.Row].Action=="F02")r.Completed=true;
        }
        void DodgeFinished()=>Record(7);
        void JumpStarted()=>Record(57);
        void JumpApexReached()=>Record(58);
        void Shot(Vector2 origin,Vector2 direction)
        {
            // The public fact includes manual shots; only the owning response receives auto credit.
            Record(59);
            var r=running[3];
            if(disposed||gun.LastShotActor!=ProjectileActor.InnerAI||r.Row<0)return;
            string action=loadout.Operations[3].Rules[r.Row].Action;
            if(action!=gun.LastShotActionId||action=="F06"||r.Completed)return;
            if(action=="F02"&&!r.Accepted)
            {
                // ShotFired follows the real ammo spend: capture this magazine at actual acceptance.
                r.ShotLimit=gun.BurstBudget;
                r.Deadline=float.PositiveInfinity;
            }
            AutomaticShots++;r.Shots++;acceptedAt[3,r.Row]=Time.time;acceptedFrame[3,r.Row]=Time.frameCount;Accept(3,r);
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
                ExpiresAt=Time.time+ConditionalDefaults.EventWindow};
            if(position.HasValue){ctx.HasPosition=true;ctx.Position=position.Value;}
            // Damage/death identifies its source; only retain an already observed static body position.
            else if(source>=0&&visible[source]){ctx.HasPosition=true;ctx.Position=contacts[source].Bounds.center;}
            events.Add(new Notice {Condition=condition,At=Time.time,Context=ctx});
        }
        bool Start(int op,ConditionalRule rule,Running r)
        {
            string action=rule.Action;
            bool targetAction=op==2||action=="M01"||action=="M02"||action=="M03"||action=="D02";
            // Explicit Auto Aim/Aim Assist strategies retain their own visible target policy.
            r.Target=targetAction?Choose(op==2||!r.Context.EnemyBound?allVisible:r.Context.Candidates,action):-1;
            if(action=="Q03")r.Target=r.Context.Source>=0&&visible[r.Context.Source]?r.Context.Source:-1;
            r.Aim=raw.CommittedAim;
            switch((ControlOperation)op)
            {
                case ControlOperation.Movement:
                    if((action=="M01"||action=="M02"||action=="M03")&&r.Target<0)return false;
                    if(action=="M04")
                    {
                        r.Direction=movement.FacingDirection*(rule.ShortMoveDirection==ShortMoveDirectionOption.Forward?1:-1);
                        r.Destination=body.position.x+r.Direction*ConditionalDefaults.StepBack;
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
                    if(!gun.isActiveAndEnabled||gun.IsReloading||gun.AmmoInMagazine<=0)return false;
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
                    WriteFireRequest(rule);return true;
                case ControlOperation.Dodge:
                    if(!dodge.CanDodge)return false;
                    if(action=="D01") {if(!SourcePoint(r,out var source))return false;r.Direction=Away(source.x);}
                    else if(action=="D02") {if(r.Target<0)return false;r.Direction=-Away(contacts[r.Target].Bounds.center.x);}
                    else if(action=="D03")r.Direction=Mathf.Abs(output.Move)>.01f?Mathf.Sign(output.Move):movement.MovementFacingDirection;
                    else if(action=="D04") {if(Mathf.Abs(raw.Cursor.x-body.position.x)<.01f)return false;r.Direction=Mathf.Sign(raw.Cursor.x-body.position.x);}
                    else throw new InvalidOperationException("Unknown Dodge response");
                    if(dodge.AvailableTravel(r.Direction,dodge.Distance)<.08f)return false;
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
            if(r.Target>=0&&(!visible[r.Target]||!actors[r.Target].IsAlive)
                &&action!="Q06"&&action!="Q09")return false;
            switch((ControlOperation)op)
            {
                case ControlOperation.Movement:return WriteMovement(action,r);
                case ControlOperation.Aim:return WriteAim(action,r);
                case ControlOperation.Fire:
                    if(action=="F06"){output.FireOwned=true;output.Fire=false;Accept(op,r);return true;}
                    if(action=="F02"&&r.Accepted)return false; // Only the weapon may continue a burst.
                    if(!gun.isActiveAndEnabled||gun.IsReloading||gun.AmmoInMagazine<=0)return false;
                    if(!r.Accepted&&!gun.CanFireNow)return false;
                    if(action!="F03"&&r.Shots>=r.ShotLimit)return false;
                    WriteFireRequest(rule);return true;
                case ControlOperation.Jump:
                    if(!CanJump())return false;
                    output.JumpOwned=true;output.Jump=true;return true;
                case ControlOperation.Dodge:return false;
                default:throw new InvalidOperationException("Unknown running operation");
            }
        }
        void WriteFireRequest(ConditionalRule rule)
        {
            output.FireOwned=true;output.Fire=true;output.FireActionId=rule.Action;
            output.FireRequest=rule.Action=="F02"?WeaponFireRequest.Burst
                :rule.Action=="F03"?WeaponFireRequest.Continuous:WeaponFireRequest.Single;
        }
        bool CompleteShortMove(Running r)
        {
            if(!r.Accepted||r.Completed||(body.position.x-r.Destination)*r.Direction<-.005f)return false;
            r.Completed=true;Record(ConditionalCatalog.MoveFinishedCondition);return true;
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
                case "M01":
                    // Own a stable zero near horizontal alignment, even across platform heights.
                    r.ApproachStopped=distance<=(r.ApproachStopped?.30f:.15f);
                    move=r.ApproachStopped?0:toward;break;
                case "M02":move=-toward;break;
                case "M03":
                    var state=states[0,r.Row];
                    if(state.RangeMode<0&&distance>=ConditionalDefaults.Close+ConditionalDefaults.Hysteresis
                        ||state.RangeMode>0&&distance<=ConditionalDefaults.Far-ConditionalDefaults.Hysteresis)state.RangeMode=0;
                    if(state.RangeMode==0)state.RangeMode=distance<ConditionalDefaults.Close?-1:distance>ConditionalDefaults.Far?1:0;
                    if(state.RangeMode==0)return false;
                    move=state.RangeMode*toward;break;
                case "M04":
                    if(r.Completed||CompleteShortMove(r))return false;
                    move=r.Direction*Mathf.Clamp01(Mathf.Abs(r.Destination-x)/(movement.MoveSpeed*Time.fixedDeltaTime));break;
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
            r.PreviousX=x;output.MovementOwned=true;output.Move=move;
            output.MovementKeepsFacing=action=="M04";return true;
        }
        bool WriteAim(string action,Running r)
        {
            // A current public candidate set, never a hidden target lock.
            if(action=="Q01"||action=="Q02")
            {r.Target=Choose(allVisible,action);if(r.Target<0)return false;}
            if(action=="Q06"||action=="Q09")
            {
                int best=Choose(allVisible,action);
                float cone=action=="Q06"?ConditionalDefaults.AimAssistCone:ConditionalDefaults.MouseExit;
                bool valid=r.Target>=0&&visible[r.Target]&&MouseAngle(r.Target)<=cone;
                if(!valid||best>=0&&MouseAngle(best)+2f<MouseAngle(r.Target))r.Target=best;
                if(r.Target<0)return false;
            }
            output.AimOwned=true;output.Aim=action=="Q08"?r.Aim:AimAt(r.Target);
            if(action=="Q06")
            {
                // Smooth only the additive assistance: the raw mouse direction responds immediately.
                // Reach 95% of a stationary correction in .15 combat seconds, without prediction.
                float desired=Vector2.SignedAngle(raw.Aim,output.Aim);
                float blend=1-Mathf.Exp(-Time.deltaTime/ConditionalDefaults.AimAssistTimeConstant);
                r.AssistOffset=Mathf.Lerp(r.AssistOffset,desired,blend);
                // Never pull past the body or oppose a mouse movement across the target.
                r.AssistOffset=Mathf.Clamp(r.AssistOffset,Mathf.Min(0,desired),Mathf.Max(0,desired));
                output.Aim=((Vector2)(Quaternion.Euler(0,0,r.AssistOffset)*raw.Aim.normalized)).normalized;
            }
            else if(action=="Q09")
                output.Aim=((Vector2)Vector3.RotateTowards(raw.Aim.normalized,output.Aim,
                    ConditionalDefaults.MouseEnter*Mathf.Deg2Rad,0)).normalized;
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
                if(op==1){if(!output.Drop)AcceptedJumps++;if(r.Platform!=null)r.Platform.Launched=true;}
                if(op==4)AcceptedDodges++;return;
            }
            // Losing a physical attempt never refunds an already consumed occurrence.
            r.Accepted=false;
            if(op==0||op==2)return;
            r.Row=-1;
        }
    }
}
