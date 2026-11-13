using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Override UI, stage 4: the rule editor as a Condition step and an Action step around the centre-stage character.
    public sealed partial class DuelUI
    {
        const float FigureSlideSeconds = .32f;
        // Frame-space bookkeeping lets the rebuilt character continue from where the last page left it.
        Vector2 figureSlideFrom, figureFrameHome, figureFramePosition;
        float figureSlideAt = float.NegativeInfinity, figureSeenAt = float.NegativeInfinity;
        int builtStep = -1;
        bool editorTour;

        bool ConditionChosen()
        {
            if (pendingRule == null || pendingRule.Condition == 0) return false;
            return ConditionalCatalog.ConditionOptions(pendingRule.GetCondition(0).Id).Length == 0
                || ConditionalCatalog.ConditionOption(pendingRule, 0) >= 0;
        }

        bool ActionChosen()
        {
            if (pendingRule == null || string.IsNullOrEmpty(pendingRule.Action)) return false;
            return ConditionalCatalog.ActionOptions(pendingRule.Action).Length == 0 || ConditionalCatalog.ActionOption(pendingRule) >= 0;
        }

        // Completing one side opens the other while it is still empty.
        void AdvanceEditorStep(bool fromCondition)
        {
            if (fromCondition) selectingCondition = !(ConditionChosen() && !ActionChosen());
            else if (ActionChosen() && !ConditionChosen())
            {
                selectingCondition = true; selectedConditionIndex = 0; selectingAdditional = false;
                conditionCategory = pendingRule.Condition != 0 ? ConditionalCatalog.Condition(pendingRule.Condition).Category : conditionCategory;
            }
        }

        void SelectActionOption(int option)
        {
            selectingCondition = false;
            ConditionalCatalog.SetActionOption(pendingRule, option);
            AdvanceEditorStep(false);
            Show();
        }

        // While Happy explains the editor, show the side she is talking about, then return to the open choice.
        bool FollowEditorTour()
        {
            if (selectorRow < 0 || pendingRule == null) return false;
            var focus = VisibleTutorialFocus;
            bool condition = selectingCondition;
            if (focus == TutorialFocus.Condition) { condition = true; editorTour = true; }
            else if (focus == TutorialFocus.Action) { condition = false; editorTour = true; }
            else if (focus != TutorialFocus.Relation && editorTour)
            {
                editorTour = false;
                if (!ConditionChosen()) condition = true;
            }
            if (condition == selectingCondition) return false;
            selectingCondition = condition; selectedConditionIndex = 0; selectingAdditional = false;
            Show();
            return true;
        }

        void StandFigure(RectTransform stage, Vector2 home)
        {
            Vector2 frameHome = stage.anchoredPosition + home;
            if (Time.unscaledTime - figureSeenAt > .25f) figureSlideAt = float.NegativeInfinity;
            else if ((frameHome - figureFrameHome).sqrMagnitude > 1)
            {
                figureSlideFrom = figureFramePosition - frameHome;
                figureSlideAt = Time.unscaledTime;
            }
            figureFrameHome = frameHome;
            overviewFigureHome = home;
            // 245 canvas units per world unit, the same size on the overview and in the editor.
            overviewFigureRoot = OvPlayerFigure(stage, home, 245);
        }

        Button OvOption(string name, string label, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click,
            Color fill, Color ink, float points, float enterDelay, float side)
        {
            var button = OvButton(label, parent, pos, size, click, OverrideButtonKind.Tertiary, points, enterDelay: enterDelay);
            button.name = name;
            var behaviour = button.GetComponent<SlabButton>();
            behaviour.Fill = fill; behaviour.Ink = ink; behaviour.Shift = 8; behaviour.EnterSide = side;
            var text = behaviour.Label;
            float pad = Mathf.Min(24, size.x * .12f);
            text.rectTransform.sizeDelta = new Vector2(size.x - pad * 2, size.y);
            text.enableAutoSizing = true; text.fontSizeMax = points; text.fontSizeMin = 14;
            return button;
        }

        Button OvChoice(string label, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click, Color fill, Color ink,
            string voiceKey, string reason, NeuralBodySystem system, float enterDelay, float side)
        {
            var option = OvOption("Option", label, parent, pos, size, () =>
            {
                game.ConfigurationVoice.ReadRuleDescription(voiceKey);
                click();
            }, fill, ink, 28, enterDelay, side);
            option.interactable = reason == null;
            var hover = option.gameObject.AddComponent<RuleOptionHover>();
            hover.Description = reason ?? VoiceStudioCatalog.Current.Text(voiceKey, label);
            bool demonstrate = system != NeuralBodySystem.None && reason == null;
            hover.Enter = () => { hoveredRuleOption = hover; if (demonstrate) PreviewRule(system); };
            hover.Leave = () => { if (hoveredRuleOption == hover) hoveredRuleOption = null; if (demonstrate) PreviewRule(NeuralBodySystem.None); };
            return option;
        }

        void OvEditorCard(string name, string heading, string value, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click,
            bool active, bool filled, Color activeFill, Color restInk, float enterDelay, float side)
        {
            var root = Rect(name, parent, pos, size);
            var hit = root.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
            var button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None; button.targetGraphic = hit; button.onClick.AddListener(click);
            var face = Rect("Face", root, Vector2.zero, size);
            var slab = face.gameObject.AddComponent<SlabGraphic>();
            Color fill = active ? activeFill : filled ? OverridePalette.Neutral : OverridePalette.Surface;
            Color ink = active ? OverridePalette.Ink : filled ? restInk : OverridePalette.Disabled;
            slab.color = fill; slab.Cut = 22; slab.raycastTarget = false;
            const float pad = 46;
            var title = OvText(heading, face, new Vector2(4, 28), new Vector2(size.x - pad * 2, 28), 22, ink, TextAlignmentOptions.CaplineLeft, false, 1);
            var text = OvText(value, face, new Vector2(-4, -15), new Vector2(size.x - pad * 2, 50), 40, ink, TextAlignmentOptions.CaplineLeft, false, .5f);
            text.enableAutoSizing = true; text.fontSizeMax = 40; text.fontSizeMin = 20;
            var behaviour = root.gameObject.AddComponent<SlabButton>();
            behaviour.Owner = this; behaviour.Button = button; behaviour.Face = face; behaviour.Slab = slab;
            behaviour.Label = text; behaviour.Icon = title; behaviour.Fill = fill; behaviour.Ink = ink;
            behaviour.Shift = 8; behaviour.EnterDelay = enterDelay; behaviour.EnterSide = side;
        }

        void BuildOverrideEditor()
        {
            installingCard = null;
            previewParts.Clear(); previewConditionJoins.Clear();
            for (int i = 0; i < neuralRegions.Length; i++) neuralRegions[i] = null;
            OvFill(page, OverridePalette.Ink);
            var frame = OvFrame();
            float fit = frame.localScale.x;
            var rule = pendingRule;
            int step = selectingCondition ? 0 : 1;
            bool fresh = builtSelector != selectorRow, turned = fresh || builtStep != step;
            builtStep = step;

            OvText("Body Configuration", frame, new Vector2(56 + 600 - 800, 450 - 81.6f), new Vector2(1200, 112), 112,
                OverridePalette.Text, TextAlignmentOptions.CaplineLeft, true, -.5f);
            OvButton("Help", frame, At(1424, 56, 120, 48), new Vector2(120, 48), OpenHelp, OverrideButtonKind.Tertiary, 28,
                align: TextAlignmentOptions.Capline).name = "ConfigurationHelp";
            OvRule(frame, 56, 132, 1488);

            // The sentence: Condition » Action. The card of the open side is filled.
            const float column = 520, leftX = 56, rightX = 1024, cardTop = 160, cardHeight = 108, listTop = 292, listHeight = 398;
            var expression = ConditionalCatalog.Expression(rule);
            bool hasCondition = rule.Condition != 0, hasAction = !string.IsNullOrEmpty(rule.Action);
            Color system = hasAction ? NeuralPalette.Body(RuleSystem(rule)) : OverridePalette.Text;
            tutorialPreview = Rect("RulePreview", frame, At(leftX, cardTop, 1488, cardHeight), new Vector2(1488, cardHeight));
            OvEditorCard("PreviewCondition", "Condition", expression.Conditions, frame, At(leftX, cardTop, column, cardHeight),
                new Vector2(column, cardHeight), () => EditPreview(0), selectingCondition, hasCondition, OverridePalette.Text, OverridePalette.Text,
                fresh ? 0 : -1, -1);
            OvEditorCard("PreviewAction", "Action", expression.Action, frame, At(rightX, cardTop, column, cardHeight),
                new Vector2(column, cardHeight), () => EditPreview(-1), !selectingCondition, hasAction, system, system,
                fresh ? .05f : -1, 1);
            bool linked = ConditionChosen();
            var arrow = OvText("»", frame, At(616, cardTop - 6, 368, cardHeight), new Vector2(368, cardHeight), 110,
                linked ? OverridePalette.Signal : (Color)new Color32(0x3A, 0x3A, 0x44, 255));
            arrow.name = "PreviewActionArrow";
            if (linked) arrow.gameObject.AddComponent<Drift>();

            bodyPreview = Rect("BodyPreview", frame, At(616, listTop, 368, 410), new Vector2(368, 410));
            var stage = Rect("Stage", bodyPreview, new Vector2(0, -194), new Vector2(368, 22)).gameObject.AddComponent<SlabGraphic>();
            stage.color = OverridePalette.Neutral; stage.Cut = 10; stage.raycastTarget = false;
            StandFigure(bodyPreview, new Vector2(-80, -183 + .75f * 245));

            // Both columns exist for the tutorial frame; only the open side holds choices.
            ruleEditor = Rect("RuleEditor", frame, At(leftX, listTop, column, listHeight), new Vector2(column, listHeight));
            ruleList = Rect("RuleList", frame, At(rightX, listTop, column, listHeight), new Vector2(column, listHeight));
            if (selectingCondition) OvConditionStep(ruleEditor, rule, turned);
            else OvActionStep(ruleList, rule, turned);

            var cancel = OvButton("Cancel", frame, At(1020, 762, 200, 72), new Vector2(200, 72), CancelRuleEdit, OverrideButtonKind.Tertiary, 34,
                align: TextAlignmentOptions.Capline);
            cancel.name = "CancelRuleEdit";
            var confirm = OvButton("Confirm", frame, At(1244, 744, 300, 108), new Vector2(300, 108), CommitRuleChoice, OverrideButtonKind.Primary, 54,
                align: TextAlignmentOptions.Capline);
            confirm.name = "ConfirmRule";
            confirm.interactable = CanConfirmRule();
            tutorialSubmit = (RectTransform)confirm.transform;
            if (game.ConfigurationVoice.Focus != TutorialFocus.None)
            {
                game.ConfigurationVoice.RuleDraftChanged(confirm.interactable);
                page.GetComponentsInChildren(false, tutorialEditorButtons);
            }

            // Happy's box reads the hovered choice, left of Cancel and Confirm.
            float halfWidth = page.sizeDelta.x / 2, halfHeight = page.sizeDelta.y / 2;
            dialogueFooterLeftInset = halfWidth - 744 * fit;
            dialogueFooterRightInset = halfWidth - 180 * fit;
            dialogueFooterBottomInset = halfHeight - 402 * fit;
            bodyPreview.SetAsLastSibling();
            UpdateConfigurationSignals();
        }

        void OvConditionStep(RectTransform panel, ConditionalRule rule, bool enter)
        {
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            Vector2 Slot(float left, float top, float width, float height) => new Vector2(-w / 2 + left + width / 2, h / 2 - top - height / 2);
            int selected = selectedConditionIndex < rule.ConditionCount ? rule.GetCondition(selectedConditionIndex).Id : 0;
            const float categoryWidth = 208, gap = 16, row = 56, pitch = 64;
            float optionLeft = categoryWidth + gap, optionWidth = w - optionLeft;
            int shown = 0;
            for (int i = 0; i < ConditionalCatalog.Categories.Length; i++)
            {
                int category = i;
                if (!CategoryHasSelectableCondition(category)) continue;
                bool open = conditionCategory == i;
                OvOption("ConditionCategory" + i, ConditionalCatalog.Categories[i], panel, Slot(0, shown * pitch, categoryWidth, row),
                    new Vector2(categoryWidth, row), () => { selectingCondition = true; conditionCategory = category; Show(); },
                    open ? OverridePalette.Text : OverridePalette.Surface, open ? OverridePalette.Ink : OverridePalette.Text, 26,
                    enter ? shown * .03f : -1, -1);
                shown++;
            }
            float top = 0;
            int index = 0;
            foreach (var entry in ConditionalCatalog.Conditions)
            {
                if (entry.Category != conditionCategory) continue;
                int condition = entry.Id;
                var candidate = rule.Copy();
                if (condition != selected) candidate.SetCondition(selectedConditionIndex, new RuleCondition(condition));
                string reason = ConditionalCatalog.HasAnyCompatibleAction(candidate) ? null : "These conditions cannot be true together.";
                bool chosen = selected == condition;
                OvChoice(entry.Label, panel, Slot(optionLeft, top, optionWidth, row), new Vector2(optionWidth, row),
                    () => ApplyChoice(condition, false, null), chosen ? OverridePalette.Text : OverridePalette.Surface,
                    chosen ? OverridePalette.Ink : OverridePalette.Text, "Handler/EditorCondition" + condition, reason, NeuralBodySystem.None,
                    enter ? .06f + index * .03f : -1, -1);
                top += pitch; index++;
                if (!chosen) continue;
                // Parameters open directly under the chosen option.
                string[] parameters = ConditionalCatalog.ConditionOptions(condition);
                if (parameters.Length == 0) continue;
                int current = ConditionalCatalog.ConditionOption(rule, selectedConditionIndex);
                float chip = (optionWidth - (parameters.Length - 1) * 8) / parameters.Length;
                for (int p = 0; p < parameters.Length; p++)
                {
                    int value = p;
                    var draft = rule.Copy();
                    ConditionalCatalog.SetConditionOption(draft, value, selectedConditionIndex);
                    var button = OvOption("ConditionParameter" + p, parameters[p], panel, Slot(optionLeft + p * (chip + 8), top, chip, 48),
                        new Vector2(chip, 48), () => SelectConditionOption(value), current == p ? OverridePalette.Text : OverridePalette.Surface,
                        current == p ? OverridePalette.Ink : OverridePalette.Text, 22, -1, -1);
                    button.interactable = ConditionalCatalog.HasAnyCompatibleAction(draft);
                    button.GetComponent<SlabButton>().Waiting = current < 0;
                }
                top += 56;
            }
        }

        void OvActionStep(RectTransform panel, ConditionalRule rule, bool enter)
        {
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            Vector2 Slot(float left, float top, float width, float height) => new Vector2(-w / 2 + left + width / 2, h / 2 - top - height / 2);
            const float gap = 12, row = 52;
            float cell = (w - gap) / 2, top = 0;
            string picked = ConditionalCatalog.PickerAction(rule.Action);
            int index = 0;

            float Chips(float y)
            {
                string[] parameters = ConditionalCatalog.ActionOptions(rule.Action);
                if (parameters.Length == 0) return y;
                int current = ConditionalCatalog.ActionOption(rule);
                Color colour = NeuralPalette.Body(RuleSystem(rule));
                // Two chips sit under the two option columns.
                float space = parameters.Length == 2 ? gap : 8;
                float chip = (w - (parameters.Length - 1) * space) / parameters.Length;
                for (int p = 0; p < parameters.Length; p++)
                {
                    int value = p;
                    var draft = rule.Copy();
                    ConditionalCatalog.SetActionOption(draft, value);
                    var button = OvOption("ActionParameter" + p, parameters[p], panel, Slot(p * (chip + space), y, chip, 48), new Vector2(chip, 48),
                        () => SelectActionOption(value), current == p ? colour : OverridePalette.Surface,
                        current == p ? OverridePalette.Ink : colour, 22, -1, 1);
                    button.interactable = ActionChoiceReason(draft, draft.Action) == null;
                    button.GetComponent<SlabButton>().Waiting = current < 0;
                }
                return y + 56;
            }

            foreach (var group in ActionGroups)
            {
                OvText(group.Label, panel, Slot(4, top, 300, 24), new Vector2(300, 24), 22, OverridePalette.TextDim,
                    TextAlignmentOptions.CaplineLeft, false, 1);
                top += 30;
                Color colour = NeuralPalette.Body(group.System);
                int columnIndex = 0;
                bool parametersDue = false;
                foreach (var entry in ConditionalCatalog.Responses)
                {
                    if (NeuralPalette.System(entry.Operation) != group.System) continue;
                    string action = entry.Id;
                    bool chosen = picked == action;
                    OvChoice(entry.Label, panel, Slot(columnIndex * (cell + gap), top, cell, row), new Vector2(cell, row),
                        () => ApplyChoice(0, false, action), chosen ? colour : OverridePalette.Surface, chosen ? OverridePalette.Ink : colour,
                        "Handler/EditorAction" + action, ActionChoiceReason(rule, action), group.System, enter ? index * .03f : -1, 1);
                    index++;
                    parametersDue |= chosen;
                    if (++columnIndex < 2) continue;
                    columnIndex = 0; top += row + 8;
                    if (parametersDue) { top = Chips(top); parametersDue = false; }
                }
                if (columnIndex != 0)
                {
                    top += row + 8;
                    if (parametersDue) top = Chips(top);
                }
                top += 6;
            }
        }
    }
}
