#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BindingFlags = System.Reflection.BindingFlags;
using GoGhost.Duel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Development-only bounded checks. No formal scene/prefab references this editor helper.
public static class BossEncounterSmoke
{
    const string SessionKey = "GO_GHOST.BossSmoke", Output = "Logs/BossEncounter/";
    static readonly List<string> lines = new List<string>();
    static readonly Stack<IEnumerator> work = new Stack<IEnumerator>();
    static readonly List<Action> restoration = new List<Action>();
    static readonly List<InputDevice> disabledDevices = new List<InputDevice>();
    static Keyboard keyboard;
    static Mouse mouse;
    static Key[] heldKeys = Array.Empty<Key>();
    static Vector2 mousePosition;
    static bool mouseHeld, batch, inputReady, checkingPause;
    static InputSettings.BackgroundBehavior previousBackground;
    static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
    static bool previousRunInBackground;
    static string mode;
    static double deadline;
    static int frame = -1, failures;
    static DuelGame game;
    static PlayerInputReader focusedReader;
    const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    public static bool Running { get; private set; }
    public static bool Done { get; private set; }
    public static IReadOnlyList<string> Results => lines;

    // Batch invocation deliberately omits -quit. Editor entry points return immediately.
    public static void RunBatch() => Start("all", true);
    public static void RunInEditor() => Start("all", false);
    public static void RunCore() => Start("core", false);
    public static void RunInputOnly() => Start("input-only", false);
    public static void RunLegalFollowup() => Start("legal-followup", false);
    public static void RunNavigationOnly() => Start("navigation-only", false);
    public static void RunCoordination() => Start("coordination", false);
    public static void RunCornerChecks() => Start("corner", false);
    public static void RunWaveTwoObservation() => Start("observe-wave2", false);
    public static void RunObservation() => Start("observe-fixed", false);
    public static void RunPitchSmoke() => Start("pitch", false);
    public static void RunProjectileWalls() => Start("projectile-walls", false);
    public static void Cancel()
    { if (Running) { Record("NOT TESTED: operator cancelled unfinished checks."); Finish(false); } }

