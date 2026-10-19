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
        bool selectingCondition, selectingAdditional;
        ConditionalRule pendingRule;
        ConditionalLoadout displayedDraft;
        // Presentation order only. Runtime priority remains the relative order within each operation.
        readonly List<ConditionalRule> displayedRules = new List<ConditionalRule>();
        Vector2Int configurationScreen;
        RectTransform ruleEditor, ruleList, bodyPreview;
        readonly MissionBodyRegion[] neuralRegions=new MissionBodyRegion[6];
        readonly List<UnityEngine.UI.Image> equippedAccents=new List<UnityEngine.UI.Image>();
        NeuralBodySystem previewSystem, installedSystem;
        int previewRow=-1,installedRow=-1;
        float installedAt=float.NegativeInfinity;
        TMP_Text optionDescription, optionDescriptionTitle;
        string inspectedDescription;
        float selectorRowHeight;
        readonly Dictionary<string,float> configurationScroll = new Dictionary<string,float>();
        string focusName, focusLabel;
        int builtSelector=-1, builtCategory=-1;
        bool builtCondition, builtAdditional;
        static readonly string[] ShortOperationLabels = { "Move", "Jump", "Aim", "Fire", "Dodge" };
        public int SelectedOperation => actionCategory;
        public bool ConfigurationReady => pendingRule == null && selectorRow < 0 && ConditionalCatalog.IsValid(game.DraftLoadout);

        string ScrollKey(string name,int row,bool condition,bool additional,int category) => name=="EquippedRules"?name:name+"/"+row+"/"+condition+"/"+additional+"/"+category;
        void CaptureConfigurationView()
        {
            focusName=focusLabel=null;
            if(page==null||game.Phase!=DuelPhase.Configure||displayedDraft!=game.DraftLoadout)
            {configurationScroll.Clear();return;}
            foreach(var scroll in page.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
                configurationScroll[ScrollKey(scroll.name,builtSelector,builtCondition,builtAdditional,builtCategory)]=scroll.verticalNormalizedPosition;
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.transform.IsChildOf(page)&&selected.GetComponent<DuelButtonOutline>() is {KeyboardFocused:true})
            {focusName=selected.name;focusLabel=selected.GetComponentInChildren<TMP_Text>()?.text;}
        }
        void RestoreConfigurationView()
        {
            builtSelector=selectorRow;builtCondition=selectingCondition;builtAdditional=selectingAdditional;builtCategory=selectingCondition?conditionCategory:actionCategory;
            Canvas.ForceUpdateCanvases();
            foreach(var scroll in page.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
                if(configurationScroll.TryGetValue(ScrollKey(scroll.name,builtSelector,builtCondition,builtAdditional,builtCategory),out float position))scroll.verticalNormalizedPosition=position;
            if(focusName==null||EventSystem.current==null)return;
            UnityEngine.UI.Button target=null,fallback=null;
            foreach(var button in page.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                if(!button.IsInteractable())continue;
                if(button.name==focusName&&button.GetComponentInChildren<TMP_Text>()?.text==focusLabel)target=button;
                if(selectorRow>=0&&button.name=="Option"&&fallback==null)fallback=button;
                if(selectorRow<0&&button.name=="AddRule")fallback=button;
                if(selectorRow<0&&button.name=="Rule0Condition"&&fallback==null)fallback=button;
            }
            target=target!=null?target:fallback;
            if(target!=null)EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        void SetOptionDescription(string value)
        {
            value=value??"";
            int split=value.IndexOf('\n');
            optionDescriptionTitle.text=split<0?"":value.Substring(0,split);
            optionDescription.text=split<0?value:value.Substring(split+1);
        }

        // Only the configuration page uses a pixel floor. Combat/menu retain their authored scale.
        void ConfigurePageScale()
        {
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            var root = canvas.GetComponent<Canvas>();
            configurationScreen = new Vector2Int(Screen.width, Screen.height);
            if (game.Phase == DuelPhase.Configure && !game.DeveloperToolsOpen)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = Mathf.Max(Screen.height / 900f, .75f);
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
            UpdateConfigurationSignals();
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
            selectingAdditional=false;
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
                installedAt=float.NegativeInfinity;installedRow=-1;
            }
            previewSystem=NeuralBodySystem.None;previewRow=-1;equippedAccents.Clear();
            SyncRuleRows();
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            float width = page.sizeDelta.x, height = page.sizeDelta.y;
            float left = -width / 2 + 32, right = width / 2 - 32;
            float top = height / 2 - 32, bottom = -height / 2 + 32;
            float editorTop = top - 76;
            float editorBottom = bottom;
            ConfigButton("ConfigureBack", "Back", page, new Vector2(left + 48, top - 22), new Vector2(96, 44), game.MainMenu);
            Text("WAVE " + game.Wave, page, new Vector2(left + 195, top - 22), new Vector2(170, 44), 24, TextAlignmentOptions.Left);
            float available=right-left-48;
            // Return the width saved by the compact rule controls to the selector.
            float bodyWidth=available*.22f,selectorWidth=available*.41f+52;
            float selectorLeft=left+bodyWidth+24,listLeft=selectorLeft+selectorWidth+24;
            bodyPreview=Rect("BodyPreview",page,new Vector2(left+bodyWidth/2,(editorTop+editorBottom)/2),new Vector2(bodyWidth,editorTop-editorBottom));
            ruleEditor = Rect("RuleEditor", page, new Vector2(selectorLeft + selectorWidth / 2, (editorTop + editorBottom) / 2),
                new Vector2(selectorWidth, editorTop - editorBottom));
            ruleList = Rect("RuleList", page, new Vector2((listLeft + right) / 2, (editorTop + editorBottom) / 2),
                new Vector2(right - listLeft, editorTop - editorBottom));
            Image(page, new Vector2(selectorLeft - 12, (editorTop + editorBottom) / 2), new Vector2(1, editorTop - editorBottom), NeuralPalette.Border);
            Image(page, new Vector2(listLeft - 12, (editorTop + editorBottom) / 2), new Vector2(1, editorTop - editorBottom), NeuralPalette.Border);
            BuildConfigurationBody();
            float w = ruleEditor.sizeDelta.x, h = ruleEditor.sizeDelta.y;
            Text(selectorRow < 0 ? "Rule Editor" : selectingAdditional ? "Optional AND" : selectingCondition ? "1 / 2   Select Condition" : "2 / 2   Select Action", ruleEditor,
                new Vector2(0, h / 2 - 24), new Vector2(w, 48), 30, TextAlignmentOptions.Left,headingFont);
            Image(ruleEditor, new Vector2(0, h / 2 - 58), new Vector2(w, 1), NeuralPalette.Border);
            if (selectorRow >= 0) BuildRuleSelector();
            BuildRuleRows();

        }

        void BuildConfigurationBody()
        {
            float bodyScale = Mathf.Min((bodyPreview.sizeDelta.x-12)/660f,(bodyPreview.sizeDelta.y-40)/680f);
            var body = Rect("ConditionalBody", bodyPreview, new Vector2(0, -10), new Vector2(810, 810));
            body.localScale = Vector3.one * bodyScale;
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1;
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
                graphic.NeuralStyle=true;graphic.NeuralRegion=i;neuralRegions[i]=graphic;
                if (i == 0)
                {
                    var brainRect = Rect("ParticleBrain", rect, Vector2.zero, Vector2.zero);
                    brainRect.anchorMin = Vector2.zero;
                    brainRect.anchorMax = Vector2.one;
                    var brain = brainRect.gameObject.AddComponent<MissionParticleBrain>();
                    brain.color = new Color32(232, 245, 255, 255);
                }
            }
            UpdateConfigurationSignals();
        }

        NeuralBodySystem RuleSystem(ConditionalRule rule)=>NeuralPalette.System(ConditionalCatalog.Response(rule.Action).Operation);
        void PreviewRule(NeuralBodySystem system,int row=-1){previewSystem=system;previewRow=row;UpdateConfigurationSignals();}
        void RestoreFocusedPreview()
        {
            PreviewRule(NeuralBodySystem.None);
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.activeInHierarchy)selected.GetComponent<ConditionalOptionDescription>()?.Inspect?.Invoke();
        }
        void UpdateConfigurationSignals()
        {
            float age=Time.unscaledTime-installedAt;
            // Keep a selected equipped row linked while its local editor is open.
            int highlightedRow=previewRow>=0?previewRow:pendingRow<displayedRules.Count?pendingRow:-1;
            var highlightedSystem=previewSystem!=NeuralBodySystem.None?previewSystem:
                highlightedRow>=0?RuleSystem(displayedRules[highlightedRow]):NeuralBodySystem.None;
            int connectedSystems=0;
            for(int i=0;i<displayedRules.Count;i++)connectedSystems|=1<<(int)RuleSystem(displayedRules[i]);
            for(int i=0;i<neuralRegions.Length;i++)
            {
                var region=neuralRegions[i];if(region==null)continue;
                var system=i==0?NeuralBodySystem.None:i==1?NeuralBodySystem.Spine:i<4?NeuralBodySystem.Arms:NeuralBodySystem.Legs;
                bool connected=system!=NeuralBodySystem.None&&(connectedSystems&(1<<(int)system))!=0;
                float signal=system!=NeuralBodySystem.None&&highlightedSystem==system?1:connected?.38f:0;
                float flow=connected&&system==installedSystem&&age<.55f?age/.55f:-1;
                if(flow>=0)signal=Mathf.Max(signal,.8f);
                if(!Mathf.Approximately(region.NeuralSignal,signal)||!Mathf.Approximately(region.NeuralFlow,flow))
                {region.NeuralSignal=signal;region.NeuralFlow=flow;region.SetVerticesDirty();}
            }
            for(int i=0;i<equippedAccents.Count;i++)if(equippedAccents[i]!=null)
            {
                float strength=i==highlightedRow?1:i==installedRow&&age<.55f?Mathf.Lerp(1,.45f,age/.55f):.45f;
                equippedAccents[i].color=NeuralPalette.Alpha(NeuralPalette.Body(RuleSystem(displayedRules[i])),strength);
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
                new Vector2(0, h/2-24), new Vector2(w,48),30,TextAlignmentOptions.Left,headingFont);
            var rows=BuildRuleOptions("EquippedRules",ruleList,new Vector2(0,18),new Vector2(w,h-168));
            for (int i = 0; i < displayedRules.Count; i++)
            {
                int row = i;
                var rule = displayedRules[i];
                float rw=w-24,fieldWidth=rw-62,fieldX=-rw/2+fieldWidth/2;
                var line=Rect("RuleRow"+i,rows,Vector2.zero,new Vector2(rw,92));
                var rowLayout=line.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                bool editing=pendingRule!=null&&pendingRow==i;
                var hover=line.gameObject.AddComponent<ConditionalOptionDescription>();
                hover.Inspect=()=>PreviewRule(RuleSystem(rule),row);hover.Leave=RestoreFocusedPreview;hover.Deselect=()=>PreviewRule(NeuralBodySystem.None);
                var fields=new List<UnityEngine.UI.Button>();
                fields.Add(ConfigButton("Rule" + i + "Condition", "IF  "+ConditionalCatalog.ConditionLabel(rule), line, Vector2.zero,
                    new Vector2(fieldWidth,44), () => ChooseRule(row, true), editing&&selectingCondition&&!selectingAdditional, true));
                if(rule.AdditionalCondition!=0)
                    fields.Add(ConfigButton("Rule"+i+"AdditionalCondition","AND  "+ConditionalCatalog.ConditionLabel(rule,true),line,Vector2.zero,
                        new Vector2(fieldWidth,44),()=>ChooseAdditionalCondition(row),editing&&selectingAdditional,true));
                fields.Add(ConfigButton("Rule" + i + "Action", "THEN  "+ConditionalCatalog.RuleActionLabel(rule),line,Vector2.zero,
                    new Vector2(fieldWidth,44), () => ChooseRule(row, false), editing&&!selectingCondition, true));
                float height=0;
                foreach(var field in fields)
                {
                    var text=field.GetComponentInChildren<TMP_Text>();
                    float fieldHeight=Mathf.Max(44,TextHeight(text,fieldWidth-28)+8);
                    ((RectTransform)field.transform).sizeDelta=new Vector2(fieldWidth,fieldHeight);
                    height+=fieldHeight+4;
                    var inspect=field.gameObject.AddComponent<ConditionalOptionDescription>();
                    inspect.Inspect=hover.Inspect;inspect.Leave=hover.Leave;inspect.Deselect=hover.Deselect;
                }
                height-=4;line.sizeDelta=new Vector2(rw,height);rowLayout.minHeight=rowLayout.preferredHeight=height;
                float y=height/2;
                foreach(var field in fields)
                {
                    var rect=(RectTransform)field.transform;
                    rect.anchoredPosition=new Vector2(fieldX,y-rect.sizeDelta.y/2);y-=rect.sizeDelta.y+4;
                }
                var accent=Image(line,new Vector2(-rw/2+1,0),new Vector2(2,height-6),NeuralPalette.Body(RuleSystem(rule)));
                accent.name="RuleIdentity";equippedAccents.Add(accent);
                var up = ConfigButton("MoveRuleUp" + i, "▲", line, new Vector2(rw/2-28,32), new Vector2(40,28), () => MoveRule(row,-1));
                up.GetComponentInChildren<TMP_Text>().fontSize=18;
                up.GetComponent<DuelButtonOutline>().Subtle=true;up.interactable=row>0;
                var down = ConfigButton("MoveRuleDown" + i, "▼", line, new Vector2(rw/2-28,0), new Vector2(40,28), () => MoveRule(row,1));
                down.GetComponentInChildren<TMP_Text>().fontSize=18;
                down.GetComponent<DuelButtonOutline>().Subtle=true;down.interactable=row+1<displayedRules.Count;
                var remove=ConfigButton("RemoveRule" + i, "", line, new Vector2(rw/2-28,-32), new Vector2(40,28), () => RemoveRule(row));
                remove.GetComponent<DuelButtonOutline>().Subtle=true;BuildTrashIcon(remove.transform);
                accent.rectTransform.SetAsLastSibling();
            }
            var add=ConfigButton("AddRule", "+ Add Rule", ruleList, new Vector2(-w/2+94,-h/2+28),new Vector2(188,48),AddRule);
            add.interactable=game.DraftLoadout.TotalRules<ConditionalCatalog.MaxRulesTotal&&pendingRule==null;
            var start=ConfigButton("StartFight", "Start Fight", ruleList, new Vector2(w/2-105,-h/2+28),new Vector2(210,48),game.StartFight);
            start.GetComponent<DuelButtonOutline>().Primary=true;start.interactable=ConfigurationReady;
        }

        void BuildTrashIcon(Transform parent)
        {
            // Geometric strokes keep the icon independent of font glyph coverage.
            var icon=Rect("TrashIcon",parent,Vector2.zero,new Vector2(20,20));
            float stroke=Mathf.Max(2,Mathf.Round(2*canvas.GetComponent<Canvas>().scaleFactor))/canvas.GetComponent<Canvas>().scaleFactor;
            var color=NeuralPalette.Secondary;
            Image(icon,new Vector2(0,5),new Vector2(18,stroke),color);
            Image(icon,new Vector2(0,8),new Vector2(6,stroke),color);
            Image(icon,new Vector2(-6,-2),new Vector2(stroke,12),color);
            Image(icon,new Vector2(6,-2),new Vector2(stroke,12),color);
            Image(icon,new Vector2(0,-8),new Vector2(12+stroke,stroke),color);
            Image(icon,new Vector2(-2,-2),new Vector2(stroke,7),color);
            Image(icon,new Vector2(2,-2),new Vector2(stroke,7),color);
        }

        static string ConditionLabel(ConditionalRule rule) => ConditionalCatalog.RuleConditionLabel(rule);

        void AddRule()
        {
            if (pendingRule != null || game.DraftLoadout.TotalRules >= ConditionalCatalog.MaxRulesTotal) return;
            // Incomplete choices stay UI-local; the combat loadout only receives a complete pair.
            pendingRule = new ConditionalRule();
            selectingAdditional=false;
            pendingRow = displayedRules.Count;
            actionCategory = 2;
            conditionCategory = 0;
            inspectedDescription = null;
            ChooseRule(pendingRow, true);
        }

        void RemoveRule(int row)
        {
            installedAt=float.NegativeInfinity;installedRow=-1;
            pendingRule = null;
            pendingRow = selectorRow = -1;
            if (row == displayedRules.Count) Show();
            else game.EditLoadout(d => { displayedRules.RemoveAt(row); WriteRuleOrder(d); });
        }

        void MoveRule(int row, int direction)
        {
            installedAt=float.NegativeInfinity;installedRow=-1;
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
            selectorRow = row;selectingAdditional=false;
            selectingCondition = condition || pendingRule.Condition == 0;
            inspectedDescription = null;
            if (selectingCondition && pendingRule.Condition != 0)
                conditionCategory = ConditionalCatalog.Condition(pendingRule.Condition).Category;
            if (!selectingCondition && !string.IsNullOrEmpty(pendingRule.Action))
                actionCategory = (int)ConditionalCatalog.Response(pendingRule.Action).Operation;
            Show();
        }

        void ChooseAdditionalCondition(int row)
        {
            if(pendingRule==null||pendingRow!=row)
            {pendingRule=displayedRules[row].Copy();pendingRow=row;}
            selectorRow=row;selectingCondition=selectingAdditional=true;inspectedDescription=null;
            conditionCategory=pendingRule.AdditionalCondition==0?0:ConditionalCatalog.Condition(pendingRule.AdditionalCondition).Category;
            Show();
        }

        void RemoveAdditionalCondition()
        {
            pendingRule.AdditionalCondition=0;ConditionalCatalog.SetConditionOption(pendingRule,-1,true);
            ReturnToAction();
        }

        void ReturnToAction()
        {
            selectingAdditional=selectingCondition=false;inspectedDescription=null;
            if(!string.IsNullOrEmpty(pendingRule.Action))actionCategory=(int)ConditionalCatalog.Response(pendingRule.Action).Operation;
            Show();
        }

        void FinishConditionChoice()
        {
            if(!string.IsNullOrEmpty(pendingRule.Action)&&ConditionalCatalog.IncompatibilityReasonForActionChoice(pendingRule,pendingRule.Action)!=null)
                pendingRule.Action=null;
            ReturnToAction();
        }

        void SelectCondition(int condition)
        {
            int previous=selectingAdditional?pendingRule.AdditionalCondition:pendingRule.Condition;
            if(selectingAdditional)pendingRule.AdditionalCondition=condition;else pendingRule.Condition=condition;
            if(previous!=condition)ConditionalCatalog.SetConditionOption(pendingRule,-1,selectingAdditional);
            inspectedDescription=null;
            if(ConditionalCatalog.ConditionOptions(condition).Length==0)FinishConditionChoice();
            else Show();
        }

        void SelectConditionOption(int option)
        {
            ConditionalCatalog.SetConditionOption(pendingRule,option,selectingAdditional);
            FinishConditionChoice();
        }

        // Retain the existing editor entry point; all choices now validate the complete draft.
        void ApplyChoice(int condition, bool below, string action)
        {
            if(action==null){SelectCondition(condition);return;}
            if(!ConditionalCatalog.ConditionsAreComplete(pendingRule)||ConditionalCatalog.IncompatibilityReasonForActionChoice(pendingRule,action)!=null)return;
            if(pendingRule.Action!=action&&ConditionalCatalog.ActionOptions(action).Length>0&&!ConditionalCatalog.UsesAmmoOption(action))
                pendingRule.MoveDirection=MoveDirectionOption.Unspecified;
            pendingRule.Action=action;inspectedDescription=null;
            if(ConditionalCatalog.ActionOptions(action).Length>0)Show();else CommitRuleChoice();
        }

        void CommitRuleChoice()
        {
            if(pendingRule==null||string.IsNullOrEmpty(pendingRule.Action)||
                !ConditionalCatalog.IsAllowed((int)ConditionalCatalog.Response(pendingRule.Action).Operation,pendingRule))return;
            int row=selectorRow;
            if(row==displayedRules.Count&&displayedRules.Count>=ConditionalCatalog.MaxRulesTotal)return;
            var complete=pendingRule.Copy();
            installedSystem=RuleSystem(complete);installedRow=row;installedAt=Time.unscaledTime;
            pendingRule=null;pendingRow=selectorRow=-1;selectingAdditional=false;
            game.EditLoadout(d=>
            {
                if(row==displayedRules.Count)displayedRules.Add(complete);else displayedRules[row]=complete;
                WriteRuleOrder(d);
            });
        }

        bool CategoryHasSelectableCondition(int category)
        {
            foreach(var entry in ConditionalCatalog.Conditions)
                if(entry.Category==category&&(!selectingAdditional||!entry.Event))return true;
            return false;
        }

        void BuildRuleSelector()
        {
            float w=ruleEditor.sizeDelta.x,h=ruleEditor.sizeDelta.y,top=h/2;
            var rule=EditingRule;
            ConfigButton("CancelRuleEdit","Cancel",ruleEditor,new Vector2(w/2-58,top-90),new Vector2(116,44),CancelRuleEdit);
            if(selectingAdditional)
                ConfigButton("SelectorBack","< Action",ruleEditor,new Vector2(-w/2+90,top-90),new Vector2(180,44),ReturnToAction);
            else if(!selectingCondition)
                ConfigButton("SelectorBack","< Condition",ruleEditor,new Vector2(-w/2+90,top-90),new Vector2(180,44),()=>ChooseRule(pendingRow,true));
            bool hasAdditional=rule.AdditionalCondition!=0;
            float andWidth=hasAdditional?w-56:156;
            var andButton=ConfigButton("AdditionalCondition",hasAdditional?"AND  "+ConditionalCatalog.ConditionLabel(rule,true):"+ AND",ruleEditor,
                new Vector2(-w/2+andWidth/2,top-142),new Vector2(andWidth,44),()=>ChooseAdditionalCondition(pendingRow),selectingAdditional,true);
            andButton.interactable=rule.Condition!=0&&(ConditionalCatalog.ConditionOptions(rule.Condition).Length==0||ConditionalCatalog.ConditionOption(rule)>=0);
            if(hasAdditional)
            {
                var remove=ConfigButton("RemoveAdditionalCondition","",ruleEditor,new Vector2(w/2-22,top-142),new Vector2(44,44),RemoveAdditionalCondition);
                BuildTrashIcon(remove.transform);
            }
            int selectedCondition=selectingAdditional?rule.AdditionalCondition:rule.Condition;
            bool selectedAction=!selectingCondition&&!string.IsNullOrEmpty(rule.Action)&&(int)ConditionalCatalog.Response(rule.Action).Operation==actionCategory;
            string[] parameters=selectingCondition?selectedCondition==0?System.Array.Empty<string>():ConditionalCatalog.ConditionOptions(selectedCondition)
                :selectedAction?ConditionalCatalog.ActionOptions(rule.Action):System.Array.Empty<string>();
            bool hasParameters=parameters.Length>0;
            const float categoryWidth=234,descriptionHeight=132;
            float optionsWidth=w-categoryWidth-20,optionsX=-w/2+categoryWidth+20+optionsWidth/2;
            float optionsTop=top-210,optionsBottom=-h/2+(hasParameters?238:descriptionHeight+26);
            selectorRowHeight=Mathf.Clamp(Mathf.Floor((optionsTop-optionsBottom-4-5*12)/6),56,64);
            Text("Category",ruleEditor,new Vector2(-w/2+categoryWidth/2,top-184),new Vector2(categoryWidth,28),20,TextAlignmentOptions.Left);
            Text("Options",ruleEditor,new Vector2(optionsX,top-184),new Vector2(optionsWidth,28),20,TextAlignmentOptions.Left);
            var categories=BuildRuleOptions("SelectorCategories",ruleEditor,new Vector2(-w/2+categoryWidth/2,(optionsTop+optionsBottom)/2),new Vector2(categoryWidth,optionsTop-optionsBottom));
            var choices=BuildRuleOptions("RuleOptions",ruleEditor,new Vector2(optionsX,(optionsTop+optionsBottom)/2),new Vector2(optionsWidth,optionsTop-optionsBottom));
            Image(ruleEditor,new Vector2(0,-h/2+descriptionHeight+12),new Vector2(w,1),NeuralPalette.Border);
            optionDescriptionTitle=Text("",ruleEditor,new Vector2(0,-h/2+descriptionHeight-15),new Vector2(w,30),22,TextAlignmentOptions.TopLeft,controlFont);
            optionDescriptionTitle.name="OptionDescriptionTitle";
            optionDescription=Text("",ruleEditor,new Vector2(0,-h/2+48),new Vector2(w,96),22,TextAlignmentOptions.TopLeft);
            optionDescription.name="OptionDescription";
            if(selectingCondition)
            {
                for(int i=0;i<ConditionalCatalog.Categories.Length;i++)
                {
                    int category=i;if(!CategoryHasSelectableCondition(category))continue;
                    CategoryOption("ConditionCategory"+i,ConditionalCatalog.Categories[i],categories,()=>
                    {conditionCategory=category;inspectedDescription=null;Show();},conditionCategory==i);
                }
                foreach(var entry in ConditionalCatalog.Conditions)
                {
                    if(entry.Category!=conditionCategory||selectingAdditional&&entry.Event)continue;
                    int condition=entry.Id;
                    var candidate=rule.Copy();
                    if(selectingAdditional)candidate.AdditionalCondition=condition;else candidate.Condition=condition;
                    if(condition!=selectedCondition)ConditionalCatalog.SetConditionOption(candidate,-1,selectingAdditional);
                    string reason=ConditionalCatalog.HasAnyCompatibleAction(candidate)?null:"These conditions cannot be true together.";
                    RuleOption(entry.Label,choices,()=>ApplyChoice(condition,false,null),selectedCondition==condition,
                        ConditionalCatalog.ConditionDescription(condition),reason);
                }
            }
            else
            {
                foreach(int op in new[]{2,3,4,0,1})
                {
                    int category=op;
                    CategoryOption("ActionCategory"+op,ShortOperationLabels[op],categories,()=>SelectActionCategory(category),actionCategory==op);
                }
                foreach(string id in ConditionalCatalog.FormalResponses[actionCategory])
                {
                    string action=id;
                    string reason=!ConditionalCatalog.ConditionsAreComplete(rule)?"Choose the remaining condition option.":ConditionalCatalog.IncompatibilityReasonForActionChoice(rule,action);
                    RuleOption(ConditionalCatalog.Response(action).Label,choices,()=>ApplyChoice(0,false,action),rule.Action==action,
                        ConditionalCatalog.ResponseDescription(action),reason,action);
                }
            }
            if(hasParameters)BuildRuleParameters(parameters,selectedCondition,w,h);
            string selectedDescription=selectingCondition
                ?selectedCondition==0?null:ConditionalCatalog.ConditionLabel(rule,selectingAdditional)+"\n"+ConditionalCatalog.ConditionDescription(rule,selectingAdditional)
                :selectedAction?ConditionalCatalog.RuleActionLabel(rule)+"\n"+ConditionalCatalog.ResponseDescription(rule.Action):null;
            SetOptionDescription(inspectedDescription??selectedDescription);
        }

        void BuildRuleParameters(string[] options,int condition,float w,float h)
        {
            bool ammo=!selectingCondition&&ConditionalCatalog.UsesAmmoOption(pendingRule.Action);
            string label=selectingCondition?ConditionalCatalog.Condition(condition).Label:ConditionalCatalog.ActionOptionLabel(pendingRule.Action);
            Text(label,ruleEditor,new Vector2(0,-h/2+212),new Vector2(w,28),20,TextAlignmentOptions.Left);
            int columns=options.Length+(ammo?1:0);
            float width=(w-(columns-1)*8)/columns;
            int selected=selectingCondition?ConditionalCatalog.ConditionOption(pendingRule,selectingAdditional):ConditionalCatalog.ActionOption(pendingRule);
            for(int i=0;i<options.Length;i++)
            {
                int index=i;bool allowed=true;
                if(selectingCondition)
                {
                    var candidate=pendingRule.Copy();ConditionalCatalog.SetConditionOption(candidate,index,selectingAdditional);
                    allowed=ConditionalCatalog.HasAnyCompatibleAction(candidate);
                }
                var option=ConfigButton("RuleParameter"+i,options[i],ruleEditor,new Vector2(-w/2+width/2+i*(width+8),-h/2+172),new Vector2(width,44),()=>
                {
                    if(selectingCondition){SelectConditionOption(index);return;}
                    ConditionalCatalog.SetActionOption(pendingRule,index);inspectedDescription=null;
                    if(ammo)Show();else CommitRuleChoice();
                },selected==i);
                option.interactable=allowed;
            }
            if(ammo)
            {
                var commit=ConfigButton("UseAction","Use Action",ruleEditor,new Vector2(w/2-width/2,-h/2+172),new Vector2(width,44),CommitRuleChoice);
                commit.interactable=ConditionalCatalog.IsAllowed((int)ConditionalCatalog.Response(pendingRule.Action).Operation,pendingRule);
            }
        }

        void CategoryOption(string name,string label,RectTransform parent,UnityEngine.Events.UnityAction click,bool selected)
        {
            var option=ConfigButton(name,label,parent,Vector2.zero,Vector2.zero,click,selected,true);
            var layout=option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight=layout.preferredHeight=selectorRowHeight;
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
            maskImage.color = NeuralPalette.Background;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, Vector2.zero, new Vector2(w - 20, 0));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = name=="EquippedRules"?16:12;
            layout.padding = new RectOffset(2,2,2,2);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var barRoot = Rect("Scrollbar", root, new Vector2(w / 2 - 4, 0), new Vector2(8, h));
            var track = barRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
            track.color = NeuralPalette.Border;
            var handle = Image(barRoot, Vector2.zero, new Vector2(8, h), NeuralPalette.Secondary);
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

        void RuleOption(string label, RectTransform parent, UnityEngine.Events.UnityAction click, bool selected, string description, string reason = null,string action=null)
        {
            var option = ConfigButton("Option", label, parent, Vector2.zero, new Vector2(parent.rect.width, selectorRowHeight), click, selected, true);
            option.interactable = reason == null;
            var inspect=option.gameObject.AddComponent<ConditionalOptionDescription>();
            inspect.Inspect=()=>
            {
                // A click can replace this page before the inspection callback is dispatched.
                if(!option.gameObject.activeInHierarchy)return;
                inspectedDescription=label+"\n"+(reason==null?description:"Unavailable: "+reason+" "+description);
                if(optionDescription!=null)SetOptionDescription(inspectedDescription);
                PreviewRule(reason==null&&action!=null?NeuralPalette.System(ConditionalCatalog.Response(action).Operation):NeuralBodySystem.None);
            };
            inspect.Leave=RestoreFocusedPreview;
            inspect.Deselect=()=>PreviewRule(NeuralBodySystem.None);
            if(action!=null&&reason==null)
            {
                var accent=Image(option.transform,new Vector2(-5,0),new Vector2(2,24),NeuralPalette.Operation(ConditionalCatalog.Response(action).Operation)).rectTransform;
                accent.anchorMin=accent.anchorMax=new Vector2(1,.5f);
            }
            var layout = option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = selectorRowHeight;
        }
    }

    // Inspect disabled choices without making them selectable. Scroll/drag still bubble to ScrollRect.
    public sealed class ConditionalOptionDescription : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerClickHandler
    {
        public System.Action Inspect,Leave,Deselect;
        public void OnPointerEnter(PointerEventData data){if(isActiveAndEnabled)Inspect?.Invoke();}
        public void OnSelect(BaseEventData data){if(isActiveAndEnabled)Inspect?.Invoke();}
        public void OnPointerClick(PointerEventData data){if(isActiveAndEnabled)Inspect?.Invoke();}
        public void OnPointerExit(PointerEventData data){if(isActiveAndEnabled)Leave?.Invoke();}
        // EventSystem still points to this object while Deselect is dispatched.
        public void OnDeselect(BaseEventData data){if(isActiveAndEnabled)Deselect?.Invoke();}
    }
}
