using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace GoGhost.Duel
{
    public sealed class DuelUI : MonoBehaviour
    {
        DuelGame game;
        TMP_FontAsset font;
        RectTransform canvas, page;
        TMP_Text playerHealth, resources;
        TMP_Text preparationCount;
        int lastPreparationCount;
        TMP_Text[] policeHealth = System.Array.Empty<TMP_Text>();
        UnityEngine.UI.Image[] policeFill = System.Array.Empty<UnityEngine.UI.Image>();
        UnityEngine.UI.Image playerFill;
        public void Initialize(DuelGame game, TMP_FontAsset font)
        {
            this.game = game; this.font = font;
            var go = new GameObject("DuelCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform,false); canvas = go.GetComponent<RectTransform>();
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scale = go.GetComponent<UnityEngine.UI.CanvasScaler>(); scale.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1600,900); scale.matchWidthOrHeight=.5f;
            scale.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
            }
        }
        RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var o = new GameObject(name,typeof(RectTransform)); var r=o.GetComponent<RectTransform>();
            r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=position; r.sizeDelta=size; return r;
        }
        TMP_Text Text(string value,Transform parent,Vector2 pos,Vector2 size,int points=26,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var r=Rect(value,parent,pos,size); var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=font; t.text=value; t.fontSize=points; t.color=Color.white; t.alignment=align;
            t.raycastTarget=false; t.richText=false; t.textWrappingMode=TextWrappingModes.Normal; return t;
        }
        UnityEngine.UI.Image Image(Transform parent,Vector2 pos,Vector2 size,Color color)
        {
            var r=Rect("Fill",parent,pos,size); var img=r.gameObject.AddComponent<UnityEngine.UI.Image>(); img.color=color; img.raycastTarget=false; return img;
        }
        void Button(string title,Transform parent,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction click,bool selected=false)
        {
            var r=Rect(title,parent,pos,size); var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color=selected?Color.white:new Color(.12f,.12f,.12f); image.raycastTarget=true;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic=image; b.onClick.AddListener(click);
            var c=b.colors; c.normalColor=Color.white; c.highlightedColor=new Color(.7f,.7f,.7f); c.pressedColor=new Color(.4f,.4f,.4f); b.colors=c;
            var t=Text(title,r,Vector2.zero,size,24); if(selected)t.color=Color.black;
        }
        public void Show()
        {
            if (canvas == null) return;
            if(page!=null){page.gameObject.SetActive(false);Destroy(page.gameObject);}
            page=Rect("Page",canvas,Vector2.zero,new Vector2(1600,900));
            playerHealth=resources=null; playerFill=null;
            preparationCount=null; lastPreparationCount=-1;
            policeHealth=System.Array.Empty<TMP_Text>(); policeFill=System.Array.Empty<UnityEngine.UI.Image>();
            if(game.Phase==DuelPhase.Menu||game.Phase==DuelPhase.Configure)
                Image(page,Vector2.zero,new Vector2(4000,4000),Color.black);
            if(game.Paused)
            {
                Image(page,Vector2.zero,new Vector2(4000,4000),new Color(0,0,0,.88f));
                Button("Resume",page,new Vector2(0,45),new Vector2(330,65),()=>game.SetPause(false));
                Button("Main Menu",page,new Vector2(0,-45),new Vector2(330,65),game.MainMenu); return;
            }
            if(game.Phase==DuelPhase.Menu)
            {
                Text("GO GHOST",page,new Vector2(0,150),new Vector2(1000,100),78);
                Button("New Game",page,Vector2.zero,new Vector2(350,68),game.NewGame);
                Button("Quit",page,new Vector2(0,-95),new Vector2(350,68),Application.Quit); return;
            }
            if(game.Phase==DuelPhase.Configure)
            {
                Text("WAVE "+game.Wave,page,new Vector2(0,330),new Vector2(600,65),42);

                string[] labels={"Legs","Arms","Spine"};
                string[] instructions={"A / D: Move     Space: Jump","Mouse: Aim     LMB: Rifle","Left Shift: Dodge"};
                bool[] settings={game.DraftLegs,game.DraftArms,game.DraftSpine};
                for(int i=0;i<3;i++)
                {
                    int module=i; float y=135-i*125;
                    Text(labels[i],page,new Vector2(-545,y),new Vector2(160,65),30,TextAlignmentOptions.Left);
                    Button("Player",page,new Vector2(-330,y),new Vector2(145,58),()=>game.SetDraft(module,false),!settings[i]);
                    Button("AI",page,new Vector2(-165,y),new Vector2(145,58),()=>game.SetDraft(module,true),settings[i]);
                    Text(instructions[i],page,new Vector2(335,y),new Vector2(800,90),23,TextAlignmentOptions.Left);
                }
                Button("Start Fight",page,new Vector2(0,-360),new Vector2(360,65),game.StartFight); return;
            }
            if(game.Phase==DuelPhase.Failed || game.Phase==DuelPhase.Complete)
            {
                Image(page,Vector2.zero,new Vector2(4000,4000),new Color(0,0,0,.86f));
                Text(game.Phase==DuelPhase.Failed?"DEFEAT":"COMPLETE",page,new Vector2(0,160),new Vector2(800,90),64);
                if(game.Phase==DuelPhase.Failed)Button("Reconfigure",page,Vector2.zero,new Vector2(360,65),game.Reconfigure);
                else
                {
                    Button("Restart",page,Vector2.zero,new Vector2(360,65),game.NewGame);
                    Button("Main Menu",page,new Vector2(0,-90),new Vector2(360,65),game.MainMenu);
                }
                return;
            }
            Text("WAVE "+game.Wave,page,new Vector2(0,400),new Vector2(280,50),30);
            policeHealth = new TMP_Text[game.Bosses.Count];
            policeFill = new UnityEngine.UI.Image[game.Bosses.Count];
            for (int i=0; i<game.Bosses.Count; i++)
            {
                float y=345-i*40;
                policeHealth[i]=Text("",page,new Vector2(-400,y),new Vector2(300,40),24,TextAlignmentOptions.Left);
                Image(page,new Vector2(0,y),new Vector2(430,12),new Color(.18f,.18f,.18f));
                var color=game.Bosses[i].Profile.Kind==BossKind.Refraction?new Color(.333f,.788f,.953f):new Color(.91f,.298f,.298f);
                policeFill[i]=Image(page,new Vector2(-215,y),new Vector2(430,12),color);
                policeFill[i].rectTransform.pivot=new Vector2(0,.5f);
            }

            playerHealth=Text("",page,new Vector2(-400,-340),new Vector2(300,40),24,TextAlignmentOptions.Left);
            Image(page,new Vector2(0,-340),new Vector2(430,12),new Color(.18f,.18f,.18f));
            playerFill=Image(page,new Vector2(-215,-340),new Vector2(430,12),new Color(.6f,.9f,.65f));
            playerFill.rectTransform.pivot=new Vector2(0,.5f);
            resources=Text("",page,new Vector2(0,-397),new Vector2(1450,40),22);
            if(game.Phase==DuelPhase.Prepare)
                preparationCount=Text("3",page,Vector2.zero,new Vector2(300,100),72);
            LateUpdate();
        }
        void LateUpdate()
        {
            if(playerHealth==null||game.Player==null)return;
            if(preparationCount!=null)
            {
                int count=Mathf.CeilToInt(game.PreparationRemaining);
                if(count!=lastPreparationCount) {lastPreparationCount=count;preparationCount.text=count.ToString();}
            }
            playerHealth.text="YOU  "+game.Player.CurrentHealth;
            for (int i=0; i<policeHealth.Length && i<game.Bosses.Count; i++)
            {
                var actor=game.Bosses[i];
                if(actor==null)continue;
                policeHealth[i].text=(actor.ActorId==1?"GUNNER  ":"SWORDSMAN  ")+actor.Health.CurrentHealth;
                policeFill[i].rectTransform.sizeDelta=new Vector2(430f*actor.Health.CurrentHealth/actor.Health.MaxHealth,12);
            }

            string ammo=game.Rifle.IsReloading?"Reloading":$"Ammo {game.Rifle.AmmoInMagazine} / {game.Rifle.MagazineSize}";
            resources.text=$"{ammo}     Dodge: {Ready(game.Dodge.CooldownRemaining)}";
            playerFill.rectTransform.sizeDelta=new Vector2(430f*game.Player.CurrentHealth/game.Player.MaxHealth,12);
        }
        string Ready(float remaining)=>remaining<=.01f?"Ready":$"{remaining:0.0}s";
    }
}
