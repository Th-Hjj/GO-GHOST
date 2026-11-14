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
            var skyImage = Layer("OutskirtsSky");
            var sky = skyImage.rectTransform;
            BuildTitleUnlitCity(sky);
            BuildTitleStars(sky);
            BuildTitleBeams(sky);
            BuildTitleAircraft(sky);
            BuildTitleCity(sky);
            titleLayers = new[]
            {
                Mist("OutskirtsMistFar", new Color(.34f, .66f, .92f, .22f)), Layer("OutskirtsHills").rectTransform,
                Mist("OutskirtsMistNear", new Color(.22f, .46f, .68f, .11f)), Layer("OutskirtsSlope").rectTransform,
                Layer("OutskirtsRoof").rectTransform,
            };
            titleTint = new RawImage[6];
            titleTint[0] = skyImage;
            for (int i = 0; i < 5; i++) titleTint[i + 1] = titleLayers[i].GetComponent<RawImage>();
            titleTintBase = System.Array.ConvertAll(titleTint, image => image.color);
            titleLook = TitleOpening ? Vector2.zero : TitlePointer();
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
            UpdateTitleAudio();
            if (titleBackdrop == null) return;
            var extent = ((RectTransform)titleBackdrop.parent).rect.size;
            // Bottom alignment keeps the roof; wide screens crop empty sky first.
            titleBackdrop.localScale = Vector3.one * Mathf.Max(extent.x / 1600f, extent.y / 900f);
            // Nearer layers slide further, away from the pointer; the sky and the city stay.
            titleLook = Vector2.Lerp(titleLook, TitleOpening ? Vector2.zero : TitlePointer(), 1 - Mathf.Exp(-4 * Time.unscaledDeltaTime));
            for (int i = 0; i < titleLayers.Length; i++)
                titleLayers[i].anchoredPosition = -Vector2.Scale(titleLook, TitleTravel[i]);
            UpdateTitleStars();
            UpdateTitleBeams();
            UpdateTitleAircraft();
            UpdateTitleCity();
            // A slow breath, about four seconds: light, so it swells smoothly while the body stays still.
            // The opening lights the core in steps and hands over to the breath at its brightest.
            float lit = TitleIgnition();
            float breath = lit >= 0 ? lit : .5f + .5f * Mathf.Cos((float)((Time.unscaledTimeAsDouble - titleBreathFrom) * Mathf.PI * 2 / 4.2));
            // At its lowest the core itself goes grey, as if nearly out; the swing has to read from across the roof.
            titleCore.color = lit == 0 ? Color.black : Color.Lerp(TitleCoreSpent, RevolverPresentationGeometry.CoreColor, breath);
            titleCoreHalo.color = NeuralPalette.Alpha(TitleCoreColour, lit == 0 ? 0 : Mathf.Lerp(.12f, 1, breath));
            titleCoreHalo.rectTransform.localScale = new Vector3(.80f, 1, 1) * Mathf.Lerp(.92f, 1.22f, breath);
            titleCoreLight.color = NeuralPalette.Alpha(TitleCoreColour, Mathf.Lerp(0, .13f, breath));
            ApplyTitleOpening();
        }

        // ---- sound: wind on the roof and the city's hum while the title is up, and the opening's cues ----

        AudioSource titleWind, titleHum;
        float titleWindLevel, titleHumLevel;

        void UpdateTitleAudio()
        {
            bool on = game.Phase == DuelPhase.Menu;
            if (titleWind == null)
            {
                if (!on) return;
                AudioSource Loop(string name)
                {
                    var source = gameObject.AddComponent<AudioSource>();
                    source.clip = Resources.Load<AudioClip>("TitleSfx/" + name);
                    source.loop = true; source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0;
                    source.ignoreListenerPause = true;
                    return source;
                }
                titleWind = Loop("TitleWind"); titleHum = Loop("TitleHum");
            }
            // In the opening the wind is there first and the hum comes up as the city lights.
            float wind = on ? 1 : 0, hum = on ? 1 : 0;
            if (on && TitleOpening && !titleOpenPlain)
            {
                float t = TitleOpenAt;
                wind = Mathf.Clamp01(t / 1.6f); hum = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.6f, 3.8f, t));
                titleWindLevel = Mathf.Min(titleWindLevel, wind); titleHumLevel = Mathf.Min(titleHumLevel, hum);
            }
            // A launch's long first frame must not carry the loops straight to full.
            float step = Mathf.Min(Time.unscaledDeltaTime, .05f) / .7f;
            titleWindLevel = Mathf.MoveTowards(titleWindLevel, wind, step);
            titleHumLevel = Mathf.MoveTowards(titleHumLevel, hum, step);
            Drive(titleWind, titleWindLevel * .30f); Drive(titleHum, titleHumLevel * .22f);

            void Drive(AudioSource source, float level)
            {
                source.volume = level * GameSettings.EffectsScale;
                if (level > 0 && !source.isPlaying) source.Play();
                else if (level <= 0 && source.isPlaying) source.Stop();
            }
        }

        void PlayTitleCue(string name, float level)
        {
            LoadUiAudio();
            var clip = Resources.Load<AudioClip>("TitleSfx/" + name);
            if (clip != null) uiAudio.PlayOneShot(clip, level * GameSettings.EffectsScale);
        }

        // ---- the opening: one light, a figure, the night, the city, the name ----

        // Once per launch. Statics outlive Play Mode here, so the flag is cleared when play starts.
        static bool titleOpened;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTitleOpening() => titleOpened = false;

        // Seconds from the start: the logo lands, the opening is over, and the plain fade under Reduce Flashing.
        const float TitleLogoBeat = 4.2f, TitleOpeningEnd = 5f, TitlePlainBeat = 1f;
        // Plate columns bounding the building groups, and when each lights: the ends first, the tower last.
        static readonly float[] TitleCityGroups = { 716, 765, 851, 890, 949, 992 };
        static readonly float[] TitleCityLit = { 2.6f, 2.9f, 3.6f, 3.05f, 2.75f };
        // Each group's note, in the order they light: a step higher each time, the tower highest.
        static readonly int[] TitleCityNote = { 1, 3, 5, 4, 2 };
        float titleOpenHeard;
        double titleOpenFrom = -1, titleBreathFrom, titleBeaconFrom;
        bool titleOpenPlain;
        // A launch's first frames are long. The opening waits in the dark until they run evenly.
        int titleOpenSteady;
        float titleOpenWaited;
        GameObject titleBlocker;
        RawImage[] titleTint, titleUnlit;
        Color[] titleTintBase, titleBodyBase;
        Image[] titleBody, titleStars, titleLamps;
        float[] titleStarAt;
        readonly System.Collections.Generic.List<Image> titleBodyParts = new System.Collections.Generic.List<Image>();

        bool TitleOpening => titleOpenFrom >= 0;
        float TitleOpenAt => (float)(Time.unscaledTimeAsDouble - titleOpenFrom);
        float TitleOpenBeat => titleOpenPlain ? TitlePlainBeat : TitleLogoBeat;

        // Called before the title is built. Returns how long the logo and menu wait, or zero for no opening.
        float BeginTitleOpening()
        {
            double now = Time.unscaledTimeAsDouble;
            if (TitleOpening)
            {
                // Rebuilt part-way: carry on while the page's entrance still counts, otherwise finish at once.
                if (pageEntering && TitleOpenAt < TitleOpenBeat) return TitleOpenBeat;
                double done = now - TitleOpeningEnd - titleOpenFrom;
                titleOpenFrom = -1; titleBreathFrom += done; titleBeaconFrom += done;
                return 0;
            }
            if (titleOpened || overlay != OverlayPage.None || !pageEntering || game.Phase != DuelPhase.Menu) return 0;
            titleOpened = true;
            titleOpenPlain = GameSettings.ReduceFlashing;
            titleOpenFrom = System.Math.Max(now, OvMotion.EntranceStart);
            titleBreathFrom = titleOpenFrom + TitleOpeningEnd; titleBeaconFrom = titleOpenFrom + 3.75;
            titleOpenSteady = 0; titleOpenWaited = 0; titleOpenHeard = 0;
            return TitleOpenBeat;
        }

        // Nothing under the dark can be pressed; any key or click jumps to the logo instead.
        void BuildTitleBlocker()
        {
            var cover = Rect("Opening hold", page, Vector2.zero, Vector2.zero);
            cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one;
            cover.gameObject.AddComponent<Image>().color = Color.clear;
            titleBlocker = cover.gameObject;
        }

        void BuildTitleUnlitCity(RectTransform sky)
        {
            titleUnlit = null;
            if (!TitleOpening || titleOpenPlain) return;
            // The plate paints the city already lit. These cover it with its dark skyline, one building group each.
            var dark = Resources.Load<Texture2D>("Title/OutskirtsCityDark");
            titleUnlit = new RawImage[TitleCityLit.Length];
            for (int i = 0; i < titleUnlit.Length; i++)
            {
                float from = TitleCityGroups[i], to = TitleCityGroups[i + 1];
                var strip = Rect("Unlit city", sky, TitlePlatePoint((from + to) * .5f, 758),
                    new Vector2((to - from) * 1600 / 1672, 116 * 900f / 941)).gameObject.AddComponent<RawImage>();
                strip.texture = dark; strip.uvRect = new Rect((from - 716) / 276, 0, (to - from) / 276, 1); strip.raycastTarget = false;
                titleUnlit[i] = strip;
            }
        }

        // The core's level while the opening holds it, 0 to 1, or -1 when it breathes by itself.
        float TitleIgnition()
        {
            if (!TitleOpening || titleOpenPlain) return -1;
            float t = TitleOpenAt;
            // One even rise. Three steps were tried first and read as dropped frames, not as a light coming on.
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, 1, t));
        }

        static bool TitleSkipPressed()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame || keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }

        // Moves the opening and everything waiting on it forward by this many seconds, or back when negative.
        void JumpTitleOpening(float jump)
        {
            titleOpenFrom -= jump; titleBreathFrom -= jump; titleBeaconFrom -= jump; titleFlightNext -= jump;
            foreach (var arrive in page.GetComponentsInChildren<Arrive>()) arrive.Delay -= jump;
            foreach (var slam in page.GetComponentsInChildren<SlamIn>()) slam.Delay -= jump;
            foreach (var slab in page.GetComponentsInChildren<SlabButton>())
                if (slab.EnterDelay >= 0) slab.EnterDelay = Mathf.Max(0, slab.EnterDelay - jump);
        }

        // Runs after the title's own updates and holds back whatever the opening has not reached yet.
        void ApplyTitleOpening()
        {
            if (!TitleOpening) return;
            if (titleOpenSteady < 3)
            {
                float frame = Time.unscaledDeltaTime;
                titleOpenSteady = frame < .05f ? titleOpenSteady + 1 : 0;
                titleOpenWaited += frame;
                if (titleOpenWaited > 4) titleOpenSteady = 3;
                if (TitleOpenAt > 0) JumpTitleOpening(-TitleOpenAt);
            }
            float t = TitleOpenAt;
            if (t > 0 && t < TitleOpenBeat && TitleSkipPressed())
            {
                JumpTitleOpening(TitleOpenBeat - t);
                t = TitleOpenBeat;
                // The cues that were jumped over stay silent; only the logo is heard.
                titleOpenHeard = TitleOpenBeat - .001f;
            }
            if (!titleOpenPlain)
            {
                bool Reached(float at) => titleOpenHeard < at && t >= at;
                for (int i = 0; i < TitleCityLit.Length; i++)
                    if (Reached(TitleCityLit[i])) PlayTitleCue("TitleLight" + TitleCityNote[i], .34f);
                if (Reached(TitleLogoBeat)) PlayTitleCue("TitleLogo", .6f);
            }
            titleOpenHeard = t;
            if (titleBlocker != null && t >= TitleOpenBeat) { Destroy(titleBlocker); titleBlocker = null; }
            bool over = t >= TitleOpeningEnd;
            if (over) t = 1000;
            float Rise(float from, float to) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));

            float world, roof, body, city;
            if (titleOpenPlain) { world = roof = body = Rise(0, TitlePlainBeat); city = 1; }
            else
            {
                // The figure and the roof under it come up together, in one rise behind the core's light. Lit apart,
                // the roof looked like it had missed a beat.
                roof = body = Rise(.5f, 1.5f);
                world = Rise(1.6f, 2.6f); city = Rise(2.6f, 3.8f);
            }
            for (int i = 0; i < titleTint.Length; i++)
            {
                Color colour = titleTintBase[i];
                // The mists are light: they fade by strength. The far one is lit by the city.
                if (i == 1) colour.a *= world * city;
                else if (i == 3) colour.a *= world;
                else { float level = i == 5 ? roof : world; colour.r *= level; colour.g *= level; colour.b *= level; }
                titleTint[i].color = colour;
            }
            for (int i = 0; i < titleBody.Length; i++)
            {
                Color colour = titleBodyBase[i];
                colour.r *= body; colour.g *= body; colour.b *= body;
                titleBody[i].color = colour;
            }
            if (!titleOpenPlain)
            {
                // Stars come out in three bands from the top; each building group lights outright.
                for (int i = 0; i < titleStars.Length; i++) titleStars[i].enabled = t >= titleStarAt[i];
                for (int i = 0; i < titleUnlit.Length; i++)
                {
                    if (titleUnlit[i] == null) continue;
                    titleUnlit[i].enabled = t < TitleCityLit[i];
                    titleUnlit[i].color = new Color(world, world, world);
                }
                for (int i = 0; i < titleWindows.Length; i++)
                {
                    Color colour = titleWindows[i].color;
                    colour.a = t >= 3.05f + i * 37 % 26 / 26f * .9f ? .85f : 0;
                    titleWindows[i].color = colour;
                }
                Color glow = titleGlow.color; glow.a *= city; titleGlow.color = glow;
                if (t < 3.75f) titleBeacon.color = titleBeaconHalo.color = Color.clear;
                // The lamps come on and the shafts climb out of them.
                foreach (var lamp in titleLamps) lamp.enabled = t >= 3.7f;
                for (int i = 0; i < titleBeams.Length; i++)
                    titleBeams[i].sizeDelta = new Vector2(20, TitleBeamSetup[i * 6 + 5] * Rise(3.7f, 4.4f));
                // A slow pull back from the gun's light to the whole view; nearer layers settle from lower down.
                float back = 1 - OvMotion.Glide(t / 4.7f);
                titleBackdrop.localScale *= 1 + .08f * back;
                for (int i = 0; i < titleLayers.Length; i++)
                    titleLayers[i].anchoredPosition += Vector2.down * (TitleTravel[i].x * 2.2f * back);
            }
            if (over)
            {
                titleOpenFrom = -1;
                if (titleUnlit != null) foreach (var strip in titleUnlit) if (strip != null) Destroy(strip.gameObject);
            }
        }

        static readonly Color TitleCoreColour = new Color(.35f, .78f, 1f), TitleCoreSpent = new Color(.47f, .54f, .60f);
        Image titleCore, titleCoreHalo, titleCoreLight;

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
            var lamps = new Image[titleBeams.Length * 2];
            for (int i = 0; i < titleBeams.Length; i++)
            {
                Vector2 lamp = TitlePlatePoint(TitleBeamSetup[i * 6], TitleBeamSetup[i * 6 + 1]);
                var beam = Shape("Searchlight", titleBeamSprite, lamp, new Vector2(20, TitleBeamSetup[i * 6 + 5]), 1);
                beam.material = TitleLight(false);
                beam.rectTransform.pivot = new Vector2(.5f, 0);
                titleBeams[i] = beam.rectTransform;
                // The lamp itself: a small glow and a hard point on the roof.
                lamps[i * 2] = Shape("Lamp glow", StickmanSprites.Pool, lamp, new Vector2(11, 6), .32f);
                lamps[i * 2 + 1] = Shape("Lamp", StickmanSprites.Block, lamp, Vector2.one * 1.5f, .95f);
            }
            titleLamps = lamps;
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

        // Routes on the plate: from, to, seconds in flight and strength. In to the city from the right, out of it
        // climbing to the right, and a higher, fainter crossing that ends behind the logo.
        static readonly float[] TitleRoutes =
        {
            1700, 610, 905, 748, 40, 1,
            818, 752, 1700, 470, 46, 1,
            1700, 372, 1080, 438, 58, .6f,
        };
        Image titleCraft, titleStrobe, titleStrobeGlow;
        int titleRoute;
        float titleFlightFrom, titleFlightNext;

        void BuildTitleAircraft(RectTransform sky)
        {
            Image Light(string name, Sprite sprite, Vector2 size)
            {
                var image = Rect(name, sky, Vector2.zero, size).gameObject.AddComponent<Image>();
                image.sprite = sprite; image.color = Color.clear; image.raycastTarget = false;
                return image;
            }
            titleStrobeGlow = Light("Aircraft glow", StickmanSprites.Pool, new Vector2(12, 12));
            titleCraft = Light("Aircraft", StickmanSprites.Block, Vector2.one * 1.5f);
            titleStrobe = Light("Aircraft strobe", StickmanSprites.Block, Vector2.one * 2.3f);
            titleRoute = -1;
            // The first one comes soon, so the sky is seen to be travelled; after that they are rare.
            titleFlightNext = Time.unscaledTime + Random.Range(7f, 14f) + (TitleOpening ? Mathf.Max(0, TitleOpeningEnd - TitleOpenAt) : 0);
        }

        void UpdateTitleAircraft()
        {
            float now = Time.unscaledTime;
            if (titleRoute < 0)
            {
                if (now < titleFlightNext) return;
                titleRoute = Random.Range(0, TitleRoutes.Length / 6);
                titleFlightFrom = now;
            }
            int r = titleRoute * 6;
            float along = (now - titleFlightFrom) / TitleRoutes[r + 4];
            if (along >= 1)
            {
                titleRoute = -1;
                titleFlightNext = now + Random.Range(40f, 90f);
                titleCraft.color = titleStrobe.color = titleStrobeGlow.color = Color.clear;
                return;
            }
            Vector2 point = Vector2.Lerp(TitlePlatePoint(TitleRoutes[r], TitleRoutes[r + 1]),
                TitlePlatePoint(TitleRoutes[r + 2], TitleRoutes[r + 3]), along);
            titleCraft.rectTransform.anchoredPosition = titleStrobe.rectTransform.anchoredPosition
                = titleStrobeGlow.rectTransform.anchoredPosition = point;
            // It comes out of and goes into the distance rather than appearing.
            float strength = TitleRoutes[r + 5] * Mathf.Clamp01(Mathf.Min(along, 1 - along) * 12);
            // A steady running light and a double strobe every 1.4 s. Reduce Flashing keeps only the steady light.
            float beat = (now - titleFlightFrom) % 1.4f;
            bool flash = !GameSettings.ReduceFlashing && (beat < .07f || beat > .19f && beat < .26f);
            titleCraft.color = new Color(.80f, .90f, 1, .5f * strength);
            titleStrobe.color = new Color(.94f, .98f, 1, flash ? strength : 0);
            titleStrobeGlow.color = new Color(.62f, .86f, 1, flash ? .3f * strength : 0);
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
            var all = new System.Collections.Generic.List<Image>();
            var when = new System.Collections.Generic.List<float>();
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
                all.Add(star); when.Add(1.75f + Mathf.Floor(y / 640 * 3) * .28f);
                if (large && twinklers.Count < 14) { twinklers.Add(star); levels.Add(level); }
            }
            titleTwinklers = twinklers.ToArray(); titleTwinkleLevel = levels.ToArray();
            titleStars = all.ToArray(); titleStarAt = when.ToArray();
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
            float beat = (float)((Time.unscaledTimeAsDouble - titleBeaconFrom) % 3);
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
            titleBodyParts.Clear();
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
                titleBodyParts.Add(image);
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
            Image GunPart(string name, Sprite sprite, Vector2 offset, Color colour)
            {
                var image = Part(name, sprite, muzzle + GunPoint(offset), sprite.bounds.size, colour, gunAngle);
                image.rectTransform.pivot = sprite.pivot / sprite.rect.size;
                image.rectTransform.localScale = new Vector3(foreshorten, 1, 1);
                return image;
            }
            // The core is the one thing on the player that moves: its light swells and settles on the air around it.
            titleCoreLight = Part("Core light", StickmanSprites.Pool, muzzle + GunPoint(RevolverPresentationGeometry.CorePosition),
                new Vector2(.62f, .46f), Color.clear);
            GunPart("Revolver", RevolverPresentationGeometry.Body, Vector2.zero, RevolverPresentationGeometry.BodyColor);
            titleCoreHalo = GunPart("Core halo", RevolverPresentationGeometry.Halo, RevolverPresentationGeometry.CorePosition, TitleCoreColour);
            titleCore = GunPart("Core", RevolverPresentationGeometry.Core, RevolverPresentationGeometry.CorePosition, RevolverPresentationGeometry.CoreColor);

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
            // The core, its halo and its light follow the breath; everything else can be held in the dark.
            titleBodyParts.RemoveAll(part => part == titleCore || part == titleCoreHalo || part == titleCoreLight);
            titleBody = titleBodyParts.ToArray();
            titleBodyBase = System.Array.ConvertAll(titleBody, part => part.color);
        }
    }
}
