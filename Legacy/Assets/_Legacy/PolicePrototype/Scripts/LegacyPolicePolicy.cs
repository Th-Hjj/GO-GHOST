using System;
using System.Collections.Generic;
using UnityEngine;

// Small encounter-local memory. All arguments are copied public observations or the police's own state.
public sealed class LegacyPolicePolicy
{
    enum Reaction { None, Retreat, BackDodge, JumpLeft, JumpRight, LeaveLeft, LeaveRight, FireToReload }
    enum Tactic { None, RetreatPressure, RoutePressure, ReloadLure }
    sealed class Habit { public Reaction Last; public int Count,Misses; public float SuspendedUntil; }
    readonly Dictionary<int,Habit> habits=new Dictionary<int,Habit>();
    readonly int wave;
    readonly Action<string> trace;
    Tactic tactic;
    int stage, tacticKey, watchedKey, lastShotSerial, lastReloadSerial, streamShots, lureShotBaseline;
    Reaction expected, response;
    LegacyPoliceMove pressure;
    LegacyPlayerPublicFacts watchStart;
    Vector2 policeAtWatch;
    float watchUntil, deadline, lastShotAt=-100, streamStarted, lastPressureAt=-100, lureDestination;
    float setupDestination,setupDeadline,prepareRetryAt,lastDefenceAt=-100;
    float evasionDestination,evasionUntil;
    bool repositioningFromShot;
    LegacyPoliceMove lastStarted;
    bool watching, targetedWatch, observedExpected;

    public LegacyPolicePolicy(int wave,Action<string> trace) { this.wave=wave;this.trace=trace; }
    Habit Memory(int key) { if(!habits.TryGetValue(key,out var h)){h=new Habit();habits.Add(key,h);}return h; }
    static int Side(float value)=>value<0?-1:1;
    static float Space(LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s,int direction)
        => direction>0?s.Arena.max.x-p.Body.max.x:p.Body.min.x-s.Arena.min.x;
    static int Context(LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s)
    {
        float distance=Mathf.Abs(p.Position.x-s.Position.x);
        int away=Side(p.Position.x-s.Position.x);
        return (distance<8?0:1)*8+(p.OnPlatform?4:0)+(p.Grounded?0:2)+(Space(p,s,away)>3.5f?1:0);
    }
    static int PressureKey(LegacyPoliceMove move,LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s)
        => ((int)move)*32+Context(p,s);
    static int ReloadKey(LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s)
    {
        int region=p.Position.x<s.Arena.center.x-4?0:p.Position.x>s.Arena.center.x+4?2:1;
        return 1000+region*4+(p.OnPlatform?2:0)+(Mathf.Abs(p.Position.x-s.Position.x)<9?0:1);
    }
    bool Learned(int key,float now,Reaction wanted=Reaction.None)
    {
        var h=Memory(key);
        return h.Count>=2&&now>=h.SuspendedUntil&&(wanted==Reaction.None||h.Last==wanted);
    }
    void Record(int key,Reaction actual,float now)
    {
        var h=Memory(key);
        if(now<h.SuspendedUntil)return;
        h.Count=actual==Reaction.None?0:actual==h.Last?h.Count+1:1;h.Last=actual;
        if(h.Count==2)trace?.Invoke($"habit learned key={key} reaction={actual}");
    }
    void FinishTarget(bool expectedSeen,float now,string reason)
    {
        var h=Memory(tacticKey);
        if(expectedSeen)h.Misses=0;
        else if(++h.Misses>=2)
        {
            h.Count=0;h.Last=Reaction.None;h.Misses=0;h.SuspendedUntil=now+2f;
            trace?.Invoke($"targeting suspended key={tacticKey}; two absent reactions; fresh observations required");
        }
        trace?.Invoke($"tactic exit={tactic} expectedSeen={expectedSeen} reason={reason}; hits are independent");
        tactic=Tactic.None;stage=0;observedExpected=false;
    }
    void AbortPreparation(float now,string reason)
    {
        trace?.Invoke($"prepare exit={tactic} reason={reason}; no induced reaction was requested");
        tactic=Tactic.None;stage=0;observedExpected=false;prepareRetryAt=now+.6f;
    }

