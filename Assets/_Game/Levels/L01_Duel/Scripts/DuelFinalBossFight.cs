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
        // Fight-only copy of the equipped rules that carries the Boss's invasions; null until the first one.
        // DraftLoadout is never modified, so Retry and Configuration discard invaded rules.
        ConditionalLoadout invadedLoadout;
        public int InvadedRuleCount { get; private set; }
        BossAgent CreateFinalBoss(float spawnX)
        {
            var instance = Instantiate(swordsmanPrefab, new Vector3(spawnX, swordsmanSpawn.y, 0), Quaternion.identity, encounter.transform);
            instance.name = "FinalBoss";
            invadedLoadout = null; InvadedRuleCount = 0;
            var actor = instance.GetComponent<BossAgent>();
            // Actor id 2 on purpose: the player's rules already understand a solo second opponent.
            actor.ConfigureFinal(Player, navigation, coordinator, 2, FinalBossFirstHealth, FinalBossSecondHealth, BeginFinalBossPhaseTwo, InvadePlayerRules);
            return actor;
        }

        // Appends one random legal rule after the player's last rule, beyond the five-rule cap, and rebinds.
        ConditionalRule InvadePlayerRules()
        {
            if (Player == null || Phase != DuelPhase.Fight) return null;
            var rule = FinalBossInvasion.Pick();
            if (rule == null) return null;
            invadedLoadout ??= DraftLoadout.Copy();
            int order = 0;
            foreach (var existing in invadedLoadout.OrderedRules()) order = Mathf.Max(order, existing.DisplayOrder + 1);
            rule.DisplayOrder = order;
            invadedLoadout.Operations[(int)ConditionalCatalog.Response(rule.Action).Operation].Rules.Add(rule);
            var installed = DraftLoadout;
            DraftLoadout = invadedLoadout;
            try { BindEncounterRules(); }
            finally { DraftLoadout = installed; }
            InvadedRuleCount++;
            ui.FlashInvadedRule(order);
            ui.Show();
            Trace($"final boss invasion: {ConditionalCatalog.Expression(rule).Conditions} -> {ConditionalCatalog.Expression(rule).Action}");
            return rule;
        }

        // Adjacent platforms (ordered left to right) bob in opposite phase. The floor never moves.
        void StartPlatformMotion(bool easeIn = false)
        {
            var supports = ActivePlatforms;
            if (supports == null || supports.Length < 2) return;
            var moving = new System.Collections.Generic.List<Collider2D>(supports.Length - 1);
            for (int i = 1; i < supports.Length; i++) if (supports[i] != null) moving.Add(supports[i]);
            moving.Sort((a, b) => a.bounds.center.x.CompareTo(b.bounds.center.x));
            for (int i = 0; i < moving.Count; i++)
            {
                var oscillator = moving[i].GetComponent<PlatformOscillator>();
                if (oscillator == null) oscillator = moving[i].gameObject.AddComponent<PlatformOscillator>();
                oscillator.Begin(i % 2 == 0 ? 0 : Mathf.PI, easeIn: easeIn);
            }
            Trace($"final boss phase 2: {moving.Count} platforms moving");
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
