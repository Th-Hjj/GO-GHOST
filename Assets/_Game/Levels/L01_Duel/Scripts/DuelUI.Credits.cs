using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // The credits roll up the title's night sky, where the logo stands, and the title lands when they have gone.
    public sealed partial class DuelUI
    {
        // Trial tuning. Units are the 1600x900 frame's; the window is the sky above the hazard band.
        const float CreditsLead = .5f, CreditsSpeed = 80, CreditsWindowTop = 40, CreditsWindowHeight = 500, CreditsFade = 90;
        const float CreditsRoleHeight = 44, CreditsNameHeight = 84, CreditsGap = 150;
        // A row with columns sets its names side by side in smaller type: the playtesters, three to a line.
        const float CreditsSmallHeight = 58, CreditsColumn = 330;
        // Thomas, 2026-11-18: the course stands over the instructor, the playtesters' names come later,
        // and the fonts are no longer listed (their licence texts ship with the game).
        static readonly (string over, string role, string[] names, int columns)[] CreditRows =
        {
            (null, "A game by", new[] { "Thomas Hong", "Walker Grupp" }, 1),
            (null, "Music", new[] { "Efe Inan" }, 1),
            ("CTIN 489, Fall 2026", "Instructor", new[] { "Jung-Ho Sohn" }, 1),
            (null, "Playtesters", new[] { "[Placeholder]", "[Placeholder]", "[Placeholder]" }, 3),
        };
        static float CreditsRowHeight((string over, string role, string[] names, int columns) row) =>
            (row.over != null ? CreditsRoleHeight : 0) + CreditsRoleHeight + (row.columns > 1
                ? Mathf.Ceil(row.names.Length / (float)row.columns) * CreditsSmallHeight : row.names.Length * CreditsNameHeight);
        bool creditsRoll;
        double creditsAt = -1;
        GameObject creditsBlocker, creditsWindow;

        // The next time the title is built it opens with the roll.
        internal void PlayCredits() { creditsRoll = true; creditsAt = -1; }
        void PlayCreditsFromTitle() { PlayCredits(); Show(); }

        static float CreditsHeight
        {
            get
            {
                float height = -CreditsGap;
                foreach (var row in CreditRows) height += CreditsRowHeight(row) + CreditsGap;
                return height;
            }
        }
        // The logo lands as the last name fades at the top of the window.
        static float CreditsSeconds => CreditsLead + (CreditsWindowHeight + 60 + CreditsHeight - CreditsFade) / CreditsSpeed;

        // Seconds the title's own parts wait, counted from the clock their entrances use; zero when no roll is playing.
        float BeginCredits()
        {
            if (!creditsRoll || game.Phase != DuelPhase.Menu || overlay != OverlayPage.None) return 0;
            double now = Time.unscaledTimeAsDouble;
            if (creditsAt < 0) creditsAt = System.Math.Max(now, OvMotion.EntranceStart);
            double born = pageEntering ? OvMotion.Born : now;
            float wait = (float)(creditsAt + CreditsSeconds - born);
            if (wait <= 0) { creditsRoll = false; return 0; }
            return wait;
        }

        void BuildCreditsRoll(RectTransform frame)
        {
            var window = Rect("Credits", frame, At(92, CreditsWindowTop, 1100, CreditsWindowHeight), new Vector2(1100, CreditsWindowHeight));
            var mask = window.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(0, Mathf.RoundToInt(CreditsFade));
            creditsWindow = window.gameObject;
            float total = CreditsHeight;
            var content = Rect("Roll", window, Vector2.zero, new Vector2(1100, total));
            float y = total / 2;
            foreach (var row in CreditRows)
            {
                foreach (string label in new[] { row.over, row.role })
                {
                    if (label == null) continue;
                    OvText(label, content, new Vector2(0, y - CreditsRoleHeight / 2), new Vector2(1100, CreditsRoleHeight), 30, OverridePalette.TextDim,
                        TextAlignmentOptions.CaplineLeft, false, 1);
                    y -= CreditsRoleHeight;
                }
                for (int i = 0; i < row.names.Length; i++)
                {
                    if (row.columns > 1)
                    {
                        int column = i % row.columns;
                        OvText(row.names[i], content, new Vector2(-550 + CreditsColumn * (column + .5f), y - CreditsSmallHeight / 2),
                            new Vector2(CreditsColumn, CreditsSmallHeight), 44, OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, -.5f);
                        if (column == row.columns - 1 || i == row.names.Length - 1) y -= CreditsSmallHeight;
                        continue;
                    }
                    OvText(row.names[i], content, new Vector2(0, y - CreditsNameHeight / 2), new Vector2(1100, CreditsNameHeight), 76, OverridePalette.Text,
                        TextAlignmentOptions.CaplineLeft, true, -.5f);
                    y -= CreditsNameHeight;
                }
                y -= CreditsGap;
            }
            var roll = content.gameObject.AddComponent<CreditsRoller>();
            roll.StartAt = creditsAt + CreditsLead; roll.Speed = CreditsSpeed;
            roll.From = -CreditsWindowHeight / 2 - 60 - total / 2;
            roll.Apply();

            // Nothing on the title can be pressed while the names pass; any key or click ends the roll.
            var cover = Rect("Credits hold", page, Vector2.zero, Vector2.zero);
            cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one;
            cover.gameObject.AddComponent<Image>().color = Color.clear;
            creditsBlocker = cover.gameObject;
        }

        void UpdateCredits()
        {
            if (!creditsRoll || creditsAt < 0) return;
            if (game.Phase != DuelPhase.Menu) { creditsRoll = false; return; }
            double age = Time.unscaledTimeAsDouble - creditsAt;
            if (age >= CreditsSeconds)
            {
                // The roll has gone and the logo is landing: the title is the title again.
                creditsRoll = false;
                if (creditsBlocker != null) Destroy(creditsBlocker);
                if (creditsWindow != null) Destroy(creditsWindow);
                return;
            }
            if (age < .3 || wipePending) return;
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            bool pressed = keyboard != null && keyboard.anyKey.wasPressedThisFrame
                || mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame);
            if (!pressed) return;
            creditsRoll = false;
            Show();
        }
    }

    // Moves the credits upward at a steady pace on the interface clock.
    public sealed class CreditsRoller : MonoBehaviour
    {
        public double StartAt;
        public float Speed, From;

        void LateUpdate() => Apply();
        public void Apply()
        {
            float travelled = Mathf.Max(0, (float)(Time.unscaledTimeAsDouble - StartAt)) * Speed;
            ((RectTransform)transform).anchoredPosition = new Vector2(0, From + travelled);
        }
    }
}
