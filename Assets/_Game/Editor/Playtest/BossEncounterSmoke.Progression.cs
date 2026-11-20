#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using GoGhost.Duel;
using TMPro;
using UnityEngine;

public static partial class BossEncounterSmoke
{
    static readonly string[] ProfileFields = { "Codename", "Threat", "Weapon", "Location" };

    static IEnumerator GameFlow()
    {
        // This Editor-only scope also keeps GameProgress.Cleared in memory.
        testStorage = GameSave.BeginTestStorage(Path.Combine(reportDirectory, "Save"));
        previousNoticeSeen = GameSettings.NoticeSeen;
        GameSettings.NoticeSeen = true; // Exercise the title without writing the real first-launch preference.
        GameProgress.Cleared = false;
        yield return CheckSaveRecovery();
        if (saveRecoveryOnly) yield break;
        yield return Click("New Game");
        yield return Click("ConfirmNewGame");
        yield return WaitFor(() => AvailableButton("ChooseNormal") != null, "Missing standalone difficulty page.");
        Require(Field<GameDifficulty>(game.GetComponent<DuelUI>(), "newRunDifficulty") == GameDifficulty.Normal, "New Game did not default to Normal.");
        yield return Click("TutorialOff");
        yield return Click("TutorialOn");
        Require(AvailableButton("TutorialOn").GetComponent<SlabButton>().Fill == OverridePalette.Text, "Tutorial On was not selected.");
        yield return Click("TutorialOff");
        yield return Click("ChooseHard");
        yield return Click("OverlayBack");
        Require(GameSave.Read(out _).difficulty == GameDifficulty.Normal, "Backing out of selection changed the existing run.");
        yield return Click("New Game");
        yield return Click("ConfirmNewGame");
        yield return WaitFor(() => Field<RectTransform>(game.GetComponent<DuelUI>(), "saveNoticePanel") == null,
            "Save recovery notice did not finish.", 13);
        yield return Settle(.8f);
        Require(AvailableButton("TutorialOn").GetComponent<SlabButton>().Fill == OverridePalette.Text, "New Game did not default to Tutorial On.");
        yield return Shot("difficulty");
        yield return Click("TutorialOff");
        yield return Click("ChooseEasy");
        Require(AvailableButton("TutorialOff").GetComponent<SlabButton>().Fill == OverridePalette.Text, "Changing difficulty lost Tutorial Off.");
        yield return Click("ConfirmDifficulty");
        Require(game.Phase == DuelPhase.CharacterCreation && game.Difficulty == GameDifficulty.Easy, "Difficulty selection did not enter creation on Easy.");
        Require(!game.TutorialWanted, "Tutorial Off did not carry into character creation.");
        PlayMetrics.MarkTestData();
        game.Appearance.Name = "Game Flow Smoke";
        yield return Settle(1f);
        Require(AvailableButton("TutorialOff") == null && AvailableButton("TutorialOn") == null, "Character creation still offers the tutorial choice.");
        yield return Shot("creation");
        yield return CheckMenu("creation", false);
        game.CompleteCharacterCreation();
        Require(game.Phase == DuelPhase.Configure && game.DraftLoadout.TotalRules == 0, "Tutorial-off creation did not open empty configuration.");
        yield return Settle(1f);
        yield return Shot("overview");
        yield return CheckMenu("overview", false, true);
        yield return Click("AddRule");
        yield return Settle(1f);
        yield return Shot("rule-editor");
        yield return CheckMenu("rule editor", false);
        yield return Click("CancelRuleEdit");
        yield return WaitFor(() => AvailableButton("StartMission") != null, "Cancel did not return to the overview.");
        string metricsPath = PlayMetrics.JsonPath;
        Require(PlayMetrics.Run.initialDifficulty == "Easy" && PlayMetrics.Run.tutorialSelection == "Off", "New Game telemetry lost the selected settings.");
        int equippedRules = game.DraftLoadout.TotalRules;

        for (int wave = 1; wave <= DuelGame.FinalBossWave; wave++)
        {
            Require(game.Wave == wave && game.TrainingPending, "Missing preparation for Mission " + wave);
            if (wave == 1)
            {
                game.EditLoadout(loadout => Require(loadout.TryAddRule(ControlOperation.Jump, ConditionalLoadout.CreateTutorialJumpRule()), "Missing victory sidebar fixture."));
                Require(PlayMetrics.Run.firstCustomRuleAtSeconds < 0 && PlayMetrics.Run.rulesAdded == 0, "Example counted as a custom rule.");
                game.EditLoadout(loadout => loadout.Operations[(int)ControlOperation.Jump].Rules.Clear(), "remove");
                game.EditLoadout(loadout => Require(loadout.TryAddRule(ControlOperation.Jump, ConditionalLoadout.CreateTutorialJumpRule()), "Missing custom rule fixture."), "add");
                Require(PlayMetrics.Run.firstCustomRuleAtSeconds >= PlayMetrics.Run.firstRuleAtSeconds && PlayMetrics.Run.rulesAdded == 1, "First confirmed custom rule missing.");
            }
            if (wave == 2)
                game.EditLoadout(loadout => loadout.Operations[(int)ControlOperation.Jump].Rules.Clear());
            yield return CheckProfile(wave);
            yield return Click("TestLoadout");
            // Entering and leaving the Test Room happen once the wipe has covered the screen.
            yield return WaitFor(() => game.Phase == DuelPhase.Training && game.Bosses.Count == 1 && game.Bosses[0].IsTrainingEnemy, "Missing Test Room.");
            if (wave <= 2) yield return CheckDifficultyInTestRoom(wave == 1);
            if (wave == 1) { yield return Settle(.8f); yield return Shot("test-room"); yield return CheckMenu("Test Room", true); }
            yield return Click("← Back");
            yield return WaitFor(() => game.Phase == DuelPhase.Configure && game.Player == null, "Test Room did not cleanly return to preparation.");
            if (wave == 2) yield return CheckMainMenuAndContinue(wave);
            yield return Click("StartMission");
            yield return Settle(1.2f);
            yield return Shot("target-card-" + wave);
            yield return WaitFor(() => game.Phase != DuelPhase.MissionEntry, "Mission entry did not finish.");
            Require(game.Phase == DuelPhase.Prepare && game.PendingDialogue == null, "Missing normal countdown.");
            if (wave == 1) yield return CheckMenu("countdown", true);
            yield return WaitFor(() => game.Phase == DuelPhase.Fight, "Combat did not start.");
            game.Player.IsInvulnerable = true;
            yield return Settle(.4f);
            yield return Shot("fight-" + wave);
            if (wave == 1) { yield return CheckMenu("fight", true); yield return CheckDefeatRoutes(); }
            Note("PASS Mission " + wave + ": preparation, Test Room return, Start Mission, countdown.");
            if (wave == DuelGame.FinalBossWave) break;

            yield return CheckMissionVictoryFraming(wave == 1);
            if (wave == 1)
            {
                yield return WaitFor(() => game.Phase == DuelPhase.Between && game.PendingDialogue == null, "Mission 1 result.", 25);
                Require(game.Bosses[0].Motor.Grounded && Time.timeScale == 0, "Victory froze before the Boss landed.");
                yield return Settle(1f);
                yield return Shot("cleared");
                yield return Click("ContinueToWaveTwo");
                yield return WaitFor(() => game.Wave == 2 && game.Phase == DuelPhase.Configure, "Mission 2 preparation.");
            }
            else
            {
                yield return WaitFor(() => game.Wave == DuelGame.FinalBossWave && game.Phase == DuelPhase.Configure, "Mission 3 preparation.", 25);
                Require(PlayMetrics.JsonPath == metricsPath && PlayMetrics.RunActive && PlayMetrics.Run.levelCleared && !PlayMetrics.Run.gameCleared,
                    "Mission 2 did not retain its milestone in the original active record.");
            }
        }

        var finalBoss = game.Bosses[0];
        int phaseOneEndHealth = Math.Max(1, game.Player.MaxHealth - 37);
        game.Player.SetRuntimeHealth(game.Player.MaxHealth, phaseOneEndHealth);
        yield return CheckInvasionGate(finalBoss, 660, 1);
        yield return CheckInvasionGate(finalBoss, 330, 2);
        Require(game.DraftLoadout.TotalRules == equippedRules, "Fight-only invasions changed the equipped loadout.");
        autoAcknowledge = false;
        finalBoss.Health.TakeDamage(100000, game.Player.gameObject);
        yield return WaitFor(() => game.FinalBossCutscene, "Missing Final Boss transition.", 15);
        Require(!game.CanChangeDifficulty, "The phase transition unlocked difficulty.");
        foreach (var entry in VoiceStudioCatalog.Current.BossTransition)
        {
            yield return WaitFor(() => game.CutsceneText == entry.text && game.PendingDialogue == entry.text,
                "Missing transition line: " + entry.key, 25);
            yield return WaitFor(() => game.DialogueReady, "Transition line never became advanceable: " + entry.key, 25);
            game.AcknowledgeDialogue();
            yield return null;
        }
        autoAcknowledge = true;
        yield return WaitFor(() => finalBoss.Axe.PhaseTwo && !game.FinalBossCutscene && game.Phase == DuelPhase.Fight,
            "Phase two did not finish its transition and countdown.", 40);
        Require(game.Player.CurrentHealth == phaseOneEndHealth && game.Rules.OrderedRows.Count == 0,
            "Phase two changed saved entry health or retained rule assistance.");
        Note("PASS Final Boss: both health gates, two completed invasions, dialogue, EMP transition, empty runtime rules.");

        game.Player.IsInvulnerable = false;
        game.Player.TakeDamage(100000, finalBoss.gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.Failed && game.CanRetryFinalBossPhaseTwo, "Missing phase-two defeat/retry.");
        var lost = PlayMetrics.History[PlayMetrics.History.Count - 1];
        Require(lost.wave == 3 && lost.result == "died" && lost.finalBossStartPhase == 1 && lost.finalBossPhase == 2
            && lost.phaseOneExitPlayerHealth == phaseOneEndHealth && lost.invadedRules.Count == 2
            && lost.swordHealth == -1 && lost.finalBossMaxHealth == finalBoss.Health.MaxHealth && lost.phaseOneCombatSeconds > 0,
            "Missing Final Boss phase, invasions or health in the defeated attempt.");
        yield return Click("RetryPhaseTwo");
        Require(game.Phase == DuelPhase.Prepare && game.Bosses[0].Axe.PhaseTwo && !game.FinalBossCutscene, "Phase-two retry replayed phase one or its transition.");
        Require(!game.CanChangeDifficulty && game.Difficulty == GameDifficulty.Hard, "Phase-two retry lost its difficulty lock or selection.");
        Require(game.Player.CurrentHealth == phaseOneEndHealth, "Phase-two retry did not restore phase-one end health.");
        Require(game.Bosses[0].Health.CurrentHealth == game.Bosses[0].Health.MaxHealth, "Phase-two retry did not restore the full Boss pool.");
        Require(game.InvadedRuleCount == 0 && game.Rules.OrderedRows.Count == 0 && game.DraftLoadout.TotalRules == equippedRules,
            "Phase-two retry retained invasion rules or changed equipped rules.");
        game.Player.IsInvulnerable = true;
        yield return WaitFor(() => game.Phase == DuelPhase.Fight, "Phase-two retry countdown.");
        Note("PASS phase-two death and real Retry Phase 2 callback.");

        finalBoss = game.Bosses[0];
        // Remove fixture immunity first, so the following check proves victory protection.
        game.Player.IsInvulnerable = false;
        finalBoss.Health.TakeDamage(100000, game.Player.gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.FinalVictoryHold, "Missing Boss defeat hold.");
        int finalHealth = game.Player.CurrentHealth;
        game.Player.TakeDamage(100000, finalBoss.gameObject);
        Require(game.Player.CurrentHealth == finalHealth && !finalBoss.Running, "Damage continued during victory.");
        yield return WaitFor(() => game.Phase == DuelPhase.Complete, "Missing final completion page.");
        var texts = CurrentPage().GetComponentsInChildren<TMP_Text>();
        foreach (var line in new[] { "You've", "Finished", "The Game!" })
            Require(Array.Exists(texts, text => text.text == line), "Missing ending text: " + line);
        Require(!GameSave.Exists && GameProgress.Cleared, "Victory did not remove the test save and set the isolated cleared flag.");
        var exported = JsonUtility.FromJson<PlayMetrics.RunData>(File.ReadAllText(metricsPath));
        var won = exported.attempts[exported.attempts.Count - 1];
        Require(exported.schemaVersion == 5 && exported.finished && exported.endReason == "complete" && exported.gameCleared
            && exported.initialDifficulty == "Easy" && exported.latestDifficulty == "Hard" && exported.tutorialSelection == "Off"
            && exported.difficultyChanges.Exists(change => change.wave == 1 && change.from == "Easy" && change.to == "Hard")
            && exported.difficultyChanges.Exists(change => change.wave == 2 && change.location == "Menu" && change.to == "Easy")
            && exported.difficultyChanges.Exists(change => change.wave == 2 && change.location == "Menu" && change.to == "Hard")
            && exported.guidedTestRoomVisits == 0 && exported.freeTestRoomVisits == 3
            && exported.levelCleared && exported.deathsByWave[3] == 1 && exported.gameClearSeconds > exported.levelClearSeconds
            && exported.activeAttempt.result == "none" && won.wave == 3 && won.result == "won" && won.finalBossHealth == 0
            && won.finalBossStartPhase == 2 && won.finalBossPhase == 2 && won.difficulty == "Hard"
            && won.phaseOneCombatSeconds == 0 && won.phaseOneExitPlayerHealth == -1 && won.invadedRules.Count == 0,
            "Export did not preserve the W2 milestone, Final Boss defeat, phase-two retry and final clear.");
        yield return Settle(1.5f);
        yield return Shot("finished");
        yield return Click("Main Menu");
        Require(game.Phase == DuelPhase.Menu && game.Player == null && !game.Paused && !AudioListener.pause && Time.timeScale == 1,
            "Main Menu did not clear combat/global pause.");
        yield return CheckCredits();
        Note("PASS final victory protection, ending, save removal, cleared flag, full-run analytics, Main Menu cleanup.");
        Note("NOT TESTED: complete tutorial, rule editing, physical input, the pause key, Reduce Flashing, balance, standalone build, other hardware.");
    }

