using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        int selectedOperation;
        public int SelectedOperation=>selectedOperation;
        void SelectOperation(int op){selectedOperation=op;Show();}
        void BuildConditionalLoadout()
        {
            Text("WAVE "+game.Wave+"  CONFIGURE",page,new Vector2(0,365),new Vector2(800,65),38);
            Button("Back",page,new Vector2(-685,385),new Vector2(150,50),game.MainMenu);
            var body=Rect("ConditionalBody",page,new Vector2(-420,0),new Vector2(810,810));
            body.localScale=Vector3.one*.78f;
            var guide=Rect("AugmentationGuide",body,Vector2.zero,new Vector2(810,810));
            guide.gameObject.AddComponent<MissionAugmentationGuide>().raycastTarget=false;
            Vector2[] positions={new Vector2(860,821),new Vector2(860,645),new Vector2(663,757.5f),new Vector2(1057,757.5f),new Vector2(801.5f,364),new Vector2(918.5f,364)};
            Vector2[] sizes={new Vector2(72,88),new Vector2(146,222),new Vector2(264,117),new Vector2(264,117),new Vector2(103,334),new Vector2(103,334)};
            int selectedModule=selectedOperation<2?0:selectedOperation<4?1:2;
            for(int i=0;i<6;i++)
            {
                int module=i>=4?0:i>=2?1:2;
                var rect=Rect("BodyRegion"+i,body,positions[i]-new Vector2(860,570),sizes[i]);
                var graphic=rect.gameObject.AddComponent<MissionBodyRegion>();
                graphic.Round=i==0;graphic.Contour=MissionUI.BodyContour(i);graphic.raycastTarget=i!=0;
                graphic.Selected=i!=0&&module==selectedModule;
                if(i==0)continue;
                var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=graphic;
                int op=module==0?0:module==1?2:4;
                button.onClick.AddListener(()=>SelectOperation(op));
            }
            string[] labels={"Legs","Arms","Spine"};
            for(int m=0;m<3;m++)
            {
                int op=m==0?0:m==1?2:4;
                Button(labels[m],page,new Vector2(-650+m*225,-335),new Vector2(200,48),()=>SelectOperation(op),selectedModule==m);
            }
            int first=selectedModule==0?0:selectedModule==1?2:4;
            int count=selectedModule==2?1:2;
            for(int i=0;i<count;i++)
            {
                int op=first+i;
                Button(ConditionalCatalog.OperationLabels[op],page,new Vector2(220+i*360,275),new Vector2(345,58),()=>SelectOperation(op),selectedOperation==op);
            }
            var config=game.DraftLoadout.Operations[selectedOperation];
            Button("Manual",page,new Vector2(220,200),new Vector2(330,52),()=>SetOperationEnabled(false),!config.Enabled);
            Button("Rules",page,new Vector2(580,200),new Vector2(330,52),()=>SetOperationEnabled(true),config.Enabled);
            for(int i=0;i<config.Rules.Count;i++)
            {
                int row=i;var rule=config.Rules[i];float y=90-i*175;
                var condition=ConditionalCatalog.Condition(rule.Condition);
                string conditionLabel=rule.Condition==37?(rule.Below?"Enemy Below":"Enemy Above"):condition.Label;
                Button("WHEN  "+condition.Code+"  "+conditionLabel,page,new Vector2(400,y),new Vector2(680,47),()=>ChooseRule(row,true));
                Button("THEN  "+rule.Action+"  "+ConditionalCatalog.Response(rule.Action).Label,page,new Vector2(400,y-54),new Vector2(680,47),()=>ChooseRule(row,false));
                Button("Remove",page,new Vector2(137,y-106),new Vector2(155,40),()=>game.EditLoadout(d=>d.Operations[selectedOperation].Rules.RemoveAt(row)));
                if(config.Rules.Count>1)
                    Button(row==0?"Move Down":"Move Up",page,new Vector2(330,y-106),new Vector2(200,40),()=>game.EditLoadout(d=>d.Operations[selectedOperation].Rules.Reverse()));
                if(rule.Condition==37)
                    Button(rule.Below?"Enemy Below":"Enemy Above",page,new Vector2(600,y-106),new Vector2(240,40),()=>game.EditLoadout(d=>d.Operations[selectedOperation].Rules[row].Below=!rule.Below));
                if(rule.Action=="Q03"&&!HasEnemySource(rule.Condition))
                    Text("Requires an enemy source.",page,new Vector2(400,y-137),new Vector2(650,30),18);
            }
            if(config.Rules.Count<2)
                Button("Add Rule",page,new Vector2(400,-255),new Vector2(680,48),()=>game.EditLoadout(d=>
                {
                    string action="";foreach(var b in ConditionalCatalog.Responses)if((int)b.Operation==selectedOperation){action=b.Id;break;}
                    d.Operations[selectedOperation].Rules.Add(new ConditionalRule{Condition=9,Action=action});
                }));
            Text("No matching rule: manual control.",page,new Vector2(400,-305),new Vector2(700,35),20);
            Button("Start Fight",page,new Vector2(400,-375),new Vector2(680,62),game.StartFight);
        }
        void SetOperationEnabled(bool enabled)=>game.EditLoadout(d=>d.Operations[selectedOperation].Enabled=enabled);
        static bool HasEnemySource(int id)=>id==6||id==13||id==14||id==18||id==21||id==22||id>=25&&id<=43&&id!=39||id==50;
        void ChooseRule(int row,bool condition)
        {
            // Modal input blocker keeps the page behind this list from receiving pointer clicks.
            var overlay=Rect("RuleSelector",page,Vector2.zero,new Vector2(1600,900));
            var backdrop=overlay.gameObject.AddComponent<UnityEngine.UI.Image>();backdrop.color=new Color(0,0,0,.94f);backdrop.raycastTarget=true;
            Text(condition?"Choose Condition":"Choose Action",overlay,new Vector2(0,340),new Vector2(950,60),34);
            Button("Back",overlay,new Vector2(-650,355),new Vector2(170,50),()=>{overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);});
            var scrollRoot=Rect("CatalogScroll",overlay,new Vector2(0,-15),new Vector2(1080,590));
            var scroll=scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var viewport=Rect("Viewport",scrollRoot,Vector2.zero,new Vector2(1080,590));
            var image=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.025f,.035f,.045f);image.raycastTarget=true;
            var mask=viewport.gameObject.AddComponent<UnityEngine.UI.Mask>();mask.showMaskGraphic=false;
            var content=Rect("Content",viewport,Vector2.zero,new Vector2(1040,1));
            content.anchorMin=new Vector2(.5f,1);content.anchorMax=new Vector2(.5f,1);content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=7;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var fitter=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=content;scroll.scrollSensitivity=35;
            if(condition)
                for(int category=0;category<6;category++)
                {
                    var heading=Text(ConditionalCatalog.Categories[category],content,Vector2.zero,new Vector2(1040,40),23,TextAlignmentOptions.Left);
                    heading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=40;
                    foreach(var entry in ConditionalCatalog.Conditions)
                    {
                        if(entry.Category!=category)continue;int id=entry.Id;
                        SelectorButton(entry.Code+"  "+entry.Label,content,()=>game.EditLoadout(d=>d.Operations[selectedOperation].Rules[row].Condition=id));
                    }
                }
            else foreach(var entry in ConditionalCatalog.Responses)
            {
                if((int)entry.Operation!=selectedOperation)continue;string id=entry.Id;
                SelectorButton(id+"  "+entry.Label,content,()=>game.EditLoadout(d=>d.Operations[selectedOperation].Rules[row].Action=id));
            }
            scroll.verticalNormalizedPosition=1;
        }
        void SelectorButton(string label,RectTransform content,UnityEngine.Events.UnityAction click)
        {
            Button(label,content,Vector2.zero,new Vector2(1040,48),click);
            var row=content.GetChild(content.childCount-1).gameObject.AddComponent<UnityEngine.UI.LayoutElement>();row.preferredHeight=48;
        }
    }
}
