# Gameplay Analytics

Thomas, 2026-10-29: one independent record per New Game, identified by the character nickname. No participant numbering or cross-run player account. Schema 3 extends the existing local collector; F8 has Tuning / Waves / Analytics, with Fight / Run / Actions / History views.

## What is recorded

| Group | Fields and interpretation |
|---|---|
| Identity and lifecycle | Nickname (`playerName`, blank becomes YOU), character confirmation, unique run/attempt IDs, UTC timestamps, application version, platform, Editor/test flags, finished state and end reason. Identical names never overwrite each other. |
| Time | Real elapsed play time split into character creation, configuration (including Help), Training, preparation, fight, pause/F8 and result time. Categories are mutually exclusive. Combat simulation seconds are retained separately, including failed/abandoned attempts. |
| Rule use | Help opens, committed rule additions/edits/removals/reorders, maximum equipped count, time to first committed rule and first actual fight, latest readable configuration and each attempt's full initial rule/parameter/order snapshot. Cancelled drafts and hover choices do not count. |
| Encounters | Attempt wave, result, reason, combat seconds, player and each present Boss's last/current and maximum HP, per-wave deaths, Training deaths separately, W3 completion, seven-minute reminder state and explicit W1 skip flag. Missing actors use HP -1. |
| Controls | Actual manual/automatic shots, successful jumps and Dodge starts; effective Move duration; manual, assisted and automatic Aim duration. Per-Action ID details and separate drop-through counts remain. |

Names are data, not identifiers. The run ID keeps same-name New Games separate; retries and wave transitions remain attempts within that run. The face drawing is not exported. Rule snapshots and action totals show usage, not comprehension or enjoyment. No per-frame input/position log or damage event stream is collected. Action totals still group by Action ID, not individual rule; snapshots retain each rule's parameters and order.

## Measurement boundaries

Fire counts each actual paid bullet, including every bullet in a committed burst, using its accepted source attribution. Jump counts successful takeoffs; Dodge counts successful starts. Move measures actual ordinary horizontal displacement in physics steps, excluding blocked movement, Dodge displacement, hit push and passive falling. Aim counts live control time even when aim direction is unchanged; it is not mouse distance or accuracy.

Q06 Aim Assist is stored as `assistedSeconds`, separately from fully automatic Aim. For timed operations the denominator includes assisted time; only automatic time enters the automatic numerator. Counted operations use automatic / total successful counts. Empty denominators are N/A. The retained overall percentage is an equal mean of nonempty operation ratios and is only a secondary descriptor, not a player score. Parallel operation durations must not be summed as elapsed play time. Run operation totals pool finalized attempts; `activeAttempt` contains a checkpoint of the ongoing fight separately.

Training records time and player deaths, not numbered-wave attempts or combat-operation totals. Stage times use real elapsed seconds and continue under pause (in the pause category); formal combat seconds use the combat clock. F8 time is in pause even outside a fight. Initial/final tuning snapshots and development flags remain. Editor records and flagged attempts must be separated from ordinary playtest samples. The readable Run page exposes the small summary without the obsolete hold rows.

Remaining Boss HP measures encounter progress, not damage caused by the player: Boss friendly fire exists. W3 victory sets `levelCleared`; the separate skip flag and attempt history distinguish natural progression from assistance. The reminder's `shown` / `no` / `yes` is recorded even when no W1 attempt exists. Skipping an active W1 attempt records `skipped`, never a death/win or developer jump.

## Saving and collection

Each New Game immediately creates `<run-id>.json` under `Application.persistentDataPath/Analytics`. On the current Windows project this is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GO GHOST/Analytics`. Use F8 > Analytics > Open data folder. Nicknames are inside the JSON and visible in F8 > Analytics > Run; file paths use only generated IDs, so arbitrary nickname characters cannot affect paths.

Save on character confirmation, Help/configuration changes, phase/pause transitions and fight completion, plus every ten real seconds while a run is active. A Training-only or character-creation-only run still has a file. Menu, a new New Game, ordinary quit, scene closure or completion finalizes the run. An unfinished checkpoint has `finished=false` and may contain `activeAttempt`; treat it as incomplete after a crash, not a fabricated death. Checkpoints limit the potential loss window but do not provide resume/recovery gameplay.

Only JSON is generated for schema 3. Existing JSON, CSV and older metrics.csv are left untouched. Writes use the existing temporary-file replacement and expose failures in the Console/F8 footer. There is no network upload and no historical-file reloading into the current F8 panel. A later analysis tool can derive tables from the JSON without collecting duplicate files.

## Verification

**GO GHOST > Playtest > Help and Named Metrics** checks actual help/draft UI, nickname round trips, same-name run isolation, Training-only records, configuration counters, checkpoints, pause, real manual/assisted controls, endpoint HP, retry, reminder skips and short W1/W2/W3 flow. Controlled damage/completion proves recording and transitions, not balance. See [ImplementationLog.md](ImplementationLog.md) for the executed evidence and limitations.
