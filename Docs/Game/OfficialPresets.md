# Official Rule Presets

Confirmed by Thomas, 2026-10-22 (Prompt 02 and the explicit Low-Ammo Rush correction). Rule Setup has a bottom-left Help button. Its picker shows preset names and the complete rules, with only Back and Apply controls; no explanatory copy. Choosing a name only previews it. Apply replaces the setup with a fresh editable copy and returns to Rule Setup without starting combat. Back/Escape preserves existing rules and unfinished edits. F8 > Presets remains available for developer testing. The first four templates are Learn; the other twelve are Playstyles. Incoming Fire is initially selected for preview only. The code authority is `OfficialRulePresets.cs`; every preview/application creates a fresh ordinary `ConditionalLoadout` with explicit catalog IDs, typed parameters and global DisplayOrder.

The global limit is **20 rules**, separate from the unchanged five operations. The longest template has ten rules. Rules support any number of AND conditions (minimum one). Existing template definitions are unchanged; their copied rules can append, edit or remove conditions without changing the template. Applied rules remain freely editable, reorderable and removable; no template lock, mode switch, saved-preset system or tutorial progression is introduced. Full validation happens before replacement; failure preserves the previous loadout and encounter and reports the reason. Both preview columns, setup rules and Combat Rules scroll independently with fixed readable text.

The following phase behavior applies to F8 > Presets.

| Phase | Apply / Clear Rules |
|---|---|
| Configure | Replace the setup; do not start an encounter |
| Training | Rebuild normal Training with fresh resources |
| Prepare / Fight | Restart the current wave through normal three-second preparation |
| Menu / Between / Failed / Complete | Enter Configure, retaining a legal wave (otherwise Wave 1) |

**Try in Training** applies and builds normal Training from any phase. Back returns to its configuration with Training still selected; Continue follows the existing path to Wave 1 configuration. This selection does not forge FirstConfiguration. Successful application closes F8, discards incomplete rule-picker edits/focus, cleans the previous controller/actors/events and gates both mouse buttons until release. Clear Rules uses the same path with an empty loadout. Session tuning persists; Restore defaults changes tuning only. Retry/next wave retain every rule and field; New Game/Restart still clears rules.

Presets use the current six-round Revolver: LMB/F01 precise single shots, F03 accurate primary cadence, RMB/F02 the same committed remaining-magazine burst and real Burst Finished event. Accepted weapon snapshots and resource limits remain authoritative. Burst damage remains 75% rounded half up. Reload keeps the world-up physical barrel and synchronized red flash; the prompt's older smoke description is superseded. No preset changes weapon/Boss values or adds hidden execution logic.

## Learn

### 01. Incoming Fire (`incoming_fire`)

Jump automatically when a ranged attack is about to reach you.

Try: Stand on the floor in front of the training enemy. Let a bullet approach without pressing Jump.

Tweak: Try Dodge Away from Threat instead of Jump Once.

1. Incoming Attack (Ranged) → Jump Once

### 02. Jump Counter (`jump_counter`)

Jump over incoming fire, aim in the air, and request a precise shot at the apex.

Try: Let a bullet trigger the jump, or jump manually and watch the follow-up.

Tweak: Replace Fire 1 Shot with Short Burst to compare the two attacks.

1. Incoming Attack (Ranged) → Jump Once
2. Ground State (Airborne) → Auto Aim at Nearest Enemy
3. Jump Apex Reached → Fire 1 Shot

### 03. Roll and Fan (`roll_and_fan`)

Dodge into close range, then automatically fan the remaining rounds.

Try: Dodge manually at different distances. Compare a close finish with a distant one.

Tweak: Use Fire 1 Shot for a slower, precise follow-up.

1. Dodge Finished → Auto Aim at Nearest Enemy
2. Firing → Auto Aim at Nearest Enemy
3. Dodge Finished + Horizontal Distance (Close) → Short Burst

### 04. Shot Step (`shot_step`)

Firing triggers a short backward step.

Try: Fire one precise shot without moving. Turn your body and try again.

Tweak: Change Back to Forward and compare the movement.

1. Shot Fired → Move (Back)

## Playstyles

### 05. Read and Shoot (`read_and_shoot`)

Aim manually and let attack tells or a finished dodge trigger a precise shot.

