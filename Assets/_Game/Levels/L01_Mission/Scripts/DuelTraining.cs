using UnityEngine;

namespace GoGhost.Duel
{
    // A separate attempt in the existing scene; never a numbered wave.
    public sealed partial class DuelGame
    {
        float playerRespawnAt, enemyRespawnAt;

        public void StartConfiguredEncounter()
        {
            if (Phase != DuelPhase.Configure || Paused || !ui.ConfigurationReady) return;
            CreateEncounter(TrainingPending);
        }

        public void BackFromTraining()
        {
            if (Phase != DuelPhase.Training) return;
            ClearEncounter();
            SetPhase(DuelPhase.Configure);
        }

        public void ContinueFromTraining()
        {
            if (Phase != DuelPhase.Training) return;
            ClearEncounter();
            FirstConfiguration = false;
            Wave = 1;
            SetPhase(DuelPhase.Configure);
        }

        void BindEncounterRules()
        {
            Player.GetComponent<OfficerVisual>()?.ClearConditionalGlow();
            Rules?.Dispose();
            Rules = new ConditionalController(Player, actors, ActivePlatforms, ArenaBounds, DraftLoadout);
            if (!input.ConditionalControlsActive) input.ConfigureConditionalControls(raw => Rules.Resolve(raw));
            Player.GetComponent<OfficerVisual>()?.ConfigureConditionalGlow(conditionalGlow, Rules.VisualFeedback);
        }

        BossAgent CreateTrainingEnemy()
        {
            float spawnX = Mathf.Abs(Player.transform.position.x - gunnerSpawn.x) < 2
                ? 2 * arenaBounds.center.x - gunnerSpawn.x : gunnerSpawn.x;
            var instance = Instantiate(gunnerPrefab, new Vector3(spawnX, gunnerSpawn.y), Quaternion.identity, encounter.transform);
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
                if (playerRespawnAt == 0) playerRespawnAt = Time.time + 1.2f;
                if (Time.time >= playerRespawnAt) CreateEncounter(true);
                return;
            }
            if (actors.Length == 0 || actors[0].IsAlive) return;
            if (enemyRespawnAt == 0) enemyRespawnAt = Time.time + 1.2f;
            if (Time.time < enemyRespawnAt) return;
            var previous = actors[0];
            foreach (var shot in encounter.GetComponentsInChildren<Projectile>())
                if (shot.Owner == previous.gameObject) shot.Retire();
            previous.gameObject.SetActive(false);
            Destroy(previous.gameObject);
            actors[0] = CreateTrainingEnemy();
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
