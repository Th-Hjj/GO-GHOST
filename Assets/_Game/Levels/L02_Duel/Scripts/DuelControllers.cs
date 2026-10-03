using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Facts expressed by bodies/projectiles only. Neither policy receives the other's policy or permissions.
    public static class DuelPerception
    {
        public static int Toward(FighterView self, FighterView other) => Mathf.Abs(other.Position.x - self.Position.x) < .08f ? self.Facing : other.Position.x > self.Position.x ? 1 : -1;
        public static float Distance(FighterView a, FighterView b) => Mathf.Abs(a.Position.x - b.Position.x);
        public static float EmissionY(FighterView shooter,float delay) => shooter.Grounded ? shooter.Position.y : Mathf.Max(0,shooter.Feet+shooter.Velocity.y*delay-10.4f*delay*delay)+1f;
        public static float RiseTime(float clearance)
        {
            float discriminant=DuelRules.JumpSpeed*DuelRules.JumpSpeed-2*DuelRules.Gravity*Mathf.Max(0,clearance);
            return discriminant<=0?float.PositiveInfinity:(DuelRules.JumpSpeed-Mathf.Sqrt(discriminant))/DuelRules.Gravity;
        }
        public static float BackDodgeContact(float eta,float speed)
        {
            float contact=eta;
            for(int i=0;i<5;i++)contact=eta+DuelRules.DodgeTravel(contact)/speed;
            return contact;
        }
        public static bool HorizontalHit(FighterView shooter, FighterView target, float delay,float emissionDelay=.12f)
        {
            float feet = Mathf.Max(0, target.Feet + target.Velocity.y * delay - (target.Grounded ? 0 : .5f * DuelRules.Gravity * delay * delay));
            float line = EmissionY(shooter,emissionDelay);
            return line + .13f >= feet && line - .13f <= feet + DuelRules.H;
        }
        public static float Threat(FighterView self, FighterView foe, IReadOnlyList<DuelProjectile> shots, out Strike type,out float clearance)
        {
            float best = float.PositiveInfinity; type = Strike.None; clearance=0;
            int towardSelf = Toward(foe, self);
            float distance = Distance(self, foe);
            if (foe.Windup && (!foe.Locked || foe.Facing == towardSelf))
            {
                float wait = DuelRules.Windup(foe.Attack) - foe.Elapsed;
                if (foe.Attack == Strike.Intercept && distance < 3.2f && self.Feet < foe.Feet + 2.4f)
                { best = wait; type = Strike.Intercept; }
                else if (foe.Attack != Strike.Intercept && distance < DuelRules.Range)
                {
                    float arrival = wait + Mathf.Max(0, distance - 1f) / (foe.Attack == Strike.Wave ? 60 : 40);
                    bool intersects = foe.Attack == Strike.Wave ? self.Grounded || self.Feet + self.Velocity.y * arrival - 10.4f * arrival * arrival < 1.2f : HorizontalHit(foe, self, arrival,wait);
                    if (intersects) { best = arrival; type = foe.Attack; clearance=foe.Attack==Strike.Wave?1.22f:EmissionY(foe,wait)+.10f-self.Feet; }
                }
            }
            for (int i = 0; i < shots.Count; i++)
            {
                var p = shots[i];
                if (!p.Alive || Mathf.Sign(p.Direction) != Mathf.Sign(self.Position.x - p.Position.x)) continue;
                float arrival = Mathf.Max(0, Mathf.Abs(self.Position.x - p.Position.x) - .65f) / p.Speed;
                if (arrival >= best) continue;
                float feet = Mathf.Max(0, self.Feet + self.Velocity.y * arrival - (self.Grounded ? 0 : 10.4f * arrival * arrival));
                if (p.Position.y + p.Height * .5f < feet || p.Position.y - p.Height * .5f > feet + 2) continue;
                best = arrival; type = p.Attack;clearance=p.Position.y+p.Height*.5f+.02f-self.Feet;
            }
            return best;
        }
    }

    public sealed class BuiltInController
    {
        float quietOpportunity;
        public ControlIntent Decide(FighterView self, FighterView foe, IReadOnlyList<DuelProjectile> hostile)
        {
            int toward = DuelPerception.Toward(self, foe), away = -toward;
            float d = DuelPerception.Distance(self, foe);
            var intent = new ControlIntent { Aim = toward };
            float eta = DuelPerception.Threat(self, foe, hostile, out Strike threat,out float clearance);
            bool danger = !float.IsPositiveInfinity(eta);
            // Midrange hysteresis band; no travel-dodging or idle hopping.
            intent.Move = d > 5.2f ? toward : d < 3.7f ? away : 0;
            if (self.BackSpace(away) < .4f && d < 3.7f) intent.Move = 0;
            if (danger && self.Grounded && self.Free)
            {
                if (threat == Strike.Wave)
                {
                    float dodgeContact = DuelPerception.BackDodgeContact(eta,DuelRules.WaveSpeed);
                    if (self.Charges > 0 && self.BackSpace(away) > DuelRules.DodgeDistance + .12f)
                    {
                        if (dodgeContact <= .30f && dodgeContact >= .19f)
                        { intent.Dodge = true; intent.DodgeDirection = away; intent.Move = 0; }
                    }
                    else if (eta <= .20f && eta >= .12f)
                    {
                        if (self.Charges > 0 && d < 3.3f && self.BackSpace(toward) > 5f)
                        { intent.Dodge = true; intent.DodgeDirection = toward; }
                        else intent.Jump = true;
                    }
                }
                else if (threat == Strike.Shot)
                {
                    float rise=DuelPerception.RiseTime(clearance)+.035f;
                    if(eta>=rise && eta<=rise+.07f)
                    {intent.Jump=true;if(self.BackSpace(away)<4.9f&&d<3.3f)intent.Move=toward;}
                    else if(eta<rise && self.Charges>0)
                    {
                        float contact=self.BackSpace(away)>4.92f?DuelPerception.BackDodgeContact(eta,40):eta;
                        if(contact>=.18f&&contact<=.28f){intent.Dodge=true;intent.DodgeDirection=self.BackSpace(away)>4.92f?away:toward;intent.Move=0;}
                    }
                }
                else if (threat == Strike.Intercept && eta < .25f && d < 3.2f)
                {
                    if (d + 6 * eta > 3.25f) intent.Move = away;
                    else if (self.Charges > 0) { intent.Dodge = true; intent.DodgeDirection = self.BackSpace(away) > 4.9f ? away : toward; }
                }
            }
            if (self.Action == BodyAction.Dodge) intent.Move = 0;
            if (self.Action == BodyAction.Attack && self.Windup && self.Attack != Strike.Shot)
            {
                bool lostRange = self.Attack == Strike.Intercept ? d > 3.6f : !foe.Grounded && foe.Velocity.y > 0;
                if ((lostRange || (danger && eta < .26f)) && self.Elapsed <= DuelRules.CancelLimit(self.Attack)) intent.Attack = self.Attack;
                return intent;
            }
            if (!self.Free || intent.Dodge || intent.Jump) { quietOpportunity=0; return intent; }
            quietOpportunity = danger || foe.Action != BodyAction.Idle ? 0 : quietOpportunity + Time.fixedDeltaTime;
            if (d < 2.7f && foe.Feet < self.Feet + 2.2f && (!danger || eta > .3f)) intent.Attack = Strike.Intercept;
            else if (self.Grounded && foe.Grounded && foe.Remaining > .56f + Mathf.Max(0,d-1)/60 && d < 7f && !danger) intent.Attack = Strike.Wave;
            else
            {
                float arrival = .12f + Mathf.Max(0, d - 1) / 40;
                // Leave enough time to make a legitimate defensive commitment after our recovery.
                float landing = (self.Velocity.y + Mathf.Sqrt(self.Velocity.y*self.Velocity.y + 2*DuelRules.Gravity*Mathf.Max(0,self.Feet))) / DuelRules.Gravity;
                bool airOpportunity = !self.Grounded && (!danger || eta < landing + .14f);
                bool safeCommitment = airOpportunity || (danger ? eta > .72f : foe.Remaining + .12f + Mathf.Max(0,d-1)/40 > .56f || (quietOpportunity > .3f && d>7f));
                if (d < 20 && DuelPerception.HorizontalHit(self, foe, arrival) && safeCommitment) intent.Attack = Strike.Shot;
            }
            return intent;
        }
    }

    public sealed class PoliceController
    {
        enum Reaction { None, BackDodge, Jump }
        struct Habit { public Reaction Last; public int Count, Misses; }
        readonly Habit[] habits = new Habit[18];
        readonly int wave;
        readonly System.Action<string> trace;
        int observedAction = -1, bucket;
        float observationStart, lastWave = -10, nextDecision, suspensionUntil;
        bool watching, feint, cancelled;
        Reaction expected, actual;
        float punishUntil;
        public PoliceController(int wave, System.Action<string> trace) { this.wave = wave; this.trace = trace; }

        int Bucket(FighterView self, FighterView target)
        {
            float distance = DuelPerception.Distance(self, target);
            int band = distance < 5f ? 0 : distance < 9f ? 1 : 2;
            int away = -DuelPerception.Toward(target, self);
            return band * 6 + (target.BackSpace(away) > 4.92f ? 3 : 0) + target.Charges;
        }
        void Observe(FighterView self, FighterView target, float now)
        {
            if (!watching) return;
            if (actual == Reaction.None)
            {
                int away = -DuelPerception.Toward(target, self);
                if (target.DodgeAt >= observationStart && target.DodgeDirection == away) actual = Reaction.BackDodge;
                else if (target.JumpAt >= observationStart) actual = Reaction.Jump;
                if (actual != Reaction.None) trace($"observation reaction={actual} bucket={bucket}");
            }
            if (now - observationStart < .95f) return;
            var h = habits[bucket];
            if (feint)
            {
                if (actual != expected)
                {
                    h.Misses++;
                    if (h.Misses >= 2) { h.Count = 0; h.Last = Reaction.None; h.Misses = 0; suspensionUntil = now + 2; trace("targeting suspended; two absent expected reactions"); }
                }
                else h.Misses = 0;
            }
            if (!feint && now >= suspensionUntil)
            {
                h.Count = actual != Reaction.None && actual == h.Last ? h.Count + 1 : actual == Reaction.None ? 0 : 1;
                h.Last = actual;
                if (h.Count == 2) trace($"habit established {h.Last} bucket={bucket}");
            }
            habits[bucket] = h; watching = false;
        }

        ControlIntent Heavy(ControlIntent request,FighterView self,FighterView target,float now)
        {
            request.Attack=Strike.Wave;request.Move=0;
            bucket=Bucket(self,target);
            expected=habits[bucket].Count>=2?habits[bucket].Last:Reaction.None;
            feint=wave==3&&target.Free&&now>=suspensionUntil&&expected!=Reaction.None&&DuelPerception.Distance(self,target)<4.6f;
            cancelled=false;observedAction=self.ActionId+1;
            watching=wave==3&&target.Free;
            if(watching)
            {
                actual=Reaction.None;observationStart=now;
                trace($"comparable observation bucket={bucket} d={DuelPerception.Distance(self,target):0.00} charges={target.Charges}");
                trace($"plan before windup={(feint?"feint":"real")} expected={expected}");
            }
            lastWave=now;nextDecision=now+.46f;return request;
        }

        public ControlIntent Decide(FighterView self, FighterView target, IReadOnlyList<DuelProjectile> hostile, float now)
        {
            if (wave == 3) Observe(self, target, now);
            int toward = DuelPerception.Toward(self, target), away = -toward;
            float d = DuelPerception.Distance(self, target);
            var request = new ControlIntent { Aim = toward };
            float eta = DuelPerception.Threat(self, target, hostile, out Strike threat,out float clearance);
            float desired = wave == 1 ? 5.8f : 4.1f;
            request.Move = d > desired + .3f ? toward : d < desired - .5f ? away : 0;
            if (self.Action == BodyAction.Attack && self.Attack == Strike.Wave) request.Move = 0;
            if (feint && self.ActionId == observedAction && self.Action == BodyAction.Attack && self.Attack == Strike.Wave && !cancelled)
            {
                // Fixed before windup, never chosen in response to this attempt's jump/dodge.
                if (self.Elapsed >= .52f - .0001f) { request.Attack = Strike.Wave; cancelled = true; punishUntil = now + 1.15f; }
                return request;
            }
            if (!self.Free) return request;
            if (cancelled && now < punishUntil)
            {
                request.Move = toward;
                float flight = .12f + Mathf.Max(0, d - 1f) / 40;
                if (target.Action == BodyAction.Dodge && target.Elapsed + flight >= .31f && target.Elapsed + flight < .66f && DuelPerception.HorizontalHit(self, target, flight))
                { request.Attack = Strike.Shot; return request; }
                if (!target.Grounded && target.Velocity.y < 0 && DuelPerception.HorizontalHit(self, target, flight))
                { request.Attack = d < 2.7f ? Strike.Intercept : Strike.Shot; return request; }
                if (target.Grounded && target.Action != BodyAction.Dodge && now > observationStart + .9f) cancelled = false;
                else return request;
            }
            if(now>=nextDecision&&self.Grounded&&target.Grounded&&target.Free&&d>3.4f&&d<(wave==1?6f:4.6f)&&now-lastWave>1.8f&&eta>.62f)
                return Heavy(request,self,target,now);
            if (self.Grounded && eta < (wave == 1 ? .17f : .22f))
            {
                if (threat == Strike.Wave && eta > .13f) request.Jump = true;
                else if (threat == Strike.Shot && DuelPerception.BackDodgeContact(eta,40)>=.18f && DuelPerception.BackDodgeContact(eta,40)<=.29f && self.Charges>0 && self.BackSpace(away)>4.92f)
                {request.Dodge=true;request.DodgeDirection=away;return request;}
                else if (threat == Strike.Shot && eta > DuelPerception.RiseTime(clearance)+.02f) request.Jump = true;
                else if (self.Charges>0 && eta >= .17f) { request.Dodge = true; request.DodgeDirection = self.BackSpace(away) > 4.9f ? away : toward; return request; }
                if(request.Jump)return request;
            }
            if (now < nextDecision) return request;
            // Create a comparable pressure opportunity by waiting out a visible short attack.
            // This is ordinary distance/timing management, not a forced reaction or an observation.
            if(wave>=2 && self.Grounded && target.Grounded && !target.Free && target.Remaining<.44f &&
                d>3.4f && d<4.6f && now-lastWave>1.8f)
            { request.Move=0; return request; }
            // Wave 2/3 punish expressed recovery and descending arcs. No hidden input lookup.
            if (d < 2.7f && target.Feet < self.Feet + 2.3f)
                request.Attack = Strike.Intercept;
            else if (wave >= 2 && target.Action == BodyAction.Dodge && target.Elapsed > .28f && DuelPerception.HorizontalHit(self, target, .12f + d / 40))
                request.Attack = Strike.Shot;
            else if (!target.Grounded)
            {
                // At heavy-pressure range, keep the spacing and challenge the landing with
                // a real ground threat. Outside it, punish a descending horizontal opening.
                if(wave>=2 && self.Grounded && d<6f && now-lastWave>1.8f)return request;
                if ((wave == 1 || target.Velocity.y < 0) && DuelPerception.HorizontalHit(self, target, .12f + d / 40)) request.Attack = Strike.Shot;
            }
            else if (self.Grounded && target.Grounded && d > 3.4f && d < (wave == 1 ? 10f : 4.6f) && now - lastWave > 1.8f)
            {
                return Heavy(request,self,target,now);
            }
            else if (d < 20 && (wave==1 || target.Remaining>.20f) && DuelPerception.HorizontalHit(self, target, .12f + d / 40)) request.Attack = Strike.Shot;
            // Deliberate probes leave room for movement and a response. This is a policy decision,
            // not a different cooldown: either side's executor still permits a shot every .42s.
            if (request.Attack != Strike.None) nextDecision = now + (request.Attack==Strike.Shot ? (wave==1?1.1f:.95f) : .46f);
            return request;
        }
    }
}