    public void Observe(LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s,float now)
    {
        if(wave!=3)return;
        if(p.ShotSerial!=lastShotSerial)
        {
            bool plausible=(s.Position-p.LastMuzzle).sqrMagnitude>.01f&&
                Vector2.Dot((s.Position-p.LastMuzzle).normalized,p.LastShotDirection)>.88f;
            if(now-lastShotAt>.7f||!plausible){streamShots=0;streamStarted=now;}
            if(plausible)streamShots+=Mathf.Max(1,p.ShotSerial-lastShotSerial);
            lastShotAt=p.LastShotAt;lastShotSerial=p.ShotSerial;
        }
        if(p.ReloadSerial!=lastReloadSerial)
        {
            if(streamShots>=4&&now-lastShotAt<.65f&&now-streamStarted>.5f)
            {
                int key=ReloadKey(p,s);Record(key,Reaction.FireToReload,now);
                trace?.Invoke($"observed sustained shooting to visible reload key={key} x={p.Position.x:0.0}");
            }
            if(tactic==Tactic.ReloadLure&&p.ReloadVisible&&ReloadKey(p,s)==tacticKey)
            {observedExpected=true;trace?.Invoke("lure response: real visible reload");}
            streamShots=0;lastReloadSerial=p.ReloadSerial;
        }
        if(!watching)return;
        int away=Side(watchStart.Position.x-policeAtWatch.x);
        float dx=p.Position.x-watchStart.Position.x;
        if(p.LastDodgeAt>watchStart.LastDodgeAt&&dx*away>.1f)response=Reaction.BackDodge;
        else if(watchStart.Grounded&&!p.Grounded&&p.Velocity.y>1f)
            response=p.Velocity.x<-.25f?Reaction.JumpLeft:Reaction.JumpRight;
        else if(watchStart.OnPlatform&&!p.OnPlatform&&Mathf.Abs(dx)>.5f)
            response=dx<0?Reaction.LeaveLeft:Reaction.LeaveRight;
        else if(dx*away>1.2f&&watchStart.Velocity.x*away<.5f)response=Reaction.Retreat;
        if(response!=Reaction.None||now>=watchUntil)
        {
            trace?.Invoke($"pressure observation key={watchedKey} actual={response}");
            if(targetedWatch)observedExpected=response==expected;
            else Record(watchedKey,response,now);
            watching=false;
        }
    }

