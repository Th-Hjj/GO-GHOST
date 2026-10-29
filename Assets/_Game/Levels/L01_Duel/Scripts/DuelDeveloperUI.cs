using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        sealed class DeveloperFieldView
        {
            public TMP_InputField Input;
            public Func<float> Read;
            public Func<bool> Available;
            public UnityEngine.UI.Button Minus,Plus;
        }
        readonly List<DeveloperFieldView> developerFields=new List<DeveloperFieldView>();
        int developerTab,developerSection,developerShortcutFrame=-1;
        TMP_Text developerStatus;
        public bool DeveloperShortcutConsumed=>developerShortcutFrame==Time.frameCount;

        // Runs before the input reader and physical action components, including in release players.
        void Update()
        {
            if(game==null||game.PlaytestPromptOpen||Keyboard.current==null)return;
            if(Keyboard.current.f8Key.wasPressedThisFrame||(game.DeveloperToolsOpen&&Keyboard.current.escapeKey.wasPressedThisFrame))
            {developerShortcutFrame=Time.frameCount;game.ToggleDeveloperTools();}
        }

        void BuildDeveloperTools()
        {
            Image(page,Vector2.zero,new Vector2(4000,4000),new Color(.025f,.025f,.025f,.98f)).raycastTarget=true;
            Text("Developer",page,new Vector2(-390,380),new Vector2(650,60),36,TextAlignmentOptions.Left,headingFont);
            Button("Close  [F8]",page,new Vector2(565,380),new Vector2(260,54),game.ToggleDeveloperTools);
            string[] sections={"Tuning","Waves","Analytics"};
            for(int i=0;i<sections.Length;i++)
            {
                int section=i;
                Button(sections[i],page,new Vector2(-440+i*440,305),new Vector2(410,52),
                    ()=>{developerSection=section;Show();},i==developerSection);
            }
            if(developerSection==2){BuildAnalytics();return;}
            if(developerSection==1){BuildDeveloperWaves();return;}
            BuildDeveloperTuning();
        }

        void BuildDeveloperWaves()
        {
            Text("Jump to wave",page,new Vector2(0,175),new Vector2(1000,48),30);
            for(int wave=1;wave<=3;wave++)
            {
                int target=wave;
                var jump=Button("Wave "+wave,page,new Vector2(-430+(wave-1)*430,70),new Vector2(390,60),
                    ()=>game.JumpToDeveloperWave(target),game.Wave==wave);
                jump.name="Dev_Wave"+wave;
                jump.interactable=ConditionalCatalog.IsValid(game.DraftLoadout);
            }
        }

        void BuildDeveloperTuning()
        {
            string[] tabs={"Player","Weapon","Health"};
            for(int i=0;i<tabs.Length;i++)
            {
                int tab=i;
                Button(tabs[i],page,new Vector2(-430+i*430,225),new Vector2(390,52),()=>{developerTab=tab;Show();},i==developerTab);
            }
            float y=145;
            foreach(var parameter in game.DeveloperParameters)
            {
                if(parameter.Group!=developerTab)continue;
                var p=parameter;
                AddDeveloperField(p.Id.ToString(),p.Label,y,p.Min,p.Max,p.Step,()=>game.GetDeveloperValue(p.Id),
                    value=>game.SetDeveloperValue(p.Id,value),()=>true);
                y-=developerTab==1?58:68;
            }
            if(developerTab==2)
            {
                string[] labels={"Player current HP","Gunner current HP","Swordsman current HP"};
                for(int i=0;i<labels.Length;i++)
                {
                    int actor=i;
                    AddDeveloperField("Health"+actor,labels[i],y,1,10000,10,
                        ()=>game.GetDeveloperHealth(actor,out int current,out _)?current:0,
                        value=>game.SetDeveloperHealth(actor,Mathf.RoundToInt(value)),
                        ()=>game.GetDeveloperHealth(actor,out _,out _),true);
                    y-=68;
                }
            }
            developerStatus=Text("",page,new Vector2(0,-335),new Vector2(1400,45),20);
            Button("Restore defaults",page,new Vector2(0,-400),new Vector2(300,54),()=>
            {game.ResetDeveloperTuning();RefreshDeveloperFields();developerStatus.text="Defaults restored.";});
            RefreshDeveloperFields();
        }

        void AddDeveloperField(string id,string label,float y,float min,float max,float step,Func<float> read,Action<float> write,Func<bool> available,bool currentHealth=false)
        {
            Text(label,page,new Vector2(-405,y),new Vector2(570,48),25,TextAlignmentOptions.Left);
            Text(currentHealth?"1 - max HP":$"{FormatDeveloper(min)} - {FormatDeveloper(max)}",page,new Vector2(540,y),new Vector2(220,44),20,TextAlignmentOptions.Left);
            var root=Rect("Dev_"+id,page,new Vector2(175,y),new Vector2(180,48));
            var background=root.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=new Color(.13f,.13f,.13f);background.raycastTarget=true;
            var viewport=Rect("Text Area",root,Vector2.zero,new Vector2(154,42));viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text=(TextMeshProUGUI)Text("",viewport,Vector2.zero,new Vector2(154,42),25,face:numericFont);
            var field=root.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic=background;field.textViewport=viewport;field.textComponent=text;
            field.contentType=TMP_InputField.ContentType.DecimalNumber;field.lineType=TMP_InputField.LineType.SingleLine;
            field.characterLimit=12;field.customCaretColor=true;field.caretColor=Color.white;
            field.selectionColor=new Color(.3f,.6f,.4f,.5f);
            var view=new DeveloperFieldView{Input=field,Read=read,Available=available};
            view.Minus=Button("-",page,new Vector2(30,y),new Vector2(76,48),()=>Apply(Mathf.Clamp(read()-step,min,max)));
            view.Plus=Button("+",page,new Vector2(320,y),new Vector2(76,48),()=>Apply(Mathf.Clamp(read()+step,min,max)));
            view.Minus.name="Dev_"+id+"_Minus";view.Plus.name="Dev_"+id+"_Plus";
            developerFields.Add(view);
            field.onEndEdit.AddListener(value=>
            {
                if(!game.DeveloperToolsOpen||field.wasCanceled)return;
                if(float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out float number)&&!float.IsNaN(number)&&!float.IsInfinity(number))Apply(Mathf.Clamp(number,min,max));
                else {RefreshDeveloperFields();developerStatus.text="Invalid number.";}
            });
            void Apply(float value)
            {
                if(!game.DeveloperToolsOpen||!available())return;
                write(value);RefreshDeveloperFields();developerStatus.text="Applied.";
            }
        }

        static string FormatDeveloper(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        void RefreshDeveloperFields()
        {
            foreach(var view in developerFields)
            {
                bool active=view.Available();
                view.Input.interactable=view.Minus.interactable=view.Plus.interactable=active;
                view.Input.SetTextWithoutNotify(active?FormatDeveloper(view.Read()):"Unavailable");
            }
        }
    }
}
