using System;
using UnityEngine;

namespace GoGhost.Duel
{
    // Presentation snapshots derived from observed matches and actual execution.
    public sealed partial class ConditionalController
    {
        public readonly struct ExecutionFeedback
        {
            public readonly bool Executing;
            public readonly float LastExecutedAt;
            public readonly ConditionalVisualFeedback Visual;
            public ExecutionFeedback(bool executing,float at,ConditionalVisualFeedback visual=default) {Executing=executing;LastExecutedAt=at;Visual=visual;}
            public float Glow => Executing?1:Mathf.Clamp01((LastExecutedAt+.5f-Time.time)/.15f);
        }
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
    }
}
