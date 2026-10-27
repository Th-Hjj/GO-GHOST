# Official Rule Presets

Confirmed by Thomas, 2026-10-24. Six progressive presets replace the previous sixteen. New Game starts with an empty loadout. Nothing equips automatically, replaces a player's configuration without Apply, opens Help automatically, or changes Training, combat, defeat, Retry or next-wave flow. There is no onboarding/tutorial or new execution logic.

2026-10-25: Thomas removed A40 and approved replacing shared R10 with A18 (Enemy Near Aim Direction) in presets 4, 5 and 6. Counts, order and unlock behavior are unchanged.

2026-10-26: the shared Jump-priority override applies to all presets. An executable R3 Jump precedes R1/R2 Dodge despite their displayed order, and can interrupt an active Dodge. An unavailable Jump still permits Dodge. Definitions, displayed order and unlock behavior remain unchanged.

## Definitions and global priority

`OfficialRulePresets.cs` is the code authority. These are ordinary catalog conditions, actions and parameters, ordered globally from top to bottom:

| Rule | Conditions | Action |
|---|---|---|
| R1 | Incoming Attack (Any) + Near Wall | Dodge Toward Enemy |
| R2 | Incoming Attack (Any) | Dodge Away from Threat |
| R3 | Incoming Attack (Ranged) | Jump Once |
| R4 | Reloading | Retreat from Enemy |
| R5 | Ground State (Grounded) | Keep Range (Mid) |
| R6 | Ground State (Airborne) | Keep Range (Mid) |
| R7 | Horizontal Distance (Close) | Auto Aim at Nearest Enemy |
| R8 | Horizontal Distance (Mid) | Auto Aim at Nearest Enemy |
| R9 | Horizontal Distance (Far) | Auto Aim at Nearest Enemy |
| R10 | Enemy Near Aim Direction | Keep Firing |

| Preset | Stable ID | Included rules, in order |
|---|---|---|
| 1. I don't want to jump | `no_jump` | R3 |
| 2. I don't want to dodge | `no_dodge` | R2, R3 |
| 3. I don't want to aim | `no_aim` | R2, R3, R7, R8, R9 |
| 4. I only want to move | `only_move` | R2, R3, R7, R8, R9, R10 |
| 5. Retreat while reloading | `reload_retreat` | R1, R2, R3, R4, R7, R8, R9, R10 |
| 6. I don't want to do anything | `do_nothing` | R1–R10 |

Each creates a complete independent deep copy with contiguous global DisplayOrder. All rows and their conditions remain editable, reorderable and removable under the existing 20-rule budget and unlimited AND conditions. No preset changes weapon/Boss/map values, action commitments, unavailable-action fallback or manual input semantics.

R5/R6 deliberately use Ground State and the existing Keep Range (Mid) target policy. Do not substitute Close → Retreat / Far → Approach: in a two-enemy encounter those conditions can bind different targets. Distance control is independent of Firing, so interruptions in shooting do not themselves cancel movement. Names describe the requested assistance, not a guarantee of evasion or victory.

## Help and persistent unlocks

Help initially lists only preset 1. Each real `PlayerHealth.OnDeath` unlocks the next preset, once per player life, capped at six. Training player deaths count. Training robot death/respawn, rule rebind, encounter creation, intentional restart, F8 switching/application and tuning do not count. A real death during a developer-selected encounter still counts; opening or using F8 itself does not.

`GO_GHOST.Presets.UnlockedCount` is one PlayerPrefs integer (default 1, read clamped to 1–6), saved immediately when progress advances. New Game/Restart, Retry, next wave, menu transitions and application restart never clear it. No loadout is saved by this value and no general save framework is introduced. With Domain Reload disabled, reads still come directly from PlayerPrefs rather than a static cache.

`DuelGame` subscribes once when creating the player, guards each life and unsubscribes on encounter cleanup or game destruction. Robot respawn only rebinds the controller and does not add a player-death subscription. The callback records progress only; existing failure and respawn flow stays in its original owners.

Help keeps names, complete rule previews and Back / Apply. Selecting a name only previews; Back/Escape preserves loadout and unfinished picker drafts. The list and actual Apply use the same unlock predicate; a stale button cannot apply a currently locked preset. Only successful explicit Apply validates and replaces the setup with a fresh editable copy, then returns to setup.

## F8 developer entry

F8 > Presets exposes all six regardless of Help progress. Apply and Try in Training use the existing validated deep-copy path, with no unlock writes. Clear Rules retains the same path. Session tuning and current action snapshots retain their existing behavior.

| Phase | F8 Apply / Clear Rules |
|---|---|
| Configure | Replace setup without starting an encounter |
| Training | Rebuild normal Training with fresh resources |
| Prepare / Fight | Restart current wave through normal three-second preparation |
| Menu / Between / Failed / Complete | Enter Configure, retaining a legal wave (otherwise Wave 1) |

Try in Training applies and builds normal Training from any phase. Back retains its configuration; Continue follows the normal Wave 1 setup path. Successful F8 application closes tools, discards incomplete picker edits, cleans the prior controller/actors/events and gates both mouse buttons until release. Retry/next wave retain rules; New Game clears rules but preserves unlocks.

## Verification scope

Use the existing Official Presets Data and Flow / Gameplay / Final Edges Editor checks. The smoke runner protects and restores the actual unlock key (including its original absence), including on failure/cancellation. Checks cover exact definitions, deep copies, Help/F8 callbacks, death/respawn lifecycle, cap and persistence, defense order, reload resumption, wall Dodge and W3 distance control. Controlled fixtures prove mechanisms, not balance. See [ImplementationLog.md](ImplementationLog.md) for actual results and NOT TESTED limits. No full Build, win-rate matrix or full AI clear is required.
