using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        RectTransform titleBackdrop;
        RectTransform[] titleLayers;
        Vector2 titleLook;
        // Travel at the screen edge, in 1600 x 900 units, of the far mist, the hills, the near mist, the near slope
        // and the roof. Trial tuning.
        static readonly Vector2[] TitleTravel =
            { new Vector2(1.5f, .5f), new Vector2(3, 1), new Vector2(4.5f, 1.5f), new Vector2(6, 2), new Vector2(10, 3) };

        void BuildTitleBackdrop()
        {
            // Environment and figure share one frame so the feet stay on the roof at every aspect ratio.
            titleBackdrop = Rect("Title landscape", page, Vector2.zero, new Vector2(1600, 900));
            titleBackdrop.anchorMin = titleBackdrop.anchorMax = titleBackdrop.pivot = new Vector2(.5f, 0);
            // Tools/cut_title_layers.py cuts Outskirts.png into these; stacked at rest they are that plate.
            RawImage Layer(string name)
            {
                // The clamped margin keeps a shifted layer from uncovering the screen edge.
                const float margin = 16;
                var layer = Rect(name, titleBackdrop, Vector2.zero, new Vector2(1600 + margin * 2, 900 + margin * 2));
                var image = layer.gameObject.AddComponent<RawImage>();
                image.texture = Resources.Load<Texture2D>("Title/" + name);
                image.uvRect = new Rect(-margin / 1600, -margin / 900, 1 + margin * 2 / 1600, 1 + margin * 2 / 900);
                image.raycastTarget = false;
                return image;
            }
            // Mist lies between the layers, against each ridge line, and is lit from the city.
            RectTransform Mist(string name, Color colour)
            {
                var image = Layer(name);
                image.material = TitleLight(true); image.color = colour;
                return image.rectTransform;
            }
            var sky = Layer("OutskirtsSky").rectTransform;
            BuildTitleStars(sky);
            BuildTitleBeams(sky);
            BuildTitleCity(sky);
            titleLayers = new[]
            {
                Mist("OutskirtsMistFar", new Color(.34f, .66f, .92f, .22f)), Layer("OutskirtsHills").rectTransform,
                Mist("OutskirtsMistNear", new Color(.22f, .46f, .68f, .11f)), Layer("OutskirtsSlope").rectTransform,
                Layer("OutskirtsRoof").rectTransform,
            };
            titleLook = TitlePointer();
            // Stand inside the back rim on the roof surface, not directly on its bright edge.
            BuildTitlePlayer(titleLayers[4], new Vector2(443, -281), 105);
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

        static Sprite titleBeamSprite;
        static Material titleBeamMaterial, titleMistMaterial;

        // Light adds to what is behind it. The mist material also lets slow noise drift through its shape.
        static Material TitleLight(bool mist)
        {
            if (titleBeamMaterial == null)
            {
                var shader = Resources.Load<Shader>("TitleLight");
                titleBeamMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                titleMistMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                titleMistMaterial.SetFloat("_Flow", .55f);
            }
            return mist ? titleMistMaterial : titleBeamMaterial;
        }

        // Rooftops on the plate, the resting lean in degrees, the swing, its period in seconds and the length.
        static readonly float[] TitleBeamSetup =
        {
            790, 755, 10, 12, 13.5f, 300,
            903, 763, -8, 14, 17.5f, 230,
        };
        static readonly Color TitleBeamColour = new Color(.62f, .86f, 1f);
        RectTransform[] titleBeams;

        void BuildTitleBeams(RectTransform sky)
        {
            // A far searchlight is a nearly parallel shaft with no outline: a bright core that spreads into
            // nothing at the sides, strongest where it leaves the lamp and gone before its end.
            if (titleBeamSprite == null)
            {
                const int width = 64, height = 256;
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                    { name = "Title beam", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        float v = (y + .5f) / height, spread = Mathf.Lerp(.4f, 1, v);
                        float side = Mathf.Abs((x + .5f) / width - .5f) * 2 / spread; side *= side;
                        float across = (Mathf.Exp(-side * 5) * .6f + Mathf.Exp(-side * 40) * .4f) / spread;
                        byte value = (byte)(255 * Mathf.Clamp01(across * .4f * Mathf.Pow(1 - v, 2.2f)));
                        pixels[y * width + x] = new Color32(value, value, value, value);
                    }
                texture.SetPixels32(pixels); texture.Apply();
                titleBeamSprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, 0), 100);
            }
            Image Shape(string name, Sprite sprite, Vector2 point, Vector2 size, float alpha)
            {
                var image = Rect(name, sky, point, size).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = NeuralPalette.Alpha(TitleBeamColour, alpha); image.raycastTarget = false;
                return image;
            }
            titleBeams = new RectTransform[TitleBeamSetup.Length / 6];
            for (int i = 0; i < titleBeams.Length; i++)
            {
                Vector2 lamp = TitlePlatePoint(TitleBeamSetup[i * 6], TitleBeamSetup[i * 6 + 1]);
                var beam = Shape("Searchlight", titleBeamSprite, lamp, new Vector2(20, TitleBeamSetup[i * 6 + 5]), 1);
                beam.material = TitleLight(false);
                beam.rectTransform.pivot = new Vector2(.5f, 0);
                titleBeams[i] = beam.rectTransform;
                // The lamp itself: a small glow and a hard point on the roof.
                Shape("Lamp glow", StickmanSprites.Pool, lamp, new Vector2(11, 6), .32f);
                Shape("Lamp", StickmanSprites.Block, lamp, Vector2.one * 1.5f, .95f);
            }
        }

        void UpdateTitleBeams()
        {
            float now = Time.unscaledTime;
            titleMistMaterial.SetFloat("_Clock", now);
            // A moving head turns smoothly; the periods never line up, and now and then the two shafts cross.
            for (int i = 0; i < titleBeams.Length; i++)
                titleBeams[i].localRotation = Quaternion.Euler(0, 0, TitleBeamSetup[i * 6 + 2] + TitleBeamSetup[i * 6 + 3]
                    * Mathf.Sin(now * Mathf.PI * 2 / TitleBeamSetup[i * 6 + 4] + i * 2.1f));
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
