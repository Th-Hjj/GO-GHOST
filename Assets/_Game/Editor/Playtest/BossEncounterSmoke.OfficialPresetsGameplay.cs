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
    [MenuItem("GO GHOST/Playtest/Official Presets Gameplay")]
    public static void RunOfficialPresetsGameplay()=>Start("official-presets-gameplay",false);
    [MenuItem("GO GHOST/Playtest/Official Presets Final Edges")]
    public static void RunOfficialPresetsEdges()=>Start("official-presets-edges",false);
    static IEnumerator PresetTraining(string id,bool live=false)
    {
        mouseHeld=mouseBurstHeld=false;heldKeys=Array.Empty<Key>();game.MainMenu();PresetApply(id,true);yield return Frames(5);
        if(!live)game.Bosses[0].StopCombat();
    }
    static void PresetThreat()
    {
        var boss=game.Bosses[0];
        Fire(boss,boss.gameObject,(Vector2)game.Player.GetComponent<Collider2D>().bounds.center+Vector2.right*3,Vector2.left);
    }
    static void ClearPresetShots()
    {foreach(var shot in Projectile.Active.ToArray())if(shot!=null)shot.Retire();}
    static IEnumerator OfficialPresetsGameplay()
    {
        Record("Bounded preset checks: real projectile/executor fixtures, ordinary physics, then short live W1/W2/W3 observations. No balance/win claim.");
        yield return PresetTraining("no_jump");AlphaPlace(-4,8);yield return Seconds(.15f);PresetThreat();
        yield return Until(()=>game.Rules.AcceptedJumps>0,.5f);
        Check("R3 real ranged projectile requests Jump Once",game.Rules.AcceptedJumps==1&&game.Rules.AcceptedDodges==0);

        yield return PresetTraining("no_dodge");AlphaPlace(-4,8);yield return Seconds(.15f);PresetThreat();
        yield return Until(()=>game.Dodge.IsDodging,.5f);
        Check("R2 Dodge wins before R3 Jump in global order",game.Dodge.IsDodging&&game.Rules.AcceptedDodges==1&&game.Rules.AcceptedJumps==0&&game.Dodge.CommittedHorizontalSpeed<0);
        ClearPresetShots();yield return Until(()=>!game.Dodge.IsDodging,.5f);yield return Seconds(.12f);PresetThreat();
        yield return Until(()=>game.Rules.AcceptedJumps>0,.5f);
        Check("cooling Dodge falls through to executable Jump",game.Rules.AcceptedJumps==1&&game.Rules.AcceptedDodges==1);

        yield return PresetTraining("do_nothing");
        AlphaPlace(game.ArenaBounds.min.x+.65f,game.ArenaBounds.min.x+7);PresetThreat();
        yield return Until(()=>game.Dodge.IsDodging,.5f);
        Check("wall R1 dodges toward enemy before away/jump/movement",game.Dodge.IsDodging&&game.Dodge.CommittedHorizontalSpeed>0&&game.Rules.AcceptedJumps==0,
            "speed="+game.Dodge.CommittedHorizontalSpeed+" jumps="+game.Rules.AcceptedJumps);
        ClearPresetShots();

        yield return PresetTraining("only_move");AlphaPlace(-3,7);yield return Frames(2);
        // Keep the existing target alive through two magazines without changing weapon values.
        game.Bosses[0].Health.SetRuntimeHealth(10000,10000);
        int first=game.Rifle.ShotsFired;
        yield return Until(()=>game.Rifle.IsReloading,4);
        Check("R10 automatic F03 empties a magazine",game.Rifle.IsReloading&&game.Rifle.ShotsFired>=first+5&&game.Rifle.LastShotActionId=="F03");
        int atReload=game.Rifle.ShotsFired;yield return Until(()=>game.Rifle.ShotsFired>atReload,2.8f);
        Check("F03 resumes after reload without manual trigger",game.Rifle.ShotsFired>atReload&&game.Rifle.LastShotActionId=="F03"&&!mouseHeld&&!mouseBurstHeld);

        yield return PresetTraining("reload_retreat");AlphaPlace(-3,7);game.Bosses[0].Health.SetRuntimeHealth(10000,10000);
        yield return Until(()=>game.Rifle.IsReloading,4);float reloadX=game.Player.transform.position.x;yield return Seconds(.25f);
        Check("R4 reload drives ordinary retreat",game.Rifle.IsReloading&&game.Player.transform.position.x<reloadX-.4f&&game.Rules.Ownership.Move<0);

        game.ConfigurePlaytestWave(3);PresetApply("do_nothing");game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        game.Rifle.enabled=false;ClearPresetShots();
        var nav=game.Bosses[0].Navigation;
        OnFloor(game.Player.gameObject,nav,0);OnFloor(game.Bosses[0].gameObject,nav,5.5f);OnFloor(game.Bosses[1].gameObject,nav,-12);
        yield return Seconds(.15f);
        Check("W3 nearest in mid range releases Move despite another far enemy",!game.Rules.Ownership.MovementOwned,"move="+game.Rules.Ownership.Move);
        OnFloor(game.Bosses[0].gameObject,nav,2);yield return Seconds(.15f);
        Check("W3 grounded R5 retreats from nearest without Firing",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move<0&&!game.Rifle.IsFiring);
        OnFloor(game.Player.gameObject,nav,0);OnFloor(game.Bosses[0].gameObject,nav,11);OnFloor(game.Bosses[1].gameObject,nav,-13);yield return Seconds(.15f);
        Check("W3 grounded R5 approaches nearest far enemy",game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move>0);
        heldKeys=new[]{Key.Space};yield return Seconds(.1f);heldKeys=Array.Empty<Key>();
        Check("W3 airborne R6 keeps distance control without Firing",!game.Player.GetComponent<PlayerMovement>().IsGrounded&&game.Rules.Ownership.MovementOwned&&game.Rules.Ownership.Move>0&&!game.Rifle.IsFiring);
        yield return CardsCapture("six-presets-w3-distance");

        for(int wave=1;wave<=3;wave++)
        {
            game.ConfigurePlaytestWave(wave);PresetApply("do_nothing");game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
            int startFrame=Time.frameCount;float startClock=game.FightClock;int hp=game.Player.CurrentHealth;float until=Time.time+5;
            while(Time.time<until&&game.Phase==DuelPhase.Fight)yield return null;
            Check("W"+wave+" live scene advances without flow failure",Time.frameCount>startFrame&&game.FightClock>startClock+1);
            Record("OBSERVE W"+wave+" phase="+game.Phase+" clock="+game.FightClock.ToString("F2")+" hp="+hp+"->"+game.Player.CurrentHealth+" shots="+game.Rifle.ShotsFired+" jumps="+(game.Rules?.AcceptedJumps??0)+" dodges="+(game.Rules?.AcceptedDodges??0));
        }
        game.MainMenu();
        Record("NOT TESTED: physical input feel, sustained balance, full-fight outcomes, standalone or win rates.");
    }
    static IEnumerator OfficialPresetsEdges()
    {
        game.NewGame();PresetApply("do_nothing");yield return Frames(3);
        ClickConfig("MoveRuleUp9");yield return Frames(3);
        Check("ordinary last row reorders across operation boundaries",game.DraftLoadout.OrderedRules()[8].Action=="F03"&&game.DraftLoadout.OrderedRules().Select((r,i)=>r.DisplayOrder==i).All(v=>v));
        ClickConfig("RemoveRule9");yield return Frames(2);
        Check("ordinary late row deletes independently",game.DraftLoadout.TotalRules==9);
        PresetApply("do_nothing");Check("reapply restores fresh complete definitions",PresetSnapshot(game.DraftLoadout)==PresetSnapshot(Preset("do_nothing").CreateLoadout()));
        string before=PresetSnapshot(game.DraftLoadout);game.ToggleDeveloperTools();
        var invalid=Preset("do_nothing").CreateLoadout();invalid.OrderedRules()[0].Action="invalid";
        Check("invalid F8 apply preserves setup",!game.ApplyDeveloperLoadout(invalid,false,out _)&&PresetSnapshot(game.DraftLoadout)==before);
        game.ToggleDeveloperTools();game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        game.Player.TakeDamage(100000,null);yield return Until(()=>game.Phase==DuelPhase.Failed,1);
        PresetApply("no_dodge");Check("Failed F8 Apply retains normal same-wave setup",game.Phase==DuelPhase.Configure&&game.Wave==1&&game.Player==null&&game.DraftLoadout.TotalRules==2);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);StopPolicies();
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,null);
        yield return Until(()=>game.Phase==DuelPhase.Configure,2);
        Check("normal next-wave flow retains ordinary applied rules",game.Wave==2&&game.DraftLoadout.TotalRules==2);
        game.MainMenu();
    }
}
#endif
