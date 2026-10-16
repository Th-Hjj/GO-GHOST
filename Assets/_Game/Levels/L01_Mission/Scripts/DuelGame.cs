using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public enum DuelPhase { Menu, Configure, Prepare, Fight, Between, Failed, Complete }

    // Retained three-wave shell. The legacy components own every physical action and hit.
    public sealed class DuelGame : MonoBehaviour
    {
        public const float PreparationSeconds = 3f;
        public float PreparationRemaining => Phase == DuelPhase.Prepare ? Mathf.Max(0, PreparationSeconds - phaseTime) : 0;
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] TMP_FontAsset font;
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

        void Awake()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            music?.StopMusic(true);
            Application.runInBackground = true;
            ui = gameObject.AddComponent<DuelUI>();
            ui.Initialize(this, font);
            Phase = DuelPhase.Menu;
            ui.Show();
        }
        void Start() { if (sounds != null) sounds.BeginScope(); }
        void OnDestroy()
        {
            Rules?.Dispose();Rules=null;
            music?.StopMusic(true);
            Time.timeScale = 1;
            AudioListener.pause = false;
        }
        void OnApplicationFocus(bool focus)
        {
            if (!focus && (Phase == DuelPhase.Fight || Phase == DuelPhase.Prepare)) SetPause(true);
        }
        public void NewGame()
        {
            ClearEncounter();
            Wave = 1;
            DraftLegs = DraftArms = DraftSpine = false;
            DraftLoadout=new ConditionalLoadout();
            FirstConfiguration = true;
            SetPhase(DuelPhase.Configure);
        }
        public void SetDraft(int module, bool automatic)
        {
            // Retained development API cannot re-enable the retired permanent-AI route.
            if(automatic) Debug.LogWarning("Permanent AI permissions are retired. Configure conditional rules instead.");
        }
        public void EditLoadout(System.Action<ConditionalLoadout> edit)
        {if(Phase==DuelPhase.Configure&&!Paused){edit(DraftLoadout);ui.Show();}}
        public void StartFight()
        {
            if (Phase != DuelPhase.Configure || Paused) return;
            ClearEncounter();
            lockedLegs = lockedArms = lockedSpine = false;
            encounter = Instantiate(combatPrefab, transform);
            encounter.name = "CombatAttempt";
            Player = encounter.GetComponentInChildren<PlayerHealth>();
            input = Player.GetComponent<PlayerInputReader>();
            input.SetGameplayInputEnabled(false);
            Rifle = Player.GetComponent<PlayerShooting>();
            Dodge = Player.GetComponent<PlayerDodge>();
            navigation = encounter.AddComponent<ArenaNavigation2D>();
            navigation.Initialize(arenaBounds);
            AlignPlayerOpeningSupport();
            Player.GetComponent<PlayerMovement>().ConfigureLegacyCombat();
            Rifle.ConfigureLegacyCombat();
            Dodge.ConfigureLegacyCombat();
            Player.GetComponent<OfficerVisual>()?.ConfigureLegacyCombat();
            coordinator = encounter.AddComponent<DuoCoordinator>();
            var activeActors = new List<BossAgent>(2);
            // Solo opponents mirror the actual player opening across the arena centre.
            // Keep each Boss's supported height and the authored duo arrangement.
            float soloX = 2 * arenaBounds.center.x - Player.transform.position.x;
            if (Wave == 1 || Wave == 3) activeActors.Add(CreateBoss(gunnerPrefab,
                Wave == 1 ? new Vector2(soloX, gunnerSpawn.y) : gunnerSpawn, 1));
            if (Wave == 2 || Wave == 3) activeActors.Add(CreateBoss(swordsmanPrefab,
                Wave == 2 ? new Vector2(soloX, swordsmanSpawn.y) : swordsmanSpawn, 2));
            actors = activeActors.ToArray();
            Police = actors[0].Health; // Historical development consumers; gameplay uses the full actor list.
            coordinator.Configure(actors, navigation);
            Rules=new ConditionalController(Player,actors,platforms,arenaBounds,DraftLoadout);
            input.ConfigureConditionalControls(Rules.Resolve);
            Player.GetComponent<OfficerVisual>()?.ConfigureConditionalGlow(conditionalGlow);
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
            FirstConfiguration = false;
            SetPhase(DuelPhase.Prepare);
            music?.PlayWave(Wave);
            Trace($"start wave={Wave} player={Player.CurrentHealth} bosses={HealthSummary()} observation=empty");
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
            if (Phase != DuelPhase.Fight && Phase != DuelPhase.Prepare) return;
            if (Paused == value) return;
            Paused = value;
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
        void ClearEncounter()
        {
            StopCombat();
            music?.StopMusic(true);
            Paused = false;
            Time.timeScale = 1;
            AudioListener.pause = false;
            if (encounter != null) { encounter.SetActive(false); Destroy(encounter); }
            encounter = null;
            Player = null; Police = null; input = null;
            actors = System.Array.Empty<BossAgent>(); navigation = null; coordinator = null;
            Rifle = null; Dodge = null; effects = null; encounterSounds = null;
        }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPause(!Paused);
            if (Paused) return;
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
            if (Phase == DuelPhase.Fight && !Paused) FightClock += Time.fixedDeltaTime;
        }
        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera != null) camera.orthographicSize = Mathf.Max(10, 16.8f / Mathf.Max(.3f, camera.aspect));
            if (Phase != DuelPhase.Fight || Paused || Player == null || actors.Length == 0) return;
            bool allDead = true;
            foreach (var actor in actors) if (actor != null && actor.IsAlive) { allDead = false; break; }
            if (!Player.IsDead && !allDead) return;
            bool defeat = Player.IsDead;
            Trace($"result wave={Wave} player={Player.CurrentHealth} bosses={HealthSummary()} elapsed={FightClock:0.00}");
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
            NewGame(); Wave = Mathf.Clamp(wave, 1, 3); ui.Show();
        }
        public void ObserveThirdWave()
        {
            ConfigurePlaytestWave(3);
            diagnostics = true; StartFight();
        }
#endif
    }
}
