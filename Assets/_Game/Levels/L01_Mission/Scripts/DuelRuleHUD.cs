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
        }
        readonly List<RuleRowView> ruleRows = new List<RuleRowView>(10);
        ConditionalController hudRules;
        RectTransform ruleSidebar, battleUi;

        void ClearRuleHud()
        {
            ruleRows.Clear();hudRules=null;ruleSidebar=battleUi=null;
        }
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
            background.color=Color.black;background.raycastTarget=false;
            var divider=Image(ruleSidebar,new Vector2(-RulePanelWidth/2,0),new Vector2(1,900),new Color(.25f,.25f,.25f));
            divider.rectTransform.anchorMin=new Vector2(0,0);divider.rectTransform.anchorMax=new Vector2(0,1);
            divider.rectTransform.anchoredPosition=Vector2.zero;divider.rectTransform.sizeDelta=new Vector2(1,0);
            // A top-anchored, fixed readable block fits all ten rules at the supported 16:9 sizes.
            var content=Rect("RuleList",ruleSidebar,Vector2.zero,new Vector2(RulePanelWidth,0));
            content.anchorMin=content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);
            Text("Rules",content,new Vector2(0,-38),new Vector2(318,44),30,TextAlignmentOptions.Left);
            float y=-82;
            int[] order={2,3,4,0,1};
            foreach(int op in order)
            {
                int rows=hudRules.RuleCount((ControlOperation)op);
                if(rows==0)continue;
                var label=Text(ShortOperationLabels[op],content,new Vector2(0,y),new Vector2(318,28),22,TextAlignmentOptions.Left);
                label.color=new Color(.72f,.72f,.72f);y-=40;
                for(int row=0;row<rows;row++)
                {
                    var rule=hudRules.RuleAt((ControlOperation)op,row);
                    var root=Rect("RuleFeedback_"+op+"_"+row,content,new Vector2(0,y),new Vector2(318,50));
                    var glow=root.gameObject.AddComponent<UnityEngine.UI.Image>();
                    glow.raycastTarget=false;glow.color=Color.clear;
                    var border=new[]{Image(root,new Vector2(0,25),new Vector2(318,1),Color.clear),
                        Image(root,new Vector2(0,-25),new Vector2(318,1),Color.clear),
                        Image(root,new Vector2(-159,0),new Vector2(1,50),Color.clear),
                        Image(root,new Vector2(159,0),new Vector2(1,50),Color.clear)};
                    Text((row+1)+"  "+ConditionLabel(rule),root,new Vector2(0,13),new Vector2(306,28),20,TextAlignmentOptions.Left);
                    Text("> "+ConditionalCatalog.Response(rule.Action).Label,root,new Vector2(10,-13),new Vector2(286,28),20,TextAlignmentOptions.Left);
                    ruleRows.Add(new RuleRowView{Operation=(ControlOperation)op,Row=row,Background=glow,Border=border});
                    y-=52;
                }
                y-=6;
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
                float glow=game.Phase==DuelPhase.Fight&&hudRules==game.Rules
                    ?hudRules.Feedback(row.Operation,row.Row).Glow:0;
                row.Background.color=new Color(.15f,.8f,.35f,.11f*glow);
                foreach(var edge in row.Border)edge.color=new Color(.3f,1,.5f,.6f*glow);
            }
        }
    }
}
