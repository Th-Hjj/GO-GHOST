using System;
using System.Collections.Generic;

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
        public static IReadOnlyList<OfficialRulePreset> All { get; } = Array.AsReadOnly(new[]
        {
            new OfficialRulePreset("incoming_fire", "Incoming Fire", "Learn",
                "Jump automatically when a ranged attack is about to reach you.",
                "Stand on the floor in front of the training enemy. Let a bullet approach without pressing Jump.",
                "Try Dodge Away from Threat instead of Jump Once.",
                R(53,"J01",incoming:IncomingAttackOption.Ranged)),
            new OfficialRulePreset("jump_counter", "Jump Counter", "Learn",
                "Jump over incoming fire, aim in the air, and request a precise shot at the apex.",
                "Let a bullet trigger the jump, or jump manually and watch the follow-up.",
                "Replace Fire 1 Shot with Short Burst to compare the two attacks.",
                R(53,"J01",incoming:IncomingAttackOption.Ranged),
                R(51,"Q01",ground:GroundStateOption.Airborne),
                R(58,"F01")),
            new OfficialRulePreset("roll_and_fan", "Roll and Fan", "Learn",
                "Dodge into close range, then automatically fan the remaining rounds.",
                "Dodge manually at different distances. Compare a close finish with a distant one.",
                "Use Fire 1 Shot for a slower, precise follow-up.",
                R(7,"Q01"),
                R(56,"Q01"),
                R(7,"F02",52,additionalDistance:EnemyDistanceOption.Close)),
            new OfficialRulePreset("shot_step", "Shot Step", "Learn",
                "Firing triggers a short backward step.",
                "Fire one precise shot without moving. Turn your body and try again.",
                "Change Back to Forward and compare the movement.",
                R(59,"M04")),
            new OfficialRulePreset("read_and_shoot", "Read and Shoot", "Playstyles",
                "Aim manually and let attack tells or a finished dodge trigger a precise shot.",
                "Keep an attacking enemy in the firing line, then move your aim away and compare.",
                "Change the dodge follow-up to a close-range Short Burst.",
                R(25,"F01",40),
                R(7,"F01",40),
                R(53,"D01",incoming:IncomingAttackOption.Any)),
            new OfficialRulePreset("kiting_gunner", "Kiting Gunner", "Playstyles",
                "Hold LMB for precise fire and mid-range movement; reposition while reloading or under threat.",
                "Hold LMB from close and far positions, then release it to compare control.",
                "Replace Keep Mid Range with Approach Enemy for a more aggressive version.",
                R(45,"M09"),
                R(9,"M02"),
                R(17,"M03"),
                R(17,"Q01"),
                R(17,"F03"),
                R(53,"D03",incoming:IncomingAttackOption.Any)),
            new OfficialRulePreset("close_fan", "Close Fan", "Playstyles",
                "Choose your approach manually; close range activates rapid fan fire.",
                "Cross into close range, then stay nearby through one reload.",
                "Change Dodge Toward Enemy to Dodge Away from Threat.",
                R(52,"Q01",distance:EnemyDistanceOption.Close),
                R(56,"Q01"),
                R(52,"F02",distance:EnemyDistanceOption.Close),
                R(11,"F02",52,additionalDistance:EnemyDistanceOption.Close),
                R(25,"D02",52,additionalDistance:EnemyDistanceOption.Close)),
            new OfficialRulePreset("hit_rush", "Hit Rush", "Playstyles",
                "A mid-range hit starts a rush; finishing close can trigger fan fire and a brief retreat.",
                "At mid range, compare a missed shot with a hit. Leave enough room for the dodge.",
                "Remove the Mid condition to make more hits request another rush.",
                R(13,"D02",52,additionalDistance:EnemyDistanceOption.Mid),
                R(7,"Q01"),
                R(56,"Q01"),
                R(7,"F02",52,additionalDistance:EnemyDistanceOption.Close),
                R(60,"M02")),
            new OfficialRulePreset("run_and_gun", "Run and Gun", "Playstyles",
                "Move to fire precise shots, dodge with your movement, and jump once when reloading.",
                "Keep aiming at the enemy and switch between moving and standing still.",
                "Change Moving to Trigger Held to make shooting depend on LMB instead.",
                R(54,"F03"),
                R(53,"D03",incoming:IncomingAttackOption.Ranged),
                R(9,"J01")),
            new OfficialRulePreset("platform_rhythm", "Platform Rhythm", "Playstyles",
                "Use reloads to seek a higher platform and firing to seek a lower one.",
                "Fight near a reachable training platform and fire through a full magazine.",
                "Swap Higher and Lower to reverse the height rhythm.",
                R(9,"J04",platform:PlatformDirectionOption.Higher),
                R(56,"J04",platform:PlatformDirectionOption.Lower),
                R(18,"Q06"),
                R(40,"F03")),
            new OfficialRulePreset("aerial_gunslinger", "Aerial Gunslinger", "Playstyles",
                "Keep jumping and use each apex to choose close fan fire or a precise mid-range or distant shot.",
                "Compare apex-triggered shots at Close and Mid/Far distances.",
                "Remove the Grounded → Repeat Jumps rule and trigger jumps manually.",
                R(45,"M09"),
                R(9,"M02"),
                R(51,"J03",ground:GroundStateOption.Grounded),
                R(53,"D01",51,incoming:IncomingAttackOption.Any,additionalGround:GroundStateOption.Grounded),
                R(51,"Q01",ground:GroundStateOption.Airborne),
                R(56,"Q01"),
                R(58,"F02",52,additionalDistance:EnemyDistanceOption.Close),
                R(58,"F01",52,additionalDistance:EnemyDistanceOption.Mid),
                R(58,"F01",52,additionalDistance:EnemyDistanceOption.Far)),
            new OfficialRulePreset("anchored_marksman", "Anchored Marksman", "Playstyles",
                "Hold LMB to stand and fire precisely; aim your cursor to choose an emergency dodge direction.",
                "Hold LMB while pressing a movement key, then release LMB and compare.",
                "Replace Hold Position with Keep Mid Range for a mobile version.",
                R(9,"M09"),
                R(17,"M10"),
                R(17,"Q02"),
                R(17,"F03"),
                R(53,"D04",17,incoming:IncomingAttackOption.Ranged)),
            new OfficialRulePreset("low_ammo_rush", "Low-Ammo Rush", "Playstyles",
                "Hold LMB for precise fire; low ammo enables a rush and close-range fan.",
                "Hold LMB, then approach with one or two rounds left. Fire does not stop at two.",
                "Change Dodge Toward Enemy to Dodge Away from Threat.",
                R(53,"D02",12,incoming:IncomingAttackOption.Any),
                R(12,"Q01",52,additionalDistance:EnemyDistanceOption.Close),
                R(12,"F02",52,additionalDistance:EnemyDistanceOption.Close),
                R(17,"F03")),
            new OfficialRulePreset("emergency_protocol", "Emergency Protocol", "Playstyles",
                "Fight manually until low health activates shooting, dodging, and repositioning assistance.",
                "Use F8 Health to lower current HP below the Low Health threshold, then compare the behavior.",
                "Remove automatic fire and keep only the low-health movement assistance.",
                R(45,"M09",4),
                R(9,"M02",4),
                R(4,"Q02"),
                R(4,"F03"),
                R(53,"D01",4,incoming:IncomingAttackOption.Any),
                R(6,"J01",4)),
            new OfficialRulePreset("shot_step_chain", "Shot-Step Chain", "Playstyles",
                "Start with one precise shot; completed backward steps trigger the next shot until the magazine empties.",
                "Leave space behind you, fire one LMB shot, and keep aiming without pressing movement keys.",
                "Remove Aim Assist to keep every shot fully manually aimed.",
                R(59,"M04"),
                R(61,"Q06"),
                R(61,"F01")),
            new OfficialRulePreset("full_cycle", "Full Cycle", "Playstyles",
                "Approach, fan at close range, retreat while reloading, then approach again.",
                "Start with a visible enemy and a full magazine. Watch movement change as reloading starts and ends.",
                "Change the two approach rules to Keep Mid Range and redesign the firing rules for precise shots.",
                R(45,"M09"),
                R(9,"M02"),
                R(52,"M01",distance:EnemyDistanceOption.Far),
                R(52,"M01",distance:EnemyDistanceOption.Mid),
                R(52,"M10",distance:EnemyDistanceOption.Close),
                R(52,"Q01",distance:EnemyDistanceOption.Close),
                R(56,"Q01"),
                R(52,"F02",distance:EnemyDistanceOption.Close),
                R(11,"F02",52,additionalDistance:EnemyDistanceOption.Close),
                R(53,"D01",incoming:IncomingAttackOption.Any)),
        });

        // Explicit catalog IDs and typed fields, never labels or preset-specific execution.
        static ConditionalRule R(int condition, string action, int additional=0,
            GroundStateOption ground=GroundStateOption.Unspecified,
            EnemyDistanceOption distance=EnemyDistanceOption.Unspecified,
            IncomingAttackOption incoming=IncomingAttackOption.Unspecified,
            GroundStateOption additionalGround=GroundStateOption.Unspecified,
            EnemyDistanceOption additionalDistance=EnemyDistanceOption.Unspecified,
            PlatformDirectionOption platform=PlatformDirectionOption.Unspecified)
            => new ConditionalRule { Condition=condition, Action=action, AdditionalCondition=additional,
                GroundState=ground, Distance=distance, IncomingAttack=incoming,
                AdditionalGroundState=additionalGround, AdditionalDistance=additionalDistance,
                ShortMoveDirection=ShortMoveDirectionOption.Back, PlatformDirection=platform };
    }
}