    // Called only after the executor has actually accepted this normal attack.
    public void AttackStarted(LegacyPoliceMove move,LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s,float now)
    {
        lastStarted=move;
        if(move==LegacyPoliceMove.Burst||move==LegacyPoliceMove.Marked||move==LegacyPoliceMove.LowSweep||move==LegacyPoliceMove.HighSweep)
        {
            lastPressureAt=now;
            bool targeting=tactic==Tactic.RetreatPressure||tactic==Tactic.RoutePressure;
            if(targeting&&stage!=1)return;
            // A normal weapon switch can change the scene before the attack really begins.
            // Continue that legal attack, but do not invent a missing reaction to a different setup.
            if(targeting&&(p.Dodging||PressureKey(move,p,s)!=tacticKey||!PressureFits(move,p,s)))
            {
                watching=targetedWatch=false;
                AbortPreparation(now,"context changed during weapon switch");
            }
            if(wave==3&&!p.Dodging)
            {
                watchedKey=PressureKey(move,p,s);watchStart=p;policeAtWatch=s.Position;
                watchUntil=now+(move==LegacyPoliceMove.Burst?1.8f:2.3f);response=Reaction.None;watching=true;
                targetedWatch=tactic==Tactic.RetreatPressure||tactic==Tactic.RoutePressure;
                trace?.Invoke($"comparable pressure key={watchedKey} move={move} distance={Mathf.Abs(p.Position.x-s.Position.x):0.0}");
            }
        }
    }
    static bool IsJump(Reaction r)=>r==Reaction.JumpLeft||r==Reaction.JumpRight||r==Reaction.LeaveLeft||r==Reaction.LeaveRight;
    float Inside(LegacyPoliceSelfFacts s,float x)=>Mathf.Clamp(x,s.Arena.min.x+s.Body.extents.x+.2f,s.Arena.max.x-s.Body.extents.x-.2f);
    bool SelectTactic(LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s,float now)
    {
        if(wave!=3||now<prepareRetryAt)return false;
        float d=Mathf.Abs(p.Position.x-s.Position.x);
        int away=Side(s.Position.x-p.Position.x);
        int reloadKey=ReloadKey(p,s);
        float retreatDistance=d<9?Mathf.Min(2.5f,8.5f-d):2.5f;
        stage=0;
        if(d>4&&d<11&&(now-p.LastShotAt<.6f||p.ReloadVisible)&&Learned(reloadKey,now,Reaction.FireToReload)
            &&retreatDistance>1.3f&&Mathf.Abs(Inside(s,s.Position.x+away*retreatDistance)-s.Position.x)>1.3f)
        {
            tactic=Tactic.ReloadLure;tacticKey=reloadKey;expected=Reaction.FireToReload;deadline=now+5f;
            lureDestination=Inside(s,s.Position.x+away*retreatDistance);
            lureShotBaseline=p.ShotSerial;if(p.ReloadVisible)stage=-1;
        }
        else
        {
            LegacyPoliceMove[] candidates={p.OnPlatform?LegacyPoliceMove.HighSweep:LegacyPoliceMove.LowSweep,LegacyPoliceMove.Burst,LegacyPoliceMove.Marked};
            foreach(var move in candidates)
            {
                bool fits=move!=LegacyPoliceMove.HighSweep||p.Body.min.y<=s.HighSweepY+.15f&&p.Body.max.y>=s.HighSweepY-.15f;
                if(!fits||d<3||d>13)continue;
                // A learned pre-pressure situation may be recreated after the current jump/dodge ends.
                // Entry does not fabricate an observation; the exact context is checked again before attacking.
                foreach(var entry in habits)
                {
                    int context=entry.Key-((int)move)*32;
                    var h=entry.Value;
                    if(context<0||context>=16||h.Count<2||now<h.SuspendedUntil)continue;
                    bool platform=(context&4)!=0;
                    if(platform!=p.OnPlatform&&(platform||p.Grounded))continue;
                    int playerAway=Side(p.Position.x-s.Position.x);
                    if(((context&1)!=0)!=(Space(p,s,playerAway)>3.5f))continue;
                    if(IsJump(h.Last)){tactic=Tactic.RoutePressure;pressure=move;}
                    else if(h.Last==Reaction.Retreat||h.Last==Reaction.BackDodge){tactic=Tactic.RetreatPressure;pressure=move;}
                    else continue;
                    tacticKey=entry.Key;expected=h.Last;deadline=now+5f;setupDeadline=now+1.7f;
                    float desired=(context&8)!=0?9f:tactic==Tactic.RoutePressure?6.5f:6f;
                    setupDestination=Inside(s,p.Position.x+away*desired);break;
                }
                if(tactic!=Tactic.None)break;
            }
        }
        if(tactic==Tactic.None)return false;
        observedExpected=false;
        trace?.Invoke($"plan before inducement={tactic} key={tacticKey} expected={expected}");
        return true;
    }

