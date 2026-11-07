using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss presentation: the player-sized stickman with the retired Alpha rainbow shader on
    // its body, a neutral axe (shaft in the Weapon slot, head in the Sheath slot) and the shared
    // preparation/lock/active tell hierarchy. Phase two restores plain white body materials.
    public sealed partial class BossSilhouetteView
    {
        const float AxeShaft = 1.0f, AxeShaftWidth = .075f, AxeGrip = .14f;
        static readonly Vector2 AxeHeadSize = new Vector2(.26f, .30f);
        static readonly Part[] AxeBodyParts = { Part.Torso, Part.Head, Part.BackThigh, Part.BackCalf, Part.FrontThigh,
            Part.FrontCalf, Part.BackUpperArm, Part.BackForearm, Part.FrontUpperArm, Part.FrontForearm };
        Material rainbowMaterial;
        Material[] axeBodyMaterials;
        SpriteRenderer flyingShaft, flyingHead, frontEye, backEye;
        float drainFrom = -1, drainSeconds = 1, shockwaveAt = -1;
        public const float ShockwaveSeconds = 1.25f;
        const float ShockwaveRadius = 24;
        static readonly Color EyeRed = new Color(1f, .16f, .18f, 1f);
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
            if (rainbowMaterial == null || !FinalAxe) return;
            float height = actor.Motor.Size.y;
            rainbowMaterial.SetFloat("_RainbowBottom", actor.Position.y - height * .5f);
            rainbowMaterial.SetFloat("_RainbowHeight", height + .1f);
            if (drainFrom >= 0)
            {
                float drained = Mathf.Clamp01((clock - drainFrom) / drainSeconds);
                rainbowMaterial.SetFloat("_RainbowAmount", 1 - drained);
                if (drained >= 1) { drainFrom = -1; ApplyFinalBossBody(true); }
            }
            UpdateFlyingAxe();
            UpdateShockwave();
        }

        // Phase-change presentation, driven by the scripted transition.
        public void DrainRainbow(float seconds) { drainFrom = clock; drainSeconds = Mathf.Max(.05f, seconds); }
        public void Shockwave() { shockwaveAt = clock; Impact(actor.Position); }
        public void SetEyesRed(bool value) => EyesRed = value;

        void UpdateShockwave()
        {
            if (shockwaveAt < 0) return;
            float t = (clock - shockwaveAt) / ShockwaveSeconds;
            if (t >= 1) { shockwaveAt = -1; if (tells[2] != null) tells[2].enabled = false; return; }
            const int points = 48;
            if (tells[2] == null) tells[2] = NewLine("Shockwave", true, points);
            var line = tells[2]; line.enabled = true; line.positionCount = points; line.loop = true;
            float radius = Mathf.Lerp(.4f, ShockwaveRadius, 1 - (1 - t) * (1 - t));
            line.startWidth = line.endWidth = Mathf.Lerp(.22f, .05f, t);
            line.startColor = line.endColor = NeuralPalette.Alpha(NeuralPalette.Primary, 1 - t);
            for (int i = 0; i < points; i++)
            {
                float angle = i / (float)points * Mathf.PI * 2;
                line.SetPosition(i, actor.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
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
                var color = EyeRed; color.a = alpha; sprite.color = color;
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
                // Invasion: axe held overhead, free hand reaching toward the player.
                float reach = Ease(Mathf.Clamp01(axe.InvasionProgress / .2f));
                p.WeaponAngle = Mathf.Lerp(72, 95, reach);
                p.FrontHand = Vector2.Lerp(p.FrontHand, new Vector2(-.06f, .58f), reach);
                p.BackHand = Vector2.Lerp(p.BackHand, new Vector2(.48f, .36f), reach);
                p.Chest.x -= .04f * reach; p.Head.y += .02f * reach;
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
                        if (recover) p.WeaponAngle = Mathf.Lerp(-38, 72, t);
                    }
                    p.BackHand = p.FrontHand + new Vector2(-.12f, -.06f);
                    break;
                case FinalBossAxe.Move.Spin:
                    p.FrontFoot.x = .36f; p.BackFoot.x = -.36f; p.Hip.y -= .08f; p.Chest.y -= .05f;
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
                case FinalBossAxe.Move.Throw:
                case FinalBossAxe.Move.Boomerang:
                    bool overhand = axe.Current == FinalBossAxe.Move.Throw;
                    p.FrontFoot.x = .36f; p.BackFoot.x = -.40f;
                    if (prepare || locked)
                    {
                        float wind = prepare ? t : 1;
                        p.WeaponAngle = Mathf.Lerp(72, overhand ? 140 : 205, wind);
                        p.FrontHand = Vector2.Lerp(p.FrontHand, overhand ? new Vector2(-.26f, .52f) : new Vector2(-.34f, .12f), wind);
                        p.Chest.x -= .07f * wind;
                    }
                    else
                    {
                        // The empty hand stays forward until the axe returns to it.
                        p.FrontHand = new Vector2(.42f, overhand ? .30f : .16f);
                        p.Chest.x += .08f * settle;
                        if (recover) p.WeaponAngle = Mathf.Lerp(20, 72, t);
                    }
                    p.BackHand = new Vector2(-.18f, .02f);
                    break;
            }
        }

        // Called after the shared pose placement: the axe replaces the sword geometry.
        void ApplyAxe(Pose p)
        {
            PlaceEyes(p);
            if (actor.Axe.AxeFlying) { Hide(Part.Weapon); Hide(Part.Sheath); return; }
            Vector2 direction = Angle(p.WeaponAngle);
            Place(Part.Weapon, p.FrontHand + direction * (AxeShaft * .5f - AxeGrip), new Vector2(AxeShaft, AxeShaftWidth), p.WeaponAngle);
            Vector2 blade = new Vector2(direction.y, -direction.x);
            Place(Part.Sheath, p.FrontHand + direction * (AxeShaft - AxeGrip - .08f) + blade * .10f, AxeHeadSize, p.WeaponAngle);
            if (parts[(int)Part.Sheath] != null) { var color = weaponColor; color.a = alpha; parts[(int)Part.Sheath].color = color; }
        }

        void UpdateFlyingAxe()
        {
            var axe = actor.Axe;
            bool flying = axe.AxeFlying && !defeated;
            if (flying && flyingShaft == null)
            {
                flyingShaft = FlyingPart("FlyingAxeShaft");
                flyingHead = FlyingPart("FlyingAxeHead");
            }
            if (flyingShaft == null) return;
            flyingShaft.enabled = flyingHead.enabled = flying;
            if (!flying) return;
            Vector2 direction = Angle(axe.AxeSpin), blade = new Vector2(direction.y, -direction.x);
            SetFlying(flyingShaft, axe.AxePosition - direction * .18f, new Vector2(AxeShaft, AxeShaftWidth), axe.AxeSpin);
            SetFlying(flyingHead, axe.AxePosition + direction * .24f + blade * .10f, AxeHeadSize, axe.AxeSpin);
        }

        SpriteRenderer FlyingPart(string label)
        {
            var source = parts[(int)Part.Weapon];
            var go = new GameObject(label);
            // Parented to the actor root (never mirrored by the view's facing scale).
            go.transform.SetParent(actor.transform, false);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = source.sprite; sprite.sharedMaterial = source.sharedMaterial;
            sprite.sortingLayerID = source.sortingLayerID; sprite.sortingOrder = source.sortingOrder;
            sprite.color = weaponColor;
            return sprite;
        }

        static void SetFlying(SpriteRenderer sprite, Vector2 centre, Vector2 size, float angle)
        {
            sprite.transform.position = new Vector3(centre.x, centre.y, sprite.transform.position.z);
            sprite.transform.rotation = Quaternion.Euler(0, 0, angle);
            Vector2 source = sprite.sprite.bounds.size;
            sprite.transform.localScale = new Vector3(size.x / Mathf.Max(.001f, source.x), size.y / Mathf.Max(.001f, source.y), 1);
        }

        // A jagged, flickering signal from the Boss's reaching hand into the player's body.
        void InvasionSignal(FinalBossAxe axe)
        {
            if (actor.Player == null || axe.InvasionProgress < .2f) return;
            const int points = 14;
            if (tells[1] == null) tells[1] = NewLine("InvasionSignal", true, points);
            var line = tells[1]; line.enabled = true; line.positionCount = points;
            Vector2 from = actor.Position + new Vector2(actor.Facing * .48f, .36f), to = actor.Player.transform.position;
            Vector2 normal = new Vector2(-(to - from).y, (to - from).x).normalized;
            float pulse = .55f + .45f * Mathf.Sin(clock * 38f);
            Color color = NeuralPalette.Alpha(NeuralPalette.Primary, .45f + .5f * pulse);
            line.startWidth = line.endWidth = .035f + .025f * pulse; line.startColor = line.endColor = color;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float jitter = (i == 0 || i == points - 1) ? 0 : Mathf.Sin(clock * 47f + i * 2.3f) * .22f;
                line.SetPosition(i, ClipTellPoint(Vector2.Lerp(from, to, t) + normal * jitter, line.startWidth));
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
            if (axe.Current == FinalBossAxe.Move.None || axe.Phase == FinalBossAxe.Stage.Recovery) return;
            bool active = axe.Phase == FinalBossAxe.Stage.Active, locked = axe.Phase == FinalBossAxe.Stage.Locked || active;
            float width = SwordTellWidth(active, locked);
            Color color = AxeTellColor(active, locked);
            switch (axe.Current)
            {
                case FinalBossAxe.Move.Chop:
                {
                    // The swing arc: overhead to the ground in front, at the strike reach.
                    if (tells[0] == null) tells[0] = NewLine("AxeChopTell", false, 17);
                    var line = tells[0]; line.enabled = true; line.positionCount = 17;
                    line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
                    Vector2 root = actor.Position + Vector2.up * .3f;
                    for (int i = 0; i < 17; i++)
                    {
                        float angle = Mathf.Lerp(100, -40, i / 16f) * Mathf.Deg2Rad;
                        line.SetPosition(i, ClipTellPoint(root + new Vector2(axe.LockedFacing * Mathf.Cos(angle), Mathf.Sin(angle)) * FinalBossAxe.ChopReach, width));
                    }
                    break;
                }
                case FinalBossAxe.Move.Spin:
                {
                    if (tells[0] == null) tells[0] = NewLine("AxeSpinTell", false, 33);
                    var line = tells[0]; line.enabled = true; line.positionCount = 33;
                    line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
                    for (int i = 0; i < 33; i++)
                    {
                        float angle = i / 32f * Mathf.PI * 2;
                        line.SetPosition(i, ClipTellPoint(actor.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * FinalBossAxe.SpinRadius, width));
                    }
                    break;
                }
                default:
                {
                    var path = axe.Path;
                    if (path.Count < 2) break;
                    if (tells[0] == null) tells[0] = NewLine("AxePathTell", false, path.Count);
                    var line = tells[0]; line.enabled = true; line.positionCount = path.Count;
                    line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
                    for (int i = 0; i < path.Count; i++) line.SetPosition(i, ClipTellPoint(path[i], width));
                    break;
                }
            }
        }
    }
}
