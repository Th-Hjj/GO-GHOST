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
        float fistFan;
        readonly List<Vector2> fistTrace = new List<Vector2>(24);
        readonly List<float> fistTimes = new List<float>(24);
        Gradient punchSpectrum;
        internal void GetHeldAxe(out Vector2 pivot,out float angle) { pivot=heldPivot; angle=heldAngle; }

        void BoxerPose(ref Pose p,FinalBossBoxer boxer,float wave,float motion)
        {
            if (!boxer.Detached)
            {
                float age=boxer.Orders==0 ? 0 : Time.time-boxer.CommandAt;
                float lift=Ease(age/.27f), release=Ease((age-.70f)/.30f);
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
                    float amount=prep ? Ease(boxer.PhaseProgress) : boxer.Phase==FinalBossBoxer.Stage.Recovery ? 1-Ease(boxer.PhaseProgress) : 1;
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

        void DrawBoxer(FinalBossBoxer boxer)
        {
            float age=Time.time-boxer.CommandAt;
            if (!boxer.Retired && age<CommandPoseSeconds)
            {
                float pinch=Ease((age-.32f)/.18f);
                DrawEmpRing(14,commandPalm,Mathf.Lerp(.54f,.07f,pinch),.063f,
                    Color.Lerp(NeuralPalette.EmpHighlight,NeuralPalette.Emp,pinch));
            }
            if (boxer.Detached && !boxer.InFlight)
                DrawSpiritContact(boxer);
            DrawFistTrace(boxer);
            DrawMeteorImpact(boxer);
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
        void DrawFistTrace(FinalBossBoxer boxer)
        {
            float now=Time.time;
            if (boxer.Phase==FinalBossBoxer.Stage.Active)
            {
                if (lastStroke!=boxer.Stroke)
                {
                    fistTimes.Clear(); fistTrace.Clear(); lastStroke=boxer.Stroke;
                    fistFan=boxer.Current==FinalBossBoxer.Punch.Jab && !boxer.Cross ? .10f : .19f;
                    fistTrace.Add(boxer.StrokeStart); fistTimes.Add(now-.025f);
                }
                Vector2 fist=boxer.Fist();
                if (fistTrace.Count==0 || (fistTrace[fistTrace.Count-1]-fist).sqrMagnitude>.0004f)
                {
                    fistTrace.Add(fist); fistTimes.Add(now);
                }
            }
            while (fistTimes.Count>0 && (now-fistTimes[0]>.21f || fistTimes.Count>24))
            { fistTimes.RemoveAt(0); fistTrace.RemoveAt(0); }
            if (fistTrace.Count<2) return;
            float weight=Mathf.Clamp01(1-(now-fistTimes[fistTimes.Count-1])/.21f);
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
                line.startWidth=(main ? .039f : .029f)*weight;
                line.endWidth=(main ? .085f : .046f)*weight;
                for(int i=0;i<=last;i++)
                {
                    Vector2 dir=(fistTrace[Mathf.Min(last,i+1)]-fistTrace[Mathf.Max(0,i-1)]).normalized;
                    var normal=new Vector2(-dir.y,dir.x);
                    // Open the traces behind the strike and bring them together at the knuckles.
                    float spread=fistFan*(1-Ease(i/(float)last));
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
