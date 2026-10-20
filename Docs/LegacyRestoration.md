# Legacy archive and recovery

The optional root `Legacy/` directory contains historical files outside Unity import and compilation. It is not a UPM package, Resources folder, StreamingAssets folder or runtime data source. Removing it does not affect the current game. Keep `Assets/_Game`, Settings, Packages and ProjectSettings, including the current Shared compatibility contracts.

## Restore the saved snapshot

1. Recover the complete `Legacy/` directory from a repository revision containing the archive, or from a separate backup, if it was deleted. Fetch its Git LFS package content with `git lfs pull --include="Legacy/GO-GHOST-Legacy.unitypackage"`. A ZIP containing only the playable Windows build does not include recovery material.
2. Use the same Unity 6000.6.0f1 and existing packages. Stop Play Mode and save open scenes.
3. Select **GO GHOST > Legacy Archive > Verify Snapshot**. It checks package and readable-mirror SHA-256 values against `Legacy/manifest.json`.
4. Select **GO GHOST > Legacy Archive > Restore Historical Assets**. It refuses an existing `Assets/_Legacy` destination or any imported GUID conflict, then imports the original Unity package. It does not overwrite current Shared assets or alter Play/build entries.
5. Unity 6000.6 may display **Missing signature** even for this noninteractive import. This locally exported package has no publisher signature. Review the prompt in Unity and choose whether to import or cancel; automation must not dismiss this security prompt. SHA-256 verification establishes snapshot identity, not a publisher signature. A pending call does not prove completion; do not start another restore while the prompt is open.
6. Wait for normal compilation, then run **Complete and Verify Restoration**. Unity skips empty folders during package import; this command validates the imported files and restores only missing original folder metadata from the verified mirror, refusing conflicts. It then checks every restored file and GUID. **Verify Restored Assets** can subsequently repeat the read-only check. Historical menus/callbacks become available again. The formal scene remains `Assets/_Game/Levels/L01_Duel/Scenes/GO GHOST Duel.unity`.

The package includes 97 historical C# files and their original folder/asset metadata, including empty folders. It excludes current dependencies; preserved historical code uses retained Shared contracts and optional Editor partial hooks in this project. Restoration proves file/GUID/compile recovery, not that every old scene is playable. Known historical missing references and superseded test assertions remain historical.

## Return to the normal isolated state

Preserve any changes made after restoration before excluding it again; the immutable snapshot cannot save new work automatically. Use Unity's AssetDatabase to rename `Assets/_Legacy` to `Assets/_Legacy~`. Unity ignores folders ending in `~`; wait for compilation and confirm no Legacy paths remain in Editor/Player compilation inputs. Once it is no longer imported, move that excluded folder outside Assets/Packages as ordinary archive data. The root folder metadata is also preserved in the recovery package and readable mirror.

For the saved snapshot, run verification before and after this operation. Do not delete current Shared compatibility files or regenerate their GUIDs. Never automatically restore historical assets at Editor startup or as part of a build.

## Evidence and boundaries

The latest [Implementation_Log](Game/ImplementationLog.md) records actual compilation, recovery and archive-absent gameplay checks. [ProjectMap](ProjectMap.md) describes current ownership; [StructureMigration.csv](StructureMigration.csv) locates original files.

The normal project keeps Legacy absent from Assets. Restoring it explicitly opts back into historical compilation. No asmdef/package/version changes are required. Scripts-only player compilation is separate from a full executable build.

Unity references: [hidden assets and ignored folder names](https://docs.unity3d.com/6000.0/Documentation/Manual/SpecialFolders.html#HiddenAssets), [ExportPackageOptions](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ExportPackageOptions.html), [ImportPackage](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AssetDatabase.ImportPackage.html).