Try: Keep an attacking enemy in the firing line, then move your aim away and compare.

Tweak: Change the dodge follow-up to a close-range Short Burst.

1. Enemy Preparing an Attack + Enemy in Firing Line → Fire 1 Shot
2. Dodge Finished + Enemy in Firing Line → Fire 1 Shot
3. Incoming Attack (Any) → Dodge Away from Threat

### 06. Kiting Gunner (`kiting_gunner`)

Hold LMB for precise fire and mid-range movement; reposition while reloading or under threat.

Try: Hold LMB from close and far positions, then release it to compare control.

Tweak: Replace Keep Mid Range with Approach Enemy for a more aggressive version.

1. Near Wall → Move Toward Center
2. Reloading → Retreat from Enemy
3. Trigger Held → Keep Mid Range
4. Trigger Held → Auto Aim at Nearest Enemy
5. Trigger Held → Keep Firing
6. Incoming Attack (Any) → Dodge in Move Direction

### 07. Close Fan (`close_fan`)

Choose your approach manually; close range activates rapid fan fire.

Try: Cross into close range, then stay nearby through one reload.

Tweak: Change Dodge Toward Enemy to Dodge Away from Threat.

1. Horizontal Distance (Close) → Auto Aim at Nearest Enemy
2. Firing → Auto Aim at Nearest Enemy
3. Horizontal Distance (Close) → Short Burst
4. Reload Finished + Horizontal Distance (Close) → Short Burst
5. Enemy Preparing an Attack + Horizontal Distance (Close) → Dodge Toward Enemy

### 08. Hit Rush (`hit_rush`)

A mid-range hit starts a rush; finishing close can trigger fan fire and a brief retreat.

Try: At mid range, compare a missed shot with a hit. Leave enough room for the dodge.

Tweak: Remove the Mid condition to make more hits request another rush.

1. Hit Enemy + Horizontal Distance (Mid) → Dodge Toward Enemy
2. Dodge Finished → Auto Aim at Nearest Enemy
3. Firing → Auto Aim at Nearest Enemy
4. Dodge Finished + Horizontal Distance (Close) → Short Burst
5. Burst Finished → Retreat from Enemy

### 09. Run and Gun (`run_and_gun`)

Move to fire precise shots, dodge with your movement, and jump once when reloading.

Try: Keep aiming at the enemy and switch between moving and standing still.

Tweak: Change Moving to Trigger Held to make shooting depend on LMB instead.

1. Moving → Keep Firing
2. Incoming Attack (Ranged) → Dodge in Move Direction
3. Reloading → Jump Once

### 10. Platform Rhythm (`platform_rhythm`)

Use reloads to seek a higher platform and firing to seek a lower one.

Try: Fight near a reachable training platform and fire through a full magazine.

Tweak: Swap Higher and Lower to reverse the height rhythm.

1. Reloading → Move to Platform (Higher)
2. Firing → Move to Platform (Lower)
3. Enemy Near Aim Direction → Aim Assist
4. Enemy in Firing Line → Keep Firing

### 11. Aerial Gunslinger (`aerial_gunslinger`)

Keep jumping and use each apex to choose close fan fire or a precise mid-range or distant shot.

Try: Compare apex-triggered shots at Close and Mid/Far distances.

Tweak: Remove the Grounded → Repeat Jumps rule and trigger jumps manually.

1. Near Wall → Move Toward Center
2. Reloading → Retreat from Enemy
3. Ground State (Grounded) → Repeat Jumps
4. Incoming Attack (Any) + Ground State (Grounded) → Dodge Away from Threat
5. Ground State (Airborne) → Auto Aim at Nearest Enemy
6. Firing → Auto Aim at Nearest Enemy
7. Jump Apex Reached + Horizontal Distance (Close) → Short Burst
8. Jump Apex Reached + Horizontal Distance (Mid) → Fire 1 Shot
9. Jump Apex Reached + Horizontal Distance (Far) → Fire 1 Shot

### 12. Anchored Marksman (`anchored_marksman`)

Hold LMB to stand and fire precisely; aim your cursor to choose an emergency dodge direction.

Try: Hold LMB while pressing a movement key, then release LMB and compare.

Tweak: Replace Hold Position with Keep Mid Range for a mobile version.