    static IEnumerator CheckMissionVictoryFraming(bool withRule)
    {
        var camera = Camera.main;
        var viewport = camera.pixelRect;
        float size = camera.orthographicSize;
        Require((viewport.xMin > 0) == withRule, "Wrong combat viewport before the winning hit.");
        void CheckFrame()
        {
            var panel = CurrentPage().Find("BattleRules");
            Require((panel != null && panel.gameObject.activeInHierarchy) == withRule &&
                camera.pixelRect == viewport && Mathf.Approximately(camera.orthographicSize, size),
                $"Combat framing changed in {game.Phase}: Rules={panel != null}, viewport={camera.pixelRect} (was {viewport}), size={camera.orthographicSize} (was {size}).");
            if (withRule)
                Require(panel.Find("BattleRuleScroll/Viewport/RuleList").childCount == 1, "The equipped card disappeared.");
        }
        CheckFrame();
        game.Bosses[0].Health.TakeDamage(100000, game.Player.gameObject);
        yield return WaitFor(() => game.Phase == DuelPhase.BossVictoryHold, "Missing Boss defeat presentation.");
        CheckFrame();
        Require(Mathf.Approximately(Time.timeScale, .4f), "Victory did not start with slow motion.");
        if (withRule) yield return Shot("boss-defeat-slow-motion");
        yield return WaitFor(() => { CheckFrame(); return game.Phase == DuelPhase.Between; }, "Missing held result.");
        yield return Observe(.85f, CheckFrame);
        Note($"PASS Mission {game.Wave} winning hit through held result: Rules={withRule}, viewport={viewport}, size={size}.");
    }

