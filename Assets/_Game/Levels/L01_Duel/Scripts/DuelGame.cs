using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public enum DuelPhase { Menu, Configure, Prepare, Fight, Between, Failed, Complete, Training }

    // Retained three-wave shell. The legacy components own every physical action and hit.
    public sealed partial class DuelGame : MonoBehaviour
    {
        public const float PreparationSeconds = 3f;
        public float PreparationRemaining => Phase == DuelPhase.Prepare ? Mathf.Max(0, PreparationSeconds - phaseTime) : 0;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] TMP_FontAsset controlFont, headingFont, numericFont;
        [SerializeField] SfxSet sounds;
        [SerializeField] WaveMusicPlayer music;
        [SerializeField] bool diagnostics;
        [SerializeField] GameObject combatPrefab;
        [SerializeField] GameObject gunnerPrefab;
        [SerializeField] GameObject swordsmanPrefab;
        [SerializeField] Material conditionalGlow;
        [SerializeField] Bounds arenaBounds = new Bounds(new Vector3(0, 3.25f), new Vector3(30, 11.5f));
        [SerializeField] Vector2 gunnerSpawn = new Vector2(7, -1.6f);
        [SerializeField] Vector2 swordsmanSpawn = new Vector2(3, -1.6f);
        [SerializeField] Collider2D[] platforms;
        public DuelPhase Phase { get; private set; }
        public int Wave { get; private set; } = 1;
        public bool Paused { get; private set; }
        public bool FirstConfiguration { get; private set; }
        public PlayerHealth Player { get; private set; }
        public Enemy Police { get; private set; }
        public IReadOnlyList<BossAgent> Bosses => actors;
        public PlayerShooting Rifle { get; private set; }
        public PlayerDodge Dodge { get; private set; }
        public float FightClock { get; private set; }
        public ConditionalLoadout DraftLoadout {get;private set;}=new ConditionalLoadout();
        public ConditionalController Rules {get;private set;}
        public Bounds ArenaBounds => arenaBounds;
        public bool TrainingPending => developerTrainingSelected || FirstConfiguration && Wave == 1;
        public bool LegsAI => lockedLegs;
        public bool ArmsAI => lockedArms;
        public bool SpineAI => lockedSpine;
        public bool DraftLegs { get; private set; }
        public bool DraftArms { get; private set; }
        public bool DraftSpine { get; private set; }
        GameObject encounter;
        DuelUI ui;
        BossAgent[] actors = System.Array.Empty<BossAgent>();
        ArenaNavigation2D navigation;
        DuoCoordinator coordinator;
        PlayerInputReader input;
        AttemptPresentationVfx effects;
        SfxSet encounterSounds;
        bool lockedLegs, lockedArms, lockedSpine;
        float phaseTime;
        bool presetDeathRecorded;

        void Awake()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            music?.StopMusic(true);
            Application.runInBackground = true;
            if(Camera.main!=null)Camera.main.backgroundColor=NeuralPalette.Background;
            // Existing authored arena renderers only; no collider, platform or camera geometry changes.
            if(platforms!=null&&platforms.Length>0&&platforms[0]!=null)
                foreach(var surface in platforms[0].transform.parent.GetComponentsInChildren<SpriteRenderer>())
                    surface.color=Color.Lerp(NeuralPalette.Panel,NeuralPalette.Secondary,.70f);
            InitializeDeveloperTuning();
            ui = gameObject.AddComponent<DuelUI>();
            ui.Initialize(this, font, controlFont, headingFont, numericFont);
            Phase = DuelPhase.Menu;
            ui.Show();
        }
        void Start() { if (sounds != null) sounds.BeginScope(); }
        void OnDestroy()
        {
            UnbindPresetDeath();
            FinishMetrics("abandoned");
            ClearPlatformLayout();
            playerRespawnAt = enemyRespawnAt = 0;
            Player?.GetComponent<OfficerVisual>()?.ClearConditionalGlow();
            Rules?.Dispose();Rules=null;
            music?.StopMusic(true);
            Time.timeScale = 1;
            AudioListener.pause = false;
        }
        void OnApplicationFocus(bool focus)
        {
            if (!focus) developerResumeOnClose = false;
            if (!focus && (Phase == DuelPhase.Fight || Phase == DuelPhase.Prepare || Phase == DuelPhase.Training)) SetPause(true);
        }
        public void NewGame()
        {
            ClearEncounter();
            PlayMetrics.ResetRun();
            metricsWaveJumpPending = false;
            Wave = 1;
            DraftLegs = DraftArms = DraftSpine = false;
            DraftLoadout=new ConditionalLoadout();
            FirstConfiguration = true;
            developerTrainingSelected = false;
            SetPhase(DuelPhase.Configure);
        }
        public void SetDraft(int module, bool automatic)
        {
            // Retained development API cannot re-enable the retired permanent-AI route.
            if(automatic) Debug.LogWarning("Permanent AI permissions are retired. Configure conditional rules instead.");
        }
        public void EditLoadout(System.Action<ConditionalLoadout> edit)
        {if(Phase==DuelPhase.Configure&&!Paused){edit(DraftLoadout);ui.Show();}}
        public bool ApplyPresetToSetup(OfficialRulePreset preset)
        {
            if(Phase!=DuelPhase.Configure||Paused||DeveloperToolsOpen||!OfficialRulePresets.IsUnlocked(preset))return false;
            var replacement=preset.CreateLoadout();
            if(!ConditionalCatalog.IsValid(replacement))return false;
            ui.DiscardRulePicker();
            DraftLoadout=replacement;
            ui.Show();
            return true;
        }
        public void StartFight()
        {
            if (Phase != DuelPhase.Configure || Paused || !ui.ConfigurationReady) return;
            CreateEncounter(false);
        }
        void CreateEncounter(bool training)
        {
            ClearEncounter();
            developerTrainingSelected = training;
            ApplyPlatformLayout(training);
            lockedLegs = lockedArms = lockedSpine = false;
            encounter = Instantiate(combatPrefab, transform);
            encounter.name = "CombatAttempt";
            encounter.AddComponent<ArenaBoundary2D>().Initialize(ArenaBounds);
            Player = encounter.GetComponentInChildren<PlayerHealth>();
            presetDeathRecorded = false;
            Player.OnDeath += RecordPresetDeath;
            input = Player.GetComponent<PlayerInputReader>();
            input.SetGameplayInputEnabled(false);
            Rifle = Player.GetComponent<PlayerShooting>();
            Dodge = Player.GetComponent<PlayerDodge>();
            navigation = encounter.AddComponent<ArenaNavigation2D>();
            navigation.Initialize(ArenaBounds);
            AlignPlayerOpeningSupport();
            Player.GetComponent<PlayerMovement>().ConfigureLegacyCombat();
            Rifle.ConfigureLegacyCombat();
            Dodge.ConfigureLegacyCombat();
            Player.GetComponent<OfficerVisual>()?.ConfigureLegacyCombat();
            var playerTorso = Player.GetComponent<OfficerVisual>()?.Part(LoadoutSlot.Torso);
            if (playerTorso != null)
            {
                var cooldownView = new GameObject("DodgeCooldown").AddComponent<DodgeCooldownView>();
                cooldownView.transform.SetParent(Player.transform, false);
                cooldownView.Initialize(this, playerTorso.sharedMaterial, playerTorso.sortingLayerID);
            }
            if (!training) coordinator = encounter.AddComponent<DuoCoordinator>();
            var activeActors = new List<BossAgent>(2);
            // Solo opponents mirror the actual player opening across the arena centre.
            // Keep each Boss's supported height and the authored duo arrangement.
            float soloX = 2 * arenaBounds.center.x - Player.transform.position.x;
            if (training) activeActors.Add(CreateTrainingEnemy());
            else
            {
                if (Wave == 1 || Wave == 3) activeActors.Add(CreateBoss(gunnerPrefab,
                    Wave == 1 ? new Vector2(soloX, gunnerSpawn.y) : gunnerSpawn, 1));
                if (Wave == 2 || Wave == 3) activeActors.Add(CreateBoss(swordsmanPrefab,
                    Wave == 2 ? new Vector2(soloX, swordsmanSpawn.y) : swordsmanSpawn, 2));
            }
            actors = activeActors.ToArray();
            if (!training)
                foreach (var actor in actors) actor.Tactics.ConfigurePlatformPressure(ActivePlatforms);
            ConfigureDodgeBodies();
            ApplyDeveloperTuning(true);
            Police = actors[0].Health; // Historical development consumers; gameplay uses the full actor list.
            coordinator?.Configure(actors, navigation);
            BindEncounterRules();
            if (training) input.UiBlocksManualFire = ui.TrainingOwnsPointer;
            foreach (var body in encounter.GetComponentsInChildren<Rigidbody2D>())
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            encounterSounds = encounter.GetComponent<SfxSet>();
            encounterSounds?.BeginScope();
            effects = encounter.GetComponent<AttemptPresentationVfx>();
            effects?.BeginAttempt();
            effects?.SetPaused(true);
            Physics2D.SyncTransforms();
            FightClock = 0;
            if (!training) BeginMetrics();
            if (training)
            {
                foreach (var body in encounter.GetComponentsInChildren<Rigidbody2D>()) body.simulated = true;
                effects?.SetPaused(false);
                input.SetGameplayInputEnabled(true);
                actors[0].StartCombat();
                SetPhase(DuelPhase.Training);
                return;
            }
            FirstConfiguration = false;
            SetPhase(DuelPhase.Prepare);
            music?.PlayWave(Wave);
            Trace($"start wave={Wave} player={Player.CurrentHealth} bosses={HealthSummary()} observation=empty");
        }
        void ConfigureDodgeBodies()
        {
            var bodies = new Collider2D[actors.Length];
            for (int i = 0; i < actors.Length; i++) bodies[i] = actors[i].Body;
            Dodge.ConfigurePassThroughBodies(bodies);
        }
        void AlignPlayerOpeningSupport()
        {
            var shape = Player.GetComponent<Collider2D>();
            var body = Player.GetComponent<Rigidbody2D>();
            var bounds = shape.bounds;
            if (!navigation.TrySupport(bounds.center, bounds.size, out var support)) return;
            // Match this arena's measured resting contact skin before freezing preparation.
            // This is a small spawn-only correction, never a combat/landing snap.
            float correction = support.bounds.max.y + .015f - bounds.min.y;
            if (Mathf.Abs(correction) > .08f) return;
            body.position += Vector2.up * correction;
            // The prefab interpolates its Rigidbody. Update its spawn Transform as well so
            // disabling simulation cannot restore the pre-correction interpolation pose.
            Player.transform.position = new Vector3(body.position.x, body.position.y, Player.transform.position.z);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }
        BossAgent CreateBoss(GameObject prefab, Vector2 spawn, int actorId)
        {
            var instance = Instantiate(prefab, (Vector3)spawn, Quaternion.identity, encounter.transform);
            var actor = instance.GetComponent<BossAgent>();
            actor.Configure(Player, navigation, coordinator, actorId, diagnostics);
            return actor;
        }
        string HealthSummary()
        {
            string summary = "";
            foreach (var actor in actors)
                if (actor != null) summary += (summary.Length == 0 ? "" : ",") + actor.ActorId + ":" + actor.Health.CurrentHealth;
            return summary;
        }
        void BeginCombat()
        {
            foreach (var body in encounter.GetComponentsInChildren<Rigidbody2D>()) body.simulated = true;
            effects?.SetPaused(false);
            input.SetGameplayInputEnabled(true);
            foreach (var actor in actors) actor.StartCombat();
            coordinator.BeginCombat();
            PlayMetrics.StartCounting();   // METRICS: jumps and dodges count from here
            SetPhase(DuelPhase.Fight);
        }
        public void Reconfigure()
        {
            if (Phase != DuelPhase.Failed) return;
            ClearEncounter();
            SetPhase(DuelPhase.Configure);
        }
        public void MainMenu()
        {
            ClearEncounter();
            SetPhase(DuelPhase.Menu);
        }
        public void SetPause(bool value)
        {
            if (DeveloperToolsOpen && !value) return;
            if (Phase != DuelPhase.Fight && Phase != DuelPhase.Prepare && Phase != DuelPhase.Training) return;
            if (Paused == value) return;
            Paused = value;
            Rules?.SetFeedbackPaused(value);
            input?.SetNarrativePaused(value);
            foreach (var actor in actors) if (actor != null) actor.SetPaused(value);
            coordinator?.SetPaused(value);
            effects?.SetPaused(value || Phase == DuelPhase.Prepare);
            Time.timeScale = value ? 0 : 1;
            AudioListener.pause = value;
            ui.Show();
        }
        void SetPhase(DuelPhase value)
        {
            Phase = value;
            phaseTime = 0;
            ui.Show();
        }
        void StopCombat()
        {
            Rules?.Dispose();Rules=null;
            Player?.GetComponent<OfficerVisual>()?.ClearConditionalGlow();
            music?.StopMusic();
            if (encounter == null) return;
            // All contacts from this rendered frame finish before this LateUpdate boundary.
            foreach (var projectile in encounter.GetComponentsInChildren<Projectile>()) projectile.Retire();
            coordinator?.StopCombat();
            foreach (var actor in actors) if (actor != null) actor.StopCombat();
            Player?.StopCombat();
            effects?.EndAttempt();
            encounterSounds?.EndScope();
        }
        void RecordPresetDeath()
        {
            if (presetDeathRecorded || Player == null || !Player.IsDead) return;
            presetDeathRecorded = true;
            OfficialRulePresets.RecordPlayerDeath();
        }
        void UnbindPresetDeath()
        {
            if (Player != null) Player.OnDeath -= RecordPresetDeath;
        }
        void ClearEncounter()
        {
            UnbindPresetDeath();
            FinishMetrics("abandoned");
            DeveloperToolsOpen = developerResumeOnClose = false;
            StopCombat();
            music?.StopMusic(true);
            Paused = false;
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (encounter != null) { encounter.SetActive(false); Destroy(encounter); }
            ClearPlatformLayout();
            encounter = null;
            Player = null; Police = null; input = null;
            actors = System.Array.Empty<BossAgent>(); navigation = null; coordinator = null;
            Rifle = null; Dodge = null; effects = null; encounterSounds = null;
            playerRespawnAt = enemyRespawnAt = 0;
        }
        void Update()
        {
            if (!ui.DeveloperShortcutConsumed && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPause(!Paused);
            if (Paused || DeveloperToolsOpen) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Phase==DuelPhase.Fight&&Wave==3&&Player!=null&&!Player.IsDead
                &&Keyboard.current!=null&&Keyboard.current.pKey.wasPressedThisFrame)coordinator?.RequestNextDebugCombo();
#endif
            if (Phase == DuelPhase.Prepare || Phase == DuelPhase.Between)
            {
                phaseTime += Time.deltaTime;
                if (Phase == DuelPhase.Prepare && phaseTime >= PreparationSeconds) BeginCombat();
                else if (Phase == DuelPhase.Between && phaseTime >= .8f)
                {
                    Wave++;
                    ClearEncounter();
                    SetPhase(DuelPhase.Configure);
                }
            }
        }
        void FixedUpdate()
        {
            if ((Phase == DuelPhase.Fight || Phase == DuelPhase.Training) && !Paused) FightClock += Time.fixedDeltaTime;
            if (Phase == DuelPhase.Fight) PlayMetrics.SetClock(FightClock);
        }
        void LateUpdate()
        {
            ui.UpdateBattleLayout();
            if (Phase == DuelPhase.Training) { TickTrainingRespawn(); return; }
            if (Phase != DuelPhase.Fight || Paused || Player == null || actors.Length == 0) return;
            bool allDead = true;
            foreach (var actor in actors) if (actor != null && actor.IsAlive) { allDead = false; break; }
            if (!Player.IsDead && !allDead) return;
            bool defeat = Player.IsDead;
            Trace($"result wave={Wave} player={Player.CurrentHealth} bosses={HealthSummary()} elapsed={FightClock:0.00}");
            FinishMetrics(defeat ? "died" : "won", !defeat && Wave == 3);
            StopCombat();
            sounds?.Play(defeat ? Sfx.Defeat : Sfx.Victory);
            SetPhase(defeat ? DuelPhase.Failed : Wave == 3 ? DuelPhase.Complete : DuelPhase.Between);
        }
        public void Trace(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (diagnostics) Debug.Log($"[LegacyCombat {FightClock:0.00}] {message}");
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Entry selects only the normal configuration; no fight-state injection.
        public void ConfigurePlaytestWave(int wave)
        {
            NewGame(); Wave = Mathf.Clamp(wave, 1, 3); metricsWaveJumpPending = true; ui.Show();
        }
        public void ObserveThirdWave()
        {
            ConfigurePlaytestWave(3);
            diagnostics = true; StartFight();
        }
#endif
    }
}
