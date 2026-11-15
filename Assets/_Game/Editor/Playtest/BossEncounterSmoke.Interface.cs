#if UNITY_EDITOR
using System.Collections;
using System.IO;
using GoGhost.Duel;
using UnityEngine;

// Interface checks inside the flow check: the game menu on each kind of page, the routes out of a held result,
// the credits roll, and one capture of every page in the report folder's Pages directory for looking over afterwards.
public static partial class BossEncounterSmoke
{
    static readonly string[] MenuAlways = { "MenuResume", "MenuKeyBindings", "MenuSettings", "MenuMainMenu" };
    static bool holdPause;
    static int shots;

    // The capture is written at the end of this frame, so the caller gets the next one.
    static IEnumerator Shot(string name)
    {
        string folder = Path.Combine(reportDirectory, "Pages");
        Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, (++shots).ToString("00") + "-" + name + ".png"));
        yield return null;
    }

    static IEnumerator Settle(float seconds) => Observe(seconds, () => { });

    static IEnumerator CheckMenu(string where, bool encounter, bool viaButton = false)
    {
        float running = Time.timeScale;
        holdPause = true;
        if (viaButton) yield return Click("ConfigurationMenu"); else game.SetPause(true);
        Require(game.Paused && Time.timeScale == 0, "Menu did not open and hold: " + where);
        foreach (string name in MenuAlways)
            yield return WaitFor(() => AvailableButton(name) != null, "Menu on " + where + " is missing " + name);
        Require((AvailableButton("MenuConfiguration") != null) == encounter, "Wrong Configuration entry in the menu on " + where);
        yield return Settle(.4f);
        yield return Shot("menu-" + where);
        foreach (string page in new[] { "MenuKeyBindings", "MenuSettings" })
        {
            yield return Click(page);
            yield return Click("OverlayBack");
            yield return WaitFor(() => AvailableButton("MenuResume") != null && game.Paused, page + " did not return to the menu on " + where);
        }
        yield return Click("MenuResume");
        Require(!game.Paused && Time.timeScale == running, "Resume did not restore " + where);
        holdPause = false;
        Note("PASS menu on " + where + ": items, Key Bindings and Settings return, Resume.");
    }

    // Mission 1 only: a defeat is held, Retry and Configuration leave it, and the menu's Configuration leaves a fight.
    static IEnumerator CheckDefeatRoutes()
    {
        game.Player.IsInvulnerable = false;
        game.Player.TakeDamage(100000, game.Bosses[0].gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.Failed, "Missing Mission 1 defeat.");
        Require(Time.timeScale == 0, "Defeat did not hold the fight.");
        yield return Settle(1f);
        yield return Shot("defeat");
        yield return CheckMenu("held result", false);
        yield return Click("Retry");
        Require(game.Phase == DuelPhase.Prepare && Time.timeScale == 1, "Retry was not immediate.");
        yield return WaitFor(() => game.Phase == DuelPhase.Fight, "Retry countdown.");
        game.Player.TakeDamage(100000, game.Bosses[0].gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.Failed, "Missing second defeat.");
        yield return Click("Reconfigure");
        yield return WaitFor(() => game.Phase == DuelPhase.Configure && game.Player == null && Time.timeScale == 1, "Configuration from a defeat.");
        yield return EnterFight();

        holdPause = true;
        game.SetPause(true);
        yield return Click("MenuConfiguration");
        yield return WaitFor(() => game.Phase == DuelPhase.Configure && game.Player == null && !game.Paused && Time.timeScale == 1,
            "Menu Configuration did not leave the fight cleanly.");
        holdPause = false;
        yield return EnterFight();
        game.Player.IsInvulnerable = true;
        Note("PASS Mission 1 defeat: hold, Retry, Configuration, and Configuration from the menu.");
    }

    static IEnumerator EnterFight()
    {
        yield return Click("StartMission");
        yield return WaitFor(() => game.Phase == DuelPhase.Fight, "Combat did not start.", 12);
    }

    // The save is written on the way out and Continue brings the same preparation back.
    static IEnumerator CheckMainMenuAndContinue(int wave)
    {
        holdPause = true;
        game.SetPause(true);
        yield return Click("MenuMainMenu");
        holdPause = false;
        Require(game.Phase == DuelPhase.Menu && !game.Paused && Time.timeScale == 1, "Menu Main Menu did not reach the title.");
        yield return Settle(1f);
        yield return Shot("title-with-save");
        yield return Click("Continue");
        yield return WaitFor(() => game.Phase == DuelPhase.Configure && game.Wave == wave, "Continue did not return to Mission " + wave);
        Note("PASS Main Menu from the menu and Continue into Mission " + wave + ".");
    }

    static IEnumerator CheckCredits()
    {
        var ui = game.GetComponent<DuelUI>();
        Require(Field<bool>(ui, "creditsRoll"), "Finishing the game did not start the credits.");
        yield return WaitFor(() => Field<GameObject>(ui, "creditsBlocker") != null, "The credits roll was never built.");
        yield return Settle(4f);
        yield return Shot("credits");
        yield return WaitFor(() => !Field<bool>(ui, "creditsRoll") && Field<GameObject>(ui, "creditsBlocker") == null,
            "The credits never ended.", 30);
        yield return WaitFor(() => AvailableButton("New Game") != null, "The title did not arrive after the credits.");
        yield return Settle(1.5f);
        yield return Shot("title-after-credits");
        Note("PASS credits after the ending: roll, end, title.");
    }
}
#endif