    public LegacyPoliceOrder Decide(LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s,float now)
    {
        float d=Mathf.Abs(p.Position.x-s.Position.x);int toward=Side(p.Position.x-s.Position.x);
        if(repositioningFromShot)
        {
            if(now<evasionUntil&&Mathf.Abs(s.Position.x-evasionDestination)>.4f)
                return LegacyPoliceOrder.Go(LegacyPoliceMove.Walk,evasionDestination);
            repositioningFromShot=false;
        }
        if(tactic==Tactic.None)SelectTactic(p,s,now);
        if(tactic!=Tactic.None)
        {
            if(now>deadline){if(stage<=0)AbortPreparation(now,"preparation expired");else FinishTarget(observedExpected,now,"opportunity expired");}
            else if(tactic==Tactic.ReloadLure)
            {
                if(stage==-1)
                {
                    if(!p.ReloadVisible&&p.ShotSerial>lureShotBaseline&&now-p.LastShotAt<.6f)stage=0;
                    else return LegacyPoliceOrder.Act(LegacyPoliceMove.Wait);
                }
                if(stage==0){stage=1;return LegacyPoliceOrder.Go(LegacyPoliceMove.Ghost,lureDestination);}
                if(stage==1){stage=2;return LegacyPoliceOrder.Act(LegacyPoliceMove.EquipBlade);}
                if(stage==2&&p.ReloadVisible&&observedExpected&&d<8&&Mathf.Abs(p.Position.y-s.Position.y)<1.8f)
                {stage=3;return LegacyPoliceOrder.Act(d<3.5f?LegacyPoliceMove.Slash:LegacyPoliceMove.Crosscut);}
                if(stage==3){FinishTarget(true,now,"legal approach completed");}
                else if(observedExpected&&!p.ReloadVisible){FinishTarget(true,now,"reload ended before approach; no fabricated opening");}
                else if(now-p.LastShotAt>.8f&&!p.ReloadVisible){FinishTarget(false,now,"player stopped shooting");}
                else return d>5.5f?LegacyPoliceOrder.Go(LegacyPoliceMove.Walk,Inside(s,p.Position.x-toward*5f)):LegacyPoliceOrder.Act(LegacyPoliceMove.Wait);
            }
            else
            {
                if(stage==0)
                {
                    // The route is chosen before the provoking attack, and remains physically ordinary movement.
                    if(now>setupDeadline)AbortPreparation(now,"could not arrange a comparable position");
                    else if(Mathf.Abs(setupDestination-s.Position.x)>.7f)return LegacyPoliceOrder.Go(LegacyPoliceMove.Walk,setupDestination);
                    else if(!p.Dodging&&PressureKey(pressure,p,s)==tacticKey&&PressureFits(pressure,p,s))
                    {stage=1;return LegacyPoliceOrder.Act(pressure);}
                    else if(!p.Grounded||p.Dodging)return LegacyPoliceOrder.Act(LegacyPoliceMove.Wait);
                    else AbortPreparation(now,"context changed before pressure");
                }
                else if(stage==1)
                {
                    if(watching)return LegacyPoliceOrder.Act(LegacyPoliceMove.Wait);
                    if(!observedExpected){FinishTarget(false,now,"expected response absent");}
                    else
                    {
                        stage=2;
                        if(tactic==Tactic.RetreatPressure)
                            return LegacyPoliceOrder.Act(d<6&&Mathf.Abs(p.Position.y-s.Position.y)<1.4f?LegacyPoliceMove.Crosscut:LegacyPoliceMove.Marked);
                        // Predict only from expressed motion. No final landing point or future input is supplied.
                        float fall=(p.Velocity.y+Mathf.Sqrt(p.Velocity.y*p.Velocity.y+2*29.43f*Mathf.Max(0,p.Body.min.y-s.FloorY)))/29.43f;
                        float predicted=Inside(s,p.Position.x+p.Velocity.x*Mathf.Min(fall,.65f));
                        if(p.Body.min.y>s.FloorY+1.4f&&d<11)return LegacyPoliceOrder.Act(LegacyPoliceMove.Drop);
                        if(d<5&&Mathf.Abs(p.Position.y-s.Position.y)<1.3f)return LegacyPoliceOrder.Act(LegacyPoliceMove.Crosscut);
                        if(Mathf.Abs(predicted-s.Position.x)>9)
                        {stage=3;return LegacyPoliceOrder.Go(LegacyPoliceMove.Ghost,Inside(s,predicted-toward*5f));}
                        return LegacyPoliceOrder.Act(LegacyPoliceMove.Marked);
                    }
                }
                else if(stage==3){stage=2;return LegacyPoliceOrder.Act(p.OnPlatform?LegacyPoliceMove.Drop:LegacyPoliceMove.Marked);}
                else {FinishTarget(true,now,"follow-up completed; hit result independent");}
            }
        }
        // A real incoming shot prompts a legal positional response, not immunity or a cancelled attack.
        // One reposition is followed by normal pressure before another defensive reposition is considered.
        if(s.Incoming&&lastStarted!=LegacyPoliceMove.Ghost&&now-lastDefenceAt>1f)
        {
            if(s.Body.min.y>s.FloorY+.6f&&s.Support.size.x>0)
            {
                float edge=s.Position.x<s.Support.center.x?s.Support.min.x-s.Body.extents.x-.2f:s.Support.max.x+s.Body.extents.x+.2f;
                lastDefenceAt=now;repositioningFromShot=true;evasionDestination=Inside(s,edge);evasionUntil=now+.9f;
                return LegacyPoliceOrder.Go(LegacyPoliceMove.Walk,evasionDestination);
            }
            float retreat=Inside(s,s.Position.x-toward*3.5f);
            if(Mathf.Abs(retreat-s.Position.x)>2f)
            {lastDefenceAt=now;return LegacyPoliceOrder.Go(LegacyPoliceMove.Ghost,retreat);}
        }
        // Basic is direct gunplay. Skilled adds reactive opportunities; neither fabricates learning.
        if(wave>=2)
        {
            if(p.ReloadVisible&&d<8&&Mathf.Abs(p.Position.y-s.Position.y)<1.7f)
                return LegacyPoliceOrder.Act(d<3.5f?LegacyPoliceMove.Slash:LegacyPoliceMove.Crosscut);
            if(p.OnPlatform&&d<11&&now-lastPressureAt>1.6f)return LegacyPoliceOrder.Act(LegacyPoliceMove.Drop);
            if(!p.Grounded&&p.Velocity.y<-.5f&&d<5&&p.Body.min.y<s.Body.max.y+.4f)return LegacyPoliceOrder.Act(LegacyPoliceMove.Crosscut);
            if(p.LastDodgeAt>0&&now-p.LastDodgeAt<.8f&&d<5&&Mathf.Abs(p.Position.y-s.Position.y)<1.2f)
                return LegacyPoliceOrder.Act(LegacyPoliceMove.Slash);
        }
        if(d>11)return LegacyPoliceOrder.Go(LegacyPoliceMove.Walk,Inside(s,p.Position.x-toward*8f));
        if(d<3.2f)
        {
            float retreat=Inside(s,s.Position.x-toward*4f);
            if(Mathf.Abs(retreat-s.Position.x)>1.5f)return LegacyPoliceOrder.Go(LegacyPoliceMove.Ghost,retreat);
            return LegacyPoliceOrder.Act(wave>=2?LegacyPoliceMove.Crosscut:LegacyPoliceMove.Burst);
        }
        bool lowLane=p.Grounded&&!p.OnPlatform&&p.Body.min.y<s.FloorY+1.1f;
        bool highLane=p.Body.min.y<=s.HighSweepY+.15f&&p.Body.max.y>=s.HighSweepY-.15f;
        if(lowLane&&Mathf.Abs(p.Velocity.x)<1.5f)return LegacyPoliceOrder.Act(LegacyPoliceMove.LowSweep);
        if(highLane&&p.OnPlatform)return LegacyPoliceOrder.Act(LegacyPoliceMove.HighSweep);
        if(Mathf.Abs(p.Velocity.x)>3f||p.OnPlatform)return LegacyPoliceOrder.Act(LegacyPoliceMove.Marked);
        return LegacyPoliceOrder.Act(LegacyPoliceMove.Burst);
    }
    static bool PressureFits(LegacyPoliceMove move,LegacyPlayerPublicFacts p,LegacyPoliceSelfFacts s)
    {
        float height=move==LegacyPoliceMove.HighSweep?s.HighSweepY:s.FloorY+.85f;
        return move!=LegacyPoliceMove.HighSweep&&move!=LegacyPoliceMove.LowSweep
            ||p.Body.min.y<=height+.1f&&p.Body.max.y>=height-.1f;
    }
}
