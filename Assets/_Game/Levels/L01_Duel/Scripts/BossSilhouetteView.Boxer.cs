using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossSilhouetteView
    {
        Vector2 commandPalm, heldPivot;
        float heldAngle;
        const float CommandPoseSeconds=.50f;
        int lastStroke;
        readonly List<Vector2> fistTrace = new List<Vector2>(24);
        readonly List<float> fistTimes = new List<float>(24);
        Gradient punchSpectrum;
        internal void GetHeldAxe(out Vector2 pivot,out float angle) { pivot=heldPivot; angle=heldAngle; }

        void BoxerPose(ref Pose p,FinalBossBoxer boxer,float wave,float motion)
        {
            if (!boxer.Detached)
            {
                float age=boxer.Orders==0 ? 0 : Time.time-boxer.CommandAt;
                // The opening throw leaves the hand half a second after its order.
                float lift=Ease(age/.20f), release=Ease((age-.32f)/.18f);
                float loaded=lift*(1-release);
                var aim=LocalDirection(boxer.Destination-actor.Position).normalized;
                p.FrontHand=Vector2.Lerp(new Vector2(.23f,.08f),new Vector2(-.45f,.63f),lift);
                p.FrontHand=Vector2.Lerp(p.FrontHand,p.Chest+aim*.71f,release);
                p.WeaponAngle=Mathf.LerpAngle(Mathf.Lerp(72,151,lift),Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg+22,release);
                p.FrontFoot.x+=.19f*loaded; p.BackFoot.x-=.12f*loaded;
                p.Hip+=new Vector2(.09f*release-.10f*loaded,-.065f*loaded);
                p.Chest.x+=.14f*release-.15f*loaded; p.Head.x+=.08f*release-.07f*loaded;
                p.BackHand=new Vector2(.30f,.26f);
            }
            else
            {
                p.Hip.y-=.055f;
                p.Chest+=new Vector2(.08f,-.035f);
                p.Head+=new Vector2(.055f,-.025f);
                p.FrontHand=FinalBossBoxer.Guard+new Vector2(.045f*wave*motion,.02f*wave);
                p.BackHand=new Vector2(-.02f,.33f);
                if (boxer.Current!=FinalBossBoxer.Punch.None)
                {
                    bool prep=boxer.Phase==FinalBossBoxer.Stage.Windup || boxer.Phase==FinalBossBoxer.Stage.Slip;
                    // The Meteor's locked hold keeps the full wind-up pose.
                    float amount=prep ? (boxer.DiveLocked ? 1 : Ease(boxer.PhaseProgress)) : boxer.Phase==FinalBossBoxer.Stage.Recovery ? 1-Ease(boxer.PhaseProgress) : 1;
                    float twist=prep ? -.14f : .19f, dip=prep ? -.07f : 0;
                    switch(boxer.Current)
                    {
                        case FinalBossBoxer.Punch.Uppercut: dip=prep ? -.21f : .10f; break;
                        case FinalBossBoxer.Punch.Backfist: twist=prep ? -.27f : .23f; break;
                        case FinalBossBoxer.Punch.Leap:
                            p.BackFoot-=new Vector2(.24f,-.10f)*amount;
                            p.FrontFoot+=new Vector2(.18f,.12f)*amount;
                            twist=prep ? -.08f : .25f; break;
                        case FinalBossBoxer.Punch.Meteor:
                            twist=prep ? -.12f : .32f; dip=prep ? .03f : -.19f;
                            p.BackFoot+=new Vector2(-.22f,.14f)*amount; break;
                    }
                    p.Hip+=new Vector2(twist*.38f,dip)*amount;
                    p.Chest+=new Vector2(twist,dip)*amount;
                    p.Head+=new Vector2(twist*.76f,dip)*amount;
                    if(boxer.Cross || boxer.Current==FinalBossBoxer.Punch.Backfist)
                        p.BackHand=boxer.HandLocal;
                    else p.FrontHand=boxer.HandLocal;
                }
                else if (boxer.Cover!=FinalBossBoxer.CoverStage.None)
                {
                    // Both fists in front of the face; when nothing came, they drop and leave him open.
                    bool open=boxer.Cover==FinalBossBoxer.CoverStage.Open;
                    float up=boxer.Cover==FinalBossBoxer.CoverStage.Raise ? Ease(boxer.CoverProgress) : 1;
                    float down=open ? Ease(Mathf.Clamp01(boxer.CoverProgress*3)) : 0;
                    p.FrontHand=Vector2.Lerp(Vector2.Lerp(p.FrontHand,new Vector2(.27f,.50f),up),new Vector2(.30f,-.14f),down);
                    p.BackHand=Vector2.Lerp(Vector2.Lerp(p.BackHand,new Vector2(.19f,.44f),up),new Vector2(.08f,-.20f),down);
                    p.Chest+=new Vector2(.03f,-.03f)*(up-down); p.Head+=new Vector2(.03f,-.04f)*(up-down);
                }
            }
            float ageSinceOrder=Time.time-boxer.CommandAt;
            if(ageSinceOrder<CommandPoseSeconds && !boxer.Retired)
            {
                var direction=LocalDirection(boxer.CommandDirection).normalized;
                var reach=p.Chest+direction*.76f;
                float extend=Ease(ageSinceOrder/.15f)*(1-Ease((ageSinceOrder-.38f)/.12f));
                // The arm may already be chambering the next punch while the ring is visible.
                if (boxer.Current!=FinalBossBoxer.Punch.None && (boxer.Cross || boxer.Current==FinalBossBoxer.Punch.Backfist))
                    extend*=boxer.Phase==FinalBossBoxer.Stage.Windup ? 1-Ease(boxer.PhaseProgress) : 0;
                p.BackHand=Vector2.Lerp(p.BackHand,reach,extend);
            }
            commandPalm=transform.TransformPoint(p.BackHand);
            var forward=Angle(p.WeaponAngle);
            heldPivot=transform.TransformPoint(p.FrontHand+forward*GripToFlightPivot);
            Vector2 shaft=transform.TransformVector(forward);
            heldAngle=Mathf.Atan2(shaft.y,shaft.x)*Mathf.Rad2Deg;
        }

        float lockFlashFor=-1;
        Gradient guardSpectrum;
        readonly GradientColorKey[] guardColours=new GradientColorKey[6];
        readonly GradientAlphaKey[] guardAlphas=new GradientAlphaKey[2];
        void DrawBoxer(FinalBossBoxer boxer)
        {
            // One language for every warning of this Boss (Thomas, 2026-11-15): the pose and one white step
            // for the body, a line to the destination for what travels far, and the axe's own light for the
            // axe. No rings and no marks at a destination. Lines use the Swordsman's widths and steps.
            // An ordered axe lights up in two steps: when the order is given, and again at 60% of the delay.
            if (chargedWeapon!=null)
                chargedWeapon.SetFloat("_AxeLit",boxer.Retired || !boxer.AxeOrdered ? 0 : boxer.LaunchProgress>=.6f ? .8f : .35f);
            if (boxer.Detached && !boxer.InFlight)
                DrawSpiritContact(boxer);
            // A landing edge carries a rim light, like every other place to stand.
            if (boxer.PerchActive)
                SetLine(AxeLine(29,"Axe perch rim",2),boxer.PerchLeft,boxer.PerchRight,SwordTellWidth(false,true),NeuralPalette.Alpha(NeuralPalette.Primary,.85f));
            if (boxer.Covering)
            {
                // Defense shows a thin boundary, as the Swordsman's guard does. His is in his own rainbow,
                // flowing along the arc with his body (Thomas, 2026-11-15; it was the axe's violet).
                bool held=boxer.Cover!=FinalBossBoxer.CoverStage.Raise;
                var arc=AxeLine(5,"Guard boundary",11);
                float width=SwordTellWidth(false,held), facing=boxer.LockedFacing>0 ? 0 : 180;
                arc.startWidth=arc.endWidth=width;
                if (guardSpectrum==null) guardSpectrum=new Gradient();
                for (int i=0;i<guardColours.Length;i++) guardColours[i]=new GradientColorKey(Spectrum(RainbowPhase+i*.16f),i/(guardColours.Length-1f));
                guardAlphas[0]=new GradientAlphaKey(held ? .8f : .3f,0); guardAlphas[1]=new GradientAlphaKey(held ? .8f : .3f,1);
                guardSpectrum.SetKeys(guardColours,guardAlphas);
                arc.colorGradient=guardSpectrum;
                Vector2 centre=(Vector2)actor.Position+Vector2.up*.25f;
                for(int i=0;i<11;i++) arc.SetPosition(i,ClipTellPoint(centre+Angle(facing+Mathf.Lerp(-60,60,i/10f))*1.1f,width));
                if (boxer.Cover==FinalBossBoxer.CoverStage.Warn)
                {
                    // The whole dash, from the fist to where it ends. The white step on his body, when the
                    // shot was stopped, is the rest of the warning.
                    SetLine(AxeLine(13,"Counter warning",2),boxer.Fist(),boxer.CounterEnd,SwordTellWidth(false,true),AxeTellColor(false,true));
                }
            }
            DrawFistTrace(boxer);
            DrawMeteorImpact(boxer);
            DrawBoxerWarnings(boxer);
        }

        // Phase two warns as phase one does: the ordered axe's flight line, the stroke of the punch being
        // wound up, and the Meteor's dive with its landing wave.
        void DrawBoxerWarnings(FinalBossBoxer boxer)
        {
            if (boxer.Retired) return;
            if (boxer.AxeOrdered)
            {
                // The axe's route in its own violet: dim when ordered, bright from 60% of the delay.
                bool locked=boxer.LaunchProgress>=.6f;
                Vector2 from=boxer.Detached ? boxer.AxeEdge : heldPivot;
                SetLine(AxeLine(6,"Axe order warning",2),from,boxer.Destination,SwordTellWidth(false,locked),
                    NeuralPalette.Alpha(NeuralPalette.Emp,locked ? .8f : .3f));
            }
            bool winding=boxer.Phase==FinalBossBoxer.Stage.Windup || boxer.Phase==FinalBossBoxer.Stage.Slip;
            if (boxer.Rising && boxer.Phase==FinalBossBoxer.Stage.Windup && boxer.RisingArc.Count>2)
            {
                // The Rising Uppercut warns with one smooth curve from the fist to the top of the jump.
                // It is drawn from those two points, not from the jump's sampled path, so it cannot kink.
                var path=boxer.RisingArc;
                int top=1;
                while (top<path.Count-1 && path[top+1].y>=path[top].y) top++;
                bool locked=boxer.PhaseProgress>=.6f;
                const int samples=16;
                var line=AxeLine(10,"Rising warning",samples+1);
                float width=SwordTellWidth(false,locked);
                line.startWidth=line.endWidth=width;
                line.startColor=line.endColor=AxeTellColor(false,locked);
                Vector2 start=boxer.Fist(), peak=path[top]+new Vector2(boxer.LockedFacing*.30f,.85f);
                for(int i=0;i<=samples;i++)
                {
                    float t=i/(float)samples, rise=1-(1-t)*(1-t);
                    line.SetPosition(i,ClipTellPoint(new Vector2(Mathf.Lerp(start.x,peak.x,t),Mathf.Lerp(start.y,peak.y,rise)),width));
                }
            }
            // An ordinary punch draws no line (Thomas, 2026-11-15: the short stroke line read as a mistake).
            // The wind-up pose is the warning, and the body shows one white step as the punch locks in.
            if (boxer.Current!=FinalBossBoxer.Punch.None && boxer.Phase==FinalBossBoxer.Stage.Windup
                && boxer.PhaseProgress>=.6f && !Mathf.Approximately(lockFlashFor,boxer.PhaseStartedAt))
            {
                lockFlashFor=boxer.PhaseStartedAt;
                LockFlash();
            }
            if (boxer.Diving)
            {
                // The dive line alone; the ground wave is drawn when he lands.
                SetLine(AxeLine(8,"Meteor dive warning",2),boxer.Fist(),boxer.DiveLanding,SwordTellWidth(false,true),AxeTellColor(false,true));
            }
        }

        void DrawSpiritContact(FinalBossBoxer boxer)
        {
            Vector2 normal=boxer.AnchorNormal, tangent=new Vector2(-normal.y,normal.x);
            Vector2 contact=boxer.AnchorPoint+normal*.10f;
            float t=Mathf.Clamp01((Time.time-boxer.PlantedAt)/.34f);
            if (t>=1) return;
            for (int i=0;i<2;i++)
            {
                Vector2 along=tangent*(i==0 ? -1 : 1);
                float lead=Mathf.Lerp(.16f,1.36f,Ease(t));
                float tail=Mathf.Lerp(.05f,1.33f,Ease(t*t));
                SetLine(AxeLine(11+i,"Violet contact sweep",2),contact+along*tail,
                    contact+along*lead,.07f,NeuralPalette.Alpha(NeuralPalette.EmpHighlight,1-t*t));
            }
        }

        static Color Spectrum(float phase,float alpha=1)
        {
            Color c=Color.HSVToRGB(Mathf.Repeat(phase,1),.80f,1);
            c.a=alpha; return c;
        }
        internal void RecordFistSweep(FinalBossBoxer boxer, Vector2 point)
        {
            if (lastStroke != boxer.Stroke)
            {
                fistTimes.Clear(); fistTrace.Clear(); lastStroke = boxer.Stroke;
                fistTrace.Add(boxer.StrokeStart); fistTimes.Add(Time.time);
            }
            if (fistTrace.Count > 0 && (fistTrace[fistTrace.Count-1]-point).sqrMagnitude < .0001f) return;
            fistTrace.Add(point); fistTimes.Add(Time.time);
        }

        void DrawFistTrace(FinalBossBoxer boxer)
        {
            float now=Time.time;
            if (boxer.Phase != FinalBossBoxer.Stage.Active || boxer.Stroke != lastStroke) return;
            while (fistTimes.Count>1 && (now-fistTimes[0]>.14f || fistTimes.Count>24))
            { fistTimes.RemoveAt(0); fistTrace.RemoveAt(0); }
            if (fistTrace.Count == 0) return;
            // The contact rim also reads when a diving fist is held at full extension.
            var rim = AxeLine(3,"Fist contact",25);
            float radius = FinalBossBoxer.FistRadius;
            rim.startWidth = rim.endWidth = .048f;
            rim.startColor = rim.endColor = Color.Lerp(Spectrum(RainbowPhase), Color.white, .60f);
            Vector2 tip = fistTrace[fistTrace.Count-1];
            for (int i=0;i<25;i++)
                rim.SetPosition(i,ClipTellPoint(tip+Angle(i*15f)*(radius-.024f),.048f));
            if (fistTrace.Count<2) return;
            int last=fistTrace.Count-1;
            for(int strand=-1;strand<=1;strand++)
            {
                bool main=strand==0;
                var line=AxeLine(strand+1,"Rainbow fist stroke",fistTrace.Count);
                if (main) ApplyPunchSpectrum(line);
                else
                {
                    var color=Spectrum(RainbowPhase+strand*.26f);
                    line.startColor=NeuralPalette.Alpha(color,.20f);
                    line.endColor=NeuralPalette.Alpha(color,.94f);
                }
                line.startWidth=main ? .08f : .045f;
                line.endWidth=main ? radius*1.22f : .075f;
                for(int i=0;i<=last;i++)
                {
                    Vector2 dir=(fistTrace[Mathf.Min(last,i+1)]-fistTrace[Mathf.Max(0,i-1)]).normalized;
                    var normal=new Vector2(-dir.y,dir.x);
                    // Open the traces behind the strike and bring them together at the knuckles.
                    float spread=radius*Mathf.Lerp(.78f,.45f,Ease(i/(float)last));
                    line.SetPosition(i,ClipTellPoint(fistTrace[i]+normal*(strand*spread),line.endWidth));
                }
            }
        }
        void ApplyPunchSpectrum(LineRenderer line)
        {
            if (punchSpectrum==null)
            {
                var keys=new GradientColorKey[6];
                for (int i=0;i<keys.Length;i++) keys[i]=new GradientColorKey(Spectrum(.78f-i*.135f),i/5f);
                punchSpectrum=new Gradient();
                punchSpectrum.SetKeys(keys,new[]{new GradientAlphaKey(.10f,0),new GradientAlphaKey(.92f,.40f),new GradientAlphaKey(1,1)});
            }
            line.colorGradient=punchSpectrum;
        }
        void DrawMeteorImpact(FinalBossBoxer boxer)
        {
            float age=Time.time-boxer.ImpactAt;
            if (age<0 || age>.40f) return;
            float t=age/.40f;
            Vector2 point=boxer.ImpactPoint;
            for(int i=0;i<7;i++)
            {
                float yaw=13f+i*(154f/6f);
                var ray=Angle(yaw);
                float extent=2.45f-.90f*Mathf.Sin(yaw*Mathf.Deg2Rad);
                Vector2 a=point+ray*extent*Ease(t);
                Vector2 b=point+ray*extent*Ease(t+.26f);
                SetLine(AxeLine(17+i,"Meteor impact rays",2),a,b,.11f*(1-t),Spectrum(i/7f+RainbowPhase,1-t));
            }
            var rim=AxeLine(25,"Meteor ground wave",29);
            rim.startWidth=rim.endWidth=.13f*(1-t);
            ApplyPunchSpectrum(rim);
            float radius=boxer.ImpactRadius;
            for(int i=0;i<29;i++)
            {
                float a=i*Mathf.PI/28;
                rim.SetPosition(i,ClipTellPoint(point+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.21f),.045f));
            }
        }
    }
}
