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
        int builtStep = -1, builtCategory = -1;

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
            if (fill != OverridePalette.Surface) MarkSelected(button, name + "/" + label + "/" + parent.name, OverridePalette.Surface, fill);
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
            button.transition = Selectable.Transition.None; button.targetGraphic = hit;
            button.onClick.AddListener(() => UiClicked(root.gameObject)); button.onClick.AddListener(click);
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
            // The card of the side that has just opened fills from the left.
            if (active) MarkSelected(button, name, filled ? OverridePalette.Neutral : OverridePalette.Surface, filled ? restInk : OverridePalette.Disabled);
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

            OvHeader(frame, "Body Configuration");

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
            if (pageEntering) { var land = arrow.gameObject.AddComponent<SlamIn>(); land.From = 1.35f; land.Seconds = .15f; land.Delay = 2 * OvMotion.Stagger; }

            bodyPreview = Rect("BodyPreview", frame, At(616, listTop, 368, 410), new Vector2(368, 410));
            var stage = Rect("Stage", bodyPreview, new Vector2(0, -194), new Vector2(368, 22)).gameObject.AddComponent<SlabGraphic>();
            stage.color = OverridePalette.Neutral; stage.Cut = 10; stage.raycastTarget = false;
            OvArrive(stage, Vector2.zero, 1);
            StandFigure(bodyPreview, new Vector2(-80, -183 + .75f * 245));

            // Both columns exist for the tutorial frame; only the open side holds choices.
            ruleEditor = Rect("RuleEditor", frame, At(leftX, listTop, column, listHeight), new Vector2(column, listHeight));
            ruleList = Rect("RuleList", frame, At(rightX, listTop, column, listHeight), new Vector2(column, listHeight));
            if (selectingCondition) OvConditionStep(ruleEditor, rule, turned);
            else OvActionStep(ruleList, rule, turned);

            var cancel = OvButton("Cancel", frame, At(1020, 762, 200, 72), new Vector2(200, 72), CancelRuleEdit, OverrideButtonKind.Tertiary, 34,
                align: TextAlignmentOptions.Capline);
            cancel.name = "CancelRuleEdit";
            EnterFrom(cancel, 3, 1, 40);
            var confirm = OvButton("Confirm", frame, At(1244, 744, 300, 108), new Vector2(300, 108), CommitRuleChoice, OverrideButtonKind.Primary, 54,
                align: TextAlignmentOptions.Capline);
            confirm.name = "ConfirmRule";
            confirm.interactable = CanConfirmRule();
            EnterFrom(confirm, 4, 1);
            tutorialSubmit = (RectTransform)confirm.transform;
            if (game.ConfigurationVoice.Focus != TutorialFocus.None)
            {
                game.ConfigurationVoice.RuleDraftChanged(ConditionChosen(), ActionChosen(), confirm.interactable);
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
            int shown = 0, openRow = -1;
            // Opening another category brings its choices in; picking a choice does not.
            bool switched = !enter && builtCategory >= 0 && builtCategory != conditionCategory;
            builtCategory = conditionCategory;
            for (int i = 0; i < ConditionalCatalog.Categories.Length; i++)
            {
                int category = i;
                if (!CategoryHasSelectableCondition(category)) continue;
                bool open = conditionCategory == i;
                if (open) openRow = shown;
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
                var choice = OvChoice(entry.Label, panel, Slot(optionLeft, top, optionWidth, row), new Vector2(optionWidth, row),
                    () => ApplyChoice(condition, false, null), chosen ? OverridePalette.Text : OverridePalette.Surface,
                    chosen ? OverridePalette.Ink : OverridePalette.Text, "Handler/EditorCondition" + condition, reason, NeuralBodySystem.None,
                    enter ? .06f + index * .03f : switched ? index * .02f : -1, -1);
                if (switched && !enter) choice.GetComponent<SlabButton>().EnterDistance = 40;
                top += pitch; index++;
            }
            // Parameters sit under the whole list, so choosing an option never moves the others.
            float listBottom = top - (pitch - row);
            if (selected != 0 && ConditionalCatalog.Condition(selected).Category == conditionCategory)
            {
                int condition = selected;
                string[] parameters = ConditionalCatalog.ConditionOptions(condition);
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
                if (parameters.Length > 0) listBottom = top + 48;
            }
            if (openRow < 0 || index == 0) return;
            // A Signal frame ties the open category to the choices that belong to it. It leans like the slabs inside it.
            const float line = 3, margin = 8;
            float frameTop = -margin, frameBottom = listBottom + margin, height = frameBottom - frameTop;
            float cut = Mathf.Min(20, Mathf.Round(height * .21f)), slope = cut / height;
            // Wide enough that the top row's lower corner and the bottom row's upper corner stay inside.
            float frameLeft = optionLeft - 3 - slope * (height - margin - row);
            float frameRight = w + 3 + cut - slope * (margin + row);
            float link = openRow * pitch + row / 2;
            var frame = Rect("Choice frame", panel, Slot(frameLeft, frameTop, frameRight - frameLeft, height), new Vector2(frameRight - frameLeft, height))
                .gameObject.AddComponent<SlabGraphic>();
            frame.color = Color.clear; frame.Cut = cut; frame.raycastTarget = false; frame.SetFrame(line, OverridePalette.Signal);
            frame.transform.SetAsFirstSibling();
            // The frame's left edge at the height of the open category; below the frame the edge carries on down its own slant.
            float edge = frameLeft + slope * (frameBottom - link);
            if (link > frameBottom)
            {
                Vector2 from = Slot(frameLeft, frameBottom, 0, 0), to = Slot(edge, link, 0, 0);
                var tail = Image(panel, (from + to) / 2, new Vector2((to - from).magnitude + line, line), OverridePalette.Signal);
                tail.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg);
                tail.raycastTarget = false; tail.transform.SetAsFirstSibling();
            }
            float joinLeft = categoryWidth - 4;
            var connector = Image(panel, Slot(joinLeft, link - line / 2, edge - joinLeft + line, line), new Vector2(edge - joinLeft + line, line), OverridePalette.Signal);
            connector.raycastTarget = false; connector.transform.SetAsFirstSibling();
            if (switched && !float.IsNaN(bracketLink) && !Mathf.Approximately(bracketLink, link))
            {
                var travel = connector.gameObject.AddComponent<Arrive>();
                travel.From = new Vector2(0, link - bracketLink); travel.Fade = false; travel.Seconds = .17f;
            }
            bracketLink = link;
        }

        void OvActionStep(RectTransform panel, ConditionalRule rule, bool enter)
        {
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            Vector2 Slot(float left, float top, float width, float height) => new Vector2(-w / 2 + left + width / 2, h / 2 - top - height / 2);
            const float gap = 12, row = 52;
            float cell = (w - gap) / 2, top = 0;
            string picked = ConditionalCatalog.PickerAction(rule.Action);
            int index = 0;

            // Parameters sit in one row under the groups, so choosing an action never moves the others.
            void Chips(float y)
            {
                string[] parameters = ConditionalCatalog.ActionOptions(rule.Action);
                if (parameters.Length == 0) return;
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
            }

            foreach (var group in ActionGroups)
            {
                OvText(group.Label, panel, Slot(4, top, 300, 24), new Vector2(300, 24), 22, OverridePalette.TextDim,
                    TextAlignmentOptions.CaplineLeft, false, 1);
                top += 30;
                Color colour = NeuralPalette.Body(group.System);
                int columnIndex = 0;
                foreach (var entry in ConditionalCatalog.Responses)
                {
                    if (NeuralPalette.System(entry.Operation) != group.System) continue;
                    string action = entry.Id;
                    bool chosen = picked == action;
                    OvChoice(entry.Label, panel, Slot(columnIndex * (cell + gap), top, cell, row), new Vector2(cell, row),
                        () => ApplyChoice(0, false, action), chosen ? colour : OverridePalette.Surface, chosen ? OverridePalette.Ink : colour,
                        "Handler/EditorAction" + action, ActionChoiceReason(rule, action), group.System, enter ? index * .03f : -1, 1);
                    index++;
                    if (++columnIndex < 2) continue;
                    columnIndex = 0; top += row + 8;
                }
                if (columnIndex != 0) top += row + 8;
                top += 6;
            }
            if (!string.IsNullOrEmpty(rule.Action)) Chips(Mathf.Min(top, h - 48));
        }
    }
}
