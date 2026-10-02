#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Development-only Play Mode smoke-test driver (Thomas's test area).
/// Creates a virtual keyboard + mouse, injects a scripted input sequence
/// inside the player loop, samples the player/enemy state frame by frame
/// and records the results. Not referenced by any formal scene or prefab.
/// Compiled only in the editor.
/// Usage (from an editor script while in Play Mode): TopDownSmokeTest.Launch("movement");
/// then read TopDownSmokeTest.Results once TopDownSmokeTest.Done is true.
/// Scenarios: movement, shooting, dodge (any scene with the Player prefab);
/// combat, death (L01 Mission layout: player starts left of the EncounterZone).
/// Slice: sideview (Gate 1 target only), session-flow (Paths A/B/C/D and five retries).
/// Stage 02 boss-only scenarios were superseded by the session flow.
/// </summary>
public partial class TopDownSmokeTest : MonoBehaviour
{
    public static readonly List<string> Results = new List<string>();
    public static bool Running { get; private set; }
    public static bool Done { get; private set; }

    private Keyboard kb;
    private Mouse ms;
    private readonly List<InputDevice> disabledDevices = new List<InputDevice>();
    private InputSettings.BackgroundBehavior prevBackground;
    private InputSettings.EditorInputBehaviorInPlayMode prevEditorBehavior;
    private bool prevRunInBackground;
    private static string pendingScenario;

    public static void EnterPlayModeAndLaunch(string scenario)
    {
        if (Application.isPlaying || Running) return;
        pendingScenario = scenario;
        UnityEditor.EditorApplication.playModeStateChanged -= OnEnteredPlayMode;
        UnityEditor.EditorApplication.playModeStateChanged += OnEnteredPlayMode;
        UnityEditor.EditorApplication.isPlaying = true;
    }

    private static void OnEnteredPlayMode(UnityEditor.PlayModeStateChange state)
    {
        if (state != UnityEditor.PlayModeStateChange.EnteredPlayMode) return;
        UnityEditor.EditorApplication.playModeStateChanged -= OnEnteredPlayMode;
        Launch(pendingScenario);
        pendingScenario = null;
    }

    public static void Launch(string scenario)
    {
        if (Running) return;
        var go = new GameObject("TopDownSmokeTest");
        DontDestroyOnLoad(go); // survives the scene reload triggered by the death/retry scenario
        var test = go.AddComponent<TopDownSmokeTest>();
        test.StartCoroutine(test.Run(scenario));
    }

