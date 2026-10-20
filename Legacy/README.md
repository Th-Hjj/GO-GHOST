# GO GHOST - Optional historical archive

This entire directory is outside Unity's Assets and Packages roots. The current game does not import, compile or load it and can run without it.

- `Assets/_Legacy/`: readable snapshot of Tower, PolicePrototype and Prototypes, including all original `.meta` files. Internal README text describes the snapshot's previous imported state.
- `Docs/`: superseded design, planning, prompts and maps.
- `GO-GHOST-Legacy.unitypackage`: original Unity export, recursively including only historical assets (no current dependencies).
- `manifest.json`: SHA-256 values for the package and 248 source/metadata files, plus 140 asset/folder GUIDs.

Treat the snapshot as immutable. Editing the readable mirror does not update its package; verification will reject a mismatch. The recovery package is tracked through the repository's existing Git LFS rules.

Use [LegacyRestoration](../Docs/LegacyRestoration.md) for explicit restoration and verification. Importing the package restores `Assets/_Legacy` and intentionally makes historical scripts compile again. Current Shared compatibility is a retained dependency of the historical snapshot; this archive is not a standalone old Unity project.

Deleting this directory removes local recovery material, not current game dependencies. Recover the complete directory from a repository revision containing this archive or a separate backup, including its Git LFS package content. The Windows playtest ZIP contains only the playable game, not this archive.
