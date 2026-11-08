using UnityEngine;

namespace GoGhost.Duel
{
    // Rainbow body and phase transition. Phase two retains the original axe and boxer poses.
    public sealed partial class BossSilhouetteView
    {
        const float AxeShaft = 1.0f, AxeShaftWidth = .075f, AxeGrip = .14f;
        static readonly Vector2 AxeHeadSize = new Vector2(.26f, .30f);
        static readonly Part[] AxeBodyParts = { Part.Torso, Part.Head, Part.BackThigh, Part.BackCalf, Part.FrontThigh,
            Part.FrontCalf, Part.BackUpperArm, Part.BackForearm, Part.FrontUpperArm, Part.FrontForearm };
        Material rainbowMaterial;
        Material[] axeBodyMaterials;
        SpriteRenderer frontEye, backEye;
        float drainFrom = -1, drainSeconds = 1, shockwaveAt = -1;
        public const float ShockwaveSeconds = 1.25f;
        public const float ShockwaveStartRadius = .4f;
        float shockwaveReach, releasedAt = float.NegativeInfinity;
        Vector2 shockwaveOrigin;
        SpriteRenderer[] empEdges;
        static readonly Color EyeRed = new Color(1f, .16f, .18f, 1f);
        float eyesLitAt = float.NegativeInfinity;
        bool rising;
        float kneel;
        public void RiseFromKneel() => rising = true;
        public bool EyesRed { get; private set; }
        bool FinalAxe => actor != null && actor.Axe != null;

        void ConfigureFinalBossLook()
        {
            if (!FinalAxe) return;
            if (axeBodyMaterials == null)
            {
                axeBodyMaterials = new Material[AxeBodyParts.Length];
                for (int i = 0; i < AxeBodyParts.Length; i++)
                    axeBodyMaterials[i] = parts[(int)AxeBodyParts[i]] != null ? parts[(int)AxeBodyParts[i]].sharedMaterial : null;
            }
            if (rainbowMaterial == null)
            {
                var shader = Resources.Load<Shader>("FinalBossRainbow");
                if (shader != null) rainbowMaterial = new Material(shader) { name = "Final Boss rainbow (runtime)", hideFlags = HideFlags.DontSave };
                if (rainbowMaterial != null) { rainbowMaterial.SetFloat("_RainbowAmount", 1); rainbowMaterial.SetColor("_BaseColor", NeuralPalette.Primary); }
            }
            ApplyFinalBossBody(false);
        }

        public void SetFinalBossPhaseTwo()
        {
            drainFrom = -1;
            if (empEdges != null)
                foreach (var edge in empEdges) edge.enabled = false;
            ApplyFinalBossBody(true);
        }

        void ApplyFinalBossBody(bool white)
        {
            if (axeBodyMaterials == null) return;
            for (int i = 0; i < AxeBodyParts.Length; i++)
            {
                var part = parts[(int)AxeBodyParts[i]];
                if (part != null) part.sharedMaterial = white || rainbowMaterial == null ? axeBodyMaterials[i] : rainbowMaterial;
            }
            // The rainbow shader keeps only the tint's alpha; phase two uses the player's Primary white.
            bodyColor = white ? NeuralPalette.Primary : Color.white;
            weaponColor = NeuralPalette.Primary;
        }

        void UpdateFinalBossLook()
        {
            if (!FinalAxe) return;
            UpdateShockwave();
            UpdateFlyingAxe();
            if (rainbowMaterial == null) return;
            float height = actor.Motor.Size.y;
            rainbowMaterial.SetFloat("_RainbowBottom", actor.Position.y - height * .5f);
            rainbowMaterial.SetFloat("_RainbowHeight", height + .1f);
            if (drainFrom >= 0)
            {
                float drained = Mathf.Clamp01((clock - drainFrom) / drainSeconds);
                rainbowMaterial.SetFloat("_RainbowAmount", 1 - drained);
                if (drained >= 1) SetFinalBossPhaseTwo();
            }
        }

