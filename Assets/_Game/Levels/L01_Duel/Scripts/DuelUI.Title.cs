using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform titleBackdrop;
        RectTransform[] titleLayers;
        Vector2 titleLook;
        // Travel of the hills, the near slope and the roof at the screen edge, in 1600 x 900 units. Trial tuning.
        static readonly Vector2[] TitleTravel = { new Vector2(3, 1), new Vector2(6, 2), new Vector2(10, 3) };

        void BuildTitleBackdrop()
        {
            // Environment and figure share one frame so the feet stay on the roof at every aspect ratio.
            titleBackdrop = Rect("Title landscape", page, Vector2.zero, new Vector2(1600, 900));
            titleBackdrop.anchorMin = titleBackdrop.anchorMax = titleBackdrop.pivot = new Vector2(.5f, 0);
            // Tools/cut_title_layers.py cuts Outskirts.png into these; stacked at rest they are that plate.
            RectTransform Layer(string name)
            {
                // The clamped margin keeps a shifted layer from uncovering the screen edge.
                const float margin = 16;
                var layer = Rect(name, titleBackdrop, Vector2.zero, new Vector2(1600 + margin * 2, 900 + margin * 2));
                var image = layer.gameObject.AddComponent<RawImage>();
                image.texture = Resources.Load<Texture2D>("Title/" + name);
                image.uvRect = new Rect(-margin / 1600, -margin / 900, 1 + margin * 2 / 1600, 1 + margin * 2 / 900);
                image.raycastTarget = false;
                return layer;
            }
            var sky = Layer("OutskirtsSky");
            BuildTitleStars(sky);
            BuildTitleBeams(sky);
            BuildTitleCity(sky);
            titleLayers = new[] { Layer("OutskirtsHills"), Layer("OutskirtsSlope"), Layer("OutskirtsRoof") };
            titleLook = TitlePointer();
            // Stand inside the back rim on the roof surface, not directly on its bright edge.
            BuildTitlePlayer(titleLayers[2], new Vector2(443, -281), 105);
            UpdateTitleBackdrop();
        }

        static Vector2 TitlePointer()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !Application.isFocused) return Vector2.zero;
            Vector2 point = mouse.position.ReadValue();
            return new Vector2(Mathf.Clamp(point.x / Screen.width * 2 - 1, -1, 1),
                Mathf.Clamp(point.y / Screen.height * 2 - 1, -1, 1));
        }

        void UpdateTitleBackdrop()
        {
            if (titleBackdrop == null) return;
            var extent = ((RectTransform)titleBackdrop.parent).rect.size;
            // Bottom alignment keeps the roof; wide screens crop empty sky first.
            titleBackdrop.localScale = Vector3.one * Mathf.Max(extent.x / 1600f, extent.y / 900f);
            // Nearer layers slide further, away from the pointer; the sky and the city stay.
            titleLook = Vector2.Lerp(titleLook, TitlePointer(), 1 - Mathf.Exp(-4 * Time.unscaledDeltaTime));
            for (int i = 0; i < titleLayers.Length; i++)
                titleLayers[i].anchoredPosition = -Vector2.Scale(titleLook, TitleTravel[i]);
            UpdateTitleStars();
            UpdateTitleBeams();
            UpdateTitleCity();
        }

        static Sprite titleBeamSprite, titleBandSprite;
        static Sprite TitleSprite(string name, int width, int height, Vector2 pivot, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = name, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    pixels[y * width + x] = new Color32(255, 255, 255,
                        (byte)(255 * Mathf.Clamp01(alpha((x + .5f) / width, (y + .5f) / height))));
            texture.SetPixels32(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 100);
        }

        // Rooftops on the plate, the resting lean in degrees, the swing and its period in seconds.
        static readonly float[] TitleBeamSetup =
        {
            790, 755, 9, 13, 11.5f,
            903, 763, -7, 15, 15.5f,
            963, 770, -15, 9, 9.25f,
        };
        // Plate row, width in units, plate column at rest, drift in units and its period in seconds.
        static readonly float[] TitleBandSetup = { 745, 420, 850, 40, 96, 725, 280, 915, -30, 71 };
        const float TitleBeamLength = 280;
        static readonly Color TitleBeamColour = new Color(.62f, .86f, 1f);
        RectTransform[] titleBeams, titleBands;
        Image[] titleBeamPools;

        void BuildTitleBeams(RectTransform sky)
        {
            // A beam is a flat wedge with straight edges that thins out along its length.
            if (titleBeamSprite == null)
                titleBeamSprite = TitleSprite("Title beam", 64, 256, new Vector2(.5f, 0), (u, v) =>
                    Mathf.Clamp01((Mathf.Lerp(.09f, .5f, v) - Mathf.Abs(u - .5f)) * 64 + .5f) * Mathf.Pow(1 - v, 1.4f));
            // Haze is a thin level slab that fades only toward its ends.
            if (titleBandSprite == null)
                titleBandSprite = TitleSprite("Title haze band", 128, 2, new Vector2(.5f, .5f), (u, v) =>
                    Mathf.Pow(Mathf.Sin(u * Mathf.PI), 1.5f));
            Image Shape(string name, Sprite sprite, Vector2 point, Vector2 size, float alpha)
            {
                var image = Rect(name, sky, point, size).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = NeuralPalette.Alpha(TitleBeamColour, alpha); image.raycastTarget = false;
                return image;
            }
            int bands = TitleBandSetup.Length / 5, beams = TitleBeamSetup.Length / 5;
            titleBands = new RectTransform[bands];
            for (int i = 0; i < bands; i++)
                titleBands[i] = Shape("Haze band", titleBandSprite, TitlePlatePoint(TitleBandSetup[i * 5 + 2], TitleBandSetup[i * 5]),
                    new Vector2(TitleBandSetup[i * 5 + 1], 6), .05f).rectTransform;
            titleBeams = new RectTransform[beams];
            titleBeamPools = new Image[beams * bands];
            for (int i = 0; i < beams; i++)
            {
                var beam = Shape("Searchlight", titleBeamSprite, TitlePlatePoint(TitleBeamSetup[i * 5], TitleBeamSetup[i * 5 + 1]),
                    new Vector2(13, TitleBeamLength), .15f);
                beam.rectTransform.pivot = new Vector2(.5f, 0);
                titleBeams[i] = beam.rectTransform;
                // Where a beam crosses a band, the haze it reaches lights up.
                for (int b = 0; b < bands; b++)
                    titleBeamPools[i * bands + b] = Shape("Lit haze", titleBandSprite, Vector2.zero, new Vector2(44, 6), 0);
            }
        }

        void UpdateTitleBeams()
        {
            float now = Time.unscaledTime;
            int bands = titleBands.Length;
            for (int b = 0; b < bands; b++)
            {
                Vector2 rest = TitlePlatePoint(TitleBandSetup[b * 5 + 2], TitleBandSetup[b * 5]);
                rest.x += TitleBandSetup[b * 5 + 3] * Mathf.Sin(now * Mathf.PI * 2 / TitleBandSetup[b * 5 + 4]);
                titleBands[b].anchoredPosition = rest;
            }
            for (int i = 0; i < titleBeams.Length; i++)
            {
                // A moving head turns smoothly; the periods never line up.
                float lean = TitleBeamSetup[i * 5 + 2] + TitleBeamSetup[i * 5 + 3]
                    * Mathf.Sin(now * Mathf.PI * 2 / TitleBeamSetup[i * 5 + 4] + i * 2.1f);
                titleBeams[i].localRotation = Quaternion.Euler(0, 0, lean);
                Vector2 foot = titleBeams[i].anchoredPosition;
                float slope = Mathf.Tan(lean * Mathf.Deg2Rad);
                for (int b = 0; b < bands; b++)
                {
                    var pool = titleBeamPools[i * bands + b];
                    Vector2 band = titleBands[b].anchoredPosition;
                    float rise = band.y - foot.y, x = foot.x - slope * rise;
                    float along = rise / Mathf.Cos(lean * Mathf.Deg2Rad) / TitleBeamLength;
                    float inside = 1 - Mathf.Abs(x - band.x) / (TitleBandSetup[b * 5 + 1] * .5f);
                    pool.rectTransform.anchoredPosition = new Vector2(x, band.y);
                    pool.color = NeuralPalette.Alpha(TitleBeamColour,
                        rise > 0 && along < 1 ? .16f * Mathf.Clamp01(inside * 3) * (1 - along) : 0);
                }
            }
        }

        Image[] titleTwinklers;
        float[] titleTwinkleLevel, titleTwinkleTurn;

        void BuildTitleStars(RectTransform sky)
        {
            // The same sky every launch. Out here the stars show; the city's glow puts them out toward the horizon.
            var dice = new System.Random(11);
            float Roll() => (float)dice.NextDouble();
            var twinklers = new System.Collections.Generic.List<Image>();
            var levels = new System.Collections.Generic.List<float>();
            for (int tries = 0, made = 0; tries < 4000 && made < 150; tries++)
            {
                float x = Roll() * 1672, y = Roll() * 640;
                float chance = Mathf.Pow(Mathf.Clamp01((640 - y) / 420), 1.3f);
                chance *= Mathf.Clamp01((new Vector2(x - 858, (y - 760) * 1.4f).magnitude - 90) / 330);
                Vector2 point = TitlePlatePoint(x, y);
                // Few behind the logo, where they would only show between the letters.
                if (point.x > -730 && point.x < 330 && point.y > 50 && point.y < 365) chance *= .3f;
                if (Roll() > chance) continue;
                made++;
                float bright = Roll(); bright *= bright;
                bool large = bright > .55f;
                float level = Mathf.Lerp(.22f, .9f, bright) * Mathf.Lerp(.55f, 1, chance);
                var star = Rect("Star", sky, point, Vector2.one * (large ? 2.2f : 1.35f)).gameObject.AddComponent<Image>();
                star.sprite = StickmanSprites.Block; star.raycastTarget = false;
                star.color = new Color(.80f + .12f * Roll(), .90f, 1, level);
                if (large && twinklers.Count < 14) { twinklers.Add(star); levels.Add(level); }
            }
            titleTwinklers = twinklers.ToArray(); titleTwinkleLevel = levels.ToArray();
            titleTwinkleTurn = new float[titleTwinklers.Length];
            for (int i = 0; i < titleTwinkleTurn.Length; i++) titleTwinkleTurn[i] = Time.unscaledTime + Random.Range(1f, 8f);
        }

        void UpdateTitleStars()
        {
            if (GameSettings.ReduceFlashing) return;
            // A few of the brighter stars dip one or two steps for a moment, then hold for seconds.
            float now = Time.unscaledTime;
            for (int i = 0; i < titleTwinklers.Length; i++)
            {
                if (now < titleTwinkleTurn[i]) continue;
                var colour = titleTwinklers[i].color;
                bool dipped = colour.a < titleTwinkleLevel[i] - .01f;
                colour.a = titleTwinkleLevel[i] * (dipped ? 1 : Random.value < .5f ? .55f : .25f);
                titleTwinklers[i].color = colour;
                titleTwinkleTurn[i] = now + (dipped ? Random.Range(2.5f, 9f) : Random.Range(.12f, .32f));
            }
        }

        // Dark facade spots on the plate, in its own pixels, picked once offline from the building silhouettes;
        // the spire tip follows. A window is two plate pixels square.
        static readonly short[] TitleWindowSpots =
        {
            747,799, 791,780, 903,780, 931,802, 857,745, 810,783, 824,780, 804,799, 768,769, 923,801, 821,766, 789,791, 768,797,
            796,800, 825,799, 838,795, 857,769, 783,761, 777,776, 741,792, 954,796, 776,801, 827,789, 972,790, 815,804, 855,782,
        };
        static readonly Vector2 TitleSpireTip = new Vector2(861.5f, 714);
        static readonly Color TitleWindowCold = new Color(.78f, .92f, 1f, .85f), TitleWindowWarm = new Color(.93f, .58f, .38f, .85f);
        Image[] titleWindows;
        float[] titleWindowTurn;
        Image titleGlow, titleBeacon, titleBeaconHalo;

        // Plate pixels, measured from its top left, to the 1600 x 900 frame the layers share.
        static Vector2 TitlePlatePoint(float x, float y) => new Vector2((x - 836) * 1600 / 1672, (470.5f - y) * 900 / 941);

        void BuildTitleCity(RectTransform sky)
        {
            Image Light(string name, Sprite sprite, Vector2 point, Vector2 size, Color colour)
            {
                var image = Rect(name, sky, point, size).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = colour; image.raycastTarget = false;
                return image;
            }
            // The plate already paints the glow behind the city; this only lets it breathe.
            titleGlow = Light("Horizon glow", StickmanSprites.Pool, TitlePlatePoint(858, 790), new Vector2(620, 150), Color.clear);
            int count = TitleWindowSpots.Length / 2;
            titleWindows = new Image[count]; titleWindowTurn = new float[count];
            for (int i = 0; i < count; i++)
            {
                titleWindows[i] = Light("Window", StickmanSprites.Block,
                    TitlePlatePoint(TitleWindowSpots[i * 2] + 1, TitleWindowSpots[i * 2 + 1] + 1), Vector2.one * 1.92f,
                    i % 6 == 4 ? TitleWindowWarm : TitleWindowCold);
                titleWindows[i].enabled = Random.value < .5f;
                titleWindowTurn[i] = Time.unscaledTime + Random.Range(2f, 40f);
            }
            Vector2 tip = TitlePlatePoint(TitleSpireTip.x, TitleSpireTip.y);
            titleBeaconHalo = Light("Beacon halo", StickmanSprites.Pool, tip, new Vector2(15, 15), Color.clear);
            titleBeacon = Light("Beacon", StickmanSprites.Block, tip, Vector2.one * 2.4f, Color.clear);
        }

        void UpdateTitleCity()
        {
            float now = Time.unscaledTime;
            // Windows switch outright, one at a time and rarely: lit 15 to 60 s, dark 10 to 40 s.
            for (int i = 0; i < titleWindows.Length; i++)
            {
                if (now < titleWindowTurn[i]) continue;
                bool lit = titleWindows[i].enabled = !titleWindows[i].enabled;
                titleWindowTurn[i] = now + (lit ? Random.Range(15f, 60f) : Random.Range(10f, 40f));
            }
            // The spire beacon steps on, half and off every three seconds. Reduce Flashing holds it steady.
            float beat = now % 3;
            float beacon = GameSettings.ReduceFlashing ? .7f : beat < .9f ? 1 : beat < 1.05f ? .4f : 0;
            titleBeacon.color = NeuralPalette.Alpha(NeuralPalette.Danger, beacon);
            titleBeaconHalo.color = NeuralPalette.Alpha(NeuralPalette.Danger, beacon * .22f);
            // Too slow and too faint to step: about a ten-second swell.
            titleGlow.color = new Color(.36f, .72f, .95f, .035f + .03f * Mathf.Sin(now * Mathf.PI * 2 / 10.5f));
        }

        void BuildTitlePlayer(RectTransform parent, Vector2 origin, float k)
        {
            // The cover looks at the player's back. Combat's side-on, bent-knee skeleton does not fit
            // this camera: keep its visual identity, but construct the shoulder/hip plane and limbs here.
            var root = Rect("Player back", parent, origin, Vector2.one * (3 * k));
            Color white = NeuralPalette.Primary;
            Color far = Color.Lerp(white, new Color(.35f, .44f, .50f), .16f);
            // The roof recedes up to screen-right. Project horizontal body axes along that same
            // direction while keeping vertical axes upright; do not roll the whole person sideways.
            Vector2 Project(Vector2 p) => new Vector2(p.x * .94f, p.y + p.x * .105f);

            Image Part(string name, Sprite sprite, Vector2 point, Vector2 size, Color colour, float angle = 0)
            {
                var image = Rect(name, root, point * k, size * k).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = colour; image.raycastTarget = false;
                image.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                return image;
            }
            void Limb(string name, Vector2 a, Vector2 b, float width, Color colour)
            {
                // Project the two attachment points only. Each limb remains one rigid rectangle,
                // so its end caps stay perpendicular to its long sides, including at the floor.
                a = Project(a); b = Project(b);
                Vector2 delta = b - a;
                // Opaque geometry keeps edges sharp; a stretched transparent border blurs the end caps.
                Part(name, StickmanSprites.Block, (a + b) * .5f,
                    new Vector2(delta.magnitude, width),
                    colour, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }

            // Fit the actual revolver to the end of the single right-arm block, a little closer in.
            Vector2 rightHand = new Vector2(.26f, -.24f), leftHand = new Vector2(-.26f, -.24f);
            const float gunAngle = -86, foreshorten = .80f;
            Vector2 GunPoint(Vector2 point) => (Vector2)(Quaternion.Euler(0, 0, gunAngle)
                * new Vector2(point.x * foreshorten, point.y));
            Vector2 handOnRoof = Project(rightHand);
            Vector2 muzzle = handOnRoof - GunPoint(RevolverPresentationGeometry.GripPosition);
            void GunPart(string name, Sprite sprite, Vector2 offset, Color colour)
            {
                var image = Part(name, sprite, muzzle + GunPoint(offset), sprite.bounds.size, colour, gunAngle);
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                image.rectTransform.localScale = new Vector3(foreshorten, 1, 1);
            }
            GunPart("Revolver", RevolverPresentationGeometry.Body, Vector2.zero, RevolverPresentationGeometry.BodyColor);
            GunPart("Core halo", RevolverPresentationGeometry.Halo, RevolverPresentationGeometry.CorePosition, new Color(.35f, .78f, 1f));
            GunPart("Core", RevolverPresentationGeometry.Core, RevolverPresentationGeometry.CorePosition, RevolverPresentationGeometry.CoreColor);

            // The arm itself overlaps the grip; no separate hand, elbow, knee or calf pieces.
            Limb("Right arm", new Vector2(.135f, .31f), rightHand, .16f, far);
            Limb("Right leg", new Vector2(.075f, -.115f), new Vector2(.185f, -.78f), .17f, far);
            Limb("Left leg", new Vector2(-.065f, -.115f), new Vector2(-.175f, -.78f), .17f, white);

            // Keep the approved torso and neck proportions; only flatten the torso edges.
            // No raised side-plane corners beside the neck.
            Limb("Back", new Vector2(0, -.16f), new Vector2(0, .35f), .22f, white);
            Limb("Neck", new Vector2(-.02f, .32f), new Vector2(-.035f, .46f), .125f, white);
            // No player-drawn face on the back of the head. The slight left turn faces the distant city.
            Part("Head back", StickmanSprites.Disc, Project(new Vector2(-.045f, .59f)), new Vector2(.46f, .46f), white);
            Limb("Left arm", new Vector2(-.135f, .31f), leftHand, .16f, white);
        }
    }
}
