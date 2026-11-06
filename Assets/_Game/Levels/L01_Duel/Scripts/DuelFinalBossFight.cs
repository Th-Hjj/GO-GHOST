using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>
    /// FINAL BOSS FIGHT (partial DuelGame).
    ///  CreateFinalBoss():        spawns the boss at 2x size from the Swordsman prefab and attaches its special moves.
    ///  StripPlayerAssistance():  phase 2: rebuilds the player's rules from an EMPTY loadout, so raw input passes through.
    ///                            The equipped loadout itself is kept and comes back on the Configure screen.
    /// </summary>
    public sealed partial class DuelGame
    {
        const int FinalBossFirstHealth = 1400, FinalBossSecondHealth = 1000;
        // True only while the dialogue page is showing (it has no platforms); the fight itself uses the 5-platform layout.
        bool finalBossVisit;

        BossAgent CreateFinalBoss(float spawnX)
        {
            // Instantiated under an INACTIVE holder so Awake runs only after the 2x scale is set:
            // BossMotor2D caches its body offset in Awake, so scaling afterwards would break its Position.
            var holder = new GameObject("FinalBossHolder");
            holder.SetActive(false);
            holder.transform.SetParent(encounter.transform, false);
            var instance = Instantiate(swordsmanPrefab, holder.transform);
            instance.name = "FinalBoss";
            instance.transform.localScale = swordsmanPrefab.transform.localScale * 2f;
            instance.transform.position = new Vector3(spawnX, swordsmanSpawn.y, 0);
            instance.transform.SetParent(encounter.transform, true);   // activates it now, with the scale in place
            Destroy(holder);
            Physics2D.SyncTransforms();

            // Stand the 2x body on the floor.
            var shape = instance.GetComponent<Collider2D>();
            float lift = platforms[0].bounds.max.y + .02f - shape.bounds.min.y;
            instance.transform.position += Vector3.up * lift;
            instance.GetComponent<Rigidbody2D>().position += Vector2.up * lift;
            Physics2D.SyncTransforms();

            var actor = instance.GetComponent<BossAgent>();
            // Actor id 2 on purpose: the player's rules already understand "the swordsman".
            actor.ConfigureFinal(Player, navigation, coordinator, 2,
                gunnerPrefab.GetComponent<BossAgent>().ProjectilePrefab, FinalBossFirstHealth, FinalBossSecondHealth);
            instance.AddComponent<FinalBossSpecials>().Begin(actor, Player, navigation, ActivePlatforms, StripPlayerAssistance);
            return actor;
        }

        void StripPlayerAssistance()
        {
            if (Player == null) return;
            var installed = DraftLoadout;
            DraftLoadout = new ConditionalLoadout();   // no rules installed
            try { BindEncounterRules(); }
            finally { DraftLoadout = installed; }
            Trace("final boss phase 2: player assistance removed");
        }
    }
}
