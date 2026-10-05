# GO GHOST — Start Here

The current confirmed design is [Legacy_Combat_Implementation_Spec.md](Legacy_Combat_Implementation_Spec.md), the confirmed 2026-10-05 player AI override followed by the retained 2026-10-03 restoration R01–R22. It replaces conflicting Duel rules, tower routes, upgrades, narrative contracts and staged tasks. The 2026-10-05 task explicitly prohibits push, merge and deploy.

Read [AGENTS.md](../../AGENTS.md) / [CLAUDE.md](../../CLAUDE.md), the specification and the newest [Implementation_Log.md](Implementation_Log.md) entry. [README](../../README.md) is the run guide. Unity stays **6000.6.0f1**, with existing packages.

Restore actual legacy free-aim Rifle combat, original actors and five-platform arena; keep only the three-permission, three-wave outer loop. The earlier Duel executor is superseded. Do not equate design requirements, source implementation and actual runtime evidence.

Validation is narrow: compile, core action/module/flow checks, one mixed and one normal all-AI combat smoke and Windows build/launch. No permission win matrix, manual-win gate, batch simulation or new test platform. Win rate is not an acceptance gate. Prior Duel combat-result evidence is historical, not evidence for this restoration. Implementation_Log remains the only progress log.