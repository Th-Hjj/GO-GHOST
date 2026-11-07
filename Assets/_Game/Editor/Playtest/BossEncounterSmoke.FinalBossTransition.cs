#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss Transition")]
    public static void RunFinalBossTransition() => Start("final-boss-transition", false);

    // Plays through the scripted phase change (about 35 s) and picks a player line through its UI callback.
    static IEnumerator CompleteFinalBossTransition(int choice = 0)
    {
        yield return Until(() => game.CutsceneChoiceOpen, 45f);
        if (game.CutsceneChoiceOpen) game.ChooseFinalBossLine(choice);
        yield return Until(() => game.Phase == DuelPhase.Fight && !game.FinalBossCutscene, 25f);
        yield return Frames(3);
    }

    // Final Boss stage 7: the scripted phase change from the first pool to the bare-handed fight.
    static IEnumerator FinalBossPhaseChange()
    {
        Record("Final Boss transition: developer Wave 3 with two player rules, normal dialogue/countdown. The first pool is emptied directly; the player is invulnerable. The choice is clicked through its UI callback. Sequence and presentation only; listening not claimed.");
        game.ConfigurePlaytestWave(3); yield return Frames(4);
        game.EditLoadout(d =>
        {
            d.TryAddRule((int)ControlOperation.Aim, new ConditionalRule { Condition = 13, Action = "Q01" });
            d.TryAddRule((int)ControlOperation.Fire, new ConditionalRule { Condition = 6, Action = "F03" });
        });
        SetAlphaGameSize(1920, 1080); yield return Until(() => Screen.width == 1920, 2f);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.FinalBoss, 2f);
        while (game.Phase == DuelPhase.FinalBoss) { game.AdvanceFinalBossDialogue(); yield return Frames(1); }
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        var actor = game.Bosses.Single(); var axe = actor.Axe;
        var view = actor.GetComponentInChildren<BossSilhouetteView>();
        var reader = game.Player.GetComponent<PlayerInputReader>();
        var visual = game.Player.GetComponent<OfficerVisual>();
        var speaker = Private<AudioSource>(game.GetComponent<HandlerVoice>(), "speaker");
        var torso = actor.GetComponentsInChildren<SpriteRenderer>(true).First(r => r.name.Contains("Torso"));
        float cameraBefore = Camera.main.pixelRect.width;
        bool Exists(string name) => CardsUI().GetComponentsInChildren<RectTransform>().Any(r => r.name == name);
        int Bound() => Enumerable.Range(0, 5).Sum(op => game.Rules.RuleCount((ControlOperation)op));

        game.Player.IsInvulnerable = true;
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => game.CutsceneStep == 1, 1f); yield return Frames(3);
        Check("Emptying the first pool starts the transition with input off and both sidebars live",
            game.FinalBossCutscene && game.Phase == DuelPhase.Fight && !reader.GameplayInputEnabled && actor.Health.AwaitingSecondPhase
            && Exists("BattleRules") && Exists("BossRules") && !CardsUI().SidebarsSealed && Bound() == 2);
        Check("Line 1 shows in the subtitle box and plays", Exists("FinalBossCutsceneLine") && game.CutsceneSpeakerId == DuelGame.CutsceneSpeaker.FinalBoss
            && game.CutsceneText == VoiceStudioCatalog.Current.Find("FinalBossVoice/Turn1").text && speaker.clip == Resources.Load<AudioClip>("FinalBossVoice/Turn1"));
        ScreenCapture.CaptureScreenshot(Output + "final-boss-transition-line1.png"); yield return Frames(3);

        checkingPause = true;
        game.SetPause(true); yield return Frames(30);
        Check("Pause holds the transition", game.CutsceneStep == 1 && game.CutsceneText == VoiceStudioCatalog.Current.Find("FinalBossVoice/Turn1").text);
        game.SetPause(false); checkingPause = false;

        yield return Until(() => game.CutsceneStep == 2, 8f); yield return Seconds(.5f);
        Check("Shockwave: Happy warns, rules seal and cross out, assistance is removed, platforms move",
            speaker.clip == Resources.Load<AudioClip>("Handler/LookOut") && CardsUI().SidebarsSealed && Bound() == 0
            && CardsUI().GetComponentsInChildren<RectTransform>().Count(r => r.name == "SealCross") == 2 * (2 + 5)
            && game.Bosses.Single().Navigation != null && LayoutSupports().Skip(1).All(c => c.GetComponent<PlatformOscillator>() != null));
        ScreenCapture.CaptureScreenshot(Output + "final-boss-transition-shockwave.png"); yield return Frames(3);

        yield return Until(() => game.CutsceneStep == 3, 6f);
        Check("The rainbow has drained to the plain body", torso.sharedMaterial.shader.name != "GO GHOST/Final Boss Rainbow");
        yield return Until(() => axe.Unarmed, 9f); yield return Seconds(.6f);
        Check("Line 2 leads to the dropped axe", axe.Unarmed && actor.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name == "DroppedAxeShaft" && r.enabled));
        yield return Until(() => game.CutsceneStep == 4, 2f);
        Check("The energy gun clicks empty", game.CutsceneText == "*click*" && !visual.AntiqueRevolver);
        yield return Until(() => game.CutsceneStep == 6, 10f); yield return Frames(3);
        Check("The antique revolver is drawn and two choices are offered", visual.AntiqueRevolver && game.CutsceneChoiceOpen
            && Exists("FinalBossChoice0") && Exists("FinalBossChoice1"));
        ScreenCapture.CaptureScreenshot(Output + "final-boss-transition-choice.png"); yield return Frames(3);
        Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b => b.name == "FinalBossChoice1").onClick.Invoke();
        yield return Frames(2);
        Check("The choice becomes the player's line", !game.CutsceneChoiceOpen && game.CutsceneSpeakerId == DuelGame.CutsceneSpeaker.Player
            && game.CutsceneText == DuelGame.PlayerTransitionLines[1]);
        yield return Until(() => view.EyesRed, 6f);
        Check("…What? and the red eyes", view.EyesRed && game.CutsceneStep == 7);
        yield return Until(() => game.Phase == DuelPhase.Prepare, 8f); yield return Seconds(1.5f);
        float midWidth = Camera.main.pixelRect.width;
        Check("The countdown widens the camera while the sealed sidebars slide away", game.Phase == DuelPhase.Prepare
            && midWidth > cameraBefore + 50 && midWidth < Screen.width - 10, $"before={cameraBefore:F0} mid={midWidth:F0}");
        ScreenCapture.CaptureScreenshot(Output + "final-boss-transition-countdown.png"); yield return Frames(3);
        yield return Until(() => game.Phase == DuelPhase.Fight && !game.FinalBossCutscene, 3f); yield return Frames(4);
        Check("Phase two begins full screen, unarmed, with input back", game.Phase == DuelPhase.Fight && actor.Health.PhaseIndex == 2
            && axe.PhaseTwo && axe.Unarmed && reader.GameplayInputEnabled && !Exists("BattleRules") && !Exists("BossRules")
            && Mathf.Abs(Camera.main.pixelRect.width - Screen.width) <= 1);
        float start = Mathf.Abs(actor.Position.x - game.Player.transform.position.x);
        yield return Seconds(1.2f);
        Check("The bare-handed placeholder closes in", Mathf.Abs(actor.Position.x - game.Player.transform.position.x) < start - .5f || Mathf.Abs(actor.Position.x - game.Player.transform.position.x) < 1.6f);
        ScreenCapture.CaptureScreenshot(Output + "final-boss-transition-phase-two.png"); yield return Frames(3);
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.BonusOffer, 1.5f);
        Check("Second pool defeat still opens the Bonus offer", game.Phase == DuelPhase.BonusOffer);
        game.MainMenu();
        Record("NOT TESTED: listening, Thomas's player recordings (absent; silent), hand play, Retry during the transition.");
    }
}
#endif
