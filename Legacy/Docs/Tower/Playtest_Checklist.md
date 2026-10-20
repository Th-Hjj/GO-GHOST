# GO GHOST — Focused Flow / Playtest Worksheet

> Historical reference. Superseded by [current authority](../../../Docs/Game/README.md). Paths in historical prose describe their original delivery; see the [migration map](../../../Docs/StructureMigration.csv) for current locations.

Current behaviour: [Tower_Design_Override](Tower_Design_Override.md). Numbers: [Prototype_Test_Profile](Prototype_Test_Profile.md). **This is a blank worksheet, not new test evidence or a task to rerun every path.** Executed results belong only in [Implementation_Log](../../../Docs/Game/ImplementationLog.md).

Record commit, machine, Unity version, tester and any assistance. Use PASS / FAIL / NOT RUN / BLOCKED honestly. Forced kills, invulnerability, repositioning and virtual input establish specific mechanisms/flow, not balance or hardware usability. Use an isolated Archive for fixtures. Assault Rifle is the only active weapon; no retired weapon requirements.

| Focused path | What to check | New manual record |
|---|---|---|
| Normal continuous run | Loadout/practice -> actual fights -> both final F decisions -> Result/ending/Main Menu; same officer/snapshot, no room refills or DEV completions | NOT RUN |
| F / ReplyOrAttack | Single nearby F, fresh presses, cancellation distinct; sole response directly follows; 99F waits for F/actual attack; tier 0 omits Inner AI | NOT RUN |
| 1F | Short real walk/reset, later greeting, cancellation and physical death; untouched unresolved; no exit softlock | NOT RUN |
| P1 permissions / cover | Player reply -> ??? silence -> tracking charged laser; T1 advice, T2 manual aim + red danger/blue outlines, T3 one paid real shot; fallen cover blocks laser/both bullet owners and supports climbing | NOT RUN |
| 50F T2 / T3 | F call before guard, manual T2, real T3 ammo/reload/projectile/fall; actual causality and no scripted NPC death. Replacement T2 observation rule explicitly deferred | NOT RUN |
| P3 rule | Existing selector retained; normal Restrained has no runtime producer until Thomas decides the replacement 50F observation. No substitute route or deleted denial trigger | BLOCKED: design deferred |
| Final local setups | A–E through two F decisions, Control prerequisite, D cue in either order, actual death auto-commit without corpse F, once-only completion | NOT RUN |
| Lifecycle | Mid-run death/new officer, F8 during pause/aim, restart/full restart, default skipped facts, no DEV Archive pollution | NOT RUN |
| Content / UI | Exact JSON lookup including President summon, entrance placeholder and P1 silence; speaker bubbles follow/clamp correctly; You / Voice in your head / ???; W/S/F and hover/click; no single-choice menu or leaked shots | NOT RUN |

Select only checks relevant to the authorized change. Compile and a short affected continuation are the normal code gate. No old exhaustive phase matrix, standalone Build, every-upgrade campaign or generic harness required. For a documentation-only update, verify authority/links/diff and do not run gameplay tests.

Later human review can assess AI usefulness, control conflict, readable physical consequences and unassisted Boss difficulty. Preserve sparse storytelling; do not answer player confusion by automatically adding explanatory dialogue/HUD. Body tiers, final art/voice and responsive move redesign require their own approved scope.
