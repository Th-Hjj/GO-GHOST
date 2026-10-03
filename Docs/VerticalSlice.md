# GO GHOST — Design Overview and MSP Strategy

The canonical current design is [Tower_Design_Override](GO_GHOST_VS/Tower_Design_Override.md), confirmed by Thomas on **2026-10-02 end of day**. Read [START_HERE](GO_GHOST_VS/START_HERE.md) for authority and [Implementation_Log](GO_GHOST_VS/Implementation_Log.md) for evidence. This page is an overview, not a competing baseline.

GO GHOST is a single-player 2D side-view short-tower infiltration. Power increases as the officer gives a self-aware Inner AI more body authority. Competent AI execution can conflict with the human's present wish. Actions, sparse dialogue and real control transfer carry the theme; body percentage is not morality or an ending selector.

Loadout -> practice -> entrance -> 1F Maintenance AI -> P1 -> 50F Receptionist/Security -> P2 -> 99F Security -> CEO office -> P3 -> separate CEO/core F dispositions -> ending. Keep one officer/locked snapshot through a run, distinguish local encounter defeat from mission completion, and start a new officer from the opening after death. No checkpoints.

The current greybox connects this normal route using S01/S02 and the authorized draft JSON, including the explicit Chinese President summon correction. F starts/advances nearby interaction; a sole player response plays directly without choice UI. 99F waits for facing nearby F or an actual attack. F8 is developer-only route/tier/existing-upgrade access. The active weapon is Assault Rifle only. Current independent upgrade options remain prototypes; the six body-area cumulative T1/T2/T3 plus Hidden table is **Open**.

P3 selection and the five final combinations are defined exactly in the canonical document. The living Maintenance AI/reset-cancel plus real 50F T2 veto and living Receptionist select restraint; all other inputs select Full Force. No kill-count or percentage scoring. The unintended denial/normal Overdrive trigger is removed without replacement. Actual CEO/core deaths auto-commit their disposition; final choices/state complete once, with a warning before D and living-CEO prerequisites for Control. Ordinary P3 carrier defeat preserves these choices.

## Development strategy

Target **MSP: functional completeness > development speed > playability > robustness > polish**. Reuse the existing input, aim, shooting, snapshot, pause, route and Archive systems. Do not add generic managers/frameworks or redesign the body catalogue because the flow can now run.

For an authorized code increment: compile, exercise the changed normal path and check the affected continuation/cleanup. Fix compile failures, crashes, softlocks, unusable requested features and core regressions. Record balance, rough greybox art, final cinematic/audio, rare harmless edge cases and untested hardware separately. Do not routinely repeat historical suites or produce a build. A skilled unaugmented victory remains a design goal, not a reason to soften Bosses to satisfy a test driver.

For documentation-only work: update confirmed design, mark Open details and observed implementation mismatches, and stop without gameplay or asset edits. Current mismatches include independent prototype upgrades, incomplete responsive Boss/cinematic design and legacy blue terminal/Case Closure presentation. See the status section in the canonical document and latest log.

Historical single-arena rules, old percentage endings, weapon tables and earlier UI/phase descriptions are superseded. Their evidence remains in dated Implementation Log entries and the original historical package. They are not an instruction to undo today's work.
