using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        const float RulePanelWidth = 350;
        sealed class RuleRowView
        {
            public ControlOperation Operation;
            public int Row;
            public UnityEngine.UI.Image Background;
            public UnityEngine.UI.Image[] Border;
            public TMP_Text Condition, Action, Arrow;
            public Material Signal;
            public bool Executing;
            public UnityEngine.UI.Image Identity;
        }
        readonly List<RuleRowView> ruleRows = new List<RuleRowView>(ConditionalCatalog.MaxRulesTotal);
        ConditionalController hudRules;
        RectTransform ruleSidebar, battleUi;

        void ClearRuleHud()
        {
            foreach(var row in ruleRows)if(row.Signal!=null)Destroy(row.Signal);
            ruleRows.Clear();hudRules=null;ruleSidebar=battleUi=null;
        }
        void OnDestroy()=>ClearRuleHud();
        void BuildRuleHud()
        {
            if(game.Phase!=DuelPhase.Prepare&&game.Phase!=DuelPhase.Fight)return;
            battleUi=Rect("BattleHUD",page,Vector2.zero,new Vector2(1600,900));
            hudRules=game.Rules;
            int count=0;
            if(hudRules!=null)for(int op=0;op<5;op++)count+=hudRules.RuleCount((ControlOperation)op);
            if(count==0)return;
            ruleSidebar=Rect("BattleRules",page,Vector2.zero,new Vector2(RulePanelWidth,900));
            var group=ruleSidebar.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts=false;group.interactable=false;
            var background=ruleSidebar.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color=NeuralPalette.Background;background.raycastTarget=false;
            var divider=Image(ruleSidebar,new Vector2(-RulePanelWidth/2,0),new Vector2(1,900),NeuralPalette.Border);
            divider.rectTransform.anchorMin=new Vector2(0,0);divider.rectTransform.anchorMax=new Vector2(0,1);
            divider.rectTransform.anchoredPosition=Vector2.zero;divider.rectTransform.sizeDelta=new Vector2(1,0);
            // A top-anchored, fixed readable block fits all five rules at the supported 16:9 sizes.
            var content=Rect("RuleList",ruleSidebar,Vector2.zero,new Vector2(RulePanelWidth,0));
            content.anchorMin=content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);
            Text("Rules: "+count+"/"+ConditionalCatalog.MaxRulesTotal,content,new Vector2(0,-38),new Vector2(318,44),30,TextAlignmentOptions.Left,headingFont);
            float y=-82;
            int[] order={2,3,4,0,1};
            foreach(int op in order)
            {
                int rows=hudRules.RuleCount((ControlOperation)op);
                if(rows==0)continue;
                var label=Text(ShortOperationLabels[op],content,new Vector2(0,y),new Vector2(318,32),22,TextAlignmentOptions.Left);
                label.color=NeuralPalette.Secondary;y-=22;
                for(int row=0;row<rows;row++)
                {
                    var rule=hudRules.RuleAt((ControlOperation)op,row);
                    var root=Rect("RuleFeedback_"+op+"_"+row,content,Vector2.zero,Vector2.zero);
                    var condition=Text((row+1)+"  "+ConditionLabel(rule),root,Vector2.zero,new Vector2(294,28),22,TextAlignmentOptions.TopLeft);
                    condition.name="ConditionText";
                    var arrow=Text(">",root,Vector2.zero,new Vector2(18,28),22,TextAlignmentOptions.TopLeft,controlFont);
                    arrow.name="ActionArrow";
                    var action=Text(ConditionalCatalog.Response(rule.Action).Label,root,Vector2.zero,new Vector2(264,28),22,TextAlignmentOptions.TopLeft,controlFont);
                    action.name="ActionText";
                    // One material per visible row; only glow alpha changes at acceptance/release.
                    // Fixed weight, keyword and padding keep every glyph and line break in place.
                    var signal=new Material(action.fontSharedMaterial){name="Rule signal",hideFlags=HideFlags.DontSave};
                    signal.EnableKeyword("GLOW_ON");
                    signal.SetFloat(ShaderUtilities.ID_GlowOffset,0);
                    signal.SetFloat(ShaderUtilities.ID_GlowOuter,.12f);
                    signal.SetFloat(ShaderUtilities.ID_GlowInner,.02f);
                    signal.SetColor(ShaderUtilities.ID_GlowColor,Color.clear);
                    action.fontSharedMaterial=arrow.fontSharedMaterial=signal;
                    condition.lineSpacing=action.lineSpacing=2;
                    float conditionHeight=Mathf.Max(28,TextHeight(condition,294));
                    float actionHeight=Mathf.Max(28,TextHeight(action,264));
                    float arrowHeight=TextHeight(arrow,18);
                    float height=16+conditionHeight+4+actionHeight;
                    root.sizeDelta=new Vector2(318,height);root.anchoredPosition=new Vector2(0,y-height/2);
                    condition.rectTransform.sizeDelta=new Vector2(294,conditionHeight);
                    condition.rectTransform.anchoredPosition=new Vector2(0,height/2-8-conditionHeight/2);
                    action.rectTransform.sizeDelta=new Vector2(264,actionHeight);
                    action.rectTransform.anchoredPosition=new Vector2(15,height/2-12-conditionHeight-actionHeight/2);
                    arrow.rectTransform.sizeDelta=new Vector2(18,arrowHeight);
                    arrow.rectTransform.anchoredPosition=new Vector2(-126,height/2-12-conditionHeight-arrowHeight/2);
                    var glow=root.gameObject.AddComponent<UnityEngine.UI.Image>();
                    glow.raycastTarget=false;glow.color=Color.clear;
                    var border=new[]{Image(root,new Vector2(0,height/2-1),new Vector2(318,2),Color.clear),
                        Image(root,new Vector2(0,-height/2+1),new Vector2(318,2),Color.clear),
                        Image(root,new Vector2(-158,0),new Vector2(2,height-4),Color.clear),
                        Image(root,new Vector2(158,0),new Vector2(2,height-4),Color.clear)};
                    var identity=Image(root,new Vector2(-157,0),new Vector2(2,height-16),NeuralPalette.Operation((ControlOperation)op));
                    ruleRows.Add(new RuleRowView{Operation=(ControlOperation)op,Row=row,Background=glow,Border=border,Condition=condition,Action=action,Arrow=arrow,Signal=signal,Identity=identity});
                    y-=height+8;
                }
                y-=26;
            }
        }
        public void UpdateBattleLayout()
        {
            if(canvas==null)return;
            float scale=canvas.GetComponent<Canvas>().scaleFactor;
            float width=Screen.width/Mathf.Max(.01f,scale),height=Screen.height/Mathf.Max(.01f,scale);
            bool hasRules=ruleSidebar!=null&&hudRules==game.Rules&&
                (game.Phase==DuelPhase.Prepare||game.Phase==DuelPhase.Fight);
            float panel=hasRules?RulePanelWidth:0;
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
        void UpdateRuleHud()
        {
            UpdateBattleLayout();
            foreach(var row in ruleRows)
            {
                var f=game.Phase==DuelPhase.Fight&&hudRules==game.Rules
                    ?hudRules.Feedback(row.Operation,row.Row).Visual:default;
                var color=NeuralPalette.Operation(row.Operation);
                bool executing=f.Executing||f.AcceptedThisFrame;
                row.Condition.color=f.Matched?Color.Lerp(NeuralPalette.Primary,color,.55f):NeuralPalette.Primary;
                row.Action.color=row.Arrow.color=executing?color:NeuralPalette.Primary;
                if(row.Executing!=executing)
                {
                    row.Executing=executing;
                    row.Signal.SetColor(ShaderUtilities.ID_GlowColor,NeuralPalette.Alpha(color,executing?.28f:0));
                }
                row.Identity.color=NeuralPalette.Alpha(color,executing?1:.45f);
                row.Background.color=Color.clear;
                // A fading edge never keeps the action/arrow marked as the operation owner.
                for(int i=0;i<row.Border.Length;i++)row.Border[i].color=NeuralPalette.Alpha(color,
                    executing?(i==1?.7f:.08f):i==1?Mathf.Max(f.Pulse*.5f,f.Afterglow*.28f):0);
            }
        }
    }
}