    static void Start(string requested, bool exitEditor)
    {
        if (Running || SessionState.GetString(SessionKey, "") != "")
            throw new InvalidOperationException("Boss smoke already running; do not start it twice.");
        if (!EditorApplication.isPlaying && UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save or preserve the open scene before the smoke.");
        mode = requested; batch = exitEditor; Running = true; Done = false; failures = 0; lines.Clear();
        SessionState.SetString(SessionKey, requested);
        deadline = EditorApplication.timeSinceStartup + (requested == "all" ? 750 : requested == "core" ? 350 : 155);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= PlayState; EditorApplication.playModeStateChanged += PlayState;
        Application.logMessageReceived -= Capture; Application.logMessageReceived += Capture;
        if (!EditorApplication.isPlaying)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DuelBuild.ScenePath)
                EditorSceneManager.OpenScene(DuelBuild.ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
    static void PlayState(PlayModeStateChange state)
    { if (Running && state == PlayModeStateChange.ExitingPlayMode) { Record("FAIL: Play Mode interrupted."); failures++; Finish(false); } }
    static void Tick()
    {
        if (!Running || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.timeSinceStartup > deadline)
        { Record("FAIL: bounded smoke wall-clock timeout."); failures++; Finish(false); return; }
        if (!EditorApplication.isPlaying || Time.frameCount == frame) return;
        frame = Time.frameCount;
        try
        {
            if (!inputReady)
            {
                game = Object.FindAnyObjectByType<DuelGame>();
                if (game == null) return;
                SetupInput();
                work.Push(Run());
            }
            if (game.Player != null)
            {
                var reader = game.Player.GetComponent<PlayerInputReader>();
                // A real focus transition re-arms release gates and clears current intent.
                // Calling this every frame would suppress all manual buttons and corrupt the fixture.
                if (reader != focusedReader || !Private<bool>(reader, "hasFocus"))
                {
                    reader.SendMessage("OnApplicationFocus", true);
                    focusedReader = reader;
                    Record("FOCUS resumed reader once; normal release gates retained, frame=" + Time.frameCount);
                }
                if (!checkingPause && game.Paused) game.SetPause(false);
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = mousePosition }.WithButton(MouseButton.Left, mouseHeld));
            // Nested IEnumerators keep this file independent of a new runtime testing MonoBehaviour.
            while (work.Count > 0)
            {
                var iterator = work.Peek();
                if (!iterator.MoveNext()) { (iterator as IDisposable)?.Dispose(); work.Pop(); continue; }
                if (iterator.Current is IEnumerator child) { work.Push(child); continue; }
                break;
            }
            if (work.Count == 0) Finish(true);
        }
        catch (Exception exception)
        { Record("FAIL: " + exception); failures++; Finish(false); }
    }
    static void Capture(string message, string trace, LogType type)
    {
        if (message.StartsWith("[Boss ") || message.StartsWith("[Duo") || message.StartsWith("[LegacyCombat"))
            lines.Add("EVENT " + message);
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        { lines.Add("ERROR " + message + "\n" + trace); failures++; }
    }
    static IEnumerator Run()
    {
        Record("Editor Play Mode; simulated focus and virtual keyboard/mouse. No physical-input or audibility claim.");
        if (mode == "pitch") { yield return PitchSmoke(); yield break; }
        if (mode == "projectile-walls")
        {
            yield return TeamBlocking();
            yield return Fresh(1);
            MouseAt(game.Bosses[0].Position, true);
            yield return Seconds(4);
            Check("short normal combat fires real player shots", game.Rifle.ShotsFired > 0,
                $"shots={game.Rifle.ShotsFired} bossBurst={game.Bosses[0].Actions.Activated[(int)BossAbility.Burst]} phase={game.Phase}");
            mouseHeld = false;
        }
        if (mode == "input-only")
        {
            yield return Fresh(1); StopPolicies();
            yield return CheckInputs();
        }
        if (mode == "legal-followup")
        {
            yield return DrawContact();
            yield return LegalReturn();
            yield return DropContact();
        }
        if (mode == "navigation-only") yield return NavigationChanges();
        if (mode == "coordination") yield return Coordination();
        if (mode == "corner") yield return CornerChecks();
        if (mode == "all" || mode == "core")
        {
            yield return InputAndFlow();
            yield return NavigationChanges();
            yield return Skills();
            yield return TeamBlocking();
            Record("NOT TESTED: human audio audition, animation smoothness and standalone are separate checks.");
        }
        if (mode == "all") yield return Observe();
        else if (mode == "observe-wave2") yield return Observe(2);
        else if (mode == "observe-fixed") yield return Observe();
        game.MainMenu(); yield return Frames(2);
        Check("cleanup removes all actors/projectiles", Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length == 0 && Projectile.Active.Count == 0);
    }

    static IEnumerator Fresh(int wave, bool automatic = false, bool stationaryAutomatic = false)
    {
        RestoreFixture(); heldKeys = Array.Empty<Key>(); mouseHeld = false;
        game.NewGame();
        // Controlled entry changes only the shell wave selection, never actor resources or outcomes.
        typeof(DuelGame).GetProperty("Wave").SetValue(game, wave);
        if (automatic) for (int i = 0; i < 3; i++) game.SetDraft(i, true);
        else if (stationaryAutomatic) { game.SetDraft(1,true); game.SetDraft(2,true); }
        game.StartFight();
        yield return Until(() => game.Phase == DuelPhase.Fight, 2.2f);
        Check("wave " + wave + " created", game.Phase == DuelPhase.Fight && game.Bosses.Count == (wave == 3 ? 2 : 1));
    }
    static IEnumerator PitchSmoke()
    {
        Record("PITCH: one W1 manual virtual-input smoke, one W2 AI sample (25s), one W3 AI sample (60s). No outcome gate or fixtures.");
        yield return Fresh(1);
        yield return CheckInputs(); // Real action-map events with normal Boss policy still running.
        while(game.Phase==DuelPhase.Fight&&game.FightClock<25)
        {
            if(game.Bosses[0].IsAlive)MouseAt(game.Bosses[0].Position,true);
            yield return null;
        }
        PitchResult();heldKeys=Array.Empty<Key>();mouseHeld=false;
        for(int wave=2;wave<=3;wave++)
        {
            yield return Fresh(wave,true);
            float next=0;
            while(game.Phase==DuelPhase.Fight&&game.FightClock<(wave==3?60:25))
            {
                if(game.FightClock>=next)
                {
                    next+=10;
                    Record($"PITCH position W{wave} t={game.FightClock:F1} player={game.Player.transform.position} hp={game.Player.CurrentHealth} "
                        +string.Join(";",game.Bosses.Select(a=>$"{a.Profile.Kind}={a.Position}/{a.Actions.Ability}/{a.Actions.Phase}")));
                }
                yield return null;
            }
            PitchResult();
        }
        Record("NOT TESTED: physical keyboard/mouse, full regression, balance matrix, audio audition, Windows build/launch.");
    }
    static void PitchResult()
    {
        Record($"PITCH RESULT W{game.Wave} phase={game.Phase} seconds={game.FightClock:F2} playerHP={game.Player.CurrentHealth}");
        foreach(var a in game.Bosses)Record($"PITCH actor={a.ActorId} hp={a.Health.CurrentHealth} starts={string.Join(",",a.Actions.Started)} active={string.Join(",",a.Actions.Activated)} hits={a.Actions.ConnectedHits} guard={a.Actions.Guarded}");
        var duo=game.GetComponentInChildren<DuoCoordinator>();
        Record($"PITCH duo started={duo.CombosStarted} completeA={duo.CrossCompleted} completeB={duo.SweepDropCompleted} abort={duo.CombosAborted}");
    }
    static void StopPolicies()
    {
        var coordinator = game.GetComponentInChildren<DuoCoordinator>();
        coordinator.StopCombat(); coordinator.BeginCombat(); coordinator.enabled = false;
        foreach (var actor in game.Bosses) { actor.Tactics.Enabled = false; actor.Actions.StopCombat(); actor.Motor.Stop(); }
    }
    static IEnumerator InputAndFlow()
    {
        Record("CONTROLLED input/flow: policies stopped for input measurements; direct deaths below prove lifecycle only.");
        yield return Fresh(1); StopPolicies();
        yield return CheckInputs();
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        game.Bosses[0].Actions.TryStart(BossAbility.Piercing, player.GetComponent<Collider2D>().bounds.center);
        yield return Seconds(.1f);
        checkingPause = true; game.SetPause(true);
        float clock = game.FightClock; var pos = body.position;
        float[] progress = game.Bosses.Select(a => a.Actions.PhaseProgress).ToArray();
        yield return RealSeconds(.25f);
        Check("pause freezes clock/motion/audio", Mathf.Approximately(clock, game.FightClock)
            && Vector2.Distance(pos, body.position) < .001f && AudioListener.pause
            && game.Bosses.Select((a,i) => Mathf.Approximately(a.Actions.PhaseProgress, progress[i])).All(v => v));
        game.SetPause(false); checkingPause = false;
        game.Bosses[0].Health.TakeDamage(99999, player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.Configure, 1.5f);
        Check("first wave advances to second configuration", game.Wave == 2 && game.Phase == DuelPhase.Configure);
        game.SetDraft(1, true); game.StartFight(); yield return Until(() => game.Phase == DuelPhase.Fight, 2);
        Check("mixed permissions stay independent", !game.LegsAI && game.ArmsAI && !game.SpineAI);
        yield return Seconds(2);
        game.Bosses[0].Health.TakeDamage(99999, game.Player.gameObject);
        yield return Until(() => game.Phase == DuelPhase.Configure, 1.5f);
        Check("second wave advances to third; choices retained", game.Wave == 3 && game.DraftArms && !game.DraftLegs);
        game.SetDraft(0, true); game.StartFight();
        yield return Until(() => game.Phase == DuelPhase.Fight, 2); StopPolicies();
        var first = game.Bosses[0]; var survivor = game.Bosses[1];
        first.Health.TakeDamage(99999, game.Player.gameObject); yield return Frames(2);
        Check("one independent death leaves third wave running", !first.IsAlive && !first.Body.enabled && survivor.IsAlive && game.Phase == DuelPhase.Fight);
        game.Player.TakeDamage(99999, survivor.gameObject); yield return Frames(2);
        Check("player death resolves defeat", game.Phase == DuelPhase.Failed);
        game.Reconfigure();
        Check("retry keeps wave/permissions", game.Wave == 3 && game.DraftLegs && game.DraftArms);
        game.StartFight(); yield return Until(() => game.Phase == DuelPhase.Fight, 2); StopPolicies();
        Check("retry creates fresh independent resources", game.Player.CurrentHealth == game.Player.MaxHealth
            && game.Bosses.All(a => a.Health.CurrentHealth == a.Health.MaxHealth && a.Actions.Ammo == a.Profile.Magazine));
        foreach (var actor in game.Bosses) actor.Health.TakeDamage(99999, game.Player.gameObject);
        game.Player.TakeDamage(99999, null); yield return Frames(2);
        Check("simultaneous final deaths prefer defeat", game.Phase == DuelPhase.Failed);
        yield return Fresh(3); StopPolicies();
        foreach (var actor in game.Bosses) actor.Health.TakeDamage(99999, game.Player.gameObject);
        yield return Frames(2); Check("both enemies down completes", game.Phase == DuelPhase.Complete);
    }

    static IEnumerator CheckInputs()
    {
        Record("CONTROLLED INPUT ONLY: virtual events enter the unchanged action maps; no property/input/velocity override.");
        var player = game.Player; var body = player.GetComponent<Rigidbody2D>();
        var reader = player.GetComponent<PlayerInputReader>(); var movement = player.GetComponent<PlayerMovement>();
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        yield return Frames(4); yield return Until(() => movement.IsGrounded, 2);
        InputProbe("neutral");
        float x = body.position.x; int ammo = game.Rifle.AmmoInMagazine;
        MouseAt(body.position + new Vector2(4, 2), true); heldKeys = new[] { Key.D };
        yield return Seconds(.28f);
        InputProbe("D + upper-right mouse + LMB");
        Check("manual move", body.position.x > x + .6f, $"x={x:F3}->{body.position.x:F3} velocity={body.linearVelocity} move={reader.MoveInput}");
        Check("manual free aim", reader.AimDirection.x > .1f && reader.AimDirection.y > .1f,
            $"aim={reader.AimDirection} screen={reader.AimScreenPosition} requested={mousePosition}");
        Check("manual real Rifle shot", game.Rifle.AmmoInMagazine < ammo,
            $"ammo={ammo}->{game.Rifle.AmmoInMagazine} held={reader.AttackHeld} bullets={Projectile.Active.Count}");
        heldKeys = Array.Empty<Key>(); mouseHeld = false; yield return Frames(4);
        yield return Until(() => movement.IsGrounded && game.Dodge.CanDodge, 2);
        InputProbe("before ground Shift");
        float previousStart = game.Dodge.LastDodgeStartedAt, groundX = body.position.x;
        heldKeys = new[] { Key.LeftShift };
        yield return Until(() => game.Dodge.LastDodgeStartedAt > previousStart, .35f);
        InputProbe("ground Shift accepted or timeout");
        bool groundAccepted = game.Dodge.LastDodgeStartedAt > previousStart;
        Check("ground Dodge executes", groundAccepted,
            $"started={previousStart}->{game.Dodge.LastDodgeStartedAt:F3} grounded={movement.IsGrounded} dodging={game.Dodge.IsDodging} cooldown={game.Dodge.CooldownRemaining:F3}");
        heldKeys = Array.Empty<Key>(); yield return Seconds(game.Dodge.Duration + .04f);
        Check("ground Dodge moves real body", groundAccepted && Mathf.Abs(body.position.x - groundX) > .3f,
            $"x={groundX:F3}->{body.position.x:F3} distance={game.Dodge.Distance:F3}");
        yield return Until(() => game.Dodge.CanDodge && movement.IsGrounded, game.Dodge.DodgeStartInterval + 1);
        yield return Frames(3); InputProbe("before Space");
        heldKeys = new[] { Key.Space };
        yield return Until(() => !movement.IsGrounded && body.linearVelocity.y > .5f, .35f);
        InputProbe("Space accepted or timeout");
        bool airborne = !movement.IsGrounded && body.linearVelocity.y > .5f;
        Check("manual Jump executes", airborne, $"position={body.position} velocity={body.linearVelocity}");
        heldKeys = Array.Empty<Key>(); yield return Frames(2);
        previousStart = game.Dodge.LastDodgeStartedAt;
        heldKeys = new[] { Key.LeftShift };
        yield return Until(() => game.Dodge.LastDodgeStartedAt > previousStart, .30f);
        InputProbe("air Shift accepted or timeout");
        Check("air Dodge executes with same cooldown", airborne && game.Dodge.LastDodgeStartedAt > previousStart,
            $"started={previousStart:F3}->{game.Dodge.LastDodgeStartedAt:F3} grounded={movement.IsGrounded} cooldown={game.Dodge.CooldownRemaining:F3} interval={game.Dodge.DodgeStartInterval:F3}");
        heldKeys = Array.Empty<Key>(); yield return Seconds(.35f);
    }

    static T Private<T>(object owner, string field) => (T)owner.GetType().GetField(field, PrivateInstance).GetValue(owner);
    static void InputProbe(string label)
    {
        var p = game.Player; var r = p.GetComponent<PlayerInputReader>(); var b = p.GetComponent<Rigidbody2D>();
        var move = Private<InputAction>(r, "moveAction"); var attack = Private<InputAction>(r, "attackAction");
        var jump = Private<InputAction>(r, "jumpAction"); var dodge = Private<InputAction>(r, "dodgeAction");
        Record($"INPUT {label} frame={Time.frameCount} t={Time.time:F3} pos={b.position} vel={b.linearVelocity} "
            + $"ready={r.IsReady} enabled={r.GameplayInputEnabled} focus={Private<bool>(r,"hasFocus")} paused={r.NarrativePaused} "
            + $"waitRelease={Private<bool>(r,"waitingForRelease")} gates={Private<bool>(r,"attackGated")}/{Private<bool>(r,"jumpGated")}/{Private<bool>(r,"dodgeGated")} "
            + $"map={move.actionMap.enabled} rawMove={move.ReadValue<Vector2>()} resolvedMove={r.MoveInput} "
            + $"rawButtons={attack.IsPressed()}/{jump.IsPressed()}/{dodge.IsPressed()} resolved={r.AttackHeld}/{r.JumpPressedThisFrame}/{r.DodgePressedThisFrame} "
            + $"aim={r.AimDirection} screen={r.AimScreenPosition} virtual={keyboard.added}/{keyboard.enabled}/{(Keyboard.current==keyboard)} "
            + $"rawKeys=D:{keyboard.dKey.isPressed},Space:{keyboard.spaceKey.isPressed},Shift:{keyboard.leftShiftKey.isPressed} "
            + $"requested={string.Join(",",heldKeys)} mouse={mouse.leftButton.isPressed} controls={string.Join(",",move.controls.Select(c=>c.path))}");
    }

    static void OnFloor(GameObject target, ArenaNavigation2D nav, float x)
    {
        float floor = nav.Nodes.Min(n => n.Position.y);
        Position(target, new Vector2(x, floor + target.GetComponent<Collider2D>().bounds.extents.y + .025f));
    }
    static void ActionProbe(string label, BossAgent actor)
    {
        var observed = actor.Perception.Facts;
        Record($"ACTION {label} t={Time.time:F3} ability={actor.Actions.Ability} phase={actor.Actions.Phase} attack={actor.Actions.AttackId} "
            + $"actor={actor.Position} v={actor.Velocity} grounded={actor.Motor.Grounded} dash={actor.Motor.DashActive}/{actor.Motor.DashBlocked} "
            + $"player={game.Player.GetComponent<Collider2D>().bounds.center} playerV={game.Player.GetComponent<Rigidbody2D>().linearVelocity} "
            + $"observed={observed.Position} confidence={observed.Confidence:F2} age={Time.time-observed.ObservedAt:F3} "
            + $"line={actor.Actions.Origin}->{actor.Actions.End} hp={game.Player.CurrentHealth} threats={actor.PublicThreats.Count}");
    }
    static IEnumerator DrawContact()
    {
        Record("CONTROLLED S2 contact: stationary target, fresh resources, normal DrawDash request; only initial fixture placement.");
        yield return Fresh(2); StopPolicies();
        var actor = game.Bosses[0];
        OnFloor(actor.gameObject, actor.Navigation, -3); OnFloor(game.Player.gameObject, actor.Navigation, 1);
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        yield return Seconds(.30f);
        int hp = game.Player.CurrentHealth;
        bool accepted = actor.Actions.TryStart(BossAbility.DrawDash, actor.Perception.Facts.Position);
        ActionProbe("S2 requested accepted=" + accepted, actor);
        var phase = actor.Actions.Phase;
        float end = Time.time + 3;
        while (actor.Actions.Busy && Time.time < end)
        {
            if (phase != actor.Actions.Phase) { phase = actor.Actions.Phase; ActionProbe("S2 phase transition", actor); }
            yield return null;
        }
        int lost = hp - game.Player.CurrentHealth;
        Check("S2 real swept contact damages once", accepted && actor.Actions.Activated[(int)BossAbility.DrawDash] == 1
            && lost == actor.Profile.DrawDash.Damage,
            $"hp={hp}->{game.Player.CurrentHealth} expectedDamage={actor.Profile.DrawDash.Damage} endpoint={actor.Position} blocked={actor.Motor.DashBlocked}");
        AudioProbe("S2 cues", actor, Sfx.DrawPrepare, Sfx.DashCut);
    }
    static IEnumerator LegalReturn()
    {
        Record("CONTROLLED S2->S3 legal branch: real manual jump across the dash, real landing, public delayed observation, full second tell. No TryStart(ReturnCut), mid-action relocation, immunity or cooldown edits.");
        yield return Fresh(2); StopPolicies();
        var actor = game.Bosses[0]; var actions = actor.Actions;
        var playerBody = game.Player.GetComponent<Rigidbody2D>(); var playerShape = game.Player.GetComponent<Collider2D>();
        var movement = game.Player.GetComponent<PlayerMovement>();
        OnFloor(actor.gameObject, actor.Navigation, 3); OnFloor(game.Player.gameObject, actor.Navigation, -1);
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        yield return Frames(4); yield return Seconds(.30f);
        Vector2 publicHypothesis = actor.Perception.Facts.Position;
        int hp = game.Player.CurrentHealth;
        heldKeys = new[] { Key.Space, Key.D };
        yield return Until(() => !movement.IsGrounded && playerBody.linearVelocity.y > .5f, .35f);
        bool jumped = !movement.IsGrounded && playerBody.linearVelocity.y > .5f;
        InputProbe("legal return jump");
        yield return Seconds(.04f);
        // The normal cooperative request holds this already public pre-jump position as its aim hypothesis.
        // The subsequent branch is still selected by the executor from delayed observed commitment.
        bool accepted = actions.TryStart(BossAbility.DrawDash, publicHypothesis, true);
        ActionProbe("legal parent requested accepted=" + accepted, actor);
        float moveDeadline = Time.time + .40f;
        while (playerShape.bounds.center.x < 1 && Time.time < moveDeadline) yield return null;
        heldKeys = Array.Empty<Key>();
        InputProbe("jump crossing horizontal key released");
        bool crossedAbove = false, landedAfterCrossing = false, sawReturnPrepare = false, sawReturnLock = false;
        float previousSide = playerShape.bounds.center.x - actor.Position.x;
        float prepareAt = -1, activeAt = -1, end = Time.time + 3;
        BossAbility previousAbility = actions.Ability; BossActionPhase previousPhase = actions.Phase;
        while (actions.Busy && Time.time < end && !game.Player.IsDead)
        {
            float side = playerShape.bounds.center.x - actor.Position.x;
            if (previousSide <= 0 && side > 0)
            {
                crossedAbove = playerShape.bounds.min.y >= actor.Body.bounds.max.y - .08f;
                ActionProbe("physical crossing above=" + crossedAbove, actor);
            }
            previousSide = side;
            if (crossedAbove && movement.IsGrounded) landedAfterCrossing = true;
            if (previousAbility != actions.Ability || previousPhase != actions.Phase)
            {
                previousAbility = actions.Ability; previousPhase = actions.Phase;
                ActionProbe("legal follow-up state", actor);
            }
            if (actions.Ability == BossAbility.ReturnCut)
            {
                if (actions.Phase == BossActionPhase.Prepare && !sawReturnPrepare) { sawReturnPrepare = true; prepareAt = Time.time; }
                if (actions.Phase == BossActionPhase.Locked) sawReturnLock = true;
                if (actions.Phase == BossActionPhase.Active && activeAt < 0) activeAt = Time.time;
            }
            yield return null;
        }
        Check("S2 passes a real jumping player then observes landing", jumped && accepted && crossedAbove && landedAfterCrossing,
            $"jump={jumped} parent={accepted} above={crossedAbove} landed={landedAfterCrossing} actor={actor.Position} player={playerShape.bounds.center}");
        Check("S3 branches once through legal parent window", actions.Activated[(int)BossAbility.DrawDash] == 1
            && actions.Started[(int)BossAbility.ReturnCut] == 1 && actions.Activated[(int)BossAbility.ReturnCut] == 1,
            $"S2={actions.Activated[(int)BossAbility.DrawDash]} S3started={actions.Started[(int)BossAbility.ReturnCut]} S3active={actions.Activated[(int)BossAbility.ReturnCut]} branchWindow={actor.Profile.ReturnBranchWindow:F3}");
        Check("S3 has its own complete visible preparation/lock", sawReturnPrepare && sawReturnLock && activeAt > 0
            && activeAt - prepareAt >= actor.Profile.ReturnCut.Prepare + actor.Profile.ReturnCut.Lock - .055f,
            $"prepareAt={prepareAt:F3} activeAt={activeAt:F3} tell={activeAt-prepareAt:F3}");
        Record($"LEGAL FOLLOWUP damage hp={hp}->{game.Player.CurrentHealth}; real hit or miss remains an executor outcome, not injected damage.");
        AudioProbe("S2/S3 cues", actor, Sfx.DrawPrepare, Sfx.ReturnPrepare, Sfx.DashCut);
        heldKeys = Array.Empty<Key>();
    }
    static IEnumerator DropContact()
    {
        Record("CONTROLLED S4 contact: Boss jumps vertically; actual manual walking places player under the airborne body before the legal descending attack. No airborne reposition or direct damage.");
        yield return Fresh(2); StopPolicies();
        var actor = game.Bosses[0]; var playerShape = game.Player.GetComponent<Collider2D>();
        OnFloor(actor.gameObject, actor.Navigation, 0); OnFloor(game.Player.gameObject, actor.Navigation, 1.4f);
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        yield return Frames(4); yield return Seconds(.25f);
        bool jumped = actor.Motor.TryJump();
        heldKeys = new[] { Key.A };
        float end = Time.time + .37f;
        while (playerShape.bounds.center.x > actor.Position.x + .12f && Time.time < end) yield return null;
        heldKeys = Array.Empty<Key>();
        int hp = game.Player.CurrentHealth;
        bool airborne = !actor.Motor.Grounded && actor.Velocity.y > -.5f;
        bool accepted = actor.Actions.TryStart(BossAbility.DropCut, playerShape.bounds.center);
        ActionProbe("drop requested jumped=" + jumped + " airborne=" + airborne + " accepted=" + accepted, actor);
        var phase = actor.Actions.Phase; end = Time.time + 3;
        while (actor.Actions.Busy && Time.time < end)
        {
            if (phase != actor.Actions.Phase) { phase = actor.Actions.Phase; ActionProbe("drop phase transition", actor); }
            yield return null;
        }
        Check("S4 real jump and downward blade contact damage", jumped && airborne && accepted
            && actor.Actions.Activated[(int)BossAbility.DropCut] == 1 && game.Player.CurrentHealth < hp,
            $"hp={hp}->{game.Player.CurrentHealth} position={actor.Position} player={playerShape.bounds.center}");
        AudioProbe("S4 cue", actor, Sfx.BladeHeavy);
    }
    static void AudioProbe(string label, BossAgent actor, params Sfx[] cues)
    {
        var set = actor.Sounds;
        var output = set != null ? set.GetComponent<PresentationOneShot>() : null;
        bool valid = set != null && output != null;
        foreach (var cue in cues)
        {
            var clip = set != null ? set.Clip(cue) : null;
            int requested = set != null ? set.Requested(cue) : 0;
            bool clipReady = clip != null && clip.length > 0 && clip.samples > 0;
            valid &= clipReady && requested > 0;
            Record($"AUDIO {label} cue={cue} requested={requested} clip={(clip!=null?clip.name:"null")} "
                + $"length={(clip!=null?clip.length:0):F3} samples={(clip!=null?clip.samples:0)} "
                + $"loadState={(clip!=null?clip.loadState.ToString():"null")} path={(clip!=null?AssetDatabase.GetAssetPath(clip):"null")}");
        }
        Check(label + " valid clips + event requests + AudioSource.Play calls", valid && output.PlayedCount >= cues.Length && output.DroppedCount == 0,
            $"played={(output!=null?output.PlayedCount:0)} dropped={(output!=null?output.DroppedCount:0)} voices={(output!=null?output.VoiceCount:0)}; audibility NOT TESTED");
    }

    static IEnumerator NavigationChanges()
    {
        Record("CONTROLLED navigation: temporary runtime layout edits are restored before normal observations.");
        yield return Fresh(1); StopPolicies();
        var actor = game.Bosses[0]; var nav = actor.Navigation;
        var platform = nav.Nodes.Select(n => n.Collider).Distinct().FirstOrDefault(c => c.GetComponent<PlatformEffector2D>() != null);
        if (platform == null) { Check("navigation has an actual one-way platform", false); yield break; }
        Vector3 original = platform.transform.position; bool enabled = platform.enabled;
        restoration.Add(() => { if (platform != null) { platform.transform.position = original; platform.enabled = enabled; } });
        int version = nav.Version; platform.transform.position += new Vector3(.7f, .25f); Physics2D.SyncTransforms();
        yield return Until(() => nav.Version > version, 1.2f);
        Check("moved platform updates geometry", nav.Version > version && nav.Nodes.Any(n => n.Collider == platform));
        yield return FollowAnyJump(actor, nav, platform, "moved layout");
        version = nav.Version; platform.enabled = false;
        var temporary = new GameObject("BossSmoke temporary surface") { layer = LayerMask.NameToLayer("Ground") };
        temporary.transform.position = original + new Vector3(-.7f, .15f);
        var shape = temporary.AddComponent<BoxCollider2D>(); shape.size = new Vector2(3, .2f); shape.usedByEffector = true;
        var effector = temporary.AddComponent<PlatformEffector2D>(); effector.useOneWay = true; effector.surfaceArc = 160;
        restoration.Add(() => { if (temporary != null) Object.DestroyImmediate(temporary); });
        Physics2D.SyncTransforms(); yield return Until(() => nav.Version > version, 1.2f);
        Check("disable + new collider updates geometry", nav.Version > version
            && nav.Nodes.All(n => n.Collider != platform) && nav.Nodes.Any(n => n.Collider == shape));
        yield return FollowAnyJump(actor, nav, shape, "replacement layout");
        RestoreFixture(); Physics2D.SyncTransforms(); nav.Refresh();
        Check("temporary terrain restored", platform.enabled == enabled && platform.transform.position == original);
    }
    static IEnumerator FollowAnyJump(BossAgent actor, ArenaNavigation2D nav, Collider2D editedSurface, string label)
    {
        // Fixture placement only. The measured route must actually land on the edited surface.
        float floor = nav.Nodes.Min(n => n.Position.y);
        Position(actor.gameObject, new Vector2(editedSurface.bounds.center.x - 2,
            floor + actor.Motor.Size.y * .5f + .025f));
        actor.Motor.Stop();
        yield return Until(() => actor.Motor.Grounded, 2);
        ArenaNavigation2D.Route chosen = null;
        foreach (var node in nav.Nodes.Where(n => n.Collider == editedSurface).OrderBy(n => Vector2.Distance(n.Position, actor.Position)))
            if (nav.TryRoute(actor.Position, node.Center(actor.Motor.Size), actor.Motor, out var route)
                && route.Steps.Any(step => step.Kind == ArenaNavigation2D.TravelKind.Jump))
            { chosen = route; break; }
        Check(label + " produces reachable jump route", chosen != null);
        if (chosen == null) yield break;
        for (int i = 0; i < chosen.Steps.Count; i++)
        {
            var step = chosen.Steps[i];
            Record($"NAV {label} step={i} kind={step.Kind} start={step.Start} launch={step.Launch} end={step.End} "
                + $"duration={step.Duration:F3} surface={(step.Support!=null?step.Support.name:"missing")} version={chosen.Version}");
        }
        actor.Motor.SetRoute(chosen); bool rose = false; float start = Time.time;
        Vector2 last = actor.Position; bool continuous = true;
        while (Time.time - start < 7 && actor.Motor.HasRoute)
        {
            if (!actor.Motor.Grounded && actor.Velocity.y > .5f) rose = true;
            if (Vector2.Distance(actor.Position, last) > 1.2f) continuous = false;
            last = actor.Position; yield return null;
        }
        Check(label + " executes physical jump/landing", rose && continuous && actor.Motor.RouteCompleted && actor.Motor.Grounded
            && nav.TrySupport(actor.Position, actor.Motor.Size, out var landed) && landed == editedSurface,
            "position=" + actor.Position + " failed=" + actor.Motor.RouteFailed + " failure=" + actor.Motor.RouteFailure);
    }

    static IEnumerator Skills()
    {
        Record("CONTROLLED skill wiring: fresh actors, real executors/projectiles; no tuning, immunity or artificial damage.");
        foreach (var ability in new[] { BossAbility.Burst, BossAbility.Camo, BossAbility.Piercing, BossAbility.Sweep,
            BossAbility.Rhythm, BossAbility.DrawDash, BossAbility.DropCut, BossAbility.Guard })
        {
            bool gunner = ability <= BossAbility.Sweep;
            yield return Fresh(gunner ? 1 : 2); StopPolicies();
            var actor = game.Bosses[0]; var actions = actor.Actions;
            game.Player.GetComponent<PlayerInputReader>().SetGameplayInputEnabled(false);
            Position(actor.gameObject, new Vector2(-3, -1.65f));
            Position(game.Player.gameObject, new Vector2(ability == BossAbility.Rhythm ? -1.6f : 1, -1.65f));
            yield return Until(() => actor.Motor.Grounded, 1);
            float observedUntil = Time.time + .3f;
            while (Time.time < observedUntil) { actor.Perception.Tick(actor.Position); yield return null; }
            if (ability == BossAbility.DropCut)
            {
                Check("drop starts with a real jump", actor.Motor.TryJump());
                yield return Until(() => !actor.Motor.Grounded && actor.Velocity.y > 0, .4f);
            }
            int initial = actions.Activated[(int)ability], hp = game.Player.CurrentHealth;
            bool accepted = actions.TryStart(ability, game.Player.GetComponent<Collider2D>().bounds.center);
            Check(ability + " accepted by normal executor", accepted);
            if (!accepted) continue;
            bool sawLocked = false, sawActive = false, sawRecovery = false, guardFront = false, guardRear = false;
            int actorHp = actor.Health.CurrentHealth;
            float started = Time.time;
            while (Time.time - started < 5 && actions.Busy && !game.Player.IsDead)
            {
                actor.Perception.Tick(actor.Position);
                sawLocked |= actions.Phase == BossActionPhase.Locked;
                sawActive |= actions.Phase == BossActionPhase.Active;
                sawRecovery |= actions.Phase == BossActionPhase.Recovery;
                if (ability == BossAbility.Guard && actions.Phase == BossActionPhase.Active)
                {
                    if (!guardFront)
                    { guardFront = true; Fire(actor, game.Player.gameObject, actor.Position + actions.Aim * .8f, -actions.Aim); }
                    else if (!guardRear && actions.Guarded > 0)
                    { guardRear = true; Fire(actor, game.Player.gameObject, actor.Position - actions.Aim * .8f, actions.Aim); }
                }
                yield return null;
            }
            Check(ability + " actually active/recovered", actions.Activated[(int)ability] > initial && sawActive && sawRecovery,
                $"locked={sawLocked} hp={hp}->{game.Player.CurrentHealth} guarded={actions.Guarded}");
            if (ability == BossAbility.Guard)
            {
                Check("guard consumes a real frontal projectile", actions.Guarded > 0);
                Check("guard rear remains damageable", guardRear && actor.Health.CurrentHealth < actorHp);
            }
            if (ability == BossAbility.DrawDash)
                Record("ReturnCut activation after parent=" + actions.Activated[(int)BossAbility.ReturnCut]
                    + "; zero needs a separate legal parent-window check, not direct ReturnCut invocation.");
        }
        Record("NOT TESTED: ReturnCut legal follow-up if no parent-window activation above; natural duo completion/abort measured by observation logs.");
    }
    static IEnumerator TeamBlocking()
    {
        Record("CONTROLLED projectile fixture: repositioned actors and temporary cover; mechanism evidence only.");
        yield return Fresh(3); StopPolicies();
        foreach (var shot in Projectile.Active.ToArray()) shot.Retire();
        var gunner = game.Bosses[0]; var sword = game.Bosses[1];
        game.Player.GetComponent<PlayerInputReader>().SetGameplayInputEnabled(false);
        Position(gunner.gameObject, new Vector2(-6, -1.65f)); Position(sword.gameObject, new Vector2(-1, -1.65f));
        Position(game.Player.gameObject, new Vector2(3, -1.65f)); yield return Seconds(.12f);
        int playerHp = game.Player.CurrentHealth, swordHp = sword.Health.CurrentHealth;
        Fire(gunner, gunner.gameObject, gunner.Position + Vector2.right, Vector2.right);
        yield return Seconds(.65f);
        Check("ally body blocks ordinary bullet with zero friendly damage", game.Player.CurrentHealth == playerHp
            && sword.Health.CurrentHealth == swordHp && Projectile.Active.Count == 0);
        Position(sword.gameObject, new Vector2(7, -1.65f));
        yield return Seconds(.3f);
        Check("beam starts through normal solo reservation", gunner.Actions.TryStart(BossAbility.Piercing, game.Player.GetComponent<Collider2D>().bounds.center));
        yield return Until(() => gunner.Actions.Phase == BossActionPhase.Locked, 1.2f);
        Position(sword.gameObject, new Vector2(-1, -1.65f));
        yield return Until(() => gunner.Actions.Activated[(int)BossAbility.Piercing] > 0, 2);
        yield return Seconds(.12f);
        Check("ally body blocks beam with zero friendly damage", gunner.Actions.Activated[(int)BossAbility.Piercing] > 0
            && game.Player.CurrentHealth == playerHp && sword.Health.CurrentHealth == swordHp);
        Position(sword.gameObject, new Vector2(7, -1.65f));
        var cover = new GameObject("BossSmoke projectile cover") { layer = LayerMask.NameToLayer("Ground") };
        cover.transform.position = new Vector2(0, -1.4f);
        cover.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 2.1f);
        restoration.Add(() => { if (cover != null) Object.DestroyImmediate(cover); });
        Physics2D.SyncTransforms();
        int gunnerHp = gunner.Health.CurrentHealth;
        Fire(gunner, gunner.gameObject, gunner.Position + Vector2.right, Vector2.right);
        var playerShot = Object.Instantiate(Private<Projectile>(game.Rifle, "bulletPrefab"),
            (Vector2)game.Player.transform.position + Vector2.left, Quaternion.identity);
        playerShot.Launch(Vector2.left, game.Rifle.BulletSpeed, game.Rifle.BulletDamage, game.Player.gameObject);
        yield return Seconds(.6f);
        Check("player and Boss bullets cross Ground and damage opposing bodies once", game.Player.CurrentHealth == playerHp - 12
            && gunner.Health.CurrentHealth == gunnerHp - game.Rifle.BulletDamage && Projectile.Active.Count == 0);
        Check("Boss firing line crosses cover while geometric terrain query still blocks", gunner.Tactics.ClearGunLine(gunner.Position, game.Player.transform.position)
            && Projectile.TerrainHit(gunner.Position, game.Player.transform.position, .06f, out _));
        yield return Until(() => !gunner.Actions.Busy && gunner.Actions.Ready(BossAbility.Piercing), 5);
        playerHp = game.Player.CurrentHealth;
        int activations = gunner.Actions.Activated[(int)BossAbility.Piercing];
        Check("beam through cover starts normally", gunner.Actions.TryStart(BossAbility.Piercing, game.Player.GetComponent<Collider2D>().bounds.center));
        yield return Until(() => gunner.Actions.Activated[(int)BossAbility.Piercing] > activations, 2);
        Check("special beam penetrates Ground via real hit", gunner.Actions.Activated[(int)BossAbility.Piercing] > activations
            && game.Player.CurrentHealth < playerHp);
        cover.transform.position = new Vector2(0, 3);
        var platform = cover.GetComponent<BoxCollider2D>();
        platform.size = new Vector2(4, .3f);
        var effector = cover.AddComponent<PlatformEffector2D>(); effector.useOneWay = true;
        platform.usedByEffector = true;
        Physics2D.SyncTransforms();
        var upward = Object.Instantiate(Private<Projectile>(game.Rifle, "bulletPrefab"), new Vector2(-.5f, 2), Quaternion.identity);
        var downward = Object.Instantiate(gunner.ProjectilePrefab, new Vector2(.5f, 4), Quaternion.identity);
        upward.Launch(Vector2.up, 4, 12, game.Player.gameObject);
        downward.Launch(Vector2.down, 4, 12, gunner.gameObject);
        yield return Seconds(.4f);
        Check("bullets cross both sides of a one-way platform", upward != null && downward != null
            && upward.transform.position.y > 3.3f && downward.transform.position.y < 2.7f);
        if (upward != null) upward.Retire();
        if (downward != null) downward.Retire();
        RestoreFixture(); Physics2D.SyncTransforms();
    }

