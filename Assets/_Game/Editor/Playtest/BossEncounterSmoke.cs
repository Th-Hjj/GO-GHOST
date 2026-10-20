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
public static partial class BossEncounterSmoke
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
    public static void RunFocusedPressure() => Start("focused-pressure", false);
    public static void RunPressureMechanics() => Start("pressure-mechanics", false);
    public static void RunSoloPressureMechanics() => Start("solo-pressure-mechanics", false);
    public static void RunSoloPressureObservation() => Start("solo-pressure-observation", false);
    public static void RunSoloDropMechanics() => Start("solo-drop-mechanics", false);
    public static void RunSoloPressureEdges() => Start("solo-pressure-edges", false);
    public static void RunSoloPressureFinal() => Start("solo-pressure-final", false);
    public static void RunPlayerAnimation() => Start("player-animation", false);
    public static void RunPlayerOpening() => Start("player-opening", false);
    public static void RunPlayerGunPose() => Start("player-gun-pose", false);
    public static void RunWaveMusic() => Start("wave-music", false);
    public static void RunMusicLevels() => Start("music-levels", false);
    public static void RunPursuitFlank() => Start("pursuit-flank", false);
    public static void RunPursuitMechanics() => Start("pursuit-mechanics", false);
    public static void RunPursuitObservation() => Start("pursuit-observation", false);
    public static void RunDashMobility() => Start("dash-mobility", false);
    public static void RunReleaseSmoke() => Start("release", false);
    public static void RunGuardReflection() => Start("guard-reflection", false);
    public static void RunDirectionalDash() => Start("directional-dash", false);
    public static void RunSwordIntelligence() => Start("sword-intelligence", false);
    public static void RunSwordRework() => Start("sword-rework", false);
    public static void RunSwordReworkProbe() => Start("sword-rework-probe", false);
    public static void RunSwordReworkLifecycle() => Start("sword-rework-lifecycle", false);
    public static void RunSwordFollowup() => Start("sword-followup", false);
    public static void RunSwordFollowupLinks() => Start("sword-followup-links", false);
    public static void RunSwordPlatforms() => Start("sword-platforms", false);
    public static void RunSwordPlatformLink() => Start("sword-platform-link", false);
    public static void RunSwordPressureBaseline() => Start("sword-pressure-baseline", false);
    public static void RunSwordPressure() => Start("sword-pressure", false);
    public static void RunSwordPressureFlow() => Start("sword-pressure-flow", false);
    public static void RunSwordAfterbladeAdjustment() => Start("sword-afterblade-adjustment", false);
    public static void RunSwordPhraseBlades() => Start("sword-phrase-blades", false);
    public static void RunSwordCloseReadability() => Start("sword-close-readability", false);
    public static void RunSwordPerformance() => Start("sword-performance", false);
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
        if (message.StartsWith("[Boss ") || message.StartsWith("[Duo") || message.StartsWith("[Legs ") || message.StartsWith("[LegacyCombat"))
            lines.Add("EVENT " + message);
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        { lines.Add("ERROR " + message + "\n" + trace); failures++; }
    }
    static partial void SelectLegacyScenario(string requested, ref IEnumerator scenario);

    static IEnumerator Run()
    {
        Record("Editor Play Mode; simulated focus and virtual keyboard/mouse. No physical-input or audibility claim.");
        IEnumerator legacyScenario = null;
        SelectLegacyScenario(mode, ref legacyScenario);
        if (legacyScenario != null) { yield return legacyScenario; yield break; }
        if(mode=="player-turning") {yield return PlayerTurning();yield break;}
        if(mode=="behavior-corrections") {yield return BehaviorCorrections();yield break;}
        if(mode=="behavior-public-facts") {yield return BehaviorPublicFacts();yield break;}
        if(mode=="behavior-platforms") {yield return BehaviorPlatforms();yield break;}
        if(mode=="behavior-catalog") {yield return BehaviorCatalog();yield break;}
        if(mode=="boss-body-scale") {yield return BossBodyScale();yield break;}
        if(mode=="jump-platform") {yield return JumpPlatform();yield break;}
        if(mode=="jump-platform-edges") {yield return JumpPlatformEdges();yield break;}
        if(mode=="map-gunner") {yield return MapGunner();yield break;}
        if(mode=="duo-camo-tell") {yield return DuoCamoTell();yield break;}
        if(mode=="dash-bodies") {yield return DashBodies();yield break;}
        if(mode=="stage-platforms") {yield return StagePlatforms();yield break;}
        if(mode=="gunner-platforms") {yield return GunnerPlatforms();yield break;}
        if(mode=="gunner-crossings") {yield return GunnerPlatformCrossings();yield break;}
        if(mode=="training-room") {yield return TrainingRoom();yield break;}
        if(mode=="training-patrol") {yield return TrainingPatrol();yield break;}
        if(mode=="training-threat") {yield return TrainingThreat();yield break;}
        if(mode=="gunner-vfx") {yield return GunnerVfx();yield break;}
        if(mode=="wave-performance-mechanisms") {yield return WavePerformanceMechanisms();yield break;}
        if(mode.StartsWith("wave-performance-")) {yield return WavePerformance();yield break;}
        if(mode=="guard-reflection") {yield return GuardReflection();yield break;}
        if(mode=="directional-dash") {yield return DirectionalDash();yield break;}
        if(mode=="sword-intelligence") {yield return SwordIntelligence();yield break;}
        if(mode=="sword-rework") {yield return SwordRework();yield break;}
        if(mode=="sword-rework-probe") {yield return SwordReworkProbe();yield break;}
        if(mode=="sword-rework-lifecycle") {yield return SwordReworkLifecycle();yield break;}
        if(mode=="sword-followup") {yield return SwordFollowup();yield break;}
        if(mode=="sword-followup-links") {yield return SwordFollowupLinks();yield break;}
        if(mode=="sword-platforms") {yield return SwordPlatforms();yield break;}
        if(mode=="sword-platform-link") {yield return SwordPlatforms(true);yield break;}
        if(mode=="sword-pressure-baseline") {yield return SwordPressureBaseline();yield break;}
        if(mode=="sword-pressure") {yield return SwordPressure();yield break;}
        if(mode=="sword-pressure-flow") {yield return SwordPressureFlow();yield break;}
        if(mode=="sword-afterblade-adjustment") {yield return SwordAfterbladeAdjustment();yield break;}
        if(mode=="sword-phrase-blades") {yield return SwordPhraseBlades();yield break;}
        if(mode=="sword-close-readability") {yield return SwordCloseReadability();yield break;}
        if(mode=="sword-performance") {yield return SwordPerformance();yield break;}
        if(mode=="cold-light") {yield return ColdLight();yield break;}
        if(mode=="cold-light-review") {yield return ColdLightReview();yield break;}
        if(mode=="cold-light-final") {yield return ColdLightFinal();yield break;}
        if(mode=="cold-light-projectiles") {yield return ColdLightProjectiles();yield break;}
        if(mode=="cold-light-closeout") {yield return ColdLightCloseout();yield break;}
        if(mode=="cold-light-layout") {yield return ColdLightLayoutReview();yield break;}
        if(mode=="typography") {yield return Typography();yield break;}
        if(mode=="typography-followup") {yield return TypographyFollowup();yield break;}
        if(mode=="plex-ui") {yield return PlexUI();yield break;}
        if(mode=="ui-polish") {yield return PlexUI(true);yield break;}
        if(mode=="ui-polish-focus") {yield return UIPolishFocus();yield break;}
        if(mode=="compact-rule-controls") {yield return CompactRuleControls();yield break;}
        if(mode=="selector-spacing") {yield return SelectorSpacing();yield break;}
        if(mode=="contour-pulse") {yield return ContourPulse();yield break;}
        if(mode=="contour-visibility") {yield return ContourVisibility();yield break;}
        if(mode=="developer-tuning") {yield return DeveloperTuning();yield break;}
        if(mode=="developer-wave") {yield return DeveloperWaveJump();yield break;}
        if(mode=="release") {yield return ReleaseSmoke();yield break;}
        if(mode=="pursuit-flank") {yield return PursuitFlank();yield break;}
        if(mode=="pursuit-mechanics") {yield return PursuitMechanics();yield break;}
        if(mode=="pursuit-observation") {yield return PursuitObservation();yield break;}
        if(mode=="dash-mobility") {yield return DashMobility();yield break;}
        if(mode=="wave-music") {yield return WaveMusic();yield break;}
        if(mode=="music-levels") {yield return MusicLevels();yield break;}
        if(mode=="player-opening") {yield return PlayerOpening();yield break;}
        if(mode=="solo-pressure-mechanics"||mode=="solo-drop-mechanics") {yield return SoloPressureMechanics();yield break;}
        if(mode=="solo-pressure-observation") {yield return SoloPressureObservation();yield break;}
        if(mode=="solo-pressure-final") {yield return SoloPressureObservation();yield break;}
        if(mode=="solo-pressure-edges") {yield return SoloPressureEdges();yield break;}
        if(mode=="player-animation") {yield return PlayerAnimation();yield break;}
        if(mode=="player-gun-pose") {yield return PlayerGunPose();yield break;}
        if(mode=="focused-pressure"||mode=="pressure-mechanics")
        {yield return PressureMechanics();if(mode=="focused-pressure")yield return PressureObservation();yield break;}
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

    static IEnumerator Fresh(int wave, bool automatic = false, bool stationaryAutomatic = false, bool manualPursuit = false)
    {
        if(automatic||stationaryAutomatic||manualPursuit)throw new InvalidOperationException("Historical permanent-AI fixture retired. Use current condition-action checks with explicit conditional loadouts.");
        RestoreFixture(); heldKeys = Array.Empty<Key>(); mouseHeld = false;
        game.NewGame();
        // Controlled entry changes only the shell wave selection, never actor resources or outcomes.
        typeof(DuelGame).GetProperty("Wave").SetValue(game, wave);
        if (automatic) for (int i = 0; i < 3; i++) game.SetDraft(i, true);
        else if (stationaryAutomatic) { game.SetDraft(1,true); game.SetDraft(2,true); }
        else if(manualPursuit)game.SetDraft(1,true);
        game.StartFight();
        yield return Until(() => game.Phase == DuelPhase.Fight, DuelGame.PreparationSeconds + 1.2f);
        Check("wave " + wave + " created", game.Phase == DuelPhase.Fight && game.Bosses.Count == (wave == 3 ? 2 : 1));
    }
    static IEnumerator PursuitMechanics()
    {
        Record("CONTROLLED pursuit mechanisms: initial positions only, real physics/tells/cooldowns. No balance proof.");
        yield return Fresh(1);StopPolicies();var nav=game.Bosses[0].Navigation;
        OnFloor(game.Player.gameObject,nav,-4);OnFloor(game.Bosses[0].gameObject,nav,8);yield return Seconds(.2f);
        var dodge=game.Dodge;var body=game.Player.GetComponent<Rigidbody2D>();float x=body.position.x;
        bool accepted=dodge.TryDodge(1);float start=dodge.LastDodgeStartedAt;
        Check("Dodge approved values",Mathf.Abs(dodge.Distance-3)<.001f&&Mathf.Abs(dodge.Duration-.18f)<.001f
            &&Mathf.Abs(dodge.InvulnerabilityDuration-.2f)<.001f&&Mathf.Abs(dodge.DodgeStartInterval-2f)<.001f);
        yield return Seconds(.1f);int hp=game.Player.CurrentHealth;game.Player.TakeDamage(1,game.Bosses[0].gameObject);
        Check("Dodge first .1s blocks actual damage",accepted&&game.Player.CurrentHealth==hp);
        yield return Until(()=>Time.time>=start+.24f,.3f);
        Check("Dodge movement unchanged and immunity expires",!dodge.IsDodging&&!game.Player.IsInvulnerable
            &&Mathf.Abs(body.position.x-x-3)<.18f,$"travel={body.position.x-x:F3} elapsed={Time.time-start:F3}");
        game.Player.TakeDamage(1,game.Bosses[0].gameObject);
        Check("Dodge .24s accepts actual damage",game.Player.CurrentHealth==hp-1);
        heldKeys=new[]{Key.Space};yield return Frames(2);heldKeys=Array.Empty<Key>();yield return Seconds(.12f);
        Check("airborne Dodge retains start cooldown",!game.Player.GetComponent<PlayerMovement>().IsGrounded&&!dodge.TryDodge(-1));
        yield return Until(()=>Time.time>=start+dodge.DodgeStartInterval-.17f,dodge.DodgeStartInterval);
        Check("landing does not refresh Dodge",dodge.CooldownRemaining>0&&!dodge.TryDodge(-1));
        yield return Until(()=>Time.time>=start+dodge.DodgeStartInterval+.02f,.4f);
        Check("Dodge ready after 2.0s",dodge.TryDodge(-1));yield return Seconds(.25f);

        yield return PursuitFlank();

        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];nav=sword.Navigation;
        var supports=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null).GroupBy(n=>n.Collider).Select(g=>g.First()).OrderBy(n=>n.Collider.bounds.center.x).ToArray();
        var low=supports[1];var high=supports[2];var size=(Vector2)game.Player.GetComponent<Collider2D>().bounds.size;
        Position(sword.gameObject,new Vector2(low.Collider.bounds.center.x,low.Position.y+sword.Motor.Size.y*.5f+.025f));
        Position(game.Player.gameObject,new Vector2(high.Collider.bounds.center.x,high.Position.y+size.y*.5f+.025f));yield return Seconds(.3f);
        bool legal=sword.Tactics.TryPlanMobility(BossAbility.LeapSlash,sword.Perception.Facts,out var leap);
        accepted=legal&&sword.Actions.TryStartMobility(leap);bool locked=false;float slash=-1;bool frozen=true;float end=Time.time+3.5f;
        while(sword.Actions.Busy&&Time.time<end)
        {
            if(sword.Actions.Ability==BossAbility.LeapSlash&&sword.Actions.Phase==BossActionPhase.Locked&&!locked){locked=true;slash=leap.SlashOffset;}
            if(locked&&sword.Actions.Ability==BossAbility.LeapSlash)frozen&=Mathf.Abs(slash-leap.SlashOffset)<.001f;
            yield return null;
        }
        Check("Leap intercept locks and physically lands",accepted&&locked&&frozen&&sword.Motor.Grounded&&!sword.Actions.Busy,
            $"legal={legal} destination={leap?.Destination} end={sword.Position}");
        Check("Leap has one paid follow-up",sword.Actions.Started[(int)BossAbility.LeapFollowup]==1
            &&sword.Actions.Started[(int)BossAbility.Rhythm]==0,$"followup={sword.Actions.Activated[(int)BossAbility.LeapFollowup]}");
        Record("NOT TESTED: physical controls, exhaustive routes, subjective pursuit feel, Windows Build.");
    }
    static IEnumerator PursuitFlank()
    {
        yield return Fresh(2,manualPursuit:true);StopPolicies();var sword=game.Bosses[0];var nav=sword.Navigation;
        OnFloor(sword.gameObject,nav,-3);OnFloor(game.Player.gameObject,nav,0);yield return Seconds(.3f);
        yield return Seconds(.5f);
        var facts=sword.Perception.Facts;
        bool legal=sword.Tactics.TryPlanFlank(facts,out var flank);
        Record($"FLANK facts position={facts.Position} velocity={facts.Velocity} aim={facts.Aim} visible={facts.Visible} confidence={facts.Confidence:F3} grounded={sword.Motor.Grounded}");
        bool accepted=legal&&sword.Actions.TryStartMobility(flank);int before=game.Player.CurrentHealth;
        float cutPrepare=-1,cutActive=-1,peak=sword.Position.y,startY=peak,lastX=sword.Position.x;
        bool airborne=false,frozen=true,noTravelDamage=true,finiteMotion=true,tell=false;
        float end=Time.time+3.5f;
        while(sword.Actions.Busy&&Time.time<end)
        {
            var a=sword.Actions;peak=Mathf.Max(peak,sword.Position.y);airborne|=!sword.Motor.Grounded;
            finiteMotion&=Mathf.Abs(sword.Position.x-lastX)<=sword.Profile.FlankMotion.Speed*Mathf.Max(Time.deltaTime,Time.fixedDeltaTime)*2+.1f;lastX=sword.Position.x;
            if(a.Ability==BossAbility.FlankLeap){frozen&=Vector2.Distance(a.JumpDestination,flank.Destination)<.001f;noTravelDamage&=game.Player.CurrentHealth==before;}
            if(a.Ability==BossAbility.FlankCut&&a.Phase==BossActionPhase.Prepare&&cutPrepare<0)cutPrepare=Time.time;
            if(a.Ability==BossAbility.FlankCut&&a.Phase==BossActionPhase.Active&&cutActive<0)cutActive=Time.time;
            tell|=a.Ability==BossAbility.FlankCut&&a.PublicThreats.Any(t=>t.Valid&&t.Shape==BossThreatShape.BladeArc);
            yield return null;
        }
        Check("Flank physically crosses and lands behind",accepted&&airborne&&sword.Motor.Grounded&&sword.Position.x>game.Player.transform.position.x
            &&peak-startY<=ArenaNavigation2D.ExecutableJumpHeight(sword.Motor.Profile)+.12f,$"legal={legal} from=-3 target={flank?.Destination} end={sword.Position} peak={peak-startY:F3}");
        Check("Flank freezes landing and has no travel damage",accepted&&frozen&&noTravelDamage&&finiteMotion);
        Check("Flank arrival exposes full independent tell",tell&&cutPrepare>=0&&cutActive-cutPrepare>=.34f,$"prepare={cutPrepare:F3} active={cutActive:F3}");
        Check("Flank one real cut then full recovery",!sword.Actions.Busy&&sword.Actions.Activated[(int)BossAbility.FlankCut]==1
            &&before-game.Player.CurrentHealth==sword.Profile.Rhythm.Damage,$"hp={before}->{game.Player.CurrentHealth} cut={sword.Actions.Activated[(int)BossAbility.FlankCut]}");
        Check("Flank consumes cooldown and restores normal motion",!sword.Actions.Ready(BossAbility.FlankLeap)
            &&sword.Motor.TraversalProfile==sword.Motor.Profile&&!sword.Actions.TryStart(BossAbility.FlankCut,sword.Position));

    }
    static IEnumerator DashMobility()
    {
        yield return Fresh(1);StopPolicies();var gun=game.Bosses[0];
        OnFloor(gun.gameObject,gun.Navigation,-5);OnFloor(game.Player.gameObject,gun.Navigation,0);yield return Seconds(.3f);
        float began=Time.time,locked=-1,active=-1;int ammo=gun.Actions.Ammo;
        bool accepted=gun.Actions.TryStart(BossAbility.Piercing,game.Player.transform.position);
        bool prepaid=false;float deadline=Time.time+2.5f;
        while(gun.Actions.Busy&&Time.time<deadline)
        {
            if(gun.Actions.Phase==BossActionPhase.Prepare||gun.Actions.Phase==BossActionPhase.Locked)prepaid|=gun.Actions.Ammo!=ammo;
            if(gun.Actions.Phase==BossActionPhase.Locked&&locked<0)locked=Time.time;
            if(gun.Actions.Phase==BossActionPhase.Active&&active<0)active=Time.time;
            yield return null;
        }
        Check("Piercing full longer preparation and lock",accepted&&locked-began>=.89f&&active-locked>=.23f&&active-began<1.3f,
            $"prepare={locked-began:F3} lock={active-locked:F3}");
        Check("Piercing pays three rounds on firing",!prepaid&&gun.Actions.Ammo==ammo-3&&gun.Actions.Activated[(int)BossAbility.Piercing]==1);
        yield return Fresh(2,manualPursuit:true);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-4);OnFloor(game.Player.gameObject,sword.Navigation,0);yield return Seconds(.4f);
        sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Busy,1);
        Check("Sword chooses legal closing dash before flank/leap",sword.Tactics.PursuitDashes==1
            &&sword.Actions.Ability==BossAbility.DrawDash&&sword.Actions.Started[(int)BossAbility.FlankLeap]==0);
        bool supported=true;float stepAt=-1,stepX=0,stepEnd=0,recovered=-1;bool outsideRecovery=false;
        deadline=Time.time+3;
        while(sword.Actions.Busy&&Time.time<deadline)
        {
            if(sword.Actions.RecoveryStepping)
            {
                if(stepAt<0){stepAt=Time.time;stepX=sword.Position.x;sword.Tactics.Enabled=false;}
                stepEnd=sword.Position.x;supported&=sword.Motor.Grounded;
                outsideRecovery|=sword.Actions.Phase!=BossActionPhase.Recovery;
            }
            yield return null;
        }
        recovered=Time.time;sword.Tactics.Enabled=false;
        Check("Sword recovery footwork physically moves on support",stepAt>=0&&supported&&!outsideRecovery
            &&Mathf.Abs(stepEnd-stepX)>.3f&&Mathf.Abs(stepEnd-stepX)<=sword.Navigation.PlatformWidth*.18f+.08f,
            $"steps={sword.Actions.RecoverySteps} distance={Mathf.Abs(stepEnd-stepX):F3}");
        Check("Footwork retains full follow-up recovery and stops",!sword.Actions.RecoveryStepping&&recovered-stepAt>=.62f,
            $"recovery={recovered-stepAt:F3}");
        Check("Dash retains cooldown",!sword.Actions.Ready(BossAbility.DrawDash));
        yield return Fresh(3,true);var duo=game.GetComponentInChildren<DuoCoordinator>();bool overlap=false;deadline=Time.time+10;
        while(game.Phase==DuelPhase.Fight&&Time.time<deadline)
        {
            if(duo.Phase==DuoPhase.Idle)overlap|=game.Bosses.Count(a=>a.Actions.HighThreat&&a.Actions.Busy&&!a.Actions.SafeRecovery)>1;
            yield return null;
        }
        PitchResult();Check("W3 ordinary shared threat budget",!overlap);
        foreach(var boss in game.Bosses)Record($"{boss.Profile.Kind}: pursuitDashes={boss.Tactics.PursuitDashes} recoverySteps={boss.Actions.RecoverySteps}");
        Record("Controlled initial W1/W2 placement proves mechanisms only. Human feel, physical controls and Windows Build NOT TESTED.");
    }
    static IEnumerator PursuitObservation()
    {
        Record("W2 controlled initial platform positions; virtual manual Legs repeatedly traverse platforms; normal Arms AI, Spine Player (no Dodge).");
        yield return Fresh(2,manualPursuit:true);var sword=game.Bosses[0];var nav=sword.Navigation;
        var supports=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null).GroupBy(n=>n.Collider).Select(g=>g.First()).OrderBy(n=>n.Collider.bounds.center.x).ToArray();
        var size=(Vector2)game.Player.GetComponent<Collider2D>().bounds.size;
        Position(game.Player.gameObject,new Vector2(supports[0].Collider.bounds.center.x,supports[0].Position.y+size.y*.5f+.025f));
        Position(sword.gameObject,new Vector2(supports[1].Collider.bounds.center.x,supports[1].Position.y+sword.Motor.Size.y*.5f+.025f));
        int destination=1,direction=1,jumps=0;bool flying=false;float nextJump=Time.time+.2f,end=Time.time+12;
        while(game.Phase==DuelPhase.Fight&&Time.time<end)
        {
            var move=game.Player.GetComponent<PlayerMovement>();float dx=supports[destination].Collider.bounds.center.x-game.Player.transform.position.x;
            if(!move.IsGrounded)flying=true;
            if(flying&&move.IsGrounded){flying=false;destination+=direction;if(destination>=supports.Length){direction=-1;destination=supports.Length-2;}else if(destination<0){direction=1;destination=1;}nextJump=Time.time+.12f;}
            var keys=new List<Key>();if(Mathf.Abs(dx)>.15f)keys.Add(dx>0?Key.D:Key.A);
            if(move.IsGrounded&&Time.time>=nextJump){keys.Add(Key.Space);nextJump=Time.time+.4f;jumps++;}
            heldKeys=keys.ToArray();yield return null;
        }
        heldKeys=Array.Empty<Key>();PitchResult();Record($"W2 traversal jumpRequests={jumps} interceptPlans={sword.Tactics.InterceptPlansUsed} hits={sword.Actions.ConnectedHits}");
        yield return Fresh(3,true);var duo=game.GetComponentInChildren<DuoCoordinator>();bool overlap=false;end=Time.time+12;
        while(game.Phase==DuelPhase.Fight&&Time.time<end)
        {
            if(duo.Phase==DuoPhase.Idle)overlap|=game.Bosses.Count(a=>a.Actions.HighThreat&&a.Actions.Busy&&!a.Actions.SafeRecovery)>1;
            yield return null;
        }
        PitchResult();Check("W3 keeps ordinary major-threat budget",!overlap);
        foreach(var ability in new[]{BossAbility.FlankLeap,BossAbility.FlankCut,BossAbility.LeapSlash,BossAbility.LeapFollowup})
            Record((game.Bosses.Any(a=>a.Actions.Activated[(int)ability]>0)?"OBSERVED ":"NOT OBSERVED ")+"W3 "+ability);
        Record("No outcome gate. W2 is a repeatable virtual-input fixture, not human skill/balance evidence. Build NOT RUN.");
    }
    static IEnumerator MusicLevels()
    {
        Record("Focused music gain check: normal preparation entry, streamed playback and source gain; no human-audition claim.");
        var music=game.GetComponentInChildren<WaveMusicPlayer>();
        if(music==null){Check("music player exists",false);yield break;}
        var source=music.GetComponent<AudioSource>();float original=music.Volume;
        restoration.Add(()=>music.SetVolume(original));
        float[] gains={1f,.524807f,.699842f};
        for(int wave=1;wave<=3;wave++)
        {
            game.ConfigurePlaytestWave(wave);game.StartFight();
            yield return Until(()=>source.isPlaying,1.5f);yield return Seconds(.45f);
            Check("W"+wave+" normalized streamed gain",source.isPlaying&&game.Phase==DuelPhase.Prepare
                &&music.CurrentWave==wave&&Mathf.Abs(music.CurrentTrackGain-gains[wave-1])<.00001f
                &&Mathf.Abs(source.volume-original*gains[wave-1])<.0001f,"sourceVolume="+source.volume);
            music.SetVolume(original*.5f);yield return Frames(2);
            Check("W"+wave+" master volume retains track trim",Mathf.Abs(source.volume-original*.5f*gains[wave-1])<.0001f);
            music.SetVolume(original);yield return Frames(2);
            checkingPause=true;game.SetPause(true);float position=source.time,level=source.volume;
            yield return RealSeconds(.15f);
            Check("W"+wave+" pause preserves normalized playback",Mathf.Abs(source.time-position)<.05f
                &&Mathf.Abs(source.volume-level)<.0001f);
            game.SetPause(false);checkingPause=false;yield return Seconds(.15f);
            Check("W"+wave+" resume preserves trim",source.isPlaying&&source.time>position
                &&Mathf.Abs(source.volume-original*gains[wave-1])<.0001f);
            game.MainMenu();yield return Frames(2);
            Check("W"+wave+" cleanup still clears music",source.clip==null&&music.CurrentWave==0&&source.volume==0);
        }
        Record("NOT TESTED: subjective listening, all device outputs, standalone build. Full-source LUFS verification is offline.");
    }

    static IEnumerator WaveMusic()
    {
        Record("Music lifecycle only: real streamed playback/sample time; end seeks and direct deaths are controlled flow fixtures.");
        var music=game.GetComponentInChildren<WaveMusicPlayer>();
        Check("one dedicated music player",music!=null&&game.GetComponentsInChildren<WaveMusicPlayer>().Length==1);
        if(music==null)yield break;
        var source=music.GetComponent<AudioSource>();
        string[] names={"open_warfare","boss_fight_2","achilles"};
        game.MainMenu();yield return Frames(2);
        Check("menu starts silent",music.CurrentWave==0&&source.clip==null&&!source.isPlaying);
        for(int wave=1;wave<=3;wave++)
        {
            game.ConfigurePlaytestWave(wave);
            if(wave==3)for(int i=0;i<3;i++)game.SetDraft(i,true);
            Check("W"+wave+" configuration silent",source.clip==null&&!source.isPlaying);
            game.StartFight();
            Check("W"+wave+" track selected at Start Fight",game.Phase==DuelPhase.Prepare&&music.CurrentWave==wave
                &&source.clip!=null&&source.clip.name==names[wave-1]);
            yield return Until(()=>source.isPlaying,1.5f);
            Check("W"+wave+" streams during three-second preparation",source.isPlaying&&game.Phase==DuelPhase.Prepare
                &&source.clip.loadType==AudioClipLoadType.Streaming&&source.loop&&!source.playOnAwake&&!source.ignoreListenerPause);
            yield return Seconds(.45f);
            Check("W"+wave+" fades to normalized music volume",Mathf.Abs(source.volume-.18f*music.CurrentTrackGain)<.002f);
            checkingPause=true;game.SetPause(true);float pausedTime=source.time,pausedVolume=source.volume;
            yield return RealSeconds(.25f);
            Check("W"+wave+" preparation pause freezes music",AudioListener.pause&&Mathf.Abs(source.time-pausedTime)<.05f
                &&Mathf.Abs(source.volume-pausedVolume)<.001f);
            game.SetPause(false);checkingPause=false;
            yield return Until(()=>game.Phase==DuelPhase.Fight,DuelGame.PreparationSeconds+1);
            float before=source.time;yield return Seconds(.25f);
            Check("W"+wave+" continues across preparation/combat",source.isPlaying&&source.time>before+.1f&&music.CurrentWave==wave);
            if(wave<3)
            {
                MouseAt(game.Bosses[0].Position,true);int shots=game.Rifle.ShotsFired;
                yield return Seconds(.55f);mouseHeld=false;
                Check("W"+wave+" real shots coexist with music",game.Rifle.ShotsFired>shots&&source.isPlaying
                    &&game.Player.GetComponentInParent<PresentationOneShot>().PlayedCount>0);
            }
            else
            {
                yield return Seconds(2);
                Check("W3 short all-AI combat coexists with music",game.Phase==DuelPhase.Fight&&game.Rifle.ShotsFired>0&&source.isPlaying,
                    "shots="+game.Rifle.ShotsFired+" seconds="+game.FightClock);
            }
            StopPolicies();
            checkingPause=true;game.SetPause(true);pausedTime=source.time;
            yield return RealSeconds(.25f);
            Check("W"+wave+" combat pause freezes music",Mathf.Abs(source.time-pausedTime)<.05f);
            game.SetPause(false);checkingPause=false;
            // Seeking only the music voice proves native looping without a full-length fight.
            source.time=source.clip.length-.15f;yield return Seconds(.5f);
            Check("W"+wave+" native loop restarts",source.isPlaying&&source.time<1.5f,"position="+source.time);
            if(wave==1)
            {
                game.Bosses[0].Health.TakeDamage(99999,game.Player.gameObject);
                yield return Until(()=>game.Phase==DuelPhase.Between,.5f);yield return Seconds(.45f);
                Check("victory fades music and preserves result sound",source.clip==null&&!source.isPlaying
                    &&game.GetComponent<SfxSet>().Requested(Sfx.Victory)>0);
                yield return Until(()=>game.Phase==DuelPhase.Configure,1);
                Check("next-wave configuration remains silent",game.Wave==2&&source.clip==null&&music.CurrentWave==0);
            }
            else if(wave==2)
            {
                game.Player.TakeDamage(99999,game.Bosses[0].gameObject);
                yield return Until(()=>game.Phase==DuelPhase.Failed,.5f);yield return Seconds(.45f);
                Check("defeat fades music and preserves result sound",source.clip==null&&!source.isPlaying
                    &&game.GetComponent<SfxSet>().Requested(Sfx.Defeat)>0);
                game.Reconfigure();game.StartFight();yield return Until(()=>source.isPlaying,1.5f);
                Check("retry starts same track from beginning",music.CurrentWave==2&&source.clip!=null&&source.clip.name==names[1]
                    &&source.time<1.5f&&game.Phase==DuelPhase.Prepare);
            }
            else
            {
                foreach(var boss in game.Bosses)boss.Health.TakeDamage(99999,game.Player.gameObject);
                yield return Until(()=>game.Phase==DuelPhase.Complete,.5f);yield return Seconds(.45f);
                Check("completion fades music",game.Phase==DuelPhase.Complete&&source.clip==null&&!source.isPlaying);
            }
            game.MainMenu();yield return Frames(2);
            Check("W"+wave+" menu clears dedicated voice",source.clip==null&&!source.isPlaying&&music.CurrentWave==0);
        }
        Check("only one music voice after retries",game.GetComponentsInChildren<WaveMusicPlayer>().Length==1);
        Record("NOT TESTED: human audition, seamless musical edits, hardware input, standalone build, balance.");
    }

    static IEnumerator PlayerGunPose()
    {
        Record("CONTROLLED gun pose: stopped Boss policies, real mouse aim/fire; chest origin shared by manual/AI/spawn.");
        yield return Fresh(1); StopPolicies(); yield return Seconds(.15f);
        var reader=game.Player.GetComponent<PlayerInputReader>();
        var visual=game.Player.GetComponent<OfficerVisual>();
        var gun=visual.Part(LoadoutSlot.Weapon).transform;
        var grip=gun.parent.Find("GunGrip");
        var forearm=game.Player.transform.Find("Stickman/RightForearm");
        int shots=0; bool aligned=true;
        Action<Vector2,Vector2> onShot=(origin,direction)=>{shots++;aligned&=Vector2.Distance(origin,game.Rifle.GetMuzzlePosition(direction))<.001f;};
        game.Rifle.ShotFired+=onShot;
        foreach(var offset in new[]{new Vector2(6,0),new Vector2(-6,0),new Vector2(4,4),new Vector2(-4,-3)})
        {
            Vector2 target=game.Rifle.AimOrigin+offset;
            MouseAt(target,false); yield return Seconds(.12f);
            Vector2 muzzle=game.Rifle.GetMuzzlePosition(reader.AimDirection);
            Check("barrel follows raised real muzzle "+offset,Vector2.Distance(gun.TransformPoint(new Vector3(.5f,0,0)),muzzle)<.005f);
            Check("manual ray reaches cursor "+offset,Vector2.Dot((target-muzzle).normalized,reader.AimDirection)>.9999f);
            Check("trigger hand meets grip "+offset,Vector2.Distance(forearm.TransformPoint(new Vector3(.5f,0,0)),grip.position)<.065f);
            if(offset.y==0)
            {
                Check("gun held at chest height "+offset,Mathf.Abs(gun.position.y-game.Player.transform.position.y-.28f)<.005f);
                Directory.CreateDirectory(Output);ScreenCapture.CaptureScreenshot(Output+(offset.x>0?"player-gun-right.png":"player-gun-left.png"));yield return Frames(2);
            }
            mouseHeld=true;yield return Seconds(.28f);mouseHeld=false;
        }
        game.Rifle.ShotFired-=onShot;
        Check("real shots spawn at raised muzzle",shots>=4&&aligned);
        MouseAt(game.Rifle.AimOrigin+Vector2.right*6,true);yield return Until(()=>game.Rifle.IsReloading,3);mouseHeld=false;
        yield return Seconds(.4f);Check("reload still expressed",game.Rifle.IsReloading&&game.Rifle.ReloadVisible);
        yield return Until(()=>!game.Rifle.IsReloading,2);yield return Frames(2);
        Check("reload restores barrel and hands",Quaternion.Angle(gun.localRotation,Quaternion.identity)<.01f&&Vector2.Distance(gun.TransformPoint(new Vector3(.5f,0,0)),game.Rifle.GetMuzzlePosition(reader.AimDirection))<.005f);
        yield return Fresh(1,true,true);StopPolicies();int hp=game.Bosses[0].Health.CurrentHealth;yield return Seconds(1.2f);
        Check("AI aim hits using chest origin",game.Rifle.ShotsFired>0&&game.Bosses[0].Health.CurrentHealth<hp);
        yield return Fresh(3,true);yield return Seconds(4);Check("short W3 firing",game.Rifle.ShotsFired>0);PitchResult();
        Record("NOT TESTED: hardware input, exhaustive angles/poses, balance, Windows Build/launch.");
    }

    static IEnumerator ReleaseSmoke()
    {
        game.NewGame();
        Check("New Game defaults to manual configuration",game.Phase==DuelPhase.Configure
            &&!game.DraftLegs&&!game.DraftArms&&!game.DraftSpine);
        for(int i=0;i<3;i++)game.SetDraft(i,true);
        for(int wave=1;wave<=3;wave++)
        {
            Check("normal configuration W"+wave,game.Wave==wave&&game.Phase==DuelPhase.Configure);
            var opening=System.Diagnostics.Stopwatch.StartNew();game.StartFight();opening.Stop();
            Record($"PERF W{wave} synchronous StartFight={opening.Elapsed.TotalMilliseconds:F2}ms");
            var start=game.Player.transform.position;
            if(wave<3)Check("solo mirror W"+wave,Mathf.Abs(start.x+game.Bosses[0].Position.x
                -2*game.Bosses[0].Navigation.ArenaBounds.center.x)<.001f,
                $"player={start} boss={game.Bosses[0].Position}");
            else Check("duo authored opening retained",Mathf.Abs(game.Bosses[0].Position.x-9)<.001f
                &&Mathf.Abs(game.Bosses[1].Position.x-3)<.001f);
            Check("preparation freezes actors",game.Phase==DuelPhase.Prepare
                &&!game.Player.GetComponent<Rigidbody2D>().simulated&&game.Bosses.All(a=>!a.Running));
            float prepAt=Time.time;
            yield return Until(()=>game.Phase==DuelPhase.Fight,DuelGame.PreparationSeconds+1.2f);
            Check("full preparation enters fight W"+wave,game.Phase==DuelPhase.Fight
                &&Time.time-prepAt>=DuelGame.PreparationSeconds-.025f);
            if(wave==1)
            {
                checkingPause=true;game.SetPause(true);float clock=game.FightClock;
                yield return RealSeconds(.15f);
                Check("fight pause freezes time and audio",game.Paused&&Time.timeScale==0
                    &&game.FightClock==clock&&AudioListener.pause);
                game.SetPause(false);checkingPause=false;
            }
            var frames=new List<float>(1500);int peakProjectiles=0;
            float until=Time.time+6;
            while(game.Phase==DuelPhase.Fight&&Time.time<until)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime*1000);
                peakProjectiles=Mathf.Max(peakProjectiles,Projectile.Active.Count);
            }
            frames.Sort();
            if(frames.Count>0)Record($"PERF W{wave} Editor frame samples={frames.Count} median={frames[frames.Count/2]:F2}ms "
                +$"p95={frames[Mathf.Min(frames.Count-1,(int)(frames.Count*.95f))]:F2}ms max={frames[frames.Count-1]:F2}ms "
                +$"over100ms={frames.Count(t=>t>100)} peakProjectiles={peakProjectiles}");
            Check("normal combat advances and fires W"+wave,game.Phase==DuelPhase.Fight&&game.Rifle.ShotsFired>0);
            PitchResult();
            Record("CONTROLLED deaths now validate flow only, not balance or winnability.");
            StopPolicies();game.Bosses[0].Health.TakeDamage(99999,game.Player.gameObject);
            if(wave==3)
            {
                yield return Frames(2);
                Check("one Boss death cannot complete duo",game.Phase==DuelPhase.Fight&&game.Bosses[1].IsAlive);
                game.Bosses[1].Health.TakeDamage(99999,game.Player.gameObject);
                yield return Frames(2);Check("both Boss deaths complete game",game.Phase==DuelPhase.Complete);
            }
            else
            {
                yield return Until(()=>game.Phase==DuelPhase.Configure,1.5f);
                Check("next wave keeps permissions",game.Phase==DuelPhase.Configure&&game.Wave==wave+1
                    &&game.DraftLegs&&game.DraftArms&&game.DraftSpine);
            }
        }
        yield return Fresh(2);StopPolicies();
        var previous=game.Player;game.Player.TakeDamage(99999,null);yield return Frames(2);
        Check("death reaches defeat",game.Phase==DuelPhase.Failed);
        game.Reconfigure();game.StartFight();
        Check("retry creates fresh full resources",game.Wave==2&&game.Player!=previous
            &&game.Player.CurrentHealth==game.Player.MaxHealth&&game.Bosses[0].Health.CurrentHealth==game.Bosses[0].Health.MaxHealth);
        Check("retry keeps solo mirror",Mathf.Abs(game.Player.transform.position.x+game.Bosses[0].Position.x)<.001f);
        yield return Until(()=>game.Phase==DuelPhase.Fight,DuelGame.PreparationSeconds+1.2f);StopPolicies();
        game.Bosses[0].Health.TakeDamage(99999,game.Player.gameObject);game.Player.TakeDamage(99999,null);
        yield return Frames(2);Check("simultaneous death is defeat",game.Phase==DuelPhase.Failed);
        game.MainMenu();yield return Frames(2);
        Check("menu cleanup releases actors and projectiles",game.Phase==DuelPhase.Menu&&Projectile.Active.Count==0
            &&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0&&Time.timeScale==1&&!AudioListener.pause);
        Record("Editor timing includes Editor overhead; long-run memory, hardware input and Mac runtime NOT TESTED.");
    }
    static IEnumerator PlayerOpening()
    {
        foreach(int wave in new[]{1,2,3})
        {
            RestoreFixture(); heldKeys=Array.Empty<Key>(); mouseHeld=false;
            game.ConfigurePlaytestWave(wave); game.StartFight();
            var player=game.Player; var movement=player.GetComponent<PlayerMovement>();
            var body=player.GetComponent<Rigidbody2D>(); var shape=player.GetComponent<Collider2D>();
            var nav=game.GetComponentInChildren<ArenaNavigation2D>();
            bool supported=nav.TrySupport(shape.bounds.center,shape.bounds.size,out var support);
            float floor=supported?support.bounds.max.y:float.NaN;
            float startY=body.position.y;
            float preparationAt=Time.time;
            var calf=player.transform.Find("Stickman/RightCalf").GetComponent<SpriteRenderer>();
            Check("W"+wave+" preparation physically supported",supported&&!body.simulated
                &&Mathf.Abs(shape.bounds.min.y-floor-.015f)<.002f,
                $"y={startY:F5} colliderGap={shape.bounds.min.y-floor:F5} calfGap={calf.bounds.min.y-floor:F5}");
            Check("W"+wave+" opening feet reach ground",supported&&Mathf.Abs(calf.bounds.min.y-floor)<.015f);
            if(wave==1)
            {
                checkingPause=true; game.SetPause(true); yield return RealSeconds(.12f);
                Check("preparation pause preserves support",game.Phase==DuelPhase.Prepare&&body.position.y==startY&&!body.simulated);
                game.SetPause(false); checkingPause=false;
            }
            yield return Seconds(1.1f);
            Check("W"+wave+" three-second preparation stays stationary",game.Phase==DuelPhase.Prepare
                &&!body.simulated&&!player.GetComponent<PlayerInputReader>().GameplayInputEnabled&&body.position.y==startY
                &&game.Bosses.All(a=>!a.Running));
            yield return Until(()=>game.Phase==DuelPhase.Fight,DuelGame.PreparationSeconds+1.2f);
            Check("W"+wave+" waits full three seconds",Time.time-preparationAt>=DuelGame.PreparationSeconds-.025f
                ,$"duration={Time.time-preparationAt:F3}");
            StopPolicies(); yield return Seconds(.25f);
            Check("W"+wave+" opens without falling or false landing",movement.IsGrounded
                &&Mathf.Abs(body.position.y-startY)<.003f&&Mathf.Abs(body.linearVelocity.y)<.01f,
                $"drift={body.position.y-startY:F6}");
            if(wave==1)
            {
                heldKeys=new[]{Key.Space}; yield return Until(()=>body.linearVelocity.y>1,.3f);
                heldKeys=Array.Empty<Key>(); yield return Seconds(.18f);
                Check("real Jump leaves opening support",!movement.IsGrounded&&body.position.y>startY+1);
                yield return Until(()=>movement.IsGrounded,2); yield return Seconds(.25f);
                Check("real landing returns to aligned support",movement.IsGrounded&&Mathf.Abs(body.position.y-startY)<.003f);
            }
        }
        yield return Fresh(3,true); yield return Seconds(5);
        Check("short normal W3 advances with real shots",game.Phase==DuelPhase.Fight&&game.Rifle.ShotsFired>0); PitchResult();
        game.MainMenu(); yield return Frames(2);
        Check("opening smoke cleanup",Projectile.Active.Count==0&&Object.FindObjectsByType<BossAgent>(FindObjectsSortMode.None).Length==0);
        Record("NOT TESTED: physical input, subjective smoothness on other hardware, full lifecycle regression, Windows Build/launch.");
    }
    static IEnumerator PlayerAnimation()
    {
        Record("CONTROLLED animation/input: stopped Boss policies, real virtual input and physics; no injected player motion or outcomes.");
        yield return Fresh(1); StopPolicies(); yield return Seconds(.15f);
        var visual = game.Player.GetComponent<OfficerVisual>();
        var movement = game.Player.GetComponent<PlayerMovement>();
        var body = game.Player.GetComponent<Rigidbody2D>();
        var reader = game.Player.GetComponent<PlayerInputReader>();
        var head = visual.Part(LoadoutSlot.Brain).transform;
        var gun = visual.Part(LoadoutSlot.Weapon).transform;
        var calf = visual.transform.Find("Stickman/RightCalf");
        Check("four runtime limb segments", new[]{"LeftForearm","RightForearm","LeftCalf","RightCalf"}.All(n=>visual.transform.Find("Stickman/"+n)!=null));
        Vector3 colliderSize = game.Player.GetComponent<Collider2D>().bounds.size;
        Vector3 idleHead = head.localPosition; yield return Seconds(.25f);
        Check("idle breath with grounded support", movement.IsGrounded && Vector3.Distance(idleHead,head.localPosition)>.0001f);
        float startX = body.position.x; Quaternion leg = calf.localRotation;
        MouseAt(body.position+new Vector2(8,2),true); heldKeys=new[]{Key.A};
        yield return Seconds(.18f);
        Check("backward move plus independent paid shot", body.position.x<startX-.4f && reader.AimDirection.x>0 && game.Rifle.ShotsFired>0);
        Check("walking limbs change pose", Quaternion.Angle(leg,calf.localRotation)>1);
        Check("barrel meets real muzzle", Vector2.Distance(gun.TransformPoint(new Vector3(.5f,0,0)),game.Rifle.GetMuzzlePosition(reader.AimDirection))<.005f);
        Directory.CreateDirectory(Output); ScreenCapture.CaptureScreenshot(Output+"player-run.png"); yield return Frames(3);
        heldKeys=Array.Empty<Key>(); mouseHeld=false; yield return Seconds(.18f);
        yield return CheckInputs();
        yield return Until(()=>movement.IsGrounded,2); yield return Until(()=>game.Dodge.CanDodge,2);
        heldKeys=new[]{Key.D,Key.LeftShift}; yield return Until(()=>game.Dodge.IsDodging,.3f); yield return Frames(2);
        Check("ground dodge lowers head", game.Dodge.IsDodging && head.localPosition.y<.48f);
        ScreenCapture.CaptureScreenshot(Output+"player-dodge.png"); yield return Frames(2);
        heldKeys=Array.Empty<Key>(); yield return Until(()=>game.Dodge.CanDodge,2);
        heldKeys=new[]{Key.Space}; yield return Seconds(.14f);
        Check("jump folds legs", !movement.IsGrounded && calf.localPosition.y>-.65f);
        ScreenCapture.CaptureScreenshot(Output+"player-jump.png"); yield return Frames(2);
        heldKeys=new[]{Key.LeftShift}; yield return Until(()=>game.Dodge.IsDodging,.3f); yield return Frames(2);
        Check("air dodge uses low pose", !movement.IsGrounded && game.Dodge.IsDodging && head.localPosition.y<.48f);
        heldKeys=Array.Empty<Key>(); yield return Until(()=>movement.IsGrounded,2); yield return Frames(2);
        Check("real landing compresses pose", movement.IsGrounded && head.localPosition.y<.53f);
        yield return Seconds(.25f);
        Check("landing recovers without collider change", head.localPosition.y>.54f && Vector3.Distance(colliderSize,game.Player.GetComponent<Collider2D>().bounds.size)<.001f);
        MouseAt(body.position+new Vector2(-8,3),true); yield return Until(()=>game.Rifle.IsReloading,3);
        mouseHeld=false; yield return Seconds(.45f);
        Check("real auto reload folds gun", game.Rifle.IsReloading && game.Rifle.ReloadVisible && Quaternion.Angle(gun.localRotation,Quaternion.identity)>15);
        ScreenCapture.CaptureScreenshot(Output+"player-reload.png"); yield return Frames(2);
        checkingPause=true; game.SetPause(true); Vector3 pausedHead=head.position; Quaternion pausedGun=gun.localRotation;
        float reloadRemaining=game.Rifle.ReloadRemaining;
        yield return RealSeconds(.2f);
        Check("pause freezes pose and reload", head.position==pausedHead && gun.localRotation==pausedGun && Mathf.Approximately(reloadRemaining,game.Rifle.ReloadRemaining));
        game.SetPause(false); checkingPause=false; yield return Until(()=>!game.Rifle.IsReloading,2); yield return Frames(2);
        Check("reload restores real aim and eight rounds", game.Rifle.AmmoInMagazine==8 && Quaternion.Angle(gun.localRotation,Quaternion.identity)<.01f
            && Vector2.Distance(gun.TransformPoint(new Vector3(.5f,0,0)),game.Rifle.GetMuzzlePosition(reader.AimDirection))<.005f);
        game.NewGame(); game.StartFight(); yield return Frames(2);
        Check("fresh preparation supported and reset", game.Phase==DuelPhase.Prepare && game.Player.GetComponent<OfficerVisual>().Part(LoadoutSlot.Brain).transform.localPosition.y>.54f
            && game.Player.transform.Find("Stickman").childCount==10);
        yield return Fresh(2); yield return Seconds(2);
        Check("short W2 entry", game.Phase==DuelPhase.Fight);
        yield return Fresh(3,true); yield return Seconds(6);
        Check("short W3 all AI fires", game.Rifle.ShotsFired>0); PitchResult();
        Record("NOT TESTED: hardware input, subjective animation quality across resolutions, full lifecycle regression, Windows Build/launch.");
    }
    static IEnumerator DebugComboKeys()
    {
        yield return Fresh(3,true);var duo=game.GetComponentInChildren<DuoCoordinator>();
        for(int i=0;i<2;i++)
        {
            int before=duo.DebugRequests;heldKeys=new[]{Key.P};yield return Frames(3);heldKeys=Array.Empty<Key>();yield return Frames(3);
            Check("P requests real "+(i==0?"Cross Execution":"Sweep & Drop"),duo.DebugRequests==before+1
                &&duo.LastDebugCombination==(i==0?DuoCombination.CrossExecution:DuoCombination.SweepAndDrop)
                &&!string.IsNullOrEmpty(duo.LastDebugResult),duo.LastDebugResult);
            yield return Seconds(.3f);
        }
        yield return Seconds(2.5f);
        Check("debug request returns to ordinary combat or advances a real combo",game.FightClock>2&&!game.Paused);
        Record("NOT TESTED: physical P key, natural combo completion, full regression, balance/outcome proof, Windows build.");
    }
    static IEnumerator SoloPressureMechanics()
    {
        Record("CONTROLLED initial placement/stopped policies; real executors, projectiles and gravity. No outcome evidence.");
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,-1);OnFloor(game.Player.gameObject,sword.Navigation,.5f);
        yield return Seconds(.3f);
        bool planned=sword.Tactics.TryPlanDrop(sword.Perception.Facts,out var drop);
        bool accepted=planned&&sword.Tactics.StartDropPlan(drop,false);
        if(accepted)sword.Tactics.Enabled=true;
        yield return Until(()=>sword.Actions.Busy||!sword.Tactics.DropPlanPending,2);
        sword.Tactics.Enabled=false;
        Vector2 initial=drop!=null?drop.TargetHypothesis:Vector2.zero,locked=Vector2.zero;
        float playerStartX=game.Player.GetComponent<Rigidbody2D>().position.x;
        Vector2 landed=Vector2.zero;bool landedObserved=false;
        bool sawLock=false,frozen=true,sawActive=false,sawFollow=false;float lockedAt=0,activeAt=0,followAt=0,followActive=0;
        float end=Time.time+3;
        var previousPhase=sword.Actions.Phase;var previousAbility=sword.Actions.Ability;
        while((sword.Actions.Busy||sword.Tactics.DropPlanPending)&&Time.time<end)
        {
            var a=sword.Actions;
            if(sawActive&&!landedObserved&&sword.Motor.Grounded){landed=sword.Position;landedObserved=true;}
            if(a.Phase!=previousPhase||a.Ability!=previousAbility)
            {ActionProbe("focused drop transition",sword);previousPhase=a.Phase;previousAbility=a.Ability;}
            if(a.Ability==BossAbility.DropCut)
            {
                if(a.Phase==BossActionPhase.Locked&&!sawLock){sawLock=true;locked=a.DropTarget;lockedAt=Time.time;}
                if(sawLock)frozen&=Vector2.Distance(locked,a.DropTarget)<.001f;
                if(a.Phase==BossActionPhase.Active&&!sawActive){sawActive=true;activeAt=Time.time;}
            }
            if(a.Ability==BossAbility.DropFollowup)
            {
                if(!sawFollow){sawFollow=true;followAt=Time.time;}
                if(a.Phase==BossActionPhase.Active&&followActive==0)followActive=Time.time;
            }
            yield return null;
        }
        Check("planned Drop physically jumps, locks and lands",accepted&&sawActive&&sword.Motor.Grounded&&!sword.Actions.Busy,
            $"planned={planned} initial={initial} locked={locked} end={sword.Position} active={sawActive}");
        Check("Drop correction retains full fixed lock",sawLock&&frozen&&activeAt-lockedAt>=sword.Profile.DropCut.Lock-.04f,
            $"corrections={sword.Actions.DropCorrections} lock={activeAt-lockedAt:F3} frozen={frozen}");
        Check("Drop pushes player once while preserving sword landing",landedObserved&&Mathf.Abs(landed.x-locked.x)<.22f
            &&game.Player.GetComponent<PlayerMovement>().HitPushCount==1
            &&Mathf.Abs(game.Player.GetComponent<Rigidbody2D>().position.x-playerStartX)>.6f,
            $"landing={landed} locked={locked} playerDx={game.Player.GetComponent<Rigidbody2D>().position.x-playerStartX:F3} pushes={game.Player.GetComponent<PlayerMovement>().HitPushCount}");
        Check("landing front cut occurs once with its own tell",sawFollow&&sword.Actions.Activated[(int)BossAbility.DropFollowup]==1
            &&followActive-followAt>=sword.Profile.RhythmPrepare[0]+sword.Profile.RhythmLock[0]-.04f,
            $"started={sword.Actions.Started[(int)BossAbility.DropFollowup]} active={sword.Actions.Activated[(int)BossAbility.DropFollowup]} tell={followActive-followAt:F3}");
        Check("no direct standalone Drop follow-up entry",!sword.Actions.TryStart(BossAbility.DropFollowup,sword.Perception.Facts.Position));
        if(mode=="solo-drop-mechanics")yield break;

        yield return Fresh(1);StopPolicies();var gun=game.Bosses[0];
        OnFloor(gun.gameObject,gun.Navigation,-7);OnFloor(game.Player.gameObject,gun.Navigation,1);
        yield return Seconds(.3f);
        for(int burst=0;burst<2;burst++)
        {
            yield return Until(()=>gun.Actions.Ready(BossAbility.Burst),1.2f);
            int ammo=gun.Actions.Ammo;float fromX=gun.Position.x;
            accepted=gun.Actions.TryStart(BossAbility.Burst,gun.Perception.Facts.Position);
            var shots=new HashSet<Projectile>();var times=new List<float>();bool bullets=true;end=Time.time+2;
            while(gun.Actions.Busy&&Time.time<end)
            {
                foreach(var shot in Projectile.Active)if(shot!=null&&shot.Owner==gun.gameObject&&shots.Add(shot))
                {times.Add(Time.time);bullets&=shot.Damage==gun.Profile.Burst.Damage&&Mathf.Abs(shot.GetComponent<Rigidbody2D>().linearVelocity.magnitude-gun.Profile.BulletSpeed)<.05f;}
                yield return null;
            }
            Check("Burst "+burst+" uses three normal paid bullets",accepted&&times.Count==3&&bullets&&ammo-gun.Actions.Ammo==3,
                $"times={string.Join(",",times)} ammo={ammo}->{gun.Actions.Ammo} stagger={gun.Actions.StaggeredBurst}");
            if(burst==1)Check("staggered first gap exceeds ordinary gap",times.Count==3&&times[1]-times[0]>=gun.Profile.BurstStaggerPause-.04f
                &&times[2]-times[1]<times[1]-times[0]-.05f);
            Check("Burst physically strafes on safe support",gun.Motor.Grounded&&Mathf.Abs(gun.Position.x-fromX)>.15f,
                $"x={fromX:F2}->{gun.Position.x:F2}");
        }
        heldKeys=new[]{Key.D};yield return Seconds(.2f);
        var facts=gun.Perception.Facts;
        bool early=gun.Tactics.TryGunTarget(BossAbility.Piercing,gun.Position,facts,out var earlyPoint,.96f);
        bool late=gun.Tactics.TryGunTarget(BossAbility.Piercing,gun.Position,facts,out var latePoint,.24f);
        Check("beam prediction uses remaining tell",early&&late&&earlyPoint.x>latePoint.x+.3f,
            $"velocity={facts.Velocity} early={earlyPoint} late={latePoint}");
        heldKeys=Array.Empty<Key>();yield return Seconds(.25f);
        accepted=gun.Actions.TryStart(BossAbility.Piercing,gun.Perception.Facts.Position);
        float prepareAt=Time.time;Vector2 lockedAim=Vector2.zero;bool beamLock=false,stable=true;
        end=Time.time+2;
        while(gun.Actions.Busy&&Time.time<end)
        {
            if(gun.Actions.Phase==BossActionPhase.Locked)
            {if(!beamLock){beamLock=true;lockedAim=gun.Actions.Aim;}else stable&=Vector2.Distance(lockedAim,gun.Actions.Aim)<.001f;}
            yield return null;
        }
        Check("Piercing preserves complete tell and fixed aim",accepted&&beamLock&&stable
            &&gun.Actions.LastActiveAt-prepareAt>=gun.Profile.Piercing.Prepare+gun.Profile.Piercing.Lock-.04f);
        Record("NOT TESTED here: cooperative Drop branch exclusion is source checked; natural pressure is a separate bounded sample.");
    }
    static IEnumerator SoloPressureEdges()
    {
        Record("CONTROLLED narrow late-correction and push-motor fixtures; no balance/outcome proof.");
        yield return Fresh(2);StopPolicies();var sword=game.Bosses[0];
        OnFloor(sword.gameObject,sword.Navigation,0);OnFloor(game.Player.gameObject,sword.Navigation,1.6f);
        yield return Seconds(.3f);Vector2 original=sword.Position;
        bool jumped=sword.Motor.TryJump();yield return Seconds(.18f);
        bool accepted=sword.Actions.TryStart(BossAbility.DropCut,original);
        yield return Until(()=>sword.Actions.Phase==BossActionPhase.Locked||!sword.Actions.Busy,.5f);
        Vector2 target=sword.Actions.DropTarget;bool frozen=true;heldKeys=new[]{Key.D};
        float end=Time.time+2;
        while(sword.Actions.Busy&&Time.time<end)
        {if(sword.Actions.Ability==BossAbility.DropCut)frozen&=Vector2.Distance(target,sword.Actions.DropTarget)<.001f;yield return null;}
        heldKeys=Array.Empty<Key>();
        Check("late public Drop correction is bounded and then frozen",jumped&&accepted&&sword.Actions.DropCorrections==1
            &&Vector2.Distance(target,original)>.1f&&Mathf.Abs(target.x-original.x)<=sword.Navigation.PlatformWidth*.5f&&frozen,
            $"original={original} corrected={target} count={sword.Actions.DropCorrections} frozen={frozen}");

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];
        var movement=game.Player.GetComponent<PlayerMovement>();var body=game.Player.GetComponent<Rigidbody2D>();
        var shape=game.Player.GetComponent<Collider2D>();var bounds=sword.Navigation.Bounds;
        OnFloor(sword.gameObject,sword.Navigation,-6);OnFloor(game.Player.gameObject,sword.Navigation,bounds.max.x-shape.bounds.extents.x-.15f);
        yield return Seconds(.2f);
        bool pushed=movement.ApplyHitPush(1,1.4f,.14f);yield return Seconds(.3f);
        Check("hit push stops at wall and expires",pushed&&!movement.HitPushActive&&shape.bounds.max.x<=bounds.max.x+.03f,
            $"edge={shape.bounds.max.x:F3} wall={bounds.max.x:F3}");
        bool dodged=game.Dodge.TryDodge(-1);bool rejected=!movement.ApplyHitPush(1,1.4f,.14f);
        yield return Seconds(.3f);
        Check("accepted Dodge rejects hit push",dodged&&rejected&&!movement.HitPushActive);
        OnFloor(game.Player.gameObject,sword.Navigation,0);yield return Seconds(.1f);
        pushed=movement.ApplyHitPush(1,1.4f,.14f);yield return Frames(1);
        checkingPause=true;game.SetPause(true);Vector2 paused=body.position;
        yield return RealSeconds(.1f);
        Check("pause freezes hit push",pushed&&Vector2.Distance(paused,body.position)<.001f&&movement.HitPushActive);
        game.SetPause(false);checkingPause=false;yield return Seconds(.3f);
        Check("hit push resumes and ends",!movement.HitPushActive&&body.position.x>paused.x);
        yield return Fresh(2);Check("fresh player clears hit push",!game.Player.GetComponent<PlayerMovement>().HitPushActive
            &&game.Player.GetComponent<PlayerMovement>().HitPushCount==0);
    }
    static IEnumerator SoloPressureObservation()
    {
        bool final=mode=="solo-pressure-final";
        Record(final?"NORMAL final-source W2/W3 12s each after requested hit-push correction. Prior W1 remains unchanged.":"NORMAL all-AI W1 12s / W2 20s / W3 18s, once each. No outcome gate.");
        for(int wave=final?2:1;wave<=3;wave++)
        {
            yield return Fresh(wave,true);var duo=game.GetComponentInChildren<DuoCoordinator>();bool overlap=false;
            float duration=final?12:wave==1?12:wave==2?20:18;
            while(game.Phase==DuelPhase.Fight&&game.FightClock<duration)
            {
                if(duo.Phase==DuoPhase.Idle)overlap|=game.Bosses.Count(a=>a.Actions.HighThreat&&a.Actions.Phase==BossActionPhase.Active)>1;
                yield return null;
            }
            PitchResult();Check("W"+wave+" ordinary major budget",!overlap);
            foreach(var actor in game.Bosses)Record($"PRESSURE W{wave} actor={actor.ActorId} recoveryPlans={actor.Tactics.RecoveryPlansUsed} gunMoves={actor.Tactics.GunRepositions} dropCorrections={actor.Actions.DropCorrections}");
        }
        Record("NOT TESTED: physical input, long-run balance, exhaustive navigation, full lifecycle, Windows build.");
    }
    static IEnumerator PressureMechanics()
    {
        Record("CONTROLLED geometry/action mechanisms: initial fixture placement only; no balance/outcome evidence.");
        yield return Fresh(2);StopPolicies();
        var sword=game.Bosses[0];var nav=sword.Navigation;
        var supports=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null)
            .GroupBy(n=>n.Collider).Select(g=>g.First()).OrderBy(n=>n.Collider.bounds.center.x).ToArray();
        var first=supports[0];var next=supports.First(n=>n.Collider!=first.Collider&&Mathf.Abs(n.Position.y-first.Position.y)<.05f);
        Vector2 from=new Vector2(first.Collider.bounds.center.x,first.Position.y+sword.Motor.Size.y*.5f+.025f);
        Vector2 to=new Vector2(next.Collider.bounds.center.x,next.Position.y+sword.Motor.Size.y*.5f+.025f);
        Position(sword.gameObject,from);OnFloor(game.Player.gameObject,nav,nav.Bounds.max.x-2);
        yield return Seconds(.25f);from=sword.Position;
        bool planned=nav.TryHorizontalAttack(from,to,sword.Motor.Size,out var end);
        Check("king metrics read actual supports",Mathf.Abs(nav.SameLevelSpacing-Mathf.Abs(to.x-from.x))<.1f,
            $"platformWidth={nav.PlatformWidth:F3} spacing={nav.SameLevelSpacing:F3} jumpHeight={ArenaNavigation2D.ExecutableJumpHeight(sword.Motor.Profile):F3}");
        if(planned)sword.Motor.StartDash(Vector2.right,Vector2.Distance(from,end),sword.Profile.DashSpeed);
        yield return Until(()=>!sword.Motor.DashActive,1);yield return Seconds(.15f);
        Check("same-height full dash physically reaches supported platform",planned&&sword.Motor.Grounded
            &&Vector2.Distance(sword.Position,to)<.2f,$"from={from} wanted={to} end={sword.Position} blocked={sword.Motor.DashBlocked}");

        yield return Fresh(2);StopPolicies();sword=game.Bosses[0];nav=sword.Navigation;
        OnFloor(sword.gameObject,nav,-nav.PlatformWidth*.75f);OnFloor(game.Player.gameObject,nav,nav.PlatformWidth*.25f);
        yield return Seconds(.3f);
        bool accepted=sword.Actions.TryStart(BossAbility.DrawDash,sword.Perception.Facts.Position);
        yield return Until(()=>!sword.Actions.Busy,3);
        Check("Draw Dash has one front follow-up and exits",accepted&&!sword.Actions.Busy
            &&sword.Actions.Activated[(int)BossAbility.DashFollowup]==1&&sword.Actions.Started[(int)BossAbility.ReturnCut]==0,
            $"started={string.Join(",",sword.Actions.Started)} active={string.Join(",",sword.Actions.Activated)}");

        for(int wave=2;wave>=1;wave--)
        {
            yield return Fresh(wave);StopPolicies();var actor=game.Bosses[0];nav=actor.Navigation;
            var low=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null).OrderBy(n=>n.Position.y).ThenBy(n=>Mathf.Abs(n.Position.x+nav.SameLevelSpacing*.5f)).First();
            var high=nav.Nodes.Where(n=>n.Collider.GetComponent<PlatformEffector2D>()!=null).OrderByDescending(n=>n.Position.y).ThenBy(n=>Mathf.Abs(n.Position.x)).First();
            Position(actor.gameObject,new Vector2(low.Collider.bounds.center.x,low.Position.y+actor.Motor.Size.y*.5f+.025f));
            var playerSize=(Vector2)game.Player.GetComponent<Collider2D>().bounds.size;
            Position(game.Player.gameObject,new Vector2(high.Collider.bounds.center.x,high.Position.y+playerSize.y*.5f+.025f));
            yield return Seconds(.3f);
            BossAbility ability=wave==2?BossAbility.LeapSlash:BossAbility.TacticalJumpShot;
            bool legal=actor.Tactics.TryPlanMobility(ability,actor.Perception.Facts,out var plan);
            accepted=legal&&actor.Actions.TryStartMobility(plan);
            float startY=actor.Position.y,peak=startY,deadlineAt=Time.time+3;
            bool airborne=false,tell=false;int ammo=actor.Actions.Ammo;var bullets=new HashSet<Projectile>();bool normalBullets=true;
            while(actor.Actions.Busy&&Time.time<deadlineAt)
            {
                peak=Mathf.Max(peak,actor.Position.y);airborne|=!actor.Motor.Grounded;
                tell|=actor.PublicThreats.Any(t=>t.Valid&&(wave==1||t.Shape==BossThreatShape.BladeArc&&t.OriginPath!=null));
                foreach(var shot in Projectile.Active)
                    if(shot!=null&&shot.Owner==actor.gameObject&&bullets.Add(shot))
                        normalBullets&=shot.Damage==actor.Profile.Burst.Damage
                            &&Mathf.Abs(shot.GetComponent<Rigidbody2D>().linearVelocity.magnitude-actor.Profile.BulletSpeed)<.05f&&!actor.Motor.Grounded;
                yield return null;
            }
            Check(ability+" executes physical jump and finishes",accepted&&airborne&&actor.Motor.Grounded&&!actor.Actions.Busy
                &&peak-startY<=ArenaNavigation2D.ExecutableJumpHeight(actor.Motor.Profile)+.12f&&tell,
                $"legal={legal} accepted={accepted} airborne={airborne} tell={tell} peakDelta={peak-startY:F3} end={actor.Position} planned={plan?.Destination}");
            if(wave==1)Check("Jump Shot limited normal airborne Burst/ammo",bullets.Count>0&&bullets.Count<=3&&normalBullets
                &&ammo-actor.Actions.Ammo==bullets.Count,$"shots={bullets.Count} ammo={ammo}->{actor.Actions.Ammo}");
        }
    }
    static IEnumerator PressureObservation()
    {
        Record("NORMAL bounded W2 (25s) / W3 (35s); unchanged all-AI policy/resources. No win/loss requirement.");
        for(int wave=2;wave<=3;wave++)
        {
            yield return Fresh(wave,true);var duo=game.GetComponentInChildren<DuoCoordinator>();bool overlap=false;
            while(game.Phase==DuelPhase.Fight&&game.FightClock<(wave==2?25:35))
            {
                if(duo.Phase==DuoPhase.Idle)
                    overlap|=game.Bosses.Count(a=>a.Actions.HighThreat&&a.Actions.Phase==BossActionPhase.Active)>1;
                yield return null;
            }
            PitchResult();
            Check("W"+wave+" no illegal ordinary major-active overlap",!overlap);
            Record("OBSERVATION recovery handoffs="+duo.RecoveryHandoffs);
            foreach(var ability in new[]{BossAbility.DrawDash,BossAbility.LeapSlash,BossAbility.DashFollowup,BossAbility.TacticalJumpShot})
                Record((game.Bosses.Any(a=>a.Actions.Activated[(int)ability]>0)?"OBSERVED ":"NOT OBSERVED ")+"W"+wave+" "+ability);
            Record($"OBSERVATION pair starts={duo.CombosStarted} Cross={duo.CrossCompleted} SweepDrop={duo.SweepDropCompleted}");
        }
        Record("NOT TESTED: physical input, full regression, exhaustive navigation, balance, Windows build/launch.");
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
                Check("guard reflects a real frontal projectile", actions.Guarded > 0);
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
