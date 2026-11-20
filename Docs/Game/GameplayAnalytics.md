# Gameplay Analytics

Thomas, 2026-10-29: one independent record per New Game, identified by the character nickname. No participant numbering or cross-run player account. Schema 5 includes all three numbered missions through the winning hit on the Final Boss (Thomas, 2026-11-19). F8 has Tuning / Missions / Analytics, with Fight / Run / Actions / History views.

## What is recorded

| Group | Fields and interpretation |
|---|---|
| Identity and lifecycle | Nickname (`playerName`, blank becomes YOU), character confirmation, unique run/attempt IDs, UTC timestamps, application version, platform, Editor/test flags, finished state and end reason. Identical names never overwrite each other. |
| Time | Real elapsed play time split into character creation, configuration, Training, preparation, fight, pause/F8 and result time. Categories are mutually exclusive. Combat simulation seconds are retained separately, including failed/abandoned attempts. |
| Rule use | Committed rule additions/edits/removals/reorders, maximum equipped count, time to first nonempty configuration and first fight, latest readable configuration and each attempt's full initial rule/parameter/order snapshot. Cancelled drafts and hover choices do not count. `helpOpened` stays in the record and reads zero, since Help was removed. |
| Encounters | W1/W2/W3 attempt wave, result, reason, combat seconds, player and each present Boss's last/current and maximum HP, per-wave deaths, Training deaths separately, and separate Mission 2 and Final Boss milestones. Missing actors use HP -1. |
| Controls | Actual manual/automatic shots, successful jumps and Dodge starts; effective Move duration; manual and automatic Aim duration, plus the retained assisted-time field. Per-Action ID details and separate drop-through counts remain. |

Character creation records the empty loadout. When Happy installs the starter rule during her example in Body Configuration, it enters `latestRules`, sets `maxRulesEquipped` to one and sets `firstRuleAtSeconds`; with Tutorial Off no rule is installed. Only the player’s own confirmed additions increment `rulesAdded` and set `firstCustomRuleAtSeconds` on the first addition. Both first-rule fields use real seconds since the record began and default to -1. Sessions from the original Beta 33% download started empty. The refreshed download includes the starter rule.

Names are data, not identifiers. The run ID keeps same-name New Games separate; retries and wave transitions remain attempts within that run. The face drawing is not exported. Rule snapshots and action totals show usage, not comprehension or enjoyment. No per-frame input/position log or damage event stream is collected. Action totals still group by Action ID, not individual rule; snapshots retain each rule's parameters and order.

## Measurement boundaries

Fire counts each actual paid bullet, including every bullet in a committed burst, using its accepted source attribution. Jump counts successful takeoffs; Dodge counts successful starts. Move measures actual ordinary horizontal displacement in physics steps, excluding blocked movement, Dodge displacement, hit push and passive falling. Aim counts live control time even when aim direction is unchanged; it is not mouse distance or accuracy.

The export retains `assistedSeconds` for historical Q06 Aim Assist records. The current catalog offers nearest-enemy Auto Aim, which accrues automatic time. Old Aim Assist rules require reselection. For timed operations the denominator includes assisted time; only automatic time enters the automatic numerator. Counted operations use automatic / total successful counts. Empty denominators are N/A. The retained overall percentage is an equal mean of nonempty operation ratios and is only a secondary descriptor, not a player score. Parallel operation durations must not be summed as elapsed play time. Run operation totals pool finalized attempts; `activeAttempt` contains a checkpoint of the ongoing fight separately.

Target Profile time counts toward configuration and the mission entrance (the target card) counts toward preparation. The player-facing **Test Room** uses the internal Training phase. Practice before any numbered mission contributes Training time and player deaths. Numbered attempts and operation totals begin with the actual wave. The Bonus, now reached only through F8 or the Editor's Playtest shortcuts, is outside the record. Stage times use real elapsed seconds and continue under pause (in the pause category); formal combat seconds use the combat clock. F8 time is in pause even outside a fight. Initial/final tuning snapshots and development flags remain. Editor records and flagged attempts must be separated from ordinary playtest samples. The readable Run page exposes the small summary without the obsolete hold rows.

