# Gameplay Analytics

Thomas, 2026-10-29: one independent record per New Game, identified by the character nickname. No participant numbering or cross-run player account. Schema 4 ends measurement at successful Wave 2 completion and excludes all Wave 3 and Bonus activity. F8 has Tuning / Waves / Analytics, with Fight / Run / Actions / History views.

## What is recorded

| Group | Fields and interpretation |
|---|---|
| Identity and lifecycle | Nickname (`playerName`, blank becomes YOU), character confirmation, unique run/attempt IDs, UTC timestamps, application version, platform, Editor/test flags, finished state and end reason. Identical names never overwrite each other. |
| Time | Real elapsed play time split into character creation, configuration (including Help), Training, preparation, fight, pause/F8 and result time. Categories are mutually exclusive. Combat simulation seconds are retained separately, including failed/abandoned attempts. |
| Rule use | Help opens, committed rule additions/edits/removals/reorders, maximum equipped count, time to first nonempty configuration and first fight, latest readable configuration and each attempt's full initial rule/parameter/order snapshot. Cancelled drafts and hover choices do not count. |
| Encounters | W1/W2 attempt wave, result, reason, combat seconds, player and each present Boss's last/current and maximum HP, per-wave deaths, Training deaths separately, and W2 completion. Missing actors use HP -1. |
| Controls | Actual manual/automatic shots, successful jumps and Dodge starts; effective Move duration; manual and automatic Aim duration, plus the retained assisted-time field. Per-Action ID details and separate drop-through counts remain. |

Character creation records Happy’s starter rule in `latestRules`, sets `maxRulesEquipped` to one and sets `firstRuleAtSeconds`. Only the player’s own confirmed additions increment `rulesAdded`. Sessions from the original Beta 33% download started empty. The refreshed download includes the starter rule.

Names are data, not identifiers. The run ID keeps same-name New Games separate; retries and wave transitions remain attempts within that run. The face drawing is not exported. Rule snapshots and action totals show usage, not comprehension or enjoyment. No per-frame input/position log or damage event stream is collected. Action totals still group by Action ID, not individual rule; snapshots retain each rule's parameters and order.

## Measurement boundaries

Fire counts each actual paid bullet, including every bullet in a committed burst, using its accepted source attribution. Jump counts successful takeoffs; Dodge counts successful starts. Move measures actual ordinary horizontal displacement in physics steps, excluding blocked movement, Dodge displacement, hit push and passive falling. Aim counts live control time even when aim direction is unchanged; it is not mouse distance or accuracy.

Schema 4 retains `assistedSeconds` for historical Q06 Aim Assist records. The current catalog offers nearest-enemy Auto Aim, which accrues automatic time. Old Aim Assist rules require reselection. For timed operations the denominator includes assisted time; only automatic time enters the automatic numerator. Counted operations use automatic / total successful counts. Empty denominators are N/A. The retained overall percentage is an equal mean of nonempty operation ratios and is only a secondary descriptor, not a player score. Parallel operation durations must not be summed as elapsed play time. Run operation totals pool finalized attempts; `activeAttempt` contains a checkpoint of the ongoing fight separately.

Target Profile time counts toward configuration and the mission entrance fade counts toward preparation. The player-facing **Test Room** uses the internal Training phase. Practice before W1 or W2 contributes Training time and player deaths. Numbered attempts and operation totals begin with the actual wave. Bonus Testing occurs after the record has closed. Stage times use real elapsed seconds and continue under pause (in the pause category); formal combat seconds use the combat clock. F8 time is in pause even outside a fight. Initial/final tuning snapshots and development flags remain. Editor records and flagged attempts must be separated from ordinary playtest samples. The readable Run page exposes the small summary without the obsolete hold rows.

Remaining Boss HP measures encounter progress. W2 victory sets `levelCleared` and finishes the record with `endReason=complete`.

Wave 3 configuration, Testing, dialogue, combat and the entire Bonus flow are outside measurement: no visit/dialogue/choice events, time, configuration/Help counters, attempts, operations, health, deaths or results. The completed W2 JSON remains unchanged through Yes/No, Bonus pause, retry and victory. Neither stage starts another record. A developer jump to either unmeasured stage ends an existing test record before entry. The old four-slot `deathsByWave` shape remains compatible; slot 3 is unused in schema 4. Old schema 3 files retain their original W3 duo interpretation and are never rewritten.

## Saving and collection

Each New Game immediately creates `<run-id>.json` under `Application.persistentDataPath/Analytics`. On Windows this is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GO GHOST/Analytics`; on macOS it is `~/Library/Application Support/DefaultCompany/GO GHOST/Analytics`. Use F8 > Analytics > Open data folder. Nicknames are inside the JSON and visible in F8 > Analytics > Run; file paths use only generated IDs, so arbitrary nickname characters cannot affect paths.

Save on character confirmation, Help/configuration changes, phase/pause transitions and fight completion, plus every ten real seconds while a run is active. A Training-only or character-creation-only run still has a file. Menu, a new New Game, ordinary quit, scene closure or completion finalizes the run. An unfinished checkpoint has `finished=false` and may contain an ongoing `activeAttempt`; `activeAttempt.result == "none"` means there is no ongoing fight (JsonUtility cannot write null). Treat an unfinished checkpoint as incomplete after a crash, not a fabricated death. Checkpoints limit the potential loss window but do not provide resume/recovery gameplay.

Only JSON is generated for current schema 4. Existing JSON, CSV and older metrics.csv are left untouched. Writes use the existing temporary-file replacement and expose failures in the Console/F8 footer. There is no network upload and no historical-file reloading into the current F8 panel. A later analysis tool can derive tables from the JSON without collecting duplicate files.

## Verification

The optional **Check Game Flow** Editor check compares the completed W2 JSON again after entering Bonus. The [implementation log](ImplementationLog.md) records which versions and checks actually ran.
