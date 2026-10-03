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
        [SerializeField] Collider2D[] platforms;
        public DuelPhase Phase { get; private set; }
        public int Wave { get; private set; } = 1;
        public bool Paused { get; private set; }
        public bool FirstConfiguration { get; private set; }
        public PlayerHealth Player { get; private set; }
        public Enemy Police { get; private set; }
        public PlayerShooting Rifle { get; private set; }
        public PlayerDodge Dodge { get; private set; }
        public PlayerCounterArm Blade { get; private set; }
        public float FightClock { get; private set; }
        public bool LegsAI => lockedLegs;
        public bool ArmsAI => lockedArms;
        public bool BrainAI => lockedBrain;
        public bool DraftLegs { get; private set; }
        public bool DraftArms { get; private set; }
        public bool DraftBrain { get; private set; }
        GameObject encounter;
        DuelUI ui;
        CyberpsychoBoss boss;
        PlayerInputReader input;
        AttemptPresentationVfx effects;
        SfxSet encounterSounds;
        bool lockedLegs, lockedArms, lockedBrain;
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
            DraftLegs = DraftArms = DraftBrain = false;
            FirstConfiguration = true;
            SetPhase(DuelPhase.Configure);
        }
        public void SetDraft(int module, bool automatic)
        {
            if (Phase != DuelPhase.Configure || Paused) return;
            if (module == 0) DraftLegs = automatic;
            else if (module == 1) DraftArms = automatic;
            else if (module == 2) DraftBrain = automatic;
            ui.Show();
        }
        public void StartFight()
        {
            if (Phase != DuelPhase.Configure || Paused) return;
            ClearEncounter();
            lockedLegs = DraftLegs; lockedArms = DraftArms; lockedBrain = DraftBrain;
            encounter = Instantiate(combatPrefab, transform);
            encounter.name = "CombatAttempt";
            Player = encounter.GetComponentInChildren<PlayerHealth>();
            boss = encounter.GetComponentInChildren<CyberpsychoBoss>();
            Police = boss.GetComponent<Enemy>();
            input = Player.GetComponent<PlayerInputReader>();
            input.SetGameplayInputEnabled(false);
            Rifle = Player.GetComponent<PlayerShooting>();
            Dodge = Player.GetComponent<PlayerDodge>();
            Blade = Player.GetComponent<PlayerCounterArm>();
            Player.GetComponent<PlayerMovement>().ConfigureLegacyCombat();
            Rifle.ConfigureLegacyCombat();
            Dodge.ConfigureLegacyCombat();
            Blade.ConfigureLegacyCombat();
            Player.GetComponent<OfficerVisual>()?.ConfigureLegacyCombat();
            boss.ConfigureLegacyPolice(Player, new Bounds(new Vector3(0, 3.25f), new Vector3(30, 11.5f)), Wave, 600, diagnostics);
            var ai = new LegacyPlayerAI(Player, boss, platforms);
            input.ConfigureLegacyControls(lockedLegs, lockedArms, lockedBrain, ai.Legs, ai.Aim, ai.Brain);
            foreach (var body in encounter.GetComponentsInChildren<Rigidbody2D>())
            {
                body.linearVelocity = Vector2.zero;
                body.simulated = false;
            }
            encounterSounds = encounter.GetComponent<SfxSet>();
            encounterSounds?.BeginScope();
            effects = encounter.GetComponent<AttemptPresentationVfx>();
            effects?.BeginAttempt();
            Blade.SetNarrativePaused(true);
            effects?.SetPaused(true);
            Physics2D.SyncTransforms();
            FightClock = 0;
            FirstConfiguration = false;
            SetPhase(DuelPhase.Prepare);
            Trace($"start wave={Wave} hp={Player.CurrentHealth}/{Police.CurrentHealth} observation=empty");
        }
        void BeginCombat()
        {
            foreach (var body in encounter.GetComponentsInChildren<Rigidbody2D>()) body.simulated = true;
            Blade.SetNarrativePaused(false);
            effects?.SetPaused(false);
            input.SetGameplayInputEnabled(true);
            boss.BeginLegacyCombat();
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
            Blade?.SetNarrativePaused(value || Phase == DuelPhase.Prepare);
            boss?.SetLegacyPaused(value);
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
            boss?.StopCombat();
            Player?.StopCombat();
            Blade?.CleanupAttempt();
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
            Player = null; Police = null; boss = null; input = null;
            Rifle = null; Dodge = null; Blade = null; effects = null; encounterSounds = null;
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
            if (Phase != DuelPhase.Fight || Paused || Player == null || Police == null) return;
            if (!Player.IsDead && !Police.IsDead) return;
            bool defeat = Player.IsDead;
            Trace($"result wave={Wave} player={Player.CurrentHealth} police={Police.CurrentHealth} elapsed={FightClock:0.00}");
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
        // Normal wave-three entry, empty observation; no test UI or outcome alteration.
        public void ObserveThirdWave()
        {
            NewGame(); Wave = 3; DraftLegs = DraftArms = DraftBrain = true;
            diagnostics = true; StartFight();
        }
#endif
    }
}