    static IEnumerator Coordination()
    {
        Record("CONTROLLED duo: initial pincer positions only; local tactics idle, normal coordinator candidates/routes/resources/timelines. No forced readiness or damage.");
        for(int pass=0;pass<2;pass++)
        {
            yield return Fresh(3); StopPolicies();
            var gunner=game.Bosses[0]; var sword=game.Bosses[1];
            OnFloor(gunner.gameObject,gunner.Navigation,-7); OnFloor(sword.gameObject,gunner.Navigation,3);
            OnFloor(game.Player.gameObject,gunner.Navigation,0);
            yield return Seconds(.35f);
            var duo=game.GetComponentInChildren<DuoCoordinator>(); duo.enabled=true;
            var previous=duo.Phase; float until=Time.time+10; bool edited=false;
            while(Time.time<until&&duo.CombosCompleted==0&&duo.CombosAborted==0)
            {
                if(previous!=duo.Phase)
                {previous=duo.Phase;Record($"DUO pass={pass} phase={duo.Phase} gun={gunner.Position} sword={sword.Position}");}
                if(pass==1&&!edited&&duo.Phase==DuoPhase.Assemble)
                {
                    var surface=gunner.Navigation.Nodes.Select(n=>n.Collider).First(c=>c.GetComponent<PlatformEffector2D>()!=null);
                    var original=surface.transform.position;
                    restoration.Add(()=>{if(surface!=null)surface.transform.position=original;});
                    surface.transform.position+=Vector3.right*.2f;Physics2D.SyncTransforms();gunner.Navigation.Refresh();edited=true;
                }
                yield return null;
            }
            if(pass==0)
                Check("duo reaches real commitment/follow-up/recovery",duo.CombosCompleted==1
                    &&gunner.Actions.Activated[(int)BossAbility.Piercing]>0&&sword.Actions.Activated[(int)BossAbility.DrawDash]>0,
                    $"started={duo.CombosStarted} complete={duo.CombosCompleted} abort={duo.CombosAborted} phase={duo.Phase} playerHP={game.Player.CurrentHealth}");
            else Check("terrain change aborts assembly and releases tasks",edited&&duo.CombosAborted==1&&duo.Phase==DuoPhase.Recover
                &&!gunner.Motor.HasRoute&&!sword.Motor.HasRoute,$"edited={edited} abort={duo.CombosAborted} phase={duo.Phase}");
            RestoreFixture();Physics2D.SyncTransforms();
        }
    }