Mission 2 victory sets `levelCleared` and `levelClearSeconds` (cumulative combat seconds), retaining the Alpha definition. Final Boss victory sets `gameCleared`, `gameClearSeconds` and `endReason=complete`. `finished` also applies to early exits. Compare Alpha operations, attempts and deaths using W1/W2 only. The Beta run totals include W3.

Each Final Boss encounter has one attempt. The natural transition stays in that attempt. A direct phase-two retry opens a new attempt with `finalBossStartPhase=2`. The latest pool is `finalBossPhase`, with `finalBossHealth` and `finalBossMaxHealth`. The first pool's exit records `phaseOneExitPlayerHealth` (otherwise -1). `invadedRules` lists successfully installed fight-only rules in order. The initial rule snapshot describes the equipped configuration, including on a direct retry where assistance is sealed.

`phaseOneCombatSeconds` and `phaseTwoCombatSeconds` measure live combat simulation, excluding dialogue holds, the EMP transition, countdowns and pause. They sum to the W3 attempt's `seconds`. `phaseTwoOperations` is the phase-two subset of the attempt's ordinary operation totals. `deathsByWave[3]` counts all Final Boss deaths. Separate phases using the attempts. The Bonus and GHOST remain outside measurement. Developer entries and forced victories carry test flags.

`initialDifficulty` and `tutorialSelection` describe the start of the record. `latestDifficulty` follows the run and each attempt saves the difficulty used for combat. `difficultyChanges` records each effective change with old/new selections, mission, screen, elapsed play time and UTC timestamp, including edits to a saved run from title Settings. `missionTwoClearPlaySeconds` records real play time at the first W2 victory. `guidedTestRoomVisits` counts the required tutorial entry and `freeTestRoomVisits` counts other player entries. Practice respawns add no visits. A continuation from an older file records the resumed difficulty and an unknown original tutorial selection.

## Saving and collection

Each New Game immediately creates `<run-id>.json` under `Application.persistentDataPath/Analytics`. On Windows this is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GO GHOST/Analytics`; on macOS it is `~/Library/Application Support/DefaultCompany/GO GHOST/Analytics`. Use F8 > Analytics > Open data folder. Nicknames are inside the JSON and visible in F8 > Analytics > Run; file paths use only generated IDs, so arbitrary nickname characters cannot affect paths.

Save on character confirmation, configuration changes, phase/pause transitions and fight completion, plus every ten real seconds while a run is active. A Training-only or character-creation-only run still has a file. Menu, a new New Game, ordinary quit, scene closure or completion finalizes the run. An unfinished checkpoint has `finished=false` and may contain an ongoing `activeAttempt`; `activeAttempt.result == "none"` means there is no ongoing fight (JsonUtility cannot write null). Treat an unfinished checkpoint as incomplete after a crash, not a fabricated death. Continue reopens the saved run's record and extends it (`finished=false` again, end reason cleared); resuming a crash checkpoint preserves its active attempt as `abandoned` with reason `interrupted`. Continuing a historical save starts a new schema-5 file linked through `previousRunId`. Historical files retain their original scope (schema 4 through W2, schema 3 with the old duo).

Only JSON is generated for current schema 5. Existing JSON, CSV and older metrics.csv are left untouched. Writes use the existing temporary-file replacement and expose failures in the Console/F8 footer. There is no network upload and no historical-file reloading into the current F8 panel. A later analysis tool can derive tables from the JSON without collecting duplicate files.

## Verification

The optional **Check Game Flow** Editor check checks the Mission 2 milestone, both invasions, a phase-two defeat and retry, and the final exported victory under the original run ID. The [implementation log](ImplementationLog.md) records which versions and checks actually ran.
