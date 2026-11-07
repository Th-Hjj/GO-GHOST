using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        int conditionCategory;
        int selectorRow = -1, pendingRow = -1;
        bool selectingCondition, selectingAdditional;
        int selectedConditionIndex;
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
        const float BodyInstallSeconds=.6f;
        bool revealInstalledCard;
        CanvasGroup installingCard;
        RectTransform installingCardRect;
        Vector2[] retainedBodyPulses;
        TMP_Text ruleDescription;
        ConditionalRule completedPreview;
        int completedPreviewRow=-1;
        readonly List<UnityEngine.UI.Button> previewParts=new List<UnityEngine.UI.Button>();
        readonly List<TMP_Text> previewConditionJoins=new List<TMP_Text>();
        const float SelectorRowHeight=64;
        readonly Dictionary<string,float> configurationScroll = new Dictionary<string,float>();
        string focusName, focusLabel;
        int builtSelector=-1;
        static readonly (string Label, NeuralBodySystem System)[] ActionGroups = {
            ("Weapon",NeuralBodySystem.Arms),("Movement",NeuralBodySystem.Legs),("Dodge",NeuralBodySystem.Spine)
        };
        public int SelectedOperation => (int)(string.IsNullOrEmpty(pendingRule?.Action)
            ?ControlOperation.Aim:ConditionalCatalog.Response(pendingRule.Action).Operation);
        public bool ConfigurationReady => !helpOpen && pendingRule == null && selectorRow < 0 && ConditionalCatalog.IsValid(game.DraftLoadout);

        string ScrollKey(string name) => name=="EquippedRules"?name:name+"/"+builtSelector;
        void CaptureConfigurationView()
        {
            if(page!=null&&page==helpPage)return;
            focusName=focusLabel=null;
            if(page==null||game.Phase!=DuelPhase.Configure||displayedDraft!=game.DraftLoadout)
            {configurationScroll.Clear();return;}
            foreach(var scroll in page.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
                if(!IsDetailScroll(scroll))configurationScroll[ScrollKey(scroll.name)]=scroll.verticalNormalizedPosition;
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.transform.IsChildOf(page)&&selected.GetComponent<DuelButtonOutline>() is {KeyboardFocused:true})
            {focusName=selected.name;focusLabel=selected.GetComponentInChildren<TMP_Text>()?.text;}
        }
        void RestoreConfigurationView()
        {
            builtSelector=selectorRow;
            Canvas.ForceUpdateCanvases();
            foreach(var scroll in page.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
            {
                if (scroll.name=="EquippedRules" && installingCard!=null &&
                    Time.unscaledTime-installedAt < CardRevealSeconds && scroll.content.rect.height>scroll.viewport.rect.height)
                    scroll.verticalNormalizedPosition=0;
                else if(!IsDetailScroll(scroll)&&configurationScroll.TryGetValue(ScrollKey(scroll.name),out float position))
                    scroll.verticalNormalizedPosition=position;
            }
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

        static bool IsDetailScroll(UnityEngine.UI.ScrollRect scroll)
            => scroll.name=="RuleDescription";

        void SetDetail(string value)
        {
            var text=ruleDescription;
            if(text==null || text.text==(value??""))return;
            text.text=value??"";
            var scroll=text.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            var layout=text.GetComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight=Mathf.Max(layout.minHeight,TextHeight(text,scroll.GetComponent<RectTransform>().sizeDelta.x-24));
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.StopMovement();
            scroll.verticalNormalizedPosition=1;
        }

        string SelectedDetail(bool conditionSide)
        {
            if(pendingRule==null)return "";
            if(conditionSide)return ConditionalCatalog.ConditionDescription(pendingRule,selectedConditionIndex);
            if(string.IsNullOrEmpty(pendingRule.Action))return "";
            return ActionChoiceReason(pendingRule,pendingRule.Action)??ConditionalCatalog.ResponseDescription(pendingRule);
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
            if(helpOpen)
            {
                if(!DeveloperShortcutConsumed&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)CloseHelp();
                return true;
            }
            if (!DeveloperShortcutConsumed && selectorRow >= 0 && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelRuleEdit();
            }
            UpdateConfigurationSignals();
            return true;
        }

        void CancelRuleEdit()
        {
            pendingRule = null;
            pendingRow = selectorRow = -1;
            selectingAdditional=false;
            Show();
        }

        // A validated developer replacement starts a new setup/encounter. Do not rebuild here:
        // its lifecycle owner updates the phase and draws the final page once it is ready.
        public void DiscardRulePicker()
        {
            helpOpen=false;helpDraft=null;
            pendingRule=completedPreview=null;
            pendingRow=selectorRow=completedPreviewRow=-1;
            selectingCondition=selectingAdditional=false;
            displayedDraft=null;displayedRules.Clear();
            ruleDescription=null;
            focusName=focusLabel=null;configurationScroll.Clear();
            builtSelector=-1;
            previewRow=installedRow=-1;installedAt=float.NegativeInfinity;
            previewSystem=installedSystem=NeuralBodySystem.None;
            previewParts.Clear();equippedAccents.Clear();
            EventSystem.current?.SetSelectedGameObject(null);
        }

        UnityEngine.UI.Button ConfigButton(string name, string label, Transform parent, Vector2 pos,
            Vector2 size, UnityEngine.Events.UnityAction click, bool selected = false, bool left = false,DuelControlStyle style=DuelControlStyle.Standard)
        {
            var button = Button(label, parent, pos, size, click, selected);
            button.name = name;
            button.GetComponent<DuelButtonOutline>().Style=style;
            var text = button.GetComponentInChildren<TMP_Text>();
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.sizeDelta = Vector2.zero;
            text.fontSize = 22;
            if (left)
            {
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.margin = new Vector4(16, 4, 16, 4);
            }
            button.GetComponent<DuelButtonOutline>().Refresh();
            return button;
        }

        void BuildConditionalLoadout()
        {
            if (displayedDraft != game.DraftLoadout)
            {
                displayedDraft = game.DraftLoadout;
                displayedRules.Clear();
                pendingRule = null;
                selectorRow = -1;
                installedAt=float.NegativeInfinity;installedRow=-1;
                completedPreview=null;completedPreviewRow=-1;
            }
            previewSystem=NeuralBodySystem.None;previewRow=-1;equippedAccents.Clear();
            ruleDescription=null;
            SyncRuleRows();
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            float width = page.sizeDelta.x, height = page.sizeDelta.y;
            float left = -width / 2 + 32, right = width / 2 - 32;
            float top = height / 2 - 32, bottom = -height / 2 + 32;
            bool editing=selectorRow>=0;
            float editorTop = top - (editing?176:64);
            float editorBottom = bottom+80;
            Text("Body Configuration",page,new Vector2(0,top-22),new Vector2(360,44),30,TextAlignmentOptions.Center,headingFont);
            float available=right-left-48;
            float bodyWidth=Mathf.Clamp(available*.16f,200,256);
            float selectorLeft=left+bodyWidth+24;
            const float columnGap=48;
            float selectorWidth=(right-selectorLeft-columnGap)/2;
            float actionLeft=selectorLeft+selectorWidth+columnGap;
            float listLeft=editing?actionLeft:selectorLeft;
            if(editing)
                BuildRulePreview(selectorLeft,selectorLeft+selectorWidth,actionLeft,right,top-112);
            else
            {
                previewParts.Clear();previewConditionJoins.Clear();
            }
            bodyPreview=Rect("BodyPreview",page,new Vector2(left+bodyWidth/2,(top-64+editorBottom)/2),new Vector2(bodyWidth,top-64-editorBottom));
            ruleEditor = Rect("RuleEditor", page, new Vector2(selectorLeft + selectorWidth / 2, (editorTop + editorBottom) / 2),
                new Vector2(selectorWidth, editorTop - editorBottom));
            ruleList = Rect("RuleList", page, new Vector2((listLeft + right) / 2, (editorTop + editorBottom) / 2),
                new Vector2(right - listLeft, editorTop - editorBottom));
            ruleEditor.gameObject.SetActive(editing);
            BuildConfigurationBody();
            if (editing)
            {
                BuildRuleSelector(ruleEditor,true);
                BuildRuleSelector(ruleList,false);
                BuildSelectionDescription();
            }
            else
            {
                BuildRuleRows();
            }
            BuildRuleFooter();
        }

        void BuildConfigurationBody()
        {
            // Fit the relaxed standing silhouette by its actual bounds, without stretching anatomy.
            float bodyScale = Mathf.Min((bodyPreview.sizeDelta.x-24)/272f,bodyPreview.sizeDelta.y*.75f/680f);
            var body = Rect("ConditionalBody", bodyPreview, Vector2.zero, new Vector2(272, 680));
            body.localScale = Vector3.one * bodyScale;
            var group = body.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1;
            group.blocksRaycasts = false;
            group.interactable = false;
            Vector2[] positions = { new Vector2(0,296), new Vector2(0,140), new Vector2(-104,106), new Vector2(104,106), new Vector2(-34,-155), new Vector2(34,-155) };
            Vector2[] sizes = { new Vector2(88,88), new Vector2(140,200), new Vector2(64,260), new Vector2(64,260), new Vector2(60,370), new Vector2(60,370) };
            for (int i = 0; i < 6; i++)
            {
                var rect = Rect("BodyRegion" + i, body, positions[i], sizes[i]);
                if (i == 0)
                {
                    neuralRegions[i] = null;
                    var face = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
                    face.sprite = game.Appearance.Sprite;
                    face.color = CharacterAppearance.Tint;
                    face.raycastTarget = false;
                    continue;
                }
                var graphic = rect.gameObject.AddComponent<MissionBodyRegion>();
                graphic.Round = i == 0;
                graphic.Contour = BodyDiagramGeometry.ConfigurationContour(i);
                graphic.Monochrome = true;
                graphic.raycastTarget = false;
                graphic.Selected = false;
                graphic.NeuralStyle=true;graphic.NeuralRegion=i;neuralRegions[i]=graphic;
                graphic.EmphasizeState=true;
                graphic.PulseSpeed=150;graphic.PulseLength=170;graphic.PulseIntensity=1.1f;graphic.PulseFrequency=2;
                if (retainedBodyPulses != null) graphic.NeuralPulseState = retainedBodyPulses[i];
            }
            retainedBodyPulses = null;
            UpdateConfigurationSignals();
        }

        void BuildRulePreview(float left,float conditionRight,float actionLeft,float right,float y)
        {
            previewParts.Clear();
            float w=right-left;
            var strip=Rect("RulePreview",page,new Vector2((left+right)/2,y),new Vector2(w,100));
            var rule=pendingRule??completedPreview;
            var expression=ConditionalCatalog.Expression(rule);
            int count=Mathf.Max(rule?.ConditionCount??1,pendingRule!=null&&selectingCondition?selectedConditionIndex+1:1);
            // Condition count changes only the subdivisions, never the aligned group boundaries.
            float conditionWidth=conditionRight-left,arrowWidth=actionLeft-conditionRight,actionWidth=right-actionLeft;
            float gap=Mathf.Min(40,conditionWidth/(count*3));
            float partWidth=(conditionWidth-gap*(count-1))/count,x=-w/2;
            previewConditionJoins.Clear();
            for(int i=0;i<count;i++)
            {
                int index=i;
                if(i>0)
                {
                    var join=Text("+",strip,new Vector2(x+gap/2,0),new Vector2(gap,44),Mathf.Max(1,(int)Mathf.Min(24,gap)));
                    join.name="PreviewConditionJoin";previewConditionJoins.Add(join);x+=gap;
                }
                PreviewPart(i==0?"PreviewCondition":i==1?"PreviewAdditional":"PreviewCondition"+i,
                    ConditionalCatalog.ConditionLabel(rule,i),strip,x+partWidth/2,partWidth,i,()=>EditPreview(index));
                x+=partWidth;
            }
            Text("→",strip,new Vector2(x+arrowWidth/2,0),new Vector2(arrowWidth,44),26).name="PreviewActionArrow";x+=arrowWidth;
            PreviewPart("PreviewAction",expression.Action,strip,x+actionWidth/2,actionWidth,-1,()=>EditPreview(-1));
            UpdateRulePreview();
        }

        void PreviewPart(string name,string label,RectTransform parent,float x,float w,int part,UnityEngine.Events.UnityAction click)
        {
            var button=ConfigButton(name,label,parent,new Vector2(x,0),new Vector2(w,88),click,false,true,DuelControlStyle.Preview);
            var text=button.GetComponentInChildren<TMP_Text>();
            text.font=part<0?controlFont:font;
            float inset=Mathf.Min(16,w*.08f);
            text.margin=new Vector4(inset,28,inset,4);
            if(part<=0)
            {
                var heading=Text(part<0?"Action":"Condition",button.transform,new Vector2(0,24),
                    new Vector2(w-2*inset,24),18,TextAlignmentOptions.Left,headingFont);
                heading.color=NeuralPalette.Secondary;
            }
            if(part>=0){text.enableAutoSizing=true;text.fontSizeMin=.1f;text.fontSizeMax=text.fontSize;}
            button.GetComponent<DuelButtonOutline>().Subtle=true;
            var preview=button.gameObject.AddComponent<RulePreviewPart>();preview.Part=part;
            previewParts.Add(button);
            var controls=Rect("HoverControls",button.transform,Vector2.zero,new Vector2(w,88));
            preview.HoverControls=controls.gameObject.AddComponent<CanvasGroup>();
            preview.SetControlsVisible(false);
            var rule=pendingRule??completedPreview;
            bool hasContent=part>=0?rule!=null&&part<rule.ConditionCount&&rule.GetCondition(part).Id!=0:!string.IsNullOrEmpty(rule?.Action);
            if(hasContent)
            {
                string removeName=part==0?"RemovePrimaryCondition":part==1?"RemoveAdditionalCondition":part<0?"RemoveAction":"RemoveCondition"+part;
                float controlWidth=Mathf.Min(32,w*.7f);
                var remove=ConfigButton(removeName,"",controls,new Vector2(w/2-controlWidth/2-Mathf.Min(8,w*.1f),20),new Vector2(controlWidth,32),()=>ClearPreviewPart(part),style:DuelControlStyle.Tool);
                for(int i=0;i<2;i++)
                    Image(remove.transform,Vector2.zero,new Vector2(Mathf.Min(15,controlWidth*.6f),2),NeuralPalette.Primary).rectTransform.localRotation=Quaternion.Euler(0,0,i==0?45:-45);
            }
        }

        void EditPreview(int part)
        {
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null)return;
#endif
            if(pendingRule==null)
            {
                if(completedPreviewRow>=0&&completedPreviewRow<displayedRules.Count)ChooseRule(completedPreviewRow,part>=0);
                else {AddRule();if(pendingRule==null)return;}
            }
            if(part>=0)ChooseCondition(pendingRow,part);
            else ChooseRule(pendingRow,false);
        }

        void UpdateRulePreview()
        {
            var rule=pendingRule??completedPreview;
            var expression=ConditionalCatalog.Expression(rule);
            for(int i=0;i<previewConditionJoins.Count;i++)previewConditionJoins[i].gameObject.SetActive(rule!=null&&i+1<rule.ConditionCount&&rule.GetCondition(i).Id!=0&&rule.GetCondition(i+1).Id!=0);
            int current=selectingCondition?selectedConditionIndex:-1;
            for(int i=0;i<previewParts.Count;i++)
            {
                var button=previewParts[i];if(button==null)continue;
                int part=button.GetComponent<RulePreviewPart>().Part;
                button.GetComponentInChildren<TMP_Text>().text=part>=0?ConditionalCatalog.ConditionLabel(rule,part):expression.Action;
                var outline=button.GetComponent<DuelButtonOutline>();
                outline.Placeholder=part>=0?rule==null||part>=rule.ConditionCount||rule.GetCondition(part).Id==0:string.IsNullOrEmpty(rule?.Action);
                bool missingCondition=rule==null||rule.Condition==0;
                bool missingAction=string.IsNullOrEmpty(rule?.Action);
                outline.Persistent=pendingRule!=null&&(missingCondition!=missingAction?outline.Placeholder:current==part);
                outline.LabelSystem=part<0&&!string.IsNullOrEmpty(rule?.Action)?RuleSystem(rule):NeuralBodySystem.None;
                outline.Refresh();
            }
        }

        NeuralBodySystem RuleSystem(ConditionalRule rule)=>NeuralPalette.System(ConditionalCatalog.Response(rule.Action).Operation);
        void PreviewRule(NeuralBodySystem system,int row=-1){previewSystem=system;previewRow=row;UpdateConfigurationSignals();}
        void RestoreFocusedPreview()
        {
            SetDetail(SelectedDetail(selectingCondition));
            PreviewRule(NeuralBodySystem.None);
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.activeInHierarchy&&selected.GetComponent<DuelButtonOutline>() is {KeyboardFocused:true})selected.GetComponent<ConditionalOptionDescription>()?.Inspect?.Invoke();
        }
        void UpdateConfigurationSignals()
        {
            float elapsed=Time.unscaledTime-installedAt;
            float age=elapsed-(revealInstalledCard ? CardRevealSeconds : 0);
            if (installingCard != null)
            {
                float opacity = Reveal(elapsed, CardRevealSeconds);
                installingCard.alpha = opacity;
                installingCardRect.anchoredPosition = Vector2.right * ((1-opacity)*8);
            }
            // Keep a selected equipped row linked while its local editor is open.
            int highlightedRow=previewRow>=0?previewRow:pendingRow<displayedRules.Count?pendingRow:-1;
            var highlightedSystem=previewSystem!=NeuralBodySystem.None?previewSystem:
                highlightedRow>=0?RuleSystem(displayedRules[highlightedRow]):NeuralBodySystem.None;
            int connectedSystems=0;
            int earlierSystems=0;
            for(int i=0;i<displayedRules.Count;i++)
            {
                int bit=1<<(int)RuleSystem(displayedRules[i]);
                if (i != installedRow || !revealInstalledCard) earlierSystems |= bit;
                if (i != installedRow || age >= 0) connectedSystems |= bit;
            }
            for(int i=0;i<neuralRegions.Length;i++)
            {
                var region=neuralRegions[i];if(region==null)continue;
                var system=i==0?NeuralBodySystem.None:i==1?NeuralBodySystem.Spine:i<4?NeuralBodySystem.Arms:NeuralBodySystem.Legs;
                bool connected=system!=NeuralBodySystem.None&&(connectedSystems&(1<<(int)system))!=0;
                float signal=system!=NeuralBodySystem.None&&highlightedSystem==system?1:connected?.55f:0;
                float flow=connected&&system==installedSystem&&age>=0&&age<BodyInstallSeconds?age/BodyInstallSeconds:-1;
                if (system==installedSystem && age<0)
                    signal=(earlierSystems & (1<<(int)system))!=0 ? .55f : 0;
                if(flow>=0)
                {
                    float baseline = (earlierSystems & (1<<(int)system))!=0 ? .55f : 0;
                    signal = Mathf.Lerp(baseline, 1, Reveal(age,.12f));
                    signal = Mathf.Lerp(signal,.55f,Reveal(age-BodyInstallSeconds+.12f,.12f));
                }
                if(!Mathf.Approximately(region.NeuralSignal,signal)||!Mathf.Approximately(region.NeuralFlow,flow))
                {region.NeuralSignal=signal;region.NeuralFlow=flow;region.SetVerticesDirty();}
            }
            for(int i=0;i<equippedAccents.Count;i++)if(equippedAccents[i]!=null)
            {
                float strength=i==highlightedRow?1:i==installedRow&&age<BodyInstallSeconds?Mathf.Lerp(1,.45f,age/BodyInstallSeconds):.45f;
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
            installingCard = null;
            float w=ruleList.sizeDelta.x,h=ruleList.sizeDelta.y;
            Text("Rules "+displayedRules.Count,ruleList,new Vector2(0,h/2-20),new Vector2(w,40),28,TextAlignmentOptions.Left,headingFont);
            var rows=BuildRuleOptions("EquippedRules",ruleList,new Vector2(0,-28),new Vector2(w,h-56));
            for(int i=0;i<displayedRules.Count;i++)
            {
                int row=i;var rule=displayedRules[i];
                bool editing=pendingRule!=null&&pendingRow==i;
                // Mirror selected edits without replacing the committed rule used by Cancel and gameplay.
                var visibleRule=editing?pendingRule:rule;
                var expression=ConditionalCatalog.Expression(visibleRule);
                var actionSystem=string.IsNullOrEmpty(visibleRule.Action)?NeuralBodySystem.None:RuleSystem(visibleRule);
                float rw=w-24;
                bool entering = revealInstalledCard && i == installedRow && Time.unscaledTime-installedAt < CardRevealSeconds;
                var slot = entering ? Rect("Installing rule",rows,Vector2.zero,new Vector2(rw,84)) : null;
                var line=Rect("RuleRow"+i,entering ? slot : rows,Vector2.zero,new Vector2(rw,84));
                var background=line.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=NeuralPalette.Panel;background.raycastTarget=false;
                var number=Text((i+1).ToString(),line,Vector2.zero,Vector2.zero,20,TextAlignmentOptions.TopLeft,numericFont);
                number.name="RuleNumber";number.color=NeuralPalette.Secondary;
                var condition=Text(expression.Conditions,line,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft);
                condition.name="ConditionText";
                var arrow=Text("→",line,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);arrow.name="ActionArrow";
                var action=Text(expression.Action,line,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);action.name="ActionText";
                action.color=NeuralPalette.Body(actionSystem);
                float height=LayoutRuleCard(line,number,condition,arrow,action,rw,64,120);
                var layout=(entering ? slot : line).gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                layout.minHeight=layout.preferredHeight=height;
                if (entering)
                {
                    line.anchorMin=Vector2.zero; line.anchorMax=Vector2.one; line.sizeDelta=Vector2.zero;
                    installingCardRect=line;
                    installingCard=line.gameObject.AddComponent<CanvasGroup>();
                    float opacity=Reveal(Time.unscaledTime-installedAt,CardRevealSeconds);
                    installingCard.alpha=opacity;
                    line.anchoredPosition=Vector2.right*((1-opacity)*8);
                }
                CardEditArea("Rule"+i+"Condition",condition,line,()=>ChooseRule(row,true),editing&&selectingCondition,rule,row);
                CardEditArea("Rule"+i+"Action",action,line,()=>ChooseRule(row,false),editing&&!selectingCondition,rule,row,actionSystem);
                var accent=Image(line,new Vector2(-rw/2+2,0),new Vector2(2,height-32),NeuralPalette.Body(RuleSystem(rule)));
                accent.name="RuleIdentity";equippedAccents.Add(accent);
                var up=ConfigButton("MoveRuleUp"+i,"▲",line,new Vector2(rw/2-24,height/2-24),new Vector2(32,32),()=>MoveRule(row,-1),style:DuelControlStyle.Tool);
                up.GetComponentInChildren<TMP_Text>().fontSize=18;up.GetComponent<DuelButtonOutline>().Subtle=true;up.interactable=row>0;

                var down=ConfigButton("MoveRuleDown"+i,"▼",line,new Vector2(rw/2-24,height/2-60),new Vector2(32,32),()=>MoveRule(row,1),style:DuelControlStyle.Tool);
                down.GetComponentInChildren<TMP_Text>().fontSize=18;down.GetComponent<DuelButtonOutline>().Subtle=true;down.interactable=row+1<displayedRules.Count;

                var remove=ConfigButton("RemoveRule"+i,"",line,new Vector2(rw/2-24,height/2-96),new Vector2(32,32),()=>RemoveRule(row),style:DuelControlStyle.Tool);
                remove.GetComponent<DuelButtonOutline>().Subtle=true;BuildTrashIcon(remove.transform);

            }
        }

        void BuildRuleFooter()
        {
            float w=ruleList.sizeDelta.x,h=ruleList.sizeDelta.y;
            const float gap = 16;
            float buttonWidth=Mathf.Min(256,(w-gap)/2);
            dialogueFooterRightInset = page.sizeDelta.x / 2 - (ruleList.anchoredPosition.x + w / 2) + 2 * (buttonWidth + gap);
            dialogueFooterBottomInset = page.sizeDelta.y / 2 + ruleList.anchoredPosition.y - h / 2 - 56 - FooterButtonHeight / 2;
            bool editingRule=pendingRule!=null;
            float addX=w/2-buttonWidth/2-(editingRule?0:buttonWidth+gap);
            if(editingRule)
                ConfigButton("CancelRuleEdit","Cancel",ruleList,new Vector2(addX-buttonWidth-gap,-h/2-56),new Vector2(buttonWidth,FooterButtonHeight),CancelRuleEdit,style:DuelControlStyle.Navigation);
            UnityEngine.Events.UnityAction submit=AddRule;
            if(editingRule)submit=CommitRuleChoice;
            var add=ConfigButton(editingRule?"ConfirmRule":"AddRule",editingRule?"Confirm":"+ Add Rule",ruleList,new Vector2(addX,-h/2-56),new Vector2(buttonWidth,FooterButtonHeight),submit);
            add.interactable=editingRule?CanConfirmRule():game.DraftLoadout.TotalRules<ConditionalCatalog.PlayerRuleLimit;
            UnityEngine.UI.Button start=null;
            if(!editingRule)
            {
                start=ConfigButton("StartFight","Start Testing",ruleList,new Vector2(w/2-buttonWidth/2,-h/2-56),new Vector2(buttonWidth,FooterButtonHeight),game.StartConfiguredEncounter,style:DuelControlStyle.Primary);
                start.GetComponent<DuelButtonOutline>().Primary=true;start.interactable=ConfigurationReady;
            }
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null)
            {
                // Samples exercise sizing/scrolling/focus, never commit over-budget gameplay.
                foreach(var button in ruleList.GetComponentsInChildren<UnityEngine.UI.Button>())button.onClick.RemoveAllListeners();
                add.interactable=false;
                if(start!=null)start.interactable=false;
            }
#endif
        }

        void CardEditArea(string name,TMP_Text text,RectTransform root,UnityEngine.Events.UnityAction click,bool selected,ConditionalRule rule,int row,NeuralBodySystem labelSystem=NeuralBodySystem.None)
        {
            var target=text.rectTransform;
            var button=ConfigButton(name,"",root,target.anchoredPosition,target.sizeDelta,click,selected);
            var empty=button.GetComponentInChildren<TMP_Text>();empty.gameObject.SetActive(false);Destroy(empty.gameObject);
            target.SetParent(button.transform,false);target.anchoredPosition=Vector2.zero;
            var outline=button.GetComponent<DuelButtonOutline>();outline.Label=text;outline.Subtle=true;outline.TextRegion=true;
            outline.LabelSystem=labelSystem;outline.Refresh();
            var image=button.GetComponent<UnityEngine.UI.Image>();image.color=Color.clear;
            var inspect=button.gameObject.AddComponent<ConditionalOptionDescription>();
            inspect.Inspect=()=>{PreviewRule(RuleSystem(rule),row);};
            inspect.Leave=RestoreFocusedPreview;inspect.Deselect=()=>{PreviewRule(NeuralBodySystem.None);};
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
            if (pendingRule != null || game.DraftLoadout.TotalRules >= ConditionalCatalog.PlayerRuleLimit) return;
            // Incomplete choices stay UI-local; the combat loadout only receives a complete pair.
            pendingRule = new ConditionalRule();
            game.RuleEditorOpened();
            selectingAdditional=false;
            pendingRow = displayedRules.Count;
            conditionCategory = 0;
            ChooseRule(pendingRow, true);
        }

        void RemoveRule(int row)
        {
            completedPreview=null;completedPreviewRow=-1;
            installedAt=float.NegativeInfinity;installedRow=-1;
            pendingRule = null;
            pendingRow = selectorRow = -1;
            if (row == displayedRules.Count) Show();
            else game.EditLoadout(d => { displayedRules.RemoveAt(row); WriteRuleOrder(d); }, "remove");
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
            }, "reorder");
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
            selectorRow = row;selectingAdditional=false;selectedConditionIndex=0;
            selectingCondition = condition;
            if (pendingRule.Condition != 0)
                conditionCategory = ConditionalCatalog.Condition(pendingRule.Condition).Category;
            Show();
        }

        void ChooseAdditionalCondition(int row)=>ChooseCondition(row,1);
        void ChooseCondition(int row,int index)
        {
            if(pendingRule==null||pendingRow!=row)
            {pendingRule=displayedRules[row].Copy();pendingRow=row;completedPreview=pendingRule.Copy();completedPreviewRow=row;}
            selectorRow=row;selectingCondition=true;selectingAdditional=index>0;selectedConditionIndex=index;
            int id=index<pendingRule.ConditionCount?pendingRule.GetCondition(index).Id:0;
            conditionCategory=id==0?0:ConditionalCatalog.Condition(id).Category;
            Show();
        }

        void ClearPreviewPart(int part)
        {
            EditPreview(part);
            if(pendingRule==null)return;
            if(part>=0)
            {
                pendingRule.RemoveCondition(part);
                ChooseCondition(pendingRow,Mathf.Min(part,pendingRule.ConditionCount-1));
            }
            else
            {
                pendingRule.Action=null;
                pendingRule.MoveDirection=default;pendingRule.ShortMoveDirection=default;pendingRule.PlatformDirection=default;
                pendingRule.KeepRange=KeepRangeOption.Mid;
                pendingRule.AutoAim=default;pendingRule.DodgeDirection=default;pendingRule.Jump=default;
                ChooseRule(pendingRow,false);
            }
        }

        void SelectCondition(int condition)
        {
            selectingCondition=true;
            int previous=selectedConditionIndex<pendingRule.ConditionCount?pendingRule.GetCondition(selectedConditionIndex).Id:0;
            if(previous!=condition)pendingRule.SetCondition(selectedConditionIndex,new RuleCondition(condition));
            Show();
        }

        void SelectConditionOption(int option)
        {
            selectingCondition=true;
            ConditionalCatalog.SetConditionOption(pendingRule,option,selectedConditionIndex);
            Show();
        }

        // Retain the existing editor entry point; all choices now validate the complete draft.
        void ApplyChoice(int condition, bool below, string action)
        {
            if(action==null){SelectCondition(condition);return;}
            if(ActionChoiceReason(pendingRule,action)!=null)return;
            selectingCondition=false;
            SelectPickerAction(pendingRule,action);
            Show();
        }

        static string ActionChoiceReason(ConditionalRule rule,string action)
        {
            return ConditionalCatalog.IncompatibilityReasonForActionChoice(rule,action);
        }

        static void SelectPickerAction(ConditionalRule rule,string action)
        {
            if(action=="Q01")rule.AutoAim=AutoAimOption.Nearest;
            if(ConditionalCatalog.PickerAction(rule.Action)==action)return;
            rule.Action=action;
            switch(action)
            {
                case "M11":rule.MoveDirection=MoveDirectionOption.Unspecified;break;
                case "M03":rule.KeepRange=KeepRangeOption.Mid;break;
                case "J04":rule.PlatformDirection=PlatformDirectionOption.Unspecified;break;
                case "D01":rule.DodgeDirection=DodgeDirectionOption.AwayFromThreat;break;
            }
        }

        bool CanConfirmRule()
        {
            return pendingRule!=null&&!string.IsNullOrEmpty(pendingRule.Action)
                &&(selectorRow<displayedRules.Count||displayedRules.Count<ConditionalCatalog.PlayerRuleLimit)
                &&ConditionalCatalog.IsAllowed((int)ConditionalCatalog.Response(pendingRule.Action).Operation,pendingRule);
        }

        void CommitRuleChoice()
        {
#if UNITY_EDITOR
            if(ruleSetupViewSample!=null)return;
#endif
            if(!CanConfirmRule())return;
            int row=selectorRow;
            var complete=pendingRule.Copy();
            string change=row==displayedRules.Count?"add":"edit";
            InstallConfiguredRule(complete, row, change);
        }

        internal void InstallTutorialJumpRule()
        {
            if (game.Phase != DuelPhase.Configure || game.DraftLoadout.TotalRules >= ConditionalCatalog.PlayerRuleLimit) return;
            InstallConfiguredRule(ConditionalLoadout.CreateTutorialJumpRule(), displayedRules.Count, null);
        }

        void InstallConfiguredRule(ConditionalRule complete, int row, string change)
        {
            completedPreview=complete.Copy();completedPreviewRow=row;
            revealInstalledCard = row == displayedRules.Count;
            if (revealInstalledCard)
            {
                // The page rebuild replaces every body graphic, including systems already lit.
                int equipped = 0;
                foreach (var rule in displayedRules) equipped |= 1 << (int)RuleSystem(rule);
                retainedBodyPulses = new Vector2[neuralRegions.Length];
                for (int i = 1; i < neuralRegions.Length; i++)
                {
                    var system = i == 1 ? NeuralBodySystem.Spine : i < 4 ? NeuralBodySystem.Arms : NeuralBodySystem.Legs;
                    if (neuralRegions[i] != null && (equipped & (1 << (int)system)) != 0)
                        retainedBodyPulses[i] = neuralRegions[i].NeuralPulseState;
                }
            }
            installedSystem=RuleSystem(complete);installedRow=row;installedAt=Time.unscaledTime;
            pendingRule=null;pendingRow=selectorRow=-1;selectingAdditional=false;
            game.EditLoadout(d=>
            {
                if(row==displayedRules.Count)displayedRules.Add(complete);else displayedRules[row]=complete;
                WriteRuleOrder(d);
            }, change);
        }

        bool CategoryHasSelectableCondition(int category)
        {
            foreach(var entry in ConditionalCatalog.Conditions)
                if(entry.Category==category)return true;
            return false;
        }

        const float ActionHeadingHeight = 28, ActionGroupSpacing = 8;

        void BuildRuleSelector(RectTransform panel,bool conditionSide)
        {
            float w=panel.sizeDelta.x,h=panel.sizeDelta.y,top=h/2;
            var rule=EditingRule;
            int selectedCondition=selectedConditionIndex<rule.ConditionCount?rule.GetCondition(selectedConditionIndex).Id:0;
            bool conditionVisible=selectedCondition!=0&&ConditionalCatalog.Condition(selectedCondition).Category==conditionCategory;
            string[] parameters=conditionSide?conditionVisible?ConditionalCatalog.ConditionOptions(selectedCondition):System.Array.Empty<string>()
                :ConditionalCatalog.ActionOptions(rule.Action);
            const float parameterHeight=104;
            float dividerY=-h/2+parameterHeight;
            float optionsTop=conditionSide ? top-ActionHeadingHeight-ActionGroupSpacing : top;
            float optionsBottom=dividerY+16;
            float listY=(optionsTop+optionsBottom)/2,listHeight=optionsTop-optionsBottom;
            if(conditionSide)
            {
                float categoryWidth=Mathf.Clamp(w*.34f,176,234);
                float optionsWidth=w-categoryWidth-20;
                var categories=BuildRuleOptions("ConditionCategories",panel,new Vector2((categoryWidth-w)/2,listY),new Vector2(categoryWidth,listHeight),true);
                var choices=BuildRuleOptions("ConditionOptions"+conditionCategory,panel,new Vector2((w-optionsWidth)/2,listY),new Vector2(optionsWidth,listHeight),true);
                for(int i=0;i<ConditionalCatalog.Categories.Length;i++)
                {
                    int category=i;if(!CategoryHasSelectableCondition(category))continue;
                    CategoryOption("ConditionCategory"+i,ConditionalCatalog.Categories[i],categories,()=>
                    {selectingCondition=true;conditionCategory=category;Show();},conditionCategory==i);
                }
                foreach(var entry in ConditionalCatalog.Conditions)
                {
                    if(entry.Category!=conditionCategory)continue;
                    int condition=entry.Id;
                    var candidate=rule.Copy();
                    if(condition!=selectedCondition)candidate.SetCondition(selectedConditionIndex,new RuleCondition(condition));
                    string reason=ConditionalCatalog.HasAnyCompatibleAction(candidate)?null:"These conditions cannot be true together.";
                    RuleOption(entry.Label,choices,()=>ApplyChoice(condition,false,null),selectedCondition==condition,
                        ConditionalCatalog.ConditionDescription(condition),reason);
                }
            }
            else
            {
                var choices=BuildRuleOptions("ActionOptions",panel,new Vector2(0,listY),new Vector2(w,listHeight),true);
                BuildGroupedActions(choices,w,rule);
            }
            Image(panel,new Vector2(0,dividerY),new Vector2(w,1),NeuralPalette.Border);
            if(parameters.Length>0)BuildRuleParameters(panel,conditionSide,parameters,selectedCondition,w,h);
        }

        void BuildGroupedActions(RectTransform choices,float width,ConditionalRule rule)
        {
            const float gap=12,rowHeight=56;
            choices.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=ActionGroupSpacing;
            foreach(var group in ActionGroups)
            {
                var heading=Text(group.Label,choices,Vector2.zero,Vector2.zero,19,TextAlignmentOptions.Left,headingFont);
                heading.color=NeuralPalette.Secondary;
                var headingLayout=heading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                headingLayout.minHeight=headingLayout.preferredHeight=ActionHeadingHeight;
                var cards=Rect(group.Label+"Cards",choices,Vector2.zero,Vector2.zero);
                var grid=cards.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount=2;
                grid.cellSize=new Vector2((width-gap)/2,rowHeight);
                grid.spacing=new Vector2(gap,8);
                foreach(var entry in ConditionalCatalog.Responses)
                {
                    if(NeuralPalette.System(entry.Operation)!=group.System)continue;
                    string action=entry.Id;
                    RuleOption(entry.Label,cards,()=>ApplyChoice(0,false,action),ConditionalCatalog.PickerAction(rule.Action)==action,
                        ConditionalCatalog.PickerDescription(action),ActionChoiceReason(rule,action),action);
                }
            }
        }

        void BuildSelectionDescription()
        {
            float w=ruleEditor.sizeDelta.x,bottom=-ruleEditor.sizeDelta.y/2;
            var content=BuildRuleOptions("RuleDescription",ruleEditor,new Vector2(0,bottom-40),new Vector2(w,64));
            ruleDescription=Text("",content,Vector2.zero,Vector2.zero,21,TextAlignmentOptions.TopLeft);
            var layout=ruleDescription.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight=layout.preferredHeight=60;
            SetDetail(SelectedDetail(selectingCondition));
        }

        void BuildRuleParameters(RectTransform panel,bool conditionSide,string[] options,int condition,float w,float h)
        {
            string label=conditionSide?ConditionalCatalog.Condition(condition).Label:ConditionalCatalog.ActionOptionLabel(pendingRule.Action);
            Text(label,panel,new Vector2(0,-h/2+78),new Vector2(w,28),20,TextAlignmentOptions.Left);
            int columns=options.Length;
            float width=(w-(columns-1)*8)/columns;
            int selected=conditionSide?ConditionalCatalog.ConditionOption(pendingRule,selectedConditionIndex):ConditionalCatalog.ActionOption(pendingRule);
            for(int i=0;i<options.Length;i++)
            {
                int index=i;bool allowed;
                var candidate=pendingRule.Copy();
                string description;
                if(conditionSide)
                {
                    ConditionalCatalog.SetConditionOption(candidate,index,selectedConditionIndex);
                    allowed=ConditionalCatalog.HasAnyCompatibleAction(candidate);
                    description=ConditionalCatalog.ConditionDescription(candidate,selectedConditionIndex);
                }
                else
                {
                    ConditionalCatalog.SetActionOption(candidate,index);
                    string reason=ActionChoiceReason(candidate,candidate.Action);
                    allowed=reason==null;
                    description=reason??ConditionalCatalog.ResponseDescription(candidate);
                }
                var option=ConfigButton((conditionSide?"ConditionParameter":"ActionParameter")+i,options[i],panel,new Vector2(-w/2+width/2+i*(width+8),-h/2+34),new Vector2(width,56),()=>
                {
                    if(conditionSide){SelectConditionOption(index);return;}
                    selectingCondition=false;
                    ConditionalCatalog.SetActionOption(pendingRule,index);
                    Show();
                },selected==i,style:DuelControlStyle.Option);
                option.interactable=allowed;
                BindOptionDescription(option,description);
            }
        }

        void CategoryOption(string name,string label,RectTransform parent,UnityEngine.Events.UnityAction click,bool selected)
        {
            var option=ConfigButton(name,label,parent,Vector2.zero,Vector2.zero,click,selected,true,DuelControlStyle.Navigation);
            var layout=option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight=layout.preferredHeight=SelectorRowHeight;
        }

        RectTransform BuildRuleOptions(string name,RectTransform parent,Vector2 position,Vector2 size,bool alignCards=false)
        {
            float w=size.x,h=size.y;
            var root = Rect(name, parent, position, size);
            var scroll = root.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            var viewport = Rect("Viewport", root, new Vector2(alignCards?0:-8,0), new Vector2(alignCards?w:w-20,h));
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
            layout.spacing = alignCards?20:name=="EquippedRules"?16:8;
            layout.padding = new RectOffset(alignCards?0:2,alignCards?0:2,2,2);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var barRoot = Rect("Scrollbar", root, new Vector2(w/2+(alignCards?6:-4),0), new Vector2(8,h));
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
            var option = ConfigButton("Option", label, parent, Vector2.zero, new Vector2(parent.rect.width, SelectorRowHeight), click, selected, true,DuelControlStyle.Option);
            option.interactable = reason == null;
            var outline=option.GetComponent<DuelButtonOutline>();
            outline.LabelSystem=action!=null?NeuralPalette.System(ConditionalCatalog.Response(action).Operation):NeuralBodySystem.None;
            outline.Refresh();
            BindOptionDescription(option,reason??description);
            if(action!=null&&reason==null)
            {
                var accent=Image(option.transform,new Vector2(-5,0),new Vector2(2,24),NeuralPalette.Operation(ConditionalCatalog.Response(action).Operation)).rectTransform;
                accent.anchorMin=accent.anchorMax=new Vector2(1,.5f);
            }
            var layout = option.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight = layout.preferredHeight = SelectorRowHeight;
        }

        void BindOptionDescription(UnityEngine.UI.Button option,string description)
        {
            var inspect=option.gameObject.AddComponent<ConditionalOptionDescription>();
            inspect.Inspect=()=>{if(option.gameObject.activeInHierarchy)SetDetail(description);};
            inspect.Leave=RestoreFocusedPreview;
            inspect.Deselect=()=>SetDetail(SelectedDetail(selectingCondition));
        }
    }

    public sealed class RulePreviewPart : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public int Part;
        public CanvasGroup HoverControls;
        public void SetControlsVisible(bool visible)
        {
            if(HoverControls==null)return;
            HoverControls.alpha=visible?1:0;
            HoverControls.interactable=HoverControls.blocksRaycasts=visible;
        }
        public void OnPointerEnter(PointerEventData data)=>SetControlsVisible(true);
        public void OnPointerExit(PointerEventData data)=>SetControlsVisible(false);
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
