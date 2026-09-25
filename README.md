# GO GHOST

A single-player 2D side-view shooter about the trade-off between power and free will. One arena, one cyberpsycho Boss: each time an officer falls, the next one takes over, chooses how much of their body to augment, and fights again. USC CTIN 489 project by Thomas Hong and Walker Grupp.

## Requirements
- Unity **6000.6.0f1**, installed through Unity Hub. Do not upgrade the project to another version.
- Git with **Git LFS**. Run `git lfs install` once per machine before cloning or pulling. Textures, audio, models, and fonts are stored in LFS.
- A C# IDE selected in Unity under Preferences > External Tools (Rider, Visual Studio, or VS Code). Unity generates the solution and project files; they are not committed.
- Per-machine Git setup for Unity scene and prefab merges: see [Docs/Collaboration.md](Docs/Collaboration.md) section 6.

## Opening the project
1. Clone the repository, or pull `main`.
2. In Unity Hub choose Add > Add project from disk, select the repository root (the folder containing `Assets/`, `Packages/`, `ProjectSettings/`), and open it with 6000.6.0f1. The first open builds `Library/` and takes several minutes.
3. Open `Assets/_Game/Levels/L01_Mission/Scenes/Cyberpunk World.unity`, the only scene in Build Settings, and press Play.

## Playing the current slice

Current checkpoint: Phases 1–9, following the MSP strategy. Design and remaining scope: [VerticalSlice](Docs/VerticalSlice.md). Engineering handoff: [ProjectMap](Docs/ProjectMap.md). Phase 10 has not started.

- Officer #1 starts at 0% Body with the Basic Gun and fights the Boss.
- On defeat, the next officer opens the Loadout: install or remove modules for free, then press DEPLOY. Both sides start the new fight at full health.
- On victory, the Result shows the winning officer and their Body %. NEW GAME resets to Officer #1 at 0%.
- Controls: A / D move, Space jump, Left Shift dodge, E Counter Arm, Q Torso Time Slow (abilities require their modules), mouse aim, hold the left mouse button to fire. Q is the temporary Phase 8 secondary mapping. Dodge remains available during Time Slow and its cooldown, subject to its normal ground/cooldown rules. Transition pages wait for CONTINUE.
- Left Arm cycles Human -> Rapid Fire -> Counter Arm -> Human. Counter briefly nullifies a Burst projectile or parries a normal Ghost Slash/Crosscut into Boss recovery; Predator Drop and BLACKOUT are not counterable. Brain cycles Human -> Targeting AI -> Predictive Cortex -> Human. AI owns aim while firing/movement remain manual; Cortex keeps mouse aim and shows extra shot/slash/early High C prediction lines. These are replacements, with unchanged TEST body contributions.
- POLICE ARCHIVE opens from the current Session/New Mission pages; BACK returns to the same page. A Boss victory completes **TEMP_FIRST_VICTORY — TEMP / placeholder condition** and persistently unlocks Detached Drone Arm. After unlock, Left Arm adds Drone between Counter and Human. It hides the officer's left arm, follows and shoots the Boss autonomously, with no E/Q input or change to player aim. New Game keeps the unlock while starting Officer #1 at 0%; the existing next-officer Loadout offers earned upgrades.
- Each leg button cycles Human -> Move Speed -> Double Jump -> Hover -> Human. Every installed leg option contributes the current TEST 10%. Double Jump gives one extra Space press in the air; Hover limits descent while holding Space for about 1.2 seconds per landing. Mixed legs combine effects; duplicate Double Jump/Hover does not stack.
- Arena V1 has permanent Ground, two low one-way platforms and central High C. A 0% officer can reach every platform through ordinary jumps. Platforms block both teams' bullets; the Boss's elevated Sweep can fire across High C. BLACKOUT removes High C for the current attempt; the next deployment restores it. A player on High C falls when it disappears.
- P1 uses an MSP cycle: Burst Fire -> Marked Shot -> Ghost Reposition -> low/high Suppressive Sweep. Marked Shot tracks then locks a warning line; Ghost briefly turns transparent while moving; Sweep temporarily raises the gun to the warned horizontal height. At the TEST 50% HP threshold the Boss safely stops P1, discards its temporary gun and draws a temporary blade. P2 keeps its translucent cyan/pink flicker and cycles Ghost Slash -> Crosscut -> Predator Drop. Crosscut re-locks its second blade rush after the first; Predator Drop marks the target surface and descends from above, threatening Ground and High C. Each officer faces a fresh P1 Boss with normal appearance. Art, timings and damage remain temporary.
- At the TEST 25% HP threshold, BLACKOUT occurs once: stronger readable camouflage, two fast passes, a High C warning line, one damaging final slash, then High C disappears. The normal P2 cycle resumes afterward; this terrain state is never saved to Police Archive.

