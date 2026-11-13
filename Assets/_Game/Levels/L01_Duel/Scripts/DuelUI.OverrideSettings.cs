using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI, stage 7: Settings, Key Bindings, Credits, the New Game confirmation and the flashing-lights notice.
    public sealed partial class DuelUI
    {
        enum OverlayPage { None, Settings, Bindings, Credits, Confirm, Notice }
        OverlayPage overlay;
        int listeningBinding = -1, listeningSince;
        internal bool OverlayOpen => overlay != OverlayPage.None;

        void OpenOverlay(OverlayPage value) { overlay = value; listeningBinding = -1; Show(); }
        void CloseOverlay()
        {
            listeningBinding = -1;
            overlay = overlay == OverlayPage.Bindings ? OverlayPage.Settings : OverlayPage.None;
            Show();
        }

        RectTransform OvOverlayFrame(string title, bool back = true)
        {
            page.sizeDelta = canvas.rect.size;
            OvFill(page, game.Paused ? NeuralPalette.Alpha(OverridePalette.Ink, .94f) : OverridePalette.Ink);
            var frame = Rect("Frame", page, Vector2.zero, new Vector2(1600, 900));
            if (back)
                OvButton("« Back", frame, At(56, 44, 170, 52), new Vector2(170, 52), CloseOverlay, OverrideButtonKind.Tertiary, 30,
                    align: TextAlignmentOptions.Capline).name = "OverlayBack";
            if (title != null)
            {
                OvText(title, frame, new Vector2(56 + 600 - 800, 450 - 122 - 56), new Vector2(1200, 112), 112, OverridePalette.Text,
                    TextAlignmentOptions.CaplineLeft, true, -.5f);
                OvRule(frame, 56, 244, 1488);
            }
            return frame;
        }

        void BuildOverrideOverlay()
        {
            switch (overlay)
            {
                case OverlayPage.Settings: BuildOverrideSettings(); break;
                case OverlayPage.Bindings: BuildOverrideBindings(); break;
                case OverlayPage.Credits: BuildOverrideCredits(); break;
                case OverlayPage.Confirm: BuildOverrideConfirm(); break;
                case OverlayPage.Notice: BuildOverrideNotice(); break;
            }
        }

        Button OvToggle(string name, string label, RectTransform frame, float left, float top, float width, bool selected, UnityEngine.Events.UnityAction click)
        {
            return OvOption(name, label, frame, At(left, top, width, 52), new Vector2(width, 52), click,
                selected ? OverridePalette.Text : OverridePalette.Surface, selected ? OverridePalette.Ink : OverridePalette.Text, 30, -1, -1);
        }

        void BuildOverrideSettings()
        {
            var frame = OvOverlayFrame("Settings");
            const float rowTop = 286, pitch = 76;
            string[] audio = { "Master", "Music", "Effects", "Voice" };
            for (int i = 0; i < audio.Length; i++)
            {
                int group = i;
                float top = rowTop + i * pitch;
                int value = group == 0 ? GameSettings.Master : group == 1 ? GameSettings.Music : group == 2 ? GameSettings.Effects : GameSettings.Voice;
                OvText(audio[i], frame, At(60, top, 200, 52), new Vector2(200, 52), 36, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, false, 1);
                for (int step = 0; step < GameSettings.Steps; step++)
                {
                    int level = step + 1;
                    bool on = step < value;
                    var slab = OvOption(audio[i] + level, "", frame, At(270 + step * 40, top + 6, 34, 40), new Vector2(34, 40),
                        () => SetVolume(group, level == 1 && value == 1 ? 0 : level),
                        on ? OverridePalette.Text : OverridePalette.Surface, OverridePalette.Ink, 20, -1, -1);
                    slab.GetComponent<SlabButton>().Shift = 0;
                }
                OvText((value * 10).ToString(), frame, At(686, top, 90, 52), new Vector2(90, 52), 36, OverridePalette.TextDim, TextAlignmentOptions.CaplineLeft);
            }

            const float right = 860;
            var keys = OvButton("Key Bindings", frame, At(right, rowTop, 420, 60), new Vector2(420, 60), () => OpenOverlay(OverlayPage.Bindings),
                OverrideButtonKind.Tertiary, 34, true);
            keys.name = "KeyBindings";
            float y = rowTop + pitch;
            OvText("Fullscreen", frame, At(right + 4, y, 300, 52), new Vector2(300, 52), 36, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, false, 1);
            OvToggle("FullscreenOn", "On", frame, right + 330, y, 96, GameSettings.Fullscreen, () => SetDisplay(true, GameSettings.VSync, 0));
            OvToggle("FullscreenOff", "Off", frame, right + 434, y, 96, !GameSettings.Fullscreen, () => SetDisplay(false, GameSettings.VSync, 0));
            y += pitch;
            OvText("Resolution", frame, At(right + 4, y, 300, 52), new Vector2(300, 52), 36, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, false, 1);
            OvOption("ResolutionPrevious", "«", frame, At(right + 330, y, 52, 52), new Vector2(52, 52), () => SetDisplay(GameSettings.Fullscreen, GameSettings.VSync, -1),
                OverridePalette.Surface, OverridePalette.Text, 30, -1, -1);
            int width = GameSettings.Width > 0 ? GameSettings.Width : Screen.width, height = GameSettings.Height > 0 ? GameSettings.Height : Screen.height;
            OvText(width + " × " + height, frame, At(right + 390, y, 210, 52), new Vector2(210, 52), 34, OverridePalette.Text);
            OvOption("ResolutionNext", "»", frame, At(right + 608, y, 52, 52), new Vector2(52, 52), () => SetDisplay(GameSettings.Fullscreen, GameSettings.VSync, 1),
                OverridePalette.Surface, OverridePalette.Text, 30, -1, -1);
            y += pitch;
            OvText("VSync", frame, At(right + 4, y, 300, 52), new Vector2(300, 52), 36, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, false, 1);
            OvToggle("VSyncOn", "On", frame, right + 330, y, 96, GameSettings.VSync, () => SetDisplay(GameSettings.Fullscreen, true, 0));
            OvToggle("VSyncOff", "Off", frame, right + 434, y, 96, !GameSettings.VSync, () => SetDisplay(GameSettings.Fullscreen, false, 0));
            y += pitch;
            OvText("Reduce Flashing", frame, At(right + 4, y, 320, 52), new Vector2(320, 52), 36, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, false, 1);
            OvToggle("ReduceFlashingOn", "On", frame, right + 330, y, 96, GameSettings.ReduceFlashing, () => SetReduceFlashing(true));
            OvToggle("ReduceFlashingOff", "Off", frame, right + 434, y, 96, !GameSettings.ReduceFlashing, () => SetReduceFlashing(false));
        }

        void SetVolume(int group, int value)
        {
            value = Mathf.Clamp(value, 0, GameSettings.Steps);
            if (group == 0) GameSettings.Master = value;
            else if (group == 1) GameSettings.Music = value;
            else if (group == 2) GameSettings.Effects = value;
            else GameSettings.Voice = value;
            GameSettings.Apply(false);
            GameSettings.Save();
            Show();
        }

        void SetDisplay(bool fullscreen, bool vsync, int resolutionStep)
        {
            GameSettings.Fullscreen = fullscreen; GameSettings.VSync = vsync;
            if (resolutionStep != 0)
            {
                // Step through the distinct sizes the display reports.
                var sizes = new System.Collections.Generic.List<Vector2Int>();
                foreach (var mode in Screen.resolutions)
                {
                    var size = new Vector2Int(mode.width, mode.height);
                    if (!sizes.Contains(size)) sizes.Add(size);
                }
                if (sizes.Count > 0)
                {
                    var current = new Vector2Int(GameSettings.Width > 0 ? GameSettings.Width : Screen.width, GameSettings.Height > 0 ? GameSettings.Height : Screen.height);
                    int index = sizes.IndexOf(current);
                    if (index < 0) index = sizes.Count - 1;
                    index = Mathf.Clamp(index + resolutionStep, 0, sizes.Count - 1);
                    GameSettings.Width = sizes[index].x; GameSettings.Height = sizes[index].y;
                }
            }
            GameSettings.Apply(true);
            GameSettings.Save();
            Show();
        }

        void SetReduceFlashing(bool value)
        {
            GameSettings.ReduceFlashing = value;
            GameSettings.Save();
            Show();
        }

        void BuildOverrideBindings()
        {
            var frame = OvOverlayFrame("Key Bindings");
            for (int i = 0; i < GameSettings.BindingNames.Length; i++)
            {
                int index = i;
                float top = 268 + i * 56;
                bool listening = listeningBinding == i;
                OvText(GameSettings.BindingNames[i], frame, At(60, top, 320, 48), new Vector2(320, 48), 34, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, false, 1);
                var key = OvOption("Binding" + i, listening ? "Press a key" : GameSettings.BindingLabel(i), frame, At(400, top, 300, 48), new Vector2(300, 48),
                    () => { listeningBinding = index; listeningSince = Time.frameCount; Show(); },
                    listening ? OverridePalette.Signal : OverridePalette.Text, OverridePalette.Ink, 30, -1, -1);
                key.GetComponent<SlabButton>().Label.alignment = TextAlignmentOptions.Capline;
            }
            OvButton("Reset to Defaults", frame, At(56, 740, 400, 64), new Vector2(400, 64), () => { listeningBinding = -1; GameSettings.ResetBindings(); Show(); },
                OverrideButtonKind.Tertiary, 36).name = "ResetBindings";
        }

        // The next key or mouse button is bound. Esc cancels listening.
        void UpdateOverlayInput()
        {
            if (!OverlayOpen) return;
            var keyboard = Keyboard.current;
            if (listeningBinding < 0)
            {
                if (overlay != OverlayPage.Notice && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseOverlay();
                return;
            }
            if (Time.frameCount <= listeningSince + 1) return;
            string path = null;
            if (keyboard != null)
                foreach (var key in keyboard.allKeys)
                    if (key != null && key.wasPressedThisFrame) { path = "<Keyboard>/" + key.name; break; }
            var mouse = Mouse.current;
            if (path == null && mouse != null)
                path = mouse.leftButton.wasPressedThisFrame ? "<Mouse>/leftButton" : mouse.rightButton.wasPressedThisFrame ? "<Mouse>/rightButton"
                    : mouse.middleButton.wasPressedThisFrame ? "<Mouse>/middleButton" : null;
            if (path == null) return;
            int index = listeningBinding;
            listeningBinding = -1;
            if (path != "<Keyboard>/escape") GameSettings.Bind(index, path);
            Show();
        }

        void BuildOverrideCredits()
        {
            var frame = OvOverlayFrame("Credits");
            (string role, string names)[] rows =
            {
                ("A game by", "Thomas Hong · Walker Grupp"),
                ("Music", "Efe Inan"),
                ("Fonts", "Barlow Condensed · JetBrains Mono"),
            };
            const float rowTop = 300, rowHeight = 76, rowWidth = 1100;
            for (int i = 0; i <= rows.Length; i++)
                Image(frame, new Vector2(56 + rowWidth / 2 - 800, 450 - (rowTop + i * rowHeight)), new Vector2(rowWidth, 2), OverridePalette.Neutral);
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 450 - (rowTop + i * rowHeight + rowHeight / 2);
                OvText(rows[i].role, frame, new Vector2(84 + 130 - 800, y), new Vector2(260, 60), 28, OverridePalette.TextDim, TextAlignmentOptions.CaplineLeft, false, 1);
                OvText(rows[i].names, frame, new Vector2(360 + 400 - 800, y), new Vector2(800, 60), 40, OverridePalette.Text, TextAlignmentOptions.CaplineLeft);
            }
        }

        void BuildOverrideConfirm()
        {
            var frame = OvOverlayFrame(null, false);
            OvText("Start a new game?", frame, new Vector2(180 + 600 - 800, 450 - 330), new Vector2(1200, 150), 150, OverridePalette.Text,
                TextAlignmentOptions.CaplineLeft, true, -.5f);
            OvBodyAt("Your saved run will be replaced.", frame, At(186, 430, 1100, 44), new Vector2(1100, 44), 26, QuietCardText);
            OvButton("Cancel", frame, At(180, 520, 440, 96), new Vector2(440, 96), CloseOverlay, OverrideButtonKind.Primary, 62).name = "ConfirmCancel";
            OvButton("New Game", frame, At(180, 632, 300, 68), new Vector2(300, 68), () => { overlay = OverlayPage.None; game.NewGame(); },
                OverrideButtonKind.Secondary, 40).name = "ConfirmNewGame";
        }

        void BuildOverrideNotice()
        {
            var frame = OvOverlayFrame(null, false);
            OvText("Flashing Lights", frame, new Vector2(180 + 600 - 800, 450 - 280 - 75), new Vector2(1200, 150), 150, OverridePalette.Text,
                TextAlignmentOptions.CaplineLeft, true, -.5f);
            OvBodyAt("This game contains flashing lights and fast screen effects.", frame, At(186, 452, 1100, 44), new Vector2(1100, 44), 28, QuietCardText);
            OvButton("Continue", frame, At(180, 540, 440, 96), new Vector2(440, 96), () => AcknowledgeNotice(OverlayPage.None), OverrideButtonKind.Primary, 62).name = "NoticeContinue";
            OvButton("Settings", frame, At(180, 652, 240, 64), new Vector2(240, 64), () => AcknowledgeNotice(OverlayPage.Settings), OverrideButtonKind.Tertiary, 38).name = "NoticeSettings";
        }

        void AcknowledgeNotice(OverlayPage next)
        {
            GameSettings.NoticeSeen = true;
            GameSettings.Save();
            OpenOverlay(next);
        }

        // New Game replaces a saved run only after the player confirms.
        void RequestNewGame()
        {
            if (GameSave.Exists) OpenOverlay(OverlayPage.Confirm);
            else game.NewGame();
        }
    }
}