    static IEnumerator CheckProfile(int wave)
    {
        var entries = Array.FindAll(VoiceStudioCatalog.Current.lines, line => line.profileMission == wave);
        Require(entries.Length == (wave == DuelGame.FinalBossWave ? 6 : 5), "Unexpected dossier voice count for Mission " + wave);
        foreach (var entry in entries)
            Require(string.IsNullOrEmpty(entry.profileField) || Array.IndexOf(ProfileFields, entry.profileField) >= 0,
                "Unexpected dossier field: " + entry.profileField);
        var voice = Field<HandlerVoice>(game, "preparationVoice");
        var source = Field<AudioSource>(voice, "speaker");
        yield return Click("TargetProfile");
        Require(game.Phase == DuelPhase.TargetProfile, "Target card did not open Mission " + wave);
        CheckProfileRows(entries);
        foreach (var entry in entries)
        {
            var clip = Resources.Load<AudioClip>(entry.key);
            Require(clip != null, "Missing profile clip: " + entry.key);
            yield return WaitFor(() => source.clip == clip && voice.Audible, "Automatic profile line did not play: " + entry.key, clip.length + 8);
            yield return WaitFor(() => source.clip != clip || !voice.Speaking, "Profile clip did not finish: " + entry.key, clip.length + 8);
        }
        yield return WaitFor(() => Property<int>(game, "ProfileReading") == -1 && !voice.Speaking, "Profile reading did not end.");
        yield return Click("ProfileBack");
        Require(game.Phase == DuelPhase.Configure, "Profile Back did not return to preparation.");

        // Controlled first-visit fixture: check cancellation while speech is active, separately from the full reading above.
        Field<bool[]>(game, "profileOpened")[wave] = false;
        yield return Click("TargetProfile");
        CheckProfileRows(entries);
        yield return WaitFor(() => voice.Audible, "Interrupted-visit fixture never started speaking.");
        yield return Click("ProfileBack");
        Require(game.Phase == DuelPhase.Configure && !voice.Speaking, "Back did not cancel profile speech.");
        yield return Click("TargetProfile");
        CheckProfileRows(entries);
        yield return Observe(1.1f, () =>
        {
            Require(game.Phase == DuelPhase.TargetProfile && !voice.Speaking && Property<int>(game, "ProfileReading") == -1,
                "Revisited interrupted profile replayed speech.");
        });
        if (wave == 1) { yield return Shot("target-profile"); yield return CheckMenu("Target Profile", false); }
        yield return Click("ProfileBack");
        Note("PASS Mission " + wave + " dossier: exactly four fields, automatic ordered clips, Back during speech, silent revisit.");
    }

