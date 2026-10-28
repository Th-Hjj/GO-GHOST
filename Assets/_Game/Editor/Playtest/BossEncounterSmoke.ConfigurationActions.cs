#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Configuration Actions")]
    public static void RunConfigurationActions()=>Start("configuration-actions",false);
    [MenuItem("GO GHOST/Playtest/Configuration Action Edges")]
    public static void RunConfigurationActionEdges()=>Start("configuration-action-edges",false);

    static IEnumerator ConfigurationActionEdges()
    {
        foreach(int option in new[]{0,1,2,3})
        {
            yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("D01",option,6)));
            AlphaPlace(-5,5);MouseAt((Vector2)game.Player.transform.position+Vector2.left*8,false);
            heldKeys=new[]{Key.D};yield return Seconds(.08f);float x=game.Player.transform.position.x;
            game.Player.TakeDamage(1,game.Bosses[0].gameObject);
            yield return Until(()=>game.Dodge.IsDodging,.25f);
            float sign=option==0||option==3?-1:1;
            Check("Dodge branch direction "+option,game.Dodge.IsDodging&&game.Dodge.CommittedHorizontalSpeed*sign>0);
            heldKeys=Array.Empty<Key>();yield return Seconds(.23f);
            Check("Dodge branch completes real travel "+option,game.Rules.AcceptedDodges==1&&!game.Dodge.IsDodging
                &&(game.Player.transform.position.x-x)*sign>2.5f&&game.Dodge.CooldownRemaining>0);
        }
        foreach(bool higher in new[]{true,false})
        {
            yield return AlphaFresh(1,d=>V2Rule(d,PlatformRule(higher,6)));
            var supports=LayoutSupports();var source=supports[higher?0:3];
            PlatformPlace(source,higher?-5:source.bounds.center.x);yield return Seconds(.15f);PlatformTrigger();
            yield return Until(()=>game.Rules.PlatformTransitionsCompleted>0,3f);
            Check("Change Platform completes "+(higher?"Higher":"Lower"),game.Rules.PlatformTransitionsCompleted==1
                &&game.Player.GetComponent<PlayerMovement>().IsGrounded
                &&(game.Player.GetComponent<Collider2D>().bounds.min.y-source.bounds.max.y)*(higher?1:-1)>1);
        }
        yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("M11",4)));
        float center=game.Bosses[0].Navigation.ArenaBounds.center.x;
        AlphaPlace(center-1,10);MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,false);yield return Frames(4);
        mouseHeld=true;yield return Seconds(.3f);
        Check("Arena Center arrives and releases Move",Mathf.Abs(game.Player.transform.position.x-center)<.2f&&!game.Rules.Ownership.MovementOwned);

        yield return ConfigurationApproachEdges();

        yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("M11",1)));
        AlphaPlace(-5,-4.25f);MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,true);yield return Seconds(.3f);
        var playerShape=game.Player.GetComponent<Collider2D>();var bossShape=game.Bosses[0].GetComponent<Collider2D>();
        Check("Move stays physically blocked by Boss",playerShape.bounds.max.x<=bossShape.bounds.min.x+.02f
            &&!game.Rules.Ownership.MovementOwned&&!Physics2D.GetIgnoreCollision(playerShape,bossShape));

        // Parameterized Jump preserves its priority over the merged Dodge entry.
        yield return AlphaFresh(1,d=>{V2Rule(d,ConfigAction("D01",2));V2Rule(d,ConfigAction("J01",0));});
        AlphaPlace(-5,10);MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,false);yield return Frames(4);
        mouseHeld=true;yield return Seconds(.1f);
        Check("Merged Jump still precedes higher Dodge row",game.Rules.AcceptedJumps==1&&game.Rules.AcceptedDodges==0
            &&game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>0&&game.Dodge.CooldownRemaining==0);

        yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("F02")));
        AlphaPlace(-5,10);MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,false);yield return Frames(4);
        int shots=game.Rifle.ShotsFired,bursts=0;game.Rifle.BurstFinished+=()=>bursts++;
        mouseHeld=true;yield return Until(()=>game.Rifle.IsBursting,.25f);mouseHeld=false;
        yield return Until(()=>game.Rifle.IsReloading,1f);
        Check("Short Burst retains remaining-magazine completion",game.Rifle.ShotsFired-shots==6&&bursts==1&&game.Rifle.IsReloading
            &&game.Rifle.FireInterval==.45f&&game.Rifle.BurstInterval==.1f&&game.Rifle.BurstSpread==14);

        // Existing start/apply gates reject intact bad data, while editing can repair it.
        game.ConfigurePlaytestWave(1);yield return Frames(3);
        var invalid=ConfigAction("M11",0);invalid.AddCondition(new RuleCondition(56));
        game.DraftLoadout.Operations[0].Rules.Add(invalid);game.StartFight();yield return Frames(3);
        Check("Start Fight rejects entire invalid conjunction",game.Phase==DuelPhase.Configure&&game.Rules==null
            &&game.DraftLoadout.Operations[0].Rules[0].ConditionCount==2);
        game.DraftLoadout.Operations[0].Rules[0].SetCondition(1,new RuleCondition(12));
        Check("Explicit condition edit repairs complete row",ConditionalCatalog.IsValid(game.DraftLoadout));
        // Template definitions preserve the original tactics, with explicit new fields.
        var final=OfficialRulePresets.All[5].CreateLoadout().OrderedRules();
        Check("Final preset exact merged parameters",final[0].Action=="D01"&&final[0].DodgeDirection==DodgeDirectionOption.TowardEnemy
            &&final[1].DodgeDirection==DodgeDirectionOption.AwayFromThreat&&final[2].Jump==JumpOption.Once
            &&final[3].Action=="M11"&&final[3].MoveDirection==MoveDirectionOption.AwayFromEnemy
            &&final[4].KeepRange==KeepRangeOption.Mid&&final[5].KeepRange==KeepRangeOption.Mid
            &&final.Skip(6).Take(3).All(r=>r.Action=="Q01"&&r.AutoAim==AutoAimOption.Nearest)&&final[9].Action=="F03");
        game.MainMenu();yield return Frames(3);
    }

    public static void RunConfigurationApproachEdges()=>Start("configuration-approach-edges",false);
    static string ConfigMoveDiagnostic()=>"raw="+Private<RawPlayerInput>(game.Rules,"raw").Fire+" owned="+game.Rules.Ownership.MovementOwned+" move="+game.Rules.Ownership.Move+" player="+game.Player.transform.position+" boss="+game.Bosses[0].transform.position+" visible="+Private<int>(game.Rules,"allVisible");
    static IEnumerator ConfigurationApproachEdges()
    {
        yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("M11",2)));
        AlphaPlace(-5,10);yield return Seconds(.08f);
        var boss=game.Bosses[0];var high=LayoutSupports().Skip(1).OrderBy(c=>c.bounds.max.y).First();
        PlatformPlace(high,high.bounds.center.x);
        OnFloor(boss.gameObject,boss.Navigation,high.bounds.center.x);
        // Let interpolation and a physics step settle before reading the two body centres.
        yield return Seconds(.12f);MouseAt(game.Rifle.AimOrigin+Vector2.up*8,true);yield return Seconds(.20f);
        Check("Toward Enemy retains aligned stop across heights",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move==0,ConfigMoveDiagnostic());
        OnFloor(boss.gameObject,boss.Navigation,game.Player.transform.position.x+1);yield return Seconds(.10f);
        Check("Toward Enemy resumes after target leaves tolerance",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move>0,ConfigMoveDiagnostic());
        Position(boss.gameObject,new Vector2(100,100));yield return Frames(4);
        Check("Unavailable visible enemy releases Move",!game.Rules.Ownership.MovementOwned);

    }

    static ConditionalRule ConfigAction(string action,int option=0,int condition=17)
    {
        var rule=new ConditionalRule{Condition=condition,Action=action};
        if(ConditionalCatalog.ActionOptions(action).Length>0)ConditionalCatalog.SetActionOption(rule,option);
        return rule;
    }

    static IEnumerator ConfigurationActions()
    {
        Record("Merged actions: real catalog/UI/physics, controlled enemy positions and virtual input; no balance claim.");
        Check("21 conditions / 10 action entries",ConditionalCatalog.Conditions.Length==21&&ConditionalCatalog.Responses.Length==10);
        Check("Only approved merged entries",ConditionalCatalog.Responses.Select(r=>r.Label).SequenceEqual(new[]{
            "Move","Keep Distance","Jump","Change Platform","Auto Aim","Aim Assist","Fire 1 Shot","Short Burst","Keep Firing","Dodge"}));
        Check("Condition labels and preserved names",ConditionalCatalog.Categories[4]=="Action Events"
            &&ConditionalCatalog.Condition(52).Label=="Horizontal Enemy Distance"&&ConditionalCatalog.Condition(18).Label=="Enemy Near Your Aim"
            &&new[]{53,17,59,60,35,12,13}.Select(id=>ConditionalCatalog.Condition(id).Label).SequenceEqual(new[]{
                "Incoming Attack","Trigger Held","Shot Fired","Burst Finished","Enemy Getting Closer","Low Ammo","Hit Enemy"}));
        var migrations=new[]{("M01","M11",2),("M02","M11",3),("M09","M11",4),("Q02","Q01",1),
            ("D02","D01",1),("D03","D01",2),("D04","D01",3),("J03","J01",1)};
        foreach(var item in migrations)
        {
            var old=JsonUtility.FromJson<ConditionalRule>("{\"Condition\":17,\"Action\":\""+item.Item1+"\",\"DisplayOrder\":7,\"AdditionalCondition\":12}");
            old.AddCondition(new RuleCondition(52){Distance=EnemyDistanceOption.Mid});
            var copy=old.Copy();var json=JsonUtility.FromJson<ConditionalRule>(JsonUtility.ToJson(copy));
            Check("Equivalent migration "+item.Item1,copy.Action==item.Item2&&ConditionalCatalog.ActionOption(copy)==item.Item3
                &&ConditionalCatalog.IsAllowed((int)ConditionalCatalog.Response(copy.Action).Operation,old)
                &&copy.DisplayOrder==7&&copy.ConditionCount==3&&copy.GetCondition(2).Distance==EnemyDistanceOption.Mid
                &&JsonUtility.ToJson(copy.Copy())==JsonUtility.ToJson(json)&&old.Action==item.Item1);
        }
        foreach(var direction in new[]{ShortMoveDirectionOption.Back,ShortMoveDirectionOption.Forward})
        {
            var old=new ConditionalRule{Condition=17,Action="M04",ShortMoveDirection=direction,MoveDirection=MoveDirectionOption.Right};
            var copy=old.Copy();
            Check("Old "+direction+" needs reselection even with stale fixed direction",copy.Action=="M11"
                &&copy.MoveDirection==MoveDirectionOption.Unspecified&&!ConditionalCatalog.IsAllowed(0,old)
                &&ConditionalCatalog.RuleActionLabel(copy)=="Move (Reselect)");
            ConditionalCatalog.SetActionOption(copy,0);
            Check("Explicit reselection repairs old "+direction,ConditionalCatalog.IsAllowed(0,copy));
        }
        foreach(int removed in new[]{25,56,61})foreach(int slot in new[]{0,1,2})
        {
            var rule=ConfigAction("M11");rule.AddCondition(new RuleCondition(12));rule.AddCondition(new RuleCondition(45));
            rule.SetCondition(slot,new RuleCondition(removed));var loadout=new ConditionalLoadout();loadout.Operations[0].Rules.Add(rule);
            var copy=loadout.Copy();
            Check("Removed condition invalidates whole row: "+removed+" slot "+slot,!ConditionalCatalog.IsValid(copy)
                &&copy.TotalRules==1&&copy.Operations[0].Rules[0].ConditionCount==3&&copy.Operations[0].Rules[0].HasCondition(removed));
        }
        foreach(string removed in new[]{"M10","F06"})
        {
            var rule=new ConditionalRule{Condition=17,Action=removed};
            Check("Removed action retained and rejected: "+removed,rule.Copy().Action==removed
                &&!ConditionalCatalog.IsAllowed((int)ConditionalCatalog.Response(removed).Operation,rule));
        }
        foreach(var preset in OfficialRulePresets.All)
        {
            var rows=preset.CreateLoadout().OrderedRules();var other=preset.CreateLoadout();
            Check("Preset valid / ordered / independent: "+preset.Id,ConditionalCatalog.IsValid(other)
                &&rows.Select(r=>r.DisplayOrder).SequenceEqual(Enumerable.Range(0,rows.Count))
                &&rows.All(r=>ConditionalCatalog.IsSelectableResponse(r.Action))&&!ReferenceEquals(rows[0],other.OrderedRules()[0]));
        }
        // Actual selector buttons, full preview, JSON and independent draft copies for every branch.
        game.ConfigurePlaytestWave(1);SetAlphaGameSize(1280,720);yield return Frames(5);
        ClickConfig("AddRule");yield return Frames(2);yield return V2UISelect(17);
        foreach(string action in new[]{"M11","D01","Q01","J01","M03","J04"})
        {
            var options=ConditionalCatalog.ActionOptions(action);
            for(int i=0;i<options.Length;i++)
            {
                if(game.DraftLoadout.TotalRules>0){ClickConfig("Rule0Action");yield return Frames(2);}
                yield return V2UIAction(action);
                Check("Parameter visible: "+action+" "+options[i],ConfigButtonNamed("RuleParameter"+i,options[i])!=null);
                if(i==0&&(action=="M11"||action=="D01"))yield return CardsCapture("merged-"+action+"-options");
                ClickConfig("RuleParameter"+i);yield return Frames(3);
                var row=game.DraftLoadout.OrderedRules()[0];var json=JsonUtility.FromJson<ConditionalRule>(JsonUtility.ToJson(row));
                string label=ConditionalCatalog.RuleActionLabel(row);
                Check("Commit / preview / JSON branch: "+label,ConditionalCatalog.ActionOption(row)==i
                    &&CardsPreview("PreviewAction")==label&&ConditionalCatalog.RuleActionLabel(json)==label&&label.Contains(options[i]));
            }
        }
        var saved=JsonUtility.ToJson(game.DraftLoadout.OrderedRules()[0]);
        ClickConfig("Rule0Action");yield return Frames(2);yield return V2UIAction("M11");
        ClickConfig("CancelRuleEdit");yield return Frames(2);
        Check("Cancel restores complete parameterized rule",JsonUtility.ToJson(game.DraftLoadout.OrderedRules()[0])==saved);

        for(int option=0;option<5;option++)
        {
            int branch=option;
            yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("M11",branch)));
            AlphaPlace(-5,9);MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,false);yield return Seconds(.12f);
            float x=game.Player.transform.position.x;mouseHeld=true;yield return Seconds(.38f);
            float sign=option==0||option==3?-1:1;
            Check("Move physical branch "+option,game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move*sign>.5f
                &&(game.Player.transform.position.x-x)*sign>1.5f&&!game.Rules.Ownership.MovementKeepsFacing);
            mouseHeld=false;heldKeys=sign>0?new[]{Key.A}:new[]{Key.D};yield return Seconds(.10f);
            Check("Move condition loss restores opposing manual input "+option,!game.Rules.Ownership.MovementOwned
                &&game.Player.GetComponent<Rigidbody2D>().linearVelocity.x*sign<-.1f);
            heldKeys=Array.Empty<Key>();mouseHeld=true;yield return Seconds(.10f);
            Check("Move immediately resumes without completion lock "+option,game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move*sign>.5f);
            mouseHeld=false;yield return Frames(2);
        }
        // Priority and whole AND condition loss, including an event's actual expiry.
        yield return AlphaFresh(1,d=>{
            var first=ConfigAction("M11",0);first.AddCondition(new RuleCondition(12));V2Rule(d,first);
            V2Rule(d,ConfigAction("M11",1));
        });
        AlphaPlace(-5,10);MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,false);yield return Frames(3);
        RuleCardsAmmo(2);game.Rifle.enabled=false;mouseHeld=true;yield return Frames(4);
        Check("Higher matching Move wins",game.Rules.Ownership.Move<0);
        RuleCardsAmmo(6);yield return Frames(3);
        Check("Lost additional condition falls through to lower rule",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move>0);
        yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("M11",1,6)));
        AlphaPlace(-5,10);yield return Frames(3);game.Player.TakeDamage(1,null);yield return Seconds(.20f);
        Check("Event Move starts inside live occurrence",game.Rules.Ownership.MovementOwned);
        yield return Seconds(.36f);
        Check("Event Move ends at condition expiry",!game.Rules.Ownership.MovementOwned);

        foreach(var range in new[]{KeepRangeOption.Short,KeepRangeOption.Mid,KeepRangeOption.Long})
        {
            yield return AlphaFresh(1,d=>V2Rule(d,new ConditionalRule{Condition=17,Action="M03",KeepRange=range}));
            float near=range==KeepRangeOption.Short?2.1f:range==KeepRangeOption.Mid?5.3f:8.5f;
            float far=range==KeepRangeOption.Short?5.3f:range==KeepRangeOption.Mid?8.5f:11.7f;
            foreach(float distance in new[]{near-.5f,(near+far)/2,far+.5f})
            {
                mouseHeld=false;yield return Frames(3);AlphaPlace(-7,-7+distance);
                MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,true);yield return Frames(3);
                var frame=game.Rules.Ownership;
                Check("Keep Distance unchanged band "+range+" at "+distance,distance<near?frame.MovementOwned&&frame.Move<0:
                    distance>far?frame.MovementOwned&&frame.Move>0:!frame.MovementOwned);
            }
        }
        // Distinct target policies with two visible bodies, then unchanged Jump execution.
        foreach(int option in new[]{0,1})
        {
            yield return AlphaFresh(3,d=>V2Rule(d,ConfigAction("Q01",option)));
            AlphaPlace(-5,-1);OnFloor(game.Bosses[1].gameObject,game.Bosses[1].Navigation,8);
            game.Bosses[1].Health.TakeDamage(100,null);mouseHeld=true;yield return Frames(4);
            Check("Auto Aim target policy "+option,game.Rules.CurrentTargetId==game.Bosses[option].ActorId);
        }
        foreach(int option in new[]{0,1})
        {
            yield return AlphaFresh(1,d=>V2Rule(d,ConfigAction("J01",option)));
            AlphaPlace(-5,10);MouseAt((Vector2)game.Player.transform.position+Vector2.up*8,false);yield return Frames(4);
            mouseHeld=true;yield return Seconds(1.8f);
            Check("Jump branch accepts expected jumps "+option,option==0?game.Rules.AcceptedJumps==1:game.Rules.AcceptedJumps>=2);
        }
        // Render every merged parameter in the same real combat HUD used by all waves.
        yield return AlphaFresh(1,d=>{
            V2Rule(d,ConfigAction("M11",2));V2Rule(d,ConfigAction("D01",2));V2Rule(d,ConfigAction("Q01",1));
            V2Rule(d,ConfigAction("J01",1));V2Rule(d,ConfigAction("M03",2));V2Rule(d,ConfigAction("J04",1));
        });
        var hud=CardsUI().GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text).ToArray();
        Check("Combat HUD keeps all branch labels",game.DraftLoadout.OrderedRules().All(r=>hud.Any(s=>s.Contains(ConditionalCatalog.RuleActionLabel(r)))));
        yield return CardsCapture("merged-combat-rules");
        checkingPause=true;game.SetPause(true);float clock=game.FightClock;yield return RealSeconds(.1);
        Check("Pause freezes combat clock",game.FightClock==clock);game.Reconfigure();checkingPause=false;yield return Frames(3);
        Check("Pause Configuration retains parameters",game.Phase==DuelPhase.Configure&&game.Rules==null&&game.DraftLoadout.TotalRules==6);
        foreach(int wave in new[]{1,2,3})
        {
            yield return AlphaFresh(wave,null,false);float start=game.FightClock;int frame=Time.frameCount;yield return Seconds(.25f);
            Check("Normal W"+wave+" advances",game.Phase==DuelPhase.Fight&&game.FightClock>start&&Time.frameCount>frame);
        }
        game.MainMenu();yield return Frames(3);Check("Cleanup clears rules and actors",game.Rules==null&&game.Player==null);
    }
}
#endif
