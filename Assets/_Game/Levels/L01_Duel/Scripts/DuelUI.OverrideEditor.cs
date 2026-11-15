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
        // The slide's length follows the distance. The character always faces right (Thomas, 2026-11-14).
        float figureWalkSeconds = FigureSlideSeconds;
        RectTransform figureFloor;
        // The character keeps the overview's size in the editor (1.15 was tried and withdrawn, Thomas 2026-11-14).
        // A place may still ask for another size; the size then changes along the slide.
        float figureScale = 1, figureScaleFrom = 1, figureScaleNow = 1;
        // The body dips once when a rule is confirmed. Previews of what an option does are made separately.
        float figureDipAt = float.NegativeInfinity;
        // What the sentence cards and parameter rows last showed, so only a change is animated.
        readonly System.Collections.Generic.Dictionary<string, string> cardTexts = new System.Collections.Generic.Dictionary<string, string>();
        int builtChipCondition;
        string builtChipAction;
        double panelTurnAt = double.NegativeInfinity;
        RectTransform editorCardLeft, editorCardRight, editorArrow, editorFrame;
        Coroutine confirmSequence;
        int builtStep = -1, builtCategory = -1, builtActionCategory = -1, actionCategory;
        // Both sides share one structure (Thomas, 2026-11-14): the parameter row under the sentence card, a row
        // of category tabs, then the open category's choices in two columns inside a Signal frame joined to its tab.
        // Five rows fit, so a category holds ten choices and a side holds four or five categories.
        const float ChipHeight = 44, TabHeight = 44, ChoiceRow = 48, ChoicePitch = 54;
        static readonly Color ChoiceFill = OverridePalette.SurfaceHover, ChoiceRim = new Color32(78, 78, 92, 255);

        bool ConditionChosen()
        {
            if (pendingRule == null || pendingRule.Condition == 0) return false;
            return ConditionalCatalog.ConditionOptions(pendingRule.GetCondition(0).Id).Length == 0
                || ConditionalCatalog.ConditionOption(pendingRule, 0) >= 0;
        }

        bool ActionChosen()
        {
            if (pendingRule == null || string.IsNullOrEmpty(pendingRule.Action)) return false;
            return ConditionalCatalog.ActionOptions(pendingRule.Action).Length == 0
                || !actionOptionPending && ConditionalCatalog.ActionOption(pendingRule) >= 0;
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
            actionOptionPending = false;
            ConditionalCatalog.SetActionOption(pendingRule, option);
            AdvanceEditorStep(false);
            Show();
        }

        // floor: the slab under the feet, which opens as the figure arrives. scale: 1 is the overview's size.
        void StandFigure(RectTransform stage, Vector2 home, RectTransform floor = null, float scale = 1)
        {
            Vector2 frameHome = stage.anchoredPosition + home;
            if (Time.unscaledTime - figureSeenAt > .25f) figureSlideAt = float.NegativeInfinity;
            else if ((frameHome - figureFrameHome).sqrMagnitude > 1)
            {
                figureSlideFrom = figureFramePosition - frameHome;
                figureSlideAt = Time.unscaledTime;
                figureWalkSeconds = Mathf.Clamp(.26f + Mathf.Abs(figureSlideFrom.x) / 3000f, .3f, .55f);
                figureScaleFrom = figureScaleNow;
            }
            if (float.IsNegativeInfinity(figureSlideAt)) figureScaleFrom = scale;
            figureScale = scale;
            figureFloor = floor;
            figureFrameHome = frameHome;
            overviewFigureHome = home;
            // 245 canvas units per world unit, the same size on the overview and in the editor.
            overviewFigureRoot = OvPlayerFigure(stage, home, 245);
        }

        // rest: the fill this control has when it is not the chosen one; any other fill counts as chosen.
        Button OvOption(string name, string label, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click,
            Color fill, Color ink, float points, float enterDelay, float side, Color? rest = null)
        {
            Color resting = rest ?? OverridePalette.Surface;
            var button = OvButton(label, parent, pos, size, click, OverrideButtonKind.Tertiary, points, enterDelay: enterDelay);
            button.name = name;
            var behaviour = button.GetComponent<SlabButton>();
            behaviour.Fill = fill; behaviour.Ink = ink; behaviour.Shift = 8; behaviour.EnterSide = side; behaviour.Overshoot = true;
            if (fill != resting) MarkSelected(button, name + "/" + label + "/" + parent.name, resting, fill);
            // On the side slab a resting control needs an edge to read as a button rather than as loose words.
            else if (rest.HasValue) behaviour.Rim = ChoiceRim;
            var text = behaviour.Label;
            float pad = Mathf.Min(24, size.x * .12f);
            text.rectTransform.sizeDelta = new Vector2(size.x - pad * 2, size.y);
            text.enableAutoSizing = true; text.fontSizeMax = points; text.fontSizeMin = 14;
            return button;
        }

        Button OvChoice(string label, Transform parent, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction click, Color fill, Color ink,
            string voiceKey, string reason, NeuralBodySystem system, float enterDelay, float side, Color? rest = null)
        {
            var option = OvOption("Option", label, parent, pos, size, () =>
            {
                game.ConfigurationVoice.ReadRuleDescription(voiceKey);
                click();
            }, fill, ink, 28, enterDelay, side, rest);
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
            // Full colour only invites a choice. Once a side holds one, its card rests and the choices below carry the colour.
            bool lit = active && !filled;
            Color fill = lit ? activeFill : filled ? OverridePalette.Neutral : OverridePalette.Surface;
            Color ink = lit ? OverridePalette.Ink : filled ? restInk : OverridePalette.Disabled;
            slab.color = fill; slab.Cut = 22; slab.raycastTarget = false;
            const float pad = 46;
            var title = OvText(heading, face, new Vector2(4, 28), new Vector2(size.x - pad * 2, 28), 22, ink, TextAlignmentOptions.CaplineLeft, false, 1);
            var text = OvText(value, face, new Vector2(-4, -15), new Vector2(size.x - pad * 2, 50), 40, ink, TextAlignmentOptions.CaplineLeft, false, .5f);
            text.enableAutoSizing = true; text.fontSizeMax = 40; text.fontSizeMin = 20;
            var behaviour = root.gameObject.AddComponent<SlabButton>();
            behaviour.Owner = this; behaviour.Button = button; behaviour.Face = face; behaviour.Slab = slab;
            behaviour.Label = text; behaviour.Icon = title; behaviour.Fill = fill; behaviour.Ink = ink;
            behaviour.Shift = 8; behaviour.EnterDelay = enterDelay; behaviour.EnterSide = side;
            // The two cards come in hard from their own sides.
            behaviour.EnterDistance = 420; behaviour.Overshoot = true;
            // The card of the side that has just opened fills from the left.
            if (lit) MarkSelected(button, name, OverridePalette.Surface, OverridePalette.Disabled);
            // New words rise into the card.
            if (!pageEntering && cardTexts.TryGetValue(name, out string before) && before != value)
            {
                var rise = text.gameObject.AddComponent<Arrive>();
                rise.From = new Vector2(0, -24); rise.Seconds = .16f;
            }
            cardTexts[name] = value;
            if (side < 0) editorCardLeft = root; else editorCardRight = root;
        }

        // The open side's ground: one slab from the screen edge to the character. It opens from that edge
        // and closes back onto it, and its inner edge carries the colour of the part being given away.
        void OvSidePanel(RectTransform frame, bool left, Color edge, float from, float to, float delay, double born)
        {
            // It ends above the floor slab. Its inner edge keeps clear of the choices on its own side (which end at
            // 576 and begin at 1024) and of the character and revolver: 584 to 700 on the left, 915 to 1031 on the right.
            const float width = 2040, height = 550, cut = 116, top = 150;
            var panel = Rect(left ? "Panel left" : "Panel right", frame, At(left ? 700 - width : 915, top, width, height), new Vector2(width, height));
            panel.pivot = new Vector2(left ? 0 : 1, .5f);
            panel.anchoredPosition += new Vector2(left ? -width / 2 : width / 2, 0);
            var slab = panel.gameObject.AddComponent<SlabGraphic>();
            slab.color = DialogueFill; slab.Cut = cut; slab.raycastTarget = false;
            const float line = 10;
            var stripe = Rect("Edge", panel, new Vector2((left ? 1 : -1) * (width / 2 - (line + cut) / 2), 0), new Vector2(line + cut, height))
                .gameObject.AddComponent<SlabGraphic>();
            stripe.color = edge; stripe.Cut = cut; stripe.raycastTarget = false;
            if (Mathf.Approximately(from, to)) return;
            var wipe = panel.gameObject.AddComponent<EdgeWipe>();
            wipe.From = from; wipe.To = to; wipe.Delay = delay; wipe.Born = born;
            wipe.Seconds = to > from ? .24f : .14f; wipe.Over = 22 / width; wipe.HideWhenClosed = true;
        }

        // Confirm: the two cards are driven together, the arrow bursts, the body dips, then the rule is installed.
        void ConfirmRule()
        {
            if (confirmSequence != null || !CanConfirmRule()) return;
            confirmSequence = StartCoroutine(PlayConfirm());
        }

        System.Collections.IEnumerator PlayConfirm()
        {
            if (pageControls != null) pageControls.blocksRaycasts = false;
            hoveredRuleOption = null;
            if (editorCardLeft != null) editorCardLeft.gameObject.AddComponent<CardSlam>().Distance = 224;
            if (editorCardRight != null) editorCardRight.gameObject.AddComponent<CardSlam>().Distance = -224;
            if (editorArrow != null)
            {
                if (editorArrow.TryGetComponent(out Drift drift)) drift.enabled = false;
                var burst = editorArrow.gameObject.AddComponent<PopOut>();
                burst.Idle = 1; burst.Delay = .2f; burst.Seconds = .14f; burst.ScaleTo = 2.4f;
            }
            if (editorFrame != null && !GameSettings.ReduceFlashing)
            {
                var white = Image(editorFrame, Vector2.zero, new Vector2(ScreenSpan, ScreenSpan), Color.white);
                white.raycastTarget = false;
                var flash = white.gameObject.AddComponent<PopOut>();
                flash.Idle = 0; flash.Peak = .3f; flash.Delay = .22f; flash.Seconds = .12f;
            }
            figureDipAt = Time.unscaledTime + .22f;
            yield return new WaitForSecondsRealtime(.4f);
            confirmSequence = null;
            if (pageControls != null) pageControls.blocksRaycasts = true;
            CommitRuleChoice();
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
            editorFrame = frame;
            if (fresh) { cardTexts.Clear(); builtChipCondition = 0; builtChipAction = null; panelTurnAt = double.NegativeInfinity; }

            // The open side lies on a slab that reaches in from its screen edge. Changing sides closes one and opens the other.
            double now = Time.unscaledTimeAsDouble;
            if (turned && !fresh) panelTurnAt = now;
            bool turning = now - panelTurnAt < .4;
            Color partColour = string.IsNullOrEmpty(rule.Action) ? OverridePalette.Signal : NeuralPalette.Body(RuleSystem(rule));
            if (turning) OvSidePanel(frame, !selectingCondition, partColour, 1, 0, 0, panelTurnAt);
            if (turning) OvSidePanel(frame, selectingCondition, partColour, 0, 1, .1f, panelTurnAt);
            else if (pageEntering) OvSidePanel(frame, selectingCondition, partColour, 0, 1, .06f, -1);
            else OvSidePanel(frame, selectingCondition, partColour, 1, 1, 0, -1);

            // The sentence: Condition » Action. The card of the open side is filled.
            const float column = 520, leftX = 56, rightX = 1024, cardTop = 160, cardHeight = 108, listTop = 292, listHeight = 398;
            var expression = ConditionalCatalog.Expression(rule);
            bool hasCondition = rule.Condition != 0, hasAction = !string.IsNullOrEmpty(rule.Action);
            Color system = hasAction ? NeuralPalette.Body(RuleSystem(rule)) : OverridePalette.Text;
            tutorialPreview = Rect("RulePreview", frame, At(leftX, cardTop, 1488, cardHeight), new Vector2(1488, cardHeight));
            OvEditorCard("PreviewCondition", "Condition", expression.Conditions, frame, At(leftX, cardTop, column, cardHeight),
                new Vector2(column, cardHeight), () => EditPreview(0), selectingCondition, hasCondition, OverridePalette.Text, OverridePalette.Text,
                fresh ? 0 : -1, -1);
            // Until its parameter is chosen the card shows the action's name alone.
            string actionText = hasAction && !ActionChosen()
                ? ConditionalCatalog.Response(ConditionalCatalog.PickerAction(rule.Action)).Label : expression.Action;
            OvEditorCard("PreviewAction", "Action", actionText, frame, At(rightX, cardTop, column, cardHeight),
                new Vector2(column, cardHeight), () => EditPreview(-1), !selectingCondition, hasAction, system, system,
                fresh ? .05f : -1, 1);
            bool linked = ConditionChosen();
            var arrow = OvText("»", frame, At(616, cardTop - 6, 368, cardHeight), new Vector2(368, cardHeight), 110,
                linked ? OverridePalette.Signal : (Color)new Color32(0x3A, 0x3A, 0x44, 255));
            arrow.name = "PreviewActionArrow";
            editorArrow = arrow.rectTransform;
            if (linked) arrow.gameObject.AddComponent<Drift>();
            if (pageEntering) { var land = arrow.gameObject.AddComponent<SlamIn>(); land.From = 1.35f; land.Seconds = .15f; land.Delay = 2 * OvMotion.Stagger; }

            // The character is the centre of the page and does not move while a rule is written; the panels move
            // around them. The floor ends just above Happy's box and the buttons.
            // Body and revolver together reach 56 units behind the body's centre line and 164 ahead of it. The body
            // stands 54 left of the page's centre so that the whole silhouette sits midway between the two slabs:
            // the back of the body is as far from the left slab's edge as the muzzle is from the right one.
            // The floor is centred under the feet and ends short of both slabs.
            const float stageHeight = 442, floorHeight = 22, floorWidth = 300, bodyX = 800 - 54;
            bodyPreview = Rect("BodyPreview", frame, At(bodyX - floorWidth / 2, listTop, floorWidth, stageHeight), new Vector2(floorWidth, stageHeight));
            float floorTop = -stageHeight / 2 + floorHeight;
            var stage = Rect("Stage", bodyPreview, new Vector2(0, floorTop - floorHeight / 2), new Vector2(floorWidth, floorHeight)).gameObject.AddComponent<SlabGraphic>();
            stage.color = OverridePalette.Neutral; stage.Cut = 10; stage.raycastTarget = false;
            OvArrive(stage, Vector2.zero, 1);
            StandFigure(bodyPreview, new Vector2(0, floorTop + .75f * 245), stage.rectTransform);

            // Both columns exist for the tutorial frame; only the open side holds choices.
            ruleEditor = Rect("RuleEditor", frame, At(leftX, listTop, column, listHeight), new Vector2(column, listHeight));
            ruleList = Rect("RuleList", frame, At(rightX, listTop, column, listHeight), new Vector2(column, listHeight));
            if (selectingCondition) OvConditionStep(ruleEditor, rule, turned);
            else OvActionStep(ruleList, rule, turned);

            var cancel = OvButton("Cancel", frame, At(1020, 762, 200, 72), new Vector2(200, 72), CancelRuleEdit, OverrideButtonKind.Tertiary, 34,
                align: TextAlignmentOptions.Capline);
            cancel.name = "CancelRuleEdit";
            EnterFrom(cancel, 3, 1, 40);
            var confirm = OvButton("Confirm", frame, At(1244, 744, 300, 108), new Vector2(300, 108), ConfirmRule, OverrideButtonKind.Primary, 54,
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

        // Builds the tab row, the frame and its join, and returns where each choice of the open tab goes.
        System.Func<int, Vector2> OvTabs(RectTransform panel, float side, int count, System.Func<int, string> label, System.Func<int, Color> colour,
            int open, System.Action<int> pick, bool enter, bool switched, int choices, out Vector2 cell)
        {
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            Vector2 Slot(float left, float top, float width, float height) => new Vector2(-w / 2 + left + width / 2, h / 2 - top - height / 2);
            const float tabGap = 8, tabsTop = ChipHeight + 10, join = 12, line = 3, lean = 12, pad = 10, columnGap = 10;
            float tab = (w - (count - 1) * tabGap) / count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                bool on = i == open;
                OvOption("Tab" + i, label(i), panel, Slot(i * (tab + tabGap), tabsTop, tab, TabHeight), new Vector2(tab, TabHeight),
                    () => pick(index), on ? colour(i) : ChoiceFill, on ? OverridePalette.Ink : colour(i), 24, enter ? i * .03f : -1, side, ChoiceFill);
            }
            // The frame holds the open tab's choices and leans like the slabs inside it.
            int rows = Mathf.Max(1, (choices + 1) / 2);
            float frameTop = tabsTop + TabHeight + join, frameHeight = pad * 2 + rows * ChoicePitch - (ChoicePitch - ChoiceRow);
            var frame = Rect("Choice frame", panel, Slot(0, frameTop, w, frameHeight), new Vector2(w, frameHeight)).gameObject.AddComponent<SlabGraphic>();
            frame.color = Color.clear; frame.Cut = lean; frame.raycastTarget = false; frame.SetFrame(line, OverridePalette.Signal);
            frame.transform.SetAsFirstSibling();
            // The join drops from the middle of the open tab onto the frame; it travels when another tab opens.
            float joinX = open * (tab + tabGap) + tab / 2 - line / 2;
            var link = Image(panel, Slot(joinX, tabsTop + TabHeight, line, join), new Vector2(line, join), OverridePalette.Signal);
            link.raycastTarget = false; link.transform.SetAsFirstSibling();
            if (switched && !float.IsNaN(bracketLink) && !Mathf.Approximately(bracketLink, joinX))
            {
                var travel = link.gameObject.AddComponent<Arrive>();
                travel.From = new Vector2(bracketLink - joinX, 0); travel.Fade = false; travel.Seconds = .17f;
            }
            bracketLink = joinX;
            // The frame's slanted sides take one lean of room on each side of the two columns.
            float inner = lean + line + 8, width = (w - inner * 2 - columnGap) / 2;
            cell = new Vector2(width, ChoiceRow);
            return i => Slot(inner + i % 2 * (width + columnGap), frameTop + pad + i / 2 * ChoicePitch, width, ChoiceRow);
        }

        void OvConditionStep(RectTransform panel, ConditionalRule rule, bool enter)
        {
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            Vector2 Slot(float left, float top, float width, float height) => new Vector2(-w / 2 + left + width / 2, h / 2 - top - height / 2);
            int selected = selectedConditionIndex < rule.ConditionCount ? rule.GetCondition(selectedConditionIndex).Id : 0;
            // Opening another category brings its choices in; picking a choice does not.
            bool switched = !enter && builtCategory >= 0 && builtCategory != conditionCategory;
            builtCategory = conditionCategory;

            // The parameters complete the sentence in the Condition card, so their row sits directly under it.
            bool unfold = !enter && selected != builtChipCondition;
            builtChipCondition = selected;
            string[] parameters = selected != 0 ? ConditionalCatalog.ConditionOptions(selected) : System.Array.Empty<string>();
            int current = ConditionalCatalog.ConditionOption(rule, selectedConditionIndex);
            float chip = parameters.Length == 0 ? 0 : (w - (parameters.Length - 1) * 8) / parameters.Length;
            for (int p = 0; p < parameters.Length; p++)
            {
                int value = p;
                var draft = rule.Copy();
                ConditionalCatalog.SetConditionOption(draft, value, selectedConditionIndex);
                var button = OvOption("ConditionParameter" + p, parameters[p], panel, Slot(p * (chip + 8), 0, chip, ChipHeight),
                    new Vector2(chip, ChipHeight), () => SelectConditionOption(value), current == p ? OverridePalette.Text : ChoiceFill,
                    current == p ? OverridePalette.Ink : OverridePalette.Text, 22, -1, -1, ChoiceFill);
                button.interactable = ConditionalCatalog.HasAnyCompatibleAction(draft);
                button.GetComponent<SlabButton>().Waiting = current < 0;
                if (unfold) Unfold(button, p);
            }

            var categories = new System.Collections.Generic.List<int>();
            for (int i = 0; i < ConditionalCatalog.Categories.Length; i++) if (CategoryHasSelectableCondition(i)) categories.Add(i);
            int choices = 0;
            foreach (var entry in ConditionalCatalog.Conditions) if (entry.Category == conditionCategory) choices++;
            var place = OvTabs(panel, -1, categories.Count, i => ConditionalCatalog.Categories[categories[i]], i => OverridePalette.Text,
                categories.IndexOf(conditionCategory), i => { selectingCondition = true; conditionCategory = categories[i]; Show(); },
                enter, switched, choices, out var cell);
            int index = 0;
            foreach (var entry in ConditionalCatalog.Conditions)
            {
                if (entry.Category != conditionCategory) continue;
                int condition = entry.Id;
                var candidate = rule.Copy();
                if (condition != selected) candidate.SetCondition(selectedConditionIndex, new RuleCondition(condition));
                string reason = ConditionalCatalog.HasAnyCompatibleAction(candidate) ? null : "These conditions cannot be true together.";
                bool chosen = selected == condition;
                var choice = OvChoice(entry.Label, panel, place(index), cell,
                    () => ApplyChoice(condition, false, null), chosen ? OverridePalette.Text : ChoiceFill,
                    chosen ? OverridePalette.Ink : OverridePalette.Text, "Handler/EditorCondition" + condition, reason, NeuralBodySystem.None,
                    enter ? .09f + index * .03f : switched ? index * .02f : -1, -1, ChoiceFill);
                if (switched) choice.GetComponent<SlabButton>().EnterDistance = 40;
                index++;
            }
        }

        // A parameter row opens downward from the control it belongs to.
        static void Unfold(Button chip, int order)
        {
            var drop = chip.gameObject.AddComponent<Arrive>();
            drop.From = new Vector2(0, 16); drop.Seconds = .15f; drop.Delay = order * OvMotion.Stagger;
        }

        void OvActionStep(RectTransform panel, ConditionalRule rule, bool enter)
        {
            float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
            Vector2 Slot(float left, float top, float width, float height) => new Vector2(-w / 2 + left + width / 2, h / 2 - top - height / 2);
            string picked = ConditionalCatalog.PickerAction(rule.Action);
            // Coming to this side opens the tab that holds the chosen action.
            if (enter && !string.IsNullOrEmpty(picked))
                for (int i = 0; i < ActionGroups.Length; i++)
                    if (ActionGroups[i].System == NeuralPalette.System(ConditionalCatalog.Response(picked).Operation)) actionCategory = i;
            bool switched = !enter && builtActionCategory >= 0 && builtActionCategory != actionCategory;
            builtActionCategory = actionCategory;

            // The parameters complete the sentence in the Action card, so their row sits directly under it.
            bool unfold = !enter && picked != builtChipAction;
            builtChipAction = picked;
            string[] parameters = string.IsNullOrEmpty(rule.Action) ? System.Array.Empty<string>() : ConditionalCatalog.ActionOptions(rule.Action);
            int current = actionOptionPending ? -1 : ConditionalCatalog.ActionOption(rule);
            float chip = parameters.Length == 0 ? 0 : (w - (parameters.Length - 1) * 8) / parameters.Length;
            for (int p = 0; p < parameters.Length; p++)
            {
                int value = p;
                Color part = NeuralPalette.Body(RuleSystem(rule));
                var draft = rule.Copy();
                ConditionalCatalog.SetActionOption(draft, value);
                var button = OvOption("ActionParameter" + p, parameters[p], panel, Slot(p * (chip + 8), 0, chip, ChipHeight), new Vector2(chip, ChipHeight),
                    () => SelectActionOption(value), current == p ? part : ChoiceFill, current == p ? OverridePalette.Ink : part, 22, -1, 1, ChoiceFill);
                button.interactable = ActionChoiceReason(draft, draft.Action) == null;
                button.GetComponent<SlabButton>().Waiting = current < 0;
                if (unfold) Unfold(button, p);
            }

            var group = ActionGroups[actionCategory];
            Color colour = NeuralPalette.Body(group.System);
            int choices = 0;
            foreach (var entry in ConditionalCatalog.Responses) if (NeuralPalette.System(entry.Operation) == group.System) choices++;
            var place = OvTabs(panel, 1, ActionGroups.Length, i => ActionGroups[i].Label, i => NeuralPalette.Body(ActionGroups[i].System),
                actionCategory, i => { selectingCondition = false; actionCategory = i; Show(); }, enter, switched, choices, out var cell);
            int index = 0;
            foreach (var entry in ConditionalCatalog.Responses)
            {
                if (NeuralPalette.System(entry.Operation) != group.System) continue;
                string action = entry.Id;
                bool chosen = picked == action;
                var choice = OvChoice(entry.Label, panel, place(index), cell,
                    () => ApplyChoice(0, false, action), chosen ? colour : ChoiceFill, chosen ? OverridePalette.Ink : colour,
                    "Handler/EditorAction" + action, ActionChoiceReason(rule, action), group.System,
                    enter ? .09f + index * .03f : switched ? index * .02f : -1, 1, ChoiceFill);
                if (switched) choice.GetComponent<SlabButton>().EnterDistance = 40;
                index++;
            }
        }
    }
}
