using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>
    /// FINAL BOSS FIGHT (partial DuelGame).
    ///  CreateFinalBoss():        spawns the player-sized Final Boss from the Swordsman prefab with the axe kit.
    ///  StripPlayerAssistance():  phase 2: rebuilds the player's rules from an EMPTY loadout, so raw input passes through.
    ///                            The equipped loadout itself is kept and comes back on the Configure screen.
    /// </summary>
    public sealed partial class DuelGame
    {
        // Thomas, 2026-11-06: both phases use 1000 HP for the trial.
        const int FinalBossFirstHealth = 1000, FinalBossSecondHealth = 1000;
        BossAgent CreateFinalBoss(float spawnX)
        {
            var instance = Instantiate(swordsmanPrefab, new Vector3(spawnX, swordsmanSpawn.y, 0), Quaternion.identity, encounter.transform);
            instance.name = "FinalBoss";
            var actor = instance.GetComponent<BossAgent>();
            // Actor id 2 on purpose: the player's rules already understand a solo second opponent.
            actor.ConfigureFinal(Player, navigation, coordinator, 2, FinalBossFirstHealth, FinalBossSecondHealth, StripPlayerAssistance);
            return actor;
        }

        void StripPlayerAssistance()
        {
            if (Player == null) return;
            var installed = DraftLoadout;
            DraftLoadout = new ConditionalLoadout();   // no rules installed
            try { BindEncounterRules(); }
            finally { DraftLoadout = installed; }
            ui.Show();
            Trace("final boss phase 2: player assistance removed");
        }
    }
}
