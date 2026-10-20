# GO GHOST - Legacy

Preserved historical content, classified on 2026-10-20 with Thomas's approval:

- Tower: tower scenes, encounters, story/persistence/UI and TopDown development checks.
- PolicePrototype: retired Police/Boss/player AI, old authoring and validation.
- Prototypes/Thomas and Prototypes/Walker: personal scenes and original Walker asset folders.
- Prototypes/SharedPlayer: retired ability, upgrade, save and draft/loadout experiments.

The formal game is `Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity`. Only that scene is enabled in Build Settings and set as default Play entry. Old scene generation writes into PolicePrototype/Generated and cannot replace the formal scene or Play/build entries.

Legacy remains imported and compiled in the existing Unity assemblies. Some contracts and optional components shared with retained executors live in `_Game/Shared/Scripts/Compatibility`. Editor partial hooks retain explicit historical authoring/scenario support. This folder is not yet independently removable or excluded from builds by its name.

Keep original GUIDs and public/serialized APIs. Use Unity AssetDatabase for future moves. Do not run historical tests as current gameplay acceptance. No guarantee is made that old scenes are fully playable; preexisting missing references and saved data are preserved.

See `Docs/ProjectMap.md`, `Docs/StructureMigration.csv` and `Docs/Legacy` for locations and provenance.
