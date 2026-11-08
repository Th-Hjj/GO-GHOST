using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossSilhouetteView
    {
        // The approved 288 x 600 plate has its grip at (96, 192) in Unity texture coordinates.
        // The sprite uses a centred pivot so the dossier's UI copy and the world sprite agree.
        const float AxePixelsPerUnit = 299f;
        const float GripToCentreForward = (300f - 192f) / AxePixelsPerUnit;
        const float GripToCentreAcross = (144f - 96f) / AxePixelsPerUnit;
        const float GripToFlightPivot = .736f;
        const float GripToSocket = (523f-192f)/AxePixelsPerUnit;
        const float TrailLifetime = .16f;
        const int MaxTrailPoints = 22;
        static readonly Color FlightIce = new Color32(197, 237, 255, 255);
        Sprite originalWeaponSprite, phaseOneSprite;
        SpriteRenderer flightSprite;
        Vector2 phaseOneSocket, freePalm;
        readonly List<Vector2> trailPoints = new List<Vector2>(MaxTrailPoints);
        readonly List<float> trailTimes = new List<float>(MaxTrailPoints);
        readonly Vector2[] recentHands=new Vector2[24];
        readonly float[] handTimes=new float[24];
        int handHead,handCount;

        void PhaseOneAxePose(ref Pose p, FinalBossAxe axe, float wave, float motion)
        {
            Vector2 restingHand = new Vector2(.26f + .055f * wave * motion, .13f);
            p.FrontHand = restingHand;
            p.BackHand = new Vector2(-.13f - wave * motion * .08f, .02f);
            p.WeaponAngle = 56;
            if (axe.Current == FinalBossAxe.Move.None) return;

            bool prepare = axe.Phase == FinalBossAxe.Stage.Prepare;
            bool recover = axe.Phase == FinalBossAxe.Stage.Recovery;
            float weight = prepare ? Ease(axe.PhaseProgress) : recover ? 1 - Ease(axe.PhaseProgress) : 1;
            Vector2 grip = LocalPoint(axe.Hand());
            Vector2 direction = Angle(axe.HeldShaftAngle);
            direction.x *= axe.LockedFacing * actor.Facing;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            p.FrontHand = recover ? Vector2.Lerp(restingHand, grip, weight) : grip;
            p.WeaponAngle = recover ? Mathf.LerpAngle(56, angle, weight) : angle;
            if(axe.WindingCast)
            {
                WindUpCast(ref p,axe,restingHand,grip,angle);
                return;
            }
            if(prepare)
            {
                float raise = Ease(axe.PhaseProgress);
                p.FrontHand = Vector2.Lerp(restingHand, grip, raise);
                p.FrontHand += new Vector2(-.28f, .13f) * Mathf.Sin(raise * Mathf.PI);
            }
            float support = 0, lean = 0, crouch = 0;
            float reach = Mathf.Clamp(p.FrontHand.x / .55f, -1f, 1f);

            switch (axe.Current)
            {
                case FinalBossAxe.Move.TripleCut:
                    lean = .14f * reach;
                    crouch = .07f + .08f * Mathf.Clamp01(-direction.y);
                    if (axe.Phase == FinalBossAxe.Stage.Active || recover)
                        support = Mathf.InverseLerp(.60f, .96f, axe.ActiveTime);
                    // Preserve the walking legs while the body actually steps into
                    // the warning. Plant the guard as the forward stride ends.
                    float planted = prepare ? weight * (1 - motion) : weight;
                    p.FrontFoot.x = Mathf.Lerp(p.FrontFoot.x, .34f, planted);
                    p.BackFoot.x = Mathf.Lerp(p.BackFoot.x, -.41f, planted);
                    break;
                case FinalBossAxe.Move.LeapingCrosscut:
                    lean = actor.Motor.Grounded ? .10f : .17f;
                    crouch = actor.Motor.Grounded ? .12f : 0;
                    if (!actor.Motor.Grounded)
                    {
                        p.FrontFoot.x += .11f;
                        p.BackFoot += new Vector2(-.12f, .11f);
                    }
                    break;
                case FinalBossAxe.Move.UnderpassReturn:
                    lean = axe.AxeFlying ? -.08f : .12f * reach;
                    crouch = actor.Motor.Grounded ? .08f : .03f;
                    if (!actor.Motor.Grounded)
                    {
                        p.FrontFoot.y += .13f;
                        p.BackFoot.y += .16f;
                    }
                    break;
                case FinalBossAxe.Move.AnchorVault:
                    lean = axe.AnchorPulling ? .20f : .12f * reach;
                    crouch = actor.Motor.Grounded ? .12f : .02f;
                    if (axe.AnchorPulling)
                    {
                        p.BackFoot += new Vector2(-.22f, .15f);
                        p.FrontFoot.x -= .12f;
                    }
                    else if(axe.BracedOnAxe)
                    {
                        lean=-.12f; crouch=.14f;
                        p.FrontFoot+=new Vector2(.16f,.22f);
                        p.BackFoot+=new Vector2(-.12f,.12f);
                    }
                    else if(!actor.Motor.Grounded && !axe.AxeFlying)
                    {
                        lean=.18f;
                        p.BackFoot+=new Vector2(-.18f,.13f);
                    }
                    support = !prepare && !axe.AxeFlying ? 1 : 0;
                    break;
                case FinalBossAxe.Move.PalmDeflection:
                    lean = .12f * reach + axe.PalmAccent * .08f;
                    crouch = .075f;
                    support = !prepare && !axe.AxeFlying ? .85f : 0;
                    break;
            }
            p.Hip.y -= crouch * weight;
            p.Chest += new Vector2(lean, -crouch * .78f) * weight;
            p.Head += new Vector2(lean * 1.2f, -crouch * .75f) * weight;
            p.BackHand = Vector2.Lerp(p.BackHand, p.FrontHand - Angle(p.WeaponAngle) * .17f, support * weight);
        }

        // Reach the signature silhouette early, hold it, then release to the real grip.
        static void WindUpCast(ref Pose p, FinalBossAxe axe, Vector2 idleHand, Vector2 releaseHand, float releaseAngle)
        {
            Vector2 woundHand, offHand;
            float woundAngle, crouch;
            switch(axe.Current)
            {
                case FinalBossAxe.Move.UnderpassReturn:
                    woundHand=new Vector2(-.35f,-.17f); offHand=new Vector2(.25f,.10f);
                    woundAngle=174; crouch=.16f;
                    break;
                case FinalBossAxe.Move.AnchorVault:
                    woundHand=new Vector2(-.13f,.64f); offHand=new Vector2(-.21f,.50f);
                    woundAngle=108; crouch=.05f;
                    break;
                default: // Palm Deflection presents the empty palm ahead of the loaded axe.
                    woundHand=new Vector2(-.38f,.18f); offHand=new Vector2(.54f,.32f);
                    woundAngle=74; crouch=.07f;
                    break;
            }
            bool prepare=axe.Phase==FinalBossAxe.Stage.Prepare;
            float raise=prepare?Ease(axe.PhaseProgress/.55f):1f;
            float release=prepare?0:Ease(axe.PhaseProgress);
            float brace=raise*(1-release);
            p.FrontHand=Vector2.Lerp(Vector2.Lerp(idleHand,woundHand,raise),releaseHand,release);
            p.WeaponAngle=Mathf.LerpAngle(Mathf.LerpAngle(56,woundAngle,raise),releaseAngle,release);
            p.BackHand=Vector2.Lerp(p.BackHand,offHand,brace);
            p.FrontFoot.x=Mathf.Lerp(p.FrontFoot.x,.33f,brace);
            p.BackFoot.x=Mathf.Lerp(p.BackFoot.x,-.43f,brace);
            p.Chest+=new Vector2(-.14f,-crouch)*brace;
            p.Head+=new Vector2(-.13f,-crouch)*brace;
            p.Hip.y-=crouch*brace;
        }

        Sprite ApprovedAxe()
        {
            if (phaseOneSprite == null) phaseOneSprite = Resources.Load<Sprite>("FinalBossAxeSilhouette");
            return phaseOneSprite;
        }

        static Vector2 AxeSpriteCentre(Vector2 grip, Vector2 forward, float facing)
        {
            Vector2 towardEdge = new Vector2(forward.y, -forward.x) * facing;
            return grip + forward * GripToCentreForward + towardEdge * GripToCentreAcross;
        }

        void PlacePhaseOneAxe(Pose p)
        {
            var weapon = parts[(int)Part.Weapon];
            if (originalWeaponSprite == null) originalWeaponSprite = weapon.sprite;
            weapon.sprite = ApprovedAxe();
            weapon.enabled = true;
            weapon.color = NeuralPalette.Alpha(Color.white, alpha);
            Vector2 forward = Angle(p.WeaponAngle);
            weapon.transform.localPosition = AxeSpriteCentre(p.FrontHand, forward, 1);
            weapon.transform.localRotation = Quaternion.Euler(0, 0, p.WeaponAngle - 90);
            weapon.transform.localScale = Vector3.one;
            Hide(Part.Sheath);
            phaseOneSocket=transform.TransformPoint(p.FrontHand+forward*GripToSocket);
            freePalm=transform.TransformPoint(p.BackHand);
        }

        void UpdateFlyingAxe()
        {
            var axe = actor.Axe;
            if (axe.PhaseTwo || !axe.AxeFlying || defeated)
            {
                ClearPhaseOneFlight(); return;
            }
            if (flightSprite == null)
            {
                var go = new GameObject("Flying Axe");
                go.transform.SetParent(actor.transform, false);
                flightSprite = go.AddComponent<SpriteRenderer>();
                var held = parts[(int)Part.Weapon];
                flightSprite.sharedMaterial = held.sharedMaterial;
                flightSprite.sortingLayerID = held.sortingLayerID;
                flightSprite.sortingOrder = held.sortingOrder;
                flightSprite.sprite = ApprovedAxe();
            }
            flightSprite.enabled = true;
            flightSprite.flipX = axe.FlightFacing < 0;
            var forward = Angle(axe.AxeSpin);
            Vector2 grip = axe.AxePosition - forward * GripToFlightPivot;
            Vector2 centre = AxeSpriteCentre(grip, forward, axe.FlightFacing);
            flightSprite.transform.SetPositionAndRotation(centre, Quaternion.Euler(0, 0, axe.AxeSpin - 90));
        }

        void ClearPhaseOneFlight()
        {
            if (flightSprite != null) flightSprite.enabled = false;
            trailPoints.Clear();
            trailTimes.Clear();
        }

        void DrawPhaseOneAttack(FinalBossAxe axe)
        {
            if (axe.Current == FinalBossAxe.Move.None || axe.Phase == FinalBossAxe.Stage.Recovery) return;
            if(axe.WindingCast)DrawCastCharge(axe);
            RecordGrip(axe.Hand());
            if (axe.TryGetCut(out var cut, out bool locked, out bool active))
            {
                if(active||locked)DrawAxeCut(cut,active);
                else if(axe.Phase==FinalBossAxe.Stage.Active && !axe.AxeFlying &&
                    (axe.Current==FinalBossAxe.Move.LeapingCrosscut||axe.Current==FinalBossAxe.Move.AnchorVault))
                {
                    // The airborne wind-up reads on the raised weapon. Only the actual
                    // swing draws a blade arc; the body's flight has no danger guide.
                    float charge=Mathf.InverseLerp(cut.StartsAt-.22f,cut.StartsAt,Time.time);
                    Vector2 shaft=Angle(axe.HeldShaftAngle);
                    shaft.x*=axe.LockedFacing;
                    if(charge>0)SetLine(AxeLine(3,"Raised axe socket",2),axe.Hand()+shaft*.88f,
                        axe.Hand()+shaft*1.09f,.035f,NeuralPalette.Alpha(FlightIce,.28f+.62f*charge));
                }
            }
            if (axe.FlightWarning && axe.Path.Count >= 2)
            {
                var line = AxeLine(6, "Axe flight warning", axe.Path.Count);
                float width = SwordTellWidth(false,true);
                line.startWidth=width*1.45f;line.endWidth=width*.65f;
                line.startColor=NeuralPalette.Primary;line.endColor=NeuralPalette.Alpha(FlightIce,.55f);
                for (int i = 0; i < axe.Path.Count; i++) line.SetPosition(i, ClipTellPoint(axe.Path[i], width));
            }
            if(axe.FlightLive)DrawAxeTrail(axe.FlyingEdge);
            else {trailPoints.Clear();trailTimes.Clear();}
            if(axe.AnchorLatched)DrawLatchedAnchor(axe);
            if (axe.PalmAccent > 0)
            {
                Vector2 palm = axe.Hand();
                float angle=axe.Path.Count>1?Vector2.SignedAngle(Vector2.right,axe.Path[1]-axe.Path[0]):axe.AxeSpin;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 ray = Angle(angle+(i-1)*32);
                    Vector2 a = palm + ray * (.08f + .12f * (1 - axe.PalmAccent));
                    SetLine(AxeLine(9 + i, "Palm deflection", 2), a, a + ray * .13f, .025f,
                        NeuralPalette.Alpha(FlightIce, axe.PalmAccent));
                }
            }
        }

        void DrawCastCharge(FinalBossAxe axe)
        {
            float t=Ease(axe.CastProgress);
            float radius=Mathf.Lerp(.49f,.085f,t);
            const int points=32;
            var ring=AxeLine(14,"Axe socket wind-up",points);
            ring.loop=true;
            ring.startWidth=ring.endWidth=Mathf.Lerp(.045f,.065f,t);
            ring.startColor=ring.endColor=NeuralPalette.Alpha(FlightIce,.72f+.25f*t);
            for(int i=0;i<points;i++)
                ring.SetPosition(i,ClipTellPoint(phaseOneSocket+Angle(i*360f/points)*radius,ring.startWidth));
            SetLine(AxeLine(15,"Charged socket",2),phaseOneSocket-Vector2.up*.038f,phaseOneSocket+Vector2.up*.038f,
                .075f+.055f*t,NeuralPalette.Primary);
            if(axe.Current==FinalBossAxe.Move.PalmDeflection)
                SetLine(AxeLine(16,"Open palm",2),freePalm-Vector2.up*.055f,freePalm+Vector2.up*.095f,
                    .055f,NeuralPalette.Alpha(FlightIce,.9f));
        }

        void DrawLatchedAnchor(FinalBossAxe axe)
        {
            Vector2 forward=Angle(axe.AxeSpin);
            Vector2 grip=axe.AxePosition-forward*GripToFlightPivot;
            float tension=axe.AnchorLatchProgress;
            // A thin steel tether denotes travel. The two socket ticks close as the
            // pull engages. Neither has the white core of a live cut.
            SetLine(AxeLine(8,"Axe tether",2),axe.Hand(),grip,.012f+.008f*tension,
                NeuralPalette.Alpha(new Color32(111,129,145,255),.45f+.25f*tension));
            Vector2 normal=new Vector2(-forward.y,forward.x);
            for(int i=0;i<2;i++)
            {
                Vector2 root=axe.AxePosition+normal*((i==0?-1:1)*Mathf.Lerp(.36f,.19f,tension));
                SetLine(AxeLine(12+i,"Anchor clasp",2),root-forward*.09f,root+forward*.09f,
                    .026f,NeuralPalette.Alpha(FlightIce,.7f));
            }
        }

        void RecordGrip(Vector2 point)
        {
            float now=Time.time;
            if(handCount>0)
            {
                if(now==handTimes[handHead])return;
                if(now-handTimes[handHead]>.20f||now<handTimes[handHead])handCount=0;
            }
            handHead=(handHead+1)%recentHands.Length;
            recentHands[handHead]=point;handTimes[handHead]=now;
            handCount=Mathf.Min(handCount+1,recentHands.Length);
        }

        Vector2 GripAt(float t)
        {
            int next=handHead;
            for(int i=1;i<handCount;i++)
            {
                int previous=(next+recentHands.Length-1)%recentHands.Length;
                if(handTimes[previous]<=t)
                    return Vector2.Lerp(recentHands[previous],recentHands[next],Mathf.InverseLerp(handTimes[previous],handTimes[next],t));
                next=previous;
            }
            return recentHands[next];
        }

        LineRenderer AxeLine(int slot, string label, int points)
        {
            var line = tells[slot];
            if (line == null) tells[slot] = line = NewLine(label, false, points);
            line.loop = false;
            line.enabled = true;
            line.positionCount = points;
            return line;
        }

        void DrawAxeCut(BossThreat cut, bool active)
        {
            const int samples = 21;
            var line = AxeLine(0, "Axe blade arc", samples);
            float finish = active ? Mathf.InverseLerp(cut.StartsAt, cut.EndsAt, Time.time) : 1;
            float start = active ? Mathf.Max(0, finish - .30f) : 0;
            float width = SwordTellWidth(active, !active);
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = AxeTellColor(active, !active);
            for (int i = 0; i < samples; i++)
            {
                float step = Mathf.Lerp(start, finish, i / (samples - 1f));
                Vector2 ray = Angle(Mathf.Lerp(cut.ArcFrom, cut.ArcTo, step));
                ray.x *= cut.Facing;
                Vector2 root = active ? GripAt(Mathf.Lerp(cut.StartsAt, cut.EndsAt, step)) : actor.Axe.Hand();
                Vector2 tip = root + ray * cut.Reach;
                if (cut.TerrainBlocked && Projectile.TerrainHit(root, tip, cut.Radius, out var wall))
                    tip = root + ray * wall.distance;
                line.SetPosition(i, ClipTellPoint(tip, width));
                if (active && i == samples - 1 && Vector2.Distance(root,tip) > FinalBossAxe.PhysicalEdgeReach)
                {
                    var edge = AxeLine(1, "Axe energized edge", 2);
                    edge.startWidth = .095f;
                    edge.endWidth = .025f;
                    edge.startColor = FlightIce;
                    edge.endColor = NeuralPalette.Primary;
                    edge.SetPosition(0, root + ray * FinalBossAxe.PhysicalEdgeReach);
                    edge.SetPosition(1, ClipTellPoint(tip, edge.endWidth));
                }
            }
        }

        void DrawAxeTrail(Vector2 edge)
        {
            float now = Time.time;
            int expired = 0;
            while (expired < trailTimes.Count && trailTimes[expired] < now - TrailLifetime) expired++;
            if (expired > 0)
            {
                trailTimes.RemoveRange(0, expired);
                trailPoints.RemoveRange(0, expired);
            }
            if (trailPoints.Count == 0 || (trailPoints[trailPoints.Count - 1] - edge).sqrMagnitude > .0004f)
            {
                if (trailPoints.Count == MaxTrailPoints)
                {
                    trailPoints.RemoveAt(0); trailTimes.RemoveAt(0);
                }
                trailPoints.Add(edge); trailTimes.Add(now);
            }
            if (trailPoints.Count < 2) return;
            int first=trailPoints.Count-1;
            Vector2 tail=trailPoints[first];
            float length=1.15f;
            while(first>0 && length>0)
            {
                Vector2 point=trailPoints[--first];
                float segment=Vector2.Distance(point,tail);
                tail=Vector2.MoveTowards(tail,point,length);
                length-=segment;
            }
            var line = AxeLine(7, "Flying axe trace", trailPoints.Count-first);
            line.startWidth = .012f; line.endWidth = .065f;
            line.startColor = NeuralPalette.Alpha(FlightIce, .05f);
            line.endColor = NeuralPalette.Primary;
            line.SetPosition(0,ClipTellPoint(tail,.065f));
            for(int i=first+1;i<trailPoints.Count;i++)line.SetPosition(i-first,ClipTellPoint(trailPoints[i],.065f));
        }
    }
}
