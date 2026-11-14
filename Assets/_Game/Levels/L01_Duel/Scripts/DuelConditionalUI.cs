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
        RuleOptionHover hoveredRuleOption;
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
        internal bool OnPreparationOverview => !helpOpen && selectorRow < 0;

        string ScrollKey(string name) => name=="EquippedRules"?name:name+"/"+builtSelector;
        void CaptureConfigurationView()
        {
            if(page!=null&&page==helpPage)return;
            focusName=focusLabel=null;
            if(page==null||game.Phase!=DuelPhase.Configure||displayedDraft!=game.DraftLoadout)
            {configurationScroll.Clear();return;}
            foreach(var scroll in page.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
                configurationScroll[ScrollKey(scroll.name)]=scroll.verticalNormalizedPosition;
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
                else if(configurationScroll.TryGetValue(ScrollKey(scroll.name),out float position))
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
            if (game.Paused || OverlayOpen) return true;
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
            game.ConfigurationVoice?.RuleEditorClosed(false);
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
            hoveredRuleOption=null;
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
            hoveredRuleOption=null;
            SyncRuleRows();
            float scale = canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta = new Vector2(Screen.width / scale, Screen.height / scale);
            if (selectorRow < 0) { BuildOverrideOverview(); return; }
            overviewFigureRoot = null;
            BuildOverrideEditor();
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

        NeuralBodySystem RuleSystem(ConditionalRule rule)=>NeuralPalette.System(ConditionalCatalog.Response(rule.Action).Operation);
        void PreviewRule(NeuralBodySystem system,int row=-1){previewSystem=system;previewRow=row;UpdateConfigurationSignals();}
        void RestoreFocusedPreview()
        {
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
            // In the editor the character shows the part the draft action would own.
            if(previewSystem==NeuralBodySystem.None&&selectorRow>=0&&pendingRule!=null)
                highlightedSystem=string.IsNullOrEmpty(pendingRule.Action)?NeuralBodySystem.None:RuleSystem(pendingRule);
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
            UpdateOverrideFigure(connectedSystems,highlightedSystem,age);
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
            if (pendingRule != null || game.DraftLoadout.TotalRules >= ConditionalCatalog.PlayerRuleLimit
                || !game.ConfigurationVoice.CanOpenRuleEditor) return;
            // Incomplete choices stay UI-local; the combat loadout only receives a complete pair.
            pendingRule = new ConditionalRule();
            selectingAdditional=false;
            pendingRow = displayedRules.Count;
            conditionCategory = 0;
            ChooseRule(pendingRow, true);
            game.ConfigurationVoice.RuleEditorOpened();
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

        void ChooseCondition(int row,int index)
        {
            if(pendingRule==null||pendingRow!=row)
            {pendingRule=displayedRules[row].Copy();pendingRow=row;completedPreview=pendingRule.Copy();completedPreviewRow=row;}
            selectorRow=row;selectingCondition=true;selectingAdditional=index>0;selectedConditionIndex=index;
            int id=index<pendingRule.ConditionCount?pendingRule.GetCondition(index).Id:0;
            conditionCategory=id==0?0:ConditionalCatalog.Condition(id).Category;
            Show();
        }

        void SelectCondition(int condition)
        {
            selectingCondition=true;
            int previous=selectedConditionIndex<pendingRule.ConditionCount?pendingRule.GetCondition(selectedConditionIndex).Id:0;
            if(previous!=condition)pendingRule.SetCondition(selectedConditionIndex,new RuleCondition(condition));
            AdvanceEditorStep(true);
            Show();
        }

        void SelectConditionOption(int option)
        {
            selectingCondition=true;
            ConditionalCatalog.SetConditionOption(pendingRule,option,selectedConditionIndex);
            AdvanceEditorStep(true);
            Show();
        }

        // Retain the existing editor entry point; all choices now validate the complete draft.
        void ApplyChoice(int condition, bool below, string action)
        {
            if(action==null){SelectCondition(condition);return;}
            if(ActionChoiceReason(pendingRule,action)!=null)return;
            selectingCondition=false;
            SelectPickerAction(pendingRule,action);
            AdvanceEditorStep(false);
            Show();
        }

        static string ActionChoiceReason(ConditionalRule rule,string action)
        {
            return ConditionalCatalog.IncompatibilityReasonForActionChoice(rule,action);
        }

        static void SelectPickerAction(ConditionalRule rule,string action)
        {
            if(action==ActionIds.AutoAim)rule.AutoAim=AutoAimOption.Nearest;
            if(ConditionalCatalog.PickerAction(rule.Action)==action)return;
            rule.Action=action;
            switch(action)
            {
                case ActionIds.Move:rule.MoveDirection=MoveDirectionOption.Unspecified;break;
                case ActionIds.KeepDistance:rule.KeepRange=KeepRangeOption.Mid;break;
                case ActionIds.ChangePlatform:rule.PlatformDirection=PlatformDirectionOption.Unspecified;break;
                case ActionIds.Dodge:rule.DodgeDirection=DodgeDirectionOption.AwayFromThreat;break;
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
            // The rebuilt overview now owns the page-entry delay for Happy's next line.
            game.ConfigurationVoice.RuleEditorClosed(true);
        }

        internal void InstallTutorialJumpRule()
        {
            if (game.Phase != DuelPhase.Configure || game.DraftLoadout.TotalRules >= ConditionalCatalog.PlayerRuleLimit) return;
            InstallConfiguredRule(ConditionalLoadout.CreateTutorialJumpRule(), displayedRules.Count, null, keepDraft: pendingRule!=null);
        }

        void InstallConfiguredRule(ConditionalRule complete, int row, string change, bool keepDraft=false)
        {
            if(!keepDraft){completedPreview=complete.Copy();completedPreviewRow=row;}
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
            if(!keepDraft){pendingRule=null;pendingRow=selectorRow=-1;selectingAdditional=false;}
            else if(pendingRow==row){pendingRow++;selectorRow++;}
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

    }

    public sealed class RuleOptionHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal string Description;
        internal System.Action Enter, Leave;
        public void OnPointerEnter(PointerEventData data) => Enter?.Invoke();
        public void OnPointerExit(PointerEventData data) => Leave?.Invoke();
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
