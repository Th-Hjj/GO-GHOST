#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Object=UnityEngine.Object;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Developer Tuning Smoke")]
    public static void RunDeveloperTuning()=>Start("developer-tuning",false);
    [MenuItem("GO GHOST/Playtest/Developer Wave Jump Smoke")]
    public static void RunDeveloperWaveJump()=>Start("developer-wave",false);
    static IEnumerator DeveloperWaveJump()
    {
        Record("Focused developer wave buttons, virtual F8/Escape and normal fight lifecycle.");
        game.NewGame();game.CompleteCharacterCreation();
        V2Rule(game.DraftLoadout,V2Ground("Q01"));
        var toward=V2Ground("M11");toward.MoveDirection=MoveDirectionOption.TowardEnemy;V2Rule(game.DraftLoadout,toward);
        ClickConfig("AddRule");yield return Frames(2);
        Check("fixture has an uncommitted picker",!Object.FindAnyObjectByType<DuelUI>().ConfigurationReady);
        yield return DeveloperKey(Key.F8);
        game.SetDeveloperValue(DeveloperSetting.MoveSpeed,9);
        ClickConfig("Waves");ClickConfig("Dev_Wave4");
        Check("Bonus jump discards picker and starts fresh preparation",game.Wave==DuelGame.BonusWave&&game.Phase==DuelPhase.Prepare&&!game.DeveloperToolsOpen&&!game.Paused&&Time.timeScale==1&&!AudioListener.pause);
        Check("jump keeps equipped order and tuning",game.DraftLoadout.TotalRules==2&&game.DraftLoadout.OrderedRules()[0].Action=="Q01"&&game.Player.GetComponent<PlayerMovement>().MoveSpeed==9);
        Check("Bonus has both fresh actors and full resources",game.Bosses.Count==2&&game.Bosses.All(b=>b.Health.CurrentHealth==b.Health.MaxHealth)&&game.Player.CurrentHealth==game.Player.MaxHealth&&game.Rifle.AmmoInMagazine==game.Rifle.MagazineSize);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);yield return Seconds(.5f);
        Check("Bonus reaches live combat",game.Phase==DuelPhase.Fight&&game.FightClock>0);
        checkingPause=true;game.SetPause(true);yield return DeveloperKey(Key.F8);yield return DeveloperKey(Key.Escape);
        Check("closing panel preserves preexisting pause",!game.DeveloperToolsOpen&&game.Paused&&Time.timeScale==0);
        yield return DeveloperKey(Key.F8);
        var oldPlayer=game.Player;var oldRules=game.Rules;
        ClickConfig("Waves");ClickConfig("Dev_Wave1");yield return Frames(3);
        Check("paused Bonus to W1 clears old encounter",game.Wave==1&&game.Phase==DuelPhase.Prepare&&oldPlayer==null&&!ReferenceEquals(oldRules,game.Rules)&&game.Bosses.Count==1&&game.Bosses[0].ActorId==1&&!game.Paused);
        yield return DeveloperKey(Key.F8);ClickConfig("Waves");ClickConfig("Dev_Wave2");
        Check("jump during preparation selects Swordsman",game.Wave==2&&game.Phase==DuelPhase.Prepare&&game.Bosses.Count==1&&game.Bosses[0].ActorId==2);
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);yield return Seconds(.5f);
        Check("W2 reaches live combat",game.Phase==DuelPhase.Fight&&game.FightClock>0);
        yield return DeveloperKey(Key.F8);oldPlayer=game.Player;ClickConfig("Waves");ClickConfig("Dev_Wave2");yield return Frames(3);
        Check("current wave button restarts fresh",game.Wave==2&&game.Phase==DuelPhase.Prepare&&oldPlayer==null&&game.FightClock==0);
        yield return DeveloperKey(Key.F8);ClickConfig("Waves");ClickConfig("Dev_Wave1");
        yield return Until(()=>game.Phase==DuelPhase.Fight,4.2f);yield return Seconds(.5f);
        Check("W1 reaches live combat",game.Phase==DuelPhase.Fight&&game.FightClock>0);
        game.Player.TakeDamage(10000,null);yield return Until(()=>game.Phase==DuelPhase.Failed,.5f);
        yield return DeveloperKey(Key.F8);ClickConfig("Waves");ClickConfig("Dev_Wave4");
        Check("defeat to Bonus recovers cleanly",game.Phase==DuelPhase.Prepare&&game.Wave==DuelGame.BonusWave&&!game.Player.IsDead&&game.DraftLoadout.TotalRules==2);
        yield return DeveloperKey(Key.F8);
        SetAlphaGameSize(1280,720);yield return Frames(8);
        ScreenCapture.CaptureScreenshot(Output+"developer-wave-1280.png");yield return Frames(3);
        ClickConfig("Tuning");ClickConfig("Health");SetAlphaGameSize(1920,1080);yield return Frames(8);
        ScreenCapture.CaptureScreenshot(Output+"developer-wave-health-1920.png");yield return Frames(3);
        Check("health controls remain editable",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMP_InputField>().All(f=>f.interactable));
        game.ResetDeveloperTuning();game.MainMenu();yield return DeveloperKey(Key.F8);ClickConfig("Waves");ClickConfig("Dev_Wave1");
        Check("menu jump works and defaults restore",game.Phase==DuelPhase.Prepare&&game.Wave==1&&game.Player.GetComponent<PlayerMovement>().MoveSpeed==7);
        game.MainMenu();yield return Frames(3);
        Check("menu cleanup releases pause and actors",game.Player==null&&game.Bosses.Count==0&&!game.DeveloperToolsOpen&&Time.timeScale==1&&!AudioListener.pause);
        checkingPause=false;
        Record("NOT TESTED: physical input, standalone, full fight outcomes and balance.");
    }
    static IEnumerator DeveloperKey(Key key)
    {heldKeys=new[]{key};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Frames(3);}
    static IEnumerator DeveloperTuning()
    {
        Record("F8 runtime tuning: controlled real executors and virtual keyboard; no balance or hardware claim.");
        yield return AlphaFresh(1,null);AlphaPlace(-6,3);yield return Frames(4);
        var body=game.Player.GetComponent<Rigidbody2D>();var movement=game.Player.GetComponent<PlayerMovement>();var instance=game.Rules;
        checkingPause=true;yield return DeveloperKey(Key.F8);
        Check("F8 opens paused developer panel",game.DeveloperToolsOpen&&game.Paused&&Time.timeScale==0&&Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMP_InputField>().Length==6);
        float time=Time.time;var position=body.position;int ammo=game.Rifle.AmmoInMagazine;
        heldKeys=new[]{Key.D,Key.Space,Key.LeftShift};mouseHeld=true;yield return RealSeconds(.15);heldKeys=Array.Empty<Key>();mouseHeld=false;
        Check("panel freezes real combat and blocks action input",Time.time==time&&body.position==position&&game.Rifle.AmmoInMagazine==ammo&&!game.Dodge.IsDodging);
        var input=Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMP_InputField>().First(f=>f.name=="Dev_MoveSpeed");
        input.onEndEdit.Invoke("10");
        Check("numeric field writes effective Move speed",movement.MoveSpeed==10&&input.text=="10");
        input.onEndEdit.Invoke("NaN");Check("nonfinite input rejected",movement.MoveSpeed==10);
        game.SetDeveloperValue(DeveloperSetting.JumpSpeed,15);
        game.SetDeveloperValue(DeveloperSetting.DodgeDistance,2);game.SetDeveloperValue(DeveloperSetting.DodgeDuration,.2f);
        game.SetDeveloperValue(DeveloperSetting.DodgeCooldown,.6f);game.SetDeveloperValue(DeveloperSetting.DodgeImmunity,.1f);
        game.SetDeveloperValue(DeveloperSetting.Damage,25);game.SetDeveloperValue(DeveloperSetting.BulletSpeed,22);
        game.SetDeveloperValue(DeveloperSetting.FireInterval,.15f);game.SetDeveloperValue(DeveloperSetting.Magazine,6);game.SetDeveloperValue(DeveloperSetting.Reload,.6f);
        game.SetDeveloperValue(DeveloperSetting.PlayerMaxHealth,250);game.SetDeveloperValue(DeveloperSetting.GunnerMaxHealth,700);
        Check("max HP edits do not refill living actors",game.Player.MaxHealth==250&&game.Player.CurrentHealth==150&&game.Bosses[0].Health.MaxHealth==700&&game.Bosses[0].Health.CurrentHealth==666);
        game.SetDeveloperHealth(0,200);game.SetDeveloperHealth(1,600);
        Check("current HP edits apply without recreating actors",game.Player.CurrentHealth==200&&game.Bosses[0].Health.CurrentHealth==600&&game.Rules==instance);
        SetAlphaGameSize(1280,720);yield return Frames(8);ScreenCapture.CaptureScreenshot(Output+"developer-player-1280.png");yield return Frames(3);
        yield return DeveloperKey(Key.F8);
        Check("F8 restores running fight and same controller",!game.DeveloperToolsOpen&&!game.Paused&&game.Rules==instance);
        float x=body.position.x;heldKeys=new[]{Key.D};yield return Seconds(.2f);heldKeys=Array.Empty<Key>();yield return Frames(2);
        Check("edited Move drives real velocity and displacement",body.position.x>x+1.6f&&movement.MoveSpeed==10,$"x={x:F2}->{body.position.x:F2}");
        heldKeys=new[]{Key.Space};yield return Until(()=>body.linearVelocity.y>14,.3f);heldKeys=Array.Empty<Key>();
        Check("edited Jump drives real takeoff",body.linearVelocity.y>14,$"velocity={body.linearVelocity}");
        yield return Until(()=>movement.IsGrounded,1.6f);yield return Frames(3);
        x=body.position.x;float start=game.Dodge.LastDodgeStartedAt;
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>start,.3f);heldKeys=Array.Empty<Key>();
        Check("edited Dodge cooldown is accepted by original component",game.Dodge.IsDodging&&game.Dodge.CooldownRemaining>.5f&&game.Dodge.CooldownRemaining<=.6f);
        yield return Seconds(.3f);
        Check("edited Dodge drives two-unit travel",Mathf.Abs(Mathf.Abs(body.position.x-x)-2)<.18f,$"travel={body.position.x-x:F3}");
        AlphaPlace(-6,0);yield return Frames(5);int hp=game.Bosses[0].Health.CurrentHealth;
        MouseAt(game.Bosses[0].Body.bounds.center,true);yield return Until(()=>game.Rifle.TriggerPulls>0,.3f);mouseHeld=false;
        yield return Until(()=>game.Bosses[0].Health.CurrentHealth<hp,.5f);
        Check("edited gun spends ammo and deals real 25 damage",game.Rifle.AmmoInMagazine==5&&game.Bosses[0].Health.CurrentHealth==hp-25&&game.Rifle.BulletSpeed==22);
        mouseHeld=true;yield return Until(()=>game.Rifle.IsReloading,1.2f);mouseHeld=false;
        Check("edited magazine and cadence reach real reload",game.Rifle.IsReloading&&game.Rifle.TriggerPulls==6&&game.Rifle.MagazineSize==6&&game.Rifle.ReloadRemaining>.45f);
        yield return DeveloperKey(Key.F8);float remaining=game.Rifle.ReloadRemaining;
        game.SetDeveloperValue(DeveloperSetting.Reload,.1f);game.SetDeveloperValue(DeveloperSetting.Magazine,10);
        Check("editing weapon preserves active reload and empty ammo",Mathf.Approximately(game.Rifle.ReloadRemaining,remaining)&&game.Rifle.AmmoInMagazine==0);
        yield return DeveloperKey(Key.Escape);yield return Until(()=>!game.Rifle.IsReloading,.8f);
        Check("normal reload fills edited capacity without extra shots",game.Rifle.AmmoInMagazine==10&&game.Rifle.TriggerPulls==6&&!game.DeveloperToolsOpen&&!game.Paused);

        game.SetPause(true);yield return DeveloperKey(Key.F8);yield return DeveloperKey(Key.F8);
        Check("F8 preserves a pre-existing pause",game.Paused&&!game.DeveloperToolsOpen);game.SetPause(false);
        yield return DeveloperKey(Key.F8);game.ResetDeveloperTuning();yield return DeveloperKey(Key.F8);
        AlphaPlace(-6,3);yield return Until(()=>game.Dodge.CanDodge,.8f);yield return Frames(3);
        x=body.position.x;start=game.Dodge.LastDodgeStartedAt;
        heldKeys=new[]{Key.LeftShift};yield return Until(()=>game.Dodge.LastDodgeStartedAt>start,.3f);heldKeys=Array.Empty<Key>();
        yield return DeveloperKey(Key.F8);remaining=game.Dodge.CooldownRemaining;
        game.SetDeveloperValue(DeveloperSetting.DodgeDistance,1);game.SetDeveloperValue(DeveloperSetting.DodgeDuration,.4f);
        game.SetDeveloperValue(DeveloperSetting.DodgeCooldown,5);game.SetDeveloperValue(DeveloperSetting.DodgeImmunity,1);
        Check("Dodge edit preserves active cooldown deadline",Mathf.Approximately(game.Dodge.CooldownRemaining,remaining));
        yield return DeveloperKey(Key.F8);yield return Seconds(.28f);
        Check("active Dodge retains old distance duration and immunity",Mathf.Abs(Mathf.Abs(body.position.x-x)-3)<.18f&&!game.Dodge.IsDodging&&!game.Player.IsInvulnerable&&game.Dodge.CooldownRemaining<2,$"travel={body.position.x-x:F3}");
        yield return DeveloperKey(Key.F8);game.SetDeveloperValue(DeveloperSetting.MoveSpeed,9);game.ToggleDeveloperTools();
        yield return AlphaFresh(3,null);
        Check("tuning survives retry and new wave actors",game.Player.GetComponent<PlayerMovement>().MoveSpeed==9&&game.Dodge.DodgeStartInterval==5&&game.Bosses.Count==2);
        yield return DeveloperKey(Key.F8);ClickConfig("Health");yield return Frames(3);
        SetAlphaGameSize(1920,1080);yield return Frames(8);ScreenCapture.CaptureScreenshot(Output+"developer-health-1920.png");yield return Frames(3);
        Check("health panel contains editable living Boss values",Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<TMP_InputField>().All(f=>f.interactable));
        game.ResetDeveloperTuning();game.ToggleDeveloperTools();
        Check("restore defaults reaches actual components",game.Player.GetComponent<PlayerMovement>().MoveSpeed==7&&Mathf.Approximately(game.Dodge.DodgeStartInterval,PlayerDodge.LegacyCooldown)&&game.Rifle.BulletDamage==28&&game.Rifle.MagazineSize==6);
        yield return AlphaFresh(1,d=>{var rule=V2Ground("M11");rule.MoveDirection=MoveDirectionOption.TowardEnemy;V2Rule(d,rule);});AlphaPlace(-8,3);yield return DeveloperKey(Key.F8);
        game.SetDeveloperValue(DeveloperSetting.MoveSpeed,0);game.SetDeveloperValue(DeveloperSetting.DodgeImmunity,0);
        game.ToggleDeveloperTools();yield return Frames(8);
        Check("zero Move speed does not claim rule execution",game.Player.GetComponent<Rigidbody2D>().linearVelocity.x==0&&!game.Player.GetComponent<PlayerInputReader>().ConditionalFrame.MovementOwned&&!Feedback(0).Executing);
        bool dodged=game.Dodge.TryDodge(1);int beforeHit=game.Player.CurrentHealth;game.Player.TakeDamage(1,null);
        Check("zero Dodge immunity grants no free first physics step",dodged&&!game.Player.IsInvulnerable&&game.Player.CurrentHealth==beforeHit-1);
        yield return DeveloperKey(Key.F8);game.ResetDeveloperTuning();game.ToggleDeveloperTools();
        game.NewGame();game.CompleteCharacterCreation();yield return Frames(3);ClickConfig("AddRule");yield return Frames(3);
        yield return DeveloperKey(Key.F8);yield return DeveloperKey(Key.Escape);
        Check("Esc closes developer panel without closing pending picker",!game.DeveloperToolsOpen&&!Object.FindAnyObjectByType<DuelUI>().ConfigurationReady
            &&Object.FindAnyObjectByType<DuelUI>().GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="Option"));
        game.MainMenu();yield return DeveloperKey(Key.F8);Check("F8 also opens from Menu",game.DeveloperToolsOpen&&!game.Paused);
        game.ToggleDeveloperTools();checkingPause=false;RestoreFixture();yield return Frames(3);
        for(int wave=1;wave<=3;wave++)
        {
            yield return AlphaFresh(wave,null,false);MouseAt(game.Bosses[0].Body.bounds.center,true);yield return Seconds(2);mouseHeld=false;
            Record($"SHORT {game.WaveLabel} phase={game.Phase} time={game.FightClock:F2} paidShots={game.Rifle.TriggerPulls} hp={game.Player.CurrentHealth}");
        }
        game.MainMenu();yield return Frames(3);
        Record("NOT TESTED: physical input, balance, exhaustive tuning combinations and long sessions.");
    }
}
#endif
