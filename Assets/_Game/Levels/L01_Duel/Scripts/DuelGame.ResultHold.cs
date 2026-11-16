using UnityEngine;

namespace GoGhost.Duel
{
    // Defeat holds the lethal frame. Victory lets the Boss fall and fold before holding the result.
    public sealed partial class DuelGame
    {
        internal bool ResultHeld { get; private set; }
        // Unscaled time at which the result freezes. Its page counts entrances from here.
        internal double ResultHeldAt { get; private set; }
        const float VictorySlowSeconds = .55f, VictorySlowScale = .4f;
        float victoryElapsed, victorySettledTime;
        internal bool VictoryPresentation => Phase == DuelPhase.BossVictoryHold || Phase == DuelPhase.FinalVictoryHold;

        void BeginVictoryPresentation()
        {
            StopCombat();
            Player.IsInvulnerable = true;
            // Keep both fighters in the same slowed world. Input and damage have already ended;
            // the player's ordinary gravity is free to bring an airborne winning pose down too.
            var playerBody = Player.GetComponent<Rigidbody2D>();
            if (playerBody != null) playerBody.simulated = true;
            victoryElapsed = victorySettledTime = 0;
            SetPhase(Wave == FinalBossWave ? DuelPhase.FinalVictoryHold : DuelPhase.BossVictoryHold);
            ApplyPause(false);
            sounds?.Play(Sfx.BossDefeat);
        }

        void TickVictoryPresentation()
        {
            victoryElapsed += Time.unscaledDeltaTime;
            Time.timeScale = victoryElapsed < VictorySlowSeconds ? VictorySlowScale : 1;
            bool settled = true;
            foreach (var actor in actors)
                if (actor != null && actor.View != null && !actor.View.DefeatPoseComplete) { settled = false; break; }
            victorySettledTime = settled ? victorySettledTime + Time.deltaTime : 0;
            if (victoryElapsed < VictorySlowSeconds || victorySettledTime < .10f) return;
            Time.timeScale = 1;
            if (Wave == FinalBossWave)
            {
                ClearEncounter();
                SetPhase(DuelPhase.Complete);
                sounds?.Play(Sfx.Victory);
                return;
            }
            HoldResult(false);
            SetPhase(Wave == BonusWave ? DuelPhase.Complete : DuelPhase.Between);
            sounds?.Play(Sfx.Victory);
            if (Wave == 2)
                handler.WaveCleared(2, () =>
                {
                    ClearEncounter();
                    Wave = FinalBossWave;
                    SetPhase(DuelPhase.Configure);
                });
            else handler.WaveCleared(Wave);
        }

        // Replaces StopCombat at a result. ClearEncounter still runs the full teardown when the result is left.
        void HoldResult(bool defeat)
        {
            EndInvasionReading();
            ResultHeld = true;
            ResultHeldAt = Time.unscaledTimeAsDouble;
            if (music != null) music.StopMusic();
            if (encounterSounds != null) encounterSounds.EndScope();
            if (Player != null) Player.StopCombat();
            // Bodies, bullets and effects stop on the combat clock, as they do under Pause.
            ApplyPause(Paused);
            if (encounter == null) return;
            if (defeat && Player != null && Player.TryGetComponent(out PlayerFeedbackView feedback)) feedback.HoldHit();
            encounter.AddComponent<ResultHoldView>().Begin(ResultHeldAt, Player, actors);
        }
    }
}
