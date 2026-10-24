#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Unlimited Conditions")]
    public static void RunUnlimitedConditions()=>Start("unlimited-conditions",false);
    [MenuItem("GO GHOST/Playtest/Condition Event Lifecycle")]
    public static void RunConditionEventLifecycle()=>Start("condition-event-lifecycle",false);

    static ConditionalRule ManyRule(string action,params RuleCondition[] conditions)
    {
        var rule=new ConditionalRule{Action=action};
        for(int i=0;i<conditions.Length;i++)rule.SetCondition(i,conditions[i]);
        return rule;
    }
    static RuleCondition GroundedCondition()=>new RuleCondition(51){GroundState=GroundStateOption.Grounded};
    static RuleCondition CloseCondition()=>new RuleCondition(52){Distance=EnemyDistanceOption.Close};
    static IEnumerator UnlimitedConditions()
    {
        Record("Unlimited AND conditions: real UI callbacks/virtual input, controlled targets/events; no physical-input or balance claim.");
        var old=JsonUtility.FromJson<ConditionalRule>("{\"Condition\":17,\"AdditionalCondition\":51,\"AdditionalGroundState\":1,\"Action\":\"F02\"}");
        Check("Old two-condition JSON and action remain valid",old.ConditionCount==2&&ConditionalCatalog.IsAllowed(3,old));
        var many=old.Copy();many.AddCondition(CloseCondition());many.AddCondition(new RuleCondition(53){IncomingAttack=IncomingAttackOption.Ranged});
        many.AddCondition(new RuleCondition(25));
        var json=JsonUtility.ToJson(many);var copy=JsonUtility.FromJson<ConditionalRule>(json);
        Check("Five conditions and every parameter survive Copy and JSON",copy.ConditionCount==5&&JsonUtility.ToJson(many.Copy())==json&&JsonUtility.ToJson(copy)==json);
        copy.SetCondition(2,new RuleCondition(4));copy.RemoveCondition(0);
        Check("Copies are independent; removing primary promotes full parameters",many.GetCondition(2).Distance==EnemyDistanceOption.Close
            &&copy.ConditionCount==4&&copy.Condition==51&&copy.GroundState==GroundStateOption.Grounded);
        var large=ManyRule("M10",new RuleCondition(17));
        for(int i=1;i<128;i++)large.AddCondition(new RuleCondition(17));
        Check("128 conditions have no model/copy/serialization cap",ConditionalCatalog.IsAllowed(0,large)&&large.Copy().ConditionCount==128
            &&JsonUtility.FromJson<ConditionalRule>(JsonUtility.ToJson(large)).ConditionCount==128);
        var bad=many.Copy();bad.SetCondition(4,new RuleCondition(51){GroundState=GroundStateOption.Airborne});
        Check("Contradictory ground options anywhere are rejected",!ConditionalCatalog.ConditionsAreComplete(bad));
        bad=many.Copy();bad.SetCondition(4,new RuleCondition(52){Distance=EnemyDistanceOption.Far});
        Check("Contradictory enemy distances anywhere are rejected",!ConditionalCatalog.ConditionsAreComplete(bad));
        bad=many.Copy();bad.SetCondition(4,new RuleCondition(9));
        Check("Reloading in any slot still blocks shooting",!ConditionalCatalog.IsAllowed(3,bad));
        bad=many.Copy();bad.SetCondition(4,new RuleCondition(53){IncomingAttack=IncomingAttackOption.Low});
        Check("Removed Low in a later slot stays invalid",!ConditionalCatalog.IsAllowed(3,bad));
        var empty=ManyRule("M10",new RuleCondition(17));empty.RemoveCondition(0);
        Check("Removing the final draft condition cannot commit an empty rule",!ConditionalCatalog.IsAllowed(0,empty));
        foreach(var preset in OfficialRulePresets.All)
        {
            var original=preset.CreateLoadout();string before=JsonUtility.ToJson(new PresetRulesEnvelope{rules=original.Operations});
            var cloned=original.Copy();cloned.OrderedRules()[0].AddCondition(new RuleCondition(17));
            var roundtrip=JsonUtility.FromJson<PresetRulesEnvelope>(before);
            Check("Preset "+preset.Id+" retains valid independent legacy data",ConditionalCatalog.IsValid(original)
                &&JsonUtility.ToJson(new PresetRulesEnvelope{rules=preset.CreateLoadout().Operations})==before
                &&JsonUtility.ToJson(roundtrip)==before);
        }

        RestoreFixture();game.NewGame();SetAlphaGameSize(1280,720);yield return Frames(5);
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(17);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(51,0);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(52,0);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(53,2);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(25);
        Check("Actual picker appends five independent conditions",V2UIPending().ConditionCount==5&&V2UIPending().GetCondition(3).IncomingAttack==IncomingAttackOption.Ranged);
        var strip=CardsRect("RulePreview");var stripSize=strip.rect.size;var stripPosition=strip.position;
        var actionRect=(RectTransform)ConfigButtonNamed("PreviewAction").transform;var actionPosition=actionRect.position;var actionSize=actionRect.rect.size;
        CheckConditionPreview("1280 five-condition preview",5);
        yield return CardsCapture("unlimited-five-1280");
        ClickConfig("PreviewCondition2");yield return Frames(2);yield return V2UISelect(52,1);
        Check("Editing third condition changes only its parameter",V2UIPending().GetCondition(2).Distance==EnemyDistanceOption.Mid&&V2UIPending().GetCondition(3).IncomingAttack==IncomingAttackOption.Ranged);
        ClickConfig("RemoveCondition3");yield return Frames(2);
        Check("Removing middle condition retains later conditions",V2UIPending().ConditionCount==4&&V2UIPending().GetCondition(3).Id==25);
        ClickConfig("RemovePrimaryCondition");yield return Frames(2);
        Check("Removing primary promotes Grounded and its parameter",V2UIPending().ConditionCount==3&&V2UIPending().Condition==51&&V2UIPending().GroundState==GroundStateOption.Grounded);
        ClickConfig("PreviewAction");yield return Frames(2);yield return V2UIAction("Q01");
        Check("Normal action choice commits a complete three-condition rule",game.DraftLoadout.TotalRules==1&&game.DraftLoadout.OrderedRules()[0].ConditionCount==3&&CardsUI().ConfigurationReady);
        var saved=V2UIRules();ClickConfig("Rule0Condition");yield return Frames(2);
        ClickConfig("AdditionalCondition");yield return Frames(2);yield return V2UISelect(59);
        Check("Event conditions are selectable after any condition",V2UIPending().GetCondition(3).Id==59);
        ClickConfig("CancelRuleEdit");yield return Frames(2);
        Check("Cancel restores the full original condition sequence",saved.SequenceEqual(V2UIRules()));
        ClickConfig("Rule0Condition");yield return Frames(2);
        for(int i=3;i<12;i++){ClickConfig("AdditionalCondition");yield return Frames(1);yield return V2UISelect(17);}
        CheckConditionPreview("1280 twelve-condition preview",12);
        Check("More conditions preserve the strip and action rectangle",CardsRect("RulePreview").rect.size==stripSize&&CardsRect("RulePreview").position==stripPosition
            &&((RectTransform)ConfigButtonNamed("PreviewAction").transform).rect.size==actionSize&&ConfigButtonNamed("PreviewAction").transform.position==actionPosition);
        SetAlphaGameSize(2560,1440);yield return Frames(6);CheckConditionPreview("2560 twelve-condition preview",12);
        yield return CardsCapture("unlimited-twelve-2560");
        ClickConfig("CancelRuleEdit");yield return Frames(2);
        game.StartConfiguredEncounter();yield return Until(()=>game.Phase==DuelPhase.Training,2);
        Check("Training uses full copied rule and complete HUD text",game.Phase==DuelPhase.Training&&game.Rules.RuleAt(ControlOperation.Aim,0).ConditionCount==3
            &&CardsUI().GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="ConditionText"&&t.text==ConditionalCatalog.RuleConditionLabel(game.DraftLoadout.OrderedRules()[0])));

        var stateRule=ManyRule("M11",GroundedCondition(),new RuleCondition(17),CloseCondition());stateRule.MoveDirection=MoveDirectionOption.Left;
        yield return AlphaFresh(1,d=>{V2Rule(d,stateRule);V2Rule(d,new ConditionalRule{Condition=17,Action="M10"});V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});});
        AlphaPlace(-7,8);yield return Seconds(.12f);mouseHeld=true;yield return Frames(5);
        Check("False third condition falls through to lower rule",game.Rules.ActionId(ControlOperation.Movement)=="M10");
        AlphaPlace(-2,1);yield return Seconds(.15f);
        Check("All three true executes the higher DisplayOrder rule",game.Rules.ActionId(ControlOperation.Movement)=="M11"&&game.Rules.Ownership.Move<0);
        mouseHeld=false;heldKeys=new[]{Key.D};yield return Seconds(.08f);
        Check("False middle condition restores manual movement",!game.Rules.Ownership.MovementOwned&&game.Player.GetComponent<Rigidbody2D>().linearVelocity.x>0);
        var snapshot=PlayMetrics.Current.rules[0].Rules[0];
        Check("Analytics JSON contains all rule conditions",snapshot.ConditionCount==3&&JsonUtility.FromJson<PlayMetrics.Attempt>(JsonUtility.ToJson(PlayMetrics.Current)).rules[0].Rules[0].ConditionCount==3);

        var eventRule=ManyRule("M10",GroundedCondition(),new RuleCondition(17),new RuleCondition(6));
        yield return AlphaFresh(1,d=>{V2Rule(d,eventRule);var fire=eventRule.Copy();fire.Action="F06";V2Rule(d,fire);});
        AlphaPlace(-6,8);yield return Seconds(.12f);game.Player.TakeDamage(1,null);yield return Seconds(.2f);mouseHeld=true;
        yield return Until(()=>game.Rules.Ownership.MovementOwned,.2f);
        Check("Event in third slot waits for all states and consumes per row",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.FireOwned);
        float deadline=BehaviorDeadline(0);checkingPause=true;game.SetPause(true);float paused=Time.time;yield return RealSeconds(.1f);
        Check("Multi-condition event response pauses with combat clock",Time.time==paused&&BehaviorDeadline(0)==deadline);game.SetPause(false);checkingPause=false;
        yield return Until(()=>Time.time>deadline+.05f,.8f);
        Check("Consumed event does not repeat while states stay true",!game.Rules.Ownership.MovementOwned);

        var conjunction=ManyRule("M10",GroundedCondition(),new RuleCondition(6),new RuleCondition(59));
        yield return AlphaFresh(1,d=>V2Rule(d,conjunction));AlphaPlace(-7,8);yield return Seconds(.12f);
        game.Player.TakeDamage(1,null);yield return Frames(3);
        Check("Two events require both live occurrences",!game.Rules.Ownership.MovementOwned);
        MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);yield return Until(()=>game.Rules.Ownership.MovementOwned,.3f);mouseHeld=false;
        Check("Overlapping real damage and shot events match AND",game.Rules.Ownership.MovementOwned);
        yield return Seconds(.6f);MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);yield return Frames(5);mouseHeld=false;
        Check("A fresh shot cannot reuse consumed/expired damage",!game.Rules.Ownership.MovementOwned);

        yield return AlphaFresh(3,d=>V2Rule(d,ManyRule("M10",GroundedCondition(),CloseCondition(),new RuleCondition(40))));
        AlphaPlace(0,-3);OnFloor(game.Bosses[1].gameObject,game.Bosses[1].Navigation,10);yield return Seconds(.2f);
        MouseAt(game.Bosses[1].Body.bounds.center,false);yield return Frames(5);
        Check("Three conditions cannot combine facts from different enemies",!game.Rules.Ownership.MovementOwned);
        MouseAt(game.Bosses[0].Body.bounds.center,false);yield return Frames(5);
        Check("Same-enemy intersection across all conditions executes",game.Rules.Ownership.MovementOwned);

        // Explicit public-event fixture checks selection when several enemies have live notices.
        yield return AlphaFresh(3,d=>V2Rule(d,ManyRule("M10",new RuleCondition(59),new RuleCondition(6),new RuleCondition(13),GroundedCondition())));
        AlphaPlace(0,-4);yield return Seconds(.12f);
        var record=typeof(ConditionalController).GetMethod("Record",PrivateInstance);
        record.Invoke(game.Rules,new object[]{59,-1,null});record.Invoke(game.Rules,new object[]{6,0,null});record.Invoke(game.Rules,new object[]{13,1,null});
        yield return Frames(3);
        Check("Multiple enemy events cannot mix their sources",!game.Rules.Ownership.MovementOwned);
        record.Invoke(game.Rules,new object[]{6,1,null});yield return Frames(3);
        Check("Event conjunction finds the common source beyond the first notice",game.Rules.Ownership.MovementOwned);

        foreach(bool threatFirst in new[]{true,false})
        {
            var incoming=new RuleCondition(53){IncomingAttack=IncomingAttackOption.Ranged};
            yield return AlphaFresh(1,d=>V2Rule(d,threatFirst?ManyRule("D01",incoming,GroundedCondition(),new RuleCondition(17))
                :ManyRule("D01",GroundedCondition(),new RuleCondition(17),incoming)));
            AlphaPlace(-4,10);yield return Seconds(.12f);mouseHeld=true;yield return Frames(3);
            var unknown=UnityEngine.Object.Instantiate(game.Bosses[0].ProjectilePrefab,(Vector2)game.Player.transform.position+Vector2.right*2,Quaternion.identity);
            unknown.transform.SetParent(game.Player.transform.parent,true);unknown.Launch(Vector2.left,18,1,null);
            yield return Until(()=>game.Rules.AcceptedDodges>0,.2f);
            Check("Anonymous incoming projectile keeps its Dodge source, threat first="+threatFirst,game.Rules.AcceptedDodges==1&&game.Dodge.CommittedHorizontalSpeed<0);
            if(unknown!=null)unknown.Retire();
        }

        yield return AlphaFresh(1,d=>{V2Rule(d,ManyRule("F02",new RuleCondition(17),GroundedCondition(),CloseCondition()));V2Rule(d,V2Ground("F06"));});
        AlphaPlace(-2,1);yield return Seconds(.12f);MouseAt(game.Rifle.AimOrigin+Vector2.up*10,true);
        yield return Until(()=>game.Rifle.IsBursting,.3f);mouseHeld=false;
        yield return Until(()=>game.Rifle.IsReloading,1.2f);
        Check("Accepted multi-condition burst finishes after condition loss",game.Rules.AutomaticShots==6&&game.Rifle.IsReloading);
        game.Player.TakeDamage(game.Player.MaxHealth*2,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1);game.Reconfigure();yield return Frames(3);
        Check("Retry preserves complete multi-condition rule",game.Phase==DuelPhase.Configure&&game.DraftLoadout.Operations[3].Rules[0].ConditionCount==3);
        game.MainMenu();yield return Frames(3);Check("Lifecycle cleanup disposes the controller",game.Player==null&&game.Rules==null);
    }

    static IEnumerator ConditionEventLifecycle()
    {
        Record("Current event timing/cadence regression; real executors with controlled public events and cooldown placement.");
        // Retained two-condition event response: states may arrive late, expiry is not acceptance.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,new ConditionalRule{Condition=6,AdditionalCondition=17,Action="M10"});
            V2Rule(d,new ConditionalRule{Condition=17,Action="F06"});
        });
        AlphaPlace(-5,10);yield return Seconds(.12f);float occurred=Time.time;
        game.Player.TakeDamage(1,null);yield return Seconds(.28f);mouseHeld=true;
        yield return Until(()=>game.Rules.Ownership.MovementOwned&&float.IsFinite(BehaviorDeadline(0)),.2f);float deadline=BehaviorDeadline(0);
        Check("Existing two-condition event starts its own .5-second response",game.Rules.Ownership.MovementOwned&&Mathf.Abs(deadline-Time.time-.5f)<.06f,
            "deadline="+deadline+" now="+Time.time);
        yield return Until(()=>Time.time>occurred+.54f,.4f);
        Check("Accepted response survives original event expiry",game.Rules.Ownership.MovementOwned);
        yield return Until(()=>Time.time>deadline+.04f,.6f);
        Check("Existing response ends at acceptance deadline",!game.Rules.Ownership.MovementOwned);

        foreach(float wait in new[]{.2f,.6f})
        {
            yield return AlphaFresh(1,d=>{
                V2Rule(d,ManyRule("F01",GroundedCondition(),new RuleCondition(6),new RuleCondition(13)));
                V2Rule(d,V2Ground("F06"));
            });
            AlphaPlace(-5,10);MouseAt(game.Rifle.AimOrigin+Vector2.up*10,false);yield return Seconds(.12f);
            typeof(PlayerShooting).GetField("nextFireTime",PrivateInstance).SetValue(game.Rifle,Time.time+wait);
            var record=typeof(ConditionalController).GetMethod("Record",PrivateInstance);
            record.Invoke(game.Rules,new object[]{6,-1,null});record.Invoke(game.Rules,new object[]{13,-1,null});
            yield return Seconds(wait+.12f);
            Check(wait<.5f?"Multi-event shot waits for legal cadence within every event window":"Multi-event shot cannot wait past the earliest event expiry",
                game.Rules.AutomaticShots==(wait<.5f?1:0));
        }

        yield return AlphaFresh(2,d=>V2Rule(d,ManyRule("M04",new RuleCondition(6),new RuleCondition(17),GroundedCondition())));
        AlphaPlace(-5,10);game.Player.GetComponent<PlayerMovement>().SetRuntimeMovement(1,12);mouseHeld=true;yield return Frames(4);
        game.Player.TakeDamage(1,null);yield return Seconds(.6f);
        Check("Event short Move keeps its original commitment beyond event expiry",game.Rules.Ownership.MovementOwned);
        mouseHeld=false;yield return Seconds(.1f);
        Check("Later state loss still interrupts short Move",!game.Rules.Ownership.MovementOwned);
        var done=new System.Collections.Generic.HashSet<long>();BehaviorNotices(game.Rules,61,done);
        Check("Interrupted short Move never invents Move Finished",done.Count==0);
        game.MainMenu();yield return Frames(3);

        // One brief normal-policy W2 observation complements focused W1/W3 fixtures above.
        yield return AlphaFresh(2,d=>V2Rule(d,ManyRule("Q01",GroundedCondition(),new RuleCondition(17),new RuleCondition(17))),false);
        mouseHeld=true;float start=Time.time;yield return Seconds(1.5f);
        Check("Normal W2 policy and combat clock advance with a multi-condition rule",game.Phase==DuelPhase.Fight&&Time.time>=start+1.5f);
        game.MainMenu();yield return Frames(3);Check("Event regression cleanup clears actors and subscriptions",game.Player==null&&game.Rules==null);
    }

    static void CheckConditionPreview(string context,int count)
    {
        Canvas.ForceUpdateCanvases();var preview=CardsRect("RulePreview");
        var parts=preview.GetComponentsInChildren<RulePreviewPart>();
        bool fits=parts.Length==count+1;
        foreach(var part in parts)
        {
            var text=part.GetComponentInChildren<TMP_Text>();text.ForceMeshUpdate();
            fits&=!text.isTextOverflowing&&CardsInside(preview,(RectTransform)part.transform);
        }
        Check(context+" has individually fitted boxes and all plus joins",fits&&preview.GetComponentsInChildren<TMP_Text>().Count(t=>t.name=="PreviewConditionJoin")==count-1);
    }
}
#endif
