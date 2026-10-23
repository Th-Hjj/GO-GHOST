# Gameplay Analytics

Local playtest telemetry, approved 2026-10-22. F8 opens **Tuning / Waves / Analytics / Presets**; F1 has no analytics binding. Opening F8 pauses combat; closing restores the prior pause state. Analytics provides Fight, Run, Actions and paginated History views. Actions shows executed action IDs for the current or last fight; JSON also retains aggregate action details.

## Measurement

- Fire counts actual paid shots; Jump counts accepted takeoffs; Dodge counts successful starts. Manual and automatic sources are independent per operation. The Revolver freezes source attribution when a burst accepts: every RMB round stays Manual and every F02 Short Burst round retains F02 credit, even while other rules or input change. Each paid bullet counts once; a burst is never counted as one shot.
- Move measures successful ordinary horizontal movement in physics steps, attributed to the input that produced that step. Blocked movement, Dodge displacement, knockback and passive falling do not count as Move.
- Aim measures time under the final manual or automatic aim authority, including an unchanged aim direction. It does not measure mouse motion or aim accuracy.
- Automatic Hold Position / Hold Fire durations and manual / automatic drop-through counts are separate. Drops do not fabricate jumps. Shared Move ownership during Move to Platform is attributed to J04.
- For Move/Aim, automation = automatic seconds / total seconds. For Jump/Fire/Dodge, automation = automatic count / total count. The overall percentage is the equal mean of operations with nonzero denominators. An unused operation displays N/A. Holds and drops do not enter this percentage. It describes automation of measured actions, not the percentage of the entire game played without human input.
- Run totals pool raw counts/durations across attempts before calculating ratios. They do not average attempt percentages. Parallel operations have separate durations; their seconds must not be summed as wall-clock time.
- Combat time excludes training, setup, preparation and pause. It includes failed retries and abandoned fights. Deaths are per wave; Duo does not identify the killing boss.

## Lifecycle and files

A run begins at New Game; retries and wave transitions retain it. Each attempt records its own ID, wave, result, UTC timestamps, rule snapshot and initial/final tuning. Wins and deaths finalize exactly once. Returning to the menu, starting another encounter, New Game and ordinary shutdown finalize an open attempt as `abandoned`, including a preparation cancellation with zero combat seconds. Training never opens an attempt. Domain Reload disabled is supported through explicit session reset and event cleanup.

Each run writes matching `<run-id>.json` and `<run-id>.csv` under `Application.persistentDataPath/Analytics`. On the current Windows project this is `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GO GHOST/Analytics`. F8 Analytics includes **Open data folder**. Existing `metrics.csv` files are retained untouched.

JSON schema version 2 contains all attempts, per-action details and a run aggregate. `hasData` / `hasAutomationData` distinguishes N/A from a measured 0%; the numeric field is zero when absent. CSV contains one row per finalized attempt plus a `run_summary` row, with raw values and percentages for all five operations. Empty percentage cells mean N/A. Summary death columns cover the run. IDs join rows to full JSON records. Floating-point output uses invariant decimal formatting; the screen rounds for readability.

Both files are generated from the same finalized in-memory records and individually replaced via temporary files. Saving happens after each result/abandonment and on ordinary shutdown, not every frame. Failures appear in the Console and Analytics footer. A crash or forced process termination can lose the active attempt; this is not a crash-recovery system. Historical files are not reloaded into a new run's panel.

`editor` identifies Editor sessions. `testData` identifies attempts using developer wave jump or tuning edits, and explicitly marked smoke fixtures. `developerWaveJump` and `tuningChanged` retain the reason; initial nondefault tuning also marks an attempt. Run totals include these attempts but propagate the test flag, so formal playtest analysis should filter them. Simply opening F8 does not mark a run as test data. No network upload is used.

## Verification

**GO GHOST > Playtest > Analytics** exercises real action executors with virtual input, focused lifecycle and F8 checks, file round trips and short live W1/W2/W3 samples. Controlled placements/deaths/completion establish mechanisms and flow only. See the newest ImplementationLog entry for actual results and untested limits.
