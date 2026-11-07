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
            ConfigButton("HelpBack","← Back",page,new Vector2(left+64,top-24),new Vector2(128,48),CloseHelp,style:DuelControlStyle.Navigation);
            string[] tabs={"What're the controls?","What is configuration?"};
            Text(tabs[helpTab],page,new Vector2(left+332+370,top-22),new Vector2(740,44),30,TextAlignmentOptions.Left,headingFont);
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
                HelpControl(content,width,"A / D","Move left / right");
                HelpControl(content,width,"Mouse","Aim");
                HelpControl(content,width,"Space","Jump");
                HelpControl(content,width,"Left Shift","Dodge");
                HelpControl(content,width,"Left mouse","Fire");
                HelpControl(content,width,"Right mouse","Rapidly fire");
                HelpControl(content,width,"Esc","Pause / resume");
            }
            else
            {
                var expression=HelpRow(content,width,96);
                float part=(width-72)/2;
                HelpPart(expression,-part/2-20,part,"Condition","When to act.");
                Text("→",expression,Vector2.zero,new Vector2(40,44),24);
                HelpPart(expression,part/2+20,part,"Action","What to do.");
            }
            Canvas.ForceUpdateCanvases();
            helpScroll.verticalNormalizedPosition=helpPositions[helpTab];
        }

        RectTransform HelpRow(RectTransform parent,float width,float height)
        {
            var row=Rect("Help row",parent,Vector2.zero,new Vector2(width-24,height));
            var layout=row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.minHeight=layout.preferredHeight=height;
            return row;
        }

        void HelpControl(RectTransform parent,float width,string key,string action)
        {
            var row=HelpRow(parent,width,52);
            float usable=width-28,keyWidth=Mathf.Min(208,usable*.32f);
            var keyBox=Image(row,new Vector2((keyWidth-usable)/2,0),new Vector2(keyWidth,50),NeuralPalette.Panel).rectTransform;
            Text(key,keyBox,new Vector2(8,0),new Vector2(keyWidth-16,46),22,TextAlignmentOptions.MidlineLeft,numericFont);
            Text(action,row,new Vector2((keyWidth+16)/2,0),new Vector2(usable-keyWidth-16,50),23,TextAlignmentOptions.MidlineLeft);
        }

        void HelpPart(RectTransform row,float x,float width,string title,string copy)
        {
            var panel=Image(row,new Vector2(x,0),new Vector2(width,88),NeuralPalette.Panel).rectTransform;
            Text(title,panel,new Vector2(0,20),new Vector2(width-32,26),18,TextAlignmentOptions.MidlineLeft,headingFont).color=NeuralPalette.Secondary;
            Text(copy,panel,new Vector2(0,-14),new Vector2(width-32,40),24,TextAlignmentOptions.MidlineLeft);
        }
    }
}
