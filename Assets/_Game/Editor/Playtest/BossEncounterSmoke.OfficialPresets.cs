#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Official Presets Data and Flow")]
    public static void RunOfficialPresets() => Start("official-presets",false);
    [Serializable] sealed class PresetRulesEnvelope { public OperationRules[] rules; }
    static OfficialRulePreset Preset(string id) => OfficialRulePresets.All.First(p=>p.Id==id);
    static string PresetFields(ConditionalRule r)
    {
        string result=r.Condition+":"+r.Action;
        if(r.AdditionalCondition!=0)result+=":a"+r.AdditionalCondition;
        if(r.GroundState!=0)result+=":g"+(int)r.GroundState;
        if(r.Distance!=0)result+=":d"+(int)r.Distance;
        if(r.IncomingAttack!=0)result+=":i"+(int)r.IncomingAttack;
        if(r.AdditionalGroundState!=0)result+=":ag"+(int)r.AdditionalGroundState;
        if(r.AdditionalDistance!=0)result+=":ad"+(int)r.AdditionalDistance;
        if(r.AdditionalIncomingAttack!=0)result+=":ai"+(int)r.AdditionalIncomingAttack;
        if(r.MoveDirection!=0)result+=":m"+(int)r.MoveDirection;
        if(r.ShortMoveDirection!=0)result+=":s"+(int)r.ShortMoveDirection;
        if(r.PlatformDirection!=0)result+=":p"+(int)r.PlatformDirection;
        if(r.AmmoUse!=0)result+=":ammo"+(int)r.AmmoUse;
        if(r.Below)result+=":below";
        return result;
    }
    static string PresetSnapshot(ConditionalLoadout loadout)
        =>string.Join(";",loadout.OrderedRules().Select(r=>r.DisplayOrder+"="+PresetFields(r)));
    static bool PresetApply(string id,bool training=false)
    {
        if(!game.DeveloperToolsOpen)game.ToggleDeveloperTools();
        bool applied=game.ApplyDeveloperLoadout(Preset(id).CreateLoadout(),training,out string error);
        Check(id+" developer apply succeeds",applied,error);
        return applied;
    }
    static ScrollRect PresetScroll(string name)=>CardsUI().GetComponentsInChildren<ScrollRect>().First(s=>s.name==name);
    static string PresetProgressKey => (string)typeof(OfficialRulePresets)
        .GetField("UnlockProgressKey",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetRawConstantValue();
    static Action restorePresetUnlockProgress;
    static void ProtectPresetUnlockProgress()
    {
        string key=PresetProgressKey;bool existed=PlayerPrefs.HasKey(key);int original=PlayerPrefs.GetInt(key);
        Record("Protect preset progress: exists="+existed+" raw="+original);
        restorePresetUnlockProgress=()=>{
            if(existed)PlayerPrefs.SetInt(key,original);else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Check("real preset unlock record restored",PlayerPrefs.HasKey(key)==existed&&(!existed||PlayerPrefs.GetInt(key)==original));
        };
    }
    static int HelpPresetCount()=>PresetScroll("HelpPresetList").content.GetComponentsInChildren<Button>().Length;
    static bool HasHelp()=>Private<bool>(CardsUI(),"presetHelpOpen");
    static int PresetDeathSubscribers(PlayerHealth player)
    {
        var handlers=(Action)typeof(PlayerHealth).GetField("OnDeath",PrivateInstance).GetValue(player);
        return handlers?.GetInvocationList().Count(d=>d.Target==game&&d.Method.Name=="RecordPresetDeath")??0;
    }
    static IEnumerator OfficialPresets()
    {
        Record("Six ordinary presets and real OnDeath lifecycle; actual UI callbacks, controlled deaths, protected PlayerPrefs.");
        string[] ids={"no_jump","no_dodge","no_aim","only_move","reload_retreat","do_nothing"};
        string[] names={"I don't want to jump","I don't want to dodge","I don't want to aim","I only want to move","Retreat while reloading","I don't want to do anything"};
        string[] expected={
            "53:J01:i2",
            "53:D01:i1;53:J01:i2",
            "53:D01:i1;53:J01:i2;52:Q01:d1;52:Q01:d2;52:Q01:d3",
            "53:D01:i1;53:J01:i2;52:Q01:d1;52:Q01:d2;52:Q01:d3;40:F03",
            "53:D02:a45:i1;53:D01:i1;53:J01:i2;9:M02;52:Q01:d1;52:Q01:d2;52:Q01:d3;40:F03",
            "53:D02:a45:i1;53:D01:i1;53:J01:i2;9:M02;51:M03:g1;51:M03:g2;52:Q01:d1;52:Q01:d2;52:Q01:d3;40:F03"
        };
        Check("exact six IDs and requested names",OfficialRulePresets.All.Select(p=>p.Id).SequenceEqual(ids)&&OfficialRulePresets.All.Select(p=>p.Name).SequenceEqual(names));
        for(int i=0;i<ids.Length;i++)
        {
            var preset=Preset(ids[i]);var first=preset.CreateLoadout();var other=preset.CreateLoadout();var rows=first.OrderedRules();
            string wanted=string.Join(";",expected[i].Split(';').Select((s,n)=>n+"="+s));
            Check(ids[i]+" complete rule fields, global order and catalog",ConditionalCatalog.IsValid(first)&&PresetSnapshot(first)==wanted&&preset.RuleCount==expected[i].Split(';').Length);
            var envelope=JsonUtility.FromJson<PresetRulesEnvelope>(JsonUtility.ToJson(new PresetRulesEnvelope{rules=first.Copy().Operations}));
            var roundTrip=new ConditionalLoadout();for(int op=0;op<5;op++)roundTrip.Operations[op]=envelope.rules[op];
            Check(ids[i]+" copy/serialization preserve fields",PresetSnapshot(first.Copy())==wanted&&PresetSnapshot(roundTrip)==wanted);
            rows[0].Condition=999;rows[0].Action="invalid";rows[0].ExtraConditions.Add(new RuleCondition(45));
            Check(ids[i]+" independent editable deep copies",PresetSnapshot(other)==wanted&&PresetSnapshot(preset.CreateLoadout())==wanted
                &&other.OrderedRules()[0].ExtraConditions.Count==0&&!ReferenceEquals(first.Operations[0],other.Operations[0]));
        }
        PlayerPrefs.SetInt(PresetProgressKey,-10);Check("invalid low progress clamps to first",OfficialRulePresets.UnlockedCount==1);
        PlayerPrefs.SetInt(PresetProgressKey,int.MaxValue);Check("invalid high progress clamps to six",OfficialRulePresets.UnlockedCount==6);
        PlayerPrefs.DeleteKey(PresetProgressKey);PlayerPrefs.Save();
        game.NewGame();yield return Frames(2);
        Check("fresh New Game empty, Help closed, training entry retained",game.DraftLoadout.TotalRules==0&&!HasHelp()&&game.TrainingPending&&OfficialRulePresets.UnlockedCount==1);
        ClickConfig("AddRule");yield return SelectEditorCondition(9);
        var draft=game.DraftLoadout;var pending=V2UIPending();string pendingJson=JsonUtility.ToJson(pending);
        ClickConfig("PresetHelp");yield return Frames(2);
        Check("Help exposes only first full preview",HelpPresetCount()==1&&CardsRows(PresetScroll("HelpPresetPreview")).Length==1);
        Check("locked Apply rejects without changing loadout or draft",!game.ApplyPresetToSetup(Preset("do_nothing"))&&ReferenceEquals(draft,game.DraftLoadout)&&pendingJson==JsonUtility.ToJson(V2UIPending()));
        ClickConfig("PresetHelpBack");yield return Frames(2);
        Check("Back preserves unfinished picker and loadout",ReferenceEquals(draft,game.DraftLoadout)&&ReferenceEquals(pending,V2UIPending())&&pendingJson==JsonUtility.ToJson(V2UIPending())&&!CardsUI().ConfigurationReady);
        ClickConfig("PresetHelp");yield return Frames(2);ClickConfig("ApplyHelpPreset");yield return Frames(2);
        Check("explicit Help Apply equips fresh editable copy only",game.Phase==DuelPhase.Configure&&game.Player==null&&!HasHelp()&&CardsUI().ConfigurationReady&&game.DraftLoadout.TotalRules==1);
        game.DraftLoadout.OrderedRules()[0].IncomingAttack=IncomingAttackOption.Any;
        Check("Help edits cannot mutate template",Preset("no_jump").CreateLoadout().OrderedRules()[0].IncomingAttack==IncomingAttackOption.Ranged);

        game.ToggleDeveloperTools();ClickConfig("Presets");yield return Frames(2);
        Check("F8 exposes all six at progress one",PresetScroll("PresetList").content.GetComponentsInChildren<Button>().Length==6);
        ClickConfig("Preset_do_nothing");yield return Frames(2);
        Check("F8 full ten-rule preview does not equip",CardsRows(PresetScroll("PresetPreview")).Length==10&&game.DraftLoadout.TotalRules==1);
        ClickConfig("Apply to Setup");yield return Frames(2);
        Check("F8 Apply bypasses Help lock without unlocking",game.DraftLoadout.TotalRules==10&&OfficialRulePresets.UnlockedCount==1&&!game.DeveloperToolsOpen);
        var source=Preset("do_nothing").CreateLoadout();game.ToggleDeveloperTools();game.ApplyDeveloperLoadout(source,true,out _);yield return Frames(3);game.Bosses[0].StopCombat();
        source.OrderedRules()[0].Action="invalid";source.OrderedRules()[0].ExtraConditions.Add(new RuleCondition(45));
        Check("F8 Apply deep copies source and starts normal Training",game.Phase==DuelPhase.Training&&ConditionalCatalog.IsValid(game.DraftLoadout)&&game.DraftLoadout.OrderedRules()[0].ConditionCount==2&&OfficialRulePresets.UnlockedCount==1);
        game.Player.IsInvulnerable=true;
        var robot=game.Bosses[0];robot.Health.TakeDamage(100000,null);
        yield return Until(()=>game.Bosses[0]!=robot,3.8f);game.Bosses[0].StopCombat();
        Check("robot death/respawn and rule rebind do not unlock or duplicate subscriptions",game.Bosses[0]!=robot&&game.Bosses[0].Health.CurrentHealth==200&&OfficialRulePresets.UnlockedCount==1&&PresetDeathSubscribers(game.Player)==1);
        var previous=game.Player;game.ToggleDeveloperTools();game.ApplyDeveloperLoadout(new ConditionalLoadout(),true,out _);
        Check("template restart unbinds old player without death",PresetDeathSubscribers(previous)==0&&OfficialRulePresets.UnlockedCount==1&&PresetDeathSubscribers(game.Player)==1);
        yield return Frames(3);game.Bosses[0].StopCombat();
        for(int life=1;life<=7;life++)
        {
            int before=OfficialRulePresets.UnlockedCount;var player=game.Player;player.IsInvulnerable=false;player.TakeDamage(100000,null);
            player.TakeDamage(100000,null);
            typeof(DuelGame).GetMethod("RecordPresetDeath",PrivateInstance).Invoke(game,null);
            Check("life "+life+" unlocks once with cap",OfficialRulePresets.UnlockedCount==Mathf.Min(before+1,6)&&PlayerPrefs.GetInt(PresetProgressKey)==OfficialRulePresets.UnlockedCount);
            yield return Until(()=>game.Player!=player,1.8f);game.Bosses[0].StopCombat();
            Check("Training respawn "+life+" preserves flow/config/progress",game.Phase==DuelPhase.Training&&game.Player!=player&&!game.Player.IsDead&&game.DraftLoadout.TotalRules==0&&OfficialRulePresets.UnlockedCount==Mathf.Min(before+1,6)&&PresetDeathSubscribers(game.Player)==1&&!HasHelp());
        }
        game.BackFromTraining();ClickConfig("PresetHelp");yield return Frames(2);ClickConfig("HelpPreset_do_nothing");yield return Frames(2);
        Check("Help now lists six with complete final preview",HelpPresetCount()==6&&CardsRows(PresetScroll("HelpPresetPreview")).Length==10);
        yield return CardsCapture("six-presets-help");
        // Stale visible button still goes through the same permission gate.
        var apply=ConfigButtonNamed("ApplyHelpPreset");PlayerPrefs.SetInt(PresetProgressKey,1);apply.onClick.Invoke();
        Check("stale Help Apply cannot bypass current lock",game.DraftLoadout.TotalRules==0&&HasHelp());
        PlayerPrefs.SetInt(PresetProgressKey,6);PlayerPrefs.Save();apply.onClick.Invoke();yield return Frames(2);
        Check("unlocked Help Apply stays in setup",game.DraftLoadout.TotalRules==10&&game.Phase==DuelPhase.Configure&&!HasHelp());
        string complete=PresetSnapshot(game.DraftLoadout);
        game.StartConfiguredEncounter();yield return Frames(2);game.ContinueFromTraining();game.StartFight();
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();game.Player.TakeDamage(100000,null);yield return Frames(3);
        Check("wave death retains Failed screen and capped progress",game.Phase==DuelPhase.Failed&&!HasHelp()&&OfficialRulePresets.UnlockedCount==6);
        game.Reconfigure();Check("Retry keeps loadout and unlocks",game.Phase==DuelPhase.Configure&&PresetSnapshot(game.DraftLoadout)==complete&&OfficialRulePresets.UnlockedCount==6);
        game.ToggleDeveloperTools();game.JumpToDeveloperWave(3);yield return Frames(2);
        Check("F8 wave switch retains progress",game.Wave==3&&OfficialRulePresets.UnlockedCount==6);
        previous=game.Player;game.NewGame();Check("New Game clears only loadout and unbinds previous player",game.DraftLoadout.TotalRules==0&&OfficialRulePresets.UnlockedCount==6&&PresetDeathSubscribers(previous)==0&&!HasHelp());
        game.MainMenu();
        Record("Persistence uses synchronous PlayerPrefs.Save; separate Play Mode restart readback follows. Standalone restart/physical input NOT TESTED.");
    }
}
#endif
