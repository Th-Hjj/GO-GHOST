using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GoGhost.Duel
{
    public enum DuelPhase { Menu, Configure, Prepare, Fight, Between, Failed, Complete }
    public enum DuelCue { ShotWindup, Intercept, WaveWindup, Shot, Wave, Hit, Dodge, Cancel }
    public sealed class DuelGame : MonoBehaviour
    {
        [SerializeField] InputActionAsset inputActions;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] SfxSet sounds;
        [SerializeField] bool diagnostics;
        public DuelPhase Phase { get; private set; }
        public int Wave { get; private set; } = 1;
        public bool Paused { get; private set; }
        public bool FirstConfiguration { get; private set; }
        public DuelFighter Player { get; private set; }
        public DuelFighter Police { get; private set; }
        public float FightClock { get; private set; }
        public bool LegsAI => lockedLegs;
        public bool ArmsAI => lockedArms;
        public bool BrainAI => lockedBrain;
        public bool DraftLegs { get; private set; }
        public bool DraftArms { get; private set; }
        public bool DraftBrain { get; private set; }
        public readonly List<DuelProjectile> Projectiles = new List<DuelProjectile>();
        readonly List<DuelProjectile> playerShots = new List<DuelProjectile>(), policeShots = new List<DuelProjectile>();
        readonly List<(DuelFighter target, DuelFighter source, Strike attack)> damage = new List<(DuelFighter,DuelFighter,Strike)>();
        Transform encounter;
        DuelUI ui;
        BuiltInController builtin;
        PoliceController policePolicy;
        InputActionAsset actions;
        InputAction move, jump, dodge, shot, intercept, waveAttack;
        ControlIntent pending;
        bool legsReleased, brainReleased, lockedLegs, lockedArms, lockedBrain;
        float phaseTime;

        void Awake()
        {
            Time.timeScale = 1; Application.runInBackground = true;
            actions = Instantiate(inputActions);
            move = actions.FindAction("Player/Move", true); jump = actions.FindAction("Player/Jump", true);
            dodge = actions.FindAction("Player/Dodge", true); shot = actions.FindAction("Player/Attack", true);
            intercept = actions.FindAction("UI/RightClick", true); waveAttack = actions.FindAction("UI/MiddleClick", true);
            Restrict(move,"<Keyboard>/a","<Keyboard>/d"); Restrict(jump,"<Keyboard>/space");
            Restrict(dodge,"<Keyboard>/leftShift"); Restrict(shot,"<Mouse>/leftButton");
            Restrict(intercept,"<Mouse>/rightButton"); Restrict(waveAttack,"<Mouse>/middleButton");
            move.Enable(); jump.Enable(); dodge.Enable(); shot.Enable(); intercept.Enable(); waveAttack.Enable();
            ui = gameObject.AddComponent<DuelUI>(); ui.Initialize(this, font);
            Phase = DuelPhase.Menu; ui.Show();
        }
        void Start() { if(sounds!=null)sounds.BeginScope(); }
        static void Restrict(InputAction action, params string[] paths)
        {
            for(int i=0;i<action.bindings.Count;i++)
                if(!action.bindings[i].isComposite && System.Array.IndexOf(paths,action.bindings[i].path)<0) action.ApplyBindingOverride(i,"");
        }
        void LateUpdate()
        {
            var cam=Camera.main;
            if(cam!=null)cam.orthographicSize=Mathf.Max(9f,15.5f/Mathf.Max(.3f,cam.aspect));
        }
        void OnDestroy()
        {
            Time.timeScale = 1; AudioListener.pause=false;
            if (actions != null) { actions.Disable(); Destroy(actions); }
        }
        void OnApplicationFocus(bool focus) { if (!focus) { pending = default; legsReleased = brainReleased = false; if (Phase == DuelPhase.Fight || Phase == DuelPhase.Prepare) SetPause(true); } }
        public void NewGame()
        {
            ClearEncounter(); Wave = 1; DraftLegs = DraftArms = DraftBrain = false;
            FirstConfiguration = true; SetPhase(DuelPhase.Configure);
        }
        public void SetDraft(int module, bool automatic)
        {
            if (Phase != DuelPhase.Configure || Paused) return;
            if (module == 0) DraftLegs = automatic; else if (module == 1) DraftArms = automatic; else DraftBrain = automatic;
            ui.Show();
        }
        public void StartFight()
        {
            if (Phase != DuelPhase.Configure) return;
            ClearEncounter();
            lockedLegs = DraftLegs; lockedArms = DraftArms; lockedBrain = DraftBrain;
            encounter = new GameObject("Duel").transform; encounter.SetParent(transform);
            Player = CreateFighter("You", new Vector2(-6,1.002f),120,1,true);
            Police = CreateFighter("Police",new Vector2(6,1.002f),Wave == 1 ? 180 : 240,-1,false);
            Player.Opponent = Police; Police.Opponent = Player;
            builtin = new BuiltInController(); policePolicy = new PoliceController(Wave, Trace);
            Physics2D.SyncTransforms(); FightClock = 0; pending = default; legsReleased = brainReleased = false;
            FirstConfiguration = false; SetPhase(DuelPhase.Prepare);
            Trace($"start wave={Wave} hp=120/{Police.Health} charges=2/2 observation=empty");
        }
        DuelFighter CreateFighter(string label, Vector2 p, int hp, int facing, bool isPlayer)
        {
            var go = new GameObject(label); go.transform.SetParent(encounter);
            var f = go.AddComponent<DuelFighter>(); f.Initialize(this,p,hp,facing);
            go.AddComponent<DuelVisual>().Initialize(f,isPlayer); return f;
        }
        public void Reconfigure() { if (Phase == DuelPhase.Failed) { ClearEncounter(); SetPhase(DuelPhase.Configure); } }
        public void MainMenu() { ClearEncounter(); Paused = false; Time.timeScale = 1; SetPhase(DuelPhase.Menu); }
        public void SetPause(bool value)
        {
            if (Phase != DuelPhase.Fight && Phase != DuelPhase.Prepare) return;
            Paused = value; Time.timeScale = value ? 0 : 1; pending = default; legsReleased = brainReleased = false;
            AudioListener.pause = value; ui.Show();
        }
        void SetPhase(DuelPhase value) { Phase = value; phaseTime = 0; pending = default; ui.Show(); }
        void ClearEncounter()
        {
            AudioListener.pause = false;
            foreach (var p in Projectiles) p.Dispose(); Projectiles.Clear(); damage.Clear(); playerShots.Clear(); policeShots.Clear();
            if (encounter != null) { encounter.gameObject.SetActive(false); Destroy(encounter.gameObject); }
            Player = Police = null;
        }
        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPause(!Paused);
            if (Paused) return;
            if (Phase == DuelPhase.Prepare || Phase == DuelPhase.Between)
            {
                phaseTime += Time.deltaTime;
                if (Phase == DuelPhase.Prepare && phaseTime >= 1) { legsReleased = brainReleased = false; SetPhase(DuelPhase.Fight); }
                else if (Phase == DuelPhase.Between && phaseTime >= .8f) { Wave++; ClearEncounter(); SetPhase(DuelPhase.Configure); }
            }
            if (Phase != DuelPhase.Fight) return;
            pending.Move = Mathf.Clamp(move.ReadValue<Vector2>().x,-1,1);
            pending.Aim = Mouse.current == null ? Player.Facing : Mouse.current.position.ReadValue().x < Screen.width * .5f ? -1 : 1;
            if(!legsReleased)legsReleased=!jump.IsPressed()&&!dodge.IsPressed();
            if(!brainReleased)brainReleased=!shot.IsPressed()&&!intercept.IsPressed()&&!waveAttack.IsPressed();
            if(legsReleased){pending.Jump |= jump.WasPressedThisFrame(); pending.Dodge |= dodge.WasPressedThisFrame();}
            pending.DodgeDirection = Mathf.Abs(pending.Move) > .1f ? (int)Mathf.Sign(pending.Move) : Player.Position.x < Police.Position.x ? -1 : 1;
            if(brainReleased)
            {
                if (shot.WasPressedThisFrame()) pending.Attack = Strike.Shot;
                if (intercept.WasPressedThisFrame()) pending.Attack = Strike.Intercept;
                if (waveAttack.WasPressedThisFrame()) pending.Attack = Strike.Wave;
            }
        }
        void FixedUpdate()
        {
            if (Phase != DuelPhase.Fight || Paused) return;
            float dt = Time.fixedDeltaTime; FightClock += dt;
            var player = new FighterView(Player); var police = new FighterView(Police);
            playerShots.Clear(); policeShots.Clear();
            foreach (var p in Projectiles) if (p.Alive) { if (p.Owner == Player) playerShots.Add(p); else policeShots.Add(p); }
            var automatic = builtin.Decide(player, police, policeShots);
            var control = pending;
            if (lockedLegs) { control.Move=automatic.Move; control.Jump=automatic.Jump; control.Dodge=automatic.Dodge; control.DodgeDirection=automatic.DodgeDirection; }
            if (lockedArms) control.Aim=automatic.Aim;
            if (lockedBrain) control.Attack=automatic.Attack;
            var opposition = policePolicy.Decide(police, player, playerShots, FightClock);
            pending.Jump = pending.Dodge = false; pending.Attack = Strike.None;
            Player.Step(control,dt,FightClock); Police.Step(opposition,dt,FightClock);
            foreach (var p in Projectiles) p.Step(this,dt);
            Projectiles.RemoveAll(p=>!p.Alive);
            // Resolve the complete physics step before deciding the result; simultaneous death is failure.
            foreach (var hit in damage) hit.target.Receive(hit.attack,hit.source);
            damage.Clear();
            if (Player.Health <= 0 || Police.Health <= 0)
            {
                foreach (var p in Projectiles) p.Dispose(); Projectiles.Clear();
                Trace($"result wave={Wave} player={Player.Health} police={Police.Health} elapsed={FightClock:0.00}");
                SetPhase(Player.Health <= 0 ? DuelPhase.Failed : Wave == 3 ? DuelPhase.Complete : DuelPhase.Between);
            }
        }
        public void Launch(DuelFighter owner, Strike attack) { Projectiles.Add(new DuelProjectile(owner,attack,encounter)); Cue(attack == Strike.Wave ? DuelCue.Wave : DuelCue.Shot); }
        public void Slash(DuelFighter owner, HashSet<DuelFighter> struck)
        {
            var center = owner.Position + new Vector2(owner.Facing * 1.3f,.2f);
            foreach (var c in Physics2D.OverlapBoxAll(center,new Vector2(2.6f,2.4f),0))
            {
                var target = c.GetComponent<DuelFighter>();
                if (target != null && target != owner && struck.Add(target)) QueueHit(target,owner,Strike.Intercept);
            }
        }
        public void QueueHit(DuelFighter target, DuelFighter source, Strike attack) { if (Phase == DuelPhase.Fight) damage.Add((target,source,attack)); }
        public void Cue(DuelCue cue)
        {
            if (sounds == null) return;
            Sfx id = cue == DuelCue.Hit ? Sfx.BulletHit : cue == DuelCue.Shot ? Sfx.Shot : cue == DuelCue.Intercept ? Sfx.BladeSwing : cue == DuelCue.Wave ? Sfx.Counter : cue == DuelCue.Cancel ? Sfx.Click : cue == DuelCue.Dodge ? Sfx.Equip : cue == DuelCue.WaveWindup ? Sfx.Glitch : Sfx.Click;
            sounds.Play(id);
        }
        public void Trace(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (diagnostics) Debug.Log($"[Duel {FightClock:0.00}] {message}");
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // No gameplay UI. Normal spawns/stats, fresh policy memory, identical release and outcome path.
        public void ObserveThirdWave() { NewGame(); Wave=3; DraftLegs=DraftArms=DraftBrain=true; diagnostics=true; StartFight(); }
#endif
    }
}
