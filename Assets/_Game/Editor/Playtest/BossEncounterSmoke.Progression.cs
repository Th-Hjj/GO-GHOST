#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using GoGhost.Duel;
using UnityEditor;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    [MenuItem("GO GHOST/Playtest/Final Boss and Bonus Progression")]
    public static void RunFinalBossProgression() => Start("final-boss-progression", false);

    static IEnumerator FinalBossProgression()
    {
        yield return AlphaFresh(2, draft => V2Rule(draft, V2Ground("F03")), false);
        PlayMetrics.MarkTestData();
        var appearance = game.Appearance;
        string rules = JsonUtility.ToJson(game.DraftLoadout);
        string path = PlayMetrics.JsonPath;
        game.Bosses[0].Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.Configure, 1);
        Check("Wave 2 opens Wave 3 configuration and closes its metrics",
            game.Wave == DuelGame.FinalBossWave && game.TrainingPending && !PlayMetrics.RunActive
            && PlayMetrics.Run.levelCleared && PlayMetrics.Run.endReason == "complete");
        string closedRecord = File.ReadAllText(path);
        ClickConfig("StartFight", "Start Testing");
        Check("Wave 3 testing uses the dummy and one practice platform",
            game.Phase == DuelPhase.Training && game.Bosses.Single().IsTrainingEnemy && LayoutSupports().Length == 2);
        ClickConfig("← Back");
        Check("Back reopens the same character and rules", game.Phase == DuelPhase.Configure
            && ReferenceEquals(appearance, game.Appearance) && JsonUtility.ToJson(game.DraftLoadout) == rules);
        ClickConfig("StartFight", "Start Testing");
        ClickConfig("Enter Wave 3");
        var actor = game.Bosses.Single();
        Check("Enter Wave 3 stages Walker's actor on five platforms",
            game.Phase == DuelPhase.FinalBoss && actor.IsFinalBoss && actor.Health.MaxHealth == 1400
            && LayoutSupports().Length == 6 && !actor.Running && game.FightClock == 0);
        var speaker = Private<AudioSource>(game.GetComponent<HandlerVoice>(), "speaker");
        var intro = VoiceStudioCatalog.Current.BossIntro;
        for (int i = 0; i < intro.Length; i++)
        {
            Check("Intro page and recording " + (i + 1), game.FinalBossDialogue == intro[i].text
                && speaker.clip == Resources.Load<AudioClip>(intro[i].key) && speaker.clip != null);
            ClickConfig("FinalBossNext");
        }
        Check("Last line starts a full three-second preparation with actors disabled",
            game.Phase == DuelPhase.Prepare && game.PreparationRemaining == DuelGame.PreparationSeconds && !actor.Running);
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        Check("Same actor starts combat after countdown", actor.Running && game.Bosses.Single() == actor);
        game.Player.TakeDamage(100000, null);
        yield return Until(() => game.Phase == DuelPhase.Failed, 1);
        ClickConfig("Retry");
        Check("Retry creates a fresh Wave 3 attempt", game.Phase == DuelPhase.FinalBoss && game.Bosses.Single() != actor);
        for (int i = 0; i < intro.Length; i++) ClickConfig("FinalBossNext");
        yield return Until(() => game.Phase == DuelPhase.Fight, 4.2f);
        game.Player.IsInvulnerable = true;
        actor = game.Bosses.Single();
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => actor.Health.PhaseIndex == 2, 3.5f);
        Check("Second pool stays in combat with saved rules preserved", game.Phase == DuelPhase.Fight
            && actor.Health.PhaseIndex == 2 && actor.Health.MaxHealth == 1000
            && game.Rules.RuleCount(ControlOperation.Fire) == 0 && JsonUtility.ToJson(game.DraftLoadout) == rules);
        actor.Health.TakeDamage(100000, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.BonusOffer, 1);
        Check("Victory opens the voiced Bonus offer", game.Phase == DuelPhase.BonusOffer
            && speaker.clip == Resources.Load<AudioClip>(VoiceStudioCatalog.Current.BonusOffer[0].key));
        while (!game.FinalBossChoiceReady) ClickConfig("FinalBossNext");
        ClickConfig("BonusYes");
        Check("Bonus reopens configuration with saved rules", game.Wave == DuelGame.BonusWave
            && game.TrainingPending && ReferenceEquals(appearance, game.Appearance) && JsonUtility.ToJson(game.DraftLoadout) == rules);
        ClickConfig("StartFight", "Start Testing");
        ClickConfig("Enter Bonus Wave");
        Check("Optional Bonus builds both opponents and leaves the W2 record byte-identical",
            game.Phase == DuelPhase.Prepare && game.Bosses.Count == 2 && File.ReadAllText(path) == closedRecord);
        game.MainMenu();
    }
}
#endif
