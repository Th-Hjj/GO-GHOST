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
        TMP_Text playerHealth;
        TMP_Text preparationCount;
        int lastPreparationCount;
        TMP_Text[] policeHealth = System.Array.Empty<TMP_Text>();
        UnityEngine.UI.Graphic[] policeFill = System.Array.Empty<UnityEngine.UI.Graphic>();
        UnityEngine.UI.Graphic playerFill;
        RectTransform trainingBack;

        void BuildTrainingButtons()
        {
            // Back sits above the rule sidebar, or in the arena corner when there are no rules.
            var parent = ruleSidebar != null ? ruleSidebar : battleUi != null ? battleUi : page;
            var back = OvButton("« Back", parent, Vector2.zero, new Vector2(150, 52), game.BackFromTraining, OverrideButtonKind.Tertiary, 30,
                align: TextAlignmentOptions.Capline);
            back.name = "← Back";
            trainingBack = back.GetComponent<RectTransform>();
            trainingBack.anchorMin = trainingBack.anchorMax = trainingBack.pivot = new Vector2(0, 1);
            trainingBack.anchoredPosition = new Vector2(24, -28);
            dialogueExits.Add(trainingBack);
            // Pause's backdrop belongs to Page. Keep the same navigation available above it.
            if (game.Paused) trainingBack.SetParent(page, true);
        }

        public bool TrainingOwnsPointer()
        {
            if (game.PendingDialogue != null) return true;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return false;
            var point = mouse.position.ReadValue();
            return trainingBack != null && RectTransformUtility.RectangleContainsScreenPoint(trainingBack, point);
        }
        public void Initialize(DuelGame game, TMP_FontAsset font, TMP_FontAsset controlFont=null,
            TMP_FontAsset headingFont=null, TMP_FontAsset numericFont=null)
        {
            this.game = game; this.font = font;
            this.controlFont=controlFont!=null?controlFont:font;
            this.headingFont=headingFont!=null?headingFont:font;
            this.numericFont=numericFont!=null?numericFont:font;
            LoadOverrideFonts();
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
        UnityEngine.UI.Button Button(string title,Transform parent,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction click,bool selected=false,DuelControlStyle style=DuelControlStyle.Standard)
        {
            var r=Rect(title,parent,pos,size); var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color=NeuralPalette.Panel; image.raycastTarget=true;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic=image; b.onClick.AddListener(click);
            var c=b.colors; c.normalColor=Color.white; c.highlightedColor=Color.white; c.selectedColor=Color.white; c.disabledColor=Color.white; c.pressedColor=NeuralPalette.Secondary; b.colors=c;
            var t=Text(title,r,Vector2.zero,size,24,face:controlFont);
            var outline=r.gameObject.AddComponent<DuelButtonOutline>();outline.Owner=this;outline.Persistent=selected;outline.Style=style;outline.Button=b;outline.Label=t;
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
            BuildPage();
            NoteDialoguePage();
        }

        void BuildPage()
        {
            CaptureConfigurationView();
            CaptureHelpView();
            if(game.Phase!=DuelPhase.Configure||helpDraft!=game.DraftLoadout)helpOpen=false;
            ConfigurePageScale();
            sceneDialogue = null; sceneContinue = null;
            if(page!=null){page.gameObject.SetActive(false);Destroy(page.gameObject);}
            page=Rect("Page",canvas,Vector2.zero,new Vector2(1600,900));
            pageControls=page.gameObject.AddComponent<CanvasGroup>();
            developerFields.Clear();
            dialogueExits.Clear();
            ClearTutorialView();
            trainingBack=null;
            ClearRuleHud();BuildRuleHud();UpdateBattleLayout();
            playerHealth=null; playerFill=null;
            preparationCount=null; waveTitle=null; lastPreparationCount=-1;
            ResetBattleStatus();
            policeHealth=System.Array.Empty<TMP_Text>(); policeFill=System.Array.Empty<UnityEngine.UI.Graphic>();
            if(game.DeveloperToolsOpen)
            {
                if(ruleSidebar!=null)ruleSidebar.gameObject.SetActive(false);
                BuildDeveloperTools();return;
            }
            // Settings and its pages open over the title and over Pause only.
            if(overlay!=OverlayPage.None&&game.Phase!=DuelPhase.Menu&&!game.Paused)overlay=OverlayPage.None;
            if(game.Phase==DuelPhase.Menu&&overlay==OverlayPage.None&&!GameSettings.NoticeSeen)overlay=OverlayPage.Notice;
            if(overlay!=OverlayPage.None){BuildOverrideOverlay();return;}
            if(game.Phase==DuelPhase.Menu||game.Phase==DuelPhase.Configure||game.Phase==DuelPhase.CharacterCreation||game.Phase==DuelPhase.TargetProfile)
                Image(page,Vector2.zero,new Vector2(4000,4000),NeuralPalette.Background);
            if(game.Paused&&game.Phase!=DuelPhase.Fight&&game.Phase!=DuelPhase.Prepare&&game.Phase!=DuelPhase.Training){BuildOverridePause();return;}
            if(game.Phase==DuelPhase.TargetProfile){BuildTargetProfile();return;}
            if(game.Phase==DuelPhase.MissionEntry)return;
            if(game.Phase==DuelPhase.FinalVictoryHold)return;
            if(game.Phase!=DuelPhase.CharacterCreation)ForgetCreationWords();
            if(game.Paused){BuildOverridePause();return;}
            if(game.Phase==DuelPhase.Menu){BuildOverrideMenu();return;}
            if(game.Phase==DuelPhase.CharacterCreation)
            {
                BuildCharacterCreation();return;
            }
            if(game.Phase==DuelPhase.Configure)
            {
                if(helpOpen){BuildHelp();return;}
                BuildConditionalLoadout();RestoreConfigurationView();return;
            }
            if(game.Phase==DuelPhase.Between){BuildOverrideCleared();return;}
            if(game.Phase==DuelPhase.Complete && game.Wave==DuelGame.FinalBossWave){BuildOverrideFinished();return;}
            if(game.Phase==DuelPhase.Failed || game.Phase==DuelPhase.Complete){BuildOverrideOutcome();return;}
            var hud=battleUi!=null?battleUi:page;
            BuildOverrideHud(hud);
            BuildFinalBossCutscene(hud);
            if(game.InvasionHeld)
            {
                var dim=Image(hud,Vector2.zero,Vector2.zero,NeuralPalette.Alpha(NeuralPalette.Background,.20f)).rectTransform;
                dim.anchorMin=Vector2.zero; dim.anchorMax=Vector2.one;
                dim.SetAsFirstSibling();
                if(game.PendingDialogue!=null)
                {
                    OvSceneBox(hud,760,game.HappySpeakerName,game.PendingDialogue,true,"Invasion caption");
                }
            }
            if(game.Phase==DuelPhase.Training)BuildTrainingButtons();
            LateUpdate();
        }
        void LateUpdate()
        {
            if (game == null) return;
            UpdateDialogueOverlay();
            UpdateCreationWords();
            UpdateHurtFlash();
            UpdateOverlayInput();
            if (game.DeveloperToolsOpen) return;
            if (game.Phase==DuelPhase.TargetProfile && profileResolution != new Vector2Int(Screen.width,Screen.height)) {Show();return;}
            if (RefreshConfigurationLayout()) return;
            UpdateRuleHud();
            UpdateBattleStatus();
        }
    }
    public enum DuelControlStyle { Standard, Navigation, Option, Preview, Tool, Primary }

    public sealed class DuelButtonOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, ISelectHandler, IDeselectHandler, IMoveHandler
    {
        internal DuelUI Owner;
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image[] Edges;
        public UnityEngine.UI.Image Selection;
        public TMP_Text Label;
        public NeuralBodySystem LabelSystem;
        public bool Persistent, Primary, Subtle, TextRegion;
        public DuelControlStyle Style;
        public bool Placeholder;
        public bool KeyboardFocused => focused;
        bool hovered,focused;
        int lastState=-1;
        float lastScale;
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(Edges==null||Button==null)return;
            // Keep controls visually quiet until the dialogue closes, including gaps between recordings.
            bool available=Owner != null && Owner.TutorialHighlightsControl(Button)
                ? Button.interactable : Button.IsInteractable() && !(Owner != null && Owner.DialogueVisible);
            float scale=Mathf.Max(.01f,transform.lossyScale.x);
            int state=(available?1:0)|(hovered?2:0)|(focused?4:0)|(Persistent?8:0)|(Primary?16:0)|(Subtle?32:0)|(TextRegion?64:0)|((int)LabelSystem<<7)|((int)Style<<10)|(Placeholder?8192:0);
            if(state==lastState&&Mathf.Approximately(scale,lastScale))return;
            lastState=state;lastScale=scale;
            var color=!available?NeuralPalette.Border:focused||Primary?NeuralPalette.Primary:
                hovered?NeuralPalette.Secondary:Subtle?Color.Lerp(NeuralPalette.Border,NeuralPalette.ControlBorder,.5f):NeuralPalette.ControlBorder;
            if(TextRegion&&!focused&&!hovered&&!Persistent)color=Color.clear;
            if(Style!=DuelControlStyle.Standard)
            {
                bool quiet=Style==DuelControlStyle.Navigation||Style==DuelControlStyle.Tool;
                color=available&&focused?NeuralPalette.Primary:
                    available&&hovered?NeuralPalette.ControlBorder:
                    Style==DuelControlStyle.Preview?NeuralPalette.Border:Color.clear;
                var surface=Button.targetGraphic;
                if(surface!=null)surface.color=Style==DuelControlStyle.Primary
                    ?available?NeuralPalette.Primary:NeuralPalette.Panel
                    :available&&(hovered||Persistent)?Color.Lerp(NeuralPalette.Panel,NeuralPalette.Primary,Persistent?.10f:.06f)
                    :quiet?Color.clear:NeuralPalette.Panel;
            }
            float pixels=Mathf.Max(2,Mathf.Round(2*scale));
            if(available&&focused)pixels=Mathf.Max(pixels+1,Mathf.Round(3*scale));
            for(int i=0;i<Edges.Length;i++)
            {Edges[i].color=color;Edges[i].rectTransform.sizeDelta=i<2?new Vector2(0,pixels/scale):new Vector2(pixels/scale,0);}
            if(Label!=null)Label.color=!available?NeuralPalette.DisabledText:LabelSystem!=NeuralBodySystem.None?NeuralPalette.Body(LabelSystem):
                Style==DuelControlStyle.Primary?NeuralPalette.Panel:
                Style!=DuelControlStyle.Standard?Placeholder||Style==DuelControlStyle.Tool&&!hovered&&!focused?NeuralPalette.Secondary:NeuralPalette.Primary:
                Subtle&&!TextRegion&&!hovered&&!focused?NeuralPalette.Secondary:NeuralPalette.Primary;
            if(Selection!=null)
            {
                bool primaryFocus=Style==DuelControlStyle.Primary&&focused;
                Selection.enabled=available&&(Persistent||primaryFocus);
                Selection.color=primaryFocus?NeuralPalette.Panel:NeuralPalette.Primary;
                if(Style!=DuelControlStyle.Standard)
                {
                    Selection.rectTransform.anchoredPosition=new Vector2(primaryFocus?8:2,0);
                    Selection.rectTransform.sizeDelta=new Vector2(2,Mathf.Max(18,((RectTransform)transform).rect.height-24));
                }
            }
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
