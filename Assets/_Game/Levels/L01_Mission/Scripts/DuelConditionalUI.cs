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
#if UNITY_EDITOR
        List<ConditionalRule> ruleSetupViewSample;
        // Layout capacity fixtures never change the loadout or gameplay budget.
        internal void SetRuleSetupViewSample(IReadOnlyList<ConditionalRule> sample)
        {
            ruleSetupViewSample=sample==null?null:new List<ConditionalRule>();
            if(sample!=null)foreach(var rule in sample)if(rule!=null)ruleSetupViewSample.Add(rule.Copy());
            completedPreview=null;completedPreviewRow=-1;
            CancelRuleEdit();
        }
#endif
        Vector2Int configurationScreen;
        RectTransform ruleEditor, ruleList, bodyPreview;
        readonly MissionBodyRegion[] neuralRegions=new MissionBodyRegion[6];
        readonly List<UnityEngine.UI.Image> equippedAccents=new List<UnityEngine.UI.Image>();
        NeuralBodySystem previewSystem, installedSystem;
        int previewRow=-1,installedRow=-1;
        float installedAt=float.NegativeInfinity;
        TMP_Text optionDescription;
        ConditionalRule completedPreview, candidatePreview;
        int completedPreviewRow=-1;
        readonly List<UnityEngine.UI.Button> previewParts=new List<UnityEngine.UI.Button>();
        readonly List<GameObject> previewDashes=new List<GameObject>();
        string selectedDescription;
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
            if(optionDescription!=null)optionDescription.text=split<0?value:value.Substring(split+1);
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
                // Preserve the three column minimums when a window becomes narrow as well as short.
                scaler.scaleFactor = Mathf.Min(Mathf.Max(Screen.height / 900f, .75f),Screen.width/1440f);
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
            candidatePreview=null;
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
                completedPreview=candidatePreview=null;completedPreviewRow=-1;
            }
            previewSystem=NeuralBodySystem.None;previewRow=-1;equippedAccents.Clear();
            SyncRuleRows();
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            float width = page.sizeDelta.x, height = page.sizeDelta.y;
            float left = -width / 2 + 32, right = width / 2 - 32;
            float top = height / 2 - 32, bottom = -height / 2 + 32;
            float editorTop = top - 172;
            float editorBottom = bottom;
            ConfigButton("ConfigureBack", "Back", page, new Vector2(left + 48, top - 22), new Vector2(96, 44), game.MainMenu);
            Text("WAVE " + game.Wave, page, new Vector2(left + 195, top - 22), new Vector2(170, 44), 24, TextAlignmentOptions.Left);
            Text("Rule Setup",page,new Vector2(0,top-22),new Vector2(360,44),30,TextAlignmentOptions.Center,headingFont);
            BuildRulePreview(left,right,top-112);
            float available=right-left-48;
            float bodyWidth=Mathf.Clamp(available*.19f,200,300);
            float selectorWidth=Mathf.Clamp((available-bodyWidth)*.54f,470,740);
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
            if (selectorRow >= 0) BuildRuleSelector();
            else
            {
                optionDescription=Text("",ruleEditor,new Vector2(0,-h/2+74),new Vector2(w,148),22,TextAlignmentOptions.TopLeft);
                selectedDescription="";SetOptionDescription(selectedDescription);
            }
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

        void BuildRulePreview(float left,float right,float y)
        {
            candidatePreview=null;previewParts.Clear();previewDashes.Clear();
            float w=right-left;
            var strip=Rect("RulePreview",page,new Vector2((left+right)/2,y),new Vector2(w,100));
            var background=strip.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=NeuralPalette.Panel;background.raycastTarget=false;
            var rule=pendingRule??completedPreview;
            var expression=ConditionalCatalog.Expression(rule);
            bool additional=expression.HasAdditional||selectingAdditional;
            float tools=pendingRule!=null?114:0;
            float addWidth=additional?0:172;
            float available=w-32-tools-addWidth-(additional?88:48);
            int count=additional?3:2;
            float partWidth=available/count,x=-w/2+16;
            PreviewPart("PreviewCondition",expression.Primary,strip,x+partWidth/2,partWidth,0,()=>EditPreview(0));x+=partWidth;
            if(additional)
            {
                Text("+",strip,new Vector2(x+20,0),new Vector2(40,44),24);x+=40;
                PreviewPart("PreviewAdditional",expression.HasAdditional?expression.Additional:"Select State",strip,x+partWidth/2,partWidth,1,()=>EditPreview(1));x+=partWidth;
            }
            Text("→",strip,new Vector2(x+24,0),new Vector2(48,44),26);x+=48;
            PreviewPart("PreviewAction",expression.Action,strip,x+partWidth/2,partWidth,2,()=>EditPreview(2));x+=partWidth;
            if(!additional)
            {
                var add=ConfigButton("AdditionalCondition","+ Condition",strip,new Vector2(x+86,0),new Vector2(156,44),()=>EditPreview(1));
                add.interactable=rule!=null&&rule.Condition!=0&&(ConditionalCatalog.ConditionOptions(rule.Condition).Length==0||ConditionalCatalog.ConditionOption(rule)>=0);
            }
            if(pendingRule!=null)
            {
                ConfigButton("CancelRuleEdit","Cancel",strip,new Vector2(w/2-57,additional?22:0),new Vector2(98,40),CancelRuleEdit);
                if(additional)ConfigButton("RemoveAdditionalCondition","Remove +",strip,new Vector2(w/2-57,-24),new Vector2(98,40),RemoveAdditionalCondition).GetComponentInChildren<TMP_Text>().fontSize=18;
            }
            UpdateRulePreview();
        }

        void PreviewPart(string name,string label,RectTransform parent,float x,float w,int part,UnityEngine.Events.UnityAction click)
        {
            var button=ConfigButton(name,label,parent,new Vector2(x,0),new Vector2(w,76),click,false,true);
            button.GetComponentInChildren<TMP_Text>().font=part==2?controlFont:font;
            button.GetComponent<DuelButtonOutline>().Subtle=true;
            button.gameObject.AddComponent<RulePreviewPart>().Part=part;
            previewParts.Add(button);
            var dashes=Rect("CandidatePreview",button.transform,new Vector2(0,-34),new Vector2(w-16,2));
            for(int i=0;i<12;i++)Image(dashes,new Vector2(-(w-16)/2+(i+.5f)*(w-16)/12,0),new Vector2((w-16)/20,2),NeuralPalette.Secondary);
            dashes.gameObject.SetActive(false);previewDashes.Add(dashes.gameObject);
        }

        void EditPreview(int part)
        {
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null)return;
#endif
            if(pendingRule==null)
            {
                if(completedPreviewRow>=0&&completedPreviewRow<displayedRules.Count)ChooseRule(completedPreviewRow,part!=2);
                else {AddRule();if(pendingRule==null)return;}
            }
            if(part==1)ChooseAdditionalCondition(pendingRow);
            else ChooseRule(pendingRow,part==0);
        }

        void UpdateRulePreview()
        {
            var expression=ConditionalCatalog.Expression(candidatePreview??pendingRule??completedPreview);
            int current=selectingAdditional?1:selectingCondition?0:2;
            for(int i=0;i<previewParts.Count;i++)
            {
                var button=previewParts[i];if(button==null)continue;
                int part=button.GetComponent<RulePreviewPart>().Part;
                button.GetComponentInChildren<TMP_Text>().text=part==0?expression.Primary:part==1?(expression.HasAdditional?expression.Additional:"Select State"):expression.Action;
                var outline=button.GetComponent<DuelButtonOutline>();outline.Persistent=pendingRule!=null&&current==part;outline.Refresh();
                previewDashes[i].SetActive(candidatePreview!=null&&current==part);
            }
        }

        NeuralBodySystem RuleSystem(ConditionalRule rule)=>NeuralPalette.System(ConditionalCatalog.Response(rule.Action).Operation);
        void PreviewRule(NeuralBodySystem system,int row=-1){previewSystem=system;previewRow=row;UpdateConfigurationSignals();}
        void RestoreFocusedPreview()
        {
            candidatePreview=null;UpdateRulePreview();SetOptionDescription(selectedDescription);
            PreviewRule(NeuralBodySystem.None);
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.activeInHierarchy&&selected.GetComponent<DuelButtonOutline>() is {KeyboardFocused:true})selected.GetComponent<ConditionalOptionDescription>()?.Inspect?.Invoke();
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
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null){displayedRules.AddRange(ruleSetupViewSample);return;}
#endif
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
            float w=ruleList.sizeDelta.x,h=ruleList.sizeDelta.y;
            Text("Rules "+displayedRules.Count,ruleList,new Vector2(0,h/2-24),new Vector2(w,48),30,TextAlignmentOptions.Left,headingFont);
            var rows=BuildRuleOptions("EquippedRules",ruleList,new Vector2(0,4),new Vector2(w,h-144));
            for(int i=0;i<displayedRules.Count;i++)
            {
                int row=i;var rule=displayedRules[i];var expression=ConditionalCatalog.Expression(rule);
                float rw=w-24;
                var line=Rect("RuleRow"+i,rows,Vector2.zero,new Vector2(rw,84));
                var background=line.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=NeuralPalette.Panel;background.raycastTarget=false;
                var number=Text((i+1).ToString(),line,Vector2.zero,Vector2.zero,20,TextAlignmentOptions.TopLeft,numericFont);
                number.name="RuleNumber";number.color=NeuralPalette.Secondary;
                var condition=Text(expression.Conditions,line,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft);
                condition.name="ConditionText";
                var arrow=Text("→",line,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);arrow.name="ActionArrow";
                var action=Text(expression.Action,line,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);action.name="ActionText";
                float height=LayoutRuleCard(line,number,condition,arrow,action,rw,52,84);
                var layout=line.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();layout.minHeight=layout.preferredHeight=height;
                bool editing=pendingRule!=null&&pendingRow==i;
                CardEditArea("Rule"+i+"Condition",condition,line,()=>ChooseRule(row,true),editing&&selectingCondition,rule,row);
                CardEditArea("Rule"+i+"Action",action,line,()=>ChooseRule(row,false),editing&&!selectingCondition,rule,row);
                var accent=Image(line,new Vector2(-rw/2+2,0),new Vector2(2,height-16),NeuralPalette.Body(RuleSystem(rule)));
                accent.name="RuleIdentity";equippedAccents.Add(accent);
                var up=ConfigButton("MoveRuleUp"+i,"▲",line,new Vector2(rw/2-24,26),new Vector2(36,24),()=>MoveRule(row,-1));
                up.GetComponentInChildren<TMP_Text>().fontSize=18;up.GetComponent<DuelButtonOutline>().Subtle=true;up.interactable=row>0;
                DescribeControl(up,"Move rule "+(row+1)+" up.");
                var down=ConfigButton("MoveRuleDown"+i,"▼",line,new Vector2(rw/2-24,0),new Vector2(36,24),()=>MoveRule(row,1));
                down.GetComponentInChildren<TMP_Text>().fontSize=18;down.GetComponent<DuelButtonOutline>().Subtle=true;down.interactable=row+1<displayedRules.Count;
                DescribeControl(down,"Move rule "+(row+1)+" down.");
                var remove=ConfigButton("RemoveRule"+i,"",line,new Vector2(rw/2-24,-26),new Vector2(36,24),()=>RemoveRule(row));
                remove.GetComponent<DuelButtonOutline>().Subtle=true;BuildTrashIcon(remove.transform);
                DescribeControl(remove,"Delete rule "+(row+1)+".");
            }
            float buttonWidth=(w-12)/2;
            var add=ConfigButton("AddRule","+ Add Rule",ruleList,new Vector2(-(buttonWidth+12)/2,-h/2+28),new Vector2(buttonWidth,48),AddRule);
            add.interactable=game.DraftLoadout.TotalRules<ConditionalCatalog.MaxRulesTotal&&pendingRule==null;
            var start=ConfigButton("StartFight",game.TrainingPending?"Enter Test Room":"Start Fight",ruleList,new Vector2((buttonWidth+12)/2,-h/2+28),new Vector2(buttonWidth,48),game.StartConfiguredEncounter);
            start.GetComponent<DuelButtonOutline>().Primary=true;start.interactable=ConfigurationReady;
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null)
            {
                // Samples exercise sizing/scrolling/focus, never commit over-budget gameplay.
                foreach(var button in ruleList.GetComponentsInChildren<UnityEngine.UI.Button>())button.onClick.RemoveAllListeners();
                add.interactable=start.interactable=false;
            }
