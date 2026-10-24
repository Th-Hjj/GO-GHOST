using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossSilhouetteView
    {
        bool swordPoseReady;
        float swordDisplayAngle;
        // Physical blade appearance is fixed; the slash arc expresses the larger attack reach.
        const float SwordBladeLength=1.5f;
        static float SwordTellWidth(bool active,bool locked)=>active?.06f:locked?.025f:.02f;
        Color SwordTellColor(bool active,bool locked)
        {
            Color color=active?Color.Lerp(bodyColor,NeuralPalette.Primary,.65f):bodyColor;
            color.a=active?.95f:locked?.8f:.3f;
            return color;
        }
        void SwordPose(ref Pose p,BossActionExecutor a)
        {
            p.FrontFoot.x=.33f;p.BackFoot.x=-.40f;
            if(a.SwordResidualWaiting)
            {p.Chest.x=.04f;p.Head.x=.03f;p.FrontHand=new Vector2(.22f,.12f);p.WeaponAngle=25;}
            else if(a.Phase==BossActionPhase.Recovery)
            {p.Chest.x=.10f;p.Head.x=.12f;p.FrontHand=new Vector2(.30f,-.25f);p.WeaponAngle=-35;}
            else if(a.CurrentSwordBeat==SwordBeat.JumpCut)
            {
                p.Chest.x=-.06f;p.Head.y+=.04f;p.FrontHand=LocalPoint(a.SwordBladeRoot);
                Vector2 direction=LocalDirection(a.SwordBladeTip-a.SwordBladeRoot).normalized;
                p.WeaponAngle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                if(a.Phase!=BossActionPhase.Active){p.Hip.y-=.10f;p.FrontHand.y+=.12f;}
            }
            else if(a.CurrentSwordBeat==SwordBeat.Draw||a.CurrentSwordBeat==SwordBeat.Traverse)
            {
                p.Hip.y-=.16f;p.Chest.y-=.14f;p.Head.y-=.12f;
                p.Chest.x+=a.Phase==BossActionPhase.Active?.18f:.03f;
                p.FrontHand=new Vector2(-.12f,-.10f);p.WeaponAngle=a.Phase==BossActionPhase.Active?Mathf.Atan2(a.Aim.y,a.Aim.x*actor.Facing)*Mathf.Rad2Deg:-165;
            }
            else if(a.CurrentSwordBeat==SwordBeat.Wave)
            {p.Chest.x=-.08f;p.FrontHand=new Vector2(-.13f,.30f);p.WeaponAngle=a.Phase==BossActionPhase.Active?0:155;}
            else
            {
                Vector2 direction=LocalDirection(a.SwordBladeTip-a.SwordBladeRoot).normalized;
                p.WeaponAngle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
                p.FrontHand=LocalPoint(a.SwordBladeRoot)+direction*.16f;
                if(a.CurrentSwordBeat==SwordBeat.DownCut){p.Chest.x=-.05f;p.Head.y+=.04f;}
            }
            // Blend wind-up transitions, but keep the active blade exactly on its damaging arc.
            if(swordPoseReady&&a.Phase==BossActionPhase.Prepare)
                p.WeaponAngle=Mathf.LerpAngle(swordDisplayAngle,p.WeaponAngle,1-Mathf.Exp(-Time.deltaTime*24));
            swordPoseReady=true;swordDisplayAngle=p.WeaponAngle;
            bool cut=a.CurrentSwordBeat==SwordBeat.JumpCut||a.CurrentSwordBeat==SwordBeat.LowCut
                ||a.CurrentSwordBeat==SwordBeat.RisingCut||a.CurrentSwordBeat==SwordBeat.DownCut;
            if(cut)
            {
                Vector2 root=LocalPoint(a.SwordBladeRoot),direction=Angle(p.WeaponAngle);
                p.FrontHand=root+direction*.15f;p.Weapon=root+direction*SwordBladeLength*.5f;
                if(a.Phase==BossActionPhase.Active){p.Chest.x+=.10f;p.Head.x+=.06f;}
            }
            else p.Weapon=p.FrontHand+Angle(p.WeaponAngle)*SwordBladeLength*.5f;
            p.BackHand=p.FrontHand+new Vector2(-.10f,-.035f);
        }
        void SwordTells(BossActionExecutor action)
        {
            int slot=0;
            foreach(var threat in action.PublicThreats)
            {
                if(threat.AttackId!=action.AttackId||slot>=4||action.Phase==BossActionPhase.Recovery)continue;
                // Wave bodies carry their own charge/flight effect; omit projected travel guides.
                if(threat.Shape==BossThreatShape.MovingBlade)continue;
                if(threat.Shape==BossThreatShape.BladeArc)
                {
                    // The body pose carries early preparation. Show the local arc only near
                    // the actual swing, never a future airborne/advancing strike destination.
                    if(action.CurrentSwordBeat==SwordBeat.JumpCut)
                    {if(action.Phase!=BossActionPhase.Active||Time.time<threat.StartsAt-.2f)continue;}
                    else if(action.Phase==BossActionPhase.Prepare)continue;
                }
                bool active=threat.Phase==BossThreatPhase.Active,locked=threat.Phase==BossThreatPhase.Locked;
                Color color=SwordTellColor(active,locked);
                if(tells[slot]==null)tells[slot]=NewLine("SwordCommitment",false,2);
                var line=tells[slot++];
                if(threat.Shape==BossThreatShape.BladeArc)
                {
                    line.enabled=true;line.positionCount=21;
                    float finish=active?action.SwordCutProgress:1,start=active?Mathf.Max(0,finish-.3f):0;
                    for(int i=0;i<21;i++)
                    {
                        float progress=Mathf.Lerp(start,finish,i/20f);
                        Vector2 root=action.SwordBladeRoot;
                        float angle=Mathf.Lerp(threat.ArcFrom,threat.ArcTo,progress)*Mathf.Deg2Rad;
                        Vector2 tip=root+new Vector2(threat.Facing*Mathf.Cos(angle),Mathf.Sin(angle))*threat.Reach;
                        if(Projectile.TerrainHit(root,tip,threat.Radius,out var wall))tip=root+(tip-root).normalized*wall.distance;
                        line.SetPosition(i,ClipTellPoint(tip,SwordTellWidth(active,locked)));
                    }
                    line.startWidth=line.endWidth=SwordTellWidth(active,locked);line.startColor=line.endColor=color;
                }
                else SetLine(line,threat.Origin,threat.End,SwordTellWidth(active,locked),color);
            }
            slot=6;
            if(action.SwordTraces==null)return;
            foreach(var trace in action.SwordTraces)
            {
                if(slot+3>=tells.Length)break;
                bool warning=!trace.Deferred&&Time.time>=trace.WarningAt;
                bool active=!trace.Deferred&&Time.time>=trace.Threat.StartsAt;
                // Emitted lane waves share the afterblade's active core, glow and trails.
                // Their original preparation and immediate emission timing stay unchanged.
                Vector2 a=trace.A(Time.time),b=trace.B(Time.time),centre=(a+b)*.5f;
                float charge=Mathf.Clamp01((Time.time-trace.BornAt)/Mathf.Max(.01f,trace.Threat.StartsAt-trace.BornAt));
                float pulse=warning?.8f+.2f*Mathf.Sin(Time.time*32):1;
                if(tells[slot]==null)tells[slot]=NewLine("AfterbladeGlow",true,2);
                SetLine(tells[slot++],a,b,active?.24f:.10f+.07f*charge,NeuralPalette.Alpha(bodyColor,(active?.85f:warning?.7f:.3f)*pulse));
                if(tells[slot]==null)tells[slot]=NewLine("AfterbladeCore",true,2);
                SetLine(tells[slot++],Vector2.Lerp(centre,a,active?1:.45f+.55f*charge),Vector2.Lerp(centre,b,active?1:.45f+.55f*charge),
                    active?.065f:.025f+.025f*charge,NeuralPalette.Alpha(NeuralPalette.Primary,active?1:warning?.95f:.5f));
                if(tells[slot]==null)tells[slot]=NewLine("AfterbladeDirection",false,2);
                tells[slot].enabled=active;
                Vector2 trail=-trace.Threat.Direction*(active?1.35f:0);
                if(active)SetLine(tells[slot],a,a+trail,.045f,NeuralPalette.Alpha(bodyColor,.55f));
                slot++;
                if(tells[slot]==null)tells[slot]=NewLine("AfterbladeTail",false,2);
                tells[slot].enabled=active;
                if(active)SetLine(tells[slot],b,b+trail,.045f,NeuralPalette.Alpha(bodyColor,.55f));
                slot++;
            }
        }
    }
}