1. Reloading → Move Toward Center
2. Trigger Held → Hold Position
3. Trigger Held → Auto Aim at Lowest-Health Enemy
4. Trigger Held → Keep Firing
5. Incoming Attack (Ranged) + Trigger Held → Dodge Toward Cursor

### 13. Low-Ammo Rush (`low_ammo_rush`)

Hold LMB for precise fire; low ammo enables a rush and close-range fan.

Try: Hold LMB, then approach with one or two rounds left. Fire does not stop at two.

Tweak: Change Dodge Toward Enemy to Dodge Away from Threat.

1. Incoming Attack (Any) + Low Ammo → Dodge Toward Enemy
2. Low Ammo + Horizontal Distance (Close) → Auto Aim at Nearest Enemy
3. Low Ammo + Horizontal Distance (Close) → Short Burst
4. Trigger Held → Keep Firing

Thomas explicitly replaced Two-Round Reserve with this version. Low Ammo remains the existing non-reloading 1–2-round state; primary fire never stops at two. The higher-priority close fan can consume what remains if accepted before the next primary shot.

### 14. Emergency Protocol (`emergency_protocol`)

Fight manually until low health activates shooting, dodging, and repositioning assistance.

Try: Use F8 Health to lower current HP below the Low Health threshold, then compare the behavior.

Tweak: Remove automatic fire and keep only the low-health movement assistance.

1. Near Wall + Low Health → Move Toward Center
2. Reloading + Low Health → Retreat from Enemy
3. Low Health → Auto Aim at Lowest-Health Enemy
4. Low Health → Keep Firing
5. Incoming Attack (Any) + Low Health → Dodge Away from Threat
6. Took Damage + Low Health → Jump Once

### 15. Shot-Step Chain (`shot_step_chain`)

Start with one precise shot; completed backward steps trigger the next shot until the magazine empties.

Try: Leave space behind you, fire one LMB shot, and keep aiming without pressing movement keys.

Tweak: Remove Aim Assist to keep every shot fully manually aimed.

1. Shot Fired → Move (Back)
2. Move Finished → Aim Assist
3. Move Finished → Fire 1 Shot

### 16. Full Cycle (`full_cycle`)

Approach, fan at close range, retreat while reloading, then approach again.

Try: Start with a visible enemy and a full magazine. Watch movement change as reloading starts and ends.

Tweak: Change the two approach rules to Keep Mid Range and redesign the firing rules for precise shots.

1. Near Wall → Move Toward Center
2. Reloading → Retreat from Enemy
3. Horizontal Distance (Far) → Approach Enemy
4. Horizontal Distance (Mid) → Approach Enemy
5. Horizontal Distance (Close) → Hold Position
6. Horizontal Distance (Close) → Auto Aim at Nearest Enemy
7. Firing → Auto Aim at Nearest Enemy
8. Horizontal Distance (Close) → Short Burst
9. Reload Finished + Horizontal Distance (Close) → Short Burst
10. Incoming Attack (Any) → Dodge Away from Threat

## Interpretation and limits

- Incoming Ranged includes bullets, beams and flying sword waves; Jump Once uses the existing grounded capability and does not guarantee evasion. Train on the floor in front of the robot's horizontal gunfire.
- Short Move Back uses body facing captured at acceptance, not enemy or cursor direction. Only real successful distance completion emits Move Finished. Shot-Step Chain can break at walls, interruption, expiry or reload; a new manual shot starts it again.
- Horizontal Distance measures horizontal separation. Nearest-target actions and lowest-health-ratio Aim retain their existing target policies, including same-enemy intersections for paired enemy conditions.
- Ordinary event responses use the existing 0.5-second combat clock. Cooldowns, ammunition and geometry may prevent acceptance; accepted burst/jump/dodge/platform commitments follow their real completion rules. There is no hidden occurrence reset or event replay.
- Close Fan and Full Cycle include Reload Finished + Close so a continuous Close state can request the next magazine. Movement/reload behavior and real feedback remain ordinary rules; a glowing card indicates a complete match, not proof of execution.
- Emergency Protocol uses the existing Low Health latch. Use F8 Health's current HP control to test it; there is no automatic heal or timed deactivation.

See [ImplementationLog.md](ImplementationLog.md) for actual Editor checks and untested limits. Templates demonstrate control relationships, not balance or automatic victory.