#endif
        }

        void CardEditArea(string name,TMP_Text text,RectTransform root,UnityEngine.Events.UnityAction click,bool selected,ConditionalRule rule,int row)
        {
            var target=text.rectTransform;
            var button=ConfigButton(name,"",root,target.anchoredPosition,target.sizeDelta,click,selected);
            var empty=button.GetComponentInChildren<TMP_Text>();empty.gameObject.SetActive(false);Destroy(empty.gameObject);
            target.SetParent(button.transform,false);target.anchoredPosition=Vector2.zero;
            var outline=button.GetComponent<DuelButtonOutline>();outline.Label=text;outline.Subtle=true;outline.TextRegion=true;
            var image=button.GetComponent<UnityEngine.UI.Image>();image.color=Color.clear;
            var inspect=button.gameObject.AddComponent<ConditionalOptionDescription>();
            inspect.Inspect=()=>{PreviewRule(RuleSystem(rule),row);SetOptionDescription("Click the condition or action to edit rule "+(row+1)+".");};
            inspect.Leave=RestoreFocusedPreview;inspect.Deselect=()=>{PreviewRule(NeuralBodySystem.None);SetOptionDescription(selectedDescription);};
        }

        void DescribeControl(UnityEngine.UI.Button button,string description)
        {
            var inspect=button.gameObject.AddComponent<ConditionalOptionDescription>();
            inspect.Inspect=()=>SetOptionDescription(description);inspect.Leave=RestoreFocusedPreview;inspect.Deselect=()=>SetOptionDescription(selectedDescription);
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
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null)return;
#endif
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
            completedPreview=null;completedPreviewRow=-1;
            installedAt=float.NegativeInfinity;installedRow=-1;
            pendingRule = null;
            pendingRow = selectorRow = -1;
            if (row == displayedRules.Count) Show();
            else game.EditLoadout(d => { displayedRules.RemoveAt(row); WriteRuleOrder(d); });
        }

        void MoveRule(int row, int direction)
        {
            completedPreview=null;completedPreviewRow=-1;
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
                completedPreview=displayedRules[row].Copy();completedPreviewRow=row;
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
            {pendingRule=displayedRules[row].Copy();pendingRow=row;completedPreview=pendingRule.Copy();completedPreviewRow=row;}
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
            {pendingRule.MoveDirection=MoveDirectionOption.Unspecified;pendingRule.ShortMoveDirection=ShortMoveDirectionOption.Back;pendingRule.PlatformDirection=PlatformDirectionOption.Unspecified;}
            pendingRule.Action=action;inspectedDescription=null;
            if(ConditionalCatalog.ActionOptions(action).Length>0)Show();else CommitRuleChoice();
        }

        void CommitRuleChoice()
        {
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null)return;
#endif
            if(pendingRule==null||string.IsNullOrEmpty(pendingRule.Action)||
                !ConditionalCatalog.IsAllowed((int)ConditionalCatalog.Response(pendingRule.Action).Operation,pendingRule))return;
            int row=selectorRow;
            if(row==displayedRules.Count&&displayedRules.Count>=ConditionalCatalog.MaxRulesTotal)return;
            var complete=pendingRule.Copy();
            completedPreview=complete.Copy();completedPreviewRow=row;candidatePreview=null;
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
            int selectedCondition=selectingAdditional?rule.AdditionalCondition:rule.Condition;
            bool selectedAction=!selectingCondition&&!string.IsNullOrEmpty(rule.Action)&&(int)ConditionalCatalog.Response(rule.Action).Operation==actionCategory;
            string[] parameters=selectingCondition?selectedCondition==0?System.Array.Empty<string>():ConditionalCatalog.ConditionOptions(selectedCondition)
                :selectedAction?ConditionalCatalog.ActionOptions(rule.Action):System.Array.Empty<string>();
            bool hasParameters=parameters.Length>0;
            const float descriptionHeight=154;
            float categoryWidth=Mathf.Clamp(w*.34f,176,234);
            float optionsWidth=w-categoryWidth-20,optionsX=-w/2+categoryWidth+20+optionsWidth/2;
            float optionsTop=top-10,optionsBottom=-h/2+(hasParameters?278:descriptionHeight+26);
            selectorRowHeight=Mathf.Clamp(Mathf.Floor((optionsTop-optionsBottom-4-5*12)/6),56,64);
            var categories=BuildRuleOptions("SelectorCategories",ruleEditor,new Vector2(-w/2+categoryWidth/2,(optionsTop+optionsBottom)/2),new Vector2(categoryWidth,optionsTop-optionsBottom));
            var choices=BuildRuleOptions("RuleOptions",ruleEditor,new Vector2(optionsX,(optionsTop+optionsBottom)/2),new Vector2(optionsWidth,optionsTop-optionsBottom));
            Image(ruleEditor,new Vector2(0,-h/2+descriptionHeight+12),new Vector2(w,1),NeuralPalette.Border);
            optionDescription=Text("",ruleEditor,new Vector2(0,-h/2+descriptionHeight/2),new Vector2(w,descriptionHeight),22,TextAlignmentOptions.TopLeft);
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
                        ConditionalCatalog.ConditionDescription(condition),reason,null,candidate);
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
            selectedDescription=selectingCondition
                ?selectedCondition==0?null:ConditionalCatalog.ConditionLabel(rule,selectingAdditional)+"\n"+ConditionalCatalog.ConditionDescription(rule,selectingAdditional)
                :selectedAction?ConditionalCatalog.RuleActionLabel(rule)+"\n"+ConditionalCatalog.ResponseDescription(rule.Action):null;
            SetOptionDescription(inspectedDescription??selectedDescription);
        }

        void BuildRuleParameters(string[] options,int condition,float w,float h)
        {
            bool ammo=!selectingCondition&&ConditionalCatalog.UsesAmmoOption(pendingRule.Action);
            string label=selectingCondition?ConditionalCatalog.Condition(condition).Label:ConditionalCatalog.ActionOptionLabel(pendingRule.Action);
            Text(label,ruleEditor,new Vector2(0,-h/2+254),new Vector2(w,28),20,TextAlignmentOptions.Left);
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
                var option=ConfigButton("RuleParameter"+i,options[i],ruleEditor,new Vector2(-w/2+width/2+i*(width+8),-h/2+208),new Vector2(width,60),()=>
                {
                    if(selectingCondition){SelectConditionOption(index);return;}
                    ConditionalCatalog.SetActionOption(pendingRule,index);inspectedDescription=null;
                    if(ammo)Show();else CommitRuleChoice();
                },selected==i);
                option.interactable=allowed;
            }
            if(ammo)
            {
                var commit=ConfigButton("UseAction","Use Action",ruleEditor,new Vector2(w/2-width/2,-h/2+208),new Vector2(width,60),CommitRuleChoice);
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
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var content = Rect("Content", viewport, Vector2.zero, new Vector2(w - 20, 0));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.spacing = name=="EquippedRules"?10:12;
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

        void RuleOption(string label, RectTransform parent, UnityEngine.Events.UnityAction click, bool selected, string description, string reason = null,string action=null,ConditionalRule candidate=null)
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
                candidatePreview=candidate;
                if(action!=null&&pendingRule!=null)
                {
                    candidatePreview=pendingRule.Copy();
                    if(candidatePreview.Action!=action&&ConditionalCatalog.ActionOptions(action).Length>0&&!ConditionalCatalog.UsesAmmoOption(action))
                    {candidatePreview.MoveDirection=MoveDirectionOption.Unspecified;candidatePreview.PlatformDirection=PlatformDirectionOption.Unspecified;}
                    candidatePreview.Action=action;
                }
                UpdateRulePreview();
                PreviewRule(reason==null&&action!=null?NeuralPalette.System(ConditionalCatalog.Response(action).Operation):NeuralBodySystem.None);
            };
            inspect.Leave=RestoreFocusedPreview;
            inspect.Deselect=()=>{candidatePreview=null;UpdateRulePreview();SetOptionDescription(selectedDescription);PreviewRule(NeuralBodySystem.None);};
            if(action!=null&&reason==null)
            {
                var accent=Image(option.transform,new Vector2(-5,0),new Vector2(2,24),NeuralPalette.Operation(ConditionalCatalog.Response(action).Operation)).rectTransform;
                accent.anchorMin=accent.anchorMax=new Vector2(1,.5f);
            }
            var layout = option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = selectorRowHeight;
        }
    }

    public sealed class RulePreviewPart : MonoBehaviour { public int Part; }

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