Boss HP and attacks, module effects, and slot weights are TEST values ([Prototype_Test_Profile](Docs/GO_GHOST_VS/Prototype_Test_Profile.md)). Endings are not implemented yet. What has been built and actually tested is recorded in [Implementation_Log](Docs/GO_GHOST_VS/Implementation_Log.md).

Police Archive has Endings and Achievements sections. No endings are authored yet; it displays "No endings recorded." The Drone victory condition is temporary, not final design. Its version-1 JSON lives at `Application.persistentDataPath/PoliceArchive/archive.json` (Windows default: `%USERPROFILE%/AppData/LocalLow/DefaultCompany/GO GHOST/PoliceArchive/archive.json`). New Game resets the session, not this file. Development checks use isolated profiles. Current-phase standalone and natural 0% complete victory are not verified; the older successful Windows build predates Phases 1–5.

## Layout
```
Assets/
  _Game/
    Shared/                 code and assets used by more than one level
      Scripts/Player/       input reader, movement, jump, dodge, shooting, health, loadout data,
                            officer identity and visuals, Time Slow, legacy abilities, projectile
      Scripts/Persistence/  PoliceArchive profile and versioned local JSON store
      Scripts/Enemies/      Enemy.cs (health and collider base, reused by the Boss)
      Scripts/Encounters/   EncounterZone.cs (legacy top-down encounter, not used by the slice)
      Prefabs/              Player.prefab, Bullet.prefab, Enemies/Enemy_Basic.prefab
      Data/                 PlayerStats.asset
    Levels/
      L01_Mission/
        Scenes/             Cyberpunk World.unity (the slice), TitleScene.unity (not in Build Settings)
        Scripts/            GameSessionFlow, BossFight, CyberpsychoBoss, SessionView
        Prefabs/            CombatAttempt.prefab (one officer + Boss per attempt), player physics material
    Development/
      Thomas/               test scene and editor-only Play Mode test drivers (TopDownSmokeTest*.cs)
      Walker/               player test scene
  Settings/                 URP, Input System actions, Build Profiles (keep in place)
  TextMesh Pro/             TMP Essential Resources
  Welcome/                  Unity 2D template welcome content
  Cyborg-Assets/            empty folders kept for Walker (do not move)
Docs/                       design, collaboration, and slice execution docs (Docs/GO_GHOST_VS/)
CLAUDE.md                   working rules for AI-assisted sessions
```

## Docs
- [Docs/VerticalSlice.md](Docs/VerticalSlice.md): English summary of the confirmed design and slice scope.
- [Docs/GO_GHOST_VS/](Docs/GO_GHOST_VS/START_HERE.md): current handoff, TEST values and implementation log, plus the historical Chinese baseline and staged prompts.
- [Docs/Collaboration.md](Docs/Collaboration.md): how two people and several machines work in one Unity project on GitHub.
- [Docs/ProjectMap.md](Docs/ProjectMap.md): where everything is, how the systems connect, and known issues.