    private IEnumerator Run(string scenario)
    {
        Results.Clear();
        Done = false;
        Running = true;
        Setup();
        // Every smoke victory now awards persistent progress. Keep historical fixtures off the normal profile too.
        // Phase 9 owns its isolation so it can verify disk reload and restore explicitly.
        if (scenario != "phase9-msp")
        {
            phase9OriginalProfile = PoliceArchive.Current;
            phase9OriginalStore = (PoliceArchiveStore)typeof(PoliceArchive).GetField("store", ArchiveFields).GetValue(null);
            phase9ArchiveIsolated = true;
            Phase9UseArchive(System.IO.Path.GetFullPath("Logs/GO_GHOST_VS/SmokeArchives/"
                + System.Guid.NewGuid().ToString("N") + "/archive.json"));
        }
        yield return null;

        switch (scenario)
        {
            case "sideview": yield return SideView(); break;
            case "session-flow": yield return SessionPaths(); break;
            case "combat-v2": yield return CombatV2(); break;
            case "combat-v2-recovery": yield return TimeRecoveryV2(); break;
            case "polish": yield return Polish(); break;
            case "class-demo": yield return ClassDemo(); break;
            case "continue-flow": yield return ContinueFlow(); break;
            case "phase1-foundation": yield return Phase1Foundation(); break;
            case "phase2-arena": yield return Phase2Arena(); break;
            case "phase2-legs": yield return Phase2Legs(); break;
            case "phase3-boss": yield return Phase3Boss(); break;
            case "phase4-msp": yield return Phase4Msp(); break;
            case "phase5-msp": yield return Phase5Msp(); break;
            case "phase6-msp": yield return Phase6Msp(); break;
            case "phase7-msp": yield return Phase7Msp(); break;
            case "phase8-msp": yield return Phase8Msp(); break;
            case "phase9-msp": yield return Phase9Msp(); break;
            case "phase10-msp": yield return Phase10Msp(); break;
            case "bare-gameplay": yield return BareGameplay(); break;
            case "strict-ui": yield return StrictUI(); break;
            case "ui-corrections": yield return StrictUICorrections(); break;
            case "run-archive": yield return RunArchive(); break;
            case "practice-flow": yield return PracticeFlow(); break;
            case "weapons": yield return Weapons(); break;
            case "arm-blade": yield return ArmBlade(); break;
            case "polish03": yield return Polish03(); break;
            case "perf-input": yield return PerfInput(); break;
            case "air-dash": yield return AirDash(); break;
            case "ui-polish": yield return UiPolish(); break;
            case "result-arena": yield return ResultArena(); break;
            case "boss-phases": yield return BossPhases(); break;
            case "blackout": yield return Blackout(); break;
            case "boss-contact": yield return BossContact(); break;
            // Earlier numerical gates are historical; TEST-v2 uses combat-v2.
            case "movement": yield return Movement(); break;
            case "shooting": yield return Shooting(); break;
            case "dodge": yield return Dodge(); break;
            case "combat": yield return Combat(); break;
            case "death": yield return Death(); break;
            default: Record("unknown scenario " + scenario); break;
        }

        RestorePhase9Archive();
        Teardown();
        System.IO.Directory.CreateDirectory("Logs/GO_GHOST_VS");
        System.IO.File.WriteAllLines("Logs/GO_GHOST_VS/" + scenario + ".txt", Results);
        Running = false;
        Done = true;
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- scenarios

    // Gate 1: real Input System events, physics and projectiles; only a stationary test target.
    private IEnumerator SideView()
    {
        var reader = FindAnyObjectByType<PlayerInputReader>();
        var rb = reader.GetComponent<Rigidbody2D>();
        var movement = reader.GetComponent<PlayerMovement>();
        var dodge = reader.GetComponent<PlayerDodge>();
        var health = reader.GetComponent<PlayerHealth>();
        var target = FindAnyObjectByType<Enemy>();
        // The editor can be behind the Codex window. Model a focused game for virtual input;
        // native focus transitions still need a separate manual check.
        reader.SendMessage("OnApplicationFocus", true);
        Record("Automated virtual keyboard/mouse in Play Mode; simulated focus, no stat or damage overrides.");
        Keys(); MouseTo(0.5f, 0.5f);
        yield return Seconds(0.8f);
        Check("landed with gravity", movement.IsGrounded && Mathf.Abs(rb.gravityScale - 3f) < 0.01f,
            $"position={rb.position}, grounded={movement.IsGrounded}");
        float groundY = rb.position.y;

        float x = rb.position.x;
        Keys(Key.D); yield return Seconds(0.25f);
        float horizontalSpeed = rb.linearVelocity.x;
        Check("D moves right", rb.position.x > x + 0.8f && horizontalSpeed > 5.5f, $"velocity={rb.linearVelocity}");
        Keys(Key.D, Key.W); yield return Seconds(0.15f);
        Check("W+D keeps full horizontal speed", horizontalSpeed > 5.5f && Mathf.Abs(rb.linearVelocity.x - horizontalSpeed) < 0.05f, $"velocity={rb.linearVelocity}");
        Keys(Key.S); yield return Seconds(0.15f);
        Check("S cannot move the player", reader.MoveInput == Vector2.zero && Mathf.Abs(rb.position.y - groundY) < 0.05f, $"input={reader.MoveInput}");
        Keys(Key.W); yield return Seconds(0.15f);
        Check("W cannot move the player", reader.MoveInput == Vector2.zero, $"input={reader.MoveInput}");

        Keys(Key.A); AimAtWorld(reader.transform.position + Vector3.right * 5f, true);
        yield return Seconds(0.1f);
        bool shotRight = false;
        foreach (var bullet in FindObjectsByType<Projectile>())
            if (bullet.OwnerLayer == reader.gameObject.layer && bullet.GetComponent<Rigidbody2D>().linearVelocity.x > 10f) shotRight = true;
        Check("move left and shoot right", rb.linearVelocity.x < -5.5f && reader.AimDirection.x > 0.95f && shotRight, $"velocity={rb.linearVelocity}, aim={reader.AimDirection}, shotRight={shotRight}");
        Keys(); MouseTo(0.05f, 0.5f); yield return Seconds(0.12f);
        Check("free aim left", reader.AimDirection.x < 0f, $"aim={reader.AimDirection}");

        Keys(Key.Space); yield return Seconds(0.06f); Keys();
        Check("Space jumps without dodge", rb.linearVelocity.y > 5f && !dodge.IsDodging && !movement.IsGrounded, $"velocity={rb.linearVelocity}");
        Keys(Key.LeftShift); yield return Seconds(0.05f); Keys();
        Check("air dodge rejected", !dodge.IsDodging && !health.IsInvulnerable, $"grounded={movement.IsGrounded}");
        yield return Seconds(0.15f);
        AimAtWorld(reader.transform.position + Vector3.right * 5f, true);
        yield return Seconds(0.08f);
        bool airShot = false;
        foreach (var bullet in FindObjectsByType<Projectile>())
            if (bullet.OwnerLayer == reader.gameObject.layer && bullet.transform.position.y > groundY + 0.5f) airShot = true;
        Check("airborne shooting", airShot && !movement.IsGrounded, $"playerY={rb.position.y}");
        MouseTo(0.5f, 0.95f); yield return Seconds(1f);
        Check("stable landing", movement.IsGrounded && Mathf.Abs(rb.position.y - groundY) < 0.08f, $"position={rb.position}");

        Keys(Key.D); yield return Seconds(0.04f); Keys(); yield return Frames(2);
        Vector2 start = rb.position;
        Keys(Key.LeftShift); yield return Seconds(0.05f); Keys();
        Check("Shift horizontal grounded dodge", dodge.IsDodging && health.IsInvulnerable && rb.linearVelocity.x > 10f
            && Mathf.Abs(rb.position.y - start.y) < 0.08f, $"velocity={rb.linearVelocity}");
        yield return Seconds(0.4f);
        Check("dodge restores normal state", !dodge.IsDodging && !health.IsInvulnerable && rb.gravityScale == 3f
            && rb.GetComponent<Collider2D>().enabled, $"delta={rb.position - start}");
        yield return Seconds(0.8f);
        start = rb.position;
        MouseTo(0.01f, 0.95f); Keys(Key.LeftShift); yield return Seconds(0.06f); Keys();
        Check("idle dodge follows movement facing, not aim", dodge.IsDodging && rb.linearVelocity.x > 10f && reader.AimDirection.x < 0f,
            $"velocity={rb.linearVelocity}, aim={reader.AimDirection}");
        yield return Seconds(0.4f);

        float deadline = Time.time + 2f;
        while (target != null && !target.IsDead && Time.time < deadline)
        {
            AimAtWorld(target.transform.position, true);
            yield return null;
        }
        MouseTo(0.01f, 0.5f);
        Check("normal bullets kill stationary target", target == null || target.IsDead, "no direct damage call");
        yield return Seconds(1.3f);
        AimAtWorld(reader.transform.position + Vector3.left * 20f, true);
        yield return Seconds(0.1f); MouseTo(0.01f, 0.5f);
        yield return Seconds(1.4f);
        Check("wall destroys projectile before 3s lifetime", FindObjectsByType<Projectile>().Length == 0, $"bullets={FindObjectsByType<Projectile>().Length}");

        Keys(Key.D); yield return Seconds(2.5f); Keys(Key.D, Key.LeftShift); yield return Seconds(0.3f); Keys();
        Check("solid arena boundary", rb.position.x <= 9.55f && movement.IsGrounded, $"position={rb.position}");
        Keys(Key.A); Mouse(0.1f, 0.5f, true); yield return Frames(2);
        reader.SetGameplayInputEnabled(false); yield return Frames(3);
        Check("input gate clears held controls", reader.MoveInput == Vector2.zero && !reader.AttackHeld, "controls disabled");
        reader.SetGameplayInputEnabled(true); yield return Frames(3);
        Check("held input cannot leak through re-enable", reader.MoveInput == Vector2.zero && !reader.AttackHeld, "release required");
        Keys(); MouseTo(0.5f, 0.5f); yield return Frames(3);
        Keys(Key.A); yield return Seconds(0.1f);
        Check("input resumes after release", rb.linearVelocity.x < -5.5f, $"velocity={rb.linearVelocity}");
        Keys();
        var cam = Camera.main;
        Vector3 bottomLeft = cam.WorldToViewportPoint(new Vector3(-11f, -3.5f, 0f));
        Vector3 topRight = cam.WorldToViewportPoint(new Vector3(11f, 6f, 0f));
        Check("fixed camera covers arena", cam.orthographic && !cam.GetComponent<CameraFollow>().enabled
            && bottomLeft.x >= 0f && bottomLeft.y >= 0f && topRight.x <= 1f && topRight.y <= 1f,
            $"viewport corners={bottomLeft}, {topRight}");
        Check("single active player", FindObjectsByType<PlayerHealth>().Length == 1, "active hierarchy");
    }

    private static void Check(string name, bool passed, string evidence)
    {
        Record((passed ? "PASS " : "FAIL ") + name + ": " + evidence);
    }

    // Augmentation gate: actual input, projectile damage and UI clicks. Forced branch setup is labelled separately.
    private IEnumerator AugmentationPaths()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        Record("Augmentation gates: virtual keyboard/mouse, actual uGUI clicks; simulated focus. Normal victories use unchanged Boss and no forced damage.");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        CheckAugmentConfig(flow, 0, 12, .8f, AimControl.Manual, "A first human");
        Check("New Game catalog locked", !flow.AugmentationsUnlocked && !new LoadoutDraft().TrySetSlot(LoadoutSlot.Brain, LoadoutRules.BrainAI), "locked at both session and draft");
        yield return NormalSessionFight(flow, true);
        CheckVictory(flow, 0, "G first try 0%");
        yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        yield return NormalSessionFight(flow, false);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        Check("A genuine failure unlocks all three", flow.OfficerIndex == 2 && flow.AugmentationsUnlocked && flow.Draft.AugmentationsAvailable, "Officer #2 / no currency or further unlocks");

        string[] clicks = { "LeftArmButton", "RightLegButton", "LeftArmButton", "BrainButton", "LeftArmButton", "RightLegButton", "LeftArmButton", "BrainButton" };
        float[] percents = { 10, 20, 10, 50, 60, 50, 40, 0 };
        for (int i = 0; i < clicks.Length; i++)
        {
            yield return ClickSessionButton(clicks[i]);
            string text = GameObject.Find("Details").GetComponent<TMPro.TMP_Text>().text;
            Check("current equipment percentage " + i, flow.Draft.BodyModificationPercent == percents[i]
                && text.Contains($"BODY MODIFICATION: {percents[i]:0}%"), "UI and draft=" + percents[i]);
        }
        float capacity = 0; for (int i = 0; i < 6; i++) capacity += LoadoutRules.MaximumContribution((LoadoutSlot)i);
        Check("full-body weights and slot restrictions", capacity == 100 && LoadoutRules.MaximumContribution(LoadoutSlot.Weapon) == 0
            && !flow.TrySetDraftSlot(LoadoutSlot.RightArm, LoadoutRules.ArmFireRate)
            && !flow.TrySetDraftSlot(LoadoutSlot.LeftLeg, LoadoutRules.LegMoveSpeed)
            && !flow.TrySetDraftSlot(LoadoutSlot.Torso, LoadoutRules.BrainAI)
            && !flow.TrySetDraftSlot(LoadoutSlot.Weapon, "second_gun"), "no fake slots, weapon or normalization");
        for (int i = 0; i < 3; i++) { yield return ClickSessionButton("LeftArmButton"); yield return ClickSessionButton("LeftArmButton"); }
        Check("repeated install/remove never accumulates", flow.Draft.BodyModificationPercent == 0, "three extra arm cycles return to zero");
        yield return ClickSessionButton("LeftArmButton"); yield return ClickSessionButton("RightLegButton"); yield return ClickSessionButton("BrainButton");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/augment-loadout-60.png"); yield return Frames(2);
        var draft60 = flow.Draft;
        yield return DeployAndFocus(flow);
        CheckAugmentConfig(flow, 60, 15, .6f, AimControl.AI, "E combined Officer 2");
        Check("combat refuses AI removal", !flow.TrySetDraftSlot(LoadoutSlot.Brain, "") && !draft60.TrySetSlot(LoadoutSlot.Brain, ""), "both UI API and retained draft locked");
        yield return VerifyOneBullet(flow, 15, "combined");
        yield return MeasureDodgeInterval(flow, .6f, "combined");
        yield return VerifyAIControl(flow);
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/augment-ai-combat.png"); yield return Frames(2);
        yield return NormalSessionFight(flow, false);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        Check("F inherited draft belongs to Officer 3", flow.OfficerIndex == 3 && flow.Draft.BodyModificationPercent == 60, "previous selection is only an editable starting point");
        yield return ClickSessionButton("LeftArmButton"); yield return ClickSessionButton("RightLegButton"); yield return ClickSessionButton("BrainButton");
        yield return DeployAndFocus(flow);
        CheckAugmentConfig(flow, 0, 12, .8f, AimControl.Manual, "F all removed Officer 3");
        yield return VerifyOneBullet(flow, 12, "restored human");
        yield return MeasureDodgeInterval(flow, .8f, "restored human");
        yield return VerifyManualAim(flow);
        yield return NormalSessionFight(flow, true);
        CheckVictory(flow, 0, "G restored 0%");

        // Both requested augmented victory records are earned in fresh, normal fights, without fixtures.
        foreach (float body in new float[] { 40, 60 })
        {
            yield return ClickSessionButton("NewGameButton");
            yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
            CheckAugmentConfig(flow, 0, 12, .8f, AimControl.Manual, "new game before " + body);
            Check("New Game clears augmentation session", flow.OfficerIndex == 1 && !flow.AugmentationsUnlocked && flow.Victory == null, "fresh draft, record, flags and modifiers");
            yield return NormalSessionFight(flow, false);
            yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
            yield return ClickSessionButton("BrainButton");
            if (body == 60) { yield return ClickSessionButton("LeftArmButton"); yield return ClickSessionButton("RightLegButton"); }
            yield return DeployAndFocus(flow);
            CheckAugmentConfig(flow, body, body == 60 ? 15 : 12, body == 60 ? .6f : .8f, AimControl.AI, "G normal " + body);
            yield return NormalSessionFight(flow, true);
            CheckVictory(flow, body, "G victory " + body);
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/augment-victory-" + body + ".png"); yield return Frames(2);
        }
        yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        CheckAugmentConfig(flow, 0, 12, .8f, AimControl.Manual, "final New Game");
        Check("final New Game clears record and unlocks", flow.Victory == null && !flow.AugmentationsUnlocked && flow.OfficerIndex == 1, "after 60% victory");

        // Controlled branch setup only; numerical checks below still use real inputs, physics and bullets.
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("LeftArmButton");
        yield return DeployAndFocus(flow);
        CheckAugmentConfig(flow, 10, 15, .8f, AimControl.Manual, "B arm only");
        yield return VerifyOneBullet(flow, 15, "arm only");
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("LeftArmButton");
        yield return DeployAndFocus(flow);
        CheckAugmentConfig(flow, 0, 12, .8f, AimControl.Manual, "B arm removed");
        yield return VerifyOneBullet(flow, 12, "arm removed");
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("RightLegButton");
        yield return DeployAndFocus(flow);
        CheckAugmentConfig(flow, 10, 12, .6f, AimControl.Manual, "C leg only");
        yield return VerifyOneBullet(flow, 12, "leg leaves damage unchanged");
        yield return MeasureDodgeInterval(flow, .6f, "leg only");
        yield return VerifyNoAirDodge(flow);
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("RightLegButton");
        yield return DeployAndFocus(flow);
        CheckAugmentConfig(flow, 0, 12, .8f, AimControl.Manual, "C leg removed");
        yield return MeasureDodgeInterval(flow, .8f, "leg removed");
        Record("A-G complete. Real victory records at 0/40/60%; controlled numerical fixtures explicitly separated.");
    }

