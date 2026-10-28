#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Fire Actions")]
    public static void RunFireActions() => Start("fire-actions", false);

    static void CheckFireCopy(string context)
    {
        var text=CardsUI().GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.gameObject.activeInHierarchy).ToArray();
        Check(context+": no old Fire names or ammo controls",text.All(t=>!t.text.Contains("Empty Magazine")&&!t.text.Contains("Magazine Emptied")
            &&!t.text.Contains("Ammo Use")&&!t.text.Contains("Allow Empty")&&!t.text.Contains("Keep Two Rounds")));
        Check(context+": new action and event labels rendered",text.Any(t=>t.text.Contains("Short Burst"))&&text.Any(t=>t.text.Contains("Burst Finished")));
    }

    static IEnumerator FireActions()
    {
        Record("Parameterless Revolver Fire actions: old JSON, actual UI callbacks and paid-shot executors. Controlled fixtures; no balance claim.");
        foreach(string action in new[]{"F01","F02","F03"})
        {
            foreach(int oldValue in new[]{0,1,99})
            {
                var old=JsonUtility.FromJson<ConditionalRule>("{\"Condition\":17,\"AdditionalCondition\":51,\"AdditionalGroundState\":1,\"Action\":\""+action+"\",\"AmmoUse\":"+oldValue+",\"DisplayOrder\":2}");
                var copy=JsonUtility.FromJson<ConditionalRule>(JsonUtility.ToJson(old.Copy()));
                Check(action+" legacy ammo "+oldValue+" stays valid and normalizes without losing rule data",ConditionalCatalog.IsAllowed(3,old)
                    &&ConditionalCatalog.IsAllowed(3,copy)&&(int)copy.AmmoUse==0&&copy.Condition==17&&copy.AdditionalCondition==51
                    &&copy.AdditionalGroundState==GroundStateOption.Grounded&&copy.Action==action&&copy.DisplayOrder==2);
            }
            Check(action+" has no parameter or label suffix",ConditionalCatalog.ActionOptions(action).Length==0
                &&ConditionalCatalog.ActionOptionLabel(action)==""&&ConditionalCatalog.ActionOption(new ConditionalRule{Action=action})==-1
                &&ConditionalCatalog.RuleActionLabel(new ConditionalRule{Action=action,AmmoUse=AmmoUseOption.KeepTwoRounds})==ConditionalCatalog.Response(action).Label);
        }
        var oldEvent=JsonUtility.FromJson<ConditionalRule>("{\"Condition\":60,\"Action\":\"J01\"}");
        Check("A60 preserves event identity and validity",ConditionalCatalog.IsAllowed(1,oldEvent)&&ConditionalCatalog.Condition(60).Event
            &&ConditionalCatalog.ConditionLabel(oldEvent)=="Burst Finished"&&ConditionalCatalog.Response("F02").Label=="Short Burst");

        game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(8);
        foreach(string action in new[]{"F01","F03","F02"})
        {
            int before=game.DraftLoadout.TotalRules;
            yield return AddEditorRule(17,action);
            Check(action+" commits immediately without ammo confirmation",game.DraftLoadout.TotalRules==before+1&&V2UIPending()==null
                &&CardsPreview("PreviewAction")==ConditionalCatalog.Response(action).Label);
        }
        yield return AddEditorRule(60,"J01");
        Check("Event preview uses Burst Finished",CardsPreview("PreviewCondition")=="Burst Finished");
        CheckFireCopy("Rule Setup");CardsLayout(false,4,"Fire setup 1280");yield return CardsCapture("fire-actions-setup-1280");
        string expected=string.Join("|",V2UIRules());
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();yield return Frames(6);
        CheckFireCopy("Combat HUD");CardsLayout(true,4,"Fire HUD 1280");yield return CardsCapture("fire-actions-hud-1280");
        Check("Fight copy retains stable Fire IDs",game.Rules.RuleAt(ControlOperation.Fire,2).Action=="F02"
            &&game.Rules.RuleAt(ControlOperation.Jump,0).Condition==60);
        game.Player.TakeDamage(game.Player.MaxHealth*2,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1);
        game.Reconfigure();yield return Frames(3);
        Check("Retry preserves complete Fire rules",string.Join("|",V2UIRules())==expected&&ConfigButtonNamed("StartFight").interactable);

        yield return AlphaFresh(1,d=>{V2Rule(d,new ConditionalRule{Condition=17,Action="F01",AmmoUse=AmmoUseOption.KeepTwoRounds});V2Rule(d,V2Ground("F06"));});
        RuleCardsAmmo(2);MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);yield return Seconds(.7f);
        Check("Fire 1 Shot spends one round below former reserve and consumes state once",game.Rifle.ShotsFired==1&&game.Rules.AutomaticShots==1&&game.Rifle.AmmoInMagazine==1);
        mouseHeld=false;yield return Frames(4);mouseHeld=true;yield return Until(()=>game.Rifle.IsReloading,.5f);mouseHeld=false;
        Check("Rearmed Fire 1 Shot spends final round and reloads",game.Rifle.ShotsFired==2&&game.Rifle.IsReloading);

        yield return AlphaFresh(1,d=>{V2Rule(d,new ConditionalRule{Condition=17,Action="F03",AmmoUse=AmmoUseOption.KeepTwoRounds});V2Rule(d,V2Ground("F06"));});
        RuleCardsAmmo(3);MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);
        using(var probe=new RuleCardsBurstProbe(game.Rules,game.Rifle))
        {
            yield return Until(()=>game.Rifle.ShotsFired>=1,.4f);mouseHeld=false;yield return Seconds(.45f);
            Check("Keep Firing stops when condition ends",game.Rifle.ShotsFired==1&&game.Rifle.AmmoInMagazine==2&&game.Rules.Ownership.FireOwned&&!game.Rules.Ownership.Fire);
            mouseHeld=true;yield return Until(()=>game.Rifle.IsReloading,1);mouseHeld=false;probe.Sample();
            Check("Keep Firing uses final two rounds with normal cadence and no completion event",game.Rifle.ShotsFired==3&&game.Rules.AutomaticShots==3
                &&probe.ShotTimes.Zip(probe.ShotTimes.Skip(1),(a,b)=>b-a).All(t=>t>=game.Rifle.FireInterval-.01f)&&probe.Bursts.Count==0);
        }

        foreach(int rounds in new[]{0,1,2})
        {
            yield return AlphaFresh(1,d=>{V2Rule(d,new ConditionalRule{Condition=17,Action="F02",AmmoUse=AmmoUseOption.KeepTwoRounds});V2Rule(d,V2Ground("F06"));});
            RuleCardsAmmo(rounds);
            using(var probe=new RuleCardsBurstProbe(game.Rules,game.Rifle))
            {
                MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);
                yield return Until(()=>game.Rifle.ShotsFired>0||game.Rifle.IsReloading,.4f);mouseHeld=false;
                yield return RuleCardsSample(probe,1,()=>game.Rifle.IsReloading);
                Check("Short Burst with "+rounds+" rounds: paid budget and exactly one completion before reload",game.Rifle.ShotsFired==rounds
                    &&game.Rules.AutomaticShots==rounds&&probe.Bursts.Count==(rounds>0?1:0)&&probe.Reloads==1
                    &&(rounds==0||probe.AcceptedBudget==rounds&&probe.BurstPresentAtReload));
                yield return Seconds(1.95f);
                Check("Short Burst with "+rounds+" rounds never bridges reload",game.Rifle.ShotsFired==rounds&&game.Rifle.AmmoInMagazine==6);
            }
        }

        yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="F02"}));
        using(var probe=new RuleCardsBurstProbe(game.Rules,game.Rifle))
        {
            MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);yield return Until(()=>game.Rifle.ShotsFired==1,.4f);mouseHeld=false;
            game.Rifle.enabled=false;yield return RuleCardsSample(probe,.35f);game.Rifle.enabled=true;yield return RuleCardsSample(probe,.4f);
            Check("Interrupted Short Burst emits no completion and does not resume",game.Rifle.ShotsFired==1&&probe.Bursts.Count==0);
        }
        game.MainMenu();yield return Frames(3);
        Check("Fire cleanup clears actors and controller",game.Player==null&&game.Rules==null);
    }
}
#endif
