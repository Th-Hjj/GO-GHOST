using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Every legal one-condition / one-action rule the player could build in the editor (Thomas,
    // 2026-11-06): each selectable condition and option paired with each selectable action and
    // option, kept only when the catalog accepts it. Final Boss invasions draw from this list.
    public static class FinalBossInvasion
    {
        static List<ConditionalRule> legal;

        public static IReadOnlyList<ConditionalRule> LegalRules => legal ??= Build();

        public static ConditionalRule Pick()
        {
            var rules = LegalRules;
            return rules.Count == 0 ? null : rules[Random.Range(0, rules.Count)].Copy();
        }

        static List<ConditionalRule> Build()
        {
            var result = new List<ConditionalRule>(256);
            for (int op = 0; op < ConditionalCatalog.FormalResponses.Length; op++)
            foreach (string action in ConditionalCatalog.FormalResponses[op])
            {
                int actionOptions = Mathf.Max(1, ConditionalCatalog.ActionOptions(action).Length);
                for (int a = 0; a < actionOptions; a++)
                foreach (int condition in ConditionalCatalog.FormalConditions[op])
                {
                    int conditionOptions = Mathf.Max(1, ConditionalCatalog.ConditionOptions(condition).Length);
                    for (int c = 0; c < conditionOptions; c++)
                    {
                        var rule = new ConditionalRule { Condition = condition, Action = action };
                        if (ConditionalCatalog.ConditionOptions(condition).Length > 0) ConditionalCatalog.SetConditionOption(rule, c);
                        if (ConditionalCatalog.ActionOptions(action).Length > 0) ConditionalCatalog.SetActionOption(rule, a);
                        if (rule.Action == ActionIds.AutoAim) rule.AutoAim = AutoAimOption.Nearest;
                        int operation = (int)ConditionalCatalog.Response(rule.Action).Operation;
                        if (!ConditionalCatalog.IsAllowed(operation, rule)) continue;
                        var probe = new ConditionalLoadout();
                        probe.Operations[operation].Rules.Add(rule);
                        if (!ConditionalCatalog.IsValid(probe)) continue;
                        // Short Burst and Keep Firing share one picker entry; keep both modes once each.
                        if (result.Exists(r => r.Condition == rule.Condition && r.Action == rule.Action
                            && ConditionalCatalog.RuleConditionLabel(r) == ConditionalCatalog.RuleConditionLabel(rule)
                            && ConditionalCatalog.RuleActionLabel(r) == ConditionalCatalog.RuleActionLabel(rule))) continue;
                        result.Add(rule);
                    }
                }
            }
            return result;
        }
    }
}
