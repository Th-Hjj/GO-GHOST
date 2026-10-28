using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    // Private templates never escape: every preview/apply gets ordinary, editable rule copies.
    public sealed class OfficialRulePreset
    {
        public string Id { get; }
        public string Name { get; }
        public string Group { get; }
        public string Summary { get; }
        public string Try { get; }
        public string Tweak { get; }
        public int RuleCount => template.TotalRules;
        readonly ConditionalLoadout template = new ConditionalLoadout();

        internal OfficialRulePreset(string id, string name, string group, string summary, string attempt, string tweak,
            params ConditionalRule[] rules)
        {
            Id=id; Name=name; Group=group; Summary=summary; Try=attempt; Tweak=tweak;
            foreach (var rule in rules)
                if (!template.TryAddRule(ConditionalCatalog.Response(rule.Action).Operation, rule))
                    throw new ArgumentException("Invalid official preset: " + id);
            if (!ConditionalCatalog.IsValid(template)) throw new ArgumentException("Invalid official preset: " + id);
        }
        public ConditionalLoadout CreateLoadout() => template.Copy();
    }

    public static class OfficialRulePresets
    {
        const string UnlockProgressKey = "GO_GHOST.Presets.UnlockedCount";

        // Read through PlayerPrefs: no static cache to survive disabled Domain Reload.
        public static int UnlockedCount => Mathf.Clamp(PlayerPrefs.GetInt(UnlockProgressKey, 1), 1, All.Count);
        public static bool IsUnlocked(OfficialRulePreset preset)
        {
            for (int i = 0; i < UnlockedCount; i++)
                if (ReferenceEquals(All[i], preset)) return true;
            return false;
        }
        internal static void RecordPlayerDeath()
        {
            int count = UnlockedCount;
            if (count >= All.Count) return;
            PlayerPrefs.SetInt(UnlockProgressKey, count + 1);
            PlayerPrefs.Save();
        }

        public static IReadOnlyList<OfficialRulePreset> All { get; } = Array.AsReadOnly(new[]
        {
            new OfficialRulePreset("no_jump", "I don't want to jump", "Presets", "", "", "",
                R(53,"J01",incoming:IncomingAttackOption.Ranged)),
            new OfficialRulePreset("no_dodge", "I don't want to dodge", "Presets", "", "", "",
                R(53,"D01",incoming:IncomingAttackOption.Any),
                R(53,"J01",incoming:IncomingAttackOption.Ranged)),
            new OfficialRulePreset("no_aim", "I don't want to aim", "Presets", "", "", "",
                R(53,"D01",incoming:IncomingAttackOption.Any),
                R(53,"J01",incoming:IncomingAttackOption.Ranged),
                R(52,"Q01",distance:EnemyDistanceOption.Close),
                R(52,"Q01",distance:EnemyDistanceOption.Mid),
                R(52,"Q01",distance:EnemyDistanceOption.Far)),
            new OfficialRulePreset("only_move", "I only want to move", "Presets", "", "", "",
                R(53,"D01",incoming:IncomingAttackOption.Any),
                R(53,"J01",incoming:IncomingAttackOption.Ranged),
                R(52,"Q01",distance:EnemyDistanceOption.Close),
                R(52,"Q01",distance:EnemyDistanceOption.Mid),
                R(52,"Q01",distance:EnemyDistanceOption.Far),
                R(18,"F03")),
            new OfficialRulePreset("reload_retreat", "Retreat while reloading", "Presets", "", "", "",
                R(53,"D01",45,incoming:IncomingAttackOption.Any,dodge:DodgeDirectionOption.TowardEnemy),
                R(53,"D01",incoming:IncomingAttackOption.Any),
                R(53,"J01",incoming:IncomingAttackOption.Ranged),
                R(9,"M11",move:MoveDirectionOption.AwayFromEnemy),
                R(52,"Q01",distance:EnemyDistanceOption.Close),
                R(52,"Q01",distance:EnemyDistanceOption.Mid),
                R(52,"Q01",distance:EnemyDistanceOption.Far),
                R(18,"F03")),
            new OfficialRulePreset("do_nothing", "I don't want to do anything", "Presets", "", "", "",
                R(53,"D01",45,incoming:IncomingAttackOption.Any,dodge:DodgeDirectionOption.TowardEnemy),
                R(53,"D01",incoming:IncomingAttackOption.Any),
                R(53,"J01",incoming:IncomingAttackOption.Ranged),
                R(9,"M11",move:MoveDirectionOption.AwayFromEnemy),
                R(51,"M03",ground:GroundStateOption.Grounded),
                R(51,"M03",ground:GroundStateOption.Airborne),
                R(52,"Q01",distance:EnemyDistanceOption.Close),
                R(52,"Q01",distance:EnemyDistanceOption.Mid),
                R(52,"Q01",distance:EnemyDistanceOption.Far),
                R(18,"F03")),
        });

        // Explicit catalog IDs and typed fields, never labels or preset-specific execution.
        static ConditionalRule R(int condition, string action, int additional=0,
            GroundStateOption ground=GroundStateOption.Unspecified,
            EnemyDistanceOption distance=EnemyDistanceOption.Unspecified,
            IncomingAttackOption incoming=IncomingAttackOption.Unspecified,
            GroundStateOption additionalGround=GroundStateOption.Unspecified,
            EnemyDistanceOption additionalDistance=EnemyDistanceOption.Unspecified,
            PlatformDirectionOption platform=PlatformDirectionOption.Unspecified,
            MoveDirectionOption move=MoveDirectionOption.Unspecified,
            DodgeDirectionOption dodge=DodgeDirectionOption.AwayFromThreat)
            => new ConditionalRule { Condition=condition, Action=action, AdditionalCondition=additional,
                GroundState=ground, Distance=distance, IncomingAttack=incoming,
                AdditionalGroundState=additionalGround, AdditionalDistance=additionalDistance,
                MoveDirection=move, DodgeDirection=dodge, PlatformDirection=platform };
    }
}
