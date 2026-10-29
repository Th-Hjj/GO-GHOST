# GO GHOST - Project Map

Last reviewed: 2026-10-29; structure established 2026-10-20. Gameplay authority: [game documentation](Game/README.md). Actual verification and limits: [implementation log](Game/ImplementationLog.md). Current naming rules: [NamingConventions.md](NamingConventions.md).

## Entry and current ownership

Only enabled build scene and default Editor Play entry: `Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity`. Open it with **GO GHOST > Combat > Open Arena**. Unity remains 6000.6.0f1 with existing packages and default assemblies.

| Folder | Contents |
|---|---|
| `Assets/_Game/Levels/L01_Duel/Scenes` | Formal Duel scene |
| `Assets/_Game/Levels/L01_Duel/Scripts` | DuelGame lifecycle/Training/Final Boss/Bonus, character creation, conditional rules, Help, metrics, Boss executors/navigation/coordination |
| `Assets/_Game/Levels/L01_Duel/Prefabs`, `Data`, `Art`, `Audio` | Actual combat prefab, Bosses/profiles, materials and licensed wave music |
| `Assets/_Game/Shared/Scripts/Player` | Existing input, movement, jump/drop-through, Dodge, Revolver (retained Rifle APIs), projectile, health, visual and WeaponProfile |
| `Assets/_Game/Shared/Scripts/Presentation` | Shared visual/audio helpers, BodyDiagramGeometry, MissionBodyRegion and MissionParticleBrain |
| `Assets/_Game/Shared/Scripts/Compatibility` | Retained serialized/API contracts and optional components used by original executors; [details](../Assets/_Game/Shared/Scripts/Compatibility/README.md) |
| `Assets/_Game/Shared/Prefabs`, `Data`, `Audio`, `Fonts`, `Presentation` | Reused player/projectile, physics/data, sounds, fonts and presentation assets |
| `Assets/_Game/Editor/Build` | DuelBuild: open formal scene, set Play entry and explicit build commands |
| `Assets/_Game/Editor/Playtest` | Current wave entries and bounded behavior, training, platform, UI and combat checks |
| `Assets/_Game/Editor/Authoring` | Current Boss/presentation authoring tools |

`ConditionalThreatGeometry` contains the physical threat prediction required by current rules. The old autonomous `LegacyPlayerAI` remains archived. Current body UI shares the extracted contour/pulse components and drawn face; the retained particle brain is inactive in formal configuration. It does not depend on historical MissionUI screens. The current combat prefab is `DuelCombatAttempt.prefab`; Boss assets are `Gunner` and `Swordsman`, with their existing GUIDs and enum values preserved.

## Archive and dependency direction

| Folder | Preserved content |
|---|---|
| `Legacy/Assets/_Legacy/Tower` | Cyberpunk World, old CombatAttempt, tower flow/UI/story/save systems, encounters and TopDown checks |
| `Legacy/Assets/_Legacy/PolicePrototype` | Cyberpsycho/Police policies, old autonomous player AI, retired Alpha/policy validation and old scene/conversion authoring |
| `Legacy/Assets/_Legacy/Prototypes/Thomas`, `Walker` | Personal scenes and Walker's original Cyborg asset folders |
| `Legacy/Assets/_Legacy/Prototypes/SharedPlayer` | Retired ability/upgrade/save experiments and old loadout draft/archive helpers |
| `Legacy/Docs/Tower`, `PolicePrototype`, `Planning` | Superseded design, maps, execution prompts and delivery notes |

Current runtime sources and serialized assets must not acquire references to archive-owned types/assets. Legacy can reuse current Shared code/contracts. Current Editor authoring declares optional partial hooks whose archive implementations support historical conversions/scenarios. TopDown callbacks leave current Duel input untouched; historical scene input recovery is scoped to those sessions.

The root `Legacy/` directory is outside both Assets and Packages. It is not imported, compiled or loaded by the current game and may be removed entirely. The Shared compatibility contracts remain current source because the actual retained executors still use them. The optional Editor partial hooks have no implementation until historical assets are explicitly restored.

The archive contains a readable `Assets/_Legacy` mirror, all original metadata, historical documents, an original Unity export package and a SHA-256/GUID manifest. [LegacyRestoration](LegacyRestoration.md) describes verification, conflict checks and explicit restoration. The archive recovery Editor commands are inert unless invoked; the current game never reads the archive.

## Location and restoration

[StructureMigration.csv](StructureMigration.csv) records original/current asset paths and unchanged GUIDs, plus document moves. Original metadata is preserved in the archive and Unity export package. Imported asset renames used AssetDatabase; exclusion used a reversible ignored-folder rename, followed by moving the unimported files out of Assets. Use the map for old historical-log paths. Restore through the verified Unity package, not by generating new metadata. Extracted helper classes have new Unity-generated metadata.

[NamingMigration.csv](NamingMigration.csv) records the subsequent filename and document-folder cleanup. Follow it after the structural map when resolving intermediate names in older logs. [NamingConventions.md](NamingConventions.md) defines current naming and the public/serialized contracts and immutable archive content that renames must preserve.

Settings, packages, Unity version, player tuning, profiles, scene/prefab serialization and existing saved games are preserved. Build scene paths changed to L01_Duel. Generated `Library`, `Temp`, `Logs`, builds and IDE output remain ignored local data; do not put them in the source archive.

`LegacyArchive.cs` in Editor/Authoring provides explicit Verify Snapshot, Restore Historical Assets, Complete and Verify Restoration, and Verify Restored Assets commands. The completion step preserves empty-folder identities skipped by Unity's importer. Removing `Legacy/` does not affect compilation or normal game startup; those maintenance commands report its absence when invoked.

New development belongs in the current responsibility folder. Superseded material goes into the appropriate Legacy category with provenance; do not recreate personal development folders in the current game. The [historical map](../Legacy/Docs/Tower/ProjectMap.md) is retained for context only.
