using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    /// <summary>
    /// METRICS OVERLAY (partial DuelUI). Press F1 to show or hide a stats panel on the LEFT side of the screen.
    /// It lives on the canvas next to the page (not inside it), so it survives every screen change and also works
    /// while paused. It never blocks clicks.
    ///
    /// Shows: phase and boss, run time, level-clear time, jumps / dodges / shots for this (or the last) fight,
    /// deaths per boss, and the last five fights.
    ///
    /// Functions:
    ///   UpdateMetrics()     F1 toggle, builds the panel once, refreshes the text about 10 times a second.
    ///   BuildMetricsPanel() creates the left-anchored panel with your existing Rect/Text helpers.
    ///   RefreshMetrics()    writes the lines.
    /// </summary>
    public sealed partial class DuelUI
    {
        RectTransform metricsPanel;
        TMP_Text metricsText;
        bool metricsVisible;
        float metricsNextRefresh;
        readonly StringBuilder metricsBuilder = new StringBuilder(600);

        // Called from LateUpdate() in DuelUI.cs (a class can only have one Update, and DuelDeveloperUI.cs owns it).
        int metricsFrame = -1;

        void UpdateMetrics()
        {
            if (metricsFrame == Time.frameCount) return;   // safe to call from more than one place per frame
            metricsFrame = Time.frameCount;
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                metricsVisible = !metricsVisible;
                Debug.Log("[Metrics] F1 pressed. Panel visible: " + metricsVisible);   // remove once it works
            }
            if (canvas == null) return;
            if (metricsPanel == null) BuildMetricsPanel();
            if (metricsPanel.gameObject.activeSelf != metricsVisible) metricsPanel.gameObject.SetActive(metricsVisible);
            if (!metricsVisible) return;

            metricsPanel.SetAsLastSibling();                       // always draw above the page
            if (Time.unscaledTime < metricsNextRefresh) return;    // unscaled so it still updates while paused
            metricsNextRefresh = Time.unscaledTime + .1f;
            RefreshMetrics();
        }

        void BuildMetricsPanel()
        {
            metricsPanel = Rect("Metrics", canvas, Vector2.zero, new Vector2(440, 660));
            metricsPanel.anchorMin = metricsPanel.anchorMax = new Vector2(0, .5f);   // stick to the left edge
            metricsPanel.pivot = new Vector2(0, .5f);
            metricsPanel.anchoredPosition = new Vector2(15, 0);
            var bg = metricsPanel.gameObject.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, .72f);
            bg.raycastTarget = false;
            metricsText = Text("", metricsPanel, Vector2.zero, new Vector2(410, 630), 20, TextAlignmentOptions.TopLeft);
            metricsPanel.gameObject.SetActive(metricsVisible);
        }

        static string Clock(float seconds)
        {
            int minutes = (int)(seconds / 60f);
            return minutes + ":" + (seconds - 60f * minutes).ToString("00.0");
        }

        void RefreshMetrics()
        {
            if (game == null || metricsText == null) return;
            var sb = metricsBuilder; sb.Clear();
            bool fighting = game.Phase == DuelPhase.Fight;
            bool inAttempt = fighting || game.Phase == DuelPhase.Prepare;
            float fightTime = fighting ? game.FightClock : PlayMetrics.LastSeconds;
            int shots = fighting && game.Rifle != null ? game.Rifle.ShotsFired : PlayMetrics.Shots;

            sb.AppendLine("METRICS  (F1 hides)");
            sb.AppendLine("Phase: " + game.Phase + "   Wave " + game.Wave + ": " + PlayMetrics.BossName(game.Wave));
            sb.AppendLine("Run time: " + Clock(PlayMetrics.RunSeconds + (fighting ? game.FightClock : 0f)));
            if (PlayMetrics.LevelCleared) sb.AppendLine("LEVEL CLEAR: " + Clock(PlayMetrics.LevelClearSeconds));
            sb.AppendLine();
            sb.AppendLine(inAttempt ? "THIS FIGHT" : "LAST FIGHT");
            sb.AppendLine("Time: " + fightTime.ToString("0.0") + "s");
            sb.AppendLine("Jumps: " + PlayMetrics.Jumps);
            sb.AppendLine("Dodges: " + PlayMetrics.Dodges);
            sb.AppendLine("Shots: " + shots);
            sb.AppendLine();
            sb.AppendLine("DEATHS BY BOSS");
            for (int wave = 1; wave <= 3; wave++)
                sb.AppendLine(PlayMetrics.BossName(wave) + " (W" + wave + "): " + PlayMetrics.DeathsByWave[wave]);
            sb.AppendLine();
            sb.AppendLine("LAST FIGHTS");
            var history = PlayMetrics.History;
            for (int i = history.Count - 1, shown = 0; i >= 0 && shown < 5; i--, shown++)
            {
                var a = history[i];
                sb.AppendLine("W" + a.Wave + " " + PlayMetrics.BossName(a.Wave) + "  " + (a.Died ? "DIED" : "WON")
                    + "  " + a.Seconds.ToString("0.0") + "s  J" + a.Jumps + " D" + a.Dodges + " S" + a.Shots);
            }
            if (history.Count == 0) sb.AppendLine("(none yet)");
            metricsText.text = sb.ToString();
        }
    }
}