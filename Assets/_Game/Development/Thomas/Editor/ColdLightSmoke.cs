#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Cold Light Focused Smoke")]
    public static void RunColdLight()=>Start("cold-light",false);
    public static void RunColdLightReview()=>Start("cold-light-review",false);
    public static void RunColdLightFinal()=>Start("cold-light-final",false);
    public static void RunColdLightProjectiles()=>Start("cold-light-projectiles",false);
    public static void RunColdLightCloseout()=>Start("cold-light-closeout",false);
    public static void RunColdLightLayout()=>Start("cold-light-layout",false);
    public static void RunTypography()=>Start("typography",false);
    public static void RunTypographyFollowup()=>Start("typography-followup",false);
    public static void RunPlexUI()=>Start("plex-ui",false);
    public static void RunUIPolish()=>Start("ui-polish",false);
    public static void RunUIPolishFocus()=>Start("ui-polish-focus",false);
    public static void RunCompactRuleControls()=>Start("compact-rule-controls",false);
    static IEnumerator CompactRuleControls()
    {
        game.NewGame();
        game.EditLoadout(d=>{foreach(var r in new[]{new ConditionalRule{Condition=25,Action="F02"},new ConditionalRule{Condition=18,Action="Q02"},new ConditionalRule{Condition=43,Action="D01"},new ConditionalRule{Condition=39,Action="M09"},new ConditionalRule{Condition=28,Action="J03"}})d.TryAddRule(ConditionalCatalog.Response(r.Action).Operation,r);});
        foreach(int width in new[]{1280,1920,2560,3840})
        {
            SetAlphaGameSize(width,width*9/16);yield return Frames(6);
            Check(width+" actual render size",Screen.width==width&&Screen.height==width*9/16);
            Check(width+" five rows and boundary controls",game.DraftLoadout.TotalRules==5&&!ConfigButtonNamed("AddRule").interactable&&!ConfigButtonNamed("MoveRuleUp0").interactable&&!ConfigButtonNamed("MoveRuleDown4").interactable);
            Check(width+" removed hint absent",!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="Add a rule or click a rule field to edit."));
            for(int i=0;i<5;i++)
            {
                var b=ConfigButtonNamed("RemoveRule"+i);
                Check(width+" trash icon "+i,b.transform.Find("TrashIcon")!=null&&b.GetComponent<RectTransform>().rect.width==40&&b.GetComponentInChildren<TMPro.TMP_Text>().text=="");
            }
            TextFits(width+" compact equipped");yield return ColdCapture("compact-equipped-"+width);
            ClickConfig("Rule0Condition");yield return Frames(3);TextFits(width+" compact selector");yield return ColdCapture("compact-editor-"+width);
            ClickConfig("CancelRuleEdit");yield return Frames(2);
        }
        var original=game.DraftLoadout.OrderedRules().Select(r=>r.Action).ToArray();
        ClickConfig("MoveRuleDown0");yield return Frames(2);Check("compact down moves row",game.DraftLoadout.OrderedRules()[1].Action==original[0]);
        ClickConfig("MoveRuleUp1");yield return Frames(2);Check("compact up restores order",game.DraftLoadout.OrderedRules().Select(r=>r.Action).SequenceEqual(original));
        ClickConfig("RemoveRule2");yield return Frames(2);Check("trash removes intended rule",game.DraftLoadout.TotalRules==4&&!game.DraftLoadout.OrderedRules().Any(r=>r.Action==original[2]));
        for(int i=0;i<4;i++){ClickConfig("RemoveRule0");yield return Frames(2);}
        Check("last trash leaves valid empty loadout",game.DraftLoadout.TotalRules==0&&ConfigButtonNamed("AddRule").interactable&&ConfigButtonNamed("StartFight").interactable);
    }
    static MissionBodyRegion Region(int i)=>Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<MissionBodyRegion>().Single(r=>r.NeuralRegion==i);
    static ConditionalVisualFeedback Visual(int op,int row=0)=>Feedback(op,row).Visual;
    static SpriteRenderer PlayerPart(string name)=>game.Player.GetComponentsInChildren<SpriteRenderer>().First(s=>s.name==name);
    static float Signal(SpriteRenderer sprite)
    {var block=new MaterialPropertyBlock();sprite.GetPropertyBlock(block);return block.GetFloat("_AIStrength");}
    static IEnumerator ColdCapture(string name)
    {System.IO.Directory.CreateDirectory("Logs/ColdLight");ScreenCapture.CaptureScreenshot("Logs/ColdLight/"+name+".png");yield return Frames(3);}
    static void FocusOption(string name,string label=null)
    {mousePosition=new Vector2(2,2);EventSystem.current.SetSelectedGameObject(ConfigButtonNamed(name,label).gameObject);}
    static void ColdLayout(int width)
    {
        Canvas.ForceUpdateCanvases();var ui=Object.FindAnyObjectByType<DuelUI>();
        var body=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="BodyPreview");
        var selector=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleEditor");
        var list=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleList");
        Check(width+" independent body / selector / full list",Screen.width==width&&body.position.x<selector.position.x&&selector.position.x<list.position.x
            &&Region(0).GetComponentInParent<CanvasGroup>().alpha==1&&body.rect.width*body.lossyScale.x>230);
        var overflow=ui.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.isTextOverflowing).Select(t=>t.name).ToArray();
        Check(width+" readable labels and description",overflow.Length==0,string.Join(",",overflow));
        var scroll=list.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
        Check(width+" all five rows fit without scrolling and Start accessible",game.DraftLoadout.TotalRules==5&&scroll.content.rect.height<=scroll.viewport.rect.height+1
            &&ConfigButtonNamed("StartFight").transform.parent==list
            &&Mathf.Approximately(ConfigButtonNamed("StartFight").transform.position.y,ConfigButtonNamed("AddRule").transform.position.y));
    }
    static IEnumerator ColdLight()
    {
        Record("COLD LIGHT: real rules/executors, virtual UI/input; controlled placements and paused policies, no fabricated visual values.");
        game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(8);
        yield return PointerConfig("AddRule");yield return SelectEditorCondition(33);
        FocusOption("Option","Aim at Nearest Enemy");yield return Frames(3);
        Check("legal Aim focus previews both arms without committing",Region(2).NeuralSignal>.9f&&Region(3).NeuralSignal>.9f&&Region(1).NeuralSignal==0&&game.DraftLoadout.TotalRules==0);
        yield return ColdCapture("config-aim-preview-1280");
        ClickConfig("Option","Aim at Nearest Enemy");yield return Frames(2);float flow=Region(2).NeuralFlow;
        yield return Seconds(.14f);
        Check("legal Aim commit produces directional circuit flow",flow>=0&&Region(2).NeuralFlow>flow&&game.DraftLoadout.TotalRules==1);
        yield return Seconds(.65f);Object.FindAnyObjectByType<DuelUI>().Show();yield return Frames(2);
        Check("Show does not replay installation; committed arms retain low signal",Region(2).NeuralFlow<0&&Mathf.Approximately(Region(2).NeuralSignal,.38f)&&Region(0).NeuralSignal==0);
        // First vertical slice: same actual accepted Aim lights HUD and both player arms.
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,-2);yield return Seconds(.15f);
        var visual=game.Player.GetComponent<OfficerVisual>();
        Check("Aim chain reaches actual accepted direction and arm renderer",Visual(2).Executing&&Signal(visual.Part(LoadoutSlot.LeftArm))>.6f&&Signal(visual.Part(LoadoutSlot.RightArm))>.6f);
        yield return ColdCapture("aim-chain-execution");
        game.NewGame();yield return Frames(3);
        yield return AddEditorRule(33,"Q01");yield return AddEditorRule(33,"F03");yield return AddEditorRule(33,"M01");yield return AddEditorRule(33,"J01");yield return AddEditorRule(1,"D04");
        yield return Seconds(.65f);
        foreach(int width in new[]{1280,1920})
        {
            SetAlphaGameSize(width,width*9/16);yield return Frames(7);
            FocusOption("Rule0Action");yield return Frames(2);
            Check(width+" equipped rule focus links arms",Region(2).NeuralSignal>.9f&&Region(4).NeuralSignal<.5f);
            yield return ColdCapture("config-equipped-"+width);
            ClickConfig("Rule4Condition");yield return Frames(2);ClickConfig("ConditionCategory4");yield return Frames(3);ColdLayout(width);
            Check(width+" condition-only navigation has no inferred body preview",Region(1).NeuralSignal<.5f&&Region(2).NeuralSignal<.5f);
            yield return ColdCapture("config-condition-"+width);
            yield return SelectEditorCondition(1);ClickConfig("ActionCategory1");yield return Frames(2);
            ExecuteEvents.Execute(ConfigButtonNamed("Option","Jump Once").gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return Frames(2);
            Check(width+" unavailable Jump stays grey without connection",!ConfigButtonNamed("Option","Jump Once").interactable&&Region(4).NeuralSignal<.5f);
            ClickConfig("CancelRuleEdit");yield return Frames(2);
            Check(width+" cancel restores committed Spine row",game.DraftLoadout.Operations[4].Rules[0].Action=="D04"&&ConfigButtonNamed("StartFight").interactable);
            ClickConfig("Rule4Action");yield return Frames(2);ClickConfig("ActionCategory2");yield return Frames(2);
            FocusOption("Option","Aim at Nearest Enemy");yield return Frames(2);ColdLayout(width);yield return ColdCapture("config-action-"+width);
            ClickConfig("CancelRuleEdit");yield return Frames(2);
        }
        ClickConfig("Rule4Action");yield return Frames(2);ClickConfig("ActionCategory2");yield return Frames(2);ClickConfig("Option","Aim at Nearest Enemy");yield return Seconds(.65f);
        Check("full-budget cross-body edit removes last Spine connection",game.DraftLoadout.TotalRules==5&&Region(1).NeuralSignal==0&&Region(2).NeuralSignal>0);
        ClickConfig("RemoveRule3");yield return Frames(2);Check("removing Jump retains Move leg connection",Region(4).NeuralSignal>0);
        ClickConfig("RemoveRule2");yield return Frames(2);Check("removing last Legs rule restores neutral contour",Region(4).NeuralSignal==0&&Region(5).NeuralSignal==0);
        // Move + Aim + paid Fire and a lower matching Aim row; source rows are operation-local.
        yield return AlphaFresh(1,d=>{Rule(d,2,33,"Q01");Rule(d,2,33,"Q02");Rule(d,3,33,"F03");Rule(d,0,33,"M02");});
        AlphaPlace(-5,-2);yield return Seconds(.12f);visual=game.Player.GetComponent<OfficerVisual>();
        Check("Move Aim and paid Fire execute concurrently",Visual(0).Executing&&Visual(2).Executing&&Visual(3).Executing&&game.Rules.AutomaticShots>0);
        Check("lower same-operation match never owns Aim",Visual(2,1).Matched&&!Visual(2,1).Executing);
        Check("body uses Arms and Legs and caps shared brightness",Signal(visual.Part(LoadoutSlot.LeftArm))>.6f&&Signal(visual.Part(LoadoutSlot.LeftLeg))>.6f&&Signal(visual.Part(LoadoutSlot.LeftArm))<=1);
        int shots=game.Rules.AutomaticShots;float pulse=Visual(3).Pulse;yield return Seconds(.06f);
        Check("Fire stays executing through normal cadence while muzzle pulse decays",game.Rules.AutomaticShots==shots&&Visual(3).Executing&&Visual(3).Pulse<pulse);
        yield return ColdCapture("combat-executing");
        var live=game.Rules;checkingPause=true;game.SetPause(true);float now=Time.time;var frozen=Visual(2);float arm=Signal(visual.Part(LoadoutSlot.LeftArm));yield return RealSeconds(.18);
        Check("pause freezes same controller HUD and actor signal",game.Rules==live&&Time.time==now&&Visual(2).Executing==frozen.Executing&&Signal(visual.Part(LoadoutSlot.LeftArm))==arm);
        game.SetPause(false);checkingPause=false;yield return Frames(3);Check("resume keeps controller",game.Rules==live);
        // A real manual Dodge pays cooldown; airborne auto Dodge must wait and only show match.
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D02"));AlphaPlace(-5,3);yield return Frames(3);
        float started=game.Dodge.LastDodgeStartedAt;heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);heldKeys=Array.Empty<Key>();
        Check("manual Dodge creates no automatic pulse",Visual(4).Pulse==0&&game.Rules.AcceptedDodges==0);
        yield return Until(()=>game.Dodge.CooldownRemaining<=.35f,2f);
        var movement=game.Player.GetComponent<PlayerMovement>();heldKeys=new[]{Key.Space};yield return Until(()=>!movement.IsGrounded,.3f);heldKeys=Array.Empty<Key>();yield return Frames(2);
        Check("cooldown yields matched-only Dodge",Visual(4).Matched&&!Visual(4).Executing&&Visual(4).Pulse==0&&Signal(PlayerPart("SpineSignal"))>.1f&&Signal(PlayerPart("SpineSignal"))<.3f);
        yield return ColdCapture("combat-matched-not-executing");
        yield return Until(()=>game.Rules.AcceptedDodges>0,.8f);yield return Frames(1);
        Check("real accepted Dodge drives local Spine pulse",Visual(4).Pulse>0&&Signal(PlayerPart("SpineSignal"))>.5f&&game.Dodge.IsDodging);
        yield return ColdCapture("combat-dodge");yield return Seconds(.22f);
        Check("Dodge action releases before afterglow expires",!Visual(4).Executing&&Visual(4).Afterglow>0);
        // Actual accepted Jump is a pulse, never sustained airborne ownership.
        yield return AlphaFresh(1,d=>{Rule(d,1,33,"J01");Rule(d,0,33,"M02");});AlphaPlace(-5,-2);
        yield return Until(()=>game.Rules.AcceptedJumps>0,.4f);yield return Frames(1);visual=game.Player.GetComponent<OfficerVisual>();
        Check("accepted Jump pulse survives no continuous Executing flag",!Visual(1).Executing&&Visual(1).Pulse>0&&Signal(visual.Part(LoadoutSlot.LeftLeg))>.5f&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>1);
        yield return ColdCapture("combat-jump");yield return Seconds(.35f);
        Check("Jump pulse fades during same airtime without false takeover",Visual(1).Pulse==0&&!Visual(1).Executing&&game.Rules.AcceptedJumps==1);
        live=game.Rules;game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(3);
        Check("death/reconfigure disposes prior feedback",game.Rules==null&&!live.VisualFeedback(ControlOperation.Jump).Matched&&live.VisualFeedback(ControlOperation.Jump).Pulse==0);
        game.StartFight();yield return Frames(3);Check("retry fresh snapshot has no residual signal",game.Rules!=live&&Visual(1).Pulse==0&&Visual(0).Afterglow==0);
        game.NewGame();yield return Frames(3);Check("New Game clears equipment and animation",game.DraftLoadout.TotalRules==0&&Region(2).NeuralSignal==0&&Region(4).NeuralSignal==0&&Region(4).NeuralFlow<0);
        yield return AlphaFresh(1,null);heldKeys=new[]{Key.D};MouseAt(game.Player.transform.position+Vector3.up*5,true);yield return Seconds(.2f);visual=game.Player.GetComponent<OfficerVisual>();
        Check("manual movement and paid manual Fire remain neutral",game.Rifle.TriggerPulls>0&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.x>1&&Signal(visual.Part(LoadoutSlot.LeftLeg))==0&&Signal(visual.Part(LoadoutSlot.Weapon))==0);
        heldKeys=Array.Empty<Key>();mouseHeld=false;
        // Existing low-health latch, including the same exit hysteresis, controls HP.
        game.Player.SetRuntimeHealth(150,60);yield return Frames(3);var fill=Private<UnityEngine.UI.Image>(Object.FindAnyObjectByType<DuelUI>(),"playerFill");
        Check("HP uses existing Low Health entry",game.Rules.IsLowHealth&&fill.color==NeuralPalette.Danger&&fill.rectTransform.pivot.x==0);
        game.Player.SetRuntimeHealth(150,65);yield return Frames(3);Check("HP retains original hysteresis",game.Rules.IsLowHealth&&fill.color==NeuralPalette.Danger);
        game.Player.SetRuntimeHealth(150,75);yield return Frames(3);Check("HP exits to cold white",!game.Rules.IsLowHealth&&fill.color==NeuralPalette.Primary);
        yield return AlphaFresh(3,null,false);float clock=game.FightClock;yield return Seconds(2.5f);
        Check("short normal Wave 3 remains live with two Bosses",game.Phase==DuelPhase.Fight&&game.Bosses.Count==2&&game.FightClock>clock+2);
        foreach(var actor in game.Bosses)
        {
            var parts=Private<SpriteRenderer[]>(actor.View,"parts");var identity=actor.Profile.Kind==BossKind.Refraction?NeuralPalette.Refraction:NeuralPalette.Severance;
            Check("Boss "+actor.ActorId+" body and weapon identities",(Vector3)(Vector4)parts[0].color==(Vector3)(Vector4)identity&&(Vector3)(Vector4)parts[14].color==(Vector3)(Vector4)NeuralPalette.Primary);
        }
        yield return ColdCapture("wave3-bosses");
        yield return AlphaFresh(3,null);var gunner=game.Bosses.First(a=>a.ActorId==1);yield return Until(()=>gunner.Actions.Ready(BossAbility.Camo),7.2f);
        bool camo=gunner.Actions.TryStart(BossAbility.Camo,game.Player.transform.position);yield return Seconds(.3f);
        Check("Camo hides every body and weapon accent",camo&&gunner.IsCamo&&gunner.View.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled).All(s=>s.color.a<.01f),$"accepted={camo} camo={gunner.IsCamo} phase={gunner.Actions.Phase}");
        yield return ColdCapture("wave3-camo");
        Record("Dynamic evidence: sampled forward install flow, paid-shot pulse decay with held Fire feedback, accepted Jump/Dodge pulses, pause freeze and cleanup. Screenshots alone do not prove animation.");
        Record("NOT TESTED: physical input, standalone/Build, long sessions, balance, natural victory, exhaustive pairs, audio audition.");
        game.MainMenu();yield return Frames(2);
    }
    static IEnumerator ColdLightReview()
    {
        Record("Targeted follow-up: long category labels, row accents, sword grip, Camo readiness and unobscured captures. Prior executor checks are not repeated.");
        yield return ColdLightLayoutReview();
        // Wait on an unchanged real cooldown. Capture without the pause menu covering the body.
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D02"));AlphaPlace(-5,3);yield return Frames(3);
        var movement=game.Player.GetComponent<PlayerMovement>();float started=game.Dodge.LastDodgeStartedAt;
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);heldKeys=Array.Empty<Key>();
        yield return Until(()=>game.Dodge.CooldownRemaining<=.4f,2f);heldKeys=new[]{Key.Space};yield return Until(()=>!movement.IsGrounded,.3f);heldKeys=Array.Empty<Key>();yield return Frames(2);
        Check("unobscured capture is real match without execution",Visual(4).Matched&&!Visual(4).Executing&&Visual(4).Pulse==0);
        yield return ColdCapture("combat-matched-not-executing");
        yield return AlphaFresh(3,null);var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        var parts=Private<SpriteRenderer[]>(sword.View,"parts");
        Check("existing sword grip slot has sprite and owner accent",parts[15].sprite!=null&&parts[15].enabled&&parts[15].color==NeuralPalette.Severance);
        yield return ColdCapture("wave3-bosses-final");
        bool ready=gunner.Actions.Ready(BossAbility.Camo);Record("Camo ready before fixture="+ready);
        yield return Until(()=>gunner.Actions.Ready(BossAbility.Camo),7.2f);
        bool accepted=gunner.Actions.TryStart(BossAbility.Camo,game.Player.transform.position);yield return Seconds(.18f);
        Check("Camo accepted by unchanged executor",accepted&&gunner.IsCamo,$"accepted={accepted} camo={gunner.IsCamo} ability={gunner.Actions.Ability} phase={gunner.Actions.Phase}");
        var visible=gunner.View.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled&&s.color.a>.01f).Select(s=>s.name+":"+s.color.a).ToArray();
        Check("all concealed body and weapon accents have zero alpha",gunner.IsCamo&&visible.Length==0,string.Join(",",visible));
        yield return ColdCapture("wave3-camo");
        yield return Until(()=>!gunner.Actions.Busy,2.2f);yield return Seconds(.15f);
        Check("Camo exit restores existing body and weapon",!gunner.IsCamo&&gunner.View.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled).All(s=>s.color.a>.99f));
        // A real paid gunner burst supplies source-colored bullets; no synthetic render values.
        OnFloor(game.Player.gameObject,gunner.Navigation,-5);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,10);yield return Seconds(.3f);
        bool fired=gunner.Actions.TryStart(BossAbility.Burst,game.Player.transform.position);yield return Until(()=>Projectile.Active.Any(p=>p.Owner==gunner.gameObject),1f);
        var bullet=Projectile.Active.FirstOrDefault(p=>p.Owner==gunner.gameObject);
        Check("real enemy bullet uses source identity",fired&&bullet!=null&&bullet.GetComponent<SpriteRenderer>().color==NeuralPalette.Refraction);
        yield return ColdCapture("wave3-projectiles");
        game.MainMenu();yield return Frames(3);
    }
    static IEnumerator ColdLightLayoutReview()
    {
        foreach(int width in new[]{1280,1920})
        {
            game.NewGame();SetAlphaGameSize(width,width*9/16);yield return Frames(6);
            game.EditLoadout(d=>{d.TryAddRule(2,new ConditionalRule{Condition=33,Action="Q01"});d.TryAddRule(3,new ConditionalRule{Condition=33,Action="F03"});d.TryAddRule(0,new ConditionalRule{Condition=33,Action="M01"});d.TryAddRule(1,new ConditionalRule{Condition=33,Action="J01"});d.TryAddRule(4,new ConditionalRule{Condition=1,Action="D04"});});yield return Frames(4);
            ClickConfig("Rule4Condition");yield return Frames(2);ClickConfig("ConditionCategory4");yield return Frames(3);ColdLayout(width);
            yield return ColdCapture("config-condition-"+width);
            heldKeys=new[]{Key.Escape};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Frames(3);
            Check(width+" Escape discards only draft",game.DraftLoadout.Operations[4].Rules[0].Condition==1&&ConfigButtonNamed("StartFight").interactable);
            FocusOption("Rule0Action");yield return Frames(2);
            ExecuteEvents.Execute(ConfigButtonNamed("Rule0Action").gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);yield return Frames(2);
            Check(width+" keyboard rule focus survives pointer exit",Region(2).NeuralSignal>.9f);
            yield return ColdCapture("config-equipped-"+width);
            ClickConfig("Rule4Action");yield return Frames(2);ClickConfig("ActionCategory2");yield return Frames(2);FocusOption("Option","Aim at Nearest Enemy");yield return Frames(3);ColdLayout(width);
            yield return ColdCapture("config-action-"+width);
            ClickConfig("CancelRuleEdit");yield return Frames(2);
        }
    }
    static IEnumerator ColdLightFinal()
    {
        Record("FINAL: corrected fixture uses TryAddRule display order, focus/exit, reorder feedback and Bloom disabled; no repeated historical suite.");
        yield return ColdLightLayoutReview();
        game.NewGame();yield return Frames(2);
        yield return AddEditorRule(33,"Q01");yield return AddEditorRule(4,"Q02");yield return AddEditorRule(33,"F03");
        ClickConfig("MoveRuleUp1");yield return Frames(2);
        Check("UI reorder and Copy retain presentation order",game.DraftLoadout.Copy().OrderedRules().Select(r=>r.Action).SequenceEqual(new[]{"Q02","Q01","F03"}));
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,-2);yield return Seconds(.12f);
        var ui=Object.FindAnyObjectByType<DuelUI>();
        var row0=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleFeedback_2_0");
        var row1=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleFeedback_2_1");
        Check("reordered HUD binds operation-local accepted row only",!Visual(2,0).Executing&&Visual(2,1).Executing
            &&row0.GetComponentsInChildren<TMPro.TMP_Text>().Last().color==NeuralPalette.Primary
            &&row1.GetComponentsInChildren<TMPro.TMP_Text>().Last().color==NeuralPalette.Arms);
        var camera=Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool post=camera.renderPostProcessing;camera.renderPostProcessing=false;restoration.Add(()=>{if(camera!=null)camera.renderPostProcessing=post;});yield return Frames(3);
        Check("Bloom-off frame retains actual execution and readable HUD",Visual(2,1).Executing&&ui.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing));
        yield return ColdCapture("combat-bloom-off");
        yield return Until(()=>Projectile.Active.Any(p=>p.Owner==game.Player.gameObject),.4f);
        var shot=Projectile.Active.FirstOrDefault(p=>p.Owner==game.Player.gameObject);
        Check("automatic player bullet keeps neutral identity",shot!=null&&shot.GetComponent<SpriteRenderer>().color==NeuralPalette.Primary);
        var controller=game.Rules;checkingPause=true;yield return DeveloperKey(Key.F8);
        Check("F8 freezes existing feedback controller",game.Paused&&game.DeveloperToolsOpen&&game.Rules==controller&&Visual(2,1).Executing);
        yield return DeveloperKey(Key.F8);checkingPause=false;Check("F8 restores prior live controller",!game.Paused&&!game.DeveloperToolsOpen&&game.Rules==controller);
        game.MainMenu();yield return Frames(3);
    }
    static IEnumerator ColdLightProjectiles()
    {
        Record("Targeted live-projectile check: observe actual paid ShotFired and owner. Automatic shots retain their original InnerAI attribution.");
        foreach(bool automatic in new[]{true,false})
        {
            yield return AlphaFresh(1,d=>{if(automatic)Rule(d,3,17,"F03");});AlphaPlace(-6,6);yield return Frames(3);
            bool observed=false;Color color=default;
            Action<Vector2,Vector2> paid=(origin,direction)=>
            {var bullet=Projectile.Active.LastOrDefault(p=>p.Owner==game.Player.gameObject);if(bullet!=null){observed=true;color=bullet.GetComponent<SpriteRenderer>().color;}};
            game.Rifle.ShotFired+=paid;MouseAt(game.Player.transform.position+Vector3.up*5,true);
            yield return Until(()=>observed,.5f);game.Rifle.ShotFired-=paid;
            Check((automatic?"Automatic":"Manual")+" paid player bullet has shared neutral identity",observed&&color==NeuralPalette.Primary,$"observed={observed} color={ColorUtility.ToHtmlStringRGB(color)}");
            Check((automatic?"Automatic":"Manual")+" Fire feedback retains true input origin",automatic?game.Rules.AutomaticShots>0&&Visual(3).Pulse>0:game.Rules.AutomaticShots==0);
            if(automatic)yield return ColdCapture("automatic-player-projectile");
            mouseHeld=false;yield return Frames(3);
        }
        game.MainMenu();yield return Frames(2);
    }
    static IEnumerator ColdLightCloseout()
    {
        Record("Final targeted fixes: EventSystem deselection, inactive callbacks, narrow Bloom-off signals and real automatic/manual bullet identity.");
        game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(6);
        ClickConfig("AddRule");yield return SelectEditorCondition(33);
        FocusOption("Option","Aim at Nearest Enemy");yield return Frames(2);
        Check("legal Action focus previews Arms",Region(2).NeuralSignal>.9f);
        FocusOption("ActionCategory2");yield return Frames(2);
        Check("neutral category focus clears former Action preview",Region(2).NeuralSignal==0);
        FocusOption("Option","Aim at Nearest Enemy");yield return Frames(2);
        EventSystem.current.SetSelectedGameObject(null);yield return Frames(2);
        Check("clearing keyboard focus restores uncommitted neutral body",Region(2).NeuralSignal==0);
        ClickConfig("Option","Aim at Nearest Enemy");yield return Seconds(.65f);
        var old=ConfigButtonNamed("Rule0Action").GetComponent<ConditionalOptionDescription>();
        ClickConfig("Rule0Condition");
        old.OnPointerClick(new PointerEventData(EventSystem.current));
        Check("inactive prior page callback cannot preview new page",!old.gameObject.activeInHierarchy&&Region(2).NeuralSignal<.5f);
        yield return Frames(3);ClickConfig("CancelRuleEdit");yield return Frames(2);
        FocusOption("Rule0Action");yield return Frames(2);FocusOption("StartFight");yield return Frames(2);
        Check("neutral button focus restores committed baseline",Mathf.Approximately(Region(2).NeuralSignal,.38f));
        yield return ColdLightProjectiles();
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D02"));AlphaPlace(-5,3);yield return Frames(3);
        var camera=Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool post=camera.renderPostProcessing;camera.renderPostProcessing=false;restoration.Add(()=>{if(camera!=null)camera.renderPostProcessing=post;});
        var movement=game.Player.GetComponent<PlayerMovement>();float started=game.Dodge.LastDodgeStartedAt;
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);heldKeys=Array.Empty<Key>();
        yield return Until(()=>game.Dodge.CooldownRemaining<=.4f,2f);heldKeys=new[]{Key.Space};yield return Until(()=>!movement.IsGrounded,.3f);heldKeys=Array.Empty<Key>();yield return Frames(2);
        Check("Bloom-off narrow Spine match has no false execution",Visual(4).Matched&&!Visual(4).Executing&&Visual(4).Pulse==0&&Mathf.Approximately(Signal(PlayerPart("SpineSignal")),.2f));
        yield return ColdCapture("combat-matched-not-executing");
        yield return Until(()=>game.Rules.AcceptedDodges>0,.8f);yield return Frames(1);
        Check("real accepted Dodge increases local pulse",Visual(4).Pulse>0&&Signal(PlayerPart("SpineSignal"))>.5f);
        yield return ColdCapture("combat-dodge");
        yield return AlphaFresh(1,d=>{Rule(d,2,33,"Q01");Rule(d,3,33,"F03");Rule(d,0,33,"M02");});AlphaPlace(-5,-2);yield return Seconds(.12f);
        Check("Bloom-off core reflects real parallel Aim Fire Move",Visual(2).Executing&&Visual(3).Executing&&Visual(0).Executing&&game.Rules.AutomaticShots>0);
        yield return ColdCapture("combat-bloom-off");
        game.MainMenu();yield return Frames(3);
    }
    static string[] TextOverflow()
    {
        Canvas.ForceUpdateCanvases();
        return Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMPro.TMP_Text>()
            .Where(t=>{t.ForceMeshUpdate();return t.isTextOverflowing;}).Select(t=>t.text.Replace('\n',' ')).ToArray();
    }
    static void TextFits(string context)
    {var errors=TextOverflow();Check(context+" text fits",errors.Length==0,string.Join(" | ",errors));}
    static void InspectCatalogText(string context)
    {
        var errors=new System.Collections.Generic.HashSet<string>();
        foreach(var option in CatalogOptions())
        {option.GetComponent<ConditionalOptionDescription>().Inspect();foreach(var error in TextOverflow())errors.Add(error);}
        Check(context+" labels and inspected descriptions fit",errors.Count==0,string.Join(" | ",errors));
    }
    static bool HudRowsFit()
    {
        var ui=Object.FindAnyObjectByType<DuelUI>();
        var rows=ui.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("RuleFeedback_")).ToArray();
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];var text=row.GetComponentsInChildren<TMPro.TMP_Text>();
            var a=text.First(t=>t.name=="ConditionText").rectTransform;var b=text.First(t=>t.name=="ActionText").rectTransform;
            if(a.anchoredPosition.y-a.rect.height/2 < b.anchoredPosition.y+b.rect.height/2)return false;
            foreach(var t in text)
            {
                var corners=new Vector3[4];t.rectTransform.GetWorldCorners(corners);
                if(corners.Any(c=>!row.rect.Contains(row.InverseTransformPoint(c))))return false;
            }
            var screenCorners=new Vector3[4];row.GetWorldCorners(screenCorners);
            if(screenCorners.Any(c=>c.y<0||c.y>Screen.height))return false;
            if(i+1<rows.Length&&row.position.y-row.rect.height*row.lossyScale.y/2<rows[i+1].position.y+rows[i+1].rect.height*rows[i+1].lossyScale.y/2)return false;
        }
        return true;
    }
    static IEnumerator Typography()
    {
        Record("Bounded typography audit: catalog copy once per category, long equipped rows, real Low Ammo Aim acceptance, menus/F8. No pair/executor matrix.");
        foreach(int width in new[]{1280,1920})
        {
            RestoreFixture();game.MainMenu();SetAlphaGameSize(width,width*9/16);yield return Frames(6);TextFits(width+" Menu");
            game.NewGame();game.EditLoadout(d=>{foreach(string action in new[]{"Q02","F05","D04","M03","J03"})d.TryAddRule((int)ConditionalCatalog.Response(action).Operation,new ConditionalRule{Condition=25,Action=action});});yield return Frames(3);
            TextFits(width+" longest equipped rows");ColdLayout(width);
            yield return ColdCapture("typography-equipped-"+width);
            ClickConfig("Rule0Condition");yield return Frames(2);
            for(int category=0;category<6;category++)
            {ClickConfig("ConditionCategory"+category);yield return Frames(2);InspectCatalogText(width+" condition category "+category);}
            yield return SelectEditorCondition(1);
            foreach(int op in new[]{2,3,4,0,1})
            {ClickConfig("ActionCategory"+op);yield return Frames(2);InspectCatalogText(width+" action category "+op);}
            yield return ColdCapture("typography-description-"+width);
            ClickConfig("CancelRuleEdit");yield return Frames(2);
            game.StartFight();yield return Frames(3);TextFits(width+" five-operation HUD");Check(width+" five HUD groups stay separate/on-screen",HudRowsFit());
            yield return ColdCapture("typography-five-hud-"+width);
            game.NewGame();game.EditLoadout(d=>{for(int i=0;i<5;i++)d.TryAddRule(2,new ConditionalRule{Condition=25,Action="Q02"});});game.StartFight();yield return Frames(3);
            TextFits(width+" five same-operation HUD");Check(width+" five same-operation rows fit",HudRowsFit());
            game.NewGame();game.EditLoadout(d=>{d.TryAddRule(2,new ConditionalRule{Condition=12,Action="Q02"});d.TryAddRule(4,new ConditionalRule{Condition=27,Action="D02"});});game.StartFight();
            yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,3);yield return Frames(3);
            var root=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleFeedback_2_0");
            var size=root.sizeDelta;var pos=root.anchoredPosition;
            MouseAt(game.Player.transform.position+Vector3.up*7,true);yield return Until(()=>game.Rifle.AmmoInMagazine<=2,2f);mouseHeld=false;yield return Frames(3);
            var actionText=root.GetComponentsInChildren<TMPro.TMP_Text>().Last();
            Check(width+" actual Low Ammo Aim emphasizes fixed-weight text without moving rows",game.Rules.Feedback(ControlOperation.Aim,0).Executing&&actionText.color==NeuralPalette.Arms&&actionText.fontStyle==TMPro.FontStyles.Normal&&root.sizeDelta==size&&root.anchoredPosition==pos);
            TextFits(width+" active long-label HUD");Check(width+" active condition/action and frames do not overlap",HudRowsFit());
            yield return ColdCapture("typography-active-"+width);
            checkingPause=true;game.SetPause(true);yield return Frames(2);TextFits(width+" Pause");game.SetPause(false);
            game.ToggleDeveloperTools();yield return Frames(2);
            foreach(string tab in new[]{"Player","Weapon","Health"}){ClickConfig(tab);yield return Frames(2);TextFits(width+" F8 "+tab);}
            game.ToggleDeveloperTools();checkingPause=false;game.Player.TakeDamage(99999,null);yield return Frames(3);TextFits(width+" Defeat");
        }
        game.MainMenu();yield return Frames(3);
    }
    // Character geometry, not just RectTransform size: catches weight/kerning/wrap changes.
    static Vector3[] GlyphPositions(TMPro.TMP_Text text)
    {
        text.ForceMeshUpdate();
        return text.textInfo.characterInfo.Take(text.textInfo.characterCount).SelectMany(c=>new[]{
            new Vector3(c.origin,c.baseLine,c.lineNumber),new Vector3(c.xAdvance,c.pointSize,0),
            c.bottomLeft,c.topRight,c.vertex_BL.position,c.vertex_TR.position}).ToArray();
    }
    static TMPro.TMP_Text PlexAimText()=>Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<RectTransform>()
        .First(r=>r.name=="RuleFeedback_2_0").GetComponentsInChildren<TMPro.TMP_Text>().Last();
    static void ControlGeometry(string context)
    {
        Canvas.ForceUpdateCanvases();
        var outlines=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<DuelButtonOutline>();
        bool contained=true,thick=true,disabled=true;
        foreach(var outline in outlines)
        {
            outline.Refresh();var rect=(RectTransform)outline.transform;
            foreach(var edge in outline.Edges)
            {
                var corners=new Vector3[4];edge.rectTransform.GetWorldCorners(corners);
                contained&=corners.All(c=>{var p=rect.InverseTransformPoint(c);return p.x>=rect.rect.xMin-.01f&&p.x<=rect.rect.xMax+.01f&&p.y>=rect.rect.yMin-.01f&&p.y<=rect.rect.yMax+.01f;});
                thick&=Mathf.Min(edge.rectTransform.rect.width,edge.rectTransform.rect.height)*rect.lossyScale.x>=1.99f;
            }
            if(!outline.Button.IsInteractable())disabled&=outline.Label.color==NeuralPalette.DisabledText&&outline.Edges.All(e=>e.color==NeuralPalette.Border)&&!outline.Selection.enabled;
        }
        Check(context+" all four control edges lie inside their bounds",contained);
        Check(context+" control borders retain at least two actual pixels",thick);
        Check(context+" disabled controls immediately dim label/frame/selection",disabled);
    }
    static IEnumerator ConfigurationPolish(int width)
    {
        ControlGeometry(width+" equipped controls");
        FocusOption("ConditionCategory0");ClickConfig("ConditionCategory0");yield return Frames(2);
        var selected=EventSystem.current.currentSelectedGameObject;
        Check(width+" keyboard focus survives category rebuild",selected!=null&&selected.name=="ConditionCategory0"&&selected.GetComponent<DuelButtonOutline>().KeyboardFocused);
        var outline=ConfigButtonNamed("ConditionCategory0").GetComponent<DuelButtonOutline>();
        outline.Refresh();float focusWidth=outline.Edges[2].rectTransform.rect.width;
        EventSystem.current.SetSelectedGameObject(null);outline.Refresh();
        Check(width+" selection marker persists independently of keyboard frame",outline.Selection.enabled&&outline.Edges[2].rectTransform.rect.width<focusWidth&&outline.Edges[2].color==NeuralPalette.ControlBorder);
        yield return SelectEditorCondition(1);ClickConfig("ActionCategory1");yield return Frames(2);
        var blocked=ConfigButtonNamed("Option","Jump Once");
        blocked.GetComponent<ConditionalOptionDescription>().Inspect();yield return Frames(2);
        ControlGeometry(width+" disabled Jump and Start Fight");TextFits(width+" disabled explanation");
        Check(width+" disabled action has no body preview or committed change",!blocked.interactable&&game.DraftLoadout.TotalRules==5&&Region(4).NeuralSignal<.5f);
        yield return ColdCapture("ui-polish-disabled-"+width);
        ClickConfig("SelectorBack");yield return Frames(2);
    }
    static IEnumerator UIPolishFocus()
    {
        Record("Focused UI follow-up only: actual EventSystem move/submit, pointer hover and draft cancel/commit.");
        foreach(int width in new[]{1280,1920,2560,3840})
        {
            RestoreFixture();game.MainMenu();SetAlphaGameSize(width,width*9/16);yield return Frames(6);
            game.NewGame();ClickConfig("AddRule");yield return Frames(2);FocusOption("ConditionCategory0");
            var previous=ConfigButtonNamed("ConditionCategory0").GetComponent<DuelButtonOutline>();
            ExecuteEvents.Execute(previous.gameObject,new AxisEventData(EventSystem.current){moveDir=MoveDirection.Down,moveVector=Vector2.down},ExecuteEvents.moveHandler);
            yield return Frames(2);
            var selected=EventSystem.current.currentSelectedGameObject;
            Check(width+" keyboard Down transfers one focus frame",selected!=null&&selected.name=="ConditionCategory1"&&!previous.KeyboardFocused&&selected.GetComponent<DuelButtonOutline>().KeyboardFocused);
            ExecuteEvents.Execute(selected,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return Frames(2);
            Check(width+" keyboard Submit keeps category focus on rebuilt page",EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.name=="ConditionCategory1");
            var hover=ConfigButtonNamed("ConditionCategory2").GetComponent<DuelButtonOutline>();
            mousePosition=RectTransformUtility.WorldToScreenPoint(null,hover.transform.position);yield return Frames(4);
            Check(width+" pointer hover differs from focus and selection",!hover.KeyboardFocused&&!hover.Selection.enabled&&hover.Edges[0].color==NeuralPalette.Secondary);
            mousePosition=new Vector2(2,2);yield return Frames(3);
            Check(width+" pointer leave restores neutral border",hover.Edges[0].color==NeuralPalette.ControlBorder);
            FocusOption("Option","Reloading");ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return Frames(2);
            Check(width+" keyboard condition step focuses a legal action without committing",EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.name=="Option"&&game.DraftLoadout.TotalRules==0&&!ConfigButtonNamed("StartFight").interactable);
            FocusOption("Option","Aim at Lowest-Health Enemy");ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return Frames(2);
            Check(width+" keyboard legal action commits once and restores Add focus",game.DraftLoadout.TotalRules==1&&game.DraftLoadout.Operations[2].Rules[0].Action=="Q02"&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.name=="AddRule"&&ConfigButtonNamed("StartFight").interactable);
            ClickConfig("Rule0Condition");yield return Frames(2);FocusOption("CancelRuleEdit");ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);yield return Frames(2);
            Check(width+" keyboard cancel retains committed rule",game.DraftLoadout.TotalRules==1&&game.DraftLoadout.Operations[2].Rules[0].Condition==9&&ConfigButtonNamed("StartFight").interactable);
            TextFits(width+" final keyboard page");ControlGeometry(width+" final keyboard page");
            yield return ColdCapture("ui-polish-equipped-"+width);
        }
        game.MainMenu();yield return Frames(3);
    }
    static IEnumerator PlexUI(bool polish=false)
    {
        string prefix=polish?"ui-polish":"plex";
        Record("IBM Plex audit at four actual Game View sizes; real Reloading -> Aim acceptance/release. No fabricated feedback.");
        foreach(int width in new[]{1280,1920,2560,3840})
        {
            RestoreFixture();game.MainMenu();SetAlphaGameSize(width,width*9/16);yield return Frames(6);
            Check(width+" actual render size",Screen.width==width&&Screen.height==width*9/16);TextFits(width+" Plex menu");
            game.NewGame();game.EditLoadout(d=>{foreach(string action in new[]{"Q02","F05","D04","M03","J03"})d.TryAddRule((int)ConditionalCatalog.Response(action).Operation,new ConditionalRule{Condition=25,Action=action});});yield return Frames(3);
            ColdLayout(width);TextFits(width+" Plex equipped");
            ClickConfig("Rule0Condition");yield return Frames(2);
            if(polish)yield return ConfigurationPolish(width);
            for(int category=0;category<6;category++)
            {ClickConfig("ConditionCategory"+category);yield return Frames(2);InspectCatalogText(width+" Plex condition category "+category);}
            if(polish){ClickConfig("ConditionCategory0");yield return Frames(2);ControlGeometry(width+" category controls");}
            yield return ColdCapture(prefix+"-config-"+width);
            yield return SelectEditorCondition(1);
            foreach(int op in new[]{2,3,4,0,1})
            {ClickConfig("ActionCategory"+op);yield return Frames(2);InspectCatalogText(width+" Plex action category "+op);}
            ClickConfig("CancelRuleEdit");game.StartFight();yield return Frames(3);
            TextFits(width+" Plex five-operation HUD");Check(width+" Plex HUD groups fit",HudRowsFit());
            yield return ColdCapture(prefix+"-five-hud-"+width);
            game.NewGame();game.EditLoadout(d=>{for(int i=0;i<5;i++)d.TryAddRule(2,new ConditionalRule{Condition=25,Action="Q02"});});game.StartFight();yield return Frames(3);
            TextFits(width+" Plex five Aim HUD");Check(width+" Plex five Aim rows fit",HudRowsFit());
            game.NewGame();game.EditLoadout(d=>{d.TryAddRule(2,new ConditionalRule{Condition=9,Action="Q02"});d.TryAddRule(4,new ConditionalRule{Condition=27,Action="D02"});});game.StartFight();
            yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,3);yield return Frames(3);
            var ui=Object.FindAnyObjectByType<DuelUI>();
            var texts=ui.GetComponentsInChildren<TMPro.TMP_Text>();
            Check(width+" assigned Plex fonts and fixed sizes",texts.All(t=>t.font.name.StartsWith("IBMPlex")&&!t.enableAutoSizing));
            var root=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleFeedback_2_0");
            var actionText=root.GetComponentsInChildren<TMPro.TMP_Text>().Last();
            var before=GlyphPositions(actionText);var material=actionText.fontSharedMaterial;var pointSize=actionText.fontSize;
            if(polish)
            {
                var arrow=root.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name=="ActionArrow");arrow.ForceMeshUpdate();
                var info=actionText.textInfo;
                float x=info.characterInfo[info.lineInfo[0].firstCharacterIndex].origin;
                Check(width+" wrapped action lines have a fixed hanging indent",Enumerable.Range(0,info.lineCount).All(i=>Mathf.Abs(info.characterInfo[info.lineInfo[i].firstCharacterIndex].origin-x)<.01f));
                float arrowBaseline=arrow.transform.TransformPoint(new Vector3(0,arrow.textInfo.characterInfo[0].baseLine)).y;
                float actionBaseline=actionText.transform.TransformPoint(new Vector3(0,info.characterInfo[0].baseLine)).y;
                Check(width+" arrow baseline aligns with first action line",Mathf.Abs(arrowBaseline-actionBaseline)<.1f);
            }
            Check(width+" starts without auto Aim",!Visual(2).Executing&&material.GetColor(TMPro.ShaderUtilities.ID_GlowColor).a==0);
            yield return ColdCapture(prefix+"-idle-"+width);
            MouseAt(game.Player.transform.position+Vector3.up*7,true);
            yield return Until(()=>game.Rifle.IsReloading&&Visual(2).Executing,3f);mouseHeld=false;yield return Frames(1);
            // Editor focus changes can rebuild Pause/Resume UI; inspect the current row after waits.
            actionText=PlexAimText();material=actionText.fontSharedMaterial;
            Check(width+" real reload accepts Aim",game.Rifle.IsReloading&&Visual(2).Matched&&Visual(2).Executing&&actionText.color==NeuralPalette.Arms&&material.GetColor(TMPro.ShaderUtilities.ID_GlowColor).a>0);
            Check(width+" every glyph and wrap stays fixed at acceptance",before.SequenceEqual(GlyphPositions(actionText))&&actionText.fontSharedMaterial==material&&actionText.fontStyle==TMPro.FontStyles.Normal&&actionText.fontSize==pointSize);
            TextFits(width+" Plex active");Check(width+" Plex active rows fit",HudRowsFit());yield return ColdCapture(prefix+"-active-"+width);
            yield return Until(()=>!game.Rifle.IsReloading&&!Visual(2).Executing,1.6f);yield return Frames(2);
            actionText=PlexAimText();material=actionText.fontSharedMaterial;
            Check(width+" every glyph stays fixed at release",before.SequenceEqual(GlyphPositions(actionText))&&actionText.color==NeuralPalette.Primary&&material.GetColor(TMPro.ShaderUtilities.ID_GlowColor).a==0);
            yield return ColdCapture(prefix+"-released-"+width);
            checkingPause=true;game.SetPause(true);yield return Frames(2);TextFits(width+" Plex Pause");game.SetPause(false);
            game.ToggleDeveloperTools();yield return Frames(2);
            foreach(string tab in new[]{"Player","Weapon","Health"}){ClickConfig(tab);yield return Frames(2);TextFits(width+" Plex F8 "+tab);}
            game.ToggleDeveloperTools();checkingPause=false;game.Player.TakeDamage(99999,null);yield return Frames(3);TextFits(width+" Plex Defeat");
            game.ConfigurePlaytestWave(3);game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();TextFits(width+" Plex dual-Boss HUD");
            foreach(var boss in game.Bosses)boss.Health.TakeDamage(99999,game.Player.gameObject);
            yield return Until(()=>game.Phase==DuelPhase.Complete,1.2f);Check(width+" controlled Complete",game.Phase==DuelPhase.Complete);TextFits(width+" Plex Complete");
            game.MainMenu();yield return Frames(3);Check(width+" row materials released",Resources.FindObjectsOfTypeAll<Material>().All(m=>m.name!="Rule signal"));
        }
        game.MainMenu();yield return Frames(3);
    }
    static IEnumerator TypographyFollowup()
    {
        Record("Targeted description fix and remaining result screen. Earlier real acceptance/HUD/menu checks are not repeated.");
        foreach(int width in new[]{1280,1920})
        {
            RestoreFixture();game.NewGame();SetAlphaGameSize(width,width*9/16);yield return Frames(5);
            foreach(var pair in new (int condition,int op)[]{(1,1),(9,3),(12,3),(4,2)})
            {
                ClickConfig("AddRule");yield return Frames(2);yield return SelectEditorCondition(pair.condition);
                ClickConfig("ActionCategory"+pair.op);yield return Frames(2);InspectCatalogText(width+" unavailable description "+pair.condition+"/"+pair.op);
                if(pair.condition==1)yield return ColdCapture("typography-description-"+width);
                ClickConfig("CancelRuleEdit");yield return Frames(2);
            }
            game.EditLoadout(d=>{foreach(string action in new[]{"Q02","F05","D04","M03","J03"})d.TryAddRule((int)ConditionalCatalog.Response(action).Operation,new ConditionalRule{Condition=25,Action=action});});yield return Frames(2);
            ClickConfig("Rule0Condition");yield return Frames(2);ColdLayout(width);TextFits(width+" revised description with five rules");
            var categoryScroll=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.ScrollRect>().First(s=>s.name=="SelectorCategories");
            Check(width+" six categories retain visible height",categoryScroll.content.rect.height<=categoryScroll.viewport.rect.height+1);
            yield return ColdCapture("typography-config-"+width);
            ClickConfig("CancelRuleEdit");game.ConfigurePlaytestWave(3);game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
            foreach(var boss in game.Bosses)boss.Health.TakeDamage(99999,game.Player.gameObject);
            yield return Until(()=>game.Phase==DuelPhase.Complete,1.2f);Check(width+" controlled result reaches Complete",game.Phase==DuelPhase.Complete);TextFits(width+" Complete");
        }
        game.MainMenu();yield return Frames(3);
    }
}
#endif
