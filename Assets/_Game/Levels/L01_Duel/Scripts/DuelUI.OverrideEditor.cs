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
        // Both sides share one layout (Thomas, 2026-11-14), read in the order it is used: a column of categories,
        // a Signal join to a Signal frame that holds the open category's choices in one column, and the parameter
        // row as the last row inside that frame. Every row steps left by the slabs' own slant, so the columns and
        // the frame run parallel to each slab's edge and the frame keeps one clearance all the way round.
        // Six rows fit: five choices and their parameters, or six choices.
        const float PickRow = 56, PickPitch = 64, PickChip = 48, PickCategory = 148;
        const float PickSlant = 12f / 56f, PickLine = 3, PickClear = 10, PickJoin = 22;
        // Each side is one figure: the sentence card and the list under it share two slanted guide lines. The
        // category column's left edge continues the card's left edge and the frame's right edge continues the
        // card's right edge, so the list is as wide as the card and sits under it to the eye, not only on paper.
        const float EditorColumn = 520, EditorCardHeight = 108;
        const float PickChoice = EditorColumn - (EditorCardHeight - PickRow) * PickSlant
            - (PickCategory + PickJoin + (PickLine + PickClear) * 2 - PickRow * PickSlant);
        // Where the top category starts within its column; set per side before the list is built.
        float pickIndent;
        // Where the choices begin: the category column, the join and the frame's room, less one slab's lean.
        float PickLeft => pickIndent + PickCategory + PickJoin + PickLine + PickClear - PickRow * PickSlant;
        // Resting controls on the side slab are a clear step lighter than it, so they read as buttons.
        static readonly Color ChoiceFill = OverridePalette.SurfaceHover;

        // A place in the picker. A row lower down starts further left, along the slant.
        static Vector2 PickSlot(RectTransform panel, float left, float top, float width, float height)
            => new Vector2(-panel.sizeDelta.x / 2 + left - top * PickSlant + width / 2, panel.sizeDelta.y / 2 - top - height / 2);

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
            // The card leans exactly as the rows under it do, so their edges can share one line.
            slab.color = fill; slab.Cut = size.y * PickSlant; slab.raycastTarget = false;
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
            // 576 and begin at 1024) and of the character and revolver: 584 to 700 on the left, 895 to 1011 on the right.
            const float width = 2040, height = 550, cut = 116, top = 150;
            var panel = Rect(left ? "Panel left" : "Panel right", frame, At(left ? 700 - width : 895, top, width, height), new Vector2(width, height));
            panel.pivot = new Vector2(left ? 0 : 1, .5f);
            panel.anchoredPosition += new Vector2(left ? -width / 2 : width / 2, 0);
            var slab = panel.gameObject.AddComponent<SlabGraphic>();
            slab.color = DialogueFill; slab.Cut = cut; slab.raycastTarget = false;
            const float line = 5;
            var stripe = Rect("Edge", panel, new Vector2((left ? 1 : -1) * (width / 2 - (line + cut) / 2), 0), new Vector2(line + cut, height))
                .gameObject.AddComponent<SlabGraphic>();
            stripe.color = edge; stripe.Cut = cut; stripe.raycastTarget = false;
            if (Mathf.Approximately(from, to)) return;
            var wipe = panel.gameObject.AddComponent<EdgeWipe>();
            wipe.From = from; wipe.To = to; wipe.Delay = delay; wipe.Born = born;
            wipe.Seconds = to > from ? .24f : .14f; wipe.Over = 22 / width; wipe.HideWhenClosed = true;
        }

        const float ClosingSideSeconds = .12f;
        RectTransform closingSide;
        bool closingLeft;

        // The side that closes keeps its categories, frame and choices for a moment: they are lifted out of the
        // page that is about to be rebuilt, and leave toward their own screen edge with the slab under them.
        void LiftClosingSide()
        {
            if (closingSide != null) { Destroy(closingSide.gameObject); closingSide = null; }
            int step = selectingCondition ? 0 : 1;
            if (builtSelector != selectorRow || builtStep < 0 || builtStep == step || page == null) return;
            var list = builtStep == 0 ? ruleEditor : ruleList;
            if (list == null || !list.IsChildOf(page)) return;
            if (hoveredRuleOption != null && hoveredRuleOption.transform.IsChildOf(list)) hoveredRuleOption = null;
            list.SetParent(canvas, true);
            closingSide = list; closingLeft = builtStep == 0;
        }

        // Called once the new page has its side slabs, so the leaving choices lie on the slab that is closing.
        void LeaveClosingSide(RectTransform frame)
        {
            if (closingSide == null) return;
            var list = closingSide; closingSide = null;
            list.SetParent(frame, true);
            list.name = "Closing side";
            foreach (var button in list.GetComponentsInChildren<SlabButton>()) button.Frozen = true;
            if (!list.TryGetComponent(out CanvasGroup group)) group = list.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1; group.blocksRaycasts = false; group.interactable = false;
            list.gameObject.AddComponent<PageLeave>().Seconds = ClosingSideSeconds;
            var away = list.gameObject.AddComponent<SlideAway>();
            away.To = new Vector2(closingLeft ? -60 : 60, 0); away.Seconds = ClosingSideSeconds; away.Go();
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
            // The hit shows on the two cards alone: they whiten as they meet and cool slowly. Nothing lights the page.
            if (!GameSettings.ReduceFlashing) { CardGlow(editorCardLeft); CardGlow(editorCardRight); }
            figureDipAt = Time.unscaledTime + .22f;
            yield return new WaitForSecondsRealtime(.56f);
            confirmSequence = null;
            if (pageControls != null) pageControls.blocksRaycasts = true;
            CommitRuleChoice();
        }

        // White laid over a sentence card's slab, under its words.
        void CardGlow(RectTransform card)
        {
            var face = card != null ? card.Find("Face") as RectTransform : null;
            if (face == null || !face.TryGetComponent(out SlabGraphic slab)) return;
            var glow = Rect("Glow", face, Vector2.zero, face.rect.size);
            glow.SetAsFirstSibling();
            var white = glow.gameObject.AddComponent<SlabGraphic>();
            white.color = Color.white; white.Cut = slab.Cut; white.raycastTarget = false;
            var fade = glow.gameObject.AddComponent<PopOut>();
            fade.Idle = 0; fade.Peak = .5f; fade.Delay = .17f; fade.Rise = .07f; fade.Seconds = .3f;
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
            LeaveClosingSide(frame);

            // The sentence: Condition » Action. The card of the open side is filled.
            const float column = 520, leftX = 56, rightX = 1024, cardTop = 160, cardHeight = 108, listTop = 308, listHeight = 392;
            var expression = ConditionalCatalog.Expression(rule);
            bool hasCondition = rule.Condition != 0, hasAction = !string.IsNullOrEmpty(rule.Action);
            Color system = hasAction ? NeuralPalette.Body(RuleSystem(rule)) : OverridePalette.Text;
            // The guide lines. On the left the lowest category touches the page margin and the card stands further
            // right along the slant; on the right the card touches the margin and the list runs left under it.
            float guide = (listTop + PickRow - cardTop - cardHeight) * PickSlant;
            float leftIndent = 2 * PickPitch * PickSlant, rightIndent = -guide;
            float leftCard = leftX + leftIndent + guide;
            editorGuideX = new Vector2(leftCard, rightX);
            tutorialPreview = Rect("RulePreview", frame, At(leftCard, cardTop, rightX + column - leftCard, cardHeight), new Vector2(rightX + column - leftCard, cardHeight));
            OvEditorCard("PreviewCondition", "Condition", expression.Conditions, frame, At(leftCard, cardTop, column, cardHeight),
                new Vector2(column, cardHeight), () => EditPreview(0), selectingCondition, hasCondition, OverridePalette.Text, OverridePalette.Text,
                fresh ? 0 : -1, -1);
            // Until its parameter is chosen the card shows the action's name alone.
            string actionText = hasAction && !ActionChosen()
                ? ConditionalCatalog.Response(ConditionalCatalog.PickerAction(rule.Action)).Label : expression.Action;
            OvEditorCard("PreviewAction", "Action", actionText, frame, At(rightX, cardTop, column, cardHeight),
                new Vector2(column, cardHeight), () => EditPreview(-1), !selectingCondition, hasAction, system, system,
                fresh ? .05f : -1, 1);
            bool linked = ConditionChosen();
            // The arrow stands midway between the two cards.
            float between = (leftCard + column + rightX) / 2;
            var arrow = OvText("»", frame, At(between - 184, cardTop - 6, 368, cardHeight), new Vector2(368, cardHeight), 110,
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
            // The floor's top edge continues the bottom edge of the side slabs (700), which leaves it clear of Happy's box.
            const float stageTop = 280, stageHeight = 442, floorHeight = 22, floorWidth = 300, bodyX = 800 - 54;
            bodyPreview = Rect("BodyPreview", frame, At(bodyX - floorWidth / 2, stageTop, floorWidth, stageHeight), new Vector2(floorWidth, stageHeight));
            float floorTop = -stageHeight / 2 + floorHeight;
            var stage = Rect("Stage", bodyPreview, new Vector2(0, floorTop - floorHeight / 2), new Vector2(floorWidth, floorHeight)).gameObject.AddComponent<SlabGraphic>();
            stage.color = OverridePalette.Neutral; stage.Cut = 10; stage.raycastTarget = false;
            OvArrive(stage, Vector2.zero, 1);
            StandFigure(bodyPreview, new Vector2(0, floorTop + .75f * 245), stage.rectTransform);

            // Both columns exist for the tutorial frame; only the open side holds choices.
            ruleEditor = Rect("RuleEditor", frame, At(leftX, listTop, column, listHeight), new Vector2(column, listHeight));
            ruleList = Rect("RuleList", frame, At(rightX, listTop, column, listHeight), new Vector2(column, listHeight));
            pickIndent = selectingCondition ? leftIndent : rightIndent;
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

        // The category column, the frame round the open category's rows and the join between them.
        // rows: the choices of the open category. chips: whether a parameter row follows them.
        void OvPicker(RectTransform panel, string name, float side, int count, System.Func<int, string> label, System.Func<int, Color> colour,
            int open, System.Action<int> pick, bool enter, bool switched, int rows, bool chips)
        {
            for (int i = 0; i < count; i++)
            {
                int index = i;
                bool on = i == open;
                OvOption(name + i, label(i), panel, PickSlot(panel, pickIndent, i * PickPitch, PickCategory, PickRow), new Vector2(PickCategory, PickRow),
                    () => pick(index), on ? colour(i) : ChoiceFill, on ? OverridePalette.Ink : colour(i), 26, enter ? i * .03f : -1, side, ChoiceFill);
            }
            if (open < 0 || rows == 0) return;
            // The frame is a slab with the same slant as the rows inside it.
            float margin = PickPitch - PickRow, room = PickLine + PickClear;
            float frameTop = -margin, frameBottom = rows * PickPitch - margin + (chips ? PickChip + margin : 0) + margin;
            float height = frameBottom - frameTop, cut = height * PickSlant;
            float left = PickLeft + PickRow * PickSlant - room, width = PickChoice - PickRow * PickSlant + room * 2 + cut;
            var frame = Rect("Choice frame", panel, PickSlot(panel, left - cut, frameTop, width, height), new Vector2(width, height))
                .gameObject.AddComponent<SlabGraphic>();
            frame.color = Color.clear; frame.Cut = cut; frame.raycastTarget = false; frame.SetFrame(PickLine, OverridePalette.Signal);
            frame.transform.SetAsFirstSibling();
            // The join runs level from the open category to the frame; it travels along the slant when another opens.
            float joinY = open * PickPitch + PickRow / 2;
            // An open category that sits lower than the frame's last row still reaches it: the frame's left edge
            // carries on down its own slant to the height of the join.
            if (joinY > frameBottom)
            {
                float edge = PickLeft + PickRow * PickSlant - room, drop = joinY - frameBottom + PickLine / 2;
                float w = panel.sizeDelta.x, h = panel.sizeDelta.y;
                var middle = new Vector2(-w / 2 + edge - (frameBottom + drop / 2) * PickSlant + PickLine / 2, h / 2 - frameBottom - drop / 2);
                var tail = Image(panel, middle, new Vector2(drop * Mathf.Sqrt(1 + PickSlant * PickSlant), PickLine), OverridePalette.Signal);
                tail.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(1, PickSlant) * Mathf.Rad2Deg);
                tail.raycastTarget = false; tail.transform.SetAsFirstSibling();
            }
            var link = Image(panel, PickSlot(panel, pickIndent + PickCategory - 2, joinY - PickLine / 2, PickJoin + 4, PickLine), new Vector2(PickJoin + 4, PickLine), OverridePalette.Signal);
            link.raycastTarget = false; link.transform.SetAsFirstSibling();
            if (switched && !float.IsNaN(bracketLink) && !Mathf.Approximately(bracketLink, joinY))
            {
                var travel = link.gameObject.AddComponent<Arrive>();
                travel.From = new Vector2((joinY - bracketLink) * PickSlant, joinY - bracketLink); travel.Fade = false; travel.Seconds = .17f;
            }
            bracketLink = joinY;
        }

        void OvConditionStep(RectTransform panel, ConditionalRule rule, bool enter)
        {
            int selected = selectedConditionIndex < rule.ConditionCount ? rule.GetCondition(selectedConditionIndex).Id : 0;
            // Opening another category brings its choices in; picking a choice does not.
            bool switched = !enter && builtCategory >= 0 && builtCategory != conditionCategory;
            builtCategory = conditionCategory;
            var categories = new System.Collections.Generic.List<int>();
            for (int i = 0; i < ConditionalCatalog.Categories.Length; i++) if (CategoryHasSelectableCondition(i)) categories.Add(i);
            int rows = 0;
            foreach (var entry in ConditionalCatalog.Conditions) if (entry.Category == conditionCategory) rows++;
            // The parameters belong to the chosen condition and show while its category is open.
            int shown = selected != 0 && ConditionalCatalog.Condition(selected).Category == conditionCategory ? selected : 0;
            string[] parameters = shown != 0 ? ConditionalCatalog.ConditionOptions(shown) : System.Array.Empty<string>();
            OvPicker(panel, "ConditionCategory", -1, categories.Count, i => ConditionalCatalog.Categories[categories[i]], i => OverridePalette.Text,
                categories.IndexOf(conditionCategory), i => { selectingCondition = true; conditionCategory = categories[i]; Show(); },
                enter, switched, rows, parameters.Length > 0);

            int index = 0;
            foreach (var entry in ConditionalCatalog.Conditions)
            {
                if (entry.Category != conditionCategory) continue;
                int condition = entry.Id;
                var candidate = rule.Copy();
                if (condition != selected) candidate.SetCondition(selectedConditionIndex, new RuleCondition(condition));
                string reason = ConditionalCatalog.HasAnyCompatibleAction(candidate) ? null : "These conditions cannot be true together.";
                bool chosen = selected == condition;
                var choice = OvChoice(entry.Label, panel, PickSlot(panel, PickLeft, index * PickPitch, PickChoice, PickRow), new Vector2(PickChoice, PickRow),
                    () => ApplyChoice(condition, false, null), chosen ? OverridePalette.Text : ChoiceFill,
                    chosen ? OverridePalette.Ink : OverridePalette.Text, "Handler/EditorCondition" + condition, reason, NeuralBodySystem.None,
                    enter ? .06f + index * .03f : switched ? index * .02f : -1, -1, ChoiceFill);
                if (switched) choice.GetComponent<SlabButton>().EnterDistance = 40;
                index++;
            }

            bool unfold = !enter && !switched && shown != builtChipCondition;
            builtChipCondition = shown;
            OvParameterRow(panel, "ConditionParameter", parameters, rows, ConditionalCatalog.ConditionOption(rule, selectedConditionIndex),
                OverridePalette.Text, -1, SelectConditionOption, p =>
                {
                    var draft = rule.Copy();
                    ConditionalCatalog.SetConditionOption(draft, p, selectedConditionIndex);
                    return ConditionalCatalog.HasAnyCompatibleAction(draft);
                }, unfold);
        }

        // The parameter row: each chip is as wide as its word needs, and the whole row shares one type size,
        // so a long word such as On Platform does not end up smaller than its neighbours.
        void OvParameterRow(RectTransform panel, string name, string[] parameters, int rows, int current, Color colour, float side,
            System.Action<int> pick, System.Func<int, bool> allowed, bool unfold)
        {
            if (parameters.Length == 0) return;
            const float gap = 8, points = 22;
            float total = 0, room = PickChoice - (parameters.Length - 1) * gap;
            foreach (string word in parameters) total += word.Length + 3;
            float left = PickLeft, fit = 1;
            var labels = new System.Collections.Generic.List<TMP_Text>();
            for (int p = 0; p < parameters.Length; p++)
            {
                int value = p;
                float width = room * (parameters[p].Length + 3) / total;
                var button = OvOption(name + p, parameters[p], panel, PickSlot(panel, left, rows * PickPitch, width, PickChip), new Vector2(width, PickChip),
                    () => pick(value), current == p ? colour : ChoiceFill, current == p ? OverridePalette.Ink : colour, points, -1, side, ChoiceFill);
                button.interactable = allowed(p);
                var behaviour = button.GetComponent<SlabButton>();
                behaviour.Waiting = current < 0;
                var label = behaviour.Label;
                label.enableAutoSizing = false; label.fontSize = points;
                label.alignment = TextAlignmentOptions.Capline;
                float space = width - 22;
                label.rectTransform.sizeDelta = new Vector2(space, PickChip);
                fit = Mathf.Min(fit, space / Mathf.Max(1, label.GetPreferredValues(parameters[p]).x));
                labels.Add(label);
                if (unfold) Unfold(button, p);
                left += width + gap;
            }
            foreach (var label in labels) label.fontSize = Mathf.Floor(points * fit);
        }

        // A parameter row opens downward from the control it belongs to.
        static void Unfold(Button chip, int order)
        {
            var drop = chip.gameObject.AddComponent<Arrive>();
            drop.From = new Vector2(0, 16); drop.Seconds = .15f; drop.Delay = order * OvMotion.Stagger;
        }

        void OvActionStep(RectTransform panel, ConditionalRule rule, bool enter)
        {
            string picked = ConditionalCatalog.PickerAction(rule.Action);
            int pickedCategory = -1;
            if (!string.IsNullOrEmpty(picked))
                for (int i = 0; i < ActionGroups.Length; i++)
                    if (ActionGroups[i].System == NeuralPalette.System(ConditionalCatalog.Response(picked).Operation)) pickedCategory = i;
            // Coming to this side opens the category that holds the chosen action.
            if (enter && pickedCategory >= 0) actionCategory = pickedCategory;
            bool switched = !enter && builtActionCategory >= 0 && builtActionCategory != actionCategory;
            builtActionCategory = actionCategory;
            var group = ActionGroups[actionCategory];
            Color colour = NeuralPalette.Body(group.System);
            int rows = 0;
            foreach (var entry in ConditionalCatalog.Responses) if (NeuralPalette.System(entry.Operation) == group.System) rows++;
            // The parameters belong to the chosen action and show while its category is open.
            bool shown = pickedCategory == actionCategory;
            string[] parameters = shown ? ConditionalCatalog.ActionOptions(rule.Action) : System.Array.Empty<string>();
            OvPicker(panel, "ActionCategory", 1, ActionGroups.Length, i => ActionGroups[i].Label, i => NeuralPalette.Body(ActionGroups[i].System),
                actionCategory, i => { selectingCondition = false; actionCategory = i; Show(); }, enter, switched, rows, parameters.Length > 0);

            int index = 0;
            foreach (var entry in ConditionalCatalog.Responses)
            {
                if (NeuralPalette.System(entry.Operation) != group.System) continue;
                string action = entry.Id;
                bool chosen = picked == action;
                var choice = OvChoice(entry.Label, panel, PickSlot(panel, PickLeft, index * PickPitch, PickChoice, PickRow), new Vector2(PickChoice, PickRow),
                    () => ApplyChoice(0, false, action), chosen ? colour : ChoiceFill, chosen ? OverridePalette.Ink : colour,
                    "Handler/EditorAction" + action, ActionChoiceReason(rule, action), group.System,
                    enter ? .06f + index * .03f : switched ? index * .02f : -1, 1, ChoiceFill);
                if (switched) choice.GetComponent<SlabButton>().EnterDistance = 40;
                index++;
            }

            string shownAction = shown ? picked : null;
            bool unfold = !enter && !switched && shownAction != builtChipAction;
            builtChipAction = shownAction;
            OvParameterRow(panel, "ActionParameter", parameters, rows, actionOptionPending ? -1 : ConditionalCatalog.ActionOption(rule),
                colour, 1, SelectActionOption, p =>
                {
                    var draft = rule.Copy();
                    ConditionalCatalog.SetActionOption(draft, p);
                    return ActionChoiceReason(draft, draft.Action) == null;
                }, unfold);
        }
    }
}
