using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase change (Thomas, 2026-11-06): both rule sidebars freeze as dimmed, crossed-out
    // snapshots at the shockwave, fade and slide away during the countdown while the camera widens,
    // and the scripted lines appear in a subtitle box.
    public sealed partial class DuelUI
    {
        List<ConditionalRule> sealedPlayerRules;
        bool sidebarsSealed;
        float sidebarFadeFrom = -1, sidebarFadeSeconds = 1;
        float sealStarted;
        readonly List<(RectTransform Card, UnityEngine.UI.Image Cover, UnityEngine.UI.Image A, UnityEngine.UI.Image B)> sealMarks = new();
        readonly List<CanvasGroup> sealedGroups = new List<CanvasGroup>();

        public bool SidebarsSealed => sidebarsSealed;
        float SidebarFade => sidebarFadeFrom < 0 ? 0 : Reveal(Time.time - sidebarFadeFrom, sidebarFadeSeconds);

        public void SealSidebars()
        {
            sealedPlayerRules = new List<ConditionalRule>();
            var rules = game.Rules;
            if (rules != null)
                for (int op = 0; op < ConditionalCatalog.OperationLabels.Length; op++)
                    for (int row = 0; row < rules.RuleCount((ControlOperation)op); row++)
                        sealedPlayerRules.Add(rules.RuleAt((ControlOperation)op, row).Copy());
            sealedPlayerRules.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
            sidebarsSealed = true; sidebarFadeFrom = -1;
            sealStarted = Time.time;
            Show();
        }

        public void FadeSidebars(float seconds) { sidebarFadeFrom = Time.time; sidebarFadeSeconds = Mathf.Max(.05f, seconds); }

        public void ClearSidebarSeal()
        {
            bool was = sidebarsSealed;
            sealedPlayerRules = null; sidebarsSealed = false; sidebarFadeFrom = -1;
            if (was) Show();
        }

        // Called for every card while sealed: a dim cover and an X across the whole card.
        void AddSealMark(RuleRowView row)
        {
            var cover = Image(row.Root, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .55f));
            cover.rectTransform.anchorMin = Vector2.zero; cover.rectTransform.anchorMax = Vector2.one; cover.rectTransform.sizeDelta = Vector2.zero;
            cover.raycastTarget = false; cover.name = "SealCover";
            var a = Image(row.Root, Vector2.zero, Vector2.zero, NeuralPalette.Alpha(NeuralPalette.Danger, .85f));
            var b = Image(row.Root, Vector2.zero, Vector2.zero, NeuralPalette.Alpha(NeuralPalette.Danger, .85f));
            a.raycastTarget = b.raycastTarget = false; a.name = b.name = "SealCross";
            sealMarks.Add((row.Root, cover, a, b));
        }

        void ClearSealMarks() { sealMarks.Clear(); sealedGroups.Clear(); }

        void SealGroup(RectTransform sidebar)
        {
            if (sidebar == null || !sidebarsSealed) return;
            var group = sidebar.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            sealedGroups.Add(group);
        }

        void UpdateSealMarks()
        {
            if (!sidebarsSealed) return;
            float age = Time.time - sealStarted;
            float dim = Reveal(age,.14f), stroke = Reveal(age-.14f,.2f);
            foreach (var (card, cover, a, b) in sealMarks)
            {
                if (card == null) continue;
                var size = card.rect.size;
                float length = size.magnitude - 12, angle = Mathf.Atan2(size.y, size.x) * Mathf.Rad2Deg;
                cover.color = new Color(0,0,0,.55f * dim);
                a.rectTransform.sizeDelta = b.rectTransform.sizeDelta = new Vector2(length * stroke, 3);
                Vector2 fromCentre = size.normalized * ((stroke-1)*length/2);
                a.rectTransform.anchoredPosition = fromCentre;
                b.rectTransform.anchoredPosition = new Vector2(fromCentre.x,-fromCentre.y);
                a.rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
                b.rectTransform.localRotation = Quaternion.Euler(0, 0, -angle);
            }
            float alpha = 1 - SidebarFade;
            foreach (var group in sealedGroups) if (group != null) group.alpha = alpha;
        }

        void BuildFinalBossCutscene(RectTransform hud)
        {
            if (!game.FinalBossCutscene) return;
            if (!string.IsNullOrEmpty(game.CutsceneText))
            {
                // Sized to fit between both sidebars at every supported resolution (about 830 canvas units).
                var box = Image(hud, new Vector2(0, 225), new Vector2(760, 128), NeuralPalette.Alpha(NeuralPalette.Panel, .94f));
                box.raycastTarget = false; box.name = "FinalBossCutsceneLine";
                if (!string.IsNullOrEmpty(game.CutsceneSpeakerLabel))
                {
                    var speaker = Text(game.CutsceneSpeakerLabel, hud, new Vector2(-210, 268), new Vector2(300, 32), 22, TextAlignmentOptions.Left, headingFont);
                    speaker.color = NeuralPalette.Secondary; speaker.name = "CutsceneSpeaker";
                }
                sceneDialogue = Text(game.CutsceneText, hud, new Vector2(0, 212), new Vector2(720, 80), 28, face:DialogueFont());
                sceneDialogue.name = "CutsceneText";
                sceneDialogue.maxVisibleCharacters = game.DialogueVisibleCharacters;
            }
        }
    }
}
