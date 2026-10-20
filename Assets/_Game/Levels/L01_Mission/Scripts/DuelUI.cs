using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace GoGhost.Duel
{
    [DefaultExecutionOrder(-200)]
    public sealed partial class DuelUI : MonoBehaviour
    {
        DuelGame game;
        TMP_FontAsset font, controlFont, headingFont, numericFont;
        RectTransform canvas, page;
        TMP_Text playerHealth, resources;
        TMP_Text preparationCount;
        int lastPreparationCount;
        TMP_Text[] policeHealth = System.Array.Empty<TMP_Text>();
        UnityEngine.UI.Image[] policeFill = System.Array.Empty<UnityEngine.UI.Image>();
        UnityEngine.UI.Image playerFill;
        RectTransform trainingBack, trainingContinue;

        void BuildTrainingButtons()
        {
            var parent = battleUi != null ? battleUi : page;
            trainingBack = Button("← Back", parent, Vector2.zero, new Vector2(150, 48), game.BackFromTraining).GetComponent<RectTransform>();
            trainingBack.anchorMin = trainingBack.anchorMax = trainingBack.pivot = new Vector2(0, 1);
            trainingBack.anchoredPosition = new Vector2(24, -24);
            trainingContinue = Button("Continue →", parent, Vector2.zero, new Vector2(180, 48), game.ContinueFromTraining).GetComponent<RectTransform>();
            trainingContinue.anchorMin = trainingContinue.anchorMax = trainingContinue.pivot = new Vector2(1, 0);
            trainingContinue.anchoredPosition = new Vector2(-24, 24);
            // Pause's backdrop belongs to Page. Keep the same navigation available above it.
            if (game.Paused) parent.SetAsLastSibling();
        }

        public bool TrainingOwnsPointer()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            var point = mouse.position.ReadValue();
            return trainingBack != null && RectTransformUtility.RectangleContainsScreenPoint(trainingBack, point)
                || trainingContinue != null && RectTransformUtility.RectangleContainsScreenPoint(trainingContinue, point);
        }
        public void Initialize(DuelGame game, TMP_FontAsset font, TMP_FontAsset controlFont=null,
            TMP_FontAsset headingFont=null, TMP_FontAsset numericFont=null)
        {
            this.game = game; this.font = font;
            this.controlFont=controlFont!=null?controlFont:font;
            this.headingFont=headingFont!=null?headingFont:font;
            this.numericFont=numericFont!=null?numericFont:font;
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
        TMP_Text Text(string value,Transform parent,Vector2 pos,Vector2 size,int points=26,TextAlignmentOptions align=TextAlignmentOptions.Center,TMP_FontAsset face=null)
        {
            var r=Rect(value,parent,pos,size); var t=r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font=face!=null?face:font; t.text=value; t.fontSize=points; t.enableAutoSizing=false; t.color=NeuralPalette.Primary; t.alignment=align;
            t.raycastTarget=false; t.richText=false; t.textWrappingMode=TextWrappingModes.Normal; return t;
        }
        // Measure once while building a view. Emphasis must not resize a live row or shrink its text.
        float TextHeight(TMP_Text text,float width)
        {
            float height=text.GetPreferredValues(text.text,width,Mathf.Infinity).y;
            return Mathf.Ceil(height)+2;
        }
        UnityEngine.UI.Image Image(Transform parent,Vector2 pos,Vector2 size,Color color)
        {
            var r=Rect("Fill",parent,pos,size); var img=r.gameObject.AddComponent<UnityEngine.UI.Image>(); img.color=color; img.raycastTarget=false; return img;
        }
        UnityEngine.UI.Button Button(string title,Transform parent,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction click,bool selected=false)
        {
            var r=Rect(title,parent,pos,size); var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color=NeuralPalette.Panel; image.raycastTarget=true;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic=image; b.onClick.AddListener(click);
            var c=b.colors; c.normalColor=Color.white; c.highlightedColor=Color.white; c.selectedColor=Color.white; c.disabledColor=Color.white; c.pressedColor=NeuralPalette.Secondary; b.colors=c;
            var t=Text(title,r,Vector2.zero,size,24,face:controlFont);
            var outline=r.gameObject.AddComponent<DuelButtonOutline>();outline.Persistent=selected;outline.Button=b;outline.Label=t;
            outline.Edges=new UnityEngine.UI.Image[4];
            for(int i=0;i<4;i++)
            {
                var edge=Image(r,Vector2.zero,Vector2.zero,NeuralPalette.ControlBorder);var er=edge.rectTransform;
                er.anchorMin=i<2?new Vector2(0,i):new Vector2(i-2,0);er.anchorMax=i<2?new Vector2(1,i):new Vector2(i-2,1);
                // Keep the entire stroke inside the control, including at a masked viewport edge.
                er.pivot=i<2?new Vector2(.5f,i):new Vector2(i-2,.5f);
                er.sizeDelta=i<2?new Vector2(0,2):new Vector2(2,0);outline.Edges[i]=edge;
            }
            outline.Selection=Image(r,new Vector2(5,0),new Vector2(3,18),NeuralPalette.Primary);
            outline.Selection.rectTransform.anchorMin=outline.Selection.rectTransform.anchorMax=new Vector2(0,.5f);
            outline.Refresh();
            return b;
        }
        public void Show()
        {
            if (canvas == null) return;
            CaptureConfigurationView();
            ConfigurePageScale();
            if(page!=null){page.gameObject.SetActive(false);Destroy(page.gameObject);}
            page=Rect("Page",canvas,Vector2.zero,new Vector2(1600,900));
            developerFields.Clear();
            trainingBack=trainingContinue=null;
            ClearRuleHud();BuildRuleHud();UpdateBattleLayout();
            playerHealth=resources=null; playerFill=null;
            preparationCount=null; lastPreparationCount=-1;
            ResetBattleStatus();
            policeHealth=System.Array.Empty<TMP_Text>(); policeFill=System.Array.Empty<UnityEngine.UI.Image>();
            if(game.DeveloperToolsOpen)
            {
                if(ruleSidebar!=null)ruleSidebar.gameObject.SetActive(false);
                BuildDeveloperTools();return;
            }
            if(game.Phase==DuelPhase.Menu||game.Phase==DuelPhase.Configure)
                Image(page,Vector2.zero,new Vector2(4000,4000),NeuralPalette.Background);
            if(game.Paused)
            {
                Image(page,Vector2.zero,new Vector2(4000,4000),new Color(0,0,0,.88f));
                Button("Resume",page,new Vector2(0,45),new Vector2(330,65),()=>game.SetPause(false));
                Button("Main Menu",page,new Vector2(0,-45),new Vector2(330,65),game.MainMenu);
                if(game.Phase==DuelPhase.Training)BuildTrainingButtons();
                if(ruleSidebar!=null)ruleSidebar.SetAsLastSibling();return;
            }
            if(game.Phase==DuelPhase.Menu)
            {
                Text("GO GHOST",page,new Vector2(0,150),new Vector2(1000,120),78,face:headingFont);
                Button("New Game",page,Vector2.zero,new Vector2(350,68),game.NewGame);
                Button("Quit",page,new Vector2(0,-95),new Vector2(350,68),Application.Quit); return;
            }
            if(game.Phase==DuelPhase.Configure)
            {
                BuildConditionalLoadout();RestoreConfigurationView();return;
            }
            if(game.Phase==DuelPhase.Failed || game.Phase==DuelPhase.Complete)
            {
                Image(page,Vector2.zero,new Vector2(4000,4000),new Color(0,0,0,.86f));
                Text(game.Phase==DuelPhase.Failed?"DEFEAT":"COMPLETE",page,new Vector2(0,160),new Vector2(800,90),64,face:headingFont);
                if(game.Phase==DuelPhase.Failed)Button("Reconfigure",page,Vector2.zero,new Vector2(360,65),game.Reconfigure);
                else
                {
                    Button("Restart",page,Vector2.zero,new Vector2(360,65),game.NewGame);
                    Button("Main Menu",page,new Vector2(0,-90),new Vector2(360,65),game.MainMenu);
                }
                return;
            }
            var hud=battleUi!=null?battleUi:page;
            Text(game.Phase==DuelPhase.Training?"TRAINING":"WAVE "+game.Wave,hud,new Vector2(0,400),new Vector2(280,50),30,face:headingFont);
            policeHealth = new TMP_Text[game.Bosses.Count];
            policeFill = new UnityEngine.UI.Image[game.Bosses.Count];
            bossHealthText = new HealthTextState[game.Bosses.Count];
            for (int i=0; i<game.Bosses.Count; i++)
            {
                float y=345-i*40;
                policeHealth[i]=Text("",hud,new Vector2(-400,y),new Vector2(300,40),24,TextAlignmentOptions.Left,numericFont);
                Image(hud,new Vector2(0,y),new Vector2(430,12),NeuralPalette.Border);
                var color=game.Bosses[i].IsTrainingEnemy?NeuralPalette.Secondary:game.Bosses[i].Profile.Kind==BossKind.Refraction?NeuralPalette.Refraction:NeuralPalette.Severance;
                policeFill[i]=Image(hud,new Vector2(-215,y),new Vector2(430,12),color);
                policeFill[i].rectTransform.pivot=new Vector2(0,.5f);
            }

            playerHealth=Text("",hud,new Vector2(-400,-360),new Vector2(300,40),24,TextAlignmentOptions.Left,numericFont);
            Image(hud,new Vector2(0,-360),new Vector2(430,12),NeuralPalette.Border);
            playerFill=Image(hud,new Vector2(-215,-360),new Vector2(430,12),NeuralPalette.Primary);
            playerFill.rectTransform.pivot=new Vector2(0,.5f);
            resources=Text("",hud,new Vector2(0,-397),new Vector2(430,40),22,face:numericFont);
            if(game.Phase==DuelPhase.Prepare)
                preparationCount=Text("3",hud,Vector2.zero,new Vector2(300,100),72,face:numericFont);
            if(game.Phase==DuelPhase.Training)BuildTrainingButtons();
            LateUpdate();
        }
        void LateUpdate()
        {
            if(game==null||game.DeveloperToolsOpen)return;
            if (RefreshConfigurationLayout()) return;
            UpdateRuleHud();
            UpdateBattleStatus();
        }
    }
    public sealed class DuelButtonOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, ISelectHandler, IDeselectHandler, IMoveHandler
    {
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image[] Edges;
        public UnityEngine.UI.Image Selection;
        public TMP_Text Label;
        public bool Persistent, Primary, Subtle, TextRegion;
        public bool KeyboardFocused => focused;
        bool hovered,focused;
        int lastState=-1;
        float lastScale;
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(Edges==null||Button==null)return;
            bool available=Button.IsInteractable();
            float scale=Mathf.Max(.01f,transform.lossyScale.x);
            int state=(available?1:0)|(hovered?2:0)|(focused?4:0)|(Persistent?8:0)|(Primary?16:0)|(Subtle?32:0)|(TextRegion?64:0);
            if(state==lastState&&Mathf.Approximately(scale,lastScale))return;
            lastState=state;lastScale=scale;
            var color=!available?NeuralPalette.Border:focused||Primary?NeuralPalette.Primary:
                hovered?NeuralPalette.Secondary:Subtle?Color.Lerp(NeuralPalette.Border,NeuralPalette.ControlBorder,.5f):NeuralPalette.ControlBorder;
            if(TextRegion&&!focused&&!hovered&&!Persistent)color=Color.clear;
            float pixels=Mathf.Max(2,Mathf.Round(2*scale));
            if(available&&focused)pixels=Mathf.Max(pixels+1,Mathf.Round(3*scale));
            for(int i=0;i<Edges.Length;i++)
            {Edges[i].color=color;Edges[i].rectTransform.sizeDelta=i<2?new Vector2(0,pixels/scale):new Vector2(pixels/scale,0);}
            if(Label!=null)Label.color=!available?NeuralPalette.DisabledText:Subtle&&!TextRegion&&!hovered&&!focused?NeuralPalette.Secondary:NeuralPalette.Primary;
            if(Selection!=null)Selection.enabled=available&&Persistent;
        }
        public void OnPointerDown(PointerEventData data){focused=false;Refresh();}
        public void OnPointerEnter(PointerEventData data){hovered=true;Refresh();}
        public void OnPointerExit(PointerEventData data){hovered=false;Refresh();}
        public void OnSelect(BaseEventData data){focused=true;Refresh();KeepVisible();}
        void KeepVisible()
        {
            var scroll=GetComponentInParent<UnityEngine.UI.ScrollRect>();if(scroll==null||scroll.content==null||scroll.viewport==null)return;
            Canvas.ForceUpdateCanvases();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,transform);
            var area=scroll.viewport.rect;float delta=bounds.max.y>area.yMax?area.yMax-bounds.max.y:bounds.min.y<area.yMin?area.yMin-bounds.min.y:0;
            if(Mathf.Abs(delta)<.01f)return;
            var pos=scroll.content.anchoredPosition;pos.y+=delta;
            pos.y=Mathf.Clamp(pos.y,0,Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height));
            scroll.content.anchoredPosition=pos;scroll.StopMovement();
        }
        public void OnDeselect(BaseEventData data){focused=false;Refresh();}
        public void OnMove(AxisEventData data)
        {
            // Button may have already moved EventSystem selection before this handler runs.
            focused=EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject==gameObject;
            Refresh();
        }
    }
}
