#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    static IEnumerator BehaviorCatalog()
    {
        Record("Behavior catalog: serialization compatibility and real Rule Setup Button callbacks. Controlled UI/defeat fixtures; no physical-input or balance claim.");
        RestoreFixture();heldKeys=Array.Empty<Key>();mouseHeld=false;
        var oldMove=JsonUtility.FromJson<ConditionalRule>("{\"Condition\":17,\"Action\":\"M04\"}");
        Check("Old M04 JSON explicitly becomes Move Back without an enemy parameter",oldMove.ShortMoveDirection==ShortMoveDirectionOption.Back
            &&ConditionalCatalog.IsAllowed(0,oldMove)&&ConditionalCatalog.RuleActionLabel(oldMove)=="Move (Back)"
            &&oldMove.Copy().ShortMoveDirection==ShortMoveDirectionOption.Back);
        var oldBullet=JsonUtility.FromJson<ConditionalRule>("{\"Condition\":53,\"IncomingAttack\":2,\"Action\":\"D01\"}");
        Check("Old Bullet numeric value maps to Ranged and correct menu index",oldBullet.IncomingAttack==IncomingAttackOption.Ranged
            &&ConditionalCatalog.ConditionOption(oldBullet)==2&&ConditionalCatalog.ConditionLabel(oldBullet)=="Incoming Attack (Ranged)"
            &&ConditionalCatalog.IsAllowed(4,oldBullet));
        var low=oldBullet.Copy();low.IncomingAttack=(IncomingAttackOption)4;
        var lowCopy=JsonUtility.FromJson<ConditionalRule>(JsonUtility.ToJson(low.Copy()));
        Check("Removed Low remains invalid and explicitly requests reselection",lowCopy.IncomingAttack==IncomingAttackOption.Low
            &&!ConditionalCatalog.IsAllowed(4,lowCopy)&&ConditionalCatalog.ConditionOption(lowCopy)==-2
            &&ConditionalCatalog.IncompatibilityReason(4,lowCopy).Contains("no longer available")
            &&ConditionalCatalog.ConditionLabel(lowCopy)=="Incoming Attack (Reselect)");
        Check("Incoming menu is exactly Any Melee Ranged with stable values",ConditionalCatalog.ConditionOptions(53).SequenceEqual(new[]{"Any","Melee","Ranged"})
            &&(int)IncomingAttackOption.Ranged==2&&(int)IncomingAttackOption.Melee==3&&(int)IncomingAttackOption.Low==4);
        var full=new ConditionalRule{Condition=53,IncomingAttack=IncomingAttackOption.Melee,AdditionalCondition=53,
            AdditionalIncomingAttack=IncomingAttackOption.Ranged,Action="M04",ShortMoveDirection=ShortMoveDirectionOption.Forward,
            MoveDirection=MoveDirectionOption.Right,PlatformDirection=PlatformDirectionOption.Lower,DisplayOrder=3};
        var clone=JsonUtility.FromJson<ConditionalRule>(JsonUtility.ToJson(full.Copy()));
        Check("Copy and JSON preserve independent short continuous and platform parameters",JsonUtility.ToJson(full)==JsonUtility.ToJson(clone)
            &&!ReferenceEquals(full,full.Copy()));
        var mixed=new ConditionalLoadout();mixed.Operations[0].Rules.Add(oldMove);mixed.Operations[4].Rules.Add(low);
        var mixedCopy=mixed.Copy();
        Check("Copy keeps invalid Low row and unrelated valid rule",mixedCopy.TotalRules==2&&!ConditionalCatalog.IsValid(mixedCopy)
            &&ConditionalCatalog.IsAllowed(0,mixedCopy.Operations[0].Rules[0])&&mixedCopy.Operations[4].Rules[0].IncomingAttack==IncomingAttackOption.Low);
        Check("Catalog IDs counts and shared timing stay explicit",ConditionalCatalog.Conditions.Length==26&&ConditionalCatalog.Responses.Length==21
            &&ConditionalCatalog.MoveFinishedCondition==61&&ConditionalCatalog.MaxRulesTotal==5
            &&ConditionalDefaults.EventWindow==.5f&&ConditionalDefaults.ResponseWindow==.5f);
        Check("Fine-Tune pair and source-dependent Dodge add no pairing ban",ConditionalCatalog.IsAllowed(2,new ConditionalRule{Condition=18,Action="Q06"})
            &&ConditionalCatalog.IsAllowed(4,new ConditionalRule{Condition=17,Action="D01"}));

        game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(8);
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(17);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(51,0);
        yield return V2UIAction("M04");
        Check("Move editor exposes separate Forward Back choices before commit",game.DraftLoadout.TotalRules==0
            &&ConfigButtonNamed("RuleParameter0","Forward")!=null&&ConfigButtonNamed("RuleParameter1","Back")!=null
            &&V2UIPending().AdditionalGroundState==GroundStateOption.Grounded);
        yield return CardsCapture("behavior-move-options-1280");
        ClickConfig("RuleParameter0");yield return Frames(3);
        Check("Actual Move Forward button commits both conditions and direction",game.DraftLoadout.Operations[0].Rules[0].ShortMoveDirection==ShortMoveDirectionOption.Forward
            &&game.DraftLoadout.Operations[0].Rules[0].AdditionalGroundState==GroundStateOption.Grounded&&CardsPreview("PreviewAction")=="Move (Forward)");
        string original=V2UIFields(game.DraftLoadout.Operations[0].Rules[0]);
        ClickConfig("Rule0Action");yield return Frames(2);yield return V2UIAction("M11");
        Check("Keep Moving has Left Right instead of Forward Back",ConfigButtonNamed("RuleParameter0","Left")!=null
            &&ConfigButtonNamed("RuleParameter1","Right")!=null&&V2UIPending().MoveDirection==MoveDirectionOption.Unspecified);
        ClickConfig("CancelRuleEdit");yield return Frames(3);
        Check("Actual Cancel restores complete short Move rule",V2UIFields(game.DraftLoadout.Operations[0].Rules[0])==original&&V2UIPending()==null);
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(17);yield return V2UIAction("M11",1);
        Check("Keep Moving Right is independent from short Move Forward",game.DraftLoadout.Operations[0].Rules[1].MoveDirection==MoveDirectionOption.Right
            &&ConditionalCatalog.RuleActionLabel(game.DraftLoadout.Operations[0].Rules[1])=="Keep Moving (Right)"
            &&game.DraftLoadout.Operations[0].Rules[0].ShortMoveDirection==ShortMoveDirectionOption.Forward);
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(6);yield return V2UIAction("J04");
        Check("Renamed platform editor keeps Higher Lower options",ConfigButtonNamed("RuleParameter0","Higher")!=null&&ConfigButtonNamed("RuleParameter1","Lower")!=null);
        ClickConfig("RuleParameter1");yield return Frames(3);
        Check("Platform summary retains explicit Lower and arrow",CardsPreview("PreviewAction")=="Move to Platform (Lower) ↓");
        ClickConfig("AddRule");yield return Frames(2);yield return SelectEditorCondition(53);
        Check("Actual Incoming editor removes Low",CardsUI().GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name.StartsWith("RuleParameter"))==3
            &&ConfigButtonNamed("RuleParameter0","Any")!=null&&ConfigButtonNamed("RuleParameter1","Melee")!=null&&ConfigButtonNamed("RuleParameter2","Ranged")!=null);
        yield return CardsCapture("behavior-incoming-options-1280");
        ClickConfig("RuleParameter2");yield return Frames(2);yield return V2UIAction("D01");
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(18);yield return V2UIAction("Q06");
        Check("Five-rule budget and legal Fine-Tune pair remain",game.DraftLoadout.TotalRules==5&&ConditionalCatalog.IsValid(game.DraftLoadout)
            &&!ConfigButtonNamed("AddRule").interactable&&ConfigButtonNamed("StartFight").interactable);
        var before=V2UIRules();ClickConfig("MoveRuleDown0");yield return Frames(3);ClickConfig("MoveRuleUp1");yield return Frames(3);
        Check("Actual reorder roundtrip retains every parameter",before.SequenceEqual(V2UIRules()));
        yield return CardsCapture("behavior-five-rules-1280");
        SetAlphaGameSize(1920,1080);yield return Frames(8);yield return CardsCapture("behavior-five-rules-1920");

        game.EditLoadout(d=>d.Operations[4].Rules[0].IncomingAttack=IncomingAttackOption.Low);yield return Frames(3);
        Check("Invalid old Low keeps other four rows and disables Start",game.DraftLoadout.TotalRules==5&&!ConfigButtonNamed("StartFight").interactable);
        ClickConfig("Rule3Condition");yield return Frames(3);
        Check("Old Low editor has no silently selected replacement",V2UIPending().IncomingAttack==IncomingAttackOption.Low
            &&ConditionalCatalog.ConditionOption(V2UIPending())==-2);
        ClickConfig("RuleParameter2");yield return Frames(2);yield return V2UIAction("D01");
        Check("Reselecting Ranged repairs only old Low",game.DraftLoadout.TotalRules==5&&ConditionalCatalog.IsValid(game.DraftLoadout)
            &&before.SequenceEqual(V2UIRules()));
        string expected=string.Join("|",V2UIRules());game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        Check("Fight snapshot preserves complete renamed configuration",game.Rules!=null
            &&game.Rules.RuleAt(ControlOperation.Movement,0).ShortMoveDirection==ShortMoveDirectionOption.Forward
            &&game.Rules.RuleAt(ControlOperation.Movement,1).MoveDirection==MoveDirectionOption.Right
            &&game.Rules.RuleAt(ControlOperation.Jump,0).PlatformDirection==PlatformDirectionOption.Lower
            &&game.Rules.RuleAt(ControlOperation.Dodge,0).IncomingAttack==IncomingAttackOption.Ranged);
        checkingPause=true;game.SetPause(true);yield return Frames(5);
        Check("Pause leaves immutable copied parameters intact",string.Join("|",V2UIRules())==expected&&game.Paused);
        game.SetPause(false);checkingPause=false;
        game.Player.TakeDamage(game.Player.MaxHealth*2,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1);
        game.Reconfigure();yield return Frames(3);
        Check("Defeat Retry restores full configuration and clears controller",game.Phase==DuelPhase.Configure&&game.Rules==null
            &&string.Join("|",V2UIRules())==expected&&ConfigButtonNamed("StartFight").interactable);
        game.MainMenu();yield return Frames(3);
        Check("Catalog test cleanup leaves no attempt",game.Rules==null&&game.Player==null);
    }
}
#endif
