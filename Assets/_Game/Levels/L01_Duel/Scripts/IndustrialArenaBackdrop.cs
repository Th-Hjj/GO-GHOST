using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // One mission's backlit look. Colours are sRGB identities; strengths are Editor tuning.
    readonly struct ArenaLook
    {
        public static readonly Color Structure = new Color32(5, 5, 6, 255);
        public readonly string Artwork, FarLabel, NearLabel;
        public readonly Color Haze, Rim;
        // Plate opacities are Thomas's per-mission choices (2026-11-13/14).
        public readonly float FarOpacity, NearOpacity;

        ArenaLook(string artwork, string farLabel, string nearLabel, Color haze, Color rim, float farOpacity, float nearOpacity)
        {
            Artwork = artwork; FarLabel = farLabel; NearLabel = nearLabel; Haze = haze; Rim = rim;
            FarOpacity = farOpacity; NearOpacity = nearOpacity;
        }

        // The GHOST arena: his blue in the haze, a pale blue-white on the landing edges, no plates.
        public static ArenaLook Ghost => new ArenaLook(null, null, null, GhostHologram.Body, new Color32(227, 235, 255, 255), 0, 0);

        // Mission 3 starts cold white; the EMP dyes its haze violet. The Test Room and the Bonus have no plates.
        public static ArenaLook For(int wave) => wave switch
        {
            1 => new ArenaLook("Industrial", "Distant Works", "Steel Columns", NeuralPalette.Gunner,
                new Color32(255, 233, 219, 255), .65f, .65f),
            2 => new ArenaLook("Casino", "Resort Hotels", "Casino Entrance", NeuralPalette.Swordsman,
                new Color32(255, 226, 230, 255), .50f, .70f),
            DuelGame.FinalBossWave => new ArenaLook("Megacity", "Distant Skyline", "Corporate Towers", NeuralPalette.Primary,
                NeuralPalette.Primary, .50f, .50f),
            _ => new ArenaLook(null, null, null, NeuralPalette.Secondary, NeuralPalette.Primary, 0, 0)
        };
    }

    // The two authored plates fill the fixed arena as authored; only their sampled region moves.
    // One generated layer of haze lies along the floor between them. The art itself is never altered.
    sealed class IndustrialArenaBackdrop : MonoBehaviour
    {
        const float VisibleWidthPixels = 966f;
        const float FarTravelPixels = 7f;
        const float NearTravelPixels = 36f;
        // Thomas, 2026-11-14: only a layer that hugs the floor; the glow in mid-air looked like a stain.
        // Thomas, 2026-11-14: no floor haze at all. The layers stay in place, hidden, so the EMP cross-fade still has its targets.
        const bool FloorHazeShown = false;
        const float HazeHeight = .24f, HazeStrength = .08f; // .20, then .13; Thomas asked for weaker each time

        Transform player;
        Bounds arena;
        RawImage far, near, farLights, farLightsRight, nearMask;
        Material contourMaterial, hazeMaterial;
        readonly List<Texture2D> hazeTextures = new();
        // Floor haze in the mission colour, and a violet copy once the EMP sweep exists.
        RawImage haze, empHaze;

        public static void Create(Transform attempt, Transform player, Bounds arena, int wave)
        {
            var look = ArenaLook.For(wave);
            Texture2D farTexture = null, nearTexture = null;
            if (look.Artwork != null)
            {
                farTexture = Resources.Load<Texture2D>($"Arena/{look.Artwork}Far");
                nearTexture = Resources.Load<Texture2D>($"Arena/{look.Artwork}Near");
                if (!farTexture || !nearTexture)
                {
                    Debug.LogError($"Mission {wave} {look.Artwork} background textures could not be loaded.");
                    farTexture = nearTexture = null;
                }
            }
            var hazeShader = Resources.Load<Shader>("ArenaHaze");
            if (hazeShader == null)
            {
                Debug.LogError("Arena haze shader could not be loaded.");
                return;
            }

            var root = new GameObject($"{look.Artwork ?? "Neutral"} Arena Background", typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)root.transform;
            rect.SetParent(attempt, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -100;
            rect.position = arena.center;
            rect.sizeDelta = arena.size;

            var backdrop = root.AddComponent<IndustrialArenaBackdrop>();
            backdrop.player = player;
            backdrop.arena = arena;
            backdrop.hazeMaterial = new Material(hazeShader) { name = "Arena haze", hideFlags = HideFlags.DontSave };
            bool plates = farTexture != null;
            if (plates) backdrop.far = backdrop.AddPlate(look.FarLabel, farTexture, look.FarOpacity);
            // Only the factory has this layer: the two stack beacons.
            var lights = plates ? Resources.Load<Texture2D>($"Arena/{look.Artwork}FarLights") : null;
            if (lights != null)
            {
                // Thomas, 2026-11-15: the two beacons no longer change together. Each half of the layer has its own clock.
                backdrop.farLights = backdrop.AddPlate("Beacon left", lights, 1f);
                backdrop.farLights.rectTransform.anchorMax = new Vector2(.5f, 1f);
                backdrop.farLightsRight = backdrop.AddPlate("Beacon right", lights, 1f);
                backdrop.farLightsRight.rectTransform.anchorMin = new Vector2(.5f, 0f);
            }
            bool casino = plates && look.Artwork == "Casino";
            if (casino) backdrop.AddCrownLights();
            bool megacity = plates && look.Artwork == "Megacity";
            if (megacity) backdrop.AddAntennaBeacons();
            backdrop.haze = backdrop.AddHaze("Floor haze", backdrop.FloorHaze(look.Haze));
            backdrop.haze.enabled = FloorHazeShown;
            // Thomas, 2026-11-15: in Mission 3 the half-opacity towers let the far skyline's amber windows show through.
            // A solid black copy of their silhouette sits behind them, so they hide what stands behind them.
            if (plates && wave == DuelGame.FinalBossWave)
            {
                backdrop.nearMask = backdrop.AddPlate($"{look.NearLabel} silhouette", nearTexture, 1f);
                backdrop.nearMask.color = Color.black;
            }
            if (plates) backdrop.near = backdrop.AddPlate(look.NearLabel, nearTexture, look.NearOpacity);
            if (casino) backdrop.AddSignLetter();
            if (megacity) backdrop.AddTowerLights();
            backdrop.AddLitHaze();
            backdrop.UpdateParallax();
        }

        // Trial (Thomas, 2026-11-14): haze across the whole arena that only shows where a weapon or attack lights it.
        // Turn LitHaze off to get the floor-only look back. Steps above zero cuts each light into flat bands.
        internal static bool LitHaze = true;
        internal static int LitHazeSteps = 0;
        const int MaxLights = 16;
        const float MuzzleSeconds = .14f;
        static readonly Color CylinderLight = new Color32(98, 202, 255, 255);
        static readonly int LightsId = Shader.PropertyToID("_Lights"), LightColoursId = Shader.PropertyToID("_LightColours"),
            LightCountId = Shader.PropertyToID("_LightCount"), StepsId = Shader.PropertyToID("_Steps");
        readonly Vector4[] lights = new Vector4[MaxLights], lightColours = new Vector4[MaxLights];
        int lightCount;
        Material litMaterial;
        RawImage litHaze;
        DuelGame game;
        PlayerShooting rifle;
        SpriteRenderer cylinder;
        Vector2 muzzle;
        float muzzleAt = float.NegativeInfinity;

        void AddLitHaze()
        {
            var shader = Resources.Load<Shader>("ArenaLitHaze");
            if (shader == null) { Debug.LogError("Arena lit haze shader could not be loaded."); return; }
            litMaterial = new Material(shader) { name = "Arena lit haze", hideFlags = HideFlags.DontSave };
            litHaze = AddPlate("Lit haze", Texture2D.whiteTexture, 1f);
            litHaze.material = litMaterial;
            game = GetComponentInParent<DuelGame>();
            rifle = player.GetComponent<PlayerShooting>();
            if (rifle != null) rifle.ShotFired += OnShotFired;
            foreach (var part in player.GetComponentsInChildren<SpriteRenderer>(true))
                if (part.name == "RevolverCylinder") { cylinder = part; break; }
        }

        void OnShotFired(Vector2 origin, Vector2 direction)
        {
            muzzle = origin + direction.normalized * .3f;
            muzzleAt = Time.time;
        }

        void Light(Vector2 at, Color colour, float radius, float strength)
        {
            if (lightCount >= MaxLights || strength <= 0) return;
            // Thomas, 2026-11-14: restrained. One scale for every light, over the per-source values below.
            // Reduced again the same day (radius .72, strength .35 before).
            lights[lightCount] = new Vector4(at.x, at.y, radius * .62f, 0);
            var linear = colour.linear * (strength * .26f);
            lightColours[lightCount++] = new Vector4(linear.r, linear.g, linear.b, 0);
        }

        // Thomas, 2026-11-14: the rifle and the blade give off no light. Sword waves travel like bullets and keep theirs.
        void SwordWaveLights(BossAgent boss)
        {
            var traces = boss.Actions.SwordTraces;
            if (traces == null) return;
            foreach (var trace in traces)
            {
                if (trace == null || trace.Retired) continue;
                Light((trace.A(Time.time) + trace.B(Time.time)) * .5f, NeuralPalette.Swordsman, 1.4f,
                    Time.time < trace.Threat.StartsAt ? .2f : .4f);
            }
        }

        // Only the axe's energy gives off light (Thomas, 2026-11-14), not its cuts.
        // Phase one: the ice-blue socket charges through a throw's wind-up and the flying axe carries it.
        // Phase two: the violet axe is dim where it stands, swells through the order's delay and is brightest in flight.
        void AxeLights(BossAgent boss)
        {
            var axe = boss.Axe;
            var boxer = axe.Boxer;
            if (boxer != null)
            {
                if (!boxer.Detached || boxer.Retired) return;
                if (boxer.InFlight) Light(boxer.AxeEdge, NeuralPalette.Emp, 1.9f, .5f);
                else if (boxer.AxeOrdered) Light(boxer.AxeEdge, NeuralPalette.Emp, 1.4f, Mathf.Lerp(.22f, .5f, boxer.LaunchProgress));
                else Light(boxer.AxeEdge, NeuralPalette.Emp, 1.3f, .2f);
                return;
            }
            if (axe.AxeFlying) Light(axe.FlyingEdge, CylinderLight, 1.6f, .4f);
            else if (axe.WindingCast)
                Light(boss.Position + new Vector2(axe.LockedFacing * .35f, .35f), CylinderLight, 1.2f, .35f * axe.CastProgress);
        }

        // Read from what the fight already exposes; nothing here changes combat.
        void UpdateLights()
        {
            if (litMaterial == null) return;
            litHaze.enabled = LitHaze;
            if (!LitHaze) return;
            lightCount = 0;
            if (cylinder != null && cylinder.enabled && cylinder.gameObject.activeInHierarchy)
                Light(cylinder.transform.position, CylinderLight, 1.0f, .15f);
            float flash = 1f - (Time.time - muzzleAt) / MuzzleSeconds;
            if (flash > 0) Light(muzzle, CylinderLight, 1.8f, .5f * flash);
            var bosses = game != null ? game.Bosses : null;
            if (bosses != null)
                foreach (var boss in bosses)
                {
                    if (boss == null || !boss.IsAlive) continue;
                    if (boss.Axe != null) AxeLights(boss);
                    else if (!boss.IsTrainingEnemy && boss.Profile.Kind == BossKind.Swordsman) SwordWaveLights(boss);
                }
            foreach (var shot in Projectile.Active)
            {
                if (shot == null) continue;
                Color colour = shot.Actor == ProjectileActor.Player || shot.Actor == ProjectileActor.InnerAI ? CylinderLight
                    : shot.Deflected ? NeuralPalette.Swordsman
                    : shot.Owner != null && shot.Owner.TryGetComponent<BossAgent>(out var owner) && owner.IsTrainingEnemy
                        ? NeuralPalette.Secondary : NeuralPalette.Gunner;
                Light(shot.transform.position, colour, .9f, .3f);
            }
            litMaterial.SetVectorArray(LightsId, lights);
            litMaterial.SetVectorArray(LightColoursId, lightColours);
            litMaterial.SetFloat(LightCountId, lightCount);
            litMaterial.SetFloat(StepsId, LitHazeSteps);
        }

        RawImage AddPlate(string label, Texture texture, float opacity)
        {
            var plate = new GameObject(label, typeof(RawImage)).GetComponent<RawImage>();
            var rect = plate.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            plate.texture = texture;
            plate.color = new Color(1f, 1f, 1f, opacity);
            plate.raycastTarget = false;
            return plate;
        }

        RawImage AddHaze(string label, Texture2D texture)
        {
            var layer = AddPlate(label, texture, 1f);
            layer.material = hazeMaterial;
            return layer;
        }

        // Authored in display space, then stored linear and premultiplied.
        Texture2D HazeTexture(int width, int height, System.Func<float, float, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
            {
                name = "Arena haze", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    // u runs left to right; depth runs from the ceiling (0) to the floor (1).
                    pixels[y * width + x] = pixel((x + .5f) / width, 1f - (y + .5f) / height);
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            hazeTextures.Add(texture);
            return texture;
        }

        static Color Linear(Color display, float occlusion)
            => new Color(Mathf.GammaToLinearSpace(display.r), Mathf.GammaToLinearSpace(display.g),
                Mathf.GammaToLinearSpace(display.b), occlusion);

        // Even across the arena, strongest at the floor and gone a short way up. Added as light.
        Texture2D FloorHaze(Color colour) => HazeTexture(4, 128, (u, depth) =>
        {
            float rise = Mathf.Clamp01((depth - (1f - HazeHeight)) / HazeHeight);
            return Linear(colour * (HazeStrength * rise * rise), 0);
        });

        // Slow beacon cycles in seconds; the lights dim to a quarter and return.
        // The two periods differ, so the beacons drift in and out of step.
        const float BeaconPeriod = 3.2f, BeaconPeriodRight = 4.3f;

        static Color Beacon(float period)
        {
            float glow = .5f + .5f * Mathf.Sin(Time.time * 2f * Mathf.PI / period);
            return new Color(1f, 1f, 1f, Mathf.Lerp(.25f, 1f, glow));
        }

        void LateUpdate()
        {
            UpdateParallax();
            UpdateLights();
            if (farLights != null)
            {
                farLights.color = Beacon(BeaconPeriod);
                farLightsRight.color = Beacon(BeaconPeriodRight);
            }
            UpdateCasinoLights();
            UpdateMegacityLights();
        }

        // Mission 3 (Thomas, 2026-11-15, trial): light climbs the near tower's crown, a light crosses the skybridge and
        // four far antennas carry slow beacons. The front that traces the skyline puts all of it out for phase two.
        static readonly Rect TowerTile = new Rect(62, 236, 164, 126), BridgeTile = new Rect(864, 207, 72, 16);
        // Antenna tips on the far plate that show between the near towers; each tile is one 7-pixel point.
        static readonly Rect[] AntennaTiles =
            { new Rect(302, 256, 7, 7), new Rect(412, 149, 7, 7), new Rect(624, 164, 7, 7), new Rect(717, 220, 7, 7) };
        static readonly float[] AntennaPeriods = { 3.7f, 5.3f, 4.6f, 2.9f };
        const float TowerPeriod = 3.5f, TowerStrength = .38f, AntennaStrength = .7f;
        const float BridgeFrom = 868, BridgeTo = 932, BridgeCore = 8, BridgeGlow = 20;
        const float BridgeCrossing = 2.6f, BridgeRest = 1.6f, BridgeStrength = .5f;
        // The city goes dark once the skyline front is this far out.
        const float PowerCutAt = .3f;
        RawImage[] towerTiers, antennas;
        RawImage bridgeCore, bridgeGlow;
        float powerCutAt = float.NaN;

        void AddAntennaBeacons()
        {
            antennas = new RawImage[AntennaTiles.Length];
            for (int i = 0; i < antennas.Length; i++) antennas[i] = AddTile($"Antenna beacon {i + 1}", "MegacityBeacon");
        }

        void AddTowerLights()
        {
            towerTiers = new RawImage[4];
            for (int tier = 0; tier < towerTiers.Length; tier++)
                towerTiers[tier] = AddTile($"Tower crown tier {tier + 1}", $"MegacityCrown{tier + 1}");
            bridgeGlow = AddTile("Skybridge light glow", "MegacityBridge");
            bridgeCore = AddTile("Skybridge light", "MegacityBridge");
        }

        // 0 while the EMP has not reached the city; 1 once the skyline front has finished (or on a direct phase-two entry).
        internal void SetEmpCity(float progress)
        {
            if (!float.IsNaN(powerCutAt) || progress < PowerCutAt) return;
            // A direct entry has no transition to watch, so the city is already dark.
            powerCutAt = progress >= 1 ? float.NegativeInfinity : Time.time;
        }

        // Full until the power is cut, then dark. Two stutters on the way out were built first; Thomas found them unnecessary.
        float CityPower() => float.IsNaN(powerCutAt) ? 1 : 0;

        void UpdateMegacityLights()
        {
            if (towerTiers == null && antennas == null) return;
            float power = CityPower();
            bool steady = GameSettings.ReduceFlashing;
            if (antennas != null)
                for (int i = 0; i < antennas.Length; i++)
                {
                    if (antennas[i] == null) continue;
                    float glow = .5f + .5f * Mathf.Sin(Time.time * 2f * Mathf.PI / AntennaPeriods[i] + i * 1.7f);
                    antennas[i].color = new Color(1f, 1f, 1f, power * AntennaStrength * (steady ? .7f : Mathf.Lerp(.15f, 1f, glow)));
                }
            if (towerTiers == null) return;
            // One crest climbs from the lowest setback to the spire and starts again from the bottom.
            for (int tier = 0; tier < towerTiers.Length; tier++)
            {
                if (towerTiers[tier] == null) continue;
                float wave = .5f + .5f * Mathf.Cos(2f * Mathf.PI * (Time.time / TowerPeriod - tier / (float)towerTiers.Length));
                towerTiers[tier].color = new Color(1f, 1f, 1f, steady ? 0 : power * wave * wave * TowerStrength);
            }
            float bridge = steady ? 0 : power * BridgeStrength;
            if (bridgeCore != null) bridgeCore.color = new Color(1f, 1f, 1f, bridge);
            if (bridgeGlow != null) bridgeGlow.color = new Color(1f, 1f, 1f, bridge * .45f);
        }

        // Where the skybridge light is, in plate pixels: across, a rest out of sight, back, a rest.
        float BridgeLightCentre()
        {
            float lap = BridgeCrossing + BridgeRest, t = Mathf.Repeat(Time.time, lap * 2);
            bool back = t >= lap;
            float along = Mathf.Clamp01((back ? t - lap : t) / BridgeCrossing);
            float from = BridgeFrom - BridgeGlow * .5f, to = BridgeTo + BridgeGlow * .5f;
            return Mathf.Lerp(from, to, back ? 1 - along : along);
        }

        // Shows only the tile's pixels between two plate columns: the image itself is narrowed to that span.
        static void SetTileWindow(RawImage tile, RawImage plate, Rect pixels, float fromPixel, float toPixel)
        {
            if (tile == null) return;
            fromPixel = Mathf.Clamp(fromPixel, pixels.xMin, pixels.xMax);
            toPixel = Mathf.Clamp(toPixel, fromPixel, pixels.xMax);
            float left = plate.uvRect.x * plate.texture.width;
            var rect = tile.rectTransform;
            rect.anchorMin = new Vector2((fromPixel - left) / VisibleWidthPixels, 0f);
            rect.anchorMax = new Vector2((toPixel - left) / VisibleWidthPixels, 1f);
            tile.uvRect = new Rect((fromPixel - pixels.x) / pixels.width, -pixels.y / pixels.height,
                (toPixel - fromPixel) / pixels.width, plate.texture.height / pixels.height);
        }

        // Mission 2 (Thomas, 2026-11-15, trial): light flows across the hotel's fan crown without a rest, the marquee's bulbs
        // run as a chase, every third one lit, and one letter of the sign drops out now and then.
        // Small tiles hold only the plate's own lit pixels and lie over it; the plate art is not altered.
        // Tile rectangles are in plate pixels, measured from the plate's lower left.
        static readonly Rect CrownTile = new Rect(834, 258, 142, 68), SignTile = new Rect(137, 259, 16, 24),
            MarqueeTile = new Rect(0, 194, 232, 36);
        // The crown was a sweep every 6 s at .6 with a rest; he asked for it continuous, flowing and a little dimmer.
        const float CrownPeriod = 2.6f, CrownStrength = .42f;
        const float ChaseStep = .2f, ChaseStrength = .8f;
        const float SignOutStrength = .85f;
        RawImage[] crownFins, chaseBulbs;
        RawImage signLetter;
        readonly System.Random sceneryDice = new System.Random();
        float signOutAt = float.NaN, signOutSeconds = .7f;

        RawImage AddTile(string label, string resource)
        {
            var texture = Resources.Load<Texture2D>($"Arena/{resource}");
            if (texture == null) { Debug.LogError($"Background light {resource} could not be loaded."); return null; }
            var tile = AddPlate(label, texture, 1f);
            tile.color = Color.clear;
            return tile;
        }

        void AddCrownLights()
        {
            crownFins = new RawImage[5];
            for (int fin = 0; fin < crownFins.Length; fin++)
                crownFins[fin] = AddTile($"Crown fin {fin + 1}", $"CasinoCrown{fin + 1}");
        }

        void AddSignLetter()
        {
            chaseBulbs = new RawImage[3];
            for (int phase = 0; phase < chaseBulbs.Length; phase++)
                chaseBulbs[phase] = AddTile($"Marquee bulbs {phase + 1}", $"CasinoMarquee{phase + 1}");
            signLetter = AddTile("Sign letter out", "CasinoSignLetter");
        }

        float Between(float from, float to) => Mathf.Lerp(from, to, (float)sceneryDice.NextDouble());

        void UpdateCasinoLights()
        {
            bool steady = GameSettings.ReduceFlashing;
            if (crownFins != null)
            {
                // One crest travels left to right and comes round again; each fin is a fifth of a turn behind the last.
                for (int fin = 0; fin < crownFins.Length; fin++)
                {
                    if (crownFins[fin] == null) continue;
                    float wave = .5f + .5f * Mathf.Cos(2f * Mathf.PI * (Time.time / CrownPeriod - fin / (float)crownFins.Length));
                    crownFins[fin].color = new Color(1f, 1f, 1f, steady ? 0 : wave * wave * CrownStrength);
                }
            }
            if (chaseBulbs != null)
            {
                // Hard steps, as a real chaser switches: the lit third moves one bulb to the right each step.
                int lit = Mathf.FloorToInt(Time.time / ChaseStep) % chaseBulbs.Length;
                for (int phase = 0; phase < chaseBulbs.Length; phase++)
                    if (chaseBulbs[phase] != null)
                        chaseBulbs[phase].color = new Color(1f, 1f, 1f, !steady && phase == lit ? ChaseStrength : 0);
            }
            if (signLetter == null) return;
            if (float.IsNaN(signOutAt)) signOutAt = Time.time + Between(10, 25);
            // A failing tube: out, back for an instant, out for a while, then it catches again.
            float e = Time.time - signOutAt, d = signOutSeconds;
            bool dark = !steady && e >= 0 && (e < .07f || (e >= .14f && e < .14f + d) || (e >= .2f + d && e < .26f + d));
            signLetter.color = new Color(1f, 1f, 1f, dark ? SignOutStrength : 0);
            if (e >= .26f + d)
            {
                signOutAt = Time.time + Between(20, 40);
                signOutSeconds = Between(.5f, .9f);
            }
        }

        // A tile shows its own rectangle of the plate it lies over, wherever that plate's crop has moved.
        static void SetTileCrop(RawImage tile, RawImage plate, Rect pixels)
        {
            if (tile == null) return;
            float left = plate.uvRect.x * plate.texture.width;
            tile.uvRect = new Rect((left - pixels.x) / pixels.width, -pixels.y / pixels.height,
                VisibleWidthPixels / pixels.width, plate.texture.height / pixels.height);
        }

        internal void AddEmpContours(Material material)
        {
            if (far == null || near == null) return;
            var mask = Resources.Load<Texture2D>("Arena/MegacityContours");
            if (mask == null)
            {
                Debug.LogError("Mission 3 architectural contour texture could not be loaded.");
                return;
            }
            var contours = AddPlate("EMP architectural contours", mask, 1f);
            contourMaterial = contours.material = material;
            contourMaterial.SetFloat("_CityContours", 1);
            // A violet copy of the haze sits directly above the cold-white layer, hidden until the sweep dyes it.
            empHaze = AddHaze("EMP floor haze", FloorHaze(NeuralPalette.Emp));
            empHaze.transform.SetSiblingIndex(haze.transform.GetSiblingIndex() + 1);
            empHaze.enabled = FloorHazeShown;
            SetEmpHaze(0);
            UpdateParallax();
        }

        // 0 keeps the mission's own haze; 1 is fully EMP violet. Driven by the front that traces the skyline.
        internal void SetEmpHaze(float amount)
        {
            if (empHaze == null) return;
            amount = Mathf.Clamp01(amount);
            haze.color = new Color(1f, 1f, 1f, 1f - amount);
            empHaze.color = new Color(1f, 1f, 1f, amount);
        }

        // The layers ease toward the player's side of the arena, so a Dodge or a hit does not jerk the scenery.
        const float ParallaxSeconds = .35f;
        float shownPosition, parallaxVelocity;
        bool parallaxPlaced;

        void UpdateParallax()
        {
            if (far == null || near == null) return;
            float target = Mathf.Clamp((player.position.x - arena.center.x) / arena.extents.x, -1f, 1f);
            if (!parallaxPlaced) { shownPosition = target; parallaxPlaced = true; }
            else if (Time.deltaTime > 0) shownPosition = Mathf.SmoothDamp(shownPosition, target, ref parallaxVelocity, ParallaxSeconds);
            float position = shownPosition;
            SetCrop(far, position * FarTravelPixels);
            SetCrop(near, position * NearTravelPixels);
            if (farLights != null)
            {
                var crop = far.uvRect;
                farLights.uvRect = new Rect(crop.x, 0f, crop.width * .5f, 1f);
                farLightsRight.uvRect = new Rect(crop.x + crop.width * .5f, 0f, crop.width * .5f, 1f);
            }
            if (crownFins != null) foreach (var fin in crownFins) SetTileCrop(fin, far, CrownTile);
            if (chaseBulbs != null) foreach (var bulbs in chaseBulbs) SetTileCrop(bulbs, near, MarqueeTile);
            SetTileCrop(signLetter, near, SignTile);
            if (antennas != null) for (int i = 0; i < antennas.Length; i++) SetTileCrop(antennas[i], far, AntennaTiles[i]);
            if (towerTiers != null)
            {
                foreach (var tier in towerTiers) SetTileCrop(tier, near, TowerTile);
                float centre = BridgeLightCentre();
                SetTileWindow(bridgeCore, near, BridgeTile, centre - BridgeCore * .5f, centre + BridgeCore * .5f);
                SetTileWindow(bridgeGlow, near, BridgeTile, centre - BridgeGlow * .5f, centre + BridgeGlow * .5f);
            }
            if (nearMask != null) nearMask.uvRect = near.uvRect;
            if (contourMaterial != null)
                contourMaterial.SetVector("_CityCrops", new Vector4(far.uvRect.x, far.uvRect.width,
                    near.uvRect.x, near.uvRect.width));
        }

        static void SetCrop(RawImage plate, float offsetPixels)
        {
            float width = plate.texture.width;
            float left = (width - VisibleWidthPixels) * .5f + offsetPixels;
            plate.uvRect = new Rect(left / width, 0f, VisibleWidthPixels / width, 1f);
        }

        void OnDestroy()
        {
            foreach (var texture in hazeTextures) if (texture != null) Destroy(texture);
            if (hazeMaterial != null) Destroy(hazeMaterial);
            if (litMaterial != null) Destroy(litMaterial);
            if (rifle != null) rifle.ShotFired -= OnShotFired;
        }
    }
}
