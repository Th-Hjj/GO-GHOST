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
        const float WaveTitleFadeSeconds = .6f;
        TMP_Text waveTitle;
        CanvasGroup[] bossHealthGroups;
        readonly TMP_Text[] invasionThresholdMarks = new TMP_Text[2];

        void BuildInvasionMarks(RectTransform bar,float y)
        {
            for(int i=0;i<FinalBossAxe.InvasionPercents.Length;i++)
            {
                float x=430*(FinalBossAxe.InvasionPercents[i]/100f-.5f);
                Image(bar,new Vector2(x,y-7),new Vector2(2,6),NeuralPalette.Emp);
                var mark=Text("?",bar,new Vector2(x,y-23),new Vector2(30,28),21,TextAlignmentOptions.Center,controlFont);
                mark.color=NeuralPalette.Emp; invasionThresholdMarks[i]=mark;
            }
        }
        float bossPoolSpentAt = float.NegativeInfinity, secondPoolAt = float.NegativeInfinity;
        internal void FadeSpentBossHealth() => bossPoolSpentAt = Time.time;

        void ResetBattleStatus()
        {
            // Show rebuilds text objects even if combat values have not changed (pause/F8).
            playerHealthText = default;
            bossHealthText = System.Array.Empty<HealthTextState>();
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
            if (waveTitle != null)
            {
                // The encounter clock survives pause/F8 page rebuilds and resets for each attempt.
                waveTitle.alpha = game.Phase == DuelPhase.Prepare
                    ? 1 : Mathf.Clamp01(1 - game.FightClock / WaveTitleFadeSeconds);
                if (waveTitle.alpha <= 0) waveTitle.gameObject.SetActive(false);
            }
        }

        void UpdateHealthStatus()
        {
            playerHealthText.Update(playerHealth, game.Appearance.DisplayName + "  ", game.Player.CurrentHealth);
            for (int i = 0; i < policeHealth.Length && i < game.Bosses.Count; i++)
            {
                var actor = game.Bosses[i];
                if (actor == null)
                    continue;
                string label = actor.IsTrainingEnemy ? "DUMMY ROBOT  "
                    : actor.IsFinalBoss ? "Final Boss  " : actor.ActorId == 1 ? "Gunner  " : "Swordsman  ";
                float opacity = 1;
                if (actor.IsFinalBoss && !float.IsNegativeInfinity(bossPoolSpentAt))
                {
                    if (actor.Axe.PhaseTwo)
                    {
                        if (float.IsNegativeInfinity(secondPoolAt)) secondPoolAt = Time.time;
                        opacity = Reveal(Time.time - secondPoolAt, .3f);
                    }
                    else opacity = 1 - Reveal(Time.time - bossPoolSpentAt, .25f);
                }
                bossHealthGroups[i].alpha = opacity;
                if(actor.IsFinalBoss && !actor.Axe.PhaseTwo)
                    for(int j=0;j<invasionThresholdMarks.Length;j++)
                    {
                        var mark=invasionThresholdMarks[j]; if(mark==null)continue;
                        bool spent=j<game.InvadedRuleCount;
                        bool reached=(long)actor.Health.CurrentHealth*100<=(long)actor.Health.MaxHealth*FinalBossAxe.InvasionPercents[j];
                        mark.text=spent?"·":"?";
                        mark.color=spent?NeuralPalette.Secondary:reached?Color.Lerp(NeuralPalette.Emp,NeuralPalette.Primary,.55f):NeuralPalette.Emp;
                    }
                bossHealthText[i].Update(policeHealth[i], label, actor.Health.CurrentHealth);
                policeFill[i].rectTransform.sizeDelta =
                    new Vector2(430f * actor.Health.CurrentHealth / actor.Health.MaxHealth, 12);
            }
            playerFill.rectTransform.sizeDelta =
                new Vector2(430f * game.Player.CurrentHealth / game.Player.MaxHealth, 12);
            playerFill.color = game.Rules != null && game.Rules.IsLowHealth
                ? NeuralPalette.Danger : NeuralPalette.Primary;
        }

    }
}
