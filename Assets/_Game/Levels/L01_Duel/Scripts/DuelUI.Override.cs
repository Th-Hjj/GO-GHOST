using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI, stage 1: shared builders plus the title, pause and result pages.
    // Layout numbers are the 1600x900 mockup coordinates recorded in Docs/Game/UIDesign.md.
    public sealed partial class DuelUI
    {
        TMP_FontAsset displayBlack, displayHuge, displayBold, monoMedium, monoBold;
        static Texture2D scanTexture;

        void LoadOverrideFonts()
        {
            displayBlack = Resources.Load<TMP_FontAsset>("Fonts/BarlowCondensed-BlackItalic SDF");
            // Capitals and digits sampled larger, for the logo, result words and the countdown.
            displayHuge = Resources.Load<TMP_FontAsset>("Fonts/BarlowCondensed-BlackItalic Display SDF");
            displayBold = Resources.Load<TMP_FontAsset>("Fonts/BarlowCondensed-ExtraBoldItalic SDF");
            monoMedium = Resources.Load<TMP_FontAsset>("Fonts/JetBrainsMono-Medium SDF");
            monoBold = Resources.Load<TMP_FontAsset>("Fonts/JetBrainsMono-Bold SDF");
            if (displayBlack == null || displayBold == null || monoMedium == null || monoBold == null)
                Debug.LogError("Override fonts are missing from Shared/Fonts/Resources/Fonts.");
        }

        // Mockup rectangles are given from the top-left corner; uGUI children here are centre-anchored.
        static Vector2 At(float left, float top, float width, float height) => new Vector2(left + width / 2 - 800, 450 - top - height / 2);

        TMP_Text OvText(string value, Transform parent, Vector2 pos, Vector2 size, float points, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Capline, bool black = true, float spacing = 0)
        {
            var r = Rect(value, parent, pos, size);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            var face = !black ? displayBold : points >= 150 && displayHuge != null ? displayHuge : displayBlack;
            t.font = face != null ? face : headingFont;
            t.text = value; t.fontSize = points; t.enableAutoSizing = false; t.color = color; t.alignment = align;
            t.fontStyle = FontStyles.UpperCase; t.characterSpacing = spacing;
            t.raycastTarget = false; t.richText = false;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        Button OvButton(string title, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click,
            OverrideButtonKind kind, float points, bool chevron = false, float enterDelay = -1,
            TextAlignmentOptions align = TextAlignmentOptions.CaplineLeft)
        {
            var root = Rect(title, parent, pos, size);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = hit; button.onClick.AddListener(click);

            var face = Rect("Face", root, Vector2.zero, size);
            var slab = face.gameObject.AddComponent<SlabGraphic>();
            slab.raycastTarget = false;
            Color fill = kind == OverrideButtonKind.Primary ? OverridePalette.Signal
                : kind == OverrideButtonKind.Secondary ? OverridePalette.Text : OverridePalette.SurfaceHover;
            Color ink = kind == OverrideButtonKind.Tertiary ? OverridePalette.Text : OverridePalette.Ink;
            slab.color = fill;

            const float pad = 44;
            var label = OvText(title, face, Vector2.zero, new Vector2(size.x - pad * 2, size.y), points, ink, align,
                kind == OverrideButtonKind.Primary, 1);
            TMP_Text icon = null;
            if (chevron)
                icon = OvText("»", face, new Vector2(size.x / 2 - pad - 22, 0), new Vector2(60, size.y), points, ink, TextAlignmentOptions.Capline);

            var behaviour = root.gameObject.AddComponent<SlabButton>();
            behaviour.Owner = this; behaviour.Button = button; behaviour.Face = face; behaviour.Slab = slab; behaviour.Label = label; behaviour.Icon = icon;
            behaviour.Fill = fill; behaviour.Ink = ink;
            behaviour.Shift = kind == OverrideButtonKind.Primary ? 14 : 10;
            behaviour.EnterDelay = enterDelay;
            return button;
        }

        // Tilted full-width hazard band.
        void OvBand(Transform parent, float top, float height = 26)
        {
            var r = Rect("Hazard band", parent, new Vector2(0, 450 - top - height / 2), new Vector2(2600, height));
            r.localRotation = Quaternion.Euler(0, 0, 1.2f);
            var stripes = r.gameObject.AddComponent<StripeGraphic>();
            stripes.color = OverridePalette.Signal; stripes.Other = OverridePalette.Ink; stripes.raycastTarget = false;
        }

        // One dark pixel row in every four, drawn at screen resolution.
        void OvScanlines(Transform parent, float alpha)
        {
            if (GameSettings.ReduceFlashing) return;
            if (scanTexture == null)
            {
                scanTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.HideAndDontSave };
                scanTexture.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0) });
                scanTexture.Apply();
            }
            const float extent = 4000;
            var r = Rect("Scanlines", parent, Vector2.zero, new Vector2(extent, extent));
            var image = r.gameObject.AddComponent<RawImage>();
            image.texture = scanTexture; image.color = new Color(0, 0, 0, alpha); image.raycastTarget = false;
            image.uvRect = new Rect(0, 0, 1, extent * canvas.GetComponent<Canvas>().scaleFactor / 4f);
        }

        void OvFill(Transform parent, Color color)
        {
            Image(parent, Vector2.zero, new Vector2(4000, 4000), color);
        }

        // The Mission 3 skyline along the bottom, fading into the ground colour above it.
        void OvSkyline(RectTransform parent, float alpha)
        {
            foreach (var path in new[] { "Arena/MegacityFar", "Arena/MegacityNear" })
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture == null) continue;
                var r = Rect(path, parent, Vector2.zero, Vector2.zero);
                r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0);
                r.anchoredPosition = Vector2.zero; r.sizeDelta = Vector2.zero;
                var image = r.gameObject.AddComponent<RawImage>();
                image.texture = texture; image.color = new Color(1, 1, 1, alpha); image.raycastTarget = false;
                var fit = r.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
                fit.aspectRatio = (float)texture.width / texture.height;
            }
            var solid = Rect("Sky cover", parent, Vector2.zero, Vector2.zero);
            solid.anchorMin = new Vector2(0, .66f); solid.anchorMax = Vector2.one; solid.sizeDelta = Vector2.zero;
            var cover = solid.gameObject.AddComponent<Image>(); cover.color = OverridePalette.Ink; cover.raycastTarget = false;
            var fade = Rect("Sky fade", parent, Vector2.zero, Vector2.zero);
            fade.anchorMin = new Vector2(0, .26f); fade.anchorMax = new Vector2(1, .66f); fade.sizeDelta = Vector2.zero;
            var gradient = fade.gameObject.AddComponent<FadeGraphic>();
            gradient.Top = OverridePalette.Ink; gradient.Bottom = NeuralPalette.Alpha(OverridePalette.Ink, 0); gradient.raycastTarget = false;
        }

        // A display word with its two colour copies and displaced slice. The rectangle is centred on the capitals.
        RectTransform OvGlitchWord(string word, Transform parent, Vector2 pos, Vector2 size, float points,
            TextAlignmentOptions align, float period, float spread)
        {
            float spacing = -.6f;
            var root = Rect(word + " word", parent, pos, size);
            var first = OvText(word, root, Vector2.zero, size, points, OverridePalette.Signal, align, true, spacing);
            var second = OvText(word, root, Vector2.zero, size, points, NeuralPalette.Arms, align, true, spacing);
            OvText(word, root, Vector2.zero, size, points, OverridePalette.Text, align, true, spacing);
            float bandCenter = points * .025f, bandHeight = points * .12f;
            var slice = Rect("Slice", root, new Vector2(0, bandCenter), new Vector2(size.x + 600, bandHeight));
            slice.gameObject.AddComponent<RectMask2D>();
            OvText(word, slice, new Vector2(0, -bandCenter), size, points, OverridePalette.Text, align, true, spacing);
            var glitch = root.gameObject.AddComponent<GlitchWord>();
            glitch.First = first.rectTransform; glitch.Second = second.rectTransform; glitch.Slice = slice;
            glitch.Period = period; glitch.Spread = spread;
            return root;
        }

        void StretchPage()
        {
            page.anchorMin = Vector2.zero; page.anchorMax = Vector2.one; page.sizeDelta = Vector2.zero;
        }

        void BuildOverrideMenu()
        {
            StretchPage();
            OvFill(page, OverridePalette.Ink);
            OvSkyline(page, .7f);
            OvScanlines(page, .28f);
            var frame = Rect("Frame", page, Vector2.zero, new Vector2(1600, 900));
            OvGlitchWord("GO GHOST", frame, new Vector2(84 + 700 - 800, 450 - 353), new Vector2(1400, 292), 340,
                TextAlignmentOptions.CaplineLeft, 3.4f, 7);
            OvBand(frame, 514);
            // With a save, Continue leads and New Game asks before replacing it.
            bool saved = GameSave.Exists;
            if (saved)
            {
                OvButton("Continue", frame, At(92, 580, 494, 96), new Vector2(494, 96), game.ContinueGame, OverrideButtonKind.Primary, 62, true);
                OvButton("New Game", frame, At(92, 690, 494, 68), new Vector2(494, 68), RequestNewGame, OverrideButtonKind.Secondary, 40);
            }
            else OvButton("New Game", frame, At(92, 600, 494, 96), new Vector2(494, 96), RequestNewGame, OverrideButtonKind.Primary, 62, true);
            // The small row spans exactly the width of the buttons above it. A browser page has nothing to quit.
            float row = saved ? 774 : 714;
            bool quit = Application.platform != RuntimePlatform.WebGLPlayer;
            const float span = 494, gap = 10;
            string[] titles = quit ? new[] { "Settings", "Credits", "Quit" } : new[] { "Settings", "Credits" };
            float[] shares = quit ? new[] { 178f, 166f, 130f } : new[] { 252f, 232f };
            UnityEngine.Events.UnityAction[] clicks = { () => OpenOverlay(OverlayPage.Settings), () => OpenOverlay(OverlayPage.Credits), Application.Quit };
            float x = 92;
            for (int i = 0; i < titles.Length; i++)
            {
                float w = i == titles.Length - 1 ? 92 + span - x : shares[i];
                OvButton(titles[i], frame, At(x, row, w, 56), new Vector2(w, 56), clicks[i], OverrideButtonKind.Tertiary, 32, align: TextAlignmentOptions.Capline);
                x += w + gap;
            }
        }

        void BuildOverridePause()
        {
            OvFill(page, NeuralPalette.Alpha(OverridePalette.Ink, .74f));
            OvScanlines(page, .3f);
            OvBand(page, 232);
            OvButton("Resume", page, At(570, 296, 460, 100), new Vector2(460, 100), () => game.SetPause(false), OverrideButtonKind.Primary, 64, true, 0);
            // Configuration is only an exit from a fight or the Test Room.
            bool encounter = game.Phase == DuelPhase.Fight || game.Phase == DuelPhase.Prepare || game.Phase == DuelPhase.Training;
            int y = 410;
            if (encounter)
            {
                OvButton("Configuration", page, At(570, y, 460, 72), new Vector2(460, 72), game.Reconfigure, OverrideButtonKind.Secondary, 42, false, .04f);
                y += 86;
            }
            OvButton("Settings", page, At(570, y, 460, 64), new Vector2(460, 64), () => OpenOverlay(OverlayPage.Settings), OverrideButtonKind.Tertiary, 38, false, .08f);
            OvButton("Main Menu", page, At(570, y + 78, 460, 64), new Vector2(460, 64), game.MainMenu, OverrideButtonKind.Tertiary, 38, false, .12f);
            if (game.Phase == DuelPhase.Training) BuildTrainingButtons();
        }

        // Defeat, and the Bonus result.
        void BuildOverrideOutcome()
        {
            bool failed = game.Phase == DuelPhase.Failed;
            OvFill(page, NeuralPalette.Alpha(OverridePalette.Ink, .72f));
            OvScanlines(page, .3f);
            var word = OvGlitchWord(failed ? "Defeat" : "Complete", page, new Vector2(0, 450 - 351), new Vector2(1600, 292), 340,
                TextAlignmentOptions.Capline, 2.8f, 8);
            word.gameObject.AddComponent<SlamIn>();
            OvBand(page, 502);
            var first = new Vector2(480, 96); var second = new Vector2(480, 68);
            Vector2 firstAt = At(560, 574, 480, 96), secondAt = At(560, 684, 480, 68);
            if (game.CanRetryFinalBossPhaseTwo)
            {
                OvButton("Retry Phase 2", page, firstAt, first, game.RetryFinalBossPhaseTwo, OverrideButtonKind.Primary, 56, true).name = "RetryPhaseTwo";
                OvButton("Configuration", page, secondAt, second, game.Reconfigure, OverrideButtonKind.Secondary, 40).name = "Reconfigure";
            }
            else if (failed)
            {
                OvButton("Retry", page, firstAt, first, game.RetryFight, OverrideButtonKind.Primary, 62, true);
                OvButton("Configuration", page, secondAt, second, game.Reconfigure, OverrideButtonKind.Secondary, 40).name = "Reconfigure";
            }
            else
            {
                OvButton("Restart", page, firstAt, first, game.NewGame, OverrideButtonKind.Primary, 62, true);
                OvButton("Main Menu", page, secondAt, second, game.MainMenu, OverrideButtonKind.Secondary, 40);
            }
        }

        void BuildOverrideCleared()
        {
            OvFill(page, OverridePalette.Ink);
            OvScanlines(page, .3f);
            var mission = Rect("Mission slab", page, new Vector2(0, 450 - 108 - 68), new Vector2(600, 136));
            var slab = mission.gameObject.AddComponent<SlabGraphic>();
            slab.color = game.Wave == 1 ? NeuralPalette.Gunner : NeuralPalette.Swordsman; slab.raycastTarget = false;
            var name = OvText(game.WaveLabel, mission, Vector2.zero, new Vector2(1200, 136), 120, OverridePalette.Ink, TextAlignmentOptions.Capline, true, 1);
            mission.sizeDelta = new Vector2(name.GetPreferredValues().x + 128, 136);
            var word = OvGlitchWord("Cleared", page, new Vector2(0, 450 - 421), new Vector2(1600, 292), 340, TextAlignmentOptions.Capline, 2.8f, 8);
            word.gameObject.AddComponent<SlamIn>().Delay = .12f;
            OvBand(page, 574);
            if (game.Wave == 1)
                OvButton("Continue to Mission 2", page, At(460, 650, 680, 100), new Vector2(680, 100), game.ContinueToWaveTwo,
                    OverrideButtonKind.Primary, 60, true).name = "ContinueToWaveTwo";
        }

        void BuildOverrideFinished()
        {
            StretchPage();
            OvFill(page, OverridePalette.Ink);
            OvSkyline(page, .5f);
            OvScanlines(page, .28f);
            var frame = Rect("Frame", page, Vector2.zero, new Vector2(1600, 900));
            string[] lines = { "You've", "Finished", "The Game!" };
            float[] centres = { 262, 426.6f, 591.2f };
            for (int i = 0; i < lines.Length; i++)
            {
                var line = OvText(lines[i], frame, Vector2.zero, new Vector2(1000, 165), 196,
                    i == 2 ? OverridePalette.Signal : OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, -1);
                line.rectTransform.pivot = new Vector2(0, .5f);
                line.rectTransform.anchoredPosition = new Vector2(76 - 800, 450 - centres[i]);
                var slam = line.gameObject.AddComponent<SlamIn>();
                slam.From = 1.35f; slam.Seconds = .1f; slam.Delay = .3f + i * .35f;
            }
            var divider = Rect("Divider", frame, new Vector2(913 - 800, 0), new Vector2(1200, 26));
            divider.localRotation = Quaternion.Euler(0, 0, 82);
            var stripes = divider.gameObject.AddComponent<StripeGraphic>();
            stripes.color = OverridePalette.Signal; stripes.Other = OverridePalette.Ink; stripes.raycastTarget = false;
            OvButton("Main Menu", frame, At(990, 690, 440, 96), new Vector2(440, 96), game.MainMenu, OverrideButtonKind.Primary, 62, true);
        }
    }
}
