using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        int actionCategory = 2;
        int selectorRow = -1, pendingRow = -1;
        bool selectingCondition;
        ConditionalRule pendingRule;
        ConditionalLoadout displayedDraft;
        // Presentation order only. Runtime priority remains the relative order within each operation.
        readonly List<ConditionalRule> displayedRules = new List<ConditionalRule>();
        Vector2Int configurationScreen;
        RectTransform ruleEditor;
        static readonly string[] ShortOperationLabels = { "Move", "Jump", "Aim", "Fire", "Dodge" };
        public int SelectedOperation => actionCategory;
        public bool ConfigurationReady => pendingRule == null && selectorRow < 0 && ConditionalCatalog.IsValid(game.DraftLoadout);

        // Only the configuration page uses a pixel floor. Combat/menu retain their authored scale.
        void ConfigurePageScale()
        {
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            var root = canvas.GetComponent<Canvas>();
            configurationScreen = new Vector2Int(Screen.width, Screen.height);
            if (game.Phase == DuelPhase.Configure && !game.DeveloperToolsOpen)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Clamp(Screen.height / 900f, .75f, 1.5f);
                root.scaleFactor = scaler.scaleFactor;
            }
            else
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                root.scaleFactor = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
                if(game.Phase!=DuelPhase.Configure){selectorRow = -1;pendingRule = null;}
            }
        }

        bool RefreshConfigurationLayout()
        {
            if (game == null || game.Phase != DuelPhase.Configure) return false;
            if (configurationScreen.x != Screen.width || configurationScreen.y != Screen.height)
            {
                Show();
                return true;
            }
            if (!DeveloperShortcutConsumed && selectorRow >= 0 && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                selectorRow = -1;
                Show();
            }
            return true;
        }

        void SelectActionCategory(int op)
        {
            actionCategory = op;
            Show();
        }

        UnityEngine.UI.Button ConfigButton(string name, string label, Transform parent, Vector2 pos,
            Vector2 size, UnityEngine.Events.UnityAction click, bool selected = false, bool left = false)
        {
            var button = Button(label, parent, pos, size, click, selected);
            button.name = name;
            var text = button.GetComponentInChildren<TMP_Text>();
            text.fontSize = 22;
            if (left)
            {
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.margin = new Vector4(14, 4, 14, 4);
            }
            return button;
        }

        void BuildConditionalLoadout()
        {
            if (displayedDraft != game.DraftLoadout)
            {
                displayedDraft = game.DraftLoadout;
                displayedRules.Clear();
                actionCategory = 2;
                pendingRule = null;
                selectorRow = -1;
            }
            SyncRuleRows();
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            float width = page.sizeDelta.x, height = page.sizeDelta.y;
            float left = -width / 2 + 32, right = width / 2 - 32;
            float top = height / 2 - 32, bottom = -height / 2 + 32;
            float navigationWidth = Mathf.Clamp(width * .27f, 250, 380);
            bool compact = width < 900 || (width < 1150 && height < 650);
            float editorLeft = compact ? left : left + navigationWidth + 32;
            float editorTop = top - 76;
            float editorBottom = bottom + 82;
            ConfigButton("ConfigureBack", "Back", page, new Vector2(left + 48, top - 22), new Vector2(96, 44), game.MainMenu);
            Text("WAVE " + game.Wave, page, new Vector2(left + 195, top - 22), new Vector2(170, 44), 24, TextAlignmentOptions.Left);
            Text("Rules: " + game.DraftLoadout.TotalRules + "/" + ConditionalCatalog.MaxRulesTotal, page,
                new Vector2(right - 105, top - 22), new Vector2(210, 44), 24, TextAlignmentOptions.Right);

            if (!compact) BuildConfigurationBody(left, navigationWidth, (editorTop + editorBottom) / 2);
            ruleEditor = Rect("RuleEditor", page, new Vector2((editorLeft + right) / 2, (editorTop + editorBottom) / 2),
                new Vector2(right - editorLeft, editorTop - editorBottom));
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y;
            Text(selectorRow < 0 ? "Rules" : selectingCondition ? "Select Condition" : "Select Action", ruleEditor,
                new Vector2(0, h / 2 - 24), new Vector2(w, 48), 30, TextAlignmentOptions.Left);
            Image(ruleEditor, new Vector2(0, h / 2 - 58), new Vector2(w, 1), new Color(.2f, .2f, .2f));
            if (selectorRow >= 0) BuildRuleSelector();
            else BuildRuleRows();

            Image(page, new Vector2((left + right) / 2, bottom + 64), new Vector2(right - left, 1), new Color(.2f, .2f, .2f));
            Text("No match: manual control.", page, new Vector2(left + 175, bottom + 20), new Vector2(350, 44), 22, TextAlignmentOptions.Left);
            var start = ConfigButton("StartFight", "Start Fight", page, new Vector2(right - 105, bottom + 20), new Vector2(210, 48), game.StartFight, true);
            start.interactable = ConfigurationReady;
        }

        void BuildConfigurationBody(float left, float width, float y)
        {
            float bodyScale = (width - 128) / 660f;
            var body = Rect("ConditionalBody", page, new Vector2(left + (width - 128) / 2, y + 12), new Vector2(810, 810));
            body.localScale = Vector3.one * bodyScale;
            Vector2[] positions = { new Vector2(860,821), new Vector2(860,645), new Vector2(663,757.5f), new Vector2(1057,757.5f), new Vector2(801.5f,364), new Vector2(918.5f,364) };
            Vector2[] sizes = { new Vector2(72,88), new Vector2(146,222), new Vector2(264,117), new Vector2(264,117), new Vector2(103,334), new Vector2(103,334) };
            for (int i = 0; i < 6; i++)
            {
                var rect = Rect("BodyRegion" + i, body, positions[i] - new Vector2(860,570), sizes[i]);
                var graphic = rect.gameObject.AddComponent<MissionBodyRegion>();
                graphic.Round = i == 0;
                graphic.Contour = MissionUI.BodyContour(i);
                graphic.Monochrome = true;
                graphic.raycastTarget = false;
                graphic.Selected = false;
            }
        }

        void SyncRuleRows()
        {
            // Keep addition order across redraws, retry and waves, while accepting existing loadout APIs.
            displayedRules.RemoveAll(rule => !ContainsDraftRule(rule));
            foreach (var operation in game.DraftLoadout.Operations)
            {
                foreach (var rule in operation.Rules)
                    if (!displayedRules.Contains(rule)) displayedRules.Add(rule);
                int row = 0;
                for (int i = 0; i < displayedRules.Count; i++)
                    if (operation.Rules.Contains(displayedRules[i])) displayedRules[i] = operation.Rules[row++];
            }
        }

        bool ContainsDraftRule(ConditionalRule rule)
        {
            foreach (var operation in game.DraftLoadout.Operations)
                if (operation.Rules.Contains(rule)) return true;
            return false;
        }

        void WriteRuleOrder(ConditionalLoadout draft)
        {
            foreach (var operation in draft.Operations) operation.Rules.Clear();
            foreach (var rule in displayedRules)
                draft.Operations[(int)ConditionalCatalog.Response(rule.Action).Operation].Rules.Add(rule);
        }

        void BuildRuleRows()
        {
            float w = ruleEditor.sizeDelta.x, top = ruleEditor.sizeDelta.y / 2 - 76;
            bool stacked = w < 680;
            float step = stacked ? 104 : 94;
            int count = displayedRules.Count + (pendingRule != null && pendingRow == displayedRules.Count ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                int row = i;
                var rule = pendingRule != null && i == pendingRow ? pendingRule : displayedRules[i];
                float y = top - i * step;
                Text((i + 1).ToString(), ruleEditor, new Vector2(-w / 2 + 12, y - 30), new Vector2(24, 50), 22);
                float fieldWidth = stacked ? w - 172 : (w - 212) / 2;
                float conditionX = -w / 2 + 32 + fieldWidth / 2;
                float actionX = stacked ? conditionX : conditionX + fieldWidth + 28;
                float actionY = stacked ? y - 74 : y - 30;
                ConfigButton("Rule" + i + "Condition", ConditionLabel(rule), ruleEditor, new Vector2(conditionX, y - 30),
                    new Vector2(fieldWidth, stacked ? 42 : 60), () => ChooseRule(row, true), false, true);
                if (!stacked) Text(">", ruleEditor, new Vector2(conditionX + fieldWidth / 2 + 14, y - 30), new Vector2(28, 44), 24);
                var action = ConfigButton("Rule" + i + "Action", string.IsNullOrEmpty(rule.Action) ? "Select Action" : ConditionalCatalog.Response(rule.Action).Label,
                    ruleEditor, new Vector2(actionX, actionY), new Vector2(fieldWidth, stacked ? 42 : 60), () => ChooseRule(row, false), false, true);
                action.interactable = rule.Condition != 0;
                float rowControlsY = y - (stacked ? 52 : 30);
                var up = ConfigButton("MoveRuleUp" + i, "▲", ruleEditor, new Vector2(w / 2 - 110, rowControlsY),
                    new Vector2(38, 44), () => MoveRule(row, -1));
                up.interactable = row > 0 && row < displayedRules.Count;
                var down = ConfigButton("MoveRuleDown" + i, "▼", ruleEditor, new Vector2(w / 2 - 68, rowControlsY),
                    new Vector2(38, 44), () => MoveRule(row, 1));
                down.interactable = row + 1 < displayedRules.Count;
                ConfigButton("RemoveRule" + i, "×", ruleEditor, new Vector2(w / 2 - 22, rowControlsY), new Vector2(44, 44), () => RemoveRule(row));
            }
            float controlsY = top - count * step - 20;
            if (game.DraftLoadout.TotalRules < ConditionalCatalog.MaxRulesTotal && pendingRule == null)
                ConfigButton("AddRule", "+ Add rule", ruleEditor, new Vector2(-w / 2 + 90, controlsY), new Vector2(180, 44), AddRule);
        }

        static string ConditionLabel(ConditionalRule rule) => rule.Condition == 0 ? "Select Condition"
            : rule.Condition == 37 ? (rule.Below ? "Enemy Below" : "Enemy Above") : ConditionalCatalog.Condition(rule.Condition).Label;

        void AddRule()
        {
            if (pendingRule != null || game.DraftLoadout.TotalRules >= ConditionalCatalog.MaxRulesTotal) return;
            // Incomplete choices stay UI-local; the combat loadout only receives a complete pair.
            pendingRule = new ConditionalRule();
            pendingRow = displayedRules.Count;
            actionCategory = 2;
            ChooseRule(pendingRow, true);
        }

        void RemoveRule(int row)
        {
            pendingRule = null;
            pendingRow = selectorRow = -1;
            if (row == displayedRules.Count) Show();
            else game.EditLoadout(d => { displayedRules.RemoveAt(row); WriteRuleOrder(d); });
        }

        void MoveRule(int row, int direction)
        {
            var rules = displayedRules;
            int other = row + direction;
            if (row < 0 || row >= rules.Count || other < 0 || other >= rules.Count) return;
            pendingRule = null;
            pendingRow = selectorRow = -1;
            game.EditLoadout(d =>
            {
                var rule = rules[row];
                rules[row] = rules[other];
                rules[other] = rule;
                WriteRuleOrder(d);
            });
        }

        ConditionalRule EditingRule => pendingRule;

        void ChooseRule(int row, bool condition)
        {
            if (pendingRule == null || pendingRow != row)
            {
                pendingRule = displayedRules[row].Copy();
                pendingRow = row;
            }
            selectorRow = row;
            selectingCondition = condition || pendingRule.Condition == 0;
            if (!selectingCondition && !string.IsNullOrEmpty(pendingRule.Action))
                actionCategory = (int)ConditionalCatalog.Response(pendingRule.Action).Operation;
            Show();
        }

        void ApplyChoice(int condition, bool below, string action)
        {
            int row = selectorRow;
            if (action == null)
            {
                pendingRule.Condition = condition; pendingRule.Below = below;
                if (!string.IsNullOrEmpty(pendingRule.Action) && !ConditionalCatalog.IsAllowed(
                    (int)ConditionalCatalog.Response(pendingRule.Action).Operation, condition, pendingRule.Action)) pendingRule.Action = null;
                selectingCondition = false;
                Show();
                return;
            }
            else
            {
                if (!ConditionalCatalog.IsAllowed((int)ConditionalCatalog.Response(action).Operation, pendingRule.Condition, action)) return;
                pendingRule.Action = action;
            }
            if (row == displayedRules.Count && displayedRules.Count >= ConditionalCatalog.MaxRulesTotal) return;
            var complete = pendingRule.Copy();
            pendingRule = null;
            pendingRow = selectorRow = -1;
            game.EditLoadout(d =>
            {
                if (row == displayedRules.Count) displayedRules.Add(complete);
                else displayedRules[row] = complete;
                WriteRuleOrder(d);
            });
        }

        void BuildRuleSelector()
        {
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y, top = h / 2;
            var rule = EditingRule;
            ConfigButton("SelectorBack", "<", ruleEditor, new Vector2(-w / 2 + 22, top - 92), new Vector2(44, 44), () => { selectorRow = -1; Show(); });
            string summary = ConditionLabel(rule) + " > " + (string.IsNullOrEmpty(rule.Action) ? "Select Action" : ConditionalCatalog.Response(rule.Action).Label);
            Text(summary, ruleEditor, new Vector2(34, top - 92), new Vector2(w - 76, 50), 22, TextAlignmentOptions.Left);
            if (!selectingCondition)
            {
                int[] categories = { 2, 3, 4, 0, 1 };
                for (int i = 0; i < categories.Length; i++)
                {
                    int op = categories[i];
                    ConfigButton("ActionCategory" + op, ShortOperationLabels[op], ruleEditor,
                        new Vector2(-w / 2 + w * (i + .5f) / 5, top - 147), new Vector2(w / 5 - 8, 44),
                        () => SelectActionCategory(op), actionCategory == op);
                }
            }
            var choices = BuildRuleOptions(selectingCondition ? 0 : 56);
            if (selectingCondition)
            {
                int category = -1;
                foreach (var entry in ConditionalCatalog.Conditions)
                {
                    if (entry.Category != category)
                    {
                        category = entry.Category;
                        var heading = Text(ConditionalCatalog.Categories[category], choices, Vector2.zero, Vector2.zero, 18, TextAlignmentOptions.BottomLeft);
                        heading.name = "ConditionCategory" + category;
                        heading.fontStyle = FontStyles.Bold;
                        heading.color = new Color(.65f, .65f, .65f);
                        heading.margin = new Vector4(14, 0, 14, 0);
                        var layout = heading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                        layout.minHeight = layout.preferredHeight = 34;
                    }
                    int condition = entry.Id;
                    RuleOption(entry.Label, choices, () => ApplyChoice(condition, false, null), rule.Condition == condition);
                }
            }
            else foreach (string id in ConditionalCatalog.FormalResponses[actionCategory])
            {
                string action = id;
                RuleOption(ConditionalCatalog.Response(action).Label, choices, () => ApplyChoice(0, false, action), rule.Action == action,
                    ConditionalCatalog.IncompatibilityReason(actionCategory, rule.Condition, action));
            }
        }

        RectTransform BuildRuleOptions(float topInset)
        {
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y - 140 - topInset;
            var root = Rect("RuleOptions", ruleEditor, new Vector2(0, -65 - topInset / 2), new Vector2(w, h));
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            var viewport = Rect("Viewport", root, new Vector2(-8, 0), new Vector2(w - 20, h));
            var maskImage = viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            maskImage.color = Color.black;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, Vector2.zero, new Vector2(w - 20, 0));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var barRoot = Rect("Scrollbar", root, new Vector2(w / 2 - 4, 0), new Vector2(8, h));
            var track = barRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            track.color = new Color(.12f, .12f, .12f);
            var handle = Image(barRoot, Vector2.zero, new Vector2(8, h), new Color(.5f, .5f, .5f));
            // Scrollbar drives handle anchors; a fixed size would extend it beyond the track.
            handle.rectTransform.sizeDelta = Vector2.zero;
            handle.raycastTarget = true;
            var bar = barRoot.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.handleRect = handle.rectTransform;
            bar.targetGraphic = handle;
            bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        void RuleOption(string label, RectTransform parent, UnityEngine.Events.UnityAction click, bool selected, string reason = null)
        {
            var option = ConfigButton("Option", label, parent, Vector2.zero, new Vector2(parent.rect.width, 56), click, selected, true);
            option.interactable = reason == null;
            if (reason != null)
            {
                var text = option.GetComponentInChildren<TMP_Text>();
                text.richText = true;
                text.text = label + "\n<size=75%>" + reason + "</size>";
                text.color = new Color(.5f, .5f, .5f);
            }
            var layout = option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = 56;
        }
    }
}
