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
            string[] tabs={"What's the controls?","What is configuration?"};
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
                HelpEntry(content,width,"Move and aim","A / D  —  Move left / right\nMouse  —  Aim independently of movement");
                HelpEntry(content,width,"Jump and dodge","Space  —  Jump\nS  —  Drop through your current platform\nLeft Shift  —  Dodge");
                HelpEntry(content,width,"Fire","Hold left mouse  —  Keep firing accurate shots\nRight mouse  —  Rapidly fire all remaining rounds with reduced accuracy\nAn empty magazine reloads automatically.");
                HelpEntry(content,width,"Pause and practice","Esc  —  Pause / resume\nConfiguration in the pause menu returns to your rules.\nUse Training to try your rules, then Continue to prepare for the fight.");
            }
            else
            {
                HelpEntry(content,width,"Condition → Action","A Condition says WHEN a rule applies. An Action says WHAT your body does.\nFor example: Incoming Attack (Any) → Dodge (Away from Threat).");
                HelpEntry(content,width,"Build a rule","1. Click Add Rule and choose a Condition.\n2. Choose its parameter if shown.\n3. Choose an Action from Weapon, Movement or Jump.\nThe rule is installed when you choose the Action or its required parameter.");
                HelpEntry(content,width,"Combine conditions","Hover a condition preview and use + Condition to add another.\nEvery condition in the rule must be true together (AND). Each rule has one Action.\nHover a preview and use its × to remove that choice.");
                HelpEntry(content,width,"Edit and prioritize","Click a rule's Condition or Action to edit it. Cancel keeps the original.\nUse ▲ / ▼ to change priority, and × on a rule to remove it.\nHigher rules have priority when they compete. You can install up to 20 rules.");
                HelpEntry(content,width,"Share control with your rules","Move, Jump, Aim, Fire and Dodge can work independently when compatible.\nWhen no rule can perform an operation, you control it manually.\nAim Assist helps your manual aim; Auto Aim chooses the aim direction.\nTry your configuration in Training and adjust it as you play.");
            }
            Canvas.ForceUpdateCanvases();
            helpScroll.verticalNormalizedPosition=helpPositions[helpTab];
        }

        void HelpEntry(RectTransform content,float width,string title,string body)
        {
            var heading=Text(title,content,Vector2.zero,Vector2.zero,26,TextAlignmentOptions.TopLeft,headingFont);
            var headingLayout=heading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            headingLayout.minHeight=headingLayout.preferredHeight=42;
            var text=Text(body,content,Vector2.zero,Vector2.zero,23,TextAlignmentOptions.TopLeft);
            text.color=NeuralPalette.Secondary;
            var layout=text.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight=layout.preferredHeight=TextHeight(text,width-30)+24;
        }
    }
}
