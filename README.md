# GO GHOST

A single-player, fixed-camera 2D duel across three police waves: basic, skilled, and adaptive/deceptive. Delegate Legs, Arms and Brain independently to a stable built-in AI, or retain their controls yourself. USC CTIN 489 — Thomas Hong and Walker Grupp.

## Open and play

1. Open this project in **Unity 6000.6.0f1**. Keep the existing packages and URP 2D renderer. Thomas uses Rider.
2. Open `Assets/_Game/Levels/L02_Duel/Scenes/GO GHOST Duel.unity`, the only enabled build scene, and press Play.
3. Choose **New Game**, configure three permissions, then **Start Fight**. New games default all manual. Each fight locks its choices after a one-second preparation.
4. Victory advances to the next configuration; defeat offers reconfiguration for the same wave. Both sides reset resources and observation on each start. Third-wave victory opens Complete. No save progression.

| Module | Manual controls |
|---|---|
| Legs | A/D move; Space jump; Left Shift ground dodge (A/D direction, otherwise away from police) |
| Arms | Mouse in the fixed left/right screen half chooses attack direction; height is ignored |
| Brain | Fresh LMB: shot; RMB: intercept; MMB: ground wave. Repeat the same cancellable attack button within its window to cancel |
| Pause | Escape: Resume or Main Menu |

AI ownership disables that module's combat inputs. There is no weapon switching, barrier, manual reload, temporary override, tower route, Archive or upgrade screen. Colors and UI follow the black/white/gray whitelist.

## Development and evidence

`DuelRules.cs` holds shared physical/action values. `DuelFighter` executes both bodies; `DuelControllers` separates built-in execution from police tactics using public snapshots. Projectile sweep contacts and actual melee overlap cause damage. Police observe comparable expressed reactions, commit real/feint plans before windup, and pay normal cancellation/recovery costs.

In Play Mode, **GO GHOST > Duel > Observe Third Wave** starts the permitted all-AI diagnostic with normal third-wave HP, spawns, resources and empty memory. It has no retail menu entry. Diagnostics are otherwise off. **GO GHOST > Duel > Build Windows x64** produces `Builds/Windows/GO-GHOST.exe`.

See the newest [implementation log](Docs/GO_GHOST_VS/Implementation_Log.md) for actual run/build evidence and limits. The [2026-10-03 specification](Docs/GO_GHOST_VS/Duel_Implementation_Spec.md) is authoritative. Old tower scenes, prefabs and code retain their GUIDs outside the active build/startup path because their serialized dependency graph is still interconnected. They are historical assets, not a second supported game. Existing Archive files are not read or modified by the duel.
