using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        int actionCategory = 2, conditionCategory;
        int selectorRow = -1, pendingRow = -1;
        bool selectingCondition;
        ConditionalRule pendingRule;
        ConditionalLoadout displayedDraft;
        // Presentation order only. Runtime priority remains the relative order within each operation.
        readonly List<ConditionalRule> displayedRules = new List<ConditionalRule>();
        Vector2Int configurationScreen;
        RectTransform ruleEditor, ruleList;
        TMP_Text optionDescription;
        string inspectedDescription;
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
                CancelRuleEdit();
            }
            return true;
        }

        void SelectActionCategory(int op)
        {
            actionCategory = op;
            inspectedDescription = null;
            Show();
        }

        void CancelRuleEdit()
        {
            pendingRule = null;
            pendingRow = selectorRow = -1;
            inspectedDescription = null;
            Show();
        }

        UnityEngine.UI.Button ConfigButton(string name, string label, Transform parent, Vector2 pos,
            Vector2 size, UnityEngine.Events.UnityAction click, bool selected = false, bool left = false)
        {
            var button = Button(label, parent, pos, size, click, selected);
            button.name = name;
            var text = button.GetComponentInChildren<TMP_Text>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
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
            float editorTop = top - 76;
            float editorBottom = bottom + 82;
            ConfigButton("ConfigureBack", "Back", page, new Vector2(left + 48, top - 22), new Vector2(96, 44), game.MainMenu);
            Text("WAVE " + game.Wave, page, new Vector2(left + 195, top - 22), new Vector2(170, 44), 24, TextAlignmentOptions.Left);
            float selectorWidth = (right - left - 32) * .58f;
            float listLeft = left + selectorWidth + 32;
            ruleEditor = Rect("RuleEditor", page, new Vector2(left + selectorWidth / 2, (editorTop + editorBottom) / 2),
                new Vector2(selectorWidth, editorTop - editorBottom));
            ruleList = Rect("RuleList", page, new Vector2((listLeft + right) / 2, (editorTop + editorBottom) / 2),
                new Vector2(right - listLeft, editorTop - editorBottom));
            Image(page, new Vector2(listLeft - 16, (editorTop + editorBottom) / 2), new Vector2(1, editorTop - editorBottom), new Color(.2f, .2f, .2f));
            BuildConfigurationBody();
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y;
            Text(selectorRow < 0 ? "Rule Editor" : selectingCondition ? "1 / 2   Select Condition" : "2 / 2   Select Action", ruleEditor,
                new Vector2(0, h / 2 - 24), new Vector2(w, 48), 30, TextAlignmentOptions.Left);
            Image(ruleEditor, new Vector2(0, h / 2 - 58), new Vector2(w, 1), new Color(.2f, .2f, .2f));
            if (selectorRow >= 0) BuildRuleSelector();
            else Text("Add a rule or click a rule field to edit.", ruleEditor, new Vector2(0, -h / 2 + 28), new Vector2(w, 56), 22);
            BuildRuleRows();

            Image(page, new Vector2((left + right) / 2, bottom + 64), new Vector2(right - left, 1), new Color(.2f, .2f, .2f));
            var start = ConfigButton("StartFight", "Start Fight", page, new Vector2(right - 105, bottom + 20), new Vector2(210, 48), game.StartFight, true);
            start.interactable = ConfigurationReady;
        }

        void BuildConfigurationBody()
        {
            float bodyScale = Mathf.Min(ruleEditor.sizeDelta.x - 80, ruleEditor.sizeDelta.y - 150) / 810f;
            var body = Rect("ConditionalBody", ruleEditor, new Vector2(0, -10), new Vector2(810, 810));
            body.localScale = Vector3.one * bodyScale;
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = selectorRow < 0 ? 1 : .08f;
            group.blocksRaycasts = false;
            group.interactable = false;
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
            displayedRules.Clear();
            displayedRules.AddRange(game.DraftLoadout.OrderedRules());
        }

        void WriteRuleOrder(ConditionalLoadout draft)
        {
            foreach (var operation in draft.Operations) operation.Rules.Clear();
            for(int i=0;i<displayedRules.Count;i++)
            {
                var rule=displayedRules[i];rule.DisplayOrder=i;
                draft.Operations[(int)ConditionalCatalog.Response(rule.Action).Operation].Rules.Add(rule);
            }
        }

        void BuildRuleRows()
        {
            float w = ruleList.sizeDelta.x, h = ruleList.sizeDelta.y;
            Text("Rules " + game.DraftLoadout.TotalRules + "/" + ConditionalCatalog.MaxRulesTotal, ruleList,
                new Vector2(0, h/2-24), new Vector2(w,48),30,TextAlignmentOptions.Left);
            var rows=BuildRuleOptions("EquippedRules",ruleList,new Vector2(0,18),new Vector2(w,h-168));
            if(displayedRules.Count==0)
            {
                var empty=Text("No rules equipped.\nAll controls are manual.",rows,Vector2.zero,Vector2.zero,22);
                empty.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=100;
            }
            for (int i = 0; i < displayedRules.Count; i++)
            {
                int row = i;
                var rule = displayedRules[i];
                var line=Rect("RuleRow"+i,rows,Vector2.zero,new Vector2(w-20,92));
                line.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=92;
                float rw=w-20,fieldWidth=rw-114,fieldX=-rw/2+fieldWidth/2;
                bool editing=pendingRule!=null&&pendingRow==i;
                ConfigButton("Rule" + i + "Condition", "IF  "+ConditionLabel(rule), line, new Vector2(fieldX,24),
                    new Vector2(fieldWidth,44), () => ChooseRule(row, true), editing&&selectingCondition, true);
                ConfigButton("Rule" + i + "Action", "THEN  "+ConditionalCatalog.Response(rule.Action).Label,
                    line, new Vector2(fieldX,-24), new Vector2(fieldWidth,44), () => ChooseRule(row, false), editing&&!selectingCondition, true);
                var up = ConfigButton("MoveRuleUp" + i, "▲", line, new Vector2(rw/2-78,24), new Vector2(44,44), () => MoveRule(row,-1));
                up.interactable=row>0;
                var down = ConfigButton("MoveRuleDown" + i, "▼", line, new Vector2(rw/2-30,24), new Vector2(44,44), () => MoveRule(row,1));
                down.interactable=row+1<displayedRules.Count;
                ConfigButton("RemoveRule" + i, "Remove", line, new Vector2(rw/2-54,-24), new Vector2(92,44), () => RemoveRule(row));
            }
            var add=ConfigButton("AddRule", "+ Add Rule", ruleList, new Vector2(-w/2+94,-h/2+28),new Vector2(188,48),AddRule);
            add.interactable=game.DraftLoadout.TotalRules<ConditionalCatalog.MaxRulesTotal&&pendingRule==null;
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
            conditionCategory = 0;
            inspectedDescription = null;
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
            inspectedDescription = null;
            if (selectingCondition && pendingRule.Condition != 0)
                conditionCategory = ConditionalCatalog.Condition(pendingRule.Condition).Category;
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
                inspectedDescription = null;
                if(!string.IsNullOrEmpty(pendingRule.Action))actionCategory=(int)ConditionalCatalog.Response(pendingRule.Action).Operation;
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
            ConfigButton("CancelRuleEdit","Cancel",ruleEditor,new Vector2(w/2-58,top-90),new Vector2(116,44),CancelRuleEdit);
            if(!selectingCondition)
                ConfigButton("SelectorBack","< Condition",ruleEditor,new Vector2(-w/2+90,top-90),new Vector2(180,44),()=>ChooseRule(pendingRow,true));
            float categoryWidth=Mathf.Clamp(w*.29f,210,250);
            float optionsWidth=w-categoryWidth-20;
            float optionsX=-w/2+categoryWidth+20+optionsWidth/2;
            float optionsTop=top-154,optionsBottom=-h/2+114;
            Text("Category",ruleEditor,new Vector2(-w/2+categoryWidth/2,top-134),new Vector2(categoryWidth,28),20,TextAlignmentOptions.Left);
            Text("Options",ruleEditor,new Vector2(optionsX,top-134),new Vector2(optionsWidth,28),20,TextAlignmentOptions.Left);
            var categories=BuildRuleOptions("SelectorCategories",ruleEditor,new Vector2(-w/2+categoryWidth/2,(optionsTop+optionsBottom)/2),new Vector2(categoryWidth,optionsTop-optionsBottom));
            var choices=BuildRuleOptions("RuleOptions",ruleEditor,new Vector2(optionsX,(optionsTop+optionsBottom)/2),new Vector2(optionsWidth,optionsTop-optionsBottom));
            Image(ruleEditor,new Vector2(0,-h/2+100),new Vector2(w,1),new Color(.2f,.2f,.2f));
            optionDescription=Text("",ruleEditor,new Vector2(0,-h/2+44),new Vector2(w,88),22,TextAlignmentOptions.TopLeft);
            optionDescription.name="OptionDescription";
            if(selectingCondition)
            {
                for(int i=0;i<ConditionalCatalog.Categories.Length;i++)
                {
                    int category=i;
                    CategoryOption("ConditionCategory"+i,ConditionalCatalog.Categories[i],categories,()=>
                    {conditionCategory=category;inspectedDescription=null;Show();},conditionCategory==i);
                }
                foreach (var entry in ConditionalCatalog.Conditions)
                {
                    if(entry.Category!=conditionCategory)continue;
                    int condition = entry.Id;
                    RuleOption(entry.Label, choices, () => ApplyChoice(condition, false, null), rule.Condition == condition,
                        ConditionalCatalog.ConditionDescription(condition));
                }
            }
            else
            {
                foreach(int op in new[]{2,3,4,0,1})
                {
                    int category=op;
                    CategoryOption("ActionCategory"+op,ShortOperationLabels[op],categories,()=>SelectActionCategory(category),actionCategory==op);
                }
                bool anyAllowed=false;
                foreach(string id in ConditionalCatalog.FormalResponses[actionCategory])
                {
                    string action=id;
                    bool allowed=ConditionalCatalog.IsAllowed(actionCategory,rule.Condition,action);
                    anyAllowed|=allowed;
                    RuleOption(ConditionalCatalog.Response(action).Label,choices,()=>ApplyChoice(0,false,action),rule.Action==action,
                        ConditionalCatalog.ResponseDescription(action),allowed?null:ConditionalCatalog.IncompatibilityReason(actionCategory,rule.Condition,action));
                }
                if(!anyAllowed)
                {
                    var unavailable=Text("No available actions in this category for this condition.",choices,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.Left);
                    unavailable.name="NoAvailableActions";
                    unavailable.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=68;
                }
            }
            string selectedDescription=selectingCondition
                ? rule.Condition==0?null:ConditionalCatalog.ConditionDescription(rule.Condition)
                : string.IsNullOrEmpty(rule.Action)?null:ConditionalCatalog.ResponseDescription(rule.Action);
            optionDescription.text=inspectedDescription??selectedDescription??"";
        }

        void CategoryOption(string name,string label,RectTransform parent,UnityEngine.Events.UnityAction click,bool selected)
        {
            var option=ConfigButton(name,label,parent,Vector2.zero,Vector2.zero,click,selected,true);
            option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=44;
        }

        RectTransform BuildRuleOptions(string name,RectTransform parent,Vector2 position,Vector2 size)
        {
            float w=size.x,h=size.y;
            var root = Rect(name, parent, position, size);
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
            layout.spacing = 8;
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

        void RuleOption(string label, RectTransform parent, UnityEngine.Events.UnityAction click, bool selected, string description, string reason = null)
        {
            var option = ConfigButton("Option", label, parent, Vector2.zero, new Vector2(parent.rect.width, 56), click, selected, true);
            option.interactable = reason == null;
            if (reason != null)
            {
                var text = option.GetComponentInChildren<TMP_Text>();
                text.color = new Color(.55f, .55f, .55f);
            }
            var inspect=option.gameObject.AddComponent<ConditionalOptionDescription>();
            inspect.Inspect=()=>
            {
                // A click can replace this page before the inspection callback is dispatched.
                if(!option.gameObject.activeInHierarchy)return;
                inspectedDescription=label+"\n"+(reason==null?description:"Unavailable: "+reason+" "+description);
                if(optionDescription!=null)optionDescription.text=inspectedDescription;
            };
            var layout = option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = 56;
        }
    }

    // Inspect disabled choices without making them selectable. Scroll/drag still bubble to ScrollRect.
    public sealed class ConditionalOptionDescription : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler
    {
        public System.Action Inspect;
        public void OnPointerEnter(PointerEventData data)=>Inspect?.Invoke();
        public void OnSelect(BaseEventData data)=>Inspect?.Invoke();
        public void OnPointerClick(PointerEventData data)=>Inspect?.Invoke();
    }
}