    static RectTransform CurrentPage() => Field<RectTransform>(game.GetComponent<DuelUI>(), "page");

    static void CheckProfileRows(VoiceStudioLine[] entries)
    {
        var root = CurrentPage();
        Require(root != null, "Missing current profile page.");
        var rows = Array.FindAll(root.GetComponentsInChildren<RectTransform>(true), rect => rect.name.StartsWith("Row ", StringComparison.Ordinal));
        Require(rows.Length == ProfileFields.Length, "Dossier does not contain exactly four rows.");
        foreach (var field in ProfileFields)
        {
            var row = Array.Find(rows, candidate => candidate.name == "Row " + field);
            Require(row != null, "Missing dossier row: " + field);
            var expected = Array.FindLast(entries, entry => entry.profileField == field)?.profileValue;
            Require(!string.IsNullOrWhiteSpace(expected) && expected != "[placeholder]", "Missing catalog value: " + field);
            var texts = row.GetComponentsInChildren<TMP_Text>(true);
            Require(Array.Exists(texts, text => text.text == field) && Array.Exists(texts, text => text.text == expected),
                "Missing visible field/value: " + field);
        }
    }

    static IEnumerator CheckSaveRecovery()
    {
        game.NewGame(); PlayMetrics.MarkTestData(); game.TutorialWanted = false;
        game.Appearance.Name = "Save Recovery Smoke";
        game.CompleteCharacterCreation();
        byte[] drawing = game.Appearance.ExportPng();
        game.MainMenu();
        string folder = Path.Combine(reportDirectory, "Save"), path = Path.Combine(folder, "save.json");
        var legacy = new SaveData { version = 1, name = "Legacy", wave = 1, tutorialDone = true,
            profiles = new bool[4], operations = new ConditionalLoadout().Operations };
        var rules = legacy.operations[(int)ControlOperation.Jump].Rules;
        for (int i = 0; i < 4; i++)
        {
            var rule = ConditionalLoadout.CreateTutorialJumpRule(); rule.DisplayOrder = i; rules.Add(rule);
        }
        rules.Add(new ConditionalRule { Condition = ConditionIds.GroundState, GroundState = GroundStateOption.Grounded,
            Action = ActionIds.Jump, DisplayOrder = 4 });
        legacy.runId = "legacy-metrics";
        string historicalPath = Path.Combine(PlayMetrics.DirectoryPath, legacy.runId + ".json");
        const string historical = "{\"schemaVersion\":4,\"levelCleared\":true,\"endReason\":\"complete\"}";
        File.WriteAllText(historicalPath, historical);
        var legacyJson = Newtonsoft.Json.Linq.JObject.Parse(JsonUtility.ToJson(legacy));
        legacyJson.Remove("difficulty");
        File.WriteAllText(path, legacyJson.ToString());
        File.WriteAllBytes(Path.Combine(folder, "face.png"), drawing);
        game.ContinueGame();
        Require(game.Difficulty == GameDifficulty.Normal, "Legacy save without difficulty did not default to Normal.");
        Require(PlayMetrics.RunActive && PlayMetrics.Run.previousRunId == legacy.runId && PlayMetrics.JsonPath != historicalPath
            && File.ReadAllText(historicalPath) == historical, "Continue overwrote historical W2 telemetry or lost the link.");
        Require(game.Phase == DuelPhase.Configure && game.DraftLoadout.TotalRules == 5
            && game.SaveNotice.Contains("reselected"), "Continue discarded historical rules or omitted repair feedback.");
        game.StartFight(); Require(game.Phase == DuelPhase.Configure, "Invalid historical rules entered combat.");
        yield return Observe(.8f, () => Require(game.DraftLoadout.TotalRules == 5, "Rules disappeared during preparation."));
        ScreenCapture.CaptureScreenshot(Path.Combine(reportDirectory, "save-reselection.png"));
        yield return null;
        game.EditLoadout(loadout => loadout.OrderedRules()[4].GroundState = GroundStateOption.OnGround);
        Require(ConditionalCatalog.IsValid(game.DraftLoadout) && game.SaveNotice == null
            && GameSave.Read(out _) != null, "Reselecting one old parameter did not resolve the save notice.");
        byte[] committed = File.ReadAllBytes(path);
        string temp = Path.Combine(folder, "save.json.tmp");
        Directory.CreateDirectory(temp);
        game.EditLoadout(loadout => { });
        Require(game.SaveNotice != null && !Field<bool>(game, "faceSaved")
            && Convert.ToBase64String(committed) == Convert.ToBase64String(File.ReadAllBytes(path)),
            "Failed write was silent, lost the drawing retry, or changed the committed save.");
        yield return Observe(.3f, () => Require(game.SaveNotice != null, "Save failure disappeared before retry."));
        ScreenCapture.CaptureScreenshot(Path.Combine(reportDirectory, "save-failure.png"));
        yield return null;
        Directory.Delete(temp); // Exact empty fixture directory.
        game.EditLoadout(loadout => { });
        Require(game.SaveNotice == null && Field<bool>(game, "faceSaved") && GameSave.Read(out _) != null,
            "A successful retry did not restore saving and clear failure feedback.");
        game.MainMenu(); File.WriteAllText(path, "{truncated");
        game.ContinueGame();
        Require(game.Phase == DuelPhase.Configure && game.DraftLoadout.TotalRules == 5
            && game.SaveNotice == "Restored your previous save.", "Continue did not recover the backup with feedback.");
        game.MainMenu();
        Note("PASS save recovery: Continue retained all five legacy rules; one-parameter repair; failed write kept slot and retried drawing; corrupt primary recovered backup.");
    }

    static IEnumerator CheckInvasionGate(BossAgent boss, int threshold, int count)
    {
        boss.Health.TakeDamage(100000, game.Player.gameObject);
        Require(boss.Health.CurrentHealth == threshold, "Damage crossed the " + threshold + " HP invasion gate.");
        boss.Health.TakeDamage(100000, game.Player.gameObject);
        Require(boss.Health.CurrentHealth == threshold, "Repeated damage crossed a closed invasion gate.");
        yield return WaitFor(() => boss.Axe.Invasions == count && !boss.Axe.Invading,
            "Invasion " + count + " did not finish its reading and recovery.", 40);
        Require(game.InvadedRuleCount == count && boss.Health.CurrentHealth == threshold,
            "Incorrect invasion count or health after reading.");
        Note("PASS invasion " + count + ": gate=" + threshold + ", reading and recovery completed.");
    }
}
#endif
