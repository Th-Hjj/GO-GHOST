#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

// Shared fixtures for current bounded checks and retained historical validation.
public static partial class BossEncounterSmoke
{
    static void V2Rule(ConditionalLoadout draft,ConditionalRule rule)
    {
        if(!draft.TryAddRule(ConditionalCatalog.Response(rule.Action).Operation,rule))
            throw new InvalidOperationException("Invalid V2 fixture: "+ConditionalCatalog.RuleConditionLabel(rule));
    }
    static ConditionalRule V2Ground(string action,GroundStateOption ground=GroundStateOption.Grounded)
        =>new ConditionalRule{Condition=51,GroundState=ground,Action=action};
    sealed class RuleCardsBurstProbe : IDisposable
    {
        readonly ConditionalController controller;
        readonly PlayerShooting rifle;
        public readonly System.Collections.Generic.HashSet<long> Shots=new System.Collections.Generic.HashSet<long>();
        public readonly System.Collections.Generic.HashSet<long> Bursts=new System.Collections.Generic.HashSet<long>();
        public int Callbacks, Reloads, AcceptedBudget=-1;
        public bool BurstPresentAtReload;
        public readonly System.Collections.Generic.List<float> ShotTimes=new System.Collections.Generic.List<float>();
        public RuleCardsBurstProbe(ConditionalController controller,PlayerShooting rifle)
        {this.controller=controller;this.rifle=rifle;rifle.ShotFired+=OnShot;rifle.ReloadStarted+=OnReload;}
        void OnShot(Vector2 origin,Vector2 direction){Callbacks++;ShotTimes.Add(Time.time);Sample();}
        void OnReload(){Reloads++;Sample();BurstPresentAtReload=Bursts.Count==1;}
        public void Sample()
        {
            foreach(object notice in Private<IList>(controller,"events"))
            {
                var type=notice.GetType();int condition=(int)type.GetField("Condition").GetValue(notice);
                var context=(ConditionalController.TriggerContext)type.GetField("Context").GetValue(notice);
                if(condition==59)Shots.Add(context.Occurrence);if(condition==60)Bursts.Add(context.Occurrence);
            }
            if(AcceptedBudget<0)
            {
                object response=Private<IList>(controller,"running")[3];var type=response.GetType();
                if((bool)type.GetField("Accepted").GetValue(response))
                    AcceptedBudget=(int)type.GetField("ShotLimit").GetValue(response);
            }
        }
        public void Dispose(){rifle.ShotFired-=OnShot;rifle.ReloadStarted-=OnReload;}
    }
    static IEnumerator RuleCardsSample(RuleCardsBurstProbe probe,float seconds,Func<bool> stop=null)
    {
        float end=Time.time+seconds;
        while(Time.time<end&&(stop==null||!stop())){probe.Sample();yield return null;}
        probe.Sample();
    }
    static void RuleCardsAmmo(int amount)
        =>typeof(PlayerShooting).GetField("<AmmoInMagazine>k__BackingField",PrivateInstance).SetValue(game.Rifle,amount);
    static void Rule(ConditionalLoadout d,int op,int condition,string response)
    {d.Operations[op].Rules.Add(new ConditionalRule{Condition=condition,Action=response});}
    static IEnumerator AlphaFresh(int wave,Action<ConditionalLoadout> configure,bool stop=true)
    {
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;game.ConfigurePlaytestWave(wave);configure?.Invoke(game.DraftLoadout);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        if(stop)StopPolicies();yield return Frames(4);
    }
    static ConditionalController.ExecutionFeedback Feedback(int op,int row=0)=>game.Rules.Feedback((ControlOperation)op,row);
    static UnityEngine.UI.Button ConfigButtonNamed(string name,string label=null)
    {
        return Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>()
            .First(b=>b.gameObject.activeInHierarchy&&b.name==name&&(label==null||b.GetComponentInChildren<TMPro.TMP_Text>().text==label));
    }
    static void ClickConfig(string name,string label=null)=>ConfigButtonNamed(name,label).onClick.Invoke();
    static void AlphaPlace(float playerX,float bossX)
    {
        OnFloor(game.Player.gameObject,game.Bosses[0].Navigation,playerX);
        OnFloor(game.Bosses[0].gameObject,game.Bosses[0].Navigation,bossX);
    }
    static void V2PreparePlace(GameObject actor,ArenaNavigation2D navigation,float x)
    {
        OnFloor(actor,navigation,x);
        // Preparation disables simulation; also update the Transform so interpolation cannot
        // restore the original spawn when the real fight enables its Rigidbody again.
        var position=actor.GetComponent<Rigidbody2D>().position;
        actor.transform.position=new Vector3(position.x,position.y,actor.transform.position.z);
        Physics2D.SyncTransforms();
    }
    static UnityEngine.UI.Button[] CatalogOptions()=>Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.name=="Option").ToArray();
    static string OptionLabel(UnityEngine.UI.Button b)=>b.GetComponentInChildren<TMPro.TMP_Text>().text.Split('\n')[0];
    static IEnumerator SelectEditorCondition(int id)
    {
        ClickConfig("ConditionCategory"+ConditionalCatalog.Condition(id).Category);yield return Frames(2);
        ClickConfig("Option",ConditionalCatalog.Condition(id).Label);yield return Frames(2);
    }
    static IEnumerator AddEditorRule(int condition,string action)
    {
        ClickConfig("AddRule");yield return Frames(2);yield return SelectEditorCondition(condition);
        ClickConfig("ActionCategory"+(int)ConditionalCatalog.Response(action).Operation);yield return Frames(2);
        ClickConfig("Option",ConditionalCatalog.Response(action).Label);yield return Frames(2);
    }
    static IEnumerator PointerConfig(string name,string label=null)
    {
        mousePosition=RectTransformUtility.WorldToScreenPoint(null,ConfigButtonNamed(name,label).transform.position);
        mouseHeld=false;yield return Frames(3);mouseHeld=true;yield return Frames(3);mouseHeld=false;yield return Frames(3);
    }
    static TMPro.TMP_Text EditorText(string name)=>Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name==name);
    static string[] EquippedActions()=>game.DraftLoadout.OrderedRules().Select(r=>r.Action).ToArray();
    static void SetAlphaGameSize(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.FlattenHierarchy;
        object sizes=sizesType.GetProperty("instance",flags).GetValue(null);
        object groupType=sizesType.GetProperty("currentGroupType",flags).GetValue(sizes);
        object group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{groupType});
        var groupClass=group.GetType();int builtin=(int)groupClass.GetMethod("GetBuiltinCount",flags).Invoke(group,null);
        int custom=(int)groupClass.GetMethod("GetCustomCount",flags).Invoke(group,null);
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
        object size=Activator.CreateInstance(sizeType,flags,null,new[]{Enum.ToObject(kind,1),(object)width,height,"Alpha50 validation"},null);
        groupClass.GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        var selected=view.GetType().GetProperty("selectedSizeIndex",flags);int original=(int)selected.GetValue(view);
        selected.SetValue(view,builtin+custom);view.Repaint();
        restoration.Add(()=>{if(view!=null)selected.SetValue(view,original);groupClass.GetMethod("RemoveCustomSize",flags).Invoke(group,new object[]{builtin+custom});});
    }
    static string V2UIFields(ConditionalRule rule)
    {var copy=rule.Copy();copy.DisplayOrder=-1;return JsonUtility.ToJson(copy);}
    static ConditionalRule V2UIPending()=>Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule");
    static string[] V2UIRules()=>game.DraftLoadout.OrderedRules().Select(V2UIFields).ToArray();
    static IEnumerator V2UISelect(int condition,int option=-1)
    {
        yield return SelectEditorCondition(condition);
        if(option>=0){ClickConfig("RuleParameter"+option);yield return Frames(2);}
    }
    static IEnumerator V2UIAction(string action,int option=-1)
    {
        ClickConfig("ActionCategory"+(int)ConditionalCatalog.Response(action).Operation);yield return Frames(2);
        ClickConfig("Option",ConditionalCatalog.Response(action).Label);yield return Frames(2);
        if(option>=0){ClickConfig("RuleParameter"+option);yield return Frames(2);}
        if(ConditionalCatalog.UsesAmmoOption(action)){ClickConfig("UseAction");yield return Frames(2);}
    }
    static DuelUI CardsUI()=>Object.FindAnyObjectByType<DuelUI>();
    static TMPro.TMP_Text CardsText(Transform root,string name)=>root.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name==name);
    static string CardsPreview(string name)=>ConfigButtonNamed(name).GetComponentInChildren<TMPro.TMP_Text>().text;
    static RectTransform CardsRect(string name)=>CardsUI().GetComponentsInChildren<RectTransform>().First(r=>r.name==name);
    static UnityEngine.UI.ScrollRect CardsScroll(bool combat)=>CardsUI().GetComponentsInChildren<UnityEngine.UI.ScrollRect>()
        .First(s=>s.name==(combat?"BattleRuleScroll":"EquippedRules"));
    static RectTransform[] CardsRows(UnityEngine.UI.ScrollRect scroll)=>scroll.content.Cast<Transform>().OfType<RectTransform>()
        .Where(r=>r.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.name=="ConditionText")).ToArray();
    static bool CardsInside(RectTransform outer,RectTransform inner,float padding=1)
    {
        var corners=new Vector3[4];inner.GetWorldCorners(corners);var bounds=outer.rect;
        bounds.xMin-=padding;bounds.xMax+=padding;bounds.yMin-=padding;bounds.yMax+=padding;
        return corners.All(c=>bounds.Contains(outer.InverseTransformPoint(c)));
    }
    static bool CardsOnScreen(RectTransform rect)
    {var corners=new Vector3[4];rect.GetWorldCorners(corners);return corners.All(c=>c.x>=-.5f&&c.x<=Screen.width+.5f&&c.y>=-.5f&&c.y<=Screen.height+.5f);}
    static void CardsLayout(bool combat,int count,string context)
    {
        Canvas.ForceUpdateCanvases();var scroll=CardsScroll(combat);var cards=CardsRows(scroll);
        Check(context+": cards derive from actual view collection",cards.Length==count,$"cards={cards.Length} expected={count}");
        bool fits=true,numbered=true,noOverlap=true;
        for(int i=0;i<cards.Length;i++)
        {
            var card=cards[i];numbered&=CardsText(card,"RuleNumber").text==(i+1).ToString();
            foreach(var text in card.GetComponentsInChildren<TMPro.TMP_Text>())
            {text.ForceMeshUpdate();fits&=!text.isTextOverflowing&&!text.enableAutoSizing&&CardsInside(card,text.rectTransform);}
            if(i+1<cards.Length)
            {
                var low=new Vector3[4];var next=new Vector3[4];card.GetWorldCorners(low);cards[i+1].GetWorldCorners(next);
                noOverlap&=low[0].y+.5f>=next[1].y;
            }
        }
        Check(context+": fixed fonts and complete text remain inside each card",fits);
        Check(context+": continuous global numbers and nonoverlapping measured rows",numbered&&noOverlap);
        Check(context+": masked vertical viewport and full content height",scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>()!=null
            &&scroll.vertical&&!scroll.horizontal&&cards.All(c=>CardsInside(scroll.content,c,2))
            &&scroll.content.rect.height+2>=cards.Sum(c=>c.rect.height),$"content={scroll.content.rect.height:F1} viewport={scroll.viewport.rect.height:F1}");
        bool overflow=scroll.content.rect.height>scroll.viewport.rect.height+1;
        Check(context+": scrollbar follows overflow only",scroll.verticalScrollbar!=null
            &&scroll.verticalScrollbarVisibility==UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide
            &&scroll.verticalScrollbar.gameObject.activeInHierarchy==overflow,$"overflow={overflow}");
        if(!combat)
        {
            var preview=CardsRect("RulePreview");var add=(RectTransform)ConfigButtonNamed("AddRule").transform;
            var start=(RectTransform)ConfigButtonNamed("StartFight").transform;
            Check(context+": preview and bottom controls remain fixed and reachable",CardsOnScreen(preview)&&CardsOnScreen(add)&&CardsOnScreen(start)
                &&preview.GetComponentInParent<UnityEngine.UI.ScrollRect>()==null&&add.GetComponentInParent<UnityEngine.UI.ScrollRect>()==null
                &&start.GetComponentInParent<UnityEngine.UI.ScrollRect>()==null);
            Check(context+": body-selector-rules relationship preserved",CardsRect("BodyPreview").position.x<CardsRect("RuleEditor").position.x
                &&CardsRect("RuleEditor").position.x<CardsRect("RuleList").position.x);
        }
        else
        {
            float panel=CardsRect("BattleRules").rect.width;
            Check(context+": HUD width is bounded independently of rule count",panel>=320-.1f&&panel<=410+.1f,$"width={panel:F1}");
        }
    }
    static void CardsSetSample(bool combat,System.Collections.Generic.IReadOnlyList<ConditionalRule> sample)
        =>typeof(DuelUI).GetMethod(combat?"SetRuleHudViewSample":"SetRuleSetupViewSample",PrivateInstance).Invoke(CardsUI(),new object[]{sample});
    static System.Collections.Generic.List<ConditionalRule> CardsSample(int count,System.Collections.Generic.IReadOnlyList<ConditionalRule> source)
    {
        var result=new System.Collections.Generic.List<ConditionalRule>();
        for(int i=0;i<count;i++){var rule=source[i%source.Count].Copy();rule.DisplayOrder=i;result.Add(rule);}return result;
    }
    static string CardsGeometry(RectTransform root)
        =>root.rect.size+"|"+string.Join("|",root.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>{
            t.ForceMeshUpdate();return t.name+":"+t.fontSize+":"+t.fontStyle+":"+t.rectTransform.rect.size+":"+t.rectTransform.anchoredPosition+":"+t.textInfo.lineCount;}));
    static object CardsHudRow(ControlOperation operation,int row)
        =>Private<IList>(CardsUI(),"ruleRows").Cast<object>().First(r=>(ControlOperation)r.GetType().GetField("Operation").GetValue(r)==operation
            &&(int)r.GetType().GetField("Row").GetValue(r)==row);
    static T CardsField<T>(object row,string field)=>(T)row.GetType().GetField(field).GetValue(row);
    static void CardsHudBinding(string context)
    {
        var rows=Private<IList>(CardsUI(),"ruleRows").Cast<object>().ToArray();
        var expected=game.DraftLoadout.OrderedRules();bool bound=rows.Length==expected.Count;
        for(int i=0;i<rows.Length&&i<expected.Count;i++)
        {
            var row=rows[i];var op=CardsField<ControlOperation>(row,"Operation");int local=CardsField<int>(row,"Row");
            var rule=game.Rules.RuleAt(op,local);var expression=ConditionalCatalog.Expression(rule);
            bound&=ReferenceEquals(CardsField<ConditionalController>(row,"Controller"),game.Rules)
                &&V2UIFields(rule)==V2UIFields(expected[i])&&CardsField<TMPro.TMP_Text>(row,"Number").text==(i+1).ToString()
                &&CardsField<TMPro.TMP_Text>(row,"Condition").text==expression.Conditions
                &&CardsField<TMPro.TMP_Text>(row,"Action").text==expression.Action;
        }
        Check(context+": global display order retains exact controller/operation/local-row binding",bound);
    }
    static IEnumerator CardsCapture(string name)
    {Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+"rule-cards-"+name+".png");yield return Frames(3);}
}
#endif
