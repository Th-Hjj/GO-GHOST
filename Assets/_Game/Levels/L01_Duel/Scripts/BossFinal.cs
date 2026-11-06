using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>
    /// FINAL BOSS SETUP (partial BossAgent, same pattern as BossTraining.cs).
    /// Takes the Swordsman prefab, gives it a runtime COPY of its profile (the asset is never written),
    /// scales its sword reach for the 2x body, switches off the Unbound finale, and adds a second health pool.
    ///  - ConfigureFinal(): call once, right after the boss is created and before combat starts.
    /// </summary>
    public sealed partial class BossAgent
    {
        public bool IsFinalBoss { get; private set; }

        public void ConfigureFinal(PlayerHealth player, ArenaNavigation2D navigation, DuoCoordinator coordinator, int actorId,
            Projectile gunProjectile, int firstHealth, int secondHealth)
        {
            IsFinalBoss = true;
            profile = Instantiate(profile);
            profile.name = "Final boss (runtime)";
            profile.hideFlags = HideFlags.DontSave;
            profile.Health = firstHealth;
            // The 2x body needs a longer blade; keep the swing arc, widen the reach.
            profile.BladeRadius *= 1.4f;
            if (profile.RhythmReach != null)
                for (int i = 0; i < profile.RhythmReach.Length; i++) profile.RhythmReach[i] *= 1.5f;
            if (profile.Sword != null)
            {
                profile.Sword.CutReach *= 1.6f;
                profile.Sword.CutRadius *= 1.4f;
                profile.Sword.UnboundHealth = -1f;   // never triggers: the special moves are this boss's only extras
            }
            if (projectile == null) projectile = gunProjectile;
            gameObject.AddComponent<FinalBossProfileOwner>().Profile = profile;
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