        // Phase-change presentation, driven by the scripted transition.
        public void DrainRainbow(float seconds) { drainFrom = clock; drainSeconds = Mathf.Max(.05f, seconds); }
        internal void Shockwave(Vector2 origin, float reach)
        {
            shockwaveOrigin = origin;
            shockwaveReach = reach;
            shockwaveAt = releasedAt = Time.time;
            empEdges = new SpriteRenderer[AxeBodyParts.Length];
            int backOrder = int.MaxValue;
            foreach (var part in AxeBodyParts)
                backOrder = Mathf.Min(backOrder, parts[(int)part].sortingOrder);
            for (int i = 0; i < AxeBodyParts.Length; i++)
                empEdges[i] = EmpOutline.Create(parts[(int)AxeBodyParts[i]], axeBodyMaterials[i], backOrder - 1, .055f);
        }
        public void SetEyesRed(bool value, bool immediate = false)
        {
            EyesRed = value;
            eyesLitAt = immediate ? float.NegativeInfinity : clock;
        }

        void UpdateShockwave()
        {
            if (empEdges != null && empEdges[0].enabled)
            {
                float age = Time.time - releasedAt;
                float strength = Mathf.Lerp(.96f, .35f, Mathf.SmoothStep(0, 1, age / .5f));
                if (drainFrom >= 0) strength *= 1 - Mathf.Clamp01((clock - drainFrom) / drainSeconds);
                var tint = NeuralPalette.Alpha(NeuralPalette.Spine, strength);
                foreach (var edge in empEdges) edge.color = tint;
            }
            if (shockwaveAt < 0) return;
            float t = (Time.time - shockwaveAt) / ShockwaveSeconds;
            if (t >= 1) { shockwaveAt = -1; if (tells[2] != null) tells[2].enabled = false; return; }
            const int points = 96;
            if (tells[2] == null) tells[2] = NewLine("EMP wave", false, points);
            var line = tells[2]; line.enabled = true; line.positionCount = points; line.loop = true;
            float radius = Mathf.Lerp(ShockwaveStartRadius, shockwaveReach, t);
            line.startWidth = line.endWidth = Mathf.Lerp(.15f, .065f, t);
            line.startColor = line.endColor = NeuralPalette.Alpha(NeuralPalette.Spine, 1 - Mathf.SmoothStep(0, 1, (t - .62f) / .38f));
            for (int i = 0; i < points; i++)
            {
                float angle = i / (float)points * Mathf.PI * 2;
                line.SetPosition(i, shockwaveOrigin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        // Two red eyes on the head, mirrored with the body.
        void PlaceEyes(Pose p)
        {
            if (!EyesRed) { if (frontEye != null) frontEye.enabled = backEye.enabled = false; return; }
            if (frontEye == null) { frontEye = EyePart("FrontEye"); backEye = EyePart("BackEye"); }
            var eye = new Vector2(.08f, .07f);
            foreach (var (sprite, offset) in new[] { (frontEye, new Vector2(.13f, .03f)), (backEye, new Vector2(.03f, .03f)) })
            {
                sprite.enabled = true;
                sprite.transform.localPosition = p.Head + offset;
                sprite.transform.localRotation = Quaternion.identity;
                Vector2 source = sprite.sprite.bounds.size;
                sprite.transform.localScale = new Vector3(eye.x / Mathf.Max(.001f, source.x), eye.y / Mathf.Max(.001f, source.y), 1);
                var color = EyeRed; color.a = alpha * Mathf.SmoothStep(0,1,(clock-eyesLitAt)/.2f); sprite.color = color;
            }
        }

        SpriteRenderer EyePart(string label)
        {
            var head = parts[(int)Part.Head];
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = head.sprite; sprite.sharedMaterial = axeBodyMaterials != null && axeBodyMaterials[1] != null ? axeBodyMaterials[1] : head.sharedMaterial;
            sprite.sortingLayerID = head.sortingLayerID; sprite.sortingOrder = head.sortingOrder + 1;
            // Tail-light trails: each eye leaves a short red streak while the Boss moves.
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = .18f; trail.minVertexDistance = .02f; trail.startWidth = .06f; trail.endWidth = 0;
            trail.sharedMaterial = glowMaterial; trail.startColor = EyeRed; trail.endColor = NeuralPalette.Alpha(EyeRed, 0);
            trail.sortingLayerID = head.sortingLayerID; trail.sortingOrder = head.sortingOrder;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
            return sprite;
        }

        void OnDestroy()
        {
            if (rainbowMaterial != null) Destroy(rainbowMaterial);
        }

        void AxePose(ref Pose p, float wave, float motion)
        {
            var axe = actor.Axe;
            float t = Ease(axe.PhaseProgress);
            bool prepare = axe.Phase == FinalBossAxe.Stage.Prepare, locked = axe.Phase == FinalBossAxe.Stage.Locked;
            bool active = axe.Phase == FinalBossAxe.Stage.Active, recover = axe.Phase == FinalBossAxe.Stage.Recovery;
            float settle = recover ? 1 - t : 1;
            // Idle: the axe rests upright in the leading hand.
            p.WeaponAngle = 72;
            p.FrontHand = new Vector2(.22f + wave * motion * .06f, .02f);
            p.BackHand = new Vector2(-.04f - wave * motion * .10f, -.05f);
            kneel = Mathf.MoveTowards(kneel, axe.Transitioning && !rising && actor.Motor.Grounded ? 1 : 0, Time.deltaTime * 2.4f);
            if (kneel > 0)
            {
                p.Hip = Vector2.Lerp(p.Hip, new Vector2(-.05f, -.43f), kneel);
                p.Chest = Vector2.Lerp(p.Chest, new Vector2(.08f, -.04f), kneel);
                p.Head = Vector2.Lerp(p.Head, new Vector2(.16f, .14f), kneel);
                float floor = -actor.Motor.Size.y * .5f + .06f;
                p.FrontFoot = Vector2.Lerp(p.FrontFoot, new Vector2(.44f, floor), kneel);
                p.BackFoot = Vector2.Lerp(p.BackFoot, new Vector2(-.48f, floor), kneel);
                p.FrontHand = Vector2.Lerp(p.FrontHand, new Vector2(.31f, -.18f), kneel);
                p.BackHand = Vector2.Lerp(p.BackHand, new Vector2(.10f, -.19f), kneel);
                p.WeaponAngle = 80;
                return;
            }
            if (axe.Invading)
            {
                float progress = axe.InvasionProgress;
                float reach = Ease(Mathf.Clamp01(progress / .20f)) * (1 - Ease(Mathf.InverseLerp(.82f, 1, progress)));
                p.WeaponAngle = Mathf.Lerp(72, 95, reach);
                p.FrontHand = Vector2.Lerp(p.FrontHand, new Vector2(-.06f, .58f), reach);
                p.BackHand = Vector2.Lerp(p.BackHand, new Vector2(-.23f, -.10f), reach);
                p.Chest.x -= .04f * reach; p.Head.y += .02f * reach;
                return;
            }
            if (!axe.PhaseTwo)
            {
                PhaseOneAxePose(ref p, axe, wave, motion);
                return;
            }
            switch (axe.Current)
            {
                case FinalBossAxe.Move.Chop:
                    p.FrontFoot.x = .34f; p.BackFoot.x = -.38f;
                    if (prepare || locked)
                    {
                        float raise = prepare ? t : 1;
                        p.WeaponAngle = Mathf.Lerp(72, 158, raise);
                        p.FrontHand = Vector2.Lerp(p.FrontHand, new Vector2(-.02f, .66f), raise);
                        p.Chest.x -= .06f * raise; p.Head.x -= .04f * raise;
                    }
                    else
                    {
                        float cut = active ? t : 1;
                        p.WeaponAngle = Mathf.Lerp(158, -38, cut);
                        p.FrontHand = Vector2.Lerp(new Vector2(-.02f, .66f), new Vector2(.40f, .02f), cut);
                        p.Chest.x += .12f * settle; p.Head.x += .09f * settle; p.Hip.y -= .07f * settle;
                        if (recover)
                        {
                            p.WeaponAngle = Mathf.Lerp(-38, 72, t);
                        }
                    }
                    p.BackHand = p.FrontHand + new Vector2(-.12f, -.06f);
                    break;
                case FinalBossAxe.Move.Spin:
                    p.FrontFoot.x = .36f; p.BackFoot.x = -.36f;
                    p.Hip.y -= .08f; p.Chest.y -= .05f;
                    if (active)
                    {
                        // The whole axe sweeps a full circle around the body at arm's length.
                        p.WeaponAngle = axe.AxeSpin;
                        p.FrontHand = new Vector2(Mathf.Cos(axe.AxeSpin * Mathf.Deg2Rad), Mathf.Sin(axe.AxeSpin * Mathf.Deg2Rad) * .4f + .25f) * .42f;
                    }
                    else
                    {
                        float out_ = prepare ? t : locked ? 1 : settle;
                        p.WeaponAngle = Mathf.Lerp(72, 180, out_);
                        p.FrontHand = Vector2.Lerp(p.FrontHand, new Vector2(-.40f, .22f), out_);
                    }
                    p.BackHand = new Vector2(.30f, .20f);
                    break;
            }
        }

        // Called after the shared pose placement: the axe replaces the sword geometry.
        void ApplyAxe(Pose p)
        {
            PlaceEyes(p);
            if (actor.Axe.AxeFlying) { Hide(Part.Weapon); Hide(Part.Sheath); return; }
            PlaceHeldAxe(p);
        }

        void PlaceHeldAxe(Pose p)
        {
            if (!FinalAxe || !actor.Axe.PhaseTwo)
            {
                PlacePhaseOneAxe(p);
                return;
            }
            var weapon = parts[(int)Part.Weapon];
            if (originalWeaponSprite != null) weapon.sprite = originalWeaponSprite;
            Vector2 direction = Angle(p.WeaponAngle);
            Place(Part.Weapon, p.FrontHand + direction * (AxeShaft * .5f - AxeGrip), new Vector2(AxeShaft, AxeShaftWidth), p.WeaponAngle);
            Vector2 blade = new Vector2(direction.y, -direction.x);
            Place(Part.Sheath, p.FrontHand + direction * (AxeShaft - AxeGrip - .08f) + blade * .10f, AxeHeadSize, p.WeaponAngle);
            if (parts[(int)Part.Sheath] != null) { var color = weaponColor; color.a = alpha; parts[(int)Part.Sheath].color = color; }
        }

        // A tight coil at the raised blade sends one violet trace; the receiving ring lands at the write.
        void InvasionSignal(FinalBossAxe axe)
        {
            if (actor.Player == null) return;
            float age = axe.InvasionProgress * FinalBossAxe.InvasionDuration;
            Vector2 from = phaseOneSocket;
            Vector2 to = actor.Player.transform.position;
            float charge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .70f, age));
            float release = Mathf.InverseLerp(FinalBossAxe.InvasionInject, FinalBossAxe.InvasionDuration, age);
            var violet = NeuralPalette.Spine;
            DrawInvasionRing(3, from, Mathf.Lerp(.14f,.48f,charge), .065f,
                NeuralPalette.Alpha(violet, charge * (1-release) * .88f));
            if (age < .55f) return;
            float travel = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,FinalBossAxe.InvasionInject,age));
            var tip = Vector2.Lerp(from,to,travel);
            if (tells[1] == null) tells[1] = NewLine("Invasion trace", true, 2);
            SetLine(tells[1], from, tip, .035f, NeuralPalette.Alpha(violet,(1-release)*.65f));
            if (tells[4] == null) tells[4] = NewLine("Invasion pulse", true, 2);
            SetLine(tells[4], Vector2.Lerp(from,tip,.82f), tip, .085f,
                NeuralPalette.Alpha(Color.Lerp(violet, NeuralPalette.Primary, .30f), 1-release));
            if (age >= FinalBossAxe.InvasionInject)
                DrawInvasionRing(5,to,Mathf.Lerp(.24f,1.08f,release),.075f,NeuralPalette.Alpha(violet,1-release));
        }

        void DrawInvasionRing(int slot, Vector2 center, float radius, float width, Color color)
        {
            const int points = 40;
            if (tells[slot] == null) tells[slot] = NewLine("Invasion ring", true, points);
            var line = tells[slot];
            line.enabled = true; line.loop = true;
            line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
            for (int i=0; i<points; i++)
            {
                float angle = 2*Mathf.PI*i/points;
                line.SetPosition(i, ClipTellPoint(center + radius*new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),width));
            }
        }

        Color AxeTellColor(bool active, bool locked)
        {
            Color color = NeuralPalette.Primary;
            color.a = active ? .95f : locked ? .8f : .3f;
            return color;
        }

        void AxeTells()
        {
            var axe = actor.Axe;
            if (axe.PhaseTwo) return;
            if (axe.Invading) { InvasionSignal(axe); return; }
            DrawPhaseOneAttack(axe);
        }
    }
}
