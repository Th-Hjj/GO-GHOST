using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        const float RulePanelWidth = 350;
        const float RuleGlowMinimumSeconds = .30f, RuleGlowFadeSeconds = .15f;
        float rulePanelWidth=RulePanelWidth;
        const float ArenaRevealSeconds = .65f, CardRevealSeconds = .3f;
        float firstInvadedSidebarAt = float.NegativeInfinity;
        static float Reveal(float age, float duration) => Mathf.SmoothStep(0, 1, age / duration);
        internal void ResetEncounterPresentation()
        {
            if (hurtFlash != null) hurtFlash.enabled = false;
            firstInvadedSidebarAt = invadedAt = bossPoolSpentAt = secondPoolAt = float.NegativeInfinity;
            invadedOrder = -1;
            injectedOrders.Clear();
        }
        sealed class RuleGlowState
        {
            public bool Matched;
            public float HoldUntil, FadeFrom=float.NegativeInfinity, Strength;

            public void Update(bool matched,float now)
            {
                if(matched&&!Matched)HoldUntil=now+RuleGlowMinimumSeconds;
                if(!matched&&Matched)FadeFrom=Mathf.Max(HoldUntil,now);
                Matched=matched;
                Strength=matched?1:Mathf.Clamp01(1-(now-FadeFrom)/RuleGlowFadeSeconds);
            }
        }
        sealed class RuleRowView
        {
            public ConditionalController Controller;
            public ConditionalRule Rule;
            public ControlOperation Operation;
            public int Row;
            public RectTransform Root;
            public UnityEngine.UI.Graphic Background;
            public Vector2 Home;
            public bool InjectedCard;
            public RectTransform Chip;
            public UnityEngine.UI.Image[] Border;
            public TMP_Text Number, Condition, Action, Arrow, Injected;
            public Material[] Signals;
            public RuleGlowState Glow;
            public float RenderedGlow=-1;
            public CanvasGroup RevealGroup;
        }
        // Pause/F8 rebuild the page: retain only timing for the same controller's rules.
        readonly Dictionary<(ControlOperation,int),RuleGlowState> ruleGlowStates = new Dictionary<(ControlOperation,int),RuleGlowState>();
        ConditionalController ruleGlowController;
        // View capacity is independent of the current five-rule gameplay budget.
        readonly List<RuleRowView> ruleRows = new List<RuleRowView>();
        ConditionalController hudRules, hudScrollController;
        RectTransform ruleSidebar, battleUi, ruleScrollRoot, ruleViewport, ruleContent, ruleScrollbarRoot;
        UnityEngine.UI.ScrollRect ruleScroll;
        TMP_Text ruleHeading, ruleCount;
        RectTransform ruleStripe;
        Vector2 ruleHudSize;
        float hudScrollPosition=1;
#if UNITY_EDITOR
        List<ConditionalRule> ruleHudViewSample;
        // Presentation-only capacity checks never install rules or read controller feedback.
        internal void SetRuleHudViewSample(IReadOnlyList<ConditionalRule> sample)
        {
            ruleHudViewSample=sample==null?null:new List<ConditionalRule>();
            if(sample!=null)foreach(var rule in sample)if(rule!=null)ruleHudViewSample.Add(rule.Copy());
            Show();
            if(ruleScroll!=null)ruleScroll.verticalNormalizedPosition=1;
        }