    static IEnumerator CornerChecks()
    {
        Record("CONTROLLED corner mechanisms: initial placement only, normal resources and physics. Not natural fight evidence.");
        yield return Fresh(2,true); StopPolicies();
        var player=game.Player;var sword=game.Bosses[0];
        var playerShape=player.GetComponent<Collider2D>();
        float floor=sword.Navigation.ArenaBounds.min.y;
        Position(player.gameObject,new Vector2(-13.7f,floor+playerShape.bounds.extents.y+.03f));
        Position(sword.gameObject,new Vector2(-12.8f,floor+sword.Body.bounds.extents.y+.03f));
        bool rose=false;float farthest=player.transform.position.x;
        float until=Time.time+4;
        while(Time.time<until&&game.Phase==DuelPhase.Fight)
        {
            rose|=player.GetComponent<Rigidbody2D>().linearVelocity.y>4;
            farthest=Mathf.Max(farthest,player.transform.position.x);yield return null;
        }
        Check("Legs physically jumps over contact and leaves left corner",rose&&farthest>-11,
            $"jump={rose} farthestX={farthest:F2}");
        Check("player movement and Dodge values retained",Mathf.Approximately(player.GetComponent<PlayerMovement>().MoveSpeed,7)
            &&Mathf.Approximately(player.GetComponent<PlayerMovement>().JumpSpeed,12)&&Mathf.Approximately(game.Dodge.Distance,3));

        Record("MIXED corner: Legs stays manual with no movement input; Arms and Spine use their normal AI permissions.");
        yield return Fresh(3,false,true);
        var gunner=game.Bosses.First(a=>a.Profile.Kind==BossKind.Refraction);
        sword=game.Bosses.First(a=>a.Profile.Kind==BossKind.Severance);player=game.Player;
        playerShape=player.GetComponent<Collider2D>();floor=sword.Navigation.ArenaBounds.min.y;
        Position(player.gameObject,new Vector2(-13.7f,floor+playerShape.bounds.extents.y+.03f));
        Position(sword.gameObject,new Vector2(-12.8f,floor+sword.Body.bounds.extents.y+.03f));
        Position(gunner.gameObject,new Vector2(-7,floor+gunner.Body.bounds.extents.y+.03f));
        bool blocked=false,opened=false;int gunBefore=gunner.Actions.Activated[(int)BossAbility.Burst]+gunner.Actions.Activated[(int)BossAbility.Piercing];
        until=Time.time+12;float snapshotAt=0;
        while(Time.time<until&&game.Phase==DuelPhase.Fight)
        {
            if(Time.time>=snapshotAt)
            {
                snapshotAt=Time.time+1;
                Record("LANE duo="+game.GetComponentInChildren<DuoCoordinator>().Phase+" "+string.Join(";",game.Bosses.Select(a=>
                    $"{a.Profile.Kind} pos={a.Position} ground={a.Motor.Grounded} action={a.Actions.Ability}/{a.Actions.Phase} plan={a.Tactics.Current?.Intent}/{a.Tactics.Current?.Destination} failed={a.Motor.RouteFailure} confidence={a.Perception.Facts.Confidence:F2}")));
            }
            if(gunner.Perception.Facts.Confidence>.3f)
            {
                bool line=gunner.Tactics.TryGunTarget(BossAbility.Burst,gunner.Position,gunner.Perception.Facts,out _);
                blocked|=!line;opened|=blocked&&line;
            }
            yield return null;
        }
        int gunAfter=gunner.Actions.Activated[(int)BossAbility.Burst]+gunner.Actions.Activated[(int)BossAbility.Piercing];
        Check("blocked gunner obtains a physical lane and attacks",blocked&&opened&&gunAfter>gunBefore,
            $"blocked={blocked} opened={opened} gunAttacks={gunAfter-gunBefore}");
        Check("stationary corner remains vulnerable to real attacks",player.CurrentHealth<150,
            $"playerHP={player.CurrentHealth} swordHits={sword.Actions.ConnectedHits} gunHits={gunner.Actions.ConnectedHits}");
    }
    static IEnumerator Observe(int wave=3)
    {
        RestoreFixture(); heldKeys = Array.Empty<Key>(); mouseHeld = false;
        game.MainMenu(); yield return Frames(2);
        Record("NATURAL ALL-AI W"+wave+" " + "fixed policy" + ": fresh actors, normal resources, no damage/position/control interventions; limit 120 combat seconds.");
        if(wave==3)game.ObserveThirdWave();else yield return Fresh(2,true);
        yield return Until(() => game.Phase == DuelPhase.Fight, 2);
        float corner=0,longestCorner=0,nextSnapshot=0,minX=float.MaxValue,maxX=float.MinValue;
        int jumps=0,dodges=0;bool airborne=false,dodging=false;
        while (game.Phase == DuelPhase.Fight && game.FightClock < 120)
        {
            var shape=game.Player.GetComponent<Collider2D>();var movement=game.Player.GetComponent<PlayerMovement>();
            var arena=game.Bosses[0].Navigation.ArenaBounds;
            bool nearWall=Mathf.Min(shape.bounds.min.x-arena.min.x,arena.max.x-shape.bounds.max.x)<1.5f;
            corner=nearWall?corner+Time.deltaTime:0;longestCorner=Mathf.Max(longestCorner,corner);
            minX=Mathf.Min(minX,shape.bounds.center.x);maxX=Mathf.Max(maxX,shape.bounds.center.x);
            bool rising=game.Player.GetComponent<Rigidbody2D>().linearVelocity.y>2;
            if(rising&&!airborne)jumps++;airborne=!movement.IsGrounded;
            if(game.Dodge.IsDodging&&!dodging)dodges++;dodging=game.Dodge.IsDodging;
            if(game.FightClock>=nextSnapshot)
            {
                nextSnapshot=game.FightClock+2;
                Record($"POSITION W{wave} t={game.FightClock:F1} player={shape.bounds.center} hp={game.Player.CurrentHealth} "
                    +string.Join(";",game.Bosses.Select(a=>$"{a.Profile.Kind}={a.Position}/{a.Actions.Ability}/{a.Tactics.Current?.Intent}")));
            }
            yield return null;
        }
        string health = string.Join(",", game.Bosses.Select(a => a.ActorId + ":" + a.Health.CurrentHealth));
        bool defeated = game.Phase == DuelPhase.Failed && game.Player.IsDead;
        Record($"NATURAL RESULT fixed phase={game.Phase} seconds={game.FightClock:0.00} player={game.Player.CurrentHealth} bosses={health} "
            + (defeated ? "OBSERVED_DEFEAT" : wave==3?"OBSERVED_NON_DEFEAT":"W2_NON_DEFEAT"));
        Record($"MOVEMENT W{wave} longestCorner={longestCorner:F2}s xRange={minX:F2}..{maxX:F2} jumps={jumps} dodges={dodges}");
        foreach (var actor in game.Bosses)
            Record($"NATURAL actor={actor.ActorId} started={string.Join(",", actor.Actions.Started)} active={string.Join(",", actor.Actions.Activated)} guard={actor.Actions.Guarded} beamAllyBlocks={actor.Actions.BeamAllyBlocks}");
        var coordinator=game.GetComponentInChildren<DuoCoordinator>();
        Record($"NATURAL duo started={coordinator.CombosStarted} complete={coordinator.CombosCompleted} abort={coordinator.CombosAborted}");
        // A non-defeat is reported distinctly, and is not turned into a scripted failure/death.
        game.MainMenu(); yield return Frames(2);
    }
    static void Position(GameObject actor, Vector2 centre)
    {
        var rb = actor.GetComponent<Rigidbody2D>(); var shape = actor.GetComponent<Collider2D>();
        rb.position = centre - ((Vector2)shape.bounds.center - rb.position); rb.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }
    static void Fire(BossAgent prefabOwner, GameObject owner, Vector2 from, Vector2 direction)
    {
        var projectile = Object.Instantiate(prefabOwner.ProjectilePrefab, from, Quaternion.identity);
        projectile.Launch(direction, 18, 12, owner);
    }
    static void MouseAt(Vector2 world, bool held)
    { mousePosition = Camera.main.WorldToScreenPoint(world); mouseHeld = held; }
    static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    static IEnumerator Seconds(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static IEnumerator RealSeconds(double seconds)
    { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) yield return null; }
    static IEnumerator Until(Func<bool> condition, float seconds)
    { float end = Time.time + seconds; while (!condition() && Time.time < end) yield return null; }
    static void Check(string label, bool passed, string detail = "")
    { if (!passed) failures++; Record((passed ? "PASS " : "FAIL ") + label + (detail == "" ? "" : " — " + detail)); }
    static void Record(string line) { lines.Add(line); Debug.Log("[BossSmoke] " + line); }
    static void RestoreFixture()
    {
        for (int i = restoration.Count - 1; i >= 0; i--) restoration[i]();
        restoration.Clear();
    }
    static void SetupInput()
    {
        previousBackground = InputSystem.settings.backgroundBehavior;
        previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        previousRunInBackground = Application.runInBackground;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Application.runInBackground = true;
        keyboard = InputSystem.AddDevice<Keyboard>("BossSmokeKeyboard"); mouse = InputSystem.AddDevice<Mouse>("BossSmokeMouse");
        foreach (var device in InputSystem.devices)
            if (device != keyboard && device != mouse && device.enabled && (device is Keyboard || device is Pointer))
            { InputSystem.DisableDevice(device); disabledDevices.Add(device); }
        inputReady = true;
    }
    static void Finish(bool completed)
    {
        bool exit = batch;
        Running = false; Done = true; SessionState.EraseString(SessionKey);
        EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= PlayState;
        Application.logMessageReceived -= Capture;
        while (work.Count > 0) (work.Pop() as IDisposable)?.Dispose();
        RestoreFixture();
        if (game != null && EditorApplication.isPlaying) game.MainMenu();
        if (inputReady)
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (var device in disabledDevices) if (device != null && device.added) InputSystem.EnableDevice(device);
            disabledDevices.Clear();
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Application.runInBackground = previousRunInBackground;
        }
        inputReady = false; checkingPause = false; frame = -1; focusedReader = null;
        heldKeys = Array.Empty<Key>(); mouseHeld = false;
        lines.Add($"SUMMARY completed={completed} failures={failures}. No win-rate guarantee; no audio-audition claim.");
        Directory.CreateDirectory(Output); File.WriteAllLines(Output + mode + ".txt", lines);
        Debug.Log("[BossSmoke] saved " + Output + mode + ".txt");
        if (exit) EditorApplication.Exit(completed && failures == 0 ? 0 : 1);
    }
}
#endif
