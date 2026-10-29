#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss and Bonus Progression")]
    public static void RunFinalBossProgression() => Start("final-boss-progression", false);

    static IEnumerator FinalBossProgression()
    {
        Directory.CreateDirectory(Output);
        game.NewGame(); PlayMetrics.MarkTestData(); game.Appearance.Name="Progression check";
        game.CompleteCharacterCreation();
        var appearance=game.Appearance;
        game.EditLoadout(d=>V2Rule(d,V2Ground("F03")),"add");
        string rules=JsonUtility.ToJson(game.DraftLoadout), path=PlayMetrics.JsonPath;
        game.StartConfiguredEncounter(); yield return Frames(5);
        Check("Normal first configuration still enters Training",game.Phase==DuelPhase.Training);
        game.ContinueFromTraining(); game.StartFight(); yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        game.Player.IsInvulnerable=true; yield return Seconds(.6f);
        Check("W1 combat advances with real rule shots",game.Wave==1&&game.FightClock>.4f&&PlayMetrics.Current.Shots>0);
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.Configure,1.3f);
        Check("W1 still advances to W2 configuration",game.Wave==2&&JsonUtility.ToJson(game.DraftLoadout)==rules);
        game.StartFight(); yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        game.Player.IsInvulnerable=true; yield return Seconds(.5f);
        foreach(var boss in game.Bosses)boss.Health.TakeDamage(100000,game.Player.gameObject);
        yield return Until(()=>game.Phase==DuelPhase.FinalBossReady,.6f);
        yield return Seconds(1);
        var ui=Object.FindAnyObjectByType<DuelUI>();
        var buttons=ui.GetComponentsInChildren<UnityEngine.UI.Button>();
        Check("W2 result waits and has only Meet the Final Boss",game.Phase==DuelPhase.FinalBossReady&&buttons.Length==1
            &&buttons[0].GetComponentInChildren<TMP_Text>().text=="Meet the Final Boss");
        Check("W2 closes the named record as complete",PlayMetrics.Run.levelCleared&&PlayMetrics.Run.finished&&PlayMetrics.Run.endReason=="complete"&&PlayMetrics.Current==null);
        string completedBytes=File.ReadAllText(path);
        SetAlphaGameSize(1280,720); yield return Frames(6);
        ScreenCapture.CaptureScreenshot(Output+"progression-result.png"); yield return Frames(3);
        ClickConfig("MeetFinalBoss"); yield return Frames(5);
        Check("Final visit has 9999 HP, no player or active combat controller",game.Wave==3&&game.Phase==DuelPhase.FinalBoss
            &&game.FinalBossHealth.CurrentHealth==9999&&game.FinalBossHealth.MaxHealth==9999&&game.Player==null&&game.Bosses.Count==0&&game.Rules==null);
        Check("Final arena has only the floor support",Private<Collider2D[]>(game,"stageSupports").Length==1);
        Check("Rainbow shader compiled and is supported",Resources.Load<Shader>("FinalBossRainbow").isSupported
            &&!ShaderUtil.ShaderHasError(Resources.Load<Shader>("FinalBossRainbow")));
        var position=game.FinalBossHealth.transform.position;
        var parts=game.FinalBossHealth.GetComponentsInChildren<SpriteRenderer>().Where(p=>p.enabled).ToArray();
        var poses=parts.Select(p=>p.transform.localToWorldMatrix).ToArray();
        game.ChooseBonus(true);
        Check("Choice cannot bypass dialogue",game.Phase==DuelPhase.FinalBoss&&game.FinalBossLine==0);
        yield return Seconds(.5f);
        Check("Final Boss stays completely stationary without physics or attacks",game.FinalBossHealth.transform.position==position
            &&!game.FinalBossHealth.GetComponent<Rigidbody2D>().simulated&&parts.Select((p,i)=>p.transform.localToWorldMatrix==poses[i]).All(v=>v)
            &&Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length==0&&game.FightClock==0);
        ScreenCapture.CaptureScreenshot(Output+"progression-final-first.png"); yield return Frames(3);
        string[] dialogue={"Hi, I'm the final boss.","I'm glad that you made it here.","The thing is, you won! Congrats!",
            "Turns out we didn't have time to present a final boss fight at the moment.","But... If you enjoyed the game.",
            "There is a bonus challenge.","Do you want to enter the bonus wave?"};
        for(int i=0;i<dialogue.Length;i++)
        {
            Check("Exact visible dialogue "+(i+1),ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==dialogue[i]));
            if(i==3){ScreenCapture.CaptureScreenshot(Output+"progression-final-long-line.png");yield return Frames(3);}
            if(i<dialogue.Length-1){ClickConfig("FinalBossNext");yield return Frames(2);}
        }
        game.ToggleDeveloperTools(); game.ToggleDeveloperTools(); yield return Frames(3);
        Check("F8 restores dialogue choice without restarting",game.FinalBossChoiceReady&&ui.GetComponentsInChildren<UnityEngine.UI.Button>().Length==2);
        ScreenCapture.CaptureScreenshot(Output+"progression-final-choice.png");yield return Frames(3);
        ClickConfig("BonusYes"); yield return Frames(4);
        Check("Yes enters Bonus configuration with same identity, rules and JSON",game.Phase==DuelPhase.Configure&&game.Wave==DuelGame.BonusWave
            &&ReferenceEquals(appearance,game.Appearance)&&JsonUtility.ToJson(game.DraftLoadout)==rules&&PlayMetrics.JsonPath==path&&!game.TrainingPending
            &&game.FinalBossHealth==null&&!PlayMetrics.RunActive);
        Check("Bonus configuration displays Bonus rather than W4",ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="BONUS WAVE"));
        game.StartConfiguredEncounter(); yield return Frames(5);
        Check("Bonus preparation restores five platforms and both Bosses",game.Phase==DuelPhase.Prepare&&game.Bosses.Count==2&&Private<Collider2D[]>(game,"stageSupports").Length==6);
        ScreenCapture.CaptureScreenshot(Output+"progression-bonus-prepare.png");yield return Frames(3);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f); game.Player.IsInvulnerable=true; yield return Seconds(.7f);
        Check("Bonus combat and original third music slot run",game.FightClock>.5f&&game.Bosses.All(b=>b.Running)
            &&Private<WaveMusicPlayer>(game,"music").CurrentWave==DuelGame.BonusWave&&PlayMetrics.Current==null&&!PlayMetrics.Counting);
        checkingPause=true; game.SetPause(true); float clock=game.FightClock; yield return RealSeconds(.2);
        Check("Bonus pause freezes the combat clock",game.FightClock==clock);
        game.Reconfigure(); checkingPause=false;
        Check("Paused Configuration retains Bonus and rules",game.Phase==DuelPhase.Configure&&game.Wave==DuelGame.BonusWave&&JsonUtility.ToJson(game.DraftLoadout)==rules);
        game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);
        game.Player.IsInvulnerable=false;game.Player.TakeDamage(100000,null);yield return Until(()=>game.Phase==DuelPhase.Failed,.6f);
        Check("Bonus defeat never changes recorded deaths or JSON",File.ReadAllText(path)==completedBytes);
        game.Reconfigure();game.StartFight();yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);game.Player.IsInvulnerable=true;
        game.Bosses[0].Health.TakeDamage(100000,game.Player.gameObject);yield return Frames(3);
        Check("Bonus requires both Bosses dead",game.Phase==DuelPhase.Fight);
        game.Bosses[1].Health.TakeDamage(100000,game.Player.gameObject);yield return Until(()=>game.Phase==DuelPhase.Complete,.6f);
        var saved=ReadNamedRun(path);
        Check("Final room and entire Bonus leave the W2 JSON byte-identical",File.ReadAllText(path)==completedBytes&&saved.schemaVersion==4&&saved.finished&&saved.levelCleared
            &&saved.attempts.All(a=>a.wave<=2)&&saved.attempts.Last().wave==2&&saved.attempts.Last().result=="won"&&PlayMetrics.Current==null);

        // Independent normal W2 result -> No branch, with a test-marked developer entry.
        yield return AlphaFresh(2,null); PlayMetrics.MarkTestData();
        game.Bosses[0].Health.TakeDamage(100000,game.Player.gameObject);yield return Until(()=>game.Phase==DuelPhase.FinalBossReady,.6f);
        string noBytes=File.ReadAllText(PlayMetrics.JsonPath);
        ClickConfig("MeetFinalBoss");yield return Frames(3);
        for(int i=0;i<6;i++){ClickConfig("FinalBossNext");yield return Frames(2);}
        string noPath=PlayMetrics.JsonPath;ClickConfig("BonusNo");yield return Frames(4);
        var declined=ReadNamedRun(noPath);
        Check("No returns to menu without changing the closed W2 record",game.Phase==DuelPhase.Menu&&game.FinalBossHealth==null
            &&declined.finished&&declined.endReason=="complete"&&declined.levelCleared&&File.ReadAllText(noPath)==noBytes);
        Check("Final visit leaves no material or object behind",!game.GetComponentsInChildren<Enemy>().Any()
            &&Private<Material>(game,"finalBossMaterial")==null);
        game.NewGame();game.CompleteCharacterCreation();yield return Frames(3);
        Check("New Game resets to empty W1 and a fresh record",game.Wave==1&&game.DraftLoadout.TotalRules==0&&game.TrainingPending
            &&PlayMetrics.RunActive&&!PlayMetrics.Run.levelCleared&&PlayMetrics.JsonPath!=noPath);
        game.MainMenu();
        Record("Controlled victories/death prove transitions, not encounter balance. Records: "+path+" ; "+noPath);
    }
}
#endif
