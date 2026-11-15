using UnityEngine;

namespace GoGhost.Duel
{
    // The lethal frame of a fight stays behind its result: combat stops where it stands until the player leaves the result.
    public sealed partial class DuelGame
    {
        internal bool ResultHeld { get; private set; }
        // Unscaled time of the lethal frame. The result page counts its entrances from here.
        internal double ResultHeldAt { get; private set; }

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
