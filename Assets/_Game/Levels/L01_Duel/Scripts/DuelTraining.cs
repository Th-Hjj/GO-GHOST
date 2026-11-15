using UnityEngine;

namespace GoGhost.Duel
{
    // A separate attempt in the existing scene; never a numbered wave.
    public sealed partial class DuelGame
    {
        float playerRespawnAt, enemyRespawnAt;
        Vector3 trainingEnemySpawn;

        public void StartConfiguredEncounter()
        {
            if (!TrainingPending || Paused || DeveloperToolsOpen || !DialogueAllowsPreparationNavigation || !ui.ConfigurationReady) return;
            CreateEncounter(true);
        }

        public void BackFromTraining()
        {
            if (Phase != DuelPhase.Training || !DialogueAllowsPreparationNavigation) return;
            if (GhostArena) { LeaveGhostArena(); return; }
            ClearEncounter();
            SetPhase(DuelPhase.Configure);
        }

        // Kept for existing scripted callers. Test Room itself offers only Back.
        public void ContinueFromTraining()
        {
            if (Phase != DuelPhase.Training || !DialogueAllowsPreparationNavigation) return;
            CreateEncounter(false);
        }

        void BindEncounterRules()
        {
            Player.GetComponent<OfficerVisual>()?.ClearConditionalGlow();
            Rules?.Dispose();
            Rules = new ConditionalController(Player, actors, ActivePlatforms, ArenaBounds, DraftLoadout);
            ruleCueAt = float.NegativeInfinity;
            Rules.RuleTriggered += PlayRuleCue;
            if (!input.ConditionalControlsActive) input.ConfigureConditionalControls(raw => Rules.Resolve(raw));
            Player.GetComponent<OfficerVisual>()?.ConfigureConditionalGlow(conditionalGlow, Rules.VisualFeedback);
        }

        float ruleCueAt;
        // One cue as a rule takes over, spaced so several rules starting together read as one.
        void PlayRuleCue(ControlOperation operation, int row)
        {
            if (encounterSounds == null || Time.time - ruleCueAt < .25f) return;
            ruleCueAt = Time.time;
            encounterSounds.Play(Sfx.RuleTrigger);
        }

        BossAgent CreateTrainingEnemy(bool respawning = false)
        {
            if (!respawning)
            {
                float spawnX = Mathf.Abs(Player.transform.position.x - gunnerSpawn.x) < 2
                    ? 2 * arenaBounds.center.x - gunnerSpawn.x : gunnerSpawn.x;
                trainingEnemySpawn = new Vector3(spawnX, gunnerSpawn.y);
                // GHOST's place: on the T, as he stood on the title.
                if (GhostArena) trainingEnemySpawn = new Vector3(arenaBounds.center.x + GhostOpponentX,
                    gunnerSpawn.y + PlatformTierRise * 3);
            }
            var instance = Instantiate(gunnerPrefab, trainingEnemySpawn, Quaternion.identity, encounter.transform);
            instance.name = "TrainingEnemy";
            var actor = instance.GetComponent<BossAgent>();
            actor.ConfigureTraining(Player, navigation);
            return actor;
        }

        void TickTrainingRespawn()
        {
            if (Paused || DeveloperToolsOpen || Player == null) return;
            if (Player.IsDead)
            {
                if (playerRespawnAt == 0) { playerRespawnAt = Time.time + 1.2f; if (!GhostArena) PlayMetrics.RecordTrainingDeath(); }
                // Losing to GHOST returns to the title.
                if (Time.time >= playerRespawnAt) { if (GhostArena) LeaveGhostArena(); else CreateEncounter(true); }
                return;
            }
            if (actors.Length == 0 || actors[0].IsAlive) return;
            if (enemyRespawnAt == 0) enemyRespawnAt = Time.time + 3f;
            if (Time.time < enemyRespawnAt) return;
            var previous = actors[0];
            foreach (var shot in encounter.GetComponentsInChildren<Projectile>())
                if (shot.Owner == previous.gameObject) shot.Retire();
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
            actors[0] = CreateTrainingEnemy(true);
            ConfigureDodgeBodies();
            Police = actors[0].Health;
            // Fresh source identity/occurrences and subscriptions, with existing player resources and physical commitments.
            BindEncounterRules();
            actors[0].StartCombat();
            enemyRespawnAt = 0;
            ui.Show();
        }

    }
}
