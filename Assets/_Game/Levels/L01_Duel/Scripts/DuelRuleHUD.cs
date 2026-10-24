using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        const float RulePanelWidth = 350;
        float rulePanelWidth=RulePanelWidth;
        sealed class RuleRowView
        {
            public ConditionalController Controller;
            public ConditionalRule Rule;
            public ControlOperation Operation;
            public int Row;
            public RectTransform Root;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Image[] Border;
            public TMP_Text Number, Condition, Action, Arrow;
            public Material[] Signals;
            public bool Matched;
        }
        // View capacity is independent of the current five-rule gameplay budget.
        readonly List<RuleRowView> ruleRows = new List<RuleRowView>();
        ConditionalController hudRules, hudScrollController;
        RectTransform ruleSidebar, battleUi, ruleScrollRoot, ruleViewport, ruleContent, ruleScrollbarRoot;
        UnityEngine.UI.ScrollRect ruleScroll;
        TMP_Text ruleHeading;
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
            ruleRows.Clear();hudRules=null;
            ruleSidebar=battleUi=ruleScrollRoot=ruleViewport=ruleContent=ruleScrollbarRoot=null;
            ruleScroll=null;ruleHeading=null;ruleHudSize=Vector2.zero;
        }
        void OnDestroy()=>ClearRuleHud();
        void BuildRuleHud()
        {
            if(game.Phase!=DuelPhase.Prepare&&game.Phase!=DuelPhase.Fight&&game.Phase!=DuelPhase.Training)return;
            battleUi=Rect("BattleHUD",page,Vector2.zero,new Vector2(1600,900));
            hudRules=game.Rules;
            if(hudScrollController!=hudRules){hudScrollPosition=1;hudScrollController=hudRules;}
#if UNITY_EDITOR
            if(ruleHudViewSample!=null)
            {
                foreach(var rule in ruleHudViewSample)
                    ruleRows.Add(new RuleRowView{Rule=rule,Operation=ConditionalCatalog.Response(rule.Action).Operation,Row=-1});
            }
            else
#endif
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
            var background=ruleSidebar.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color=NeuralPalette.Background;background.raycastTarget=false;
            var divider=Image(ruleSidebar,Vector2.zero,Vector2.zero,NeuralPalette.Border);
            divider.rectTransform.anchorMin=new Vector2(0,0);divider.rectTransform.anchorMax=new Vector2(0,1);
            divider.rectTransform.sizeDelta=new Vector2(1,0);
            ruleHeading=Text("Rules "+ruleRows.Count,ruleSidebar,Vector2.zero,Vector2.zero,30,TextAlignmentOptions.Left,headingFont);
            ruleHeading.rectTransform.anchorMin=ruleHeading.rectTransform.anchorMax=new Vector2(.5f,1);
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
            var track=ruleScrollbarRoot.gameObject.AddComponent<UnityEngine.UI.Image>();track.color=NeuralPalette.Border;
            var handle=Image(ruleScrollbarRoot,Vector2.zero,Vector2.zero,NeuralPalette.Secondary);handle.raycastTarget=true;
            var bar=ruleScrollbarRoot.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();
            bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;
            bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            ruleScroll.viewport=ruleViewport;ruleScroll.content=ruleContent;
            ruleScroll.verticalScrollbar=bar;
            ruleScroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            for(int i=0;i<ruleRows.Count;i++)
            {
                var row=ruleRows[i];var expression=ConditionalCatalog.Expression(row.Rule);
                row.Root=Rect("RuleFeedback_"+row.Operation+"_"+row.Row+"_"+i,ruleContent,Vector2.zero,Vector2.zero);
                row.Root.anchorMin=row.Root.anchorMax=new Vector2(.5f,1);
                row.Background=row.Root.gameObject.AddComponent<UnityEngine.UI.Image>();
                row.Background.raycastTarget=false;row.Background.color=NeuralPalette.Panel;
                row.Number=Text((i+1).ToString(),row.Root,Vector2.zero,Vector2.zero,20,TextAlignmentOptions.TopLeft,numericFont);
                row.Number.name="RuleNumber";row.Number.color=NeuralPalette.Secondary;
                row.Condition=Text(expression.Conditions,row.Root,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft);
                row.Condition.name="ConditionText";
                row.Arrow=Text("→",row.Root,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);
                row.Arrow.name="ActionArrow";
                row.Action=Text(expression.Action,row.Root,Vector2.zero,Vector2.zero,22,TextAlignmentOptions.TopLeft,controlFont);
                row.Action.name="ActionText";
                row.Action.color=NeuralPalette.Operation(row.Operation);
                // Preserve each font atlas and fixed geometry; the entire card shares one match signal.
                var texts=new[]{row.Number,row.Condition,row.Arrow,row.Action};
                row.Signals=new Material[texts.Length];
                for(int t=0;t<texts.Length;t++)
                {
                    var signal=new Material(texts[t].fontSharedMaterial){name="Rule match signal",hideFlags=HideFlags.DontSave};
                    signal.EnableKeyword("GLOW_ON");
                    signal.SetFloat(ShaderUtilities.ID_GlowOffset,0);
                    signal.SetFloat(ShaderUtilities.ID_GlowOuter,.12f);
                    signal.SetFloat(ShaderUtilities.ID_GlowInner,.02f);
                    signal.SetColor(ShaderUtilities.ID_GlowColor,Color.clear);
                    texts[t].fontSharedMaterial=row.Signals[t]=signal;
                }
                // Three concentric, non-overlapping rings form the same narrow glow on every edge.
                row.Border=new UnityEngine.UI.Image[12];
                for(int edge=0;edge<row.Border.Length;edge++)
                    row.Border[edge]=Image(row.Root,Vector2.zero,Vector2.zero,Color.clear);
            }
        }

        // Shared by setup and combat. Text comes from the structured catalog expression;
        // measurement uses the final fixed fonts, never temporary emphasis or auto-sizing.
        float LayoutRuleCard(RectTransform root,TMP_Text number,TMP_Text condition,TMP_Text arrow,TMP_Text action,
            float width,float rightInset=14,float minHeight=0)
        {
            const float left=48,pad=16,gap=8,arrowWidth=24;
            float available=Mathf.Max(60,width-left-rightInset);
            condition.lineSpacing=action.lineSpacing=2;
            // Every rule keeps the same reading order, regardless of label length or view width.
            float conditionWidth=available;
            float actionWidth=available-arrowWidth;
            float conditionHeight=Mathf.Max(28,TextHeight(condition,conditionWidth));
            float actionHeight=Mathf.Max(28,TextHeight(action,actionWidth));
            float arrowHeight=Mathf.Max(28,TextHeight(arrow,arrowWidth));
            float actionTop=pad+conditionHeight+gap;
            float height=Mathf.Max(minHeight,conditionHeight+gap+Mathf.Max(actionHeight,arrowHeight)+pad*2);
            root.sizeDelta=new Vector2(width,height);
            SetCardTextRect(condition,left,pad,conditionWidth,conditionHeight,width,height);
            float arrowLeft=left;
            SetCardTextRect(arrow,arrowLeft,actionTop,arrowWidth,arrowHeight,width,height);
            SetCardTextRect(action,arrowLeft+arrowWidth,actionTop,actionWidth,actionHeight,width,height);
            if(number!=null)SetCardTextRect(number,16,pad,24,28,width,height);
            return height;
        }
        static void SetCardTextRect(TMP_Text text,float left,float top,float width,float height,float cardWidth,float cardHeight)
        {
            var rect=text.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.sizeDelta=new Vector2(width,height);
            rect.anchoredPosition=new Vector2(-cardWidth/2+left+width/2,cardHeight/2-top-height/2);
        }
        float MeasureRulePanelWidth(float availableWidth)=>Mathf.Clamp(availableWidth*.24f,320,410);
        void LayoutRuleHud(float width,float height)
        {
            float position=ruleScroll!=null&&ruleHudSize!=Vector2.zero?ruleScroll.verticalNormalizedPosition:hudScrollPosition;
            rulePanelWidth=MeasureRulePanelWidth(width);
            float scrollWidth=rulePanelWidth-24,viewportWidth=scrollWidth-14,viewportHeight=Mathf.Max(80,height-92);
            ruleHeading.rectTransform.sizeDelta=new Vector2(rulePanelWidth-32,44);
            ruleHeading.rectTransform.anchoredPosition=new Vector2(0,-34);
            ruleScrollRoot.sizeDelta=new Vector2(scrollWidth,viewportHeight);
            ruleScrollRoot.anchoredPosition=new Vector2(0,-28);
            ruleViewport.sizeDelta=new Vector2(viewportWidth,viewportHeight);ruleViewport.anchoredPosition=new Vector2(-7,0);
            ruleScrollbarRoot.sizeDelta=new Vector2(8,viewportHeight);ruleScrollbarRoot.anchoredPosition=new Vector2(scrollWidth/2-4,0);
            float y=2;
            foreach(var row in ruleRows)
            {
                float cardWidth=viewportWidth-4;
                float cardHeight=LayoutRuleCard(row.Root,row.Number,row.Condition,row.Arrow,row.Action,cardWidth);
                row.Root.anchoredPosition=new Vector2(0,-y-cardHeight/2);
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
            float panel=hasRules?rulePanelWidth:0;
            if(battleUi!=null)
            {
                battleUi.anchoredPosition=new Vector2(-panel/2,0);
                battleUi.sizeDelta=new Vector2(width-panel,height);
            }
            if(ruleSidebar!=null)
            {
                ruleSidebar.anchoredPosition=new Vector2((width-panel)/2,0);
                ruleSidebar.sizeDelta=new Vector2(panel,height);
            }
            var camera=Camera.main;
            if(camera==null)return;
            float fraction=panel/Mathf.Max(1,width);
            camera.rect=new Rect(0,0,1-fraction,1);
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
                AlignRuleGlow(row,scale);
                var f=(game.Phase==DuelPhase.Fight||game.Phase==DuelPhase.Training)&&row.Controller!=null&&row.Controller==game.Rules
                    ?row.Controller.Feedback(row.Operation,row.Row).Visual:default;
                var color=NeuralPalette.Operation(row.Operation);
                bool matched=f.Matched;
                row.Action.color=color;
                row.Condition.color=row.Arrow.color=matched?color:NeuralPalette.Primary;
                row.Number.color=matched?color:NeuralPalette.Secondary;
                row.Background.color=matched?Color.Lerp(NeuralPalette.Panel,color,.08f):NeuralPalette.Panel;
                if(row.Matched!=matched)
                {
                    row.Matched=matched;
                    foreach(var signal in row.Signals)
                        signal.SetColor(ShaderUtilities.ID_GlowColor,NeuralPalette.Alpha(color,matched?.28f:0));
                }
                // Match already includes optional AND and source intersection, independent of execution.
                // Feedback never reorders, scrolls, remeasures, or changes typography.
                for(int i=0;i<row.Border.Length;i++)row.Border[i].color=NeuralPalette.Alpha(color,
                    matched?(i/4==0?.65f:i/4==1?.16f:.05f):0);
            }
        }
    }
}
