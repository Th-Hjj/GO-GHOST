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
        bool tuningBoxer;
        int tuningMove = 1;
        TMP_Text developerStatus;
        public bool DeveloperShortcutConsumed=>developerShortcutFrame==Time.frameCount;

        // Runs before the input reader and physical action components, including in release players.
        void Update()
        {
            if(game==null||Keyboard.current==null)return;
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
            for(int wave=1;wave<=DuelGame.BonusWave;wave++)
            {
                int target=wave;
                string label=wave==DuelGame.BonusWave?"Bonus":wave==DuelGame.FinalBossWave?"Final Boss":"Wave "+wave;
                var jump=Button(label,page,new Vector2(-495+(wave-1)*330,70),new Vector2(300,60),
                    ()=>game.JumpToDeveloperWave(target),game.Wave==wave);
                jump.name="Dev_Wave"+wave;
                jump.interactable=ConditionalCatalog.IsValid(game.DraftLoadout);
            }
            for (int phase = 1; phase <= 2; phase++)
            {
                int targetPhase = phase;
                var jump = Button("Start Phase " + phase, page, new Vector2(phase == 1 ? -175 : 175, -50),
                    new Vector2(310, 58), () => game.JumpToDeveloperWave(DuelGame.FinalBossWave, targetPhase));
                jump.name = "Dev_FinalPhase" + phase;
                jump.interactable = ConditionalCatalog.IsValid(game.DraftLoadout);
            }
        }

        void BuildDeveloperTuning()
        {
            string[] tabs={"Player","Weapon","Health","Final Boss"};
            for(int i=0;i<tabs.Length;i++)
            {
                int tab=i;
                Button(tabs[i],page,new Vector2(-495+i*330,225),new Vector2(305,52),()=>{developerTab=tab;Show();},i==developerTab);
            }
            if (developerTab == 3) BuildFinalBossTuning();
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
                string[] labels={"Player current HP","Gunner current HP",game.Wave == DuelGame.FinalBossWave ? "Final Boss current HP" : "Swordsman current HP"};
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
            Button("Restore defaults",page,new Vector2(developerTab==3?210:0,-400),new Vector2(300,54),()=>
            {game.ResetDeveloperTuning();RefreshDeveloperFields();developerStatus.text="Defaults restored.";});
            RefreshDeveloperFields();
        }

        void BuildFinalBossTuning()
        {
            for (int phase = 1; phase <= 2; phase++)
            {
                bool boxing = phase == 2;
                Button(boxing ? "Boxer" : "Axe", page, new Vector2(boxing ? 160 : -160, 150), new Vector2(290,48),
                    () => { tuningBoxer = boxing; tuningMove = 1; Show(); }, tuningBoxer == boxing);
            }
            var preview = Button("Try selected move", page, new Vector2(-210,-400), new Vector2(360,54),
                () => game.PreviewBossMove(tuningBoxer,tuningMove));
            preview.interactable = game.CanPreviewBossMove(tuningBoxer);
            string[] names = tuningBoxer ? new[] { "Jab", "Uppercut", "Slip / Hook", "Haymaker" }
                : new[] { "Chop", "Spin", "Throw / Recall", "Boomerang" };
            for (int i = 0; i < names.Length; i++)
            {
                int move = i + 1;
                Button(names[i], page, new Vector2(-465 + 310*i,85), new Vector2(290,48),
                    () => { tuningMove = move; Show(); }, tuningMove == move);
            }
            BossMoveTuning Read() => game.GetBossMoveTuning(tuningBoxer,tuningMove);
            void Write(BossMoveTuning t) => game.SetBossMoveTuning(tuningBoxer,tuningMove,t);
            AddDeveloperField("BossWindup","Wind-up (s)",19,.05f,4,.05f,()=>Read().Windup,
                v=>{var t=Read(); t.Windup=v; Write(t);},()=>true);
            AddDeveloperField("BossRecovery","Recovery (s)",-39,.05f,4,.05f,()=>Read().Recovery,
                v=>{var t=Read(); t.Recovery=v; Write(t);},()=>true);
            AddDeveloperField("BossCooldown","Cooldown (s)",-97,.1f,15,.1f,()=>Read().Cooldown,
                v=>{var t=Read(); t.Cooldown=v; Write(t);},()=>true);
            AddDeveloperField("BossDamage","Damage",-155,1,500,1,()=>Read().Damage,
                v=>{var t=Read(); t.Damage=Mathf.RoundToInt(v); Write(t);},()=>true);
            AddDeveloperField("FinalBossHP","Final Boss current HP",-217,1,10000,10,
                ()=>game.GetDeveloperHealth(2,out int hp,out _)?hp:0, v=>game.SetDeveloperHealth(2,Mathf.RoundToInt(v)),
                ()=>game.Wave==DuelGame.FinalBossWave && game.GetDeveloperHealth(2,out _,out _),true);
            for (int i=0;i<3;i++)
            {
                int percent = 75-25*i;
                var jump = Button(percent+"% HP", page, new Vector2(-300+300*i,-282), new Vector2(260,48), () =>
                {
                    if (game.GetDeveloperHealth(2,out _,out int max)) game.SetDeveloperHealth(2,max*percent/100);
                    RefreshDeveloperFields();
                });
                jump.interactable = game.Wave==DuelGame.FinalBossWave && !game.FinalBossPhaseTwoReached
                    && game.GetDeveloperHealth(2,out _,out _);
            }
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
