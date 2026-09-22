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
/// </summary>
public class TopDownSmokeTest : MonoBehaviour
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

    public static void Launch(string scenario)
    {
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
        yield return null;

        switch (scenario)
        {
            case "movement": yield return Movement(); break;
            case "shooting": yield return Shooting(); break;
            case "dodge": yield return Dodge(); break;
            case "combat": yield return Combat(); break;
            case "death": yield return Death(); break;
            default: Record("unknown scenario " + scenario); break;
        }

        Teardown();
        Running = false;
        Done = true;
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- scenarios

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

    private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
    private void MouseTo(float fx, float fy) => Mouse(fx, fy, false);
    private void Mouse(float fx, float fy, bool leftButton)
    {
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
        foreach (var d in disabledDevices) if (d != null && d.added) InputSystem.EnableDevice(d);
        disabledDevices.Clear();
        InputSystem.settings.backgroundBehavior = prevBackground;
        InputSystem.settings.editorInputBehaviorInPlayMode = prevEditorBehavior;
        Application.runInBackground = prevRunInBackground;
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
        if (Running) { Teardown(); Running = false; Done = true; }
    }
}
#endif
