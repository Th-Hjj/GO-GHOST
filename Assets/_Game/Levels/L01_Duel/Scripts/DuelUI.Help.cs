using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        bool helpOpen;
        int helpTab;
        ConditionalLoadout helpDraft;
        RectTransform helpPage;
        UnityEngine.UI.ScrollRect helpScroll;
        readonly float[] helpPositions = { 1, 1 };

        void OpenHelp()
        {
            helpDraft=game.DraftLoadout;
            helpOpen=true;
            PlayMetrics.OpenHelp();
            Show();
        }

        void CloseHelp()
        {
            helpOpen=false;
            Show();
        }

        void CaptureHelpView()
        {
            if(helpScroll!=null)helpPositions[helpTab]=helpScroll.verticalNormalizedPosition;
            helpScroll=null;
        }

        void SelectHelpTab(int tab)
        {
            CaptureHelpView();
            helpTab=tab;
            Show();
        }

        void BuildHelp()
        {
            helpPage=page;
            configurationScreen=new Vector2Int(Screen.width,Screen.height);
            float scale=canvas.GetComponent<Canvas>().scaleFactor;
            page.sizeDelta=new Vector2(Screen.width/scale,Screen.height/scale);
            float left=-page.sizeDelta.x/2+32,right=page.sizeDelta.x/2-32;
            float top=page.sizeDelta.y/2-32,bottom=-page.sizeDelta.y/2+32;
            ConfigButton("HelpBack","Back",page,new Vector2(left+48,top-24),new Vector2(96,48),CloseHelp,style:DuelControlStyle.Navigation);
            string[] tabs={"What're the controls?","What is configuration?"};
            Text(tabs[helpTab],page,new Vector2(100,top-22),new Vector2(740,44),30,TextAlignmentOptions.Center,headingFont);
            for(int i=0;i<tabs.Length;i++)
            {
                int tab=i;
                ConfigButton("HelpTab"+i,tabs[i],page,new Vector2(left+150,top-112-i*80),new Vector2(300,64),
                    ()=>SelectHelpTab(tab),helpTab==i,style:DuelControlStyle.Navigation);
            }
            float contentLeft=left+332,width=right-contentLeft,contentTop=top-76;
            var content=BuildRuleOptions("HelpContent",page,new Vector2((contentLeft+right)/2,(contentTop+bottom)/2),new Vector2(width,contentTop-bottom));
            helpScroll=content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if(helpTab==0)
            {
                HelpEntry(content,width,"Move and aim","A / D  —  Move left / right\nMouse  —  Aim");
                HelpEntry(content,width,"Jump and dodge","Space  —  Jump\nLeft Shift  —  Dodge");
                HelpEntry(content,width,"Fire","Left mouse  —  Fire\nRight mouse  —  Rapidly fire");
                HelpEntry(content,width,null,"Esc  —  Pause / resume");
            }
            else
            {
                HelpEntry(content,width,"Condition → Action","Condition: when to act.\nAction: what to do.");
                HelpEntry(content,width,"Add and edit","Click Add Rule, then choose a Condition and an Action.\nChoose parameters when shown. Click a rule to edit; × removes it.");
                HelpEntry(content,width,"Conditions and priority","Hover a condition and use + Condition. All conditions must be true (AND).\nHigher rules take priority. Use ▲ / ▼ to reorder.");
            }
            Canvas.ForceUpdateCanvases();
            helpScroll.verticalNormalizedPosition=helpPositions[helpTab];
        }

        void HelpEntry(RectTransform content,float width,string title,string body)
        {
            if(!string.IsNullOrEmpty(title))
            {
                var heading=Text(title,content,Vector2.zero,Vector2.zero,26,TextAlignmentOptions.TopLeft,headingFont);
                var headingLayout=heading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                headingLayout.minHeight=headingLayout.preferredHeight=42;
            }
            var text=Text(body,content,Vector2.zero,Vector2.zero,23,TextAlignmentOptions.TopLeft);
            text.color=NeuralPalette.Secondary;
            var layout=text.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight=layout.preferredHeight=TextHeight(text,width-30)+24;
        }
    }
}
