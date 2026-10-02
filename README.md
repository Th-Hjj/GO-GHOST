# GO GHOST

A single-player 2D side-view action/shooting game about a short corporate-tower infiltration. An officer gains power by granting a self-aware Inner AI authority over the same body; human and AI priorities can conflict. Death ends that officer's run, and a new officer starts from the opening. USC CTIN 489, Thomas Hong and Walker Grupp.

## Open and play

1. Use Unity **6000.6.0f1**, Git and Git LFS (`git lfs install` once per machine). Do not change Unity version. Thomas uses Rider.
2. Pull the approved `main` state with Unity closed and local work preserved; open the repository root through Unity Hub. Each machine imports its own `Library/`.
3. Open `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity` and press Play. Launch stops at **Loadout**; choose existing equipment, then **BEGIN OPERATION** to seal the snapshot and enter practice.
4. Walk through the practice door, then the short Tower route. Room exits open when the required fight is won. P3 defeat leads to two separate final decisions; complete both to reach Result, Continue to the JSON ending, then F to Main Menu. Death -> Result -> Continue -> Main Menu -> Start Mission -> new Loadout/practice. No floor checkpoint.

Route: practice -> entrance worker -> 1F Maintenance AI -> P1 -> 50F Receptionist/Security -> P2 -> 99F Security -> CEO office -> P3 -> final disposition -> ending. The same officer, Player and locked snapshot persist between rooms; only the practice door refills resources.

| Input | Current behaviour |
|---|---|
| A/D, mouse, LMB | Move, aim, fire Assault Rifle; reload automatically |
| Space / Left Shift | Jump and equipped leg ability / Dodge or equipped Air Dash |
| E / Q | Existing Arm-Blade slash/deflect / equipped prototype Time Slow |
| F | Near the single overhead F: interact; advance/close dialogue |
| W/S then fresh F, or hover + left-click | Select/commit a dialogue or final disposition choice |
| Left-click | Advance an ordinary dialogue page; a held click never advances repeatedly |
| Escape | Cancel a voluntary interaction/choice |
| F8 | Editor/Development-only route testing |

Boss ReplyOrAttack windows accept real manual shots; ordinary dialogue pauses gameplay. Tier 0 has no Inner AI; T1 advises; T2 temporarily aims for three seconds but allows no-fire refusal; T3 really aims/fires through ammo/reload/projectiles. Old held Fire cannot leak out of dialogue. The existing GameSession Inspector permission configuration is frozen at deployment; F8 can select all four tiers for testing.

Assault Rifle is the only current weapon in normal/F8 loadouts. In 50F, approach the central Receptionist beneath the chandelier and finish the F radio conversation to bring in Security; actual pre-call death also starts it. Defeat Security to reach P2. Chandelier release, fall, cover, standing surface and lethal contact are physical, with Player/InnerAI causality. No NPC death or chandelier use is required to win the guard fight.

## Developer jump

In the **Game view during Play**, press F8. Choose Practice, Tower Entrance, 1F, P1, 50F, P2, 99F, CEO Office, P3 or Final Disposition; choose 0/T1/T2/T3 and cycle currently implemented upgrade slots.

- **Jump to selected node:** fresh tagged run with the selected snapshot, entering that actual node.
- **Restart Current Node:** apply the selected equipment/tier to a fresh instance of this node.
- **Restart Full Run:** restore normal defaults/configured tier and return to Loadout, then normal deployment/practice.

Closing F8 without jumping resumes the underlying interaction. Jump/restart clears old dialogue/pause/aim/projectiles and skipped facts remain unknown. Locked prototype equipment may be tested without granting unlocks. DEV attempts never write normal Archive records/endings. Release configuration contains no F8 menu/input/jump API; this is not a player feature. On another machine, use the same commit, wait for compilation, open the correct scene and focus Game view before testing.

## Current status and boundaries

The S01/S02 foundation and subsequent full English greybox route are implemented. Normal completion stores explicit A–E results; body percentage does not choose P3/endings. The full inherited body-tier catalogue and responsive final Boss redesign are not implemented. P2/P3 cinematics, art and voice remain rough. Existing blue terminal screens and legacy two-card Case Closure presentation remain mismatches; new endings play at completion but are not yet browsable in those old cards.

See [Implementation_Log](Docs/GO_GHOST_VS/Implementation_Log.md) for actual compile/Play Mode evidence, controlled-test assists and NOT_RUN items. No new Windows build/package was produced for this flow. Ignored `Logs/` and `Builds/` do not transfer through Git; older executables do not represent current source.

## Documentation and layout

- [START_HERE](Docs/GO_GHOST_VS/START_HERE.md): authority/read order.
- [Tower_Design_Override](Docs/GO_GHOST_VS/Tower_Design_Override.md): **canonical current confirmed design** and Open items.
- [Narrative_Content_Contract](Docs/GO_GHOST_VS/Narrative_Content_Contract.md): exact text remains in the single JSON, stable IDs and finite runtime responsibilities.
- [VerticalSlice](Docs/VerticalSlice.md): overview/MSP strategy; [TEST profile](Docs/GO_GHOST_VS/Prototype_Test_Profile.md): provisional numbers.
- [ProjectMap](Docs/ProjectMap.md): real scenes/scripts/data; [Collaboration](Docs/Collaboration.md): ownership/Git/multi-machine rules.

`Assets/_Game/Shared/` holds reusable player/input/weapon/persistence/content code and data. `Levels/L01_Mission/` holds the scene and Tower coordinator/NPC/Boss content. `Development/Thomas/` holds editor-only fixtures. Settings, Packages and ProjectSettings remain in place. Historical baseline/Macro/prompts/log entries are context, not current single-arena or percentage-ending instructions.
