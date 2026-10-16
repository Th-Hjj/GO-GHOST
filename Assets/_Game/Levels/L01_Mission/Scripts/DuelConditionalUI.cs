using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        int selectedOperation;
        int selectorRow = -1, pendingRow = -1;
        bool selectingCondition;
        ConditionalRule pendingRule;
        ConditionalLoadout displayedDraft;
        Vector2Int configurationScreen;
        RectTransform ruleEditor;
        static readonly string[] ShortOperationLabels = { "Move", "Jump", "Aim", "Fire", "Dodge" };
        public int SelectedOperation => selectedOperation;
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

        void SelectOperation(int op)
        {
            selectedOperation = op;
            selectorRow = -1;
            pendingRule = null;
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
                selectedOperation = 0;
                pendingRule = null;
                selectorRow = -1;
            }
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            float width = page.sizeDelta.x, height = page.sizeDelta.y;
            float left = -width / 2 + 32, right = width / 2 - 32;
            float top = height / 2 - 32, bottom = -height / 2 + 32;
            float navigationWidth = Mathf.Clamp(width * .27f, 250, 380);
            bool compact = width < 900 || (width < 1150 && height < 650);
            float editorLeft = compact ? left : left + navigationWidth + 32;
            float editorTop = top - (compact ? 120 : 76);
            float editorBottom = bottom + 82;
            ConfigButton("ConfigureBack", "Back", page, new Vector2(left + 48, top - 22), new Vector2(96, 44), game.MainMenu);
            Text("WAVE " + game.Wave, page, new Vector2(left + 195, top - 22), new Vector2(170, 44), 24, TextAlignmentOptions.Left);
            Text("Rules: " + game.DraftLoadout.TotalRules + "/" + ConditionalCatalog.MaxRulesTotal, page,
                new Vector2(right - 105, top - 22), new Vector2(210, 44), 24, TextAlignmentOptions.Right);

            if (!compact) BuildConfigurationBody(left, navigationWidth, (editorTop + editorBottom) / 2);
            int[] operations = { 2, 3, 4, 0, 1 };
            float[] buttonY = { 106, 54, -10, -88, -140 };
            for (int i = 0; i < operations.Length; i++)
            {
                int op = operations[i];
                Vector2 position = compact
                    ? new Vector2(left + (right - left) * (i + .5f) / 5, top - 88)
                    : new Vector2(left + navigationWidth - 57, (editorTop + editorBottom) / 2 + buttonY[i]);
                ConfigButton("Operation" + op, ShortOperationLabels[op], page, position,
                    new Vector2(compact ? (right - left) / 5 - 8 : 114, 46), () => SelectOperation(op), selectedOperation == op);
            }

            ruleEditor = Rect("RuleEditor", page, new Vector2((editorLeft + right) / 2, (editorTop + editorBottom) / 2),
                new Vector2(right - editorLeft, editorTop - editorBottom));
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y;
            Text(ShortOperationLabels[selectedOperation], ruleEditor, new Vector2(-w / 2 + 100, h / 2 - 24),
                new Vector2(200, 48), 30, TextAlignmentOptions.Left);
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
            int selectedModule = selectedOperation < 2 ? 0 : selectedOperation < 4 ? 1 : 2;
            for (int i = 0; i < 6; i++)
            {
                int module = i >= 4 ? 0 : i >= 2 ? 1 : 2;
                var rect = Rect("BodyRegion" + i, body, positions[i] - new Vector2(860,570), sizes[i]);
                var graphic = rect.gameObject.AddComponent<MissionBodyRegion>();
                graphic.Round = i == 0;
                graphic.Contour = MissionUI.BodyContour(i);
                graphic.Monochrome = true;
                graphic.raycastTarget = i != 0;
                graphic.Selected = i != 0 && module == selectedModule;
                if (i == 0) continue;
                var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = graphic;
                int op = module == selectedModule ? selectedOperation : module == 0 ? 0 : module == 1 ? 2 : 4;
                button.onClick.AddListener(() => SelectOperation(op));
            }
        }

        void BuildRuleRows()
        {
            var config = game.DraftLoadout.Operations[selectedOperation];
            float w = ruleEditor.sizeDelta.x, top = ruleEditor.sizeDelta.y / 2 - 76;
            bool stacked = w < 680;
            float step = stacked ? 104 : 94;
            int count = config.Rules.Count + (pendingRule != null && pendingRow == config.Rules.Count ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                int row = i;
                var rule = pendingRule != null && i == pendingRow ? pendingRule : config.Rules[i];
                float y = top - i * step;
                Text((i + 1).ToString(), ruleEditor, new Vector2(-w / 2 + 12, y - 30), new Vector2(24, 50), 22);
                float fieldWidth = stacked ? w - 172 : (w - 212) / 2;
                float conditionX = -w / 2 + 32 + fieldWidth / 2;
                float actionX = stacked ? conditionX : conditionX + fieldWidth + 28;
                float actionY = stacked ? y - 74 : y - 30;
                ConfigButton("Rule" + i + "Condition", ConditionLabel(rule), ruleEditor, new Vector2(conditionX, y - 30),
                    new Vector2(fieldWidth, stacked ? 42 : 60), () => ChooseRule(row, true), false, true);
                if (!stacked) Text(">", ruleEditor, new Vector2(conditionX + fieldWidth / 2 + 14, y - 30), new Vector2(28, 44), 24);
                ConfigButton("Rule" + i + "Action", string.IsNullOrEmpty(rule.Action) ? "Choose action" : ConditionalCatalog.Response(rule.Action).Label,
                    ruleEditor, new Vector2(actionX, actionY), new Vector2(fieldWidth, stacked ? 42 : 60), () => ChooseRule(row, false), false, true);
                float rowControlsY = y - (stacked ? 52 : 30);
                var up = ConfigButton("MoveRuleUp" + i, "▲", ruleEditor, new Vector2(w / 2 - 110, rowControlsY),
                    new Vector2(38, 44), () => MoveRule(row, -1));
                up.interactable = row > 0 && row < config.Rules.Count;
                var down = ConfigButton("MoveRuleDown" + i, "▼", ruleEditor, new Vector2(w / 2 - 68, rowControlsY),
                    new Vector2(38, 44), () => MoveRule(row, 1));
                down.interactable = row + 1 < config.Rules.Count;
                ConfigButton("RemoveRule" + i, "×", ruleEditor, new Vector2(w / 2 - 22, rowControlsY), new Vector2(44, 44), () => RemoveRule(row));
            }
            float controlsY = top - count * step - 20;
            if (game.DraftLoadout.TotalRules < ConditionalCatalog.MaxRulesTotal && pendingRule == null)
                ConfigButton("AddRule", "+ Add rule", ruleEditor, new Vector2(-w / 2 + 90, controlsY), new Vector2(180, 44), AddRule);
        }

        static string ConditionLabel(ConditionalRule rule) => rule.Condition == 0 ? "Choose condition"
            : rule.Condition == 37 ? (rule.Below ? "Enemy Below" : "Enemy Above") : ConditionalCatalog.Condition(rule.Condition).Label;

        void AddRule()
        {
            if (pendingRule != null || game.DraftLoadout.TotalRules >= ConditionalCatalog.MaxRulesTotal) return;
            // Incomplete choices stay UI-local; the combat loadout only receives a complete pair.
            pendingRule = new ConditionalRule();
            pendingRow = game.DraftLoadout.Operations[selectedOperation].Rules.Count;
            ChooseRule(game.DraftLoadout.Operations[selectedOperation].Rules.Count, true);
        }

        void RemoveRule(int row)
        {
            pendingRule = null;
            pendingRow = selectorRow = -1;
            if (row == game.DraftLoadout.Operations[selectedOperation].Rules.Count)
            {
                pendingRule = null;
                Show();
            }
            else game.EditLoadout(d => d.Operations[selectedOperation].Rules.RemoveAt(row));
        }

        void MoveRule(int row, int direction)
        {
            var rules = game.DraftLoadout.Operations[selectedOperation].Rules;
            int other = row + direction;
            if (row < 0 || row >= rules.Count || other < 0 || other >= rules.Count) return;
            pendingRule = null;
            pendingRow = selectorRow = -1;
            game.EditLoadout(d =>
            {
                var ordered = d.Operations[selectedOperation].Rules;
                var rule = ordered[row];
                ordered[row] = ordered[other];
                ordered[other] = rule;
            });
        }

        ConditionalRule EditingRule => pendingRule;

        void ChooseRule(int row, bool condition)
        {
            if (pendingRule == null || pendingRow != row)
            {
                pendingRule = game.DraftLoadout.Operations[selectedOperation].Rules[row].Copy();
                pendingRow = row;
            }
            selectorRow = row;
            selectingCondition = condition;
            Show();
        }

        void ApplyChoice(int condition, bool below, string action)
        {
            int row = selectorRow;
            if (action == null)
            {
                pendingRule.Condition = condition; pendingRule.Below = below;
                if (!ConditionalCatalog.IsAllowed(selectedOperation, condition, pendingRule.Action)) pendingRule.Action = null;
            }
            else pendingRule.Action = action;
            if (!ConditionalCatalog.IsAllowed(selectedOperation, pendingRule.Condition, pendingRule.Action))
            {
                selectingCondition = pendingRule.Condition == 0;
                Show();
                return;
            }
            var complete = pendingRule.Copy();
            pendingRule = null;
            pendingRow = selectorRow = -1;
            game.EditLoadout(d =>
            {
                var rules = d.Operations[selectedOperation].Rules;
                if (row == rules.Count) d.TryAddRule(selectedOperation, complete);
                else rules[row] = complete;
            });
        }

        void BuildRuleSelector()
        {
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y, top = h / 2;
            var rule = EditingRule;
            ConfigButton("SelectorBack", "<", ruleEditor, new Vector2(-w / 2 + 22, top - 92), new Vector2(44, 44), () => { selectorRow = -1; Show(); });
            string summary = ConditionLabel(rule) + " > " + (string.IsNullOrEmpty(rule.Action) ? "Choose action" : ConditionalCatalog.Response(rule.Action).Label);
            Text(summary, ruleEditor, new Vector2(34, top - 92), new Vector2(w - 76, 50), 22, TextAlignmentOptions.Left);
            var choices = BuildRuleOptions();
            if (selectingCondition)
            {
                foreach (int id in ConditionalCatalog.FormalConditions[selectedOperation])
                {
                    int condition = id;
                    RuleOption(ConditionalCatalog.Condition(id).Label, choices, () => ApplyChoice(condition, false, null), rule.Condition == id);
                }
            }
            else foreach (var entry in ConditionalCatalog.Responses)
            {
                if (!ConditionalCatalog.IsAllowed(selectedOperation, rule.Condition, entry.Id)) continue;
                string action = entry.Id;
                RuleOption(entry.Label, choices, () => ApplyChoice(0, false, action), rule.Action == action);
            }
        }

        RectTransform BuildRuleOptions()
        {
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y - 140;
            var root = Rect("RuleOptions", ruleEditor, new Vector2(0, -65), new Vector2(w, h));
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

        void RuleOption(string label, RectTransform parent, UnityEngine.Events.UnityAction click, bool selected)
        {
            var option = ConfigButton("Option", label, parent, Vector2.zero, new Vector2(parent.rect.width, 56), click, selected, true);
            var layout = option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = 56;
        }
    }
}
