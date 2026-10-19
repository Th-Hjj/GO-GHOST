#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Condition Action V2 Focused Smoke")]
    public static void RunConditionalV2()=>Start("condition-v2",false);
    [MenuItem("GO GHOST/Playtest/Condition Action V2 UI Smoke")]
    public static void RunConditionalV2UI()=>Start("condition-v2-ui",false);
    static void V2Rule(ConditionalLoadout draft,ConditionalRule rule)
    {
        if(!draft.TryAddRule(ConditionalCatalog.Response(rule.Action).Operation,rule))
            throw new InvalidOperationException("Invalid V2 fixture: "+ConditionalCatalog.RuleConditionLabel(rule));
    }
    [MenuItem("GO GHOST/Playtest/Condition Action V2 Priority and Lifecycle")]
    public static void RunConditionalV2Followup()=>Start("condition-v2-followup",false);
    public static void RunConditionalV2Targets()=>Start("condition-v2-targets",false);
    static ConditionalRule V2Ground(string action,GroundStateOption ground=GroundStateOption.Grounded)
        =>new ConditionalRule{Condition=51,GroundState=ground,Action=action};
    static IEnumerator ConditionalV2()
    {
        Record("V2 focused mechanisms: real executors and virtual input; fixture positions/policies are controlled, not balance proof.");
        Check("V2 exact selectable catalog 26 / 20 in five categories",ConditionalCatalog.Conditions.Length==26
            &&ConditionalCatalog.Responses.Length==20&&ConditionalCatalog.Categories.Length==5);
        Check("V2 removed entries stay nonselectable",new[]{0,27,28,32}.All(id=>!ConditionalCatalog.IsSelectableCondition(id))
            &&!ConditionalCatalog.IsSelectableResponse("Q03")&&!ConditionalCatalog.IsSelectableResponse("F05"));
        Check("V2 optional AND and explicitly allowed Fine-Tune pair",ConditionalCatalog.IsAllowed(2,new ConditionalRule{Condition=18,Action="Q06"}));
        Check("V2 reloading Hold Fire legal",ConditionalCatalog.IsAllowed(3,new ConditionalRule{Condition=9,Action="F06"}));
        var data=V2Ground("F02");data.AdditionalCondition=52;data.AdditionalDistance=EnemyDistanceOption.Mid;data.AmmoUse=AmmoUseOption.KeepTwoRounds;
        var copied=data.Copy();var draft=new ConditionalLoadout();V2Rule(draft,copied);
        copied.GroundState=GroundStateOption.Airborne;
        Check("V2 full data deep copy",draft.Copy().Operations[3].Rules[0].GroundState==GroundStateOption.Grounded
            &&draft.Copy().Operations[3].Rules[0].AdditionalDistance==EnemyDistanceOption.Mid
            &&draft.Copy().Operations[3].Rules[0].AmmoUse==AmmoUseOption.KeepTwoRounds);
        var invalid=V2Ground("M10");invalid.AdditionalCondition=51;invalid.AdditionalGroundState=GroundStateOption.Airborne;
        Check("V2 contradictory ground states rejected",!ConditionalCatalog.IsAllowed(0,invalid));
        invalid.AdditionalCondition=59;
        Check("V2 second event rejected",!ConditionalCatalog.IsAllowed(0,invalid));
        Check("V2 required direction/condition parameter cannot silently default",!ConditionalCatalog.IsAllowed(0,new ConditionalRule{Condition=51,Action="M10"})
            &&!ConditionalCatalog.IsAllowed(0,new ConditionalRule{Condition=4,Action="M11"}));

        // Manual jump -> paid burst -> airborne Dodge. No synthetic action-result notifications.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=57,Action="F02"});
            V2Rule(d,new ConditionalRule{Condition=60,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Airborne,Action="D04"});
        });
        AlphaPlace(-6,9);yield return Seconds(.12f);
        var movement=game.Player.GetComponent<PlayerMovement>();int jumps=0,apices=0;
        movement.JumpStarted+=()=>jumps++;movement.JumpApexReached+=()=>apices++;
        MouseAt(game.Rifle.AimOrigin+new Vector2(12,1),false);yield return Frames(3);
        Check("V2 initialization fabricates no jump or automatic action",jumps==0&&game.Rules.AutomaticShots==0&&game.Rules.AcceptedDodges==0);
        heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();
        yield return Until(()=>game.Rules.AcceptedDodges==1,1.5f);
        Check("V2 manual Jump -> Short Burst -> airborne Dodge",jumps==1&&game.Rules.AutomaticShots==3&&game.Rules.AcceptedDodges==1,
            $"jumps={jumps} shots={game.Rules.AutomaticShots} dodge={game.Rules.AcceptedDodges}");
        Check("V2 Fire preserves raw aim without Aim ownership",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned
            &&Vector2.Dot(game.Player.GetComponent<PlayerInputReader>().RawInput.Aim,game.Player.GetComponent<PlayerInputReader>().AimDirection)>.999f);
        yield return Until(()=>movement.IsGrounded,1.5f);
        Check("V2 one real apex per active jump",apices==1&&game.Rules.AcceptedJumps==0);
        yield return Seconds(.35f);
        Check("V2 completed event does not replay",game.Rules.AutomaticShots==3&&game.Rules.AcceptedDodges==1);

        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=25,Action="M04"});
            V2Rule(d,new ConditionalRule{Condition=61,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Grounded,Action="J01"});
            V2Rule(d,new ConditionalRule{Condition=58,Action="F02"});
        });
        AlphaPlace(-5,4);yield return Seconds(.12f);MouseAt(game.Bosses[0].Position,false);
        bool preparing=game.Bosses[0].Actions.TryStart(BossAbility.Piercing,game.Player.transform.position);
        yield return Until(()=>game.Rules.AutomaticShots==3,2f);
        Check("V2 preparation -> completed Step Back -> Jump -> apex Burst",preparing&&game.Rules.AcceptedJumps==1&&game.Rules.AutomaticShots==3,
            $"jump={game.Rules.AcceptedJumps} shots={game.Rules.AutomaticShots}");
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=25,Action="M04"});
            V2Rule(d,new ConditionalRule{Condition=61,Action="J01"});
        });
        AlphaPlace(-14.1f,-7);yield return Seconds(.12f);
        game.Bosses[0].Actions.TryStart(BossAbility.Piercing,game.Player.transform.position);yield return Seconds(.8f);
        Check("V2 wall-blocked Step Back does not report success",game.Rules.AcceptedJumps==0);

        yield return AlphaFresh(1,d=>{
            V2Rule(d,V2Ground("M10"));V2Rule(d,V2Ground("F06"));
            var left=V2Ground("M11");left.MoveDirection=MoveDirectionOption.Left;V2Rule(d,left);
        });
        AlphaPlace(-5,8);yield return Seconds(.1f);float heldX=game.Player.transform.position.x;
        heldKeys=new[]{Key.D};MouseAt(game.Bosses[0].Position,true);yield return Seconds(.25f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        Check("V2 zero output owns Move and Fire over manual/lower rules",reader.ConditionalFrame.MovementOwned&&reader.ConditionalFrame.FireOwned
            &&Mathf.Abs(game.Player.transform.position.x-heldX)<.08f&&game.Rifle.ShotsFired==0&&Feedback(0).Executing&&Feedback(3).Executing);
        heldKeys=new[]{Key.D,Key.Space};yield return Frames(2);heldKeys=new[]{Key.D};yield return Seconds(.12f);
        Check("V2 Hold release restores manual move/fire",game.Player.transform.position.x>heldX+.25f&&game.Rifle.ShotsFired>0);
        heldKeys=Array.Empty<Key>();mouseHeld=false;

        // All three real shooting responses share the same reserve option.
        foreach(string action in new[]{"F01","F02","F03"})
        {
            yield return AlphaFresh(1,d=>{
                V2Rule(d,new ConditionalRule{Condition=18,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Airborne,
                    Action=action,AmmoUse=AmmoUseOption.KeepTwoRounds});
            });
            MouseAt(game.Rifle.AimOrigin+Vector2.up*12,true);
            yield return Until(()=>game.Rifle.AmmoInMagazine==3,1.5f);mouseHeld=false;
            MouseAt(game.Bosses[0].Position,false);heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();
            yield return Seconds(.9f);
            Check("V2 "+action+" stops at the two-round floor",game.Rifle.AmmoInMagazine==2&&game.Rules.AutomaticShots==1,
                $"ammo={game.Rifle.AmmoInMagazine} automatic={game.Rules.AutomaticShots}");
            if(action=="F03")
            {
                MouseAt(game.Bosses[0].Position,true);yield return Seconds(.35f);mouseHeld=false;
                Check("V2 reserve yields to manual remaining rounds",game.Rifle.AmmoInMagazine<2||game.Rifle.IsReloading);
            }
        }

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=59,Action="F01",AmmoUse=AmmoUseOption.KeepTwoRounds}));
        AlphaPlace(-5,7);yield return Seconds(.1f);int hitEvents=0;
        game.Rifle.EnemyHit+=(enemy,dead)=>hitEvents++;
        MouseAt(game.Bosses[0].Position,true);yield return Until(()=>game.Rifle.ShotsFired>0,.3f);mouseHeld=false;
        yield return Seconds(1.6f);
        Check("V2 real Shot->Shot loop is bounded by ammo/cadence",game.Rifle.ShotsFired==6&&game.Rifle.AmmoInMagazine==2&&game.Rules.AutomaticShots==5,
            $"total={game.Rifle.ShotsFired} auto={game.Rules.AutomaticShots} ammo={game.Rifle.AmmoInMagazine}");
        Check("V2 delayed real projectile damage reports Hit",hitEvents>0&&game.Rifle.ShotsHit==hitEvents);

        yield return AlphaFresh(1,d=>{
            V2Rule(d,V2Ground("F02"));V2Rule(d,new ConditionalRule{Condition=60,Action="D04"});
        });
        yield return Until(()=>game.Rules.AutomaticShots==1,.3f);
        heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();yield return Seconds(.6f);
        Check("V2 cancelled Burst emits no successful completion",game.Rules.AutomaticShots==1&&game.Rules.AcceptedDodges==0);
        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=57,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Grounded,Action="F01"}));
        heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();yield return Seconds(1.1f);
        Check("V2 expired event cannot wait for a later additional state",game.Rifle.ShotsFired==0);

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=3,Action="J01"}));
        Check("V2 grounded spawn emits no fake landing",game.Rules.AcceptedJumps==0);
        heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();yield return Seconds(1.85f);
        Check("V2 real Landed -> Jump loop needs physical landings",game.Rules.AcceptedJumps>=1&&game.Rules.AcceptedJumps<=3);
        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=7,Action="F01"}));
        bool finishDuringImmunity=false;int finished=0;
        game.Dodge.DodgeFinished+=()=>{finished++;finishDuringImmunity=game.Player.IsInvulnerable;};
        heldKeys=new[]{Key.LeftShift};yield return Frames(2);heldKeys=Array.Empty<Key>();yield return Seconds(.4f);
        Check("V2 Dodge Finished is one movement-end event",finished==1&&finishDuringImmunity&&game.Rules.AutomaticShots==1);
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=11,Action="J01"});V2Rule(d,new ConditionalRule{Condition=9,Action="F06"});
        });
        MouseAt(game.Rifle.AimOrigin+Vector2.up*12,true);yield return Until(()=>game.Rifle.IsReloading,2.3f);mouseHeld=false;
        yield return Frames(2);
        Check("V2 Reloading Hold Fire owns while ammo is empty",game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned&&Feedback(3).Executing);
        int reloads=0;game.Rifle.ReloadFinished+=()=>reloads++;
        yield return Until(()=>game.Rules.AcceptedJumps==1,1.6f);
        Check("V2 Reload Finished follows actual reload only once",reloads==1&&game.Rules.AcceptedJumps==1&&game.Rifle.AmmoInMagazine==game.Rifle.MagazineSize);

        // Arrange while gameplay input is disabled; a queued previous mouse sample must not
        // shoot between teleporting the fixture and applying its intended cursor direction.
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;game.ConfigurePlaytestWave(3);
        V2Rule(game.DraftLoadout,new ConditionalRule{Condition=52,Distance=EnemyDistanceOption.Close,AdditionalCondition=18,Action="F01"});
        game.StartFight();
        var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        V2PreparePlace(game.Player.gameObject,gunner.Navigation,0);V2PreparePlace(gunner.gameObject,gunner.Navigation,10);V2PreparePlace(sword.gameObject,sword.Navigation,-4);
        MouseAt(gunner.Position,false);yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();yield return Seconds(.3f);
        Check("V2 two bosses cannot supply separate AND facts",game.Rifle.ShotsFired==0,$"shots={game.Rifle.ShotsFired}");
        MouseAt(sword.Position,false);yield return Seconds(.3f);
        Check("V2 same visible boss satisfies both AND facts",game.Rifle.ShotsFired==1,$"shots={game.Rifle.ShotsFired}");
        var snapshot=game.Rules;checkingPause=true;game.SetPause(true);float pausedTime=Time.time;
        yield return RealSeconds(.2f);
        Check("V2 pause freezes real time and keeps controller",Time.time==pausedTime&&game.Rules==snapshot);
        game.SetPause(false);checkingPause=false;yield return Seconds(.12f);
        Check("V2 resume does not replay consumed occurrence",game.Rifle.ShotsFired==1);
        checkingPause=true;yield return DeveloperKey(Key.F8);float frozen=Time.time;
        yield return RealSeconds(.2f);
        Check("V2 F8 freezes time and retains active rules",game.DeveloperToolsOpen&&game.Paused&&Time.time==frozen&&game.Rules==snapshot);
        yield return DeveloperKey(Key.F8);checkingPause=false;
        Check("V2 F8 resumes the same controller",!game.DeveloperToolsOpen&&!game.Paused&&game.Rules==snapshot);
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(99999,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Complete,1.2f);
        Check("V2 real third-wave completion disposes controller",game.Phase==DuelPhase.Complete);
        game.NewGame();Check("V2 New Game clears rule data",game.DraftLoadout.TotalRules==0);
        foreach(int wave in new[]{1,2,3})
        {
            yield return AlphaFresh(wave,null,false);MouseAt(game.Bosses[0].Position,true);heldKeys=new[]{Key.D};
            yield return Seconds(.65f);heldKeys=Array.Empty<Key>();mouseHeld=false;
            Check("V2 short W"+wave+" normal executor smoke",game.Phase==DuelPhase.Fight&&game.Rifle.ShotsFired>0);
        }
        game.MainMenu();yield return Frames(3);
        Check("V2 cleanup clears actors and projectiles",Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0&&Projectile.Active.Count==0);
    }
    [MenuItem("GO GHOST/Playtest/Alpha 50 Focused Smoke")]
    public static void RunAlpha50()=>RunConditionalV2();
    public static void RunAlpha50Edges()=>Start("alpha50-edges",false);
    public static void RunAlpha50Layouts()=>Start("alpha50-layouts",false);
    public static void RunUnifiedCatalog()=>Start("unified-catalog",false);
    [MenuItem("GO GHOST/Playtest/Condition Editor Smoke")]
    public static void RunConditionEditor()=>RunConditionalV2UI();
    public static void RunAlphaCursorChecks()=>Start("alpha50-cursor",false);
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
    static IEnumerator ConditionEditor()
    {
        Record("SIDE-BY-SIDE CONDITION EDITOR: real uGUI, virtual pointer/Escape and bounded original-executor checks; no build or combat matrix.");
        game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(8);
        Check("empty configuration has no defaults and no operation navigation",game.DraftLoadout.TotalRules==0&&ConfigButtonNamed("StartFight").interactable
            &&!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name.StartsWith("ActionCategory")||b.name.StartsWith("Operation")));
        yield return PointerConfig("AddRule");
        Check("real virtual pointer opens Condition without choosing operation",Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule").Condition==0
            &&!ConfigButtonNamed("StartFight").interactable&&game.DraftLoadout.TotalRules==0);
        int[][] expected={new[]{1,4,6},new[]{9,11,12},new[]{17,18,40},new[]{27,28,32},new[]{33,34,35,39,45},new[]{25,41,42,43}};
        var seen=new System.Collections.Generic.List<string>();
        for(int category=0;category<6;category++)
        {
            ClickConfig("ConditionCategory"+category);yield return Frames(2);
            var options=CatalogOptions();seen.AddRange(options.Select(OptionLabel));
            Check("Condition category "+category+" exact options and no auto-selection",options.Select(OptionLabel).SequenceEqual(expected[category].Select(id=>ConditionalCatalog.Condition(id).Label))
                &&options.All(b=>b.interactable)&&Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule").Condition==0);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(options[0].gameObject);yield return Frames(2);
            Check("Condition category "+category+" focus shows real description",EditorText("OptionDescription").text.Contains(ConditionalCatalog.ConditionDescription(expected[category][0])));
        }
        Check("exactly 21 formal conditions once, with no historical entries",seen.Count==21&&seen.Distinct().Count()==21&&ConditionalCatalog.Conditions.Length==21);
        yield return SelectEditorCondition(1);
        int chosenCondition=Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule").Condition;
        var actions=new System.Collections.Generic.List<string>();
        foreach(int op in new[]{2,3,4,0,1})
        {
            ClickConfig("ActionCategory"+op);yield return Frames(2);
            actions.AddRange(CatalogOptions().Select(OptionLabel));
            Check("Action category "+op+" visible order and shared compatibility",CatalogOptions().Select(OptionLabel).SequenceEqual(ConditionalCatalog.FormalResponses[op].Where(id=>id!="Q03").Select(id=>ConditionalCatalog.Response(id).Label))
                &&CatalogOptions().Select(b=>b.interactable).SequenceEqual(ConditionalCatalog.FormalResponses[op].Where(id=>id!="Q03").Select(id=>ConditionalCatalog.IsAllowed(op,1,id)))
                &&Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule").Condition==chosenCondition);
        }
        Check("18 visible actions, 19 retained definitions, all five categories remain",actions.Count==18&&actions.Distinct().Count()==18&&!actions.Contains("Aim at Detected Enemy")&&ConditionalCatalog.Responses.Length==19
            &&Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.name.StartsWith("ActionCategory")).Select(OptionLabel).SequenceEqual(new[]{"Aim","Fire","Dodge","Move","Jump"}));
        var blocked=ConfigButtonNamed("Option","Jump Once");mousePosition=RectTransformUtility.WorldToScreenPoint(null,blocked.transform.position);yield return Frames(5);
        Check("disabled Action hover explains rejection without redundant category text",!blocked.interactable&&EditorText("OptionDescription").text.Contains(ConditionalCatalog.IncompatibilityReason(1,1,"J01"))&&!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.name=="NoAvailableActions"));
        blocked.onClick.Invoke();game.StartFight();yield return Frames(2);
        Check("invalid pairing cannot commit even through callback or Start",game.Phase==DuelPhase.Configure&&game.DraftLoadout.TotalRules==0);
        heldKeys=new[]{Key.Escape};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Frames(3);
        Check("Escape discards incomplete new draft",game.DraftLoadout.TotalRules==0&&ConfigButtonNamed("StartFight").interactable&&ConfigButtonNamed("AddRule").interactable);

        yield return PointerConfig("AddRule");yield return PointerConfig("ConditionCategory5");
        yield return PointerConfig("Option","Enemy Preparing an Attack");
        Check("pointer transition cannot leak old Condition description into Action",Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule").Condition==25
            &&!EditorText("OptionDescription").text.Contains(ConditionalCatalog.ConditionDescription(25)));
        yield return PointerConfig("Option","Aim at Nearest Enemy");
        Check("complete virtual-pointer flow commits one valid rule",game.DraftLoadout.TotalRules==1&&game.DraftLoadout.Operations[2].Rules[0].Action=="Q01"&&ConfigButtonNamed("StartFight").interactable);
        ClickConfig("RemoveRule0");yield return Frames(2);
        var legacy=new ConditionalLoadout();Rule(legacy,0,33,"M01");Rule(legacy,3,33,"F01");legacy.TryAddRule(2,new ConditionalRule{Condition=25,Action="Q03"});
        Check("adding through existing API appends after unnumbered legacy rows",legacy.Copy().OrderedRules().Select(r=>r.Action).SequenceEqual(new[]{"M01","F01","Q03"}));

        yield return AddEditorRule(33,"M01");yield return AddEditorRule(33,"F01");yield return AddEditorRule(33,"M02");
        Check("mixed operations stay in addition order",EquippedActions().SequenceEqual(new[]{"M01","F01","M02"}));
        ClickConfig("MoveRuleUp2");yield return Frames(2);ClickConfig("MoveRuleUp1");yield return Frames(2);
        Check("reordering changes same-operation priority only",EquippedActions().SequenceEqual(new[]{"M02","M01","F01"})&&game.DraftLoadout.Operations[0].Rules.Select(r=>r.Action).SequenceEqual(new[]{"M02","M01"}));
        var original=game.DraftLoadout;var copy=original.Copy();
        Check("Copy preserves mixed display order and deep copies rows",copy.OrderedRules().Select(r=>r.Action).SequenceEqual(EquippedActions())&&!ReferenceEquals(copy.Operations[0].Rules[0],original.Operations[0].Rules[0]));
        typeof(DuelGame).GetProperty("DraftLoadout").SetValue(game,copy);Object.FindAnyObjectByType<DuelUI>().Show();yield return Frames(3);
        Check("rebinding a copied loadout preserves the visible order",ConfigButtonNamed("Rule0Action").GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Retreat from Enemy")&&ConfigButtonNamed("Rule2Action").GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Fire One Shot"));
        yield return AddEditorRule(33,"Q01");yield return AddEditorRule(33,"J01");
        Check("five is shared budget; Add disabled but Edit available",game.DraftLoadout.TotalRules==5&&!ConfigButtonNamed("AddRule").interactable&&ConfigButtonNamed("Rule4Action").interactable);
        ClickConfig("Rule4Condition");yield return Frames(2);yield return SelectEditorCondition(1);
        Check("incompatible old Action cleared only in local draft",Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule").Action==null
            &&game.DraftLoadout.Operations[1].Rules[0].Condition==33&&ConfigButtonNamed("Rule4Action").GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Jump Once"));
        game.StartFight();Check("unfinished edit blocks direct StartFight",game.Phase==DuelPhase.Configure);
        ClickConfig("CancelRuleEdit");yield return Frames(2);
        ClickConfig("Rule0Condition");yield return Frames(2);yield return SelectEditorCondition(4);
        Check("compatible old Action retained in draft",Private<ConditionalRule>(Object.FindAnyObjectByType<DuelUI>(),"pendingRule").Action=="M02");
        ClickConfig("SelectorBack");yield return Frames(2);
        Check("Action can return to Condition without changing equipped rule",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="ConditionCategory0")&&game.DraftLoadout.Operations[0].Rules[0].Condition==33);
        heldKeys=new[]{Key.Escape};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Frames(3);
        Check("Escape cancels existing edit without damage",game.DraftLoadout.Operations[0].Rules[0].Condition==33&&ConfigButtonNamed("StartFight").interactable);
        ClickConfig("Rule4Action");yield return Frames(2);ClickConfig("ActionCategory3");yield return Frames(2);ClickConfig("Option","Keep Firing");yield return Frames(2);
        Check("cross-operation edit at five retains row without duplicate",EquippedActions().SequenceEqual(new[]{"M02","M01","F01","Q01","F03"})&&game.DraftLoadout.Operations[1].Rules.Count==0&&game.DraftLoadout.Operations[3].Rules.Count==2);

        foreach(int width in new[]{1280,1920})
        {
            SetAlphaGameSize(width,width*9/16);yield return Frames(6);
            ClickConfig("Rule4Condition");yield return Frames(2);ClickConfig("ConditionCategory4");yield return Frames(5);
            CheckEditorLayout(width,"Condition");
            Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+$"editor-condition-{width}.png");yield return Frames(3);
            yield return SelectEditorCondition(9);ClickConfig("ActionCategory3");yield return Frames(4);
            mousePosition=RectTransformUtility.WorldToScreenPoint(null,ConfigButtonNamed("Option","Fire One Shot").transform.position);yield return Frames(4);
            CheckEditorLayout(width,"Action");
            ScreenCapture.CaptureScreenshot(Output+$"editor-action-{width}.png");yield return Frames(3);
            ClickConfig("CancelRuleEdit");yield return Frames(2);
        }
        // One short controlled combat sample checks the unchanged operation loop and the copied order.
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,-2);yield return Seconds(.18f);
        Check("Start snapshot retains order and concurrent Move/Aim/Fire",game.Rules.RuleAt(ControlOperation.Movement,0).Action=="M02"
            &&game.Rules.RuleAt(ControlOperation.Fire,1).DisplayOrder==4&&Feedback(0,0).Executing&&Feedback(2).Executing&&game.Rifle.TriggerPulls>0);
        game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(3);
        Check("controlled defeat/reconfigure keeps mixed order",EquippedActions().SequenceEqual(new[]{"M02","M01","F01","Q01","F03"})&&ConfigButtonNamed("Rule4Action").GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Keep Firing"));
        for(int i=0;i<5;i++){ClickConfig("RemoveRule0");yield return Frames(2);}
        Check("removing all rules returns to empty manual configuration",game.DraftLoadout.TotalRules==0&&ConfigButtonNamed("StartFight").interactable);
        yield return AlphaFresh(1,null);heldKeys=new[]{Key.D};yield return Seconds(.15f);
        Check("empty configuration retains actual manual movement",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.x>1);
        heldKeys=Array.Empty<Key>();game.MainMenu();yield return Frames(2);
    }
    static void CheckEditorLayout(int width,string step)
    {
        Canvas.ForceUpdateCanvases();
        var ui=Object.FindAnyObjectByType<DuelUI>();
        var panels=ui.GetComponentsInChildren<RectTransform>();
        var left=panels.First(r=>r.name=="RuleEditor");var right=panels.First(r=>r.name=="RuleList");
        Check($"{width} {step}: selector and complete list side by side",Screen.width==width&&left.position.x<right.position.x
            &&right.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("Rule")&&b.name.EndsWith("Action"))==5
            &&left.sizeDelta.x/(left.sizeDelta.x+right.sizeDelta.x)>.51f&&left.sizeDelta.x/(left.sizeDelta.x+right.sizeDelta.x)<.54f);
        var overflow=left.GetComponentsInChildren<TMPro.TMP_Text>().Concat(right.GetComponentsInChildren<TMPro.TMP_Text>()).Where(t=>t.isTextOverflowing).Select(t=>t.name).ToArray();
        Check($"{width} {step}: no label/description overflow",overflow.Length==0,string.Join(",",overflow));
        var rows=panels.First(r=>r.name=="EquippedRules").GetComponent<UnityEngine.UI.ScrollRect>();
        rows.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
        var last=(RectTransform)ConfigButtonNamed("Rule4Action").transform;
        Check($"{width} {step}: final row reachable and list actions aligned",rows.viewport.rect.Contains(rows.viewport.InverseTransformPoint(last.position))
            &&ConfigButtonNamed("StartFight").transform.parent==right
            &&Mathf.Approximately(ConfigButtonNamed("StartFight").transform.position.y,ConfigButtonNamed("AddRule").transform.position.y));
    }
    static IEnumerator UnifiedCatalog()
    {
        Record("CONDITION-FIRST EDITOR: unified rows, action categories and focused executor examples; no combination matrix.");
        game.NewGame();yield return Frames(2);
        int[] conditions={25,45,25,41,9};string[] responses={"M04","J01","Q03","F01","D03"};
        for(int op=0;op<5;op++)
        {
            ClickConfig("AddRule");yield return Frames(2);
            var options=CatalogOptions();
            var headings=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.name.StartsWith("ConditionCategory")).ToArray();
            Check($"new rule {op}: same 21 A in category order",options.Select(OptionLabel).SequenceEqual(ConditionalCatalog.Conditions.Select(c=>c.Label))
                &&headings.Select(t=>t.text).SequenceEqual(ConditionalCatalog.Categories));
            Check($"new rule {op}: conditions independent of action category",options.All(b=>b.interactable)
                &&!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name.StartsWith("Operation")||b.name.StartsWith("ActionCategory")));
            ClickConfig("Option",ConditionalCatalog.Condition(conditions[op]).Label);yield return Frames(2);
            Check($"new rule {op}: action categories appear only after condition",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>()
                .Where(b=>b.name.StartsWith("ActionCategory")).Select(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text).SequenceEqual(new[]{"Aim","Fire","Dodge","Move","Jump"}));
            ClickConfig("ActionCategory"+op);yield return Frames(2);
            options=CatalogOptions();
            Check($"operation {op}: complete ordered B catalog",options.Select(OptionLabel).SequenceEqual(ConditionalCatalog.FormalResponses[op].Select(id=>ConditionalCatalog.Response(id).Label)));
            ClickConfig("Option",ConditionalCatalog.Response(responses[op]).Label);yield return Frames(2);
            Check($"example {conditions[op]}/{responses[op]} immediately equips in unified list",game.DraftLoadout.TotalRules==op+1&&ConditionalCatalog.IsValid(game.DraftLoadout)
                &&ConfigButtonNamed("Rule"+op+"Action").GetComponentInChildren<TMPro.TMP_Text>().text==ConditionalCatalog.Response(responses[op]).Label
                &&!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name.StartsWith("ActionCategory")||b.name.StartsWith("Operation")));
        }
        Check("five unified rules hide Add without a mode or enable toggle",!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="AddRule")
            &&Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Toggle>().Length==0);
        ClickConfig("Rule0Action");yield return Frames(1);ClickConfig("ActionCategory3");yield return Frames(1);ClickConfig("Option","Keep Firing");yield return Frames(2);
        Check("cross-category edit at budget replaces one row in place",game.DraftLoadout.TotalRules==5&&game.DraftLoadout.Operations[0].Rules.Count==0
            &&game.DraftLoadout.Operations[3].Rules.Select(r=>r.Action).SequenceEqual(new[]{"F03","F01"})&&ConfigButtonNamed("Rule0Action","Keep Firing")!=null);
        ClickConfig("Rule0Action");yield return Frames(1);ClickConfig("ActionCategory0");yield return Frames(1);ClickConfig("Option","Step Back");yield return Frames(2);
        game.StartFight();yield return Frames(3);
        Check("Start Fight accepts all five newly legal examples",game.Phase==DuelPhase.Prepare&&Enumerable.Range(0,5).All(op=>game.Rules.RuleAt((ControlOperation)op,0).Action==responses[op]));
        foreach(var pair in new[]{(2,4),(3,12),(1,1),(3,9)})
        {
            int op=pair.Item1;
            game.NewGame();yield return Frames(2);ClickConfig("AddRule");yield return Frames(1);
            ClickConfig("Option",ConditionalCatalog.Condition(pair.Item2).Label);yield return Frames(1);ClickConfig("ActionCategory"+op);yield return Frames(2);
            var blocked=CatalogOptions().First(b=>!b.interactable);
            Check($"operation {op}: incompatible B stays in original position",(op==2||pair.Item2==12)
                ? OptionLabel(blocked)==(op==2?"Aim at Detected Enemy":"Fire and Keep a Reserve") : CatalogOptions().All(b=>!b.interactable));
            Check($"operation {op}: complete B with explanation",blocked.GetComponentInChildren<TMPro.TMP_Text>().color.r<.7f&&blocked.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("\n")
                &&CatalogOptions().Select(OptionLabel).SequenceEqual(ConditionalCatalog.FormalResponses[op].Select(id=>ConditionalCatalog.Response(id).Label)));
            blocked.onClick.Invoke();game.StartFight();yield return Frames(1);
            Check($"operation {op}: disabled B cannot equip or start",game.DraftLoadout.TotalRules==0&&game.Phase==DuelPhase.Configure);
        }
        foreach(var pair in new[]{(1,1,"J01"),(1,1,"J03"),(3,9,"F01"),(3,9,"F02"),(3,9,"F03"),(3,9,"F05"),(3,12,"F05"),(2,4,"Q03")})
        {
            var invalid=new ConditionalLoadout();Rule(invalid,pair.Item1,pair.Item2,pair.Item3);
            Check($"structural rejection {pair.Item2}/{pair.Item3}",!ConditionalCatalog.IsValid(invalid)&&!ConditionalCatalog.IsAllowed(pair.Item1,pair.Item2,pair.Item3));
        }
        yield return ConditionFirstOrdering();
        yield return AlphaFresh(1,d=>{Rule(d,0,25,"M04");Rule(d,2,25,"Q03");Rule(d,3,25,"F02");});AlphaPlace(-6,2);yield return Seconds(.1f);
        var body=game.Player.GetComponent<Rigidbody2D>();float x=body.position.x;
        bool prepared=game.Bosses[0].Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.18f);
        Check("public preparation executes Step Back and source Aim concurrently",prepared&&body.position.x<x-.5f&&Feedback(0).Glow>0&&Feedback(2).Executing&&game.Rules.CurrentTargetId==game.Bosses[0].ActorId,
            $"prepared={prepared} x={x:F2}->{body.position.x:F2} move={Feedback(0).Glow:F2} aim={Feedback(2).Executing} target={game.Rules.CurrentTargetId} phase={game.Bosses[0].Actions.Phase}");
        yield return Seconds(.45f);
        Check("consumed preparation completes bounded Step Back and three paid burst shots",body.position.x<x-1.2f&&body.position.x>x-1.8f&&game.Rifle.TriggerPulls==3);
        x=body.position.x;yield return Seconds(.15f);
        Check("same preparation cannot restart completed finite responses",Mathf.Abs(body.position.x-x)<.08f&&game.Rifle.TriggerPulls==3);

        yield return AlphaFresh(3,d=>{Rule(d,3,41,"F01");Rule(d,2,41,"Q03");});
        var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-6);OnFloor(gunner.gameObject,gunner.Navigation,2);OnFloor(sword.gameObject,sword.Navigation,-3);yield return Frames(4);
        Fire(gunner,gunner.gameObject,game.Player.GetComponent<Collider2D>().bounds.center+Vector3.right*3,Vector2.left);
        yield return Until(()=>game.Rifle.TriggerPulls>0,.25f);
        Check("Incoming Bullet pays one shot and aims at public source over nearer enemy",game.Rifle.TriggerPulls==1&&game.Rifle.AmmoInMagazine==7&&Feedback(3).Glow>0&&Feedback(2).Glow>0&&game.Rules.CurrentTargetId==gunner.ActorId);
        yield return Seconds(.6f);
        Check("expired threat releases ownership without replay",game.Rifle.TriggerPulls==1&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);

        yield return AlphaFresh(1,d=>Rule(d,4,9,"D03"));AlphaPlace(-6,3);yield return Frames(4);
        MouseAt(game.Rifle.AimOrigin+Vector2.up*6,true);yield return Until(()=>game.Rifle.IsReloading,3);mouseHeld=false;
        yield return Until(()=>game.Dodge.IsDodging,.2f);
        Check("Reloading accepts original movement-facing Dodge",game.Rifle.IsReloading&&game.Dodge.IsDodging&&game.Rules.AcceptedDodges==1&&Feedback(4).Glow>0);

        yield return AlphaFresh(1,d=>Rule(d,1,45,"J01"));AlphaPlace(-14,3);yield return Until(()=>game.Rules.AcceptedJumps>0,.4f);
        Check("Near Wall accepts original grounded Jump",game.Rules.AcceptedJumps==1&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>1&&Feedback(1).Glow>0);
        yield return Seconds(1.2f);Check("same wall occurrence does not replay finite Jump",game.Rules.AcceptedJumps==1);

        yield return AlphaFresh(1,d=>{Rule(d,1,6,"J03");Rule(d,2,6,"Q03");});AlphaPlace(-6,3);yield return Frames(4);
        game.Player.TakeDamage(1,null);yield return Until(()=>game.Rules.AcceptedJumps>0,.3f);
        Check("event Repeat Jumps accepts once; unknown damage source cannot own Aim",game.Rules.AcceptedJumps==1&&Feedback(1).Glow>0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);
        yield return Seconds(1.2f);Check("event Jump expires without a queued repeat",game.Rules.AcceptedJumps==1);

        foreach(int width in new[]{1280,1920})
        {
            RestoreFixture();game.NewGame();SetAlphaGameSize(width,width*9/16);yield return Frames(6);
            ClickConfig("AddRule");yield return Frames(5);
            var scroll=Object.FindAnyObjectByType<DuelUI>().GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            var handleCorners=new Vector3[4];scroll.verticalScrollbar.handleRect.GetWorldCorners(handleCorners);
            Check($"{width}: scrollbar handle stays within track",handleCorners.All(c=>((RectTransform)scroll.verticalScrollbar.transform).rect.Contains(scroll.verticalScrollbar.transform.InverseTransformPoint(c)*.999f)));
            Check($"{width}: category labels fit in scrollable A",Screen.width==width&&scroll.content.rect.height>scroll.viewport.rect.height
                &&scroll.content.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing));
            Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+$"unified-A-top-{width}.png");yield return Frames(3);
            scroll.verticalNormalizedPosition=0;yield return Frames(3);
            var last=CatalogOptions().Last().transform as RectTransform;var corners=new Vector3[4];last.GetWorldCorners(corners);
            Check($"{width}: Terrain final option reachable by scrolling",scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint((corners[0]+corners[2])/2)));
            ScreenCapture.CaptureScreenshot(Output+$"unified-A-bottom-{width}.png");yield return Frames(3);
            ClickConfig("Option","Low Ammo");yield return Frames(1);ClickConfig("ActionCategory3");yield return Frames(5);
            Check($"{width}: complete B and disabled explanation fit",CatalogOptions().Length==4&&CatalogOptions().All(b=>!b.GetComponentInChildren<TMPro.TMP_Text>().isTextOverflowing));
            ScreenCapture.CaptureScreenshot(Output+$"unified-B-{width}.png");yield return Frames(3);
            ClickConfig("Option","Fire One Shot");yield return Frames(2);
            game.EditLoadout(d=>{Rule(d,2,25,"Q03");Rule(d,4,9,"D03");Rule(d,0,25,"M04");Rule(d,1,45,"J01");});yield return Frames(5);
            var editor=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleEditor");
            Check($"{width}: unified five-category rows and Start Fight fit",editor.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing)
                &&ConfigButtonNamed("Rule4Action")!=null&&ConfigButtonNamed("StartFight").interactable);
            ScreenCapture.CaptureScreenshot(Output+$"condition-first-rules-{width}.png");yield return Frames(3);
        }
        RestoreFixture();game.MainMenu();yield return Frames(2);
    }
    static IEnumerator ConditionFirstOrdering()
    {
        game.NewGame();yield return Frames(2);
        foreach(string id in new[]{"M01","F01","M02"})
        {
            ClickConfig("AddRule");yield return Frames(1);ClickConfig("Option","Enemy Close");yield return Frames(1);
            ClickConfig("ActionCategory"+(int)ConditionalCatalog.Response(id).Operation);yield return Frames(1);
            ClickConfig("Option",ConditionalCatalog.Response(id).Label);yield return Frames(2);
        }
        Check("mixed actions retain addition order in one list",ConfigButtonNamed("Rule0Action","Approach Enemy")!=null
            &&ConfigButtonNamed("Rule1Action","Fire One Shot")!=null&&ConfigButtonNamed("Rule2Action","Retreat from Enemy")!=null);
        ClickConfig("MoveRuleUp2");yield return Frames(1);ClickConfig("MoveRuleUp1");yield return Frames(2);
        Check("unified reordering preserves operation-local priority",game.DraftLoadout.Operations[0].Rules.Select(r=>r.Action).SequenceEqual(new[]{"M02","M01"})
            &&game.DraftLoadout.Operations[3].Rules.Count==1&&ConfigButtonNamed("Rule0Action","Retreat from Enemy")!=null);
        ClickConfig("Rule0Condition");yield return Frames(1);ClickConfig("Option","Airborne");yield return Frames(1);
        ClickConfig("SelectorBack");yield return Frames(2);
        Check("pending edit does not mutate equipped data and blocks Start",game.DraftLoadout.Operations[0].Rules[0].Condition==33&&!ConfigButtonNamed("StartFight").interactable);
        ClickConfig("Rule0Condition");yield return Frames(1);ClickConfig("Option","Enemy Close");yield return Frames(1);
        ClickConfig("ActionCategory0");yield return Frames(1);ClickConfig("Option","Retreat from Enemy");yield return Frames(2);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,-2);yield return Seconds(.1f);
        var body=game.Player.GetComponent<Rigidbody2D>();float x=body.position.x;yield return Seconds(.15f);
        Check("unified list drives original priority and actual execution feedback",body.position.x<x-.4f&&Feedback(0,0).Executing&&!Feedback(0,1).Executing
            &&game.Rules.RuleAt(ControlOperation.Movement,0).Action=="M02");
        game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(3);
        Check("retry retains unified order and runtime priorities",game.DraftLoadout.TotalRules==3&&ConfigButtonNamed("Rule0Action","Retreat from Enemy")!=null
            &&ConfigButtonNamed("Rule2Action","Fire One Shot")!=null);
        ClickConfig("RemoveRule1");yield return Frames(2);
        Check("remove frees one slot without disturbing other categories",game.DraftLoadout.TotalRules==2&&game.DraftLoadout.Operations[0].Rules.Count==1
            &&game.DraftLoadout.Operations[3].Rules.Count==1&&ConfigButtonNamed("AddRule")!=null);
        yield return AlphaFresh(1,null);AlphaPlace(-5,2);heldKeys=new[]{Key.D};yield return Seconds(.12f);
        Check("empty unified list retains manual movement fallback",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned
            &&game.Player.GetComponent<Rigidbody2D>().linearVelocity.x>1&&game.DraftLoadout.TotalRules==0);
        heldKeys=Array.Empty<Key>();
    }
    static IEnumerator AlphaCursorChecks()
    {
        yield return AlphaFresh(1,d=>Rule(d,2,18,"Q06"));AlphaPlace(-6,0);yield return Seconds(.1f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        MouseAt(game.Bosses[0].Body.bounds.center+Vector3.up*.4f,false);yield return Seconds(.1f);
        Check("cursor enters Assist Aim",Feedback(2).Executing);
        MouseAt(game.Rifle.AimOrigin+Vector2.up*5,false);yield return Seconds(.1f);
        Check("large mouse intention releases Assist Aim after input update",!reader.ConditionalFrame.AimOwned,
            $"requested={mousePosition} actual={reader.AimScreenPosition} rawAim={reader.RawInput.Aim} screen={Screen.width}x{Screen.height}");
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D04"));AlphaPlace(-6,2);yield return Seconds(.1f);
        reader=game.Player.GetComponent<PlayerInputReader>();var body=game.Player.GetComponent<Rigidbody2D>();
        MouseAt(body.position+Vector2.left*5,false);yield return Seconds(.1f);float x=body.position.x;
        Record($"CURSOR requested={mousePosition} actual={reader.AimScreenPosition} world={reader.RawInput.Cursor} body={body.position}");
        heldKeys=new[]{Key.Space};yield return Until(()=>game.Dodge.IsDodging,.5f);heldKeys=Array.Empty<Key>();yield return Seconds(.06f);
        Check("airborne Dodge follows left world cursor",game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0&&game.Dodge.CommittedHorizontalSpeed<0,
            $"x={x:F2}->{body.position.x:F2} speed={game.Dodge.CommittedHorizontalSpeed:F2} cursor={reader.RawInput.Cursor}");
        game.MainMenu();yield return Frames(2);
    }
    static IEnumerator Alpha50()
    {
        Record("ALPHA RULE EXPANSION: bounded controlled fixtures, original physical executors and paid shots; virtual input only.");
        game.NewGame();yield return Frames(2);
        Check("empty defaults, no independent toggle",game.DraftLoadout.Operations.All(o=>o.Rules.Count==0)
            &&Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Toggle>().Length==0);
        ClickConfig("AddRule");yield return Frames(2);
        Check("Move picker contains the complete 21-condition catalog",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name=="Option")==21);
        ClickConfig("Option","Enemy Close");yield return Frames(2);
        game.StartFight();
        Check("incomplete pair remains UI-local and blocks Start Fight",game.DraftLoadout.Operations[0].Rules.Count==0&&game.Phase==DuelPhase.Configure);
        ClickConfig("ActionCategory0");yield return Frames(1);ClickConfig("Option","Retreat from Enemy");yield return Frames(2);
        Check("complete pair equips without a switch",game.DraftLoadout.Operations[0].Rules.Count==1&&ConditionalCatalog.IsValid(game.DraftLoadout));
        Check("one rule leaves shared slots available",ConfigButtonNamed("AddRule")!=null);
        var copy=game.DraftLoadout.Copy();copy.Operations[0].Rules[0].Condition=9;
        Check("snapshot is a deep copy",game.DraftLoadout.Operations[0].Rules[0].Condition==33);
        game.EditLoadout(d=>{for(int i=1;i<5;i++)Rule(d,0,33,i%2==0?"M01":"M02");});yield return Frames(2);
        Check("five rules in one operation validate and render",ConditionalCatalog.IsValid(game.DraftLoadout)&&game.DraftLoadout.TotalRules==5
            &&ConfigButtonNamed("Rule4Condition")!=null&&!Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="AddRule"));
        bool sixth=game.DraftLoadout.TryAddRule(ControlOperation.Dodge,new ConditionalRule{Condition=1,Action="D04"});
        Check("sixth rule rejected by the shared budget",!sixth&&game.DraftLoadout.TotalRules==5);
        ClickConfig("RemoveRule4");yield return Frames(2);
        Check("removing a rule immediately frees shared capacity",game.DraftLoadout.TotalRules==4&&ConfigButtonNamed("AddRule")!=null);
        game.EditLoadout(d=>d.Operations[0].Rules.Clear());yield return Frames(2);
        Check("delete last rule restores empty manual list",game.DraftLoadout.Operations[0].Rules.Count==0&&ConfigButtonNamed("AddRule")!=null);
        Rule(game.DraftLoadout,0,33,"M10");game.StartFight();
        Check("Start Fight rejects an unapproved historical action",game.Phase==DuelPhase.Configure);
        var split=new ConditionalLoadout();for(int i=0;i<3;i++)Rule(split,0,33,"M02");for(int i=0;i<2;i++)Rule(split,4,1,"D04");
        Check("3+2 distribution and ordered full copy validate",ConditionalCatalog.IsValid(split)&&split.Copy().TotalRules==5&&split.Copy().Operations[0].Rules.Count==3);
        Rule(split,1,33,"J01");Check("directly injected sixth rule remains invalid after copying",!ConditionalCatalog.IsValid(split)&&!ConditionalCatalog.IsValid(split.Copy()));
        Check("formal catalog has 21 unique A and 19 unique B",ConditionalCatalog.FormalConditions.SelectMany(c=>c).Distinct().Count()==21
            &&Enumerable.Range(0,5).SelectMany(op=>ConditionalCatalog.Responses.Where(b=>(int)b.Operation==op&&ConditionalCatalog.FormalConditions[op].Any(a=>ConditionalCatalog.IsAllowed(op,a,b.Id)))).Select(b=>b.Id).Distinct().Count()==19);
        Check("event and source compatibility is shared and bounded",ConditionalCatalog.IsAllowed(1,6,"J03")&&!ConditionalCatalog.IsAllowed(3,12,"F05")
            &&ConditionalCatalog.IsAllowed(3,6,"F03")&&ConditionalCatalog.IsAllowed(3,11,"F05")&&!ConditionalCatalog.IsAllowed(2,4,"Q03")
            &&ConditionalCatalog.IsAllowed(2,32,"Q03")&&ConditionalCatalog.IsAllowed(0,6,"M04"));
        yield return AlphaFresh(1,null);yield return CheckInputs();

        yield return AlphaFresh(1,d=>{for(int i=0;i<5;i++)Rule(d,0,33,i==0?"M01":"M02");});AlphaPlace(-5,-2);yield return Frames(3);
        var body=game.Player.GetComponent<Rigidbody2D>();var reader=game.Player.GetComponent<PlayerInputReader>();float x=body.position.x;yield return Seconds(.12f);
        Check("five same-operation rows run highest executable priority",body.position.x>x+.3f&&Feedback(0,0).Executing&&!Feedback(0,1).Executing&&game.Rules.RuleCount(ControlOperation.Movement)==5);
        game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(3);
        Check("retry retains all five ordered rules",game.DraftLoadout.TotalRules==5&&game.DraftLoadout.Operations[0].Rules[0].Action=="M01");
        ClickConfig("MoveRuleUp1");yield return Frames(2);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();AlphaPlace(-5,-2);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();x=body.position.x;yield return Seconds(.12f);
        Check("reordered priority changes actual movement",body.position.x<x-.3f&&game.Rules.RuleAt(ControlOperation.Movement,0).Action=="M02"&&Feedback(0,0).Executing);
        foreach(var a in game.Bosses)a.Health.TakeDamage(99999,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Configure,1.2f);
        Check("next wave retains all five rules in changed order",game.Wave==2&&game.DraftLoadout.TotalRules==5
            &&game.DraftLoadout.Operations[0].Rules[0].Action=="M02"&&game.DraftLoadout.Operations[0].Rules[1].Action=="M01");
        game.StartFight();yield return Frames(2);
        Check("next wave copies five fresh rows without old execution feedback",game.Phase==DuelPhase.Prepare&&game.Rules.RuleCount(ControlOperation.Movement)==5&&Feedback(0,0).Glow==0);
        game.NewGame();yield return Frames(2);Check("New Game clears the expanded loadout",game.DraftLoadout.TotalRules==0);

        yield return AlphaFresh(1,d=>{Rule(d,0,45,"M02");Rule(d,0,45,"M09");});AlphaPlace(-14.1f,-11);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();yield return Until(()=>Feedback(0,1).Glow>0,.4f);
        Check("blocked high row yields to physically accepted Center movement",Feedback(0,1).Glow>0,$"x={body.position.x:F2} velocity={body.linearVelocity}");
        yield return AlphaFresh(1,d=>{Rule(d,0,4,"M02");Rule(d,0,33,"M01");});AlphaPlace(-6,-2);yield return Frames(3);
        reader=game.Player.GetComponent<PlayerInputReader>();
        yield return Until(()=>Feedback(0,1).Executing,.3f);
        Check("lower row starts while higher condition is false",reader.MoveInput.x>0&&Feedback(0,1).Executing);
        game.Player.TakeDamage(100,game.Bosses[0].gameObject);yield return Until(()=>Feedback(0,0).Executing,.3f);
        Check("newly executable higher row preempts lower response",reader.MoveInput.x<0&&Feedback(0,0).Executing&&!Feedback(0,1).Executing);

        yield return AlphaFresh(3,d=>Rule(d,0,6,"M04"));
        var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-5);OnFloor(gunner.gameObject,gunner.Navigation,2);OnFloor(sword.gameObject,sword.Navigation,-9);yield return Frames(4);
        body=game.Player.GetComponent<Rigidbody2D>();var movement=game.Player.GetComponent<PlayerMovement>();
        game.Player.TakeDamage(1,gunner.gameObject);bool pushed=movement.ApplyHitPush(-1,.45f,.25f);yield return Seconds(.1f);
        Check("damage response waits during real hit-push ownership",pushed&&movement.HitPushActive&&!Feedback(0).Executing);
        yield return Until(()=>!movement.HitPushActive,.5f);x=body.position.x;yield return Seconds(.3f);
        Check("Took Damage Step Back survives bounded hit reaction and uses source",body.position.x<x-.6f&&Feedback(0).Glow>0,$"afterPush={x:F2}->{body.position.x:F2}");
        yield return Seconds(.6f);x=body.position.x;yield return Seconds(.25f);
        Check("finite Step Back is consumed once with no permanent ownership",Mathf.Abs(body.position.x-x)<.08f&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned);

        yield return AlphaFresh(1,d=>Rule(d,0,35,"M03"));AlphaPlace(-8,5);yield return Frames(3);
        game.Bosses[0].Motor.SetMove(-1);yield return Seconds(.1f);
        Check("observed approach starts Keep Mid Range",Feedback(0).Executing&&game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned);
        game.Bosses[0].Motor.Brake();yield return Until(()=>!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned,1.4f);
        Check("stable mid range returns only Move to manual",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned);

        // Stay outside platform shadows so the persistent public Close condition remains true after landing.
        yield return AlphaFresh(1,d=>Rule(d,1,33,"J03"));AlphaPlace(-12,-8);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();reader=game.Player.GetComponent<PlayerInputReader>();movement=game.Player.GetComponent<PlayerMovement>();
        yield return Until(()=>game.Rules.AcceptedJumps>0,.4f);int jumps=game.Rules.AcceptedJumps;x=body.position.x;yield return Seconds(.25f);
        Check("Repeat Jumps makes one accepted takeoff per airtime",jumps==1&&game.Rules.AcceptedJumps==jumps&&body.linearVelocity.y!=0&&!reader.ConditionalFrame.MovementOwned);
        yield return Until(()=>game.Rules.AcceptedJumps>=2,1.4f);
        Check("Repeat Jumps repeats legally after landing without horizontal navigation",game.Rules.AcceptedJumps>=2&&Mathf.Abs(body.position.x-x)<.1f,
            $"jumps={game.Rules.AcceptedJumps} grounded={movement.IsGrounded} position={body.position} velocity={body.linearVelocity}");

        yield return AlphaFresh(3,d=>Rule(d,2,32,"Q03"));gunner=game.Bosses.First(a=>a.ActorId==1);sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-5);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,-2);yield return Frames(4);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);yield return Frames(3);
        bool camo=gunner.Actions.TryStart(BossAbility.Camo,game.Player.transform.position);yield return Frames(5);
        Check("hidden body cannot own Triggering Enemy Aim",camo&&gunner.IsCamo&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);
        gunner.Actions.StopCombat();yield return Frames(4);
        Check("actual observed reappearance preserves its source over nearer enemy",game.Rules.CurrentTargetId==gunner.ActorId&&Feedback(2).Executing&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned);
        yield return Seconds(1);Check("reappearance Aim has a bounded event response",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);

        yield return AlphaFresh(3,d=>Rule(d,2,4,"Q02"));gunner=game.Bosses.First(a=>a.ActorId==1);sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-5);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,-2);yield return Frames(4);
        gunner.Health.TakeDamage(gunner.Health.MaxHealth/2,game.Player.gameObject);game.Player.TakeDamage(100,sword.gameObject);yield return Frames(4);
        Check("Lowest Health Aim uses visible public fraction over nearest distance",game.Rules.CurrentTargetId==gunner.ActorId&&Feedback(2).Executing&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned);

        yield return AlphaFresh(1,d=>Rule(d,2,18,"Q06"));AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Bosses[0].Body.bounds.center+Vector3.up*.4f,false);yield return Frames(4);
        Check("near cursor enables actual Fine-Tune Aim without Fire",Feedback(2).Executing&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned&&game.Rifle.TriggerPulls==0);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);yield return Seconds(.1f);
        Check("large mouse intention change releases Assist Aim",!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);

        yield return AlphaFresh(1,d=>Rule(d,3,40,"F01"));AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Bosses[0].Body.bounds.center,false);yield return Until(()=>game.Rifle.TriggerPulls>0,.4f);
        Check("Fire One Shot pays exactly one real round and lights Fire",game.Rifle.TriggerPulls==1&&game.Rifle.AmmoInMagazine==7&&Feedback(3).Glow>0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.AimOwned);
        yield return Seconds(.8f);Check("same aligned state cannot repeat one-shot action",game.Rifle.TriggerPulls==1&&Feedback(3).Glow==0);
        yield return AlphaFresh(1,d=>{Rule(d,3,6,"F01");Rule(d,3,6,"F01");});AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);game.Player.TakeDamage(1,game.Bosses[0].gameObject);yield return Seconds(.7f);
        Check("duplicate damage rules independently consume the same accepted event",game.Rifle.TriggerPulls==2&&game.Rifle.AmmoInMagazine==6&&Feedback(3,1).LastExecutedAt>Feedback(3,0).LastExecutedAt,$"shots={game.Rifle.TriggerPulls}");
        yield return AlphaFresh(1,d=>Rule(d,3,6,"F03"));AlphaPlace(-6,0);yield return Frames(3);
        MouseAt(game.Player.transform.position+Vector3.up*5,false);game.Player.TakeDamage(1,game.Bosses[0].gameObject);
        yield return Seconds(.95f);int eventShots=game.Rifle.TriggerPulls;yield return Seconds(.3f);
        Check("event Keep Firing pays shots then expires without indefinite firing",eventShots>0&&eventShots<=4
            &&game.Rifle.TriggerPulls==eventShots&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned,$"shots={eventShots}");
        yield return AlphaFresh(1,d=>Rule(d,3,40,"F05"));AlphaPlace(-6,0);yield return Frames(3);MouseAt(game.Bosses[0].Body.bounds.center,false);
        yield return Until(()=>game.Rifle.AmmoInMagazine<=2,2f);yield return Seconds(.4f);
        Check("Fire Reserve stops at exactly two rounds without reload",game.Rifle.TriggerPulls==6&&game.Rifle.AmmoInMagazine==2&&!game.Rifle.IsReloading);
        heldKeys=new[]{Key.D};MouseAt(game.Player.transform.position+Vector3.up*5,false);yield return Frames(4);
        Check("unmatched Fire preserves independent current manual Move",game.Player.GetComponent<PlayerInputReader>().MoveInput.x>0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.FireOwned);heldKeys=Array.Empty<Key>();

        yield return AlphaFresh(1,d=>{for(int i=0;i<3;i++)Rule(d,0,12,"M02");Rule(d,4,41,"D03");Rule(d,4,1,"D04");});AlphaPlace(-6,2);yield return Frames(3);
        Check("3+2 distribution creates all five live independent rows",game.Rules.RuleCount(ControlOperation.Movement)==3&&game.Rules.RuleCount(ControlOperation.Dodge)==2);
        var liveController=game.Rules;checkingPause=true;yield return DeveloperKey(Key.F8);
        Check("existing F8 opens and pauses the expanded live controller",game.DeveloperToolsOpen&&game.Paused&&game.Rules==liveController);
        yield return DeveloperKey(Key.F8);checkingPause=false;
        Check("F8 closes and restores combat with the same rule controller",!game.DeveloperToolsOpen&&!game.Paused&&game.Rules==liveController);
        heldKeys=new[]{Key.A};yield return Frames(3);body=game.Player.GetComponent<Rigidbody2D>();x=body.position.x;
        Fire(game.Bosses[0],game.Bosses[0].gameObject,game.Player.GetComponent<Collider2D>().bounds.center+Vector3.right*3,Vector2.left);
        yield return Until(()=>game.Dodge.IsDodging,.25f);yield return Seconds(.06f);
        Check("incoming real bullet Dodge follows final resolved movement",game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0&&game.Dodge.CommittedHorizontalSpeed<0);heldKeys=Array.Empty<Key>();
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D04"));AlphaPlace(-6,2);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();MouseAt(body.position+Vector2.left*5,false);yield return Seconds(.1f);x=body.position.x;
        heldKeys=new[]{Key.Space};yield return Until(()=>game.Dodge.IsDodging,.5f);heldKeys=Array.Empty<Key>();yield return Seconds(.06f);
        Check("airborne Dodge uses actual world cursor horizontal direction",game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0&&game.Dodge.CommittedHorizontalSpeed<0);

        yield return AlphaDodgeCase();
        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,d=>{Rule(d,0,9,"M02");Rule(d,2,17,"Q01");Rule(d,1,43,"J01");Rule(d,3,40,"F03");Rule(d,4,41,"D01");},false);
            MouseAt(game.Bosses[0].Body.bounds.center,true);yield return Seconds(3);
            Record($"SHORT W{wave} phase={game.Phase} time={game.FightClock:F2} paidShots={game.Rifle.TriggerPulls} hp={game.Player.CurrentHealth}");mouseHeld=false;
        }
        game.MainMenu();yield return Frames(3);
        Check("Menu cleans controller and restores full camera",game.Rules==null&&Camera.main.rect.width==1&&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0);
        Record("NOT TESTED: physical input, balance, natural completion, exhaustive historical combinations, long sessions and audio audition.");
    }
    static IEnumerator AlphaDodgeCase()
    {
        yield return AlphaFresh(1,d=>Rule(d,4,25,"D01"));
        AlphaPlace(-5,1);yield return Seconds(.1f);
        var actor=game.Bosses[0];var body=game.Player.GetComponent<Rigidbody2D>();var movement=game.Player.GetComponent<PlayerMovement>();
        float x=body.position.x,started=game.Dodge.LastDodgeStartedAt;
        bool prepared=actor.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);yield return Seconds(.06f);
        Check("A25 accepts real Dodge away during public preparation",prepared&&game.Dodge.IsDodging&&body.position.x<x-.3f&&Feedback(4).Glow>0,
            $"x={x:F2}->{body.position.x:F2} phase={actor.Actions.Phase} cooldown={game.Dodge.CooldownRemaining:F2}");
        Check("automatic Dodge uses 2.0 second cooldown",Mathf.Approximately(game.Dodge.DodgeStartInterval,2f)&&game.Dodge.CooldownRemaining>1.8f);
        yield return AlphaFresh(1,d=>Rule(d,4,1,"D02"));AlphaPlace(-5,3);yield return Frames(3);
        body=game.Player.GetComponent<Rigidbody2D>();movement=game.Player.GetComponent<PlayerMovement>();
        started=game.Dodge.LastDodgeStartedAt;
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.3f);heldKeys=Array.Empty<Key>();
        Check("manual Dodge starts same 2.0 second cooldown without rule glow",game.Dodge.LastDodgeStartedAt>started
            &&game.Dodge.CooldownRemaining>1.8f&&Feedback(4).Glow==0);
        yield return Until(()=>game.Dodge.CooldownRemaining<=.3f,2f);
        heldKeys=new[]{Key.Space};yield return Until(()=>!movement.IsGrounded&&body.linearVelocity.y>1,.3f);heldKeys=Array.Empty<Key>();
        started=game.Dodge.LastDodgeStartedAt;
        Check("air rule cooldown rejection has no new row glow",game.Dodge.CooldownRemaining>0&&!game.Dodge.TryDodge(1)&&Feedback(4).Glow==0);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>started,.9f);x=body.position.x;yield return Seconds(.06f);
        Check("A01 executes toward public target after full cooldown",game.Dodge.LastDodgeStartedAt>=started+2f&&body.position.x>x+.3f&&Feedback(4).Glow>0&&!movement.IsGrounded,
            $"x={x:F2}->{body.position.x:F2} startInterval={game.Dodge.LastDodgeStartedAt-started:F3}");
        var instance=game.Rules;float glow=Feedback(4).Glow;int accepted=game.Rules.AcceptedDodges;
        checkingPause=true;game.SetPause(true);float clock=Time.time;yield return RealSeconds(.18);
        Check("pause freezes row feedback time and action state",Time.time==clock&&Feedback(4).Glow==glow&&game.Rules==instance);
        game.SetPause(false);checkingPause=false;yield return Seconds(.25f);
        Check("resume rebinds same controller without replay",game.Rules==instance&&game.Rules.AcceptedDodges==accepted);
        yield return Until(()=>movement.IsGrounded,1.2f);yield return Seconds(.6f);
        Check("landing does not replay invalid airborne occurrence",game.Rules.AcceptedDodges==accepted&&Feedback(4).Glow==0);
        game.Player.TakeDamage(99999,null);yield return Frames(3);game.Reconfigure();yield return Frames(2);
        Check("failure retains single-rule draft, disposes old feedback",game.Phase==DuelPhase.Configure&&game.Rules==null&&game.DraftLoadout.Operations[4].Rules.Count==1&&game.DraftLoadout.Operations[4].Rules[0].Condition==1&&instance.Feedback(ControlOperation.Dodge,0).Glow==0);
        game.StartFight();yield return Frames(2);
        Check("retry preparation starts fresh unlit snapshot",game.Phase==DuelPhase.Prepare&&game.Rules!=instance&&Feedback(4).Glow==0);
    }
    static IEnumerator Alpha50Edges()
    {
        Record("CONTROLLED public preparations, blocked response, one-shot priority and next-wave reset.");
        yield return AlphaFresh(3,d=>Rule(d,4,25,"D01"));
        var gunner=game.Bosses.First(a=>a.ActorId==1);var sword=game.Bosses.First(a=>a.ActorId==2);
        OnFloor(game.Player.gameObject,gunner.Navigation,-6);OnFloor(gunner.gameObject,gunner.Navigation,3);OnFloor(sword.gameObject,sword.Navigation,-2);yield return Frames(4);
        // Preserve the shared major-attack budget; do not force an illegal overlapping tell.
        float before=game.Dodge.LastDodgeStartedAt;
        bool s=sword.Actions.TryStart(BossAbility.Rhythm,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Until(()=>game.Dodge.LastDodgeStartedAt>before,.25f);float first=game.Dodge.LastDodgeStartedAt;
        yield return Seconds(.18f);
        bool g=gunner.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.3f);
        Check("major attack mutual exclusion and Dodge cooldown retained",s&&!g&&game.Dodge.LastDodgeStartedAt==first);
        yield return Until(()=>!sword.Actions.Busy&&game.Dodge.CanDodge,4f);
        yield return Seconds(.8f);
        OnFloor(game.Player.gameObject,gunner.Navigation,-6);yield return Frames(3);
        var dodgeBody=game.Player.GetComponent<Rigidbody2D>();
        float startX=dodgeBody.position.x, beforeGunner=game.Dodge.LastDodgeStartedAt;
        float away=Mathf.Sign(startX-gunner.Body.bounds.center.x);
        g=gunner.Actions.TryStart(BossAbility.Piercing,game.Player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.08f);
        Check("independent next source accepts a second real Dodge",g&&game.Dodge.LastDodgeStartedAt>beforeGunner&&game.Dodge.IsDodging&&(dodgeBody.position.x-startX)*away>.3f&&Feedback(4,0).Glow>0,
            $"before={beforeGunner:F3} next={game.Dodge.LastDodgeStartedAt:F3} bodyX={startX:F2}->{dodgeBody.position.x:F2} direction={game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.DodgeDirection:F0}");
        yield return Until(()=>!gunner.Actions.Busy,4f);yield return Seconds(.8f);
        bool next=sword.Actions.TryStart(BossAbility.Guard,game.Player.transform.position);
        yield return Frames(3);
        Check("defensive Guard is not an attack preparation trigger",!sword.TryGetContact(out var contact)||!contact.Preparing);
        Record("Sustained overlapping major preparations: NOT OBSERVED (shared major-attack budget retained). No forced cooldown.");

        yield return AlphaFresh(1,d=>{Rule(d,1,33,"J01");Rule(d,4,25,"D01");});
        AlphaPlace(-6,1);yield return Seconds(.1f);var actor=game.Bosses[0];
        actor.Actions.TryStart(BossAbility.Piercing,game.Player.transform.position);
        OnFloor(actor.gameObject,actor.Navigation,-4);yield return Frames(3);
        var body=game.Player.GetComponent<Rigidbody2D>();
        Check("accepted Dodge suppresses simultaneous conditional Jump",game.Dodge.IsDodging&&body.linearVelocity.y<1&&Feedback(4).Glow>0&&Feedback(1).Glow==0);
        yield return Until(()=>body.linearVelocity.y>1,.4f);
        Check("rejected Jump retries while Close remains true",body.linearVelocity.y>1&&Feedback(1).Glow>0);

        yield return AlphaFresh(1,d=>Rule(d,0,33,"M02"));
        AlphaPlace(-14.1f,-11);yield return Seconds(.45f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        Check("real wall blockage releases Move",!reader.ConditionalFrame.MovementOwned&&!Feedback(0).Executing);
        foreach(var a in game.Bosses)a.Health.TakeDamage(99999,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Configure,1.2f);
        Check("next wave retains completed rules",game.Wave==2&&game.DraftLoadout.Operations[0].Rules.Count==1);
        game.StartFight();yield return Frames(2);Check("next wave preparation has no old glow",Feedback(0).Glow==0);
        game.NewGame();Check("New Game clears all rules",game.DraftLoadout.Operations.All(o=>o.Rules.Count==0));
        game.MainMenu();yield return Frames(2);
    }
    static IEnumerator Alpha50Layouts()
    {
        Record("LAYOUT: real Game View captures, complete snapshots, no image mockups.");
        foreach(int width in new[]{1280,1920})
        {
            foreach(int count in new[]{0,1,5})
            {
                yield return AlphaFresh(3,d=>
                {
                    if(count==1)Rule(d,4,25,"D01");
                    if(count==5)
                    {
                        // Full-health actors keep these rows inactive while mouse conversion is measured.
                        foreach(string action in new[]{"M01","M02","M03","M04","M09"})Rule(d,0,4,action);
                    }
                });
                SetAlphaGameSize(width,width*9/16);yield return Frames(10);
                var camera=Camera.main;var arena=game.ArenaBounds;
                Vector3 min=camera.WorldToViewportPoint(arena.min),max=camera.WorldToViewportPoint(arena.max);
                Check($"{width} count={count}: entire arena inside battle viewport",min.x>=0&&min.y>=0&&max.x<=1&&max.y<=1,$"min={min} max={max}");
                var sidebar=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="BattleRules");
                Check($"{width} count={count}: sidebar visibility",(sidebar!=null)==(count>0));
                if(sidebar!=null)
                {
                    var labels=sidebar.GetComponentsInChildren<TMPro.TMP_Text>();
                    Check($"{width} count={count}: labels fit and remain mouse-transparent",labels.All(t=>!t.isTextOverflowing&&!t.raycastTarget)
                        &&sidebar.GetComponentsInChildren<UnityEngine.UI.Selectable>().Length==0);
                }
                // Conditional resolution runs on physics ticks; four render frames may be shorter than one tick.
                Vector2 cursorWorld=game.Player.transform.position+new Vector3(5,2);
                MouseAt(cursorWorld,false);yield return Seconds(.1f);
                var expected=(cursorWorld-game.Rifle.AimOrigin).normalized;
                var input=game.Player.GetComponent<PlayerInputReader>();
                Check($"{width} count={count}: manual mouse world aim",Vector2.Dot(input.AimDirection,expected)>.999f,
                    $"requested={mousePosition} actual={input.AimScreenPosition} expected={expected} aim={input.AimDirection}");
                Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+$"alpha50-rules-{count}-{width}.png");yield return Frames(4);
            }
        }
        foreach(int width in new[]{1280,1920})
        {
            RestoreFixture();game.NewGame();SetAlphaGameSize(width,width*9/16);yield return Frames(5);
            game.EditLoadout(d=>{for(int i=0;i<5;i++)Rule(d,0,33,i%2==0?"M01":"M02");});yield return Frames(5);
            var ui=Object.FindAnyObjectByType<DuelUI>();var editor=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleEditor");
            Check($"{width}: five configuration rows fit with accessible Start Fight",editor.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing)
                &&ConfigButtonNamed("Rule4Condition")!=null&&ConfigButtonNamed("MoveRuleUp4").interactable&&ConfigButtonNamed("StartFight").interactable);
            ScreenCapture.CaptureScreenshot(Output+$"alpha50-config-five-{width}.png");yield return Frames(3);
        }
        RestoreFixture();game.NewGame();yield return Frames(3);
        ClickConfig("AddRule");yield return Frames(2);
        ScreenCapture.CaptureScreenshot(Output+"alpha50-local-picker.png");yield return Frames(3);
        game.MainMenu();yield return Frames(3);
    }
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
    // Append inside the existing BossEncounterSmoke partial class in Alpha50Smoke.cs.
    // Uses its real Button callbacks plus the existing ColdLight text/layout helpers.
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
    static IEnumerator V2UICapture(string name)
    {Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+"condition-v2-ui-"+name+".png");yield return Frames(3);}
    static void V2UIFiveLayout(int width,string step)
    {
        Canvas.ForceUpdateCanvases();var ui=Object.FindAnyObjectByType<DuelUI>();
        var panels=ui.GetComponentsInChildren<RectTransform>();
        var body=panels.First(r=>r.name=="BodyPreview");var selector=panels.First(r=>r.name=="RuleEditor");
        var list=panels.First(r=>r.name=="RuleList");
        Check($"V2 UI {width} {step}: actual size and three preserved regions",Screen.width==width&&Screen.height==width*9/16
            &&body.position.x<selector.position.x&&selector.position.x<list.position.x&&Region(0).GetComponentInParent<CanvasGroup>().alpha==1);
        Check($"V2 UI {width} {step}: five complete rows and accessible Start/Add",game.DraftLoadout.TotalRules==5
            &&list.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("Rule")&&b.name.EndsWith("Action"))==5
            &&ConfigButtonNamed("StartFight").transform.parent==list
            &&Mathf.Approximately(ConfigButtonNamed("StartFight").transform.position.y,ConfigButtonNamed("AddRule").transform.position.y)
            &&!ConfigButtonNamed("AddRule").interactable);
        TextFits($"V2 UI {width} {step}");
        var rules=game.DraftLoadout.OrderedRules();
        bool labels=true;
        for(int i=0;i<rules.Count;i++)
        {
            labels&=ConfigButtonNamed("Rule"+i+"Condition").GetComponentInChildren<TMPro.TMP_Text>().text=="IF  "+ConditionalCatalog.ConditionLabel(rules[i]);
            labels&=ConfigButtonNamed("Rule"+i+"Action").GetComponentInChildren<TMPro.TMP_Text>().text=="THEN  "+ConditionalCatalog.RuleActionLabel(rules[i]);
            if(rules[i].AdditionalCondition!=0)
                labels&=ConfigButtonNamed("Rule"+i+"AdditionalCondition").GetComponentInChildren<TMPro.TMP_Text>().text=="AND  "+ConditionalCatalog.ConditionLabel(rules[i],true);
        }
        Check($"V2 UI {width} {step}: actual AND/direction/ammo text",labels);
    }
    static IEnumerator ConditionalV2UI()
    {
        Record("V2 UI: real configuration Button callbacks and actual 1280/1920 Game View renders; controlled policies/defeat prove UI and lifecycle, not balance.");
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(8);
        Check("V2 UI starts empty without hidden defaults",game.DraftLoadout.TotalRules==0&&ConfigButtonNamed("StartFight").interactable);
        ClickConfig("AddRule");yield return Frames(2);
        var primaryLabels=new System.Collections.Generic.List<string>();
        for(int category=0;category<ConditionalCatalog.Categories.Length;category++)
        {
            ClickConfig("ConditionCategory"+category);yield return Frames(2);
            var expected=ConditionalCatalog.Conditions.Where(c=>c.Category==category).Select(c=>c.Label);
            primaryLabels.AddRange(CatalogOptions().Select(OptionLabel));
            Check("V2 UI primary category "+category+" catalog and no auto-selection",CatalogOptions().Select(OptionLabel).SequenceEqual(expected)
                &&V2UIPending().Condition==0);
        }
        Check("V2 UI all 26 selectable conditions displayed once",primaryLabels.Count==26&&primaryLabels.Distinct().Count()==26);
        yield return V2UISelect(18);yield return V2UIAction("Q06");
        Check("V2 UI Fine-Tune standalone commits immediately without AND",game.DraftLoadout.TotalRules==1
            &&game.DraftLoadout.OrderedRules()[0].AdditionalCondition==0&&game.DraftLoadout.OrderedRules()[0].Action=="Q06");

        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(57);
        ClickConfig("AdditionalCondition");yield return Frames(2);
        var stateLabels=new System.Collections.Generic.List<string>();
        int[] stateCategories=ConditionalCatalog.Conditions.Where(c=>!c.Event).Select(c=>c.Category).Distinct().ToArray();
        var categoryButtons=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.name.StartsWith("ConditionCategory")).ToArray();
        Check("V2 UI AND hides categories without states",categoryButtons.Select(b=>b.name).SequenceEqual(stateCategories.Select(c=>"ConditionCategory"+c)));
        foreach(int category in stateCategories)
        {
            ClickConfig("ConditionCategory"+category);yield return Frames(2);
            var expected=ConditionalCatalog.Conditions.Where(c=>c.Category==category&&!c.Event).Select(c=>c.Label);
            stateLabels.AddRange(CatalogOptions().Select(OptionLabel));
            Check("V2 UI AND category "+category+" contains only actual State entries",CatalogOptions().Select(OptionLabel).SequenceEqual(expected));
        }
        Check("V2 UI optional picker exposes 16 states, no events",stateLabels.Count==16&&stateLabels.Distinct().Count()==16);
        yield return SelectEditorCondition(51);
        Check("V2 UI Ground State needs an explicit branch before commit",V2UIPending().AdditionalGroundState==GroundStateOption.Unspecified
            &&game.DraftLoadout.TotalRules==1&&!ConfigButtonNamed("StartFight").interactable);
        ClickConfig("StartFight");yield return Frames(2);
        Check("V2 UI incomplete parameter cannot start fight through callback",game.Phase==DuelPhase.Configure&&game.DraftLoadout.TotalRules==1);
        ClickConfig("RuleParameter1");yield return Frames(2);
        ClickConfig("ActionCategory3");yield return Frames(2);ClickConfig("Option","Short Burst");yield return Frames(2);
        Check("V2 UI Ammo default is visible and remains draft-local",V2UIPending().AmmoUse==AmmoUseOption.AllowEmpty
            &&ConfigButtonNamed("RuleParameter0","Allow Empty").GetComponent<DuelButtonOutline>().Persistent
            &&ConfigButtonNamed("RuleParameter1","Keep Two Rounds")!=null&&game.DraftLoadout.TotalRules==1);
        ClickConfig("RuleParameter1");yield return Frames(2);ClickConfig("UseAction");yield return Frames(2);
        Check("V2 UI Ammo selection commits complete AND rule",game.DraftLoadout.TotalRules==2
            &&game.DraftLoadout.OrderedRules()[1].AdditionalGroundState==GroundStateOption.Airborne
            &&game.DraftLoadout.OrderedRules()[1].AmmoUse==AmmoUseOption.KeepTwoRounds);

        ClickConfig("AddRule");yield return Frames(2);yield return SelectEditorCondition(52);
        Check("V2 UI Enemy Distance does not default a branch",V2UIPending().Distance==EnemyDistanceOption.Unspecified&&game.DraftLoadout.TotalRules==2);
        ClickConfig("RuleParameter1");yield return Frames(2);ClickConfig("AdditionalCondition");yield return Frames(2);
        yield return SelectEditorCondition(53);
        Check("V2 UI Incoming Attack does not default a branch",V2UIPending().AdditionalIncomingAttack==IncomingAttackOption.Unspecified);
        ClickConfig("RuleParameter1");yield return Frames(2);yield return V2UIAction("D04");
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(25);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(51,0);
        ClickConfig("ActionCategory0");yield return Frames(2);ClickConfig("Option","Move Left / Right");yield return Frames(2);
        Check("V2 UI fixed movement direction requires explicit choice",V2UIPending().MoveDirection==MoveDirectionOption.Unspecified
            &&game.DraftLoadout.TotalRules==3&&!ConfigButtonNamed("StartFight").interactable);
        ClickConfig("RuleParameter0");yield return Frames(2);
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(11);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(51,0);yield return V2UIAction("J03");
        Check("V2 UI five rules finish with complete params and independent operations",game.DraftLoadout.TotalRules==5
            &&ConditionalCatalog.IsValid(game.DraftLoadout)&&ConfigButtonNamed("StartFight").interactable&&!ConfigButtonNamed("AddRule").interactable);
        var copied=game.DraftLoadout.Copy();
        Check("V2 UI configured fields survive deep Copy",copied.OrderedRules().Select(V2UIFields).SequenceEqual(V2UIRules()));

        string[] original=V2UIRules();
        ClickConfig("Rule1Action");yield return Frames(2);ClickConfig("RuleParameter0");yield return Frames(2);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(51,0);
        ClickConfig("SelectorBack");yield return Frames(2);yield return V2UISelect(52,2);
        Check("V2 UI changes all condition/ammo fields only in local draft",V2UIPending().Condition==52&&V2UIPending().Distance==EnemyDistanceOption.Far
            &&V2UIPending().AdditionalGroundState==GroundStateOption.Grounded&&V2UIPending().AmmoUse==AmmoUseOption.AllowEmpty
            &&V2UIRules().SequenceEqual(original));
        ClickConfig("CancelRuleEdit");yield return Frames(2);
        Check("V2 UI Cancel restores every field and Start availability",V2UIRules().SequenceEqual(original)&&ConfigButtonNamed("StartFight").interactable);
        ClickConfig("Rule1AdditionalCondition");yield return Frames(2);ClickConfig("RemoveAdditionalCondition");yield return Frames(2);
        Check("V2 UI removing AND remains a draft edit",V2UIPending().AdditionalCondition==0&&V2UIRules().SequenceEqual(original));
        heldKeys=new[]{Key.Escape};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Frames(3);
        Check("V2 UI Escape restores removed AND and its parameter",V2UIPending()==null&&V2UIRules().SequenceEqual(original));
        ClickConfig("Rule1AdditionalCondition");yield return Frames(2);ClickConfig("RemoveAdditionalCondition");yield return Frames(2);
        ClickConfig("UseAction");yield return Frames(2);
        Check("V2 UI AND can actually be removed without changing the action",game.DraftLoadout.OrderedRules()[1].AdditionalCondition==0
            &&game.DraftLoadout.OrderedRules()[1].Action=="F02"&&game.DraftLoadout.OrderedRules()[1].AmmoUse==AmmoUseOption.KeepTwoRounds);
        ClickConfig("Rule1Action");yield return Frames(2);ClickConfig("AdditionalCondition");yield return Frames(2);
        yield return V2UISelect(51,1);ClickConfig("UseAction");yield return Frames(2);
        Check("V2 UI AND can be restored by direct action edit",V2UIRules().SequenceEqual(original));

        ClickConfig("Rule4Action");yield return Frames(2);yield return V2UIAction("F01",1);
        Check("V2 UI cross-operation replacement at five preserves conditions",game.DraftLoadout.TotalRules==5
            &&game.DraftLoadout.Operations[1].Rules.Count==0&&game.DraftLoadout.Operations[3].Rules.Count==2
            &&game.DraftLoadout.OrderedRules()[4].Condition==11&&game.DraftLoadout.OrderedRules()[4].AdditionalGroundState==GroundStateOption.Grounded);
        string moving=V2UIFields(game.DraftLoadout.OrderedRules()[4]);
        for(int row=4;row>1;row--){ClickConfig("MoveRuleUp"+row);yield return Frames(2);}
        Check("V2 UI reorder preserves complete fields and same-operation priority",V2UIFields(game.DraftLoadout.OrderedRules()[1])==moving
            &&game.DraftLoadout.Operations[3].Rules.Select(r=>r.Action).SequenceEqual(new[]{"F01","F02"})
            &&game.DraftLoadout.OrderedRules().Select(r=>r.DisplayOrder).SequenceEqual(Enumerable.Range(0,5)));
        for(int row=1;row<4;row++){ClickConfig("MoveRuleDown"+row);yield return Frames(2);}
        ClickConfig("Rule4Action");yield return Frames(2);yield return V2UIAction("J03");
        Check("V2 UI return across operation keeps original primary and AND",game.DraftLoadout.TotalRules==5&&game.DraftLoadout.Operations[1].Rules.Count==1
            &&game.DraftLoadout.OrderedRules()[4].Condition==11&&game.DraftLoadout.OrderedRules()[4].AdditionalGroundState==GroundStateOption.Grounded);

        foreach(int width in new[]{1280,1920})
        {
            SetAlphaGameSize(width,width*9/16);yield return Frames(7);
            V2UIFiveLayout(width,"equipped");yield return V2UICapture("equipped-"+width);
            var scroll=SelectorScroll("EquippedRules");scroll.verticalNormalizedPosition=0;yield return Frames(2);Canvas.ForceUpdateCanvases();
            var last=(RectTransform)ConfigButtonNamed("Rule4Action").transform;
            Check($"V2 UI {width}: final complete row reachable by local scrolling",scroll.viewport.rect.Contains(scroll.viewport.InverseTransformPoint(last.position)));
            yield return V2UICapture("equipped-last-"+width);
            ClickConfig("Rule1AdditionalCondition");yield return Frames(3);V2UIFiveLayout(width,"AND editor");
            Check($"V2 UI {width}: local fixed options fit and show actual choice",ConfigButtonNamed("RuleParameter1","Airborne").GetComponent<DuelButtonOutline>().Persistent
                &&!ConfigButtonNamed("StartFight").interactable);
            FocusOption("ConditionCategory1");ClickConfig("ConditionCategory1");yield return Frames(3);
            Check($"V2 UI {width}: keyboard focus and equipped scroll survive rebuild",UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject!=null
                &&UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.name=="ConditionCategory1"
                &&SelectorScroll("EquippedRules").verticalNormalizedPosition<.01f);
            yield return V2UICapture("and-"+width);ClickConfig("CancelRuleEdit");yield return Frames(2);
            ClickConfig("Rule1Action");yield return Frames(3);V2UIFiveLayout(width,"Ammo editor");
            Check($"V2 UI {width}: Ammo visible, editable, and not implicitly submitted",ConfigButtonNamed("RuleParameter1","Keep Two Rounds").GetComponent<DuelButtonOutline>().Persistent
                &&ConfigButtonNamed("UseAction").interactable&&!ConfigButtonNamed("StartFight").interactable);
            yield return V2UICapture("ammo-"+width);ClickConfig("CancelRuleEdit");yield return Frames(2);
            var beforeFight=V2UIRules();ClickConfig("StartFight");yield return Frames(3);
            Check($"V2 UI {width}: real Start button begins stationary preparation",game.Phase==DuelPhase.Prepare&&game.Rules!=null);
            yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();yield return Frames(3);
            bool fullText=true;var ui=Object.FindAnyObjectByType<DuelUI>();
            for(int op=0;op<5;op++)for(int row=0;row<game.Rules.RuleCount((ControlOperation)op);row++)
            {
                var rule=game.Rules.RuleAt((ControlOperation)op,row);
                var root=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleFeedback_"+op+"_"+row);
                var texts=root.GetComponentsInChildren<TMPro.TMP_Text>();
                string expected=(row+1)+"  "+ConditionalCatalog.ConditionLabel(rule)+(rule.AdditionalCondition==0?"":"\nAND "+ConditionalCatalog.ConditionLabel(rule,true));
                fullText&=texts.First(t=>t.name=="ConditionText").text==expected&&texts.First(t=>t.name=="ActionText").text==ConditionalCatalog.RuleActionLabel(rule);
            }
            TextFits($"V2 HUD {width}");Check($"V2 HUD {width}: all five actual rules fit in nonoverlapping rows",fullText&&HudRowsFit()
                &&ui.GetComponentsInChildren<RectTransform>().Count(r=>r.name.StartsWith("RuleFeedback_"))==5);
            var camera=Camera.main;Vector3 min=camera.WorldToViewportPoint(game.ArenaBounds.min),max=camera.WorldToViewportPoint(game.ArenaBounds.max);
            Check($"V2 HUD {width}: complete arena remains inside battle viewport",min.x>=0&&min.y>=0&&max.x<=1&&max.y<=1,$"min={min} max={max}");
            MouseAt(game.Bosses[0].Position,false);yield return Until(()=>Feedback(2).Executing,.4f);
            var aimRow=ui.GetComponentsInChildren<RectTransform>().First(r=>r.name=="RuleFeedback_2_0");
            var aimAction=aimRow.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name=="ActionText");
            Check($"V2 HUD {width}: real Fine-Tune ownership drives action color",Feedback(2).Visual.Matched&&Feedback(2).Executing
                &&aimAction.color==NeuralPalette.Operation(ControlOperation.Aim));
            yield return V2UICapture("combat-"+width);
            game.Player.TakeDamage(99999,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1.2f);game.Reconfigure();yield return Frames(3);
            Check($"V2 UI {width}: defeat/reconfigure retains all fields and ordering",game.Phase==DuelPhase.Configure&&V2UIRules().SequenceEqual(beforeFight));
        }
        game.NewGame();yield return Frames(3);
        Check("V2 UI New Game returns to a valid empty manual loadout",game.DraftLoadout.TotalRules==0&&ConfigButtonNamed("StartFight").interactable);
        game.MainMenu();yield return Frames(3);
    }
    // Append inside the existing BossEncounterSmoke partial class; invoke once with
    // yield return ConditionalV2Followup(). Uses existing helpers and real executors.
    static string V2FollowupData(ConditionalLoadout loadout)
        =>string.Join("\n",loadout.OrderedRules().Select(rule=>JsonUtility.ToJson(rule)));
    static string V2FollowupRuntimeData(ConditionalController controller)
        =>string.Join("\n",Enumerable.Range(0,5)
            .SelectMany(op=>Enumerable.Range(0,controller.RuleCount((ControlOperation)op))
                .Select(row=>controller.RuleAt((ControlOperation)op,row)))
            .OrderBy(rule=>rule.DisplayOrder).Select(rule=>JsonUtility.ToJson(rule)));
    static bool V2FollowupEmptyRuntime(ConditionalController controller)
        =>controller!=null&&controller.Starts==0&&controller.AutomaticShots==0
            &&controller.AcceptedJumps==0&&controller.AcceptedDodges==0
            &&Private<IList>(controller,"events").Count==0;
    static IEnumerator ConditionalV2Followup()
    {
        Record("V2 short follow-up: three controlled fixtures, normal input/results and lifecycle; no balance/outcome proof.");

        // One fixture covers first-executable fallback and independent consumption of one real event.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,V2Ground("D01")); // Global Grounded supplies no threat source: not executable.
            V2Rule(d,V2Ground("D04"));
            V2Rule(d,new ConditionalRule{Condition=57,AdditionalCondition=17,Action="M10"});
            V2Rule(d,new ConditionalRule{Condition=57,AdditionalCondition=17,Action="Q01"});
        });
        MouseAt(game.Rifle.AimOrigin+Vector2.right*10,false);
        yield return Until(()=>game.Rules.AcceptedDodges==1,.4f);
        var accepted=Private<float[,]>(game.Rules,"acceptedAt");
        Check("V2 follow-up first executable: source-less high Dodge yields to lower Aim-point Dodge",
            game.Rules.AcceptedDodges==1&&float.IsNegativeInfinity(accepted[4,0])&&!float.IsNegativeInfinity(accepted[4,1]));
        yield return Until(()=>!game.Dodge.IsDodging&&game.Player.GetComponent<PlayerMovement>().IsGrounded,1.3f);
        int beforeStarts=game.Rules.Starts;float jumpAt=float.NegativeInfinity;int jumps=0;
        Action started=()=>{jumpAt=Time.time;jumps++;};
        var move=game.Player.GetComponent<PlayerMovement>();move.JumpStarted+=started;
        var reader=game.Player.GetComponent<PlayerInputReader>();
        MouseAt(game.Bosses[0].Position,true);heldKeys=new[]{Key.Space};
        yield return Frames(2);heldKeys=Array.Empty<Key>();
        yield return Until(()=>Feedback(0).Executing&&Feedback(2).Executing,.12f);
        Check("V2 follow-up one manual Jump event is independently accepted by Move and Aim",
            jumps==1&&game.Rules.Starts==beforeStarts+2&&Feedback(0).Executing&&Feedback(2).Executing
            &&reader.ConditionalFrame.MovementOwned&&reader.ConditionalFrame.AimOwned&&game.Rules.AcceptedJumps==0,
            $"jumps={jumps} starts={game.Rules.Starts-beforeStarts} elapsed={Time.time-jumpAt:F3}");
        int consumedStarts=game.Rules.Starts;
        mouseHeld=false;yield return Frames(2);
        Check("V2 follow-up AND false releases both continuous responses",
            !reader.ConditionalFrame.MovementOwned&&!reader.ConditionalFrame.AimOwned);
        mouseHeld=true;yield return Frames(2);
        bool insideWindow=Time.time-jumpAt<=ConditionalDefaults.EventWindow;
        Check("V2 follow-up consumed event cannot revive when AND returns inside original window",
            insideWindow&&game.Rules.Starts==consumedStarts&&!reader.ConditionalFrame.MovementOwned&&!reader.ConditionalFrame.AimOwned,
            $"elapsed={Time.time-jumpAt:F3}/{ConditionalDefaults.EventWindow:F2} starts={game.Rules.Starts-consumedStarts}");
        mouseHeld=false;move.JumpStarted-=started;

        // Full selected fields survive both ordinary retry and next-wave transitions.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=57,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Airborne,
                Action="F02",AmmoUse=AmmoUseOption.KeepTwoRounds});
            V2Rule(d,new ConditionalRule{Condition=52,Distance=EnemyDistanceOption.Far,AdditionalCondition=53,
                AdditionalIncomingAttack=IncomingAttackOption.Any,Action="M11",MoveDirection=MoveDirectionOption.Left});
            V2Rule(d,new ConditionalRule{Condition=6,AdditionalCondition=51,AdditionalGroundState=GroundStateOption.Grounded,Action="Q02"});
            V2Rule(d,new ConditionalRule{Condition=11,AdditionalCondition=52,AdditionalDistance=EnemyDistanceOption.Mid,Action="J03"});
            V2Rule(d,new ConditionalRule{Condition=53,IncomingAttack=IncomingAttackOption.Low,AdditionalCondition=52,
                AdditionalDistance=EnemyDistanceOption.Close,Action="D04"});
        });
        string expected=V2FollowupData(game.DraftLoadout);
        MouseAt(game.Rifle.AimOrigin+Vector2.up*12,false);
        heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();
        yield return Until(()=>game.Rules.AutomaticShots==3,1.1f);
        var old=game.Rules;
        Check("V2 follow-up lifecycle starts with a consumed real finite response",old.AutomaticShots==3);
        game.Player.TakeDamage(99999,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1.2f);
        game.Reconfigure();yield return Frames(2);
        Check("V2 follow-up defeat retry keeps complete fields and presentation order",
            game.Phase==DuelPhase.Configure&&V2FollowupData(game.DraftLoadout)==expected&&Private<bool>(old,"disposed"));
        game.StartFight();StopPolicies();yield return Frames(2);
        var retry=game.Rules;
        Check("V2 follow-up retry constructs a fresh empty runtime from the same full snapshot",
            game.Phase==DuelPhase.Prepare&&!ReferenceEquals(old,retry)&&V2FollowupEmptyRuntime(retry)
            &&V2FollowupRuntimeData(retry)==expected);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();yield return Seconds(.12f);
        Check("V2 follow-up retry does not replay old events on entering combat",V2FollowupEmptyRuntime(retry));
        MouseAt(game.Rifle.AimOrigin+Vector2.up*12,false);
        heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();
        yield return Until(()=>retry.AutomaticShots==3,1.1f);
        Check("V2 follow-up fresh retry consumes a new real Jump event once",retry.AutomaticShots==3&&old.AutomaticShots==3);
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(99999,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Configure&&game.Wave==2,1.8f);
        Check("V2 follow-up next wave preserves complete rule data and disposes old runtime",
            game.Phase==DuelPhase.Configure&&game.Wave==2&&V2FollowupData(game.DraftLoadout)==expected&&Private<bool>(retry,"disposed"));
        game.StartFight();StopPolicies();yield return Frames(2);
        var next=game.Rules;
        Check("V2 follow-up next wave starts with fresh occurrences and no old ownership",
            game.Phase==DuelPhase.Prepare&&!ReferenceEquals(retry,next)&&V2FollowupEmptyRuntime(next)
            &&V2FollowupRuntimeData(next)==expected);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();yield return Seconds(.12f);
        Check("V2 follow-up next wave does not replay the previous wave's results",V2FollowupEmptyRuntime(next));

        yield return ConditionalV2TargetedAnd();
    }
    static IEnumerator ConditionalV2TargetedAnd()
    {
        // Set positions and the intended mouse sample during Prepare, before any rules can execute.
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;game.ConfigurePlaytestWave(3);
        V2Rule(game.DraftLoadout,new ConditionalRule{Condition=52,Distance=EnemyDistanceOption.Close,AdditionalCondition=18,Action="F01"});
        game.StartFight();StopPolicies();
        var gunner=game.Bosses.First(actor=>actor.ActorId==1);var sword=game.Bosses.First(actor=>actor.ActorId==2);
        V2PreparePlace(game.Player.gameObject,gunner.Navigation,0);V2PreparePlace(gunner.gameObject,gunner.Navigation,10);V2PreparePlace(sword.gameObject,sword.Navigation,-4);
        MouseAt(gunner.Position,false);yield return Frames(3);
        int prepareShots=game.Rifle.ShotsFired;
        Check("V2 follow-up W3 fixture input is settled before combat",game.Phase==DuelPhase.Prepare&&prepareShots==0);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();yield return Seconds(.3f);
        int separateShots=game.Rifle.ShotsFired;
        V2AndProbe("separate",gunner,sword);
        Check("V2 follow-up W3 different bosses cannot combine AND facts",separateShots==0,
            $"prepare={prepareShots} separate={separateShots}");
        MouseAt(sword.Position,false);yield return Seconds(.3f);
        int sameShots=game.Rifle.ShotsFired;
        V2AndProbe("same",gunner,sword);
        Check("V2 follow-up W3 one visible boss supplies both AND facts",sameShots==1,
            $"separate={separateShots} same={sameShots}");
        var controller=game.Rules;checkingPause=true;game.SetPause(true);float pausedAt=Time.time;
        yield return RealSeconds(.18f);
        Check("V2 follow-up W3 pause holds the same consumed snapshot",Time.time==pausedAt&&game.Rules==controller&&game.Rifle.ShotsFired==sameShots);
        game.SetPause(false);checkingPause=false;yield return Seconds(.12f);
        Check("V2 follow-up W3 resume preserves the consumed shot count",game.Rifle.ShotsFired==sameShots,
            $"before={sameShots} after={game.Rifle.ShotsFired}");
        game.MainMenu();yield return Frames(2);
    }
    static void V2AndProbe(string phase,BossAgent gunner,BossAgent sword)
    {
        var reader=game.Player.GetComponent<PlayerInputReader>();
        Record($"V2 AND {phase}: player={game.Player.transform.position} bounds={game.Player.GetComponent<Collider2D>().bounds} gunner={gunner.Body.bounds} sword={sword.Body.bounds} aim={reader.RawInput.Aim} cursor={reader.RawInput.Cursor} origin={game.Rifle.AimOrigin} visible={string.Join(",",Private<bool[]>(game.Rules,"visible"))} close={string.Join(",",Private<bool[]>(game.Rules,"close"))} near={string.Join(",",Private<bool[]>(game.Rules,"cursorNear"))} matched={Feedback(3).Visual.Matched} ammo={game.Rifle.AmmoInMagazine}");
    }
}
#endif