    private IEnumerator AITargetFixture()
    {
        Record("CONTROLLED AI target fixture: forced death for setup; Boss repositioned/kinematic for height and lifecycle checks. Not normal victory evidence.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        CheckAugmentConfig(flow, 0, 12, .8f, AimControl.Manual, "fresh Play Mode after augmentations");
        Check("fresh Play resets session flags", flow.OfficerIndex == 1 && !flow.AugmentationsUnlocked && flow.Victory == null,
            "Domain Reload remains disabled");
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("BrainButton");
        Keys(Key.A, Key.LeftShift);
        yield return ClickSessionButton("DeployButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        var fight = flow.ActiveFight; var oldBoss = fight.Boss;
        var reader = fight.Player.GetComponent<PlayerInputReader>(); reader.SendMessage("OnApplicationFocus", true);
        var rb = fight.Boss.GetComponent<Rigidbody2D>(); var target = fight.Boss.GetComponent<Collider2D>();
        fight.Boss.enabled = false; rb.bodyType = RigidbodyType2D.Kinematic;
        float minDot = 1f;
        foreach (Vector2 point in new[] { new Vector2(-8, 1), new Vector2(4, 2), new Vector2(-3, 0) })
        {
            rb.position = point; Physics2D.SyncTransforms();
            AimAtWorld(reader.transform.position - ((Vector3)point - reader.transform.position).normalized * 8f, false);
            yield return Frames(3);
            Vector2 expected = ((Vector2)target.bounds.center - (Vector2)reader.transform.position).normalized;
            minDot = Mathf.Min(minDot, Vector2.Dot(expected, reader.AimDirection));
        }
        Check("AI tracks height/left/right while action keys wait for release", minDot > .9999f && reader.MoveInput == Vector2.zero
            && !fight.Player.GetComponent<PlayerDodge>().IsDodging, $"minimum dot={minDot:0.00000}; held A+Shift never moves/dodges");
        Keys(); MouseTo(.5f, .5f); yield return Frames(3);
        float start = Time.time; int samples = 0; minDot = 1f;
        while (Time.time - start < .6f)
        {
            float t = (Time.time - start) / .6f;
            rb.position = new Vector2(Mathf.Lerp(-8, 6, t), 1 + Mathf.Sin(t * Mathf.PI)); Physics2D.SyncTransforms();
            AimAtWorld(reader.transform.position - (target.bounds.center - reader.transform.position).normalized * 8f, false);
            yield return null;
            Vector2 expected = ((Vector2)target.bounds.center - (Vector2)reader.transform.position).normalized;
            minDot = Mathf.Min(minDot, Vector2.Dot(expected, reader.AimDirection)); samples++;
        }
        Check("AI follows a moving elevated target", minDot > .9999f && fight.BossHealth.CurrentHealth == 320,
            $"samples={samples}, min dot={minDot:0.00000}, no LMB or damage");
        Vector2 finalAim = reader.AimDirection;
        fight.BossHealth.TakeDamage(320, fight.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        AimAtWorld(reader.transform.position - (Vector3)finalAim * 8, true); yield return Seconds(.15f);
        Check("dead target stops aim/attacks without mouse fallback", !reader.enabled && reader.AimDirection == finalAim
            && FindObjectsByType<Projectile>().Length == 0, "forced target death; last aim retained, controls disabled");
        Keys(); MouseTo(.5f, .5f); yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        Check("old AI target destroyed by New Game", oldBoss == null && flow.Deployment.Aim == AimControl.Manual, "no stale target instance");
        yield return FixtureNextOfficer(flow);
        yield return ClickSessionButton("BrainButton"); yield return DeployAndFocus(flow);
        var fresh = flow.ActiveFight; var freshReader = fresh.Player.GetComponent<PlayerInputReader>();
        fresh.Boss.enabled = false; var freshRb = fresh.Boss.GetComponent<Rigidbody2D>(); freshRb.bodyType = RigidbodyType2D.Kinematic;
        freshRb.position = new Vector2(-8, 1); Physics2D.SyncTransforms();
        AimAtWorld(freshReader.transform.position + Vector3.right * 8, false); yield return Frames(3);
        Check("new AI deployment binds the new Boss", freshReader.AimMode == AimControl.AI && freshReader.AimDirection.x < -.4f
            && freshReader.AimDirection.y > .5f && oldBoss == null, "new Boss left and above, mouse right");
    }

    private IEnumerator DeployAndFocus(GameSessionFlow flow)
    {
        yield return ClickSessionButton("DeployButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        flow.ActiveFight.Player.GetComponent<PlayerInputReader>().SendMessage("OnApplicationFocus", true);
        Keys(); MouseTo(.5f, .5f); yield return Frames(3);
    }

    private void CheckAugmentConfig(GameSessionFlow flow, float body, int damage, float interval, AimControl aim, string label)
    {
        var f = flow.ActiveFight; var reader = f.Player.GetComponent<PlayerInputReader>();
        Check(label + " runtime config", flow.Deployment.BodyModificationPercent == body && flow.Deployment.WeaponId == LoadoutRules.BasicGun
            && f.Player.GetComponent<PlayerShooting>().BulletDamage == damage && Mathf.Abs(f.Player.GetComponent<PlayerDodge>().DodgeStartInterval - interval) < .001f
            && reader.AimMode == aim && flow.Deployment.Aim == aim, $"body={body}, damage={damage}, dodge={interval}, aim={reader.AimMode}");
        var so = new UnityEditor.SerializedObject(f.Boss);
        Check(label + " Boss unchanged", f.BossHealth.MaxHealth == 320 && f.Player.MaxHealth == 100
            && so.FindProperty("bulletDamage").intValue == 10 && so.FindProperty("chargeDamage").intValue == 20
            && Mathf.Abs(so.FindProperty("shotWindup").floatValue - .7f) < .001f
            && Mathf.Abs(so.FindProperty("shotRecovery").floatValue - .9f) < .001f
            && Mathf.Abs(so.FindProperty("chargeWindup").floatValue - .8f) < .001f
            && Mathf.Abs(so.FindProperty("chargeSpeed").floatValue - 12) < .001f
            && Mathf.Abs(so.FindProperty("chargeDuration").floatValue - .65f) < .001f
            && Mathf.Abs(so.FindProperty("chargeRecovery").floatValue - 1.1f) < .001f, "same HP, damage and FSM settings");
    }

    private void CheckVictory(GameSessionFlow flow, float body, string label)
    {
        var record = flow.Victory;
        Check(label, flow.State == GameSessionFlow.FlowState.Result && record != null && record.BodyModificationPercent == body
            && record.WeaponId == LoadoutRules.BasicGun && record.OfficerIndex == flow.OfficerIndex, "winning snapshot body=" + record?.BodyModificationPercent);
        var editable = new LoadoutDraft(record.WinningDeployment, true);
        editable.TrySetSlot(LoadoutSlot.LeftArm, ""); editable.TrySetSlot(LoadoutSlot.RightLeg, ""); editable.TrySetSlot(LoadoutSlot.Brain, "");
        Check(label + " immutable", !ReferenceEquals(record.WinningDeployment, flow.Deployment)
            && !flow.Draft.TrySetSlot(LoadoutSlot.Brain, "") && record.BodyModificationPercent == body && editable.BodyModificationPercent == 0,
            "independent editable copy changed; locked draft rejected; victory unchanged");
    }

    private IEnumerator VerifyOneBullet(GameSessionFlow flow, int damage, string label)
    {
        var fight = flow.ActiveFight;
        int before = fight.BossHealth.CurrentHealth;
        AimAtWorld(fight.Boss.transform.position, true); yield return Seconds(.055f);
        AimAtWorld(fight.Boss.transform.position, false);
        // The expanded arena permits a longer flight than the old fixed 0.8s probe.
        float deadline = Time.time + 2.5f;
        while (fight.BossHealth.CurrentHealth == before && Time.time < deadline) yield return null;
        yield return Seconds(.1f);
        int lost = before - fight.BossHealth.CurrentHealth;
        Check(label + " REAL projectile damage", lost == damage, $"Boss HP {before}->{fight.BossHealth.CurrentHealth}; delta={lost}, expected={damage}");
    }

    private IEnumerator MeasureDodgeInterval(GameSessionFlow flow, float expected, string label)
    {
        var dodge = flow.ActiveFight.Player.GetComponent<PlayerDodge>();
        Keys(); yield return Frames(2);
        float old = dodge.LastDodgeStartedAt, deadline = Time.time + 2f;
        int frame = 0;
        while (dodge.LastDodgeStartedAt == old && Time.time < deadline) { Keys(frame++ % 2 == 0 ? new[] { Key.LeftShift } : new Key[0]); yield return null; }
        float first = dodge.LastDodgeStartedAt;
        Keys(); yield return Frames(2); deadline = Time.time + 2f; frame = 0;
        while (dodge.LastDodgeStartedAt == first && Time.time < deadline) { Keys(frame++ % 2 == 0 ? new[] { Key.LeftShift } : new Key[0]); yield return null; }
        Keys(); float actual = dodge.LastDodgeStartedAt - first;
        Check(label + " actual dodge start interval", !float.IsInfinity(first) && actual >= expected - .01f && actual < expected + .15f,
            $"measured={actual:0.000}s; expected~{expected:0.0}s");
        yield return Seconds(.3f);
        Check(label + " dodge invulnerability ends", !flow.ActiveFight.Player.IsInvulnerable, "after dash and i-frame window");
    }

    private IEnumerator VerifyNoAirDodge(GameSessionFlow flow)
    {
        var player = flow.ActiveFight.Player; var dodge = player.GetComponent<PlayerDodge>();
        Keys(); yield return Seconds(.9f); Keys(Key.Space); yield return Seconds(.13f);
        float previous = dodge.LastDodgeStartedAt;
        Keys(Key.LeftShift); yield return Seconds(.08f); Keys();
        Check("C no air dodge", !player.GetComponent<PlayerMovement>().IsGrounded && !dodge.IsDodging && dodge.LastDodgeStartedAt == previous,
            "jump then fresh Shift after cooldown was already ready");
        yield return Seconds(.85f);
    }

    private IEnumerator VerifyManualAim(GameSessionFlow flow)
    {
        var reader = flow.ActiveFight.Player.GetComponent<PlayerInputReader>();
        AimAtWorld(reader.transform.position + Vector3.left * 6, false); yield return Frames(3);
        bool left = reader.AimDirection.x < -.99f;
        AimAtWorld(reader.transform.position + Vector3.right * 6, false); yield return Frames(3);
        Check("F manual aim fully restored", left && reader.AimDirection.x > .99f && reader.AimMode == AimControl.Manual,
            "mouse selects both sides regardless of new Boss position");
    }

    private IEnumerator VerifyAIControl(GameSessionFlow flow)
    {
        var fight = flow.ActiveFight; var reader = fight.Player.GetComponent<PlayerInputReader>();
        var movement = fight.Player.GetComponent<PlayerMovement>(); var rb = fight.Player.GetComponent<Rigidbody2D>();
        var boss = fight.Boss; var target = boss.GetComponent<Collider2D>();
        var muzzle = (Transform)new UnityEditor.SerializedObject(fight.Player.GetComponent<PlayerShooting>()).FindProperty("firePoint").objectReferenceValue;
        float minAim = 1f, minMuzzle = 1f, nextJump = 0, moveUntil = 0, end = Time.time + 8f;
        bool left = false, right = false, jumped = false, moved = false, autoFire = false, autoMove = false, autoDodge = false;
        float neutralSince = Time.time;
        int hp = fight.BossHealth.CurrentHealth, samples = 0; Key cross = Key.D;
        float startingX = rb.position.x, manualMoveUntil = Time.time + .35f;
        while (Time.time < end && flow.State == GameSessionFlow.FlowState.Combat)
        {
            Vector3 away = ((Vector2)reader.transform.position - (Vector2)target.bounds.center).normalized;
            AimAtWorld(reader.transform.position + away * 8f, false);
            bool warning = (boss.State == CyberpsychoBoss.AttackState.ChargeWarning || boss.State == CyberpsychoBoss.AttackState.ShotWarning) && boss.StateTimeRemaining < .1f;
            if (warning && movement.IsGrounded && Time.time >= nextJump)
            {
                cross = boss.transform.position.x > rb.position.x ? Key.D : Key.A;
                if (boss.State == CyberpsychoBoss.AttackState.ChargeWarning) moveUntil = Time.time + .75f;
                Keys(Key.Space, cross); nextJump = Time.time + .9f;
            }
            else if (Time.time < manualMoveUntil) Keys(Key.D);
            else if (Time.time < moveUntil) Keys(cross);
            else Keys();
            yield return null;
            Vector2 expected = ((Vector2)target.bounds.center - (Vector2)reader.transform.position).normalized;
            minAim = Mathf.Min(minAim, Vector2.Dot(expected, reader.AimDirection));
            minMuzzle = Mathf.Min(minMuzzle, Vector2.Dot(expected, muzzle.right)); samples++;
            if (expected.x < -.1f) left = true; if (expected.x > .1f) right = true;
            if (Mathf.Abs(rb.position.x - startingX) > .5f) moved = true;
            if (!movement.IsGrounded && rb.linearVelocity.y > 1f) jumped = true;
            foreach (var bullet in FindObjectsByType<Projectile>()) if (bullet.OwnerLayer == fight.Player.gameObject.layer) autoFire = true;
            if (reader.HasMoveInput) neutralSince = Time.time;
            if (Time.time - neutralSince > .06f && Mathf.Abs(rb.linearVelocity.x) > .5f) autoMove = true;
            if (fight.Player.GetComponent<PlayerDodge>().IsDodging) autoDodge = true;
        }
        Keys();
        Check("D AI owns every sampled aim and muzzle frame", minAim > .995f && minMuzzle > .995f && left && right,
            $"samples={samples}, min aim dot={minAim:0.00000}, muzzle={minMuzzle:0.00000}, left={left}, right={right}; mouse held opposite Boss");
        Check("D AI does not fire, move or dodge automatically", !autoFire && !autoMove && !autoDodge && fight.BossHealth.CurrentHealth == hp,
            "no LMB/Shift; no player projectiles or Boss damage; neutral horizontal input stays still");
        Check("D player still moves and jumps", moved && jumped, "normal D/Space events; prior Shift test proves manual dodge");
    }

    private IEnumerator FixtureNextOfficer(GameSessionFlow flow)
    {
        Record("FIXTURE branch setup: forced player death; not normal defeat/victory evidence.");
        Keys(); MouseTo(.5f, .5f); yield return Seconds(.35f);
        var f = flow.ActiveFight; f.Player.TakeDamage(f.Player.MaxHealth, f.Boss.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
    }

    // Session gate: actual virtual input fights and pointer clicks; no forced damage in Paths A/B/C/D.
    private IEnumerator SessionPaths()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        int loadoutVisits = 0;
        System.Action<GameSessionFlow.FlowState> observed = state => { if (state == GameSessionFlow.FlowState.Loadout) loadoutVisits++; };
        flow.OnStateChanged += observed;
        Record("Paths A/B/C/D use normal stats, virtual keyboard/mouse and real uGUI pointer clicks. Focus callback simulated.");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        CheckFreshAttempt(flow, 1, "A initial");
        Check("A no unlocks before death", !flow.AugmentationsUnlocked && flow.Victory == null, "New Game initialization");
        yield return NormalSessionFight(flow, true);
        Check("A first try victory", flow.State == GameSessionFlow.FlowState.Result && loadoutVisits == 0
            && flow.Victory.OfficerIndex == 1 && flow.Victory.BodyModificationPercent == 0f
            && !flow.AugmentationsUnlocked, "never entered Loadout");
        var firstVictory = flow.Victory;
        var firstDraft = flow.Draft;
        Check("victory owns a copied snapshot", !ReferenceEquals(firstVictory.WinningDeployment, flow.Deployment)
            && firstVictory.WeaponId == LoadoutRules.BasicGun, "immutable value entries and private copied arrays");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/session-first-victory.png");
        yield return Frames(2);
        yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        CheckFreshAttempt(flow, 1, "B New Game");
        Check("New Game clears previous record/draft", flow.Victory == null && !ReferenceEquals(firstDraft, flow.Draft)
            && firstVictory.OfficerIndex == 1 && firstVictory.BodyModificationPercent == 0f, "old record remains unchanged");

        var oldFight = flow.ActiveFight;
        yield return NormalSessionFight(flow, false);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        CheckLoadout(flow, 2, "B");
        Check("B old actors destroyed", oldFight == null, "next officer uses a new prefab instance");
        ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/session-loadout.png");
        yield return Frames(2);
        Keys(Key.A, Key.LeftShift); Mouse(0.9f, 0.9f, true);
        yield return Seconds(0.3f);
        Check("Loadout combat input cannot act", FindObjectsByType<Projectile>().Length == 0 && flow.ActiveFight == null,
            "held Move/Attack/Dodge, no actors or attacks");
        var secondDraft = flow.Draft;
        Check("editable draft accepts only implemented entries", flow.TrySetDraftSlot(LoadoutSlot.LeftArm, "")
            && flow.TrySetDraftSlot(LoadoutSlot.Weapon, LoadoutRules.BasicGun)
            && !flow.TrySetDraftSlot(LoadoutSlot.Brain, "not_implemented"), "no fake catalog");
        // Keep keyboard held across the click; the fresh input reader must wait for release.
        yield return ClickSessionButton("DeployButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        var reader = flow.ActiveFight.Player.GetComponent<PlayerInputReader>();
        reader.SendMessage("OnApplicationFocus", true);
        yield return Frames(3);
        CheckFreshAttempt(flow, 2, "B deployed");
        Check("held menu keys cannot leak into deployment", reader.MoveInput == Vector2.zero
            && !flow.ActiveFight.Player.GetComponent<PlayerDodge>().IsDodging, "A+Shift still held");
        Check("combat rejects both current and retained draft writes", !flow.TrySetDraftSlot(LoadoutSlot.LeftArm, "")
            && !secondDraft.TrySetSlot(LoadoutSlot.LeftArm, "") && secondDraft.IsLocked
            && flow.Deployment.BodyModificationPercent == 0f, "sealed draft and immutable snapshot");
        Check("duplicate Deploy rejected", !flow.RequestDeploy(), "already in Combat");
        Keys(); MouseTo(0.5f, 0.5f); yield return Frames(3);
        Keys(Key.LeftShift); yield return Seconds(0.06f); Keys();
        Check("B fresh dodge ready", flow.ActiveFight.Player.GetComponent<PlayerDodge>().IsDodging
            && flow.ActiveFight.Player.IsInvulnerable, "normal Shift on new officer");
        yield return Seconds(0.4f);
        Check("B fresh invulnerability clears", !flow.ActiveFight.Player.IsInvulnerable, "normal timer");

        yield return NormalSessionFight(flow, true);
        Check("D Officer 2 wins", flow.State == GameSessionFlow.FlowState.Result && flow.Victory.OfficerIndex == 2
            && flow.Victory.BodyModificationPercent == 0f && flow.AugmentationsUnlocked, "unlock state persisted across deployment");
        var secondVictory = flow.Victory;
        bool clearedAtInitialization = false;
        System.Action<GameSessionFlow.FlowState> resetObserver = state =>
        {
            if (state == GameSessionFlow.FlowState.NewGameInitialization)
                clearedAtInitialization = flow.OfficerIndex == 1 && !flow.AugmentationsUnlocked
                    && flow.Victory == null && flow.Deployment == null && !ReferenceEquals(flow.Draft, secondDraft);
        };
        flow.OnStateChanged += resetObserver;
        yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        flow.OnStateChanged -= resetObserver;
        CheckFreshAttempt(flow, 1, "D reset");
        Check("D complete session reset", clearedAtInitialization && !flow.AugmentationsUnlocked && flow.Victory == null
            && flow.Deployment.Aim == AimControl.Manual && flow.Deployment.WeaponId == LoadoutRules.BasicGun,
            "Officer #1, human, basic gun, manual aim, fresh draft/deployment, no victory/unlock state");
        Check("D previous victory remains immutable", secondVictory.OfficerIndex == 2 && secondVictory.BodyModificationPercent == 0f,
            "held record survives New Game without changing");

        // Prompt 03 also asks for five consecutive retries; exercise all of them with real Boss attacks.
        for (int officer = 1; officer <= 5; officer++)
        {
            oldFight = flow.ActiveFight;
            yield return NormalSessionFight(flow, false);
            yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
            CheckLoadout(flow, officer + 1, "C cycle " + officer);
            Check("C old instance removed " + officer, oldFight == null, "no old callback owner");
            Keys(); MouseTo(0.5f, 0.5f); yield return Frames(3);
            yield return ClickSessionButton("DeployButton");
            yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
            CheckFreshAttempt(flow, officer + 1, "C deployed " + (officer + 1));
            Check("C unlock persists " + officer, flow.AugmentationsUnlocked, "session state does not reset on Next Officer");
        }
        flow.OnStateChanged -= observed;
        Record("A/B/C/D complete; five consecutive real defeat/deploy cycles completed.");

        // Separate branch fixture; explicitly excluded from normal-combat victory evidence.
        for (int order = 0; order < 2; order++)
        {
            var fight = flow.ActiveFight;
            int outcomes = 0;
            fight.OnResolved += _ => outcomes++;
            if (order == 0) { fight.Player.TakeDamage(100, fight.Boss.gameObject); fight.BossHealth.TakeDamage(320, fight.Player.gameObject); }
            else { fight.BossHealth.TakeDamage(320, fight.Player.gameObject); fight.Player.TakeDamage(100, fight.Boss.gameObject); }
            yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
            Check("FIXTURE mutual death routes to next officer " + order, outcomes == 1 && flow.Victory == null,
                "forced same-frame callbacks, TEST failure priority");
            Keys(); MouseTo(0.5f, 0.5f); yield return ClickSessionButton("DeployButton");
            yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        }
    }

    private IEnumerator NormalSessionFight(GameSessionFlow flow, bool win)
    {
        var fight = flow.ActiveFight;
        var reader = fight.Player.GetComponent<PlayerInputReader>();
        var movement = fight.Player.GetComponent<PlayerMovement>();
        var boss = fight.Boss;
        int outcomeCount = 0;
        fight.OnResolved += _ => outcomeCount++;
        reader.SendMessage("OnApplicationFocus", true);
        Keys(); MouseTo(0.5f, 0.5f); yield return Frames(3);
        Keys(Key.D); yield return Seconds(0.85f); Keys();
        if (!win)
        {
            AimAtWorld(boss.transform.position, true); yield return Seconds(0.3f);
            MouseTo(0.5f, 0.5f);
        }
        float deadline = Time.time + 45f, nextJump = 0f, moveUntil = 0f;
        Key cross = Key.D;
        while (flow.State == GameSessionFlow.FlowState.Combat && Time.time < deadline)
        {
            if (win)
            {
                Vector3 aimPoint = reader.AimMode == AimControl.AI
                    ? reader.transform.position + (reader.transform.position - boss.transform.position).normalized * 8f
                    : boss.transform.position;
                AimAtWorld(aimPoint, true);
                bool jump = (boss.State == CyberpsychoBoss.AttackState.ChargeWarning
                    || boss.State == CyberpsychoBoss.AttackState.ShotWarning) && boss.StateTimeRemaining < 0.1f;
                if (jump && movement.IsGrounded && Time.time >= nextJump)
                {
                    if (boss.State == CyberpsychoBoss.AttackState.ChargeWarning)
                    {
                        cross = boss.transform.position.x > reader.transform.position.x ? Key.D : Key.A;
                        moveUntil = Time.time + 0.75f;
                        Keys(Key.Space, cross);
                    }
                    else Keys(Key.Space);
                    nextJump = Time.time + 0.9f;
                }
                else if (Time.time < moveUntil) Keys(cross);
                else Keys();
            }
            yield return null;
        }
        Check("normal combat " + (win ? "victory" : "defeat") + " Officer " + flow.OfficerIndex,
            outcomeCount == 1 && fight.Result == (win ? BossFight.Outcome.Won : BossFight.Outcome.Died),
            $"Player HP={fight.Player.CurrentHealth}, Boss HP={fight.BossHealth.CurrentHealth}, outcomes={outcomeCount}");
        if (!win) Check("departing officer damaged Boss", fight.BossHealth.CurrentHealth < 320, "normal bullets; next deployment must restore 320");
        Keys(Key.A, Key.LeftShift); Mouse(0.9f, 0.9f, true);
        yield return Seconds(0.15f);
        Check("outcome freezes combat", !reader.GameplayInputEnabled && !fight.Player.GetComponent<PlayerShooting>().enabled
            && !fight.Player.IsInvulnerable && boss.State == CyberpsychoBoss.AttackState.Stopped
            && FindObjectsByType<Projectile>().Length == 0, "held fire/movement after outcome");
        Keys(); MouseTo(0.5f, 0.5f);
    }

    private void CheckFreshAttempt(GameSessionFlow flow, int officer, string label)
    {
        var fight = flow.ActiveFight;
        var player = fight.Player;
        var rb = player.GetComponent<Rigidbody2D>();
        var bossRb = fight.Boss.GetComponent<Rigidbody2D>();
        Check(label + " identity/config", flow.OfficerIndex == officer && flow.Deployment.OfficerIndex == officer
            && player.GetComponent<OfficerIdentity>().OfficerIndex == officer && flow.Deployment.BodyModificationPercent == 0f
            && flow.Deployment.WeaponId == LoadoutRules.BasicGun && flow.Deployment.Aim == AimControl.Manual,
            $"Officer #{flow.OfficerIndex}, 0%, basic_gun, Manual");
        Check(label + " full reset", player.CurrentHealth == 100 && fight.BossHealth.CurrentHealth == 320
            && !player.IsDead && !fight.BossHealth.IsDead && !player.IsInvulnerable && !player.GetComponent<PlayerDodge>().IsDodging
            && player.GetComponent<PlayerMovement>().IsGrounded && Mathf.Abs(rb.position.x + 6f) < 0.02f
            && Mathf.Abs(bossRb.position.x - 6f) < 0.02f && rb.linearVelocity.sqrMagnitude < 0.01f
            && bossRb.linearVelocity.sqrMagnitude < 0.01f && fight.Boss.State == CyberpsychoBoss.AttackState.Arrival
            && FindObjectsByType<Projectile>().Length == 0, $"HP={player.CurrentHealth}/{fight.BossHealth.CurrentHealth}, spawn={rb.position}/{bossRb.position}");
        Check(label + " no duplicate actors", FindObjectsByType<PlayerHealth>().Length == 1
            && FindObjectsByType<CyberpsychoBoss>().Length == 1 && FindObjectsByType<BossFight>().Length == 1
            && FindObjectsByType<GameSessionFlow>().Length == 1, "one flow/player/boss/attempt");
    }

    private void CheckLoadout(GameSessionFlow flow, int officer, string label)
    {
        Check(label + " next officer/loadout", flow.OfficerIndex == officer && flow.AugmentationsUnlocked
            && flow.Deployment == null && flow.Victory == null && !flow.Draft.IsLocked && flow.Draft.BodyModificationPercent == 0f
            && flow.Draft.WeaponId == LoadoutRules.BasicGun, $"Officer #{flow.OfficerIndex}, unlock={flow.AugmentationsUnlocked}");
        Check(label + " world cleared", flow.ActiveFight == null && FindObjectsByType<PlayerHealth>().Length == 0
            && FindObjectsByType<CyberpsychoBoss>().Length == 0 && FindObjectsByType<Projectile>().Length == 0,
            "old attempt destroyed while the next officer chooses deployment");
    }

    private IEnumerator WaitForState(GameSessionFlow flow, GameSessionFlow.FlowState expected)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (flow.State != expected && Time.realtimeSinceStartup < deadline)
        {
            if (flow.CanContinue)
            {
                yield return Frames(2); // Let SessionView display the current page before clicking.
                yield return ClickSessionButton("DeployButton");
            }
            else yield return null;
        }
        if (flow.State != expected) throw new System.InvalidOperationException($"Expected {expected}, got {flow.State}");
        yield return null;
    }

    private IEnumerator ClickSessionButton(string name)
    {
        var button = GameObject.Find(name).GetComponent<UnityEngine.UI.Button>();
        Vector2 p = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
        Mouse(p.x / Screen.width, p.y / Screen.height, false); yield return Frames(3);
        Mouse(p.x / Screen.width, p.y / Screen.height, true); yield return Frames(3);
        Mouse(p.x / Screen.width, p.y / Screen.height, false); yield return Frames(3);
        Record("Pointer click: " + name);
    }

    private IEnumerator Movement()
    {
        var reader = FindAnyObjectByType<PlayerInputReader>();
        var rb = reader.GetComponent<Rigidbody2D>();
        var mv = reader.GetComponent<PlayerMovement>();
        var sr = reader.GetComponent<SpriteRenderer>();

        Keys(Key.D); MouseTo(0.1f, 0.5f);
        yield return Frames(20);
        Vector2 p0 = rb.position;
        yield return Frames(20);
        Record($"D held: MoveInput={reader.MoveInput} vel={rb.linearVelocity} moved={(rb.position - p0)} AimDir={reader.AimDirection} Facing={mv.FacingDirection} flipX={sr.flipX} localScale.x={reader.transform.localScale.x}");

        Keys(Key.W, Key.D); MouseTo(0.9f, 0.5f);
        yield return Frames(20);
        Record($"W+D held: MoveInput={reader.MoveInput} |MoveInput|={reader.MoveInput.magnitude:F3} vel={rb.linearVelocity} |vel|={rb.linearVelocity.magnitude:F2} AimDir={reader.AimDirection} Facing={mv.FacingDirection} flipX={sr.flipX}");

        Keys(Key.A, Key.S); MouseTo(0.5f, 0.95f);
        yield return Frames(20);
        Record($"A+S held: vel={rb.linearVelocity} AimDir={reader.AimDirection} (expect ~(0,1)) Facing={mv.FacingDirection}");

        Keys(); MouseTo(0.5f, 0.5f);
        yield return Frames(20);
        Record($"released: MoveInput={reader.MoveInput} vel={rb.linearVelocity}");
    }

    private IEnumerator Shooting()
    {
        var reader = FindAnyObjectByType<PlayerInputReader>();
        var rb = reader.GetComponent<Rigidbody2D>();
        Keys(Key.A); MouseTo(0.9f, 0.9f); // move left, aim upper-right
        yield return Frames(10);
        int before = FindObjectsByType<Projectile>().Length;
        Mouse(0.9f, 0.9f, true);
        yield return Seconds(0.6f); // fireRate 0.25s -> expect 2-3 bullets
        var bullets = FindObjectsByType<Projectile>();
        Record($"held Attack 0.6s while moving left: bullets before={before} now={bullets.Length} AimDir={reader.AimDirection} vel={rb.linearVelocity} playerPos={rb.position}");
        foreach (var b in bullets)
        {
            var brb = b.GetComponent<Rigidbody2D>();
            Vector2 dir = brb.linearVelocity.normalized;
            Record($"  bullet pos={b.transform.position} velDir={dir} dot(aim)={Vector2.Dot(dir, reader.AimDirection):F3} speed={brb.linearVelocity.magnitude:F1} rotZ={b.transform.eulerAngles.z:F0} ownerLayer={b.OwnerLayer}");
        }
        Mouse(0.9f, 0.9f, false); Keys();
        yield return Frames(5);
    }

    private IEnumerator Dodge()
    {
        var reader = FindAnyObjectByType<PlayerInputReader>();
        var rb = reader.GetComponent<Rigidbody2D>();
        var dodge = reader.GetComponent<PlayerDodge>();
        var health = reader.GetComponent<PlayerHealth>();

        // 1) dodge along Move (A held) while aiming right
        Keys(Key.A); MouseTo(0.9f, 0.5f);
        yield return Frames(10);
        Vector2 start = rb.position;
        Keys(Key.A, Key.Space); yield return null; Keys(Key.A);
        bool sawDodging = false, sawInvuln = false;
        float t = 0f;
        while (t < 0.3f) { if (dodge.IsDodging) sawDodging = true; if (health.IsInvulnerable) sawInvuln = true; t += Time.deltaTime; yield return null; }
        Vector2 delta = rb.position - start;
        Record($"dodge with A held, aim right: delta={delta} |delta|={delta.magnitude:F2} dir={delta.normalized} (expect ~(-1,0), length >= 3 plus normal move) sawDodging={sawDodging} sawInvulnerable={sawInvuln} IsDodging now={dodge.IsDodging} vel now={rb.linearVelocity}");

        // 2) no move input -> dodge along aim (mouse up)
        Keys(); MouseTo(0.5f, 0.95f);
        yield return Seconds(1.0f); // cooldown 0.8s
        start = rb.position;
        Keys(Key.Space); yield return null; Keys();
        yield return Seconds(0.35f);
        delta = rb.position - start;
        Record($"dodge without move, aim up: delta={delta} |delta|={delta.magnitude:F2} dir={delta.normalized} (expect ~(0,1) length ~3) vel after={rb.linearVelocity} IsDodging={dodge.IsDodging} IsInvulnerable={health.IsInvulnerable}");
    }

    private IEnumerator Combat()
    {
        var reader = FindAnyObjectByType<PlayerInputReader>();
        var rb = reader.GetComponent<Rigidbody2D>();
        var health = reader.GetComponent<PlayerHealth>();
        var zone = FindAnyObjectByType<EncounterZone>();
        Record($"start: health={health.CurrentHealth} pos={rb.position} enemies active={CountActiveEnemies()} zone={(zone != null ? zone.name + " state=" + zone.State : "none")}");

        // walk right into the zone
        Keys(Key.D); MouseTo(0.9f, 0.5f);
        float t = 0f;
        while (t < 6f && (zone == null || zone.State == EncounterZone.EncounterState.Idle)) { t += Time.deltaTime; yield return null; }
        Keys();
        yield return Frames(2);
        Record($"after walking right {t:F2}s: pos={rb.position} zone state={(zone != null ? zone.State.ToString() : "none")} enemies active={CountActiveEnemies()} remaining={(zone != null ? zone.RemainingEnemies : -1)}");

        // stand still and let enemies shoot; measure damage taken
        int hp0 = health.CurrentHealth;
        yield return Seconds(3.5f);
        Record($"stood still 3.5s: health {hp0} -> {health.CurrentHealth} (enemy bullets should hurt) enemyBulletsInFlight={CountBulletsNotOwnedByLayer(LayerMask.NameToLayer("Player"))} playerBulletsInFlight={CountBulletsOwnedByLayer(LayerMask.NameToLayer("Player"))}");

        // dodge i-frames: take damage while dodging must be ignored
        int hpBeforeDodge = health.CurrentHealth;
        Keys(Key.Space); yield return null; Keys();
        yield return null;
        bool invulnDuring = health.IsInvulnerable;
        health.TakeDamage(5, null);
        Record($"dodge i-frames: IsInvulnerable during dodge={invulnDuring}; TakeDamage(5) during dodge -> health {hpBeforeDodge} -> {health.CurrentHealth} (expect unchanged)");
        yield return Seconds(1.0f);

        // heal back up (existing PlayerHealth API) so the clear-out below is not cut short by the damage taken while standing still
        health.Heal(1000);
        Record($"healed before clearing: health={health.CurrentHealth}");

        // shoot the nearest enemy until it dies (aim at it every frame)
        Enemy target = NearestEnemy(rb.position);
        if (target != null)
        {
            int ehp0 = target.CurrentHealth;
            float t2 = 0f;
            while (target != null && !target.IsDead && t2 < 10f)
            {
                AimAtWorld(target.transform.position, true);
                t2 += Time.deltaTime;
                yield return null;
            }
            Mouse(0.5f, 0.5f, false);
            Record($"shot nearest enemy: hp {ehp0} -> {(target == null || target.IsDead ? "dead" : target.CurrentHealth.ToString())} in {t2:F2}s; enemies active={CountActiveEnemies()} zone remaining={(zone != null ? zone.RemainingEnemies : -1)} state={(zone != null ? zone.State.ToString() : "none")}");
        }
        else Record("no enemy found to shoot");

        // clear the rest
        float t3 = 0f;
        while (zone != null && zone.State == EncounterZone.EncounterState.Active && t3 < 30f)
        {
            Enemy e = NearestEnemy(rb.position);
            if (e != null) AimAtWorld(e.transform.position, true); else Mouse(0.5f, 0.5f, false);
            t3 += Time.deltaTime;
            yield return null;
        }
        Mouse(0.5f, 0.5f, false);
        Record($"cleared rest in {t3:F2}s: zone state={(zone != null ? zone.State.ToString() : "none")} enemies active={CountActiveEnemies()} health={health.CurrentHealth} dead={health.IsDead}");
    }

    private IEnumerator Death()
    {
        var reader = FindAnyObjectByType<PlayerInputReader>();
        var health = reader.GetComponent<PlayerHealth>();
        PlayerHealth healthBefore = health; // reference identity check after the reload (GetInstanceID is obsolete in 6000.6)
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int zonesBefore = FindObjectsByType<EncounterZone>().Length;
        Record($"before: scene={scene.name} buildIndex={scene.buildIndex} health={health.CurrentHealth} dead={health.IsDead} players={FindObjectsByType<PlayerHealth>().Length} zones={zonesBefore}");
        health.TakeDamage(9999, null);
        yield return null;
        Record($"after lethal damage: health={health.CurrentHealth} dead={health.IsDead} movementEnabled={reader.GetComponent<PlayerMovement>().enabled} inputEnabled={reader.enabled}");
        yield return Seconds(3f);
        var players = FindObjectsByType<PlayerHealth>();
        var enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Include);
        var newHealth = players.Length > 0 ? players[0] : null;
        var zone = FindAnyObjectByType<EncounterZone>();
        Record($"3s later: scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} players={players.Length} sameInstance={ReferenceEquals(newHealth, healthBefore)} health={(newHealth != null ? newHealth.CurrentHealth.ToString() : "n/a")} dead={(newHealth != null && newHealth.IsDead)} enemies(incl inactive)={enemies.Length} zones={FindObjectsByType<EncounterZone>().Length} zoneState={(zone != null ? zone.State.ToString() : "none")} smokeTests={FindObjectsByType<TopDownSmokeTest>().Length}");
    }

    // ---------------------------------------------------------------- helpers

    private static int CountActiveEnemies()
    {
        int n = 0;
        foreach (var e in FindObjectsByType<Enemy>()) if (e.isActiveAndEnabled && !e.IsDead) n++;
        return n;
    }

    private static int CountBulletsOwnedByLayer(int layer)
    {
        int n = 0;
        foreach (var p in FindObjectsByType<Projectile>()) if (p.OwnerLayer == layer) n++;
        return n;
    }

    private static int CountBulletsNotOwnedByLayer(int layer)
    {
        int n = 0;
        foreach (var p in FindObjectsByType<Projectile>()) if (p.OwnerLayer != layer) n++;
        return n;
    }

    private static Enemy NearestEnemy(Vector2 from)
    {
        Enemy best = null; float bestD = float.MaxValue;
        foreach (var e in FindObjectsByType<Enemy>())
        {
            if (!e.isActiveAndEnabled || e.IsDead) continue;
            float d = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }

    private void AimAtWorld(Vector3 world, bool leftButton)
    {
        Vector3 screen = Camera.main.WorldToScreenPoint(world);
        Mouse(screen.x / Screen.width, screen.y / Screen.height, leftButton);
    }

    // The last virtual state is remembered so KeepVirtualFocus can re-send it: a real editor focus change makes the
    // Input System reset devices that cannot sync, which silently released held virtual keys mid-scenario.
    private Key[] heldKeys = new Key[0];
    private Vector3 heldMouse = new Vector3(.5f, .5f, 0f); // x, y fractions; z = 1 while the left button is held
    private void Keys(params Key[] keys) { heldKeys = keys; InputSystem.QueueStateEvent(kb, new KeyboardState(keys)); }
    private void ResendVirtualState()
    {
        if (kb == null || ms == null || !kb.added || !ms.added) return;
        InputSystem.QueueStateEvent(kb, new KeyboardState(heldKeys));
        var state = new MouseState { position = new Vector2(Screen.width * heldMouse.x, Screen.height * heldMouse.y) };
        InputSystem.QueueStateEvent(ms, state.WithButton(MouseButton.Left, heldMouse.z > .5f));
    }
    private void MouseTo(float fx, float fy) => Mouse(fx, fy, false);
    private void Mouse(float fx, float fy, bool leftButton)
    {
        heldMouse = new Vector3(fx, fy, leftButton ? 1f : 0f);
        var state = new MouseState { position = new Vector2(Screen.width * fx, Screen.height * fy) };
        state = state.WithButton(MouseButton.Left, leftButton);
        InputSystem.QueueStateEvent(ms, state);
    }

    private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
    private static IEnumerator Seconds(float s) { float t = 0f; while (t < s) { t += Time.deltaTime; yield return null; } }

    private static void Record(string s)
    {
        Results.Add(s);
        Debug.Log("[SmokeTest] " + s);
    }

    private void Setup()
    {
        prevBackground = InputSystem.settings.backgroundBehavior;
        prevEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        prevRunInBackground = Application.runInBackground;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Application.runInBackground = true;

        var stale = new List<InputDevice>();
        foreach (var d in InputSystem.devices) if (d.name.StartsWith("Smoke")) stale.Add(d);
        foreach (var d in stale) InputSystem.RemoveDevice(d);
        kb = InputSystem.AddDevice<Keyboard>("SmokeKeyboard");
        ms = InputSystem.AddDevice<Mouse>("SmokeMouse");
        disabledDevices.Clear();
        foreach (var d in InputSystem.devices)
        {
            if (d.name.StartsWith("Smoke")) continue;
            if ((d is Keyboard || d is Pointer) && d.enabled) { InputSystem.DisableDevice(d); disabledDevices.Add(d); }
        }
    }

    private void Teardown()
    {
        if (kb != null && kb.added) InputSystem.RemoveDevice(kb);
        if (ms != null && ms.added) InputSystem.RemoveDevice(ms);
        disabledDevices.Clear();
        RestoreRealDevices();
        InputSystem.settings.backgroundBehavior = prevBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = prevEditorBehavior;
        Application.runInBackground = prevRunInBackground;
    }

    // A scenario mutes the operator's keyboard/mouse. An aborted run once left them disabled, and the next Setup then
    // skipped them (already disabled), so normal Play Mode had no input at all. Re-enable every real device
    // unconditionally here, and again on each play-mode transition that is not a smoke launch.
    private static void RestoreRealDevices()
    {
        var stale = new List<InputDevice>();
        foreach (var d in InputSystem.devices)
        {
            if (d.name.StartsWith("Smoke")) stale.Add(d);
            else if (d is Keyboard || d is Pointer) InputSystem.EnableDevice(d);
        }
        foreach (var d in stale) InputSystem.RemoveDevice(d);
    }

    [UnityEditor.InitializeOnLoadMethod]
    private static void GuardRealDevices()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeGuard;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeGuard;
    }

    private static void OnPlayModeGuard(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) { Running = false; RestoreRealDevices(); }
        else if (state == UnityEditor.PlayModeStateChange.ExitingEditMode && !Running) RestoreRealDevices();
    }

    private void Update()
    {
        // Settings changes can re-create devices after Setup ran; keep the real keyboard/mouse muted while a scenario runs
        // so the operator's physical mouse cannot leak into the Aim action.
        if (!Running) return;
        foreach (var d in InputSystem.devices)
        {
            if (d.name.StartsWith("Smoke")) continue;
            if ((d is Keyboard || d is Pointer) && d.enabled) { InputSystem.DisableDevice(d); if (!disabledDevices.Contains(d)) disabledDevices.Add(d); }
        }
    }

    private void OnDestroy()
    {
        RestorePhase9Archive();
        if (Running) { Teardown(); Running = false; Done = true; }
    }
}
#endif
