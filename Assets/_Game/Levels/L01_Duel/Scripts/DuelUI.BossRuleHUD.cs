using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    // The Final Boss's fixed rules on the right edge (Thomas, 2026-11-06). Same cards and match glow
    // as the player's sidebar; five rows always fit, so the panel has no scroll view.
    public sealed partial class DuelUI
    {
        RectTransform bossRuleSidebar, bossRuleContent;
        TMP_Text bossRuleHeading;
        float bossRulePanelWidth = RulePanelWidth;
        Vector2 bossHudSize;
        readonly List<RuleRowView> bossRuleRows = new List<RuleRowView>(5);
        readonly Dictionary<int, RuleGlowState> bossGlowStates = new Dictionary<int, RuleGlowState>();
        ConditionalController bossGlowOwner;

        ConditionalController ActiveBossRules()
        {
            if (game.Wave != DuelGame.FinalBossWave || (game.Phase != DuelPhase.Prepare && game.Phase != DuelPhase.Fight)) return null;
            foreach (var actor in game.Bosses)
                if (actor != null && actor.Axe != null && actor.IsAlive
                    && (sidebarsSealed || actor.Axe.Transitioning || (!actor.Axe.PhaseTwo && !actor.Axe.RulesSealed))) return actor.Axe.Rules;
            return null;
        }

        void BuildBossRuleHud()
        {
            var rules = ActiveBossRules();
            if (rules != bossGlowOwner) { bossGlowStates.Clear(); bossGlowOwner = rules; }
            if (rules == null) return;
            bossRuleSidebar = Rect("BossRules", page, Vector2.zero, new Vector2(bossRulePanelWidth, 900));
            SealGroup(bossRuleSidebar);
            var background = bossRuleSidebar.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = NeuralPalette.Background; background.raycastTarget = false;
            var divider = Image(bossRuleSidebar, Vector2.zero, Vector2.zero, NeuralPalette.Border);
            divider.rectTransform.anchorMin = new Vector2(0, 0); divider.rectTransform.anchorMax = new Vector2(0, 1);
            divider.rectTransform.sizeDelta = new Vector2(1, 0);
            bossRuleHeading = Text("Boss Rules " + rules.OrderedRows.Count, bossRuleSidebar, Vector2.zero, Vector2.zero, 30, TextAlignmentOptions.Left, headingFont);
            bossRuleHeading.rectTransform.anchorMin = bossRuleHeading.rectTransform.anchorMax = new Vector2(.5f, 1);
            bossRuleContent = Rect("BossRuleList", bossRuleSidebar, Vector2.zero, Vector2.zero);
            bossRuleContent.anchorMin = bossRuleContent.anchorMax = bossRuleContent.pivot = new Vector2(.5f, 1);
            for (int i = 0; i < rules.OrderedRows.Count; i++)
            {
                var entry = rules.OrderedRows[i];
                var operation = (ControlOperation)entry.Op;
                var rule = rules.RuleAt(operation, entry.Row);
                if (!bossGlowStates.TryGetValue(i, out var glow)) bossGlowStates.Add(i, glow = new RuleGlowState());
                var row = new RuleRowView { Rule = rule, Operation = operation, Row = entry.Row, Glow = glow };
                BuildRuleCard(row, bossRuleContent, i, ConditionalCatalog.RuleConditionLabel(rule),
                    ConditionalCatalog.RuleActionLabel(rule), NeuralPalette.Operation(operation));
                bossRuleRows.Add(row);
            }
        }

        void ClearBossRuleHud()
        {
            foreach (var row in bossRuleRows) if (row.Signals != null)
                foreach (var signal in row.Signals) if (signal != null) Destroy(signal);
            bossRuleRows.Clear();
            bossRuleSidebar = bossRuleContent = null; bossRuleHeading = null; bossHudSize = Vector2.zero;
        }

        void LayoutBossRuleHud(float width, float height)
        {
            if (bossRuleSidebar == null) return;
            if (Mathf.Abs(bossHudSize.x - width) > .5f || Mathf.Abs(bossHudSize.y - height) > .5f)
            {
                bossRulePanelWidth = MeasureRulePanelWidth(width);
                bossRuleHeading.rectTransform.sizeDelta = new Vector2(bossRulePanelWidth - 32, 44);
                bossRuleHeading.rectTransform.anchoredPosition = new Vector2(0, -34);
                // Match the player's list: same card width and the same top edge as its scroll viewport.
                float cardWidth = bossRulePanelWidth - 22, y = 2;
                bossRuleContent.sizeDelta = new Vector2(cardWidth, height - 92);
                bossRuleContent.anchoredPosition = new Vector2(-5, -74);
                foreach (var row in bossRuleRows)
                {
                    float cardHeight = LayoutRuleCard(row.Root, row.Number, row.Condition, row.Arrow, row.Action, cardWidth - 4, compact:true);
                    row.Root.anchoredPosition = new Vector2(0, -y - cardHeight / 2);
                    y += cardHeight + 8;
                }
                bossHudSize = new Vector2(width, height);
            }
            bossRuleSidebar.sizeDelta = new Vector2(bossRulePanelWidth, height);
        }

        void UpdateBossRuleHud(float scale)
        {
            bool live = game.Phase == DuelPhase.Fight && bossGlowOwner != null && !sidebarsSealed && !game.FinalBossCutscene;
            for (int i = 0; i < bossRuleRows.Count; i++)
            {
                var row = bossRuleRows[i];
                AlignRuleGlow(row, scale);
                if (live && !game.Paused) row.Glow.Update(bossGlowOwner.RowMatches((int)row.Operation, row.Row), Time.time);
                ApplyRuleGlow(row, NeuralPalette.Operation(row.Operation), live ? row.Glow.Strength : 0);
            }
        }
    }
}