#endif
        void ClearRuleHud()
        {
            if(ruleScroll!=null)hudScrollPosition=ruleScroll.verticalNormalizedPosition;
            foreach(var row in ruleRows)if(row.Signals!=null)
                foreach(var signal in row.Signals)if(signal!=null)Destroy(signal);
            ruleRows.Clear();hudRules=null;ClearSealMarks();
            ruleSidebar=battleUi=ruleScrollRoot=ruleViewport=ruleContent=ruleScrollbarRoot=null;
            ruleScroll=null;ruleHeading=ruleCount=null;ruleStripe=null;ruleHudSize=Vector2.zero;
        }
        void OnDestroy()
        {ClearRuleHud();ruleGlowStates.Clear();ruleGlowController=null;DisposeCharacterFonts();if(portraitMaterial!=null)Destroy(portraitMaterial);}
        void BuildRuleHud()
        {
            bool battle=game.Phase==DuelPhase.Prepare||game.Phase==DuelPhase.Fight||game.Phase==DuelPhase.Training;
            if(!battle||ruleGlowController!=game.Rules)
            {ruleGlowStates.Clear();ruleGlowController=battle?game.Rules:null;}
            if(!battle)return;
            battleUi=Rect("BattleHUD",page,Vector2.zero,new Vector2(1600,900));
            hudRules=game.Rules;
            if (hudScrollController != hudRules)
            {
                if (!sidebarsSealed) hudScrollPosition = Time.unscaledTime-invadedAt < CardRevealSeconds ? 0 : 1;
                hudScrollController = hudRules;
            }
#if UNITY_EDITOR
            if(ruleHudViewSample!=null)
            {
                foreach(var rule in ruleHudViewSample)
                    ruleRows.Add(new RuleRowView{Rule=rule,Operation=ConditionalCatalog.Response(rule.Action).Operation,Row=-1});
            }
            else
#endif
            // Final Boss phase change: the stripped controller is shown as the frozen pre-shockwave list.
            if(sealedPlayerRules!=null)
            {
                foreach(var rule in sealedPlayerRules)
                    ruleRows.Add(new RuleRowView{Rule=rule,Operation=ConditionalCatalog.Response(rule.Action).Operation,Row=-1});
            }
            else
            if(hudRules!=null)
            {
                for(int op=0;op<ConditionalCatalog.OperationLabels.Length;op++)
                    for(int row=0;row<hudRules.RuleCount((ControlOperation)op);row++)
                        ruleRows.Add(new RuleRowView{Controller=hudRules,Rule=hudRules.RuleAt((ControlOperation)op,row),Operation=(ControlOperation)op,Row=row});
            }
            // Stable sorting matches ConditionalLoadout.OrderedRules, including unnumbered old data.
            for(int i=1;i<ruleRows.Count;i++)
            {
                var row=ruleRows[i];int j=i;
                int order=row.Rule.DisplayOrder>=0?row.Rule.DisplayOrder:int.MaxValue;
                while(j>0&&(ruleRows[j-1].Rule.DisplayOrder>=0?ruleRows[j-1].Rule.DisplayOrder:int.MaxValue)>order)
                {ruleRows[j]=ruleRows[j-1];j--;}
                ruleRows[j]=row;
            }
            if(ruleRows.Count==0)return;
            ruleSidebar=Rect("BattleRules",page,Vector2.zero,new Vector2(rulePanelWidth,900));
            SealGroup(ruleSidebar);
            var background=ruleSidebar.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color=OverridePalette.Ink;background.raycastTarget=false;
            ruleHeading=OvText("Rules",ruleSidebar,Vector2.zero,Vector2.zero,46,sidebarsSealed?OverridePalette.Disabled:OverridePalette.Text,TextAlignmentOptions.CaplineLeft);
            ruleCount=OvText(ruleRows.Count.ToString(),ruleSidebar,Vector2.zero,Vector2.zero,46,sidebarsSealed?OverridePalette.Disabled:OverridePalette.TextDim,TextAlignmentOptions.CaplineRight);
            ruleHeading.rectTransform.anchorMin=ruleHeading.rectTransform.anchorMax=new Vector2(.5f,1);
            ruleCount.rectTransform.anchorMin=ruleCount.rectTransform.anchorMax=new Vector2(.5f,1);
            ruleStripe=Rect("Rule stripe",ruleSidebar,Vector2.zero,Vector2.zero);
            ruleStripe.anchorMin=ruleStripe.anchorMax=new Vector2(.5f,1);
            var stripes=ruleStripe.gameObject.AddComponent<StripeGraphic>();
            stripes.Band=7;stripes.SecondsPerPeriod=0;stripes.raycastTarget=false;
            stripes.color=sidebarsSealed?NeuralPalette.Emp:OverridePalette.Text;stripes.Other=OverridePalette.Ink;
            ruleScrollRoot=Rect("BattleRuleScroll",ruleSidebar,Vector2.zero,Vector2.zero);
            ruleScroll=ruleScrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            ruleScroll.horizontal=false;ruleScroll.vertical=true;
            ruleScroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            ruleScroll.scrollSensitivity=36;
            ruleViewport=Rect("Viewport",ruleScrollRoot,Vector2.zero,Vector2.zero);
            var scrollSurface=ruleViewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            scrollSurface.color=Color.clear;scrollSurface.raycastTarget=true;
            ruleViewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            ruleContent=Rect("RuleList",ruleViewport,Vector2.zero,Vector2.zero);
            ruleContent.anchorMin=new Vector2(0,1);ruleContent.anchorMax=new Vector2(1,1);
            ruleContent.pivot=new Vector2(.5f,1);
            ruleScrollbarRoot=Rect("Scrollbar",ruleScrollRoot,Vector2.zero,Vector2.zero);
            var track=ruleScrollbarRoot.gameObject.AddComponent<UnityEngine.UI.Image>();track.color=OverridePalette.Neutral;
            var handle=Image(ruleScrollbarRoot,Vector2.zero,Vector2.zero,OverridePalette.TextDim);handle.raycastTarget=true;
            var bar=ruleScrollbarRoot.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;
            bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            ruleScroll.viewport=ruleViewport;ruleScroll.content=ruleContent;
            ruleScroll.verticalScrollbar=bar;
            ruleScroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            for(int i=0;i<ruleRows.Count;i++)
            {
                var row=ruleRows[i];var expression=ConditionalCatalog.Expression(row.Rule);
                if(row.Controller==null)row.Glow=new RuleGlowState();
                else
                {
                    var key=(row.Operation,row.Row);
                    if(!ruleGlowStates.TryGetValue(key,out var glow))
                        ruleGlowStates.Add(key,glow=new RuleGlowState());
                    row.Glow=glow;
                }
                BuildRuleCard(row,ruleContent,i,expression.Conditions,expression.Action,NeuralPalette.Operation(row.Operation));
                if (row.Rule.DisplayOrder == invadedOrder && Time.unscaledTime - invadedAt < CardRevealSeconds)
                {
                    row.RevealGroup = row.Root.gameObject.AddComponent<CanvasGroup>();
                    row.RevealGroup.alpha = Reveal(Time.unscaledTime - invadedAt, CardRevealSeconds);
                }
            }
            if(sealStamp!=null)sealStamp.SetAsLastSibling();
        }
        // One combat rule card: number, condition, arrow and coloured action, with the shared match glow.
        void BuildRuleCard(RuleRowView row,RectTransform parent,int i,string conditions,string action,Color actionColor)
        {
                row.Root=Rect("RuleFeedback_"+row.Operation+"_"+row.Row+"_"+i,parent,Vector2.zero,Vector2.zero);
                row.Root.anchorMin=row.Root.anchorMax=new Vector2(.5f,1);
                var slab=row.Root.gameObject.AddComponent<SlabGraphic>();
                slab.Cut=12;slab.raycastTarget=false;slab.color=OverridePalette.Surface;
                row.Background=slab;
                row.InjectedCard=injectedOrders.Contains(row.Rule.DisplayOrder);
                if(row.InjectedCard)
                {
                    row.Chip=Rect("Injected chip",row.Root,Vector2.zero,Vector2.zero);
                    var chip=row.Chip.gameObject.AddComponent<SlabGraphic>();
                    chip.Cut=5;chip.raycastTarget=false;chip.color=sidebarsSealed?OverridePalette.Neutral:NeuralPalette.Emp;
                    row.Injected=OvText("Rule Injected",row.Chip,new Vector2(0,-1),Vector2.zero,17,OverridePalette.Ink,TextAlignmentOptions.Capline,false,1);
                }
                row.Number=OvBodyAt((i+1)+" ·",row.Root,Vector2.zero,Vector2.zero,15,QuietCardText,TextAlignmentOptions.TopLeft);
                row.Number.name="RuleNumber";row.Number.textWrappingMode=TextWrappingModes.NoWrap;
                row.Condition=OvBodyAt(conditions,row.Root,Vector2.zero,Vector2.zero,15,QuietCardText,TextAlignmentOptions.TopLeft);
                row.Condition.name="ConditionText";
                row.Arrow=OvText("»",row.Root,Vector2.zero,Vector2.zero,25,actionColor,TextAlignmentOptions.TopLeft,false);
                row.Arrow.name="ActionArrow";
                row.Action=OvText(action,row.Root,Vector2.zero,Vector2.zero,25,actionColor,TextAlignmentOptions.TopLeft,false,.4f);
                row.Action.name="ActionText";row.Action.textWrappingMode=TextWrappingModes.Normal;
                if(game.InvasionHeld && row.Injected!=null && row.Rule.DisplayOrder==invadedOrder)
                {
                    if(game.InvasionPage==DuelGame.InvasionBeat.Condition)row.Condition.fontStyle=FontStyles.Bold;
                    if(game.InvasionPage==DuelGame.InvasionBeat.Action)row.Action.fontStyle=FontStyles.Bold|FontStyles.UpperCase;
                }
                if(sidebarsSealed)AddSealMark(row);
                if(game.EmpSweep != null && game.EmpSweep.Cards != null) AddEmpToCard(row, game.EmpSweep.Cards);
        }

        // Shared by setup and combat. Text comes from the structured catalog expression;
        // measurement uses the final fixed fonts, never temporary emphasis or auto-sizing.
        float LayoutRuleCard(RectTransform root,TMP_Text number,TMP_Text condition,TMP_Text arrow,TMP_Text action,
            float width,float rightInset=14,float minHeight=0,bool compact=false,TMP_Text injected=null)
        {
            float left=compact?23:48, pad=compact?9:16, gap=compact?5:8, arrowWidth=compact?18:24;
            if(compact)rightInset=7;
            float available=Mathf.Max(60,width-left-rightInset);
            condition.lineSpacing=action.lineSpacing=2;
            // Every rule keeps the same reading order, regardless of label length or view width.
            float conditionWidth=available;
            float actionWidth=available-arrowWidth;
            float conditionHeight=Mathf.Max(28,TextHeight(condition,conditionWidth));
            float actionHeight=Mathf.Max(28,TextHeight(action,actionWidth));
            float arrowHeight=Mathf.Max(28,TextHeight(arrow,arrowWidth));
            float header=injected!=null?22:0;
            float actionTop=pad+header+conditionHeight+gap;
            float height=Mathf.Max(minHeight,header+conditionHeight+gap+Mathf.Max(actionHeight,arrowHeight)+pad*2);
            root.sizeDelta=new Vector2(width,height);
            if(injected!=null)SetCardTextRect(injected,left,pad,available,header,width,height);
            SetCardTextRect(condition,left,pad+header,conditionWidth,conditionHeight,width,height);
            float arrowLeft=left;
            SetCardTextRect(arrow,arrowLeft,actionTop,arrowWidth,arrowHeight,width,height);
            SetCardTextRect(action,arrowLeft+arrowWidth,actionTop,actionWidth,actionHeight,width,height);
            if(number!=null)SetCardTextRect(number,compact?4:16,pad,compact?17:24,28,width,height);
            return height;
        }
        static void SetCardTextRect(TMP_Text text,float left,float top,float width,float height,float cardWidth,float cardHeight)
        {
            var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(width,height);
            rect.anchoredPosition=new Vector2(-cardWidth/2+left+width/2,cardHeight/2-top-height/2);
        }
        float MeasureRulePanelWidth(float availableWidth)=>Mathf.Min(availableWidth * .19f, Mathf.Clamp(availableWidth * .155f, 214, 280));
        void LayoutRuleHud(float width,float height)
        {
            float position=ruleScroll!=null&&ruleHudSize!=Vector2.zero?ruleScroll.verticalNormalizedPosition:hudScrollPosition;
            // Cards are 248 wide with a 24 margin; the viewport leaves room for the match shift.
            rulePanelWidth=296;
            const float card=248,viewportWidth=272;
            float offset=game.Phase==DuelPhase.Training?62:0;
            float listTop=126+offset,viewportHeight=Mathf.Max(80,height-listTop-20);
            ruleHeading.rectTransform.sizeDelta=new Vector2(150,50);
            ruleHeading.rectTransform.anchoredPosition=new Vector2(-card/2+75,-73-offset);
            ruleCount.rectTransform.sizeDelta=new Vector2(90,50);
            ruleCount.rectTransform.anchoredPosition=new Vector2(card/2-45,-73-offset);
            ruleStripe.sizeDelta=new Vector2(card,6);
            ruleStripe.anchoredPosition=new Vector2(0,-109-offset);
            ruleScrollRoot.sizeDelta=new Vector2(viewportWidth,viewportHeight);
            ruleScrollRoot.anchoredPosition=new Vector2(0,height/2-listTop-viewportHeight/2);
            ruleViewport.sizeDelta=new Vector2(viewportWidth,viewportHeight);ruleViewport.anchoredPosition=Vector2.zero;
            ruleScrollbarRoot.sizeDelta=new Vector2(4,viewportHeight);ruleScrollbarRoot.anchoredPosition=new Vector2(viewportWidth/2+6,0);
            float y=0;
            foreach(var row in ruleRows)
            {
                const float left=24,inner=card-44;
                float top=10;
                if(row.Chip!=null)
                {
                    float chipWidth=row.Injected.GetPreferredValues().x+24;
                    row.Chip.sizeDelta=new Vector2(chipWidth,22);
                    row.Injected.rectTransform.sizeDelta=new Vector2(chipWidth,22);
                    row.Chip.gameObject.name="Injected chip";
                    top+=27;
                }
                float numberWidth=Mathf.Ceil(row.Number.GetPreferredValues().x)+9;
                float conditionWidth=inner-numberWidth,conditionTop=top;
                float conditionHeight=Mathf.Max(20,TextHeight(row.Condition,conditionWidth));
                top+=conditionHeight+4;
                float actionTop=top,actionWidth=inner-22;
                float actionHeight=Mathf.Max(27,TextHeight(row.Action,actionWidth));
                float cardHeight=top+actionHeight+9;
                row.Root.sizeDelta=new Vector2(card,cardHeight);
                if(row.Chip!=null)
                {
                    var chip=row.Chip;chip.anchorMin=chip.anchorMax=chip.pivot=new Vector2(.5f,.5f);
                    chip.anchoredPosition=new Vector2(-card/2+left+chip.sizeDelta.x/2,cardHeight/2-10-11);
                }
                SetCardTextRect(row.Number,left,conditionTop,numberWidth,conditionHeight,card,cardHeight);
                SetCardTextRect(row.Condition,left+numberWidth,conditionTop,conditionWidth,conditionHeight,card,cardHeight);
                SetCardTextRect(row.Arrow,left,actionTop,22,actionHeight,card,cardHeight);
                SetCardTextRect(row.Action,left+22,actionTop,actionWidth,actionHeight,card,cardHeight);
                row.Home=new Vector2(0,-y-cardHeight/2);
                row.Root.anchoredPosition=row.Home;
                y+=cardHeight+8;
            }
            ruleContent.sizeDelta=new Vector2(0,Mathf.Max(0,y-6));
            ruleHudSize=new Vector2(width,height);
            Canvas.ForceUpdateCanvases();
            ruleScroll.StopMovement();ruleScroll.verticalNormalizedPosition=position;
        }

        public void UpdateBattleLayout()
        {
            if(canvas==null)return;
            float scale=canvas.GetComponent<Canvas>().scaleFactor;
            float width=Screen.width/Mathf.Max(.01f,scale),height=Screen.height/Mathf.Max(.01f,scale);
            bool hasRules=ruleSidebar!=null&&hudRules==game.Rules&&
                (game.Phase==DuelPhase.Prepare||game.Phase==DuelPhase.Fight||game.Phase==DuelPhase.Training);
            if(hasRules&&(Mathf.Abs(ruleHudSize.x-width)>.5f||Mathf.Abs(ruleHudSize.y-height)>.5f))LayoutRuleHud(width,height);
            float sidebar=hasRules?rulePanelWidth:0;
            // Reveal the player's sidebar at the opening countdown and withdraw it after the EMP.
            float visible = game.Phase == DuelPhase.Prepare && !sidebarsSealed
                ? Reveal(DuelGame.PreparationSeconds - game.PreparationRemaining, ArenaRevealSeconds)
                : 1 - SidebarFade;
            float panel=sidebar*visible*Reveal(Time.unscaledTime-firstInvadedSidebarAt,ArenaRevealSeconds);
            if(battleUi!=null)
            {
                battleUi.anchoredPosition=new Vector2(panel/2,0);
                battleUi.sizeDelta=new Vector2(width-panel,height);
            }
            if(ruleSidebar!=null)
            {
                ruleSidebar.anchoredPosition=new Vector2(-(width+sidebar)/2+panel,0);
                ruleSidebar.sizeDelta=new Vector2(sidebar,height);
            }
            var camera=Camera.main;
            if(camera==null)return;
            float left=panel/Mathf.Max(1,width);
            camera.rect=new Rect(left,0,1-left,1);
            float aspect=camera.pixelRect.width/Mathf.Max(1,camera.pixelRect.height);
            camera.orthographicSize=Mathf.Max(10,16.8f/Mathf.Max(.3f,aspect));
        }
        static void AlignRuleGlow(RuleRowView row,float scale)
        {
            // This HUD uses Screen Space Overlay. Snap only decoration, including after scrolling;
            // enabling pixelPerfect on the shared Canvas would also change text rendering.
            var rect=row.Root.rect;
            var min=row.Root.TransformPoint(rect.min);var max=row.Root.TransformPoint(rect.max);
            min=row.Root.InverseTransformPoint(new Vector3(Mathf.Round(min.x),Mathf.Round(min.y),min.z));
            max=row.Root.InverseTransformPoint(new Vector3(Mathf.Round(max.x),Mathf.Round(max.y),max.z));
            var frameRect=UnityEngine.Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            float stroke=Mathf.Max(1,Mathf.Round(scale))/scale;
            for(int i=0;i<row.Border.Length;i++)
            {
                var edge=row.Border[i].rectTransform;
                int side=i%4;float inset=(1+i/4)*stroke;
                float w=frameRect.width-2*inset,h=frameRect.height-2*inset;
                edge.sizeDelta=side<2?new Vector2(w,stroke):new Vector2(stroke,h-2*stroke);
                edge.anchoredPosition=frameRect.center+(side<2?new Vector2(0,(side==0?1:-1)*(h-stroke)/2):
                    new Vector2((side==2?-1:1)*(w-stroke)/2,0));
            }
        }
        void UpdateRuleHud()
        {
            UpdateBattleLayout();
            float scale=canvas!=null?Mathf.Max(.01f,canvas.GetComponent<Canvas>().scaleFactor):1;
            foreach(var row in ruleRows)
            {
                float arrived=1;
                if (row.RevealGroup != null)
                    row.RevealGroup.alpha = arrived = Reveal(Time.unscaledTime - invadedAt, CardRevealSeconds);
                bool live=(game.Phase==DuelPhase.Fight||game.Phase==DuelPhase.Training)&&row.Controller!=null&&row.Controller==game.Rules;
                var f=live
                    ?row.Controller.Feedback(row.Operation,row.Row).Visual:default;
                var color=NeuralPalette.Operation(row.Operation);
                if(live&&!game.Paused)row.Glow.Update(f.Matched,Time.time);
                float intrusion = InvasionFlash(row);
                float match=sidebarsSealed||!live?0:row.Glow.Strength;
                ApplyRuleGlow(row,color,match,intrusion);
                // A matched card shifts right; an injected card slams in from the left.
                row.Root.anchoredPosition=row.Home+Vector2.right*(8*match-120*(1-arrived));
                if(game.InvasionHeld && row.Rule.DisplayOrder==invadedOrder && row.Injected!=null)
                {
                    var beat=game.InvasionPage;
                    row.Action.color=row.Arrow.color=beat==DuelGame.InvasionBeat.Condition?Color.Lerp(color,EmpCard,.55f):color;
                    row.Condition.color=row.Number.color=beat==DuelGame.InvasionBeat.Action?OverridePalette.TextDim:OverridePalette.Text;
                }
            }
            UpdateSealMarks();
        }
        // A rule written by the Final Boss pulses briefly when it appears (presentation only).
        int invadedOrder=-1;float invadedAt=float.NegativeInfinity;
        readonly HashSet<int> injectedOrders=new();
        public void FlashInvadedRule(int displayOrder)
        {
            invadedOrder=displayOrder;
            injectedOrders.Add(displayOrder);
            invadedAt=Time.unscaledTime;
            if (displayOrder == 0) firstInvadedSidebarAt = Time.unscaledTime;
        }
        const float InvadedFlashSeconds=.85f;
        float InvasionFlash(RuleRowView row)
        {
            float age = Time.unscaledTime - invadedAt - CardRevealSeconds;
            if(row.Rule==null||row.Rule.DisplayOrder!=invadedOrder)return 0;
            if(game.InvasionHeld && game.InvasionPage!=DuelGame.InvasionBeat.Contact)return .8f;
            if(age<0||age>=InvadedFlashSeconds)return 0;
            return 1-Mathf.SmoothStep(0,1,age/InvadedFlashSeconds);
        }
        // A full match fills the whole card with the action's system colour and turns the text to ink.
        void ApplyRuleGlow(RuleRowView row,Color color,float strength,float intrusion)
        {
            Color fill=sidebarsSealed?SealedCard:row.InjectedCard?EmpCard:OverridePalette.Surface;
            if(intrusion>0&&!sidebarsSealed)
                fill=Color.Lerp(EmpCard,EmpCardBright,intrusion*(.5f+.5f*Mathf.Sin(Time.unscaledTime*Mathf.PI*2/1.2f)));
            row.Background.color=Color.Lerp(fill,color,strength);
            row.Number.color=row.Condition.color=Color.Lerp(QuietCardText,OverridePalette.Ink,strength);
            row.Arrow.color=row.Action.color=Color.Lerp(color,OverridePalette.Ink,strength);
        }
    }
}
