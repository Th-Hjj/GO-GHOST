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
        const float TrailLifetime = .16f;
        const int MaxTrailPoints = 22;
        static readonly Color FlightIce = new Color32(197, 237, 255, 255);
        Sprite originalWeaponSprite, phaseOneSprite;
        SpriteRenderer flightSprite;
        Vector2 phaseOneSocket;
        readonly List<Vector2> trailPoints = new List<Vector2>(MaxTrailPoints);
        readonly List<float> trailTimes = new List<float>(MaxTrailPoints);

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
            if (prepare || axe.Phase == FinalBossAxe.Stage.Locked)
            {
                if (axe.Current == FinalBossAxe.Move.UnderpassReturn ||
                    axe.Current == FinalBossAxe.Move.AnchorVault || axe.Current == FinalBossAxe.Move.PalmDeflection)
                    WindUpCast(ref p, axe, restingHand, grip, angle);
                else if (prepare)
                {
                    float raise = Ease(axe.PhaseProgress);
                    p.FrontHand = Vector2.Lerp(restingHand, grip, raise);
                    p.FrontHand += new Vector2(-.28f, .13f) * Mathf.Sin(raise * Mathf.PI);
                }
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
                    p.FrontFoot.x = Mathf.Lerp(p.FrontFoot.x, .34f, weight);
                    p.BackFoot.x = Mathf.Lerp(p.BackFoot.x, -.41f, weight);
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

        // The underpass leaves low from the hip. The anchor and deflection throws
        // open the shoulder behind the head. All three arrive at the real release grip.
        static void WindUpCast(ref Pose p, FinalBossAxe axe, Vector2 idleHand, Vector2 releaseHand, float releaseAngle)
        {
            bool low = axe.Current == FinalBossAxe.Move.UnderpassReturn;
            Vector2 woundHand = low ? new Vector2(-.23f, -.03f) : new Vector2(-.22f, .48f);
            float woundAngle = low ? 136 : 151;
            if (axe.Phase == FinalBossAxe.Stage.Prepare)
            {
                float raise = Ease(axe.PhaseProgress);
                p.FrontHand = Vector2.Lerp(idleHand, woundHand, raise);
                p.WeaponAngle = Mathf.Lerp(56, woundAngle, raise);
            }
            else
            {
                float release = Ease(axe.PhaseProgress);
                p.FrontHand = Vector2.Lerp(woundHand, releaseHand, release);
                p.WeaponAngle = Mathf.Lerp(woundAngle, releaseAngle, release);
            }
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
            // The invasion starts at the ice-blue socket of the raised axe.
            phaseOneSocket = transform.TransformPoint(p.FrontHand + forward * (59.8f * 5.5f / AxePixelsPerUnit));
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
            flightSprite.flipX = axe.LockedFacing < 0;
            var forward = Angle(axe.AxeSpin);
            Vector2 grip = axe.AxePosition - forward * GripToFlightPivot;
            Vector2 centre = AxeSpriteCentre(grip, forward, axe.LockedFacing);
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
            if (axe.TryGetCut(out var cut, out bool locked, out bool active) && (locked || active))
            {
                // Swordsman's airborne cut leaves just the live leading edge. At rest the
                // short commitment arc appears only once the destination is locked.
                bool airborneCut = axe.Current == FinalBossAxe.Move.LeapingCrosscut || axe.Current == FinalBossAxe.Move.AnchorVault;
                if (active || !airborneCut) DrawAxeCut(cut, active);
            }
            if (axe.FlightWarning && axe.Path.Count >= 2)
            {
                var line = AxeLine(6, "Axe flight warning", axe.Path.Count);
                float width = SwordTellWidth(false, axe.Phase == FinalBossAxe.Stage.Locked);
                line.startWidth = line.endWidth = width;
                line.startColor = line.endColor = AxeTellColor(false, axe.Phase == FinalBossAxe.Stage.Locked);
                for (int i = 0; i < axe.Path.Count; i++) line.SetPosition(i, ClipTellPoint(axe.Path[i], width));
            }
            if (axe.AxeFlying) DrawAxeTrail(axe.AxePosition);
            if (axe.AnchorPulling)
            {
                var tether = AxeLine(8, "Anchor pull", 2);
                Vector2 flyingGrip = axe.AxePosition - Angle(axe.AxeSpin) * GripToFlightPivot;
                SetLine(tether, axe.Hand(), flyingGrip, .024f, NeuralPalette.Alpha(FlightIce, .55f));
            }
            if (axe.PalmAccent > 0)
            {
                Vector2 palm = axe.Hand();
                for (int i = 0; i < 3; i++)
                {
                    Vector2 ray = Angle(32 + i * 56);
                    ray.x *= axe.LockedFacing;
                    Vector2 a = palm + ray * (.08f + .12f * (1 - axe.PalmAccent));
                    SetLine(AxeLine(9 + i, "Palm deflection", 2), a, a + ray * .13f, .025f,
                        NeuralPalette.Alpha(FlightIce, axe.PalmAccent));
                }
            }
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
                Vector2 root = active ? cut.OriginAt(Mathf.Lerp(cut.StartsAt, cut.EndsAt, step)) : actor.Axe.Hand();
                Vector2 tip = root + ray * cut.Reach;
                if (cut.TerrainBlocked && Projectile.TerrainHit(root, tip, cut.Radius, out var wall))
                    tip = root + ray * wall.distance;
                line.SetPosition(i, ClipTellPoint(tip, width));
            }
        }

        void DrawAxeTrail(Vector2 pivot)
        {
            float now = Time.time;
            int expired = 0;
            while (expired < trailTimes.Count && trailTimes[expired] < now - TrailLifetime) expired++;
            if (expired > 0)
            {
                trailTimes.RemoveRange(0, expired);
                trailPoints.RemoveRange(0, expired);
            }
            if (trailPoints.Count == 0 || (trailPoints[trailPoints.Count - 1] - pivot).sqrMagnitude > .0004f)
            {
                if (trailPoints.Count == MaxTrailPoints)
                {
                    trailPoints.RemoveAt(0); trailTimes.RemoveAt(0);
                }
                trailPoints.Add(pivot); trailTimes.Add(now);
            }
            if (trailPoints.Count < 2) return;
            var line = AxeLine(7, "Flying axe trace", trailPoints.Count);
            line.startWidth = .012f; line.endWidth = .045f;
            line.startColor = NeuralPalette.Alpha(FlightIce, .05f);
            line.endColor = NeuralPalette.Alpha(FlightIce, .70f);
            for (int i = 0; i < trailPoints.Count; i++) line.SetPosition(i, ClipTellPoint(trailPoints[i], .045f));
        }
    }
}
