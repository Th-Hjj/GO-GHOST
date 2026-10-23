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
    static IEnumerator OfficialPresets()
    {
        Record("Official preset data, actual UI callbacks and normal encounter lifecycle. Virtual input and controlled outcomes; no balance claim.");
        // Independent prompt transcription: all nondefault serialized fields are encoded, including optional conditions.
        string[] ids={"incoming_fire","jump_counter","roll_and_fan","shot_step","read_and_shoot","kiting_gunner","close_fan","hit_rush","run_and_gun","platform_rhythm","aerial_gunslinger","anchored_marksman","low_ammo_rush","emergency_protocol","shot_step_chain","full_cycle"};
        string[] expected={
            "53:J01:i2",
            "53:J01:i2;51:Q01:g2;58:F01",
            "7:Q01;56:Q01;7:F02:a52:ad1",
            "59:M04",
            "25:F01:a40;7:F01:a40;53:D01:i1",
            "45:M09;9:M02;17:M03;17:Q01;17:F03;53:D03:i1",
            "52:Q01:d1;56:Q01;52:F02:d1;11:F02:a52:ad1;25:D02:a52:ad1",
            "13:D02:a52:ad2;7:Q01;56:Q01;7:F02:a52:ad1;60:M02",
            "54:F03;53:D03:i2;9:J01",
            "9:J04:p1;56:J04:p2;18:Q06;40:F03",
            "45:M09;9:M02;51:J03:g1;53:D01:a51:i1:ag1;51:Q01:g2;56:Q01;58:F02:a52:ad1;58:F01:a52:ad2;58:F01:a52:ad3",
            "9:M09;17:M10;17:Q02;17:F03;53:D04:a17:i2",
            "53:D02:a12:i1;12:Q01:a52:ad1;12:F02:a52:ad1;17:F03",
            "45:M09:a4;9:M02:a4;4:Q02;4:F03;53:D01:a4:i1;6:J01:a4",
            "59:M04;61:Q06;61:F01",
            "45:M09;9:M02;52:M01:d3;52:M01:d2;52:M10:d1;52:Q01:d1;56:Q01;52:F02:d1;11:F02:a52:ad1;53:D01:i1"
        };
        Check("20 rules and five independent operations",ConditionalCatalog.MaxRulesTotal==20&&new ConditionalLoadout().Operations.Length==5);
        Check("16 stable IDs in prompt order",OfficialRulePresets.All.Select(p=>p.Id).SequenceEqual(ids));
        for(int i=0;i<ids.Length;i++)
        {
            var preset=Preset(ids[i]);var first=preset.CreateLoadout();var other=preset.CreateLoadout();var rows=first.OrderedRules();
            string wanted=string.Join(";",expected[i].Split(';').Select((s,n)=>n+"="+s));
            Check(ids[i]+" complete fields/order/count and catalog",ConditionalCatalog.IsValid(first)&&PresetSnapshot(first)==wanted
                &&preset.RuleCount==expected[i].Split(';').Length&&preset.Group==(i<4?"Learn":"Playstyles"));
            var envelope=JsonUtility.FromJson<PresetRulesEnvelope>(JsonUtility.ToJson(new PresetRulesEnvelope{rules=first.Copy().Operations}));
            var roundTrip=new ConditionalLoadout();for(int op=0;op<5;op++)roundTrip.Operations[op]=envelope.rules[op];
            Check(ids[i]+" copy and serialized rule envelope preserve all fields",PresetSnapshot(first.Copy())==wanted&&PresetSnapshot(roundTrip)==wanted);
            rows[0].Condition=999;rows[0].AdditionalCondition=999;rows[0].DisplayOrder=88;rows[0].Action="invalid";
            Check(ids[i]+" fresh copies cannot contaminate template or peers",PresetSnapshot(other)==wanted&&PresetSnapshot(preset.CreateLoadout())==wanted
                &&!ReferenceEquals(first.Operations[0],other.Operations[0]));
        }
        var twenty=Preset("full_cycle").CreateLoadout();
        for(int i=10;i<20;i++)Check("append ordinary row "+(i+1),twenty.TryAddRule(ControlOperation.Fire,new ConditionalRule{Condition=17,Action="F01"}));
        Check("20 retained by copy; 21st rejects atomically",twenty.Copy().TotalRules==20&&!twenty.TryAddRule(ControlOperation.Fire,new ConditionalRule{Condition=17,Action="F01"})&&twenty.TotalRules==20);

        game.NewGame();ClickConfig("AddRule");yield return Frames(3);
        Check("fixture opens uncommitted picker",!CardsUI().ConfigurationReady);
        game.ToggleDeveloperTools();ClickConfig("Presets");yield return Frames(3);
        string empty=PresetSnapshot(game.DraftLoadout);
        Check("default preview does not equip",game.DraftLoadout.TotalRules==0&&CardsUI().GetComponentsInChildren<Button>().Any(b=>b.name=="Preset_incoming_fire"));
        ClickConfig("Preset_full_cycle");yield return Frames(3);
        Check("selecting full preview preserves setup/picker/tuning",PresetSnapshot(game.DraftLoadout)==empty&&!CardsUI().ConfigurationReady);
        for(int width=1280;width<=1920;width+=640)
        {
            SetAlphaGameSize(width,width*9/16);yield return Frames(7);
            var list=PresetScroll("PresetList");var preview=PresetScroll("PresetPreview");
            var cards=CardsRows(preview);
            Check(width+" all 16 entries and ten real preview rules",list.content.GetComponentsInChildren<Button>().Length==16&&cards.Length==10);
            Check(width+" both preset columns mask and scroll",list.viewport.GetComponent<RectMask2D>()!=null&&preview.viewport.GetComponent<RectMask2D>()!=null
                &&list.content.rect.height>list.viewport.rect.height&&preview.content.rect.height>preview.viewport.rect.height);
            Check(width+" preset footer remains outside scrolling and on screen",new[]{"Apply to Setup","Try in Training","Clear Rules"}.All(n=>
                CardsOnScreen((RectTransform)ConfigButtonNamed(n).transform)&&ConfigButtonNamed(n).GetComponentInParent<ScrollRect>()==null));
            TextFits(width+" preset preview");yield return CardsCapture("presets-"+width+"-top");
            preview.verticalNormalizedPosition=0;list.verticalNormalizedPosition=0;yield return Frames(4);
            Check(width+" final preview row can scroll into viewport",CardsInside(preview.viewport,cards.Last(),3));
            yield return CardsCapture("presets-"+width+"-bottom");preview.verticalNormalizedPosition=1;
        }
        game.SetDeveloperValue(DeveloperSetting.Damage,37);
        ClickConfig("Apply to Setup");yield return Frames(3);
        Check("Configure apply closes F8 and drops picker without starting",game.Phase==DuelPhase.Configure&&!game.DeveloperToolsOpen&&game.Player==null
            &&CardsUI().ConfigurationReady&&V2UIPending()==null&&game.DraftLoadout.TotalRules==10);
        string full=PresetSnapshot(game.DraftLoadout);
        CardsLayout(false,10,"real ten-rule setup");yield return CardsCapture("presets-setup-ten");
        game.ToggleDeveloperTools();var invalid=Preset("full_cycle").CreateLoadout();invalid.OrderedRules()[7].Action="invalid";
        Check("invalid application preserves complete prior setup",!game.ApplyDeveloperLoadout(invalid,false,out string reason)&&!string.IsNullOrEmpty(reason)&&PresetSnapshot(game.DraftLoadout)==full&&game.DeveloperToolsOpen);
        ClickConfig("Try in Training");yield return Frames(4);
        Check("Try creates normal training and keeps tuning",game.Phase==DuelPhase.Training&&!game.Paused&&!game.DeveloperToolsOpen&&game.Rifle.BulletDamage==37&&game.Bosses[0].IsTrainingEnemy);
        var oldPlayer=game.Player;var oldRules=game.Rules;PresetApply("full_cycle");yield return Frames(4);
        Check("Training apply replaces actors/controller and resets resources",oldPlayer==null&&Private<bool>(oldRules,"disposed")&&game.Phase==DuelPhase.Training
            &&game.Player.CurrentHealth==game.Player.MaxHealth&&game.Rifle.AmmoInMagazine==game.Rifle.MagazineSize);
        game.BackFromTraining();Check("Back retains training selection",game.Phase==DuelPhase.Configure&&game.TrainingPending&&PresetSnapshot(game.DraftLoadout)==full);
        game.StartConfiguredEncounter();yield return Frames(3);game.ContinueFromTraining();game.StartFight();yield return Frames(2);
        PresetApply("full_cycle");Check("Prepare apply retains wave and normal three seconds",game.Phase==DuelPhase.Prepare&&game.Wave==1&&game.PreparationRemaining>2.85f&&!game.Paused);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        oldPlayer=game.Player;oldRules=game.Rules;PresetApply("full_cycle");yield return Frames(3);
        Check("Fight apply disposes old state and restarts same wave",oldPlayer==null&&Private<bool>(oldRules,"disposed")&&game.Phase==DuelPhase.Prepare&&game.Wave==1
            &&Projectile.Active.Count==0&&Time.timeScale==1&&!AudioListener.pause);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        for(int width=1280;width<=1920;width+=640)
        {
            SetAlphaGameSize(width,width*9/16);yield return Frames(6);CardsLayout(true,10,width+" real ten-rule HUD");CardsHudBinding(width+" HUD");
            var scroll=CardsScroll(true);scroll.verticalNormalizedPosition=0;yield return Frames(5);float retained=scroll.verticalNormalizedPosition;
            yield return Seconds(.15f);Check(width+" feedback does not move HUD scroll",Mathf.Abs(scroll.verticalNormalizedPosition-retained)<.001f);
            yield return CardsCapture("presets-hud-ten-"+width);scroll.verticalNormalizedPosition=1;
        }
        game.Player.TakeDamage(10000,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1);
        game.Reconfigure();Check("retry keeps ten complete rows and order",PresetSnapshot(game.DraftLoadout)==full);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,null);yield return Until(()=>game.Phase==DuelPhase.Between,1);
        Check("controlled victory reaches Between",game.Phase==DuelPhase.Between);PresetApply("full_cycle");
        Check("Between apply stays at the current wave in setup",game.Phase==DuelPhase.Configure&&game.Wave==1);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,null);yield return Until(()=>game.Phase==DuelPhase.Configure,2);
        Check("normal next wave retains all rows",game.Wave==2&&PresetSnapshot(game.DraftLoadout)==full);
        game.ToggleDeveloperTools();game.JumpToDeveloperWave(3);yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,null);yield return Until(()=>game.Phase==DuelPhase.Complete,1);PresetApply("full_cycle");
        Check("Complete apply retains W3 and returns setup",game.Phase==DuelPhase.Configure&&game.Wave==3);
        PresetApply("incoming_fire",true);yield return Frames(3);game.BackFromTraining();
        Check("W3 Try uses training without forging FirstConfiguration",game.Phase==DuelPhase.Configure&&game.Wave==3&&!game.FirstConfiguration&&game.TrainingPending);
        game.StartConfiguredEncounter();yield return Frames(3);game.ContinueFromTraining();
        Check("normal Continue returns W1 configuration",game.Phase==DuelPhase.Configure&&game.Wave==1&&!game.TrainingPending);
        game.MainMenu();PresetApply("full_cycle");Check("Menu apply opens setup only",game.Phase==DuelPhase.Configure&&game.Player==null);
        game.ToggleDeveloperTools();game.ApplyDeveloperLoadout(twenty,true,out _);yield return Frames(4);game.Bosses[0].StopCombat();
        Check("real controller and HUD retain 20 ordinary rules",Enumerable.Range(0,5).Sum(i=>game.Rules.RuleCount((ControlOperation)i))==20&&CardsRows(CardsScroll(true)).Length==20);
        game.ToggleDeveloperTools();game.ResetDeveloperTuning();Check("Restore defaults leaves 20 rules",game.DraftLoadout.TotalRules==20&&game.GetDeveloperValue(DeveloperSetting.Damage)==30);
        ClickConfig("Presets");mouseHeld=mouseBurstHeld=true;ClickConfig("Clear Rules");yield return Seconds(.6f);
        Check("Clear rebuilds manual Training and guards both held buttons",game.Phase==DuelPhase.Training&&game.DraftLoadout.TotalRules==0&&game.Rifle.ShotsFired==0&&!game.Paused);
        mouseHeld=mouseBurstHeld=false;yield return Frames(4);RevolverAim();yield return RevolverClick(true);yield return Until(()=>game.Rifle.ShotsFired>0,.5f);
        Check("fresh released RMB works after Clear",game.Rifle.ShotsFired>0);
        checkingPause=true;game.SetPause(true);game.ToggleDeveloperTools();game.ToggleDeveloperTools();
        Check("closing F8 preserves previous pause",game.Paused&&Time.timeScale==0);game.SetPause(false);checkingPause=false;
        game.NewGame();Check("New Game still clears rules",game.DraftLoadout.TotalRules==0);game.MainMenu();
        Record("NOT TESTED: physical input, standalone, full-fight outcomes and balance.");
    }
}
#endif
