# GO GHOST — Start Here

## Current authority — 2026-10-02 end of day

1. Read [AGENTS.md](../../AGENTS.md) / [CLAUDE.md](../../CLAUDE.md) for engineering, Unity and Git rules.
2. Read [Tower_Design_Override.md](Tower_Design_Override.md): **the canonical current design**, incorporating Thomas's EOD decisions and the non-conflicting full-flow v0.7 rules.
3. Read [Narrative_Content_Contract.md](Narrative_Content_Contract.md) for the single runtime JSON, stable bindings, F/input/pause and actual-fact boundary. Exact English draft wording belongs in that JSON, not a second script in documentation.
4. Read the newest [Implementation_Log.md](Implementation_Log.md) entry for what actually ran, assists, mismatches and NOT_RUN items. Use [ProjectMap](../ProjectMap.md) for paths, [TEST profile](Prototype_Test_Profile.md) for provisional numbers and [README](../../README.md) to play.

Current code connects a normal Loadout/practice -> short Tower -> real P1/P2/P3 -> two final F decisions -> A–E flow. S01/S02 are reused, F8 uses the real route/snapshot path, and Assault Rifle is the sole current weapon. Full body-tier design and final Boss/art work are still Open. A design requirement is not proof of implementation; a prototype is not final design approval.

The production interaction/P1 pass replaces fixed dialogue with speaker bubbles, removes all T2 aiming authority, adds T2+ red danger/blue interaction feedback and opens P1 with a tracking charged laser and real chandelier cover. Thomas deferred a replacement for the old 50F observation: the retained P3 selector currently has no normal Restrained trigger. Puppeteer remains `???` until he supplies an introduction. These are explicit current boundaries, not authorization for the next design phase.

No next task is implied by this page. Do only Thomas's authorized scope. For documentation-only work, record implementation differences and do not fix code/assets. Preserve the current worktree; the old GitHub static-review SHA is not a rollback target. Stay on Unity **6000.6.0f1**.

## Status language and verification

- **Confirmed:** Thomas approved the design; **Implemented:** inspected code exists; **TEST:** replaceable tuning.
- **Open:** undecided; **Proposed:** not approved; **Historical:** superseded context.
- Compile, focused Play Mode path and a short core-flow check are the MSP default for code changes. Mark unexecuted checks **NOT RUN**. Controlled invulnerability/positioning/forced outcomes are not balance or physical-input proof.
- Do not rerun the old broad matrix or Build/package without a scoped request. [Playtest_Checklist](Playtest_Checklist.md) is a focused worksheet, not an automatic task queue.

## Historical package

The original [Chinese baseline](GO_GHOST_Design_Baseline_ZH.md), [Macro extract](Macro_Readable_Extract.md), companion spreadsheet and `Prompts/00–08` preserve earlier work. Their single-arena, percentage-ending, old catalogue, report-tree and exhaustive-gate assumptions are **not current instructions**. [Execution_Contract](Execution_Contract.md) retains engineering history; its current authority notice takes precedence. Do not execute its numbered phase queue unless Thomas explicitly requests a reviewed scope.

S00/S01/S02 and prior iteration evidence remains dated in the one Implementation Log. Ignored `Logs/` and `Builds/` do not travel to another machine through Git. Physical Play Mode testing on another computer is separate from local virtual-input evidence.
