using TMPro;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        struct HealthTextState
        {
            string label;
            int health;

            public void Update(TMP_Text text, string nextLabel, int nextHealth)
            {
                if (label == nextLabel && health == nextHealth)
                    return;
                label = nextLabel;
                health = nextHealth;
                text.text = label + health;
            }
        }

        HealthTextState playerHealthText;
        HealthTextState[] bossHealthText = System.Array.Empty<HealthTextState>();
        int displayedAmmo, displayedMagazineSize;
        bool displayedReloading;
        bool resourceStatusValid;

        void ResetBattleStatus()
        {
            // Show rebuilds text objects even if combat values have not changed (pause/F8).
            playerHealthText = default;
            bossHealthText = System.Array.Empty<HealthTextState>();
            resourceStatusValid = false;
        }

        void UpdateBattleStatus()
        {
            if (playerHealth == null || game.Player == null)
                return;

            if (preparationCount != null)
            {
                int count = Mathf.CeilToInt(game.PreparationRemaining);
                if (count != lastPreparationCount)
                {
                    lastPreparationCount = count;
                    preparationCount.text = count.ToString();
                }
            }

            UpdateHealthStatus();
            UpdateResourceStatus();
        }

        void UpdateHealthStatus()
        {
            playerHealthText.Update(playerHealth, "YOU  ", game.Player.CurrentHealth);
            for (int i = 0; i < policeHealth.Length && i < game.Bosses.Count; i++)
            {
                var actor = game.Bosses[i];
                if (actor == null)
                    continue;
                string label = actor.IsTrainingEnemy ? "TRAINING ENEMY  "
                    : actor.ActorId == 1 ? "GUNNER  " : "SWORDSMAN  ";
                bossHealthText[i].Update(policeHealth[i], label, actor.Health.CurrentHealth);
                policeFill[i].rectTransform.sizeDelta =
                    new Vector2(430f * actor.Health.CurrentHealth / actor.Health.MaxHealth, 12);
            }
            playerFill.rectTransform.sizeDelta =
                new Vector2(430f * game.Player.CurrentHealth / game.Player.MaxHealth, 12);
            playerFill.color = game.Rules != null && game.Rules.IsLowHealth
                ? NeuralPalette.Danger : NeuralPalette.Primary;
        }

        void UpdateResourceStatus()
        {
            var rifle = game.Rifle;
            if (resourceStatusValid && displayedAmmo == rifle.AmmoInMagazine
                && displayedMagazineSize == rifle.MagazineSize && displayedReloading == rifle.IsReloading)
                return;

            displayedAmmo = rifle.AmmoInMagazine;
            displayedMagazineSize = rifle.MagazineSize;
            displayedReloading = rifle.IsReloading;
            resourceStatusValid = true;
            resources.text = displayedReloading ? "Reloading" : $"Ammo {displayedAmmo} / {displayedMagazineSize}";
        }
    }
}
