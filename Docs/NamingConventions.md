# GO GHOST - Naming Conventions

Use stable game concepts and responsibility names in current source and documentation. Names should remain useful after a prototype milestone or implementation pass ends. For current ownership, see [ProjectMap.md](ProjectMap.md).

## Current source and assets

- Use PascalCase for C# filenames and match the main declared type exactly, including interface prefixes and existing acronyms: `PlayerMovement.cs`, `IDamageable.cs`, `PlayerStatsSO.cs`.
- Name new partial-type files `Type.Feature.cs`, such as `BossEncounterSmoke.BehaviorCatalog.cs`. Keep each suffix specific to that file's responsibility. This pass normalizes the smoke suite; established runtime responsibility names such as `BossTraining.cs`, `DuelTraining.cs` and `DuelConditionalUI.cs` remain deliberate exceptions.
- Prefer established domain names such as Duel, Conditional, Boss, Player and Training over developer names, delivery dates or obsolete milestone labels. Keep responsibility folders such as Scripts, Prefabs, Data and Editor/Playtest.
- Preserve Unity-recognized locations and names such as Assets, Packages, ProjectSettings, Editor, Resources and StreamingAssets, along with package and third-party folder names. Do not rename them for cosmetic consistency. Preserve the formal scene path unless a separately scoped change requires it.
- Match asset names to their current role, for example `DuelCombatAttempt.prefab`. A retained name alone does not establish whether an asset is unused; check its real dependencies.

Rename imported Unity assets and folders through `AssetDatabase` or Unity's Project window so their `.meta` files and GUIDs remain paired. For a case-only rename, use a unique intermediate name if required by the filesystem. Update path constants, build/Editor entries, manifests and documentation, then verify the GUID and serialized references. Do not regenerate metadata or make independent filename-only moves inside Assets.

Naming cleanup must not change public types, namespaces, serialized fields, enum identities or persisted contracts merely to improve appearance. Match filenames to existing types when possible. Changes to those contracts require their own compatibility plan and validation.

## Documentation and history

Current game documents live in `Docs/Game` with stable topic names. Its [README](Game/README.md) identifies current authorities and retained references. Root documentation describes repository organization, collaboration and restoration. Keep historical decisions and validation evidence in their original chronological context.

The recovery snapshot under `Legacy/Assets`, its Unity package and `Legacy/manifest.json` are immutable: preserve names, original metadata and exact bytes. They are excluded from naming cleanup. Historical documents remain historical; navigation links may be updated when current documents move, without rewriting old design or verification claims.

[NamingMigration.csv](NamingMigration.csv) records the naming pass. [StructureMigration.csv](StructureMigration.csv) records the earlier current/archive separation. Use both maps to resolve old paths in historical logs; apply the later naming map when following an intermediate structural path. Older class or asset names in past evidence do not imply that the current project still uses that location.
