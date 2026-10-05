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
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] SfxSet sounds;
        [SerializeField] bool diagnostics;
        [SerializeField] GameObject combatPrefab;
        [SerializeField] GameObject gunnerPrefab;
        [SerializeField] GameObject swordsmanPrefab;
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
            Application.runInBackground = true;
            ui = gameObject.AddComponent<DuelUI>();
            ui.Initialize(this, font);
            Phase = DuelPhase.Menu;
            ui.Show();
        }
        void Start() { if (sounds != null) sounds.BeginScope(); }
        void OnDestroy()
        {
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
            FirstConfiguration = true;
            SetPhase(DuelPhase.Configure);
        }
        public void SetDraft(int module, bool automatic)
        {
            if (Phase != DuelPhase.Configure || Paused) return;
            if (module == 0) DraftLegs = automatic;
            else if (module == 1) DraftArms = automatic;
            else if (module == 2) DraftSpine = automatic;
            ui.Show();
        }
        public void StartFight()
        {
            if (Phase != DuelPhase.Configure || Paused) return;
            ClearEncounter();
            lockedLegs = DraftLegs; lockedArms = DraftArms; lockedSpine = DraftSpine;
            encounter = Instantiate(combatPrefab, transform);
            encounter.name = "CombatAttempt";
            Player = encounter.GetComponentInChildren<PlayerHealth>();
            input = Player.GetComponent<PlayerInputReader>();
            input.SetGameplayInputEnabled(false);
            Rifle = Player.GetComponent<PlayerShooting>();
            Dodge = Player.GetComponent<PlayerDodge>();
            Player.GetComponent<PlayerMovement>().ConfigureLegacyCombat();
            Rifle.ConfigureLegacyCombat();
            Dodge.ConfigureLegacyCombat();
            Player.GetComponent<OfficerVisual>()?.ConfigureLegacyCombat();
            navigation = encounter.AddComponent<ArenaNavigation2D>();
            navigation.Initialize(arenaBounds);
            coordinator = encounter.AddComponent<DuoCoordinator>();
            var activeActors = new List<BossAgent>(2);
            if (Wave == 1 || Wave == 3) activeActors.Add(CreateBoss(gunnerPrefab, gunnerSpawn, 1));
            if (Wave == 2 || Wave == 3) activeActors.Add(CreateBoss(swordsmanPrefab, swordsmanSpawn, 2));
            actors = activeActors.ToArray();
            Police = actors[0].Health; // Historical development consumers; gameplay uses the full actor list.
            coordinator.Configure(actors, navigation);
            var ai = new LegacyPlayerAI(Player, actors, platforms);
            input.ConfigureLegacyControls(lockedLegs, lockedArms, lockedSpine, ai.Legs, ai.Arms, ai.Spine);
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
            Trace($"start wave={Wave} player={Player.CurrentHealth} bosses={HealthSummary()} observation=empty");
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
            if (Phase == DuelPhase.Prepare || Phase == DuelPhase.Between)
            {
                phaseTime += Time.deltaTime;
                if (Phase == DuelPhase.Prepare && phaseTime >= 1) BeginCombat();
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
            for (int i=0;i<3;i++) SetDraft(i,true);
            diagnostics = true; StartFight();
        }
#endif
    }
}
