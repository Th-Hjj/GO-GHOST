using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelGame
    {
        public const int FinalBossWave = 3, BonusWave = 4;
        public string WaveLabel => Wave == BonusWave ? "BONUS WAVE" : "WAVE " + Wave;
        static readonly string[] FinalBossLines =
        {
            "Hi, I'm the final boss.",
            "I'm glad that you made it here.",
            "The thing is, you won! Congrats!",
            "Turns out we didn't have time to present a final boss fight at the moment.",
            "But... If you enjoyed the game.",
            "There is a bonus challenge.",
            "Do you want to enter the bonus wave?"
        };
        public int FinalBossLine { get; private set; }
        public string FinalBossDialogue => FinalBossLines[FinalBossLine];
        public bool FinalBossChoiceReady => FinalBossLine == FinalBossLines.Length - 1;
        public Enemy FinalBossHealth { get; private set; }
        Material finalBossMaterial;

        public void MeetFinalBoss()
        {
            if (Phase != DuelPhase.FinalBossReady || DeveloperToolsOpen) return;
            CreateFinalBossPage();
        }

        void CreateFinalBossPage()
        {
            ClearEncounter();
            PlayMetrics.EndRun("final_boss_visit");
            Wave = FinalBossWave;
            FirstConfiguration = developerTrainingSelected = false;
            FinalBossLine = 0;
            FightClock = 0;
            ApplyPlatformLayout(false);
            encounter = new GameObject("Final Boss Visit");
            encounter.transform.SetParent(transform, false);
            var boss = new GameObject("Final Boss");
            boss.transform.SetParent(encounter.transform, false);
            FinalBossHealth = boss.AddComponent<Enemy>();
            FinalBossHealth.ConfigureFixedSingleLife(9999);
            FinalBossHealth.StopCombat();
            boss.GetComponent<Rigidbody2D>().simulated = false;
            var view = Instantiate(swordsmanPrefab.GetComponentInChildren<BossSilhouetteView>(), boss.transform);
            view.transform.localPosition = Vector3.zero;
            view.transform.localScale = Vector3.one;
            view.ConfigurePlaceholder();

            var renderers = view.GetComponentsInChildren<SpriteRenderer>();
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var part in renderers)
            {
                if (!part.enabled || part.sprite == null) continue;
                if (!hasBounds) { bounds = part.bounds; hasBounds = true; }
                else bounds.Encapsulate(part.bounds);
            }
            // Stand the unchanged combat silhouette on the shared floor.
            float floorY = platforms[0].bounds.max.y;
            boss.transform.position = new Vector3(arenaBounds.center.x, floorY - bounds.min.y, 0);
            finalBossMaterial = new Material(Resources.Load<Shader>("FinalBossRainbow"));
            finalBossMaterial.SetFloat("_RainbowBottom", floorY);
            finalBossMaterial.SetFloat("_RainbowHeight", bounds.size.y);
            foreach (var part in renderers)
            {
                part.sharedMaterial = finalBossMaterial;
                part.color = Color.white;
            }
            SetPhase(DuelPhase.FinalBoss);
        }

        public void AdvanceFinalBossDialogue()
        {
            if (Phase != DuelPhase.FinalBoss || DeveloperToolsOpen || FinalBossChoiceReady) return;
            FinalBossLine++;
            ui.Show();
        }

        public void ChooseBonus(bool enter)
        {
            if (Phase != DuelPhase.FinalBoss || DeveloperToolsOpen || !FinalBossChoiceReady) return;
            if (!enter)
            {
                MainMenu();
                return;
            }
            ClearEncounter();
            Wave = BonusWave;
            FirstConfiguration = developerTrainingSelected = false;
            SetPhase(DuelPhase.Configure);
        }

        void ClearFinalBoss()
        {
            FinalBossHealth = null;
            FinalBossLine = 0;
            if (finalBossMaterial != null) Destroy(finalBossMaterial);
            finalBossMaterial = null;
        }
    }
}
