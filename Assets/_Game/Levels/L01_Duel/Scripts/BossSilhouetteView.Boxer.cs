using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossSilhouetteView
    {
        Vector2 commandPalm, heldPivot;
        float heldAngle;
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
                float lift=Ease(age/.39f), release=Ease((age-.64f)/.36f);
                var aim=LocalDirection(boxer.Destination-actor.Position).normalized;
                p.FrontHand=Vector2.Lerp(new Vector2(.23f,.08f),new Vector2(-.20f,.55f),lift);
                p.FrontHand=Vector2.Lerp(p.FrontHand,p.Chest+aim*.71f,release);
                p.WeaponAngle=Mathf.LerpAngle(Mathf.Lerp(72,149,lift),Mathf.Atan2(aim.y,aim.x)*Mathf.Rad2Deg+22,release);
                p.Hip.x+=.09f*release; p.Chest.x+=.14f*release-.09f*lift; p.Head.x+=.08f*release;
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
            if(boxer.Gesturing)
            {
                var direction=LocalDirection(boxer.CommandDirection).normalized;
                var reach=p.Chest+direction*.66f;
                float t=boxer.GestureProgress;
                float extend=Ease(t/.24f)*(1-Ease((t-.70f)/.30f));
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
            if (boxer.Gesturing)
                DrawEmpRing(14,commandPalm,Mathf.Lerp(.52f,.065f,Ease(boxer.GestureProgress)),.058f,NeuralPalette.Emp);
            if (boxer.InFlight)
            {
                DrawAxeTrail(boxer.AxeEdge,true);
                Vector2 velocity=(boxer.Destination-boxer.AxePivot).normalized;
                SetLine(AxeLine(8,"Violet axle wake",2),boxer.AxePivot-velocity*.75f,boxer.AxePivot,
                    .065f,NeuralPalette.Alpha(NeuralPalette.Emp,.70f));
            }
            else { trailPoints.Clear(); trailTimes.Clear(); }
            float plantedAge=Time.time-boxer.PlantedAt;
            if (plantedAge<.26f)
            {
                float t=plantedAge/.26f;
                Vector2 away=-Angle(boxer.AxeAngle-boxer.BladeSide*22f);
                for (int i=0;i<5;i++)
                {
                    Vector2 ray=Rotate(away, (i-2)*28f);
                    Vector2 start=boxer.Destination+ray*(.06f+t*.72f);
                    SetLine(AxeLine(8+i,"Embedded axe sparks",2),start,start+ray*((1-t)*.36f),
                        .043f,NeuralPalette.Alpha(NeuralPalette.Emp,1-t));
                }
            }
            DrawFistTrace(boxer);
            DrawMeteorImpact(boxer);
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
            var line=AxeLine(0,"Rainbow fist trail",fistTrace.Count);
            ApplyPunchSpectrum(line);
            float weight=Mathf.Clamp01(1-(now-fistTimes[fistTimes.Count-1])/.21f);
            line.startWidth=.035f*weight; line.endWidth=(boxer.Current==FinalBossBoxer.Punch.Jab?.19f:.29f)*weight;
            for(int i=0;i<fistTrace.Count;i++) line.SetPosition(i,ClipTellPoint(fistTrace[i],.04f));
            var tip=fistTrace[fistTrace.Count-1];
            var direction=(tip-fistTrace[0]).normalized;
            var normal=new Vector2(-direction.y,direction.x);
            // Parallel bands follow only the distance already travelled by the fist.
            float length=Mathf.Min(Vector2.Distance(tip,fistTrace[0]),1.65f);
            for(int i=0;i<3;i++)
            {
                float n=(i-1)*.085f;
                var end=tip+normal*n;
                SetLine(AxeLine(i+1,"Fist speed bands",2),end-direction*length+normal*n,end,.03f*weight,
                    Spectrum(RainbowPhase+i*.29f,weight*.80f));
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
