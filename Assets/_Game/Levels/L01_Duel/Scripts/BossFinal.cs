using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>
    /// FINAL BOSS SETUP (partial BossAgent, same pattern as BossTraining.cs).
    /// Thomas's 2026-11-06 trial design: a player-sized stickman built from the Swordsman prefab, with a
    /// runtime COPY of its profile (the asset is never written), the axe kit in <see cref="FinalBossAxe"/>
    /// instead of the sword tactics, and a second health pool for phase two.
    /// </summary>
    public sealed partial class BossAgent
    {
        public bool IsFinalBoss { get; private set; }
        public FinalBossAxe Axe { get; private set; }
        float secondPhaseMoveSpeed;

        internal void RestoreSecondPhaseMotion() => profile.Motion.Speed = secondPhaseMoveSpeed;

        public void ConfigureFinal(PlayerHealth player, ArenaNavigation2D navigation, DuoCoordinator coordinator, int actorId,
            int firstHealth, int secondHealth, System.Action onPhaseTwo, System.Func<ConditionalRule> onInvasion)
        {
            IsFinalBoss = true;
            profile = Instantiate(profile);
            profile.name = "Final boss (runtime)";
            profile.hideFlags = HideFlags.DontSave;
            profile.Health = firstHealth;
            secondPhaseMoveSpeed = profile.Motion.Speed;
            profile.Motion.Speed = 7.6f;
            if (profile.Sword != null) profile.Sword.UnboundHealth = -1f;   // the sword kit never runs
            gameObject.AddComponent<FinalBossProfileOwner>().Profile = profile;
            // Created before Configure so the view can apply the Final Boss look on its first frame.
            Axe = new FinalBossAxe(this, player, onPhaseTwo, onInvasion);
            Configure(player, navigation, coordinator, actorId, false);
            Health.EnableSecondPhase(secondHealth);
        }
    }

    /// <summary>Destroys the runtime profile copy together with the boss.</summary>
    sealed class FinalBossProfileOwner : MonoBehaviour
    {
        public BossProfile Profile;
        void OnDestroy() { if (Profile != null) Destroy(Profile); }
    }
}
