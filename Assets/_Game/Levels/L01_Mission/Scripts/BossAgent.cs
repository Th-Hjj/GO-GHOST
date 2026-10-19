using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    [RequireComponent(typeof(Enemy),typeof(BossMotor2D),typeof(BossActionExecutor))]
    public sealed class BossAgent : MonoBehaviour, IProjectileReceiver
    {
        [SerializeField] BossProfile profile;
        [SerializeField] Projectile projectile;
        public BossProfile Profile=>profile;
        public Projectile ProjectilePrefab=>projectile;
        public int ActorId {get;private set;}
        public Enemy Health {get;private set;}
        public Collider2D Body {get;private set;}
        public BossMotor2D Motor {get;private set;}
        public BossActionExecutor Actions {get;private set;}
        public BossSilhouetteView View {get;private set;}
        public ArenaNavigation2D Navigation {get;private set;}
        public DuoCoordinator Coordinator {get;private set;}
        public BossPerception Perception {get;private set;}
        public BossTactics Tactics {get;private set;}
        public PlayerHealth Player {get;private set;}
        public bool IsAlive=>Health!=null&&!Health.IsDead;
        public bool IsCamo=>Actions!=null&&Actions.Camouflaged;
        public Vector2 Position=>Motor.Position;
        public Vector2 Velocity=>Motor.Velocity;
        public List<BossThreat> PublicThreats=>Actions.PublicThreats;
        public bool Running {get;private set;}
        public float Facing {get;private set;}=-1;
        public SfxSet Sounds {get;private set;}
        public bool Diagnostics {get;private set;}
        BossContact lastContact;
        float previousFootstep;
        Vector2 projectileContact;
        float projectileContactAt=-100;
        bool wasGrounded, wasClose;
        public float VisualAlpha=>!IsCamo ? 1 : profile.CamoAlpha;
        public void Configure(PlayerHealth player,ArenaNavigation2D navigation,DuoCoordinator coordinator,int actorId,bool diagnostics)
        {
            Player=player;Navigation=navigation;Coordinator=coordinator;ActorId=actorId;Diagnostics=diagnostics;
            Health=GetComponent<Enemy>(); Body=GetComponent<Collider2D>(); Motor=GetComponent<BossMotor2D>();
            Actions=GetComponent<BossActionExecutor>(); View=GetComponentInChildren<BossSilhouetteView>();
            Sounds=GetComponentInParent<SfxSet>();
            Health.ConfigureFixedSingleLife(profile.Health);Health.SetTarget(player);
            Health.OnDied+=OnDied;Health.OnDamaged+=OnDamaged;
            Motor.Configure(profile.Motion,navigation);Motor.SetCombatEnabled(false);
            Perception=new BossPerception(player,profile);Tactics=new BossTactics(this);
            Actions.Configure(this);View?.Configure(this);
            wasGrounded=Motor.Grounded;
            if(BossPerception.CanSee(player.transform.position,Body.bounds))PublishContact(true);
        }
        public void StartCombat() {Running=true;Motor.SetCombatEnabled(true);}
        public void SetPaused(bool paused) {View?.SetPaused(paused);}
        public void StopCombat()
        { Running=false;Actions?.StopCombat();Motor?.SetCombatEnabled(false);Health?.StopCombat();Sounds?.StopActor(ActorId);View?.ClearTells(); }
        void OnDestroy()
        {
            Perception?.Dispose();
            if(Health!=null){Health.OnDied-=OnDied;Health.OnDamaged-=OnDamaged;}
        }
        void OnDied(Enemy enemy)
        {
            StopCombat();Body.enabled=false;
            foreach(var c in GetComponentsInChildren<Collider2D>())c.enabled=false;
            GetComponent<Rigidbody2D>().simulated=false;
            Coordinator?.Release(this);View?.Defeat();
            Sounds?.Play(Sfx.BossDefeat,ActorId,Actions.AttackId);
            Trace("defeated");
        }
        void OnDamaged(int amount,GameObject source)
        {
            if(!IsAlive)return;
            Vector2 point=Time.time-projectileContactAt<=Time.fixedDeltaTime+.001f?projectileContact
                :source!=null?Body.ClosestPoint(source.transform.position):Position;
            projectileContactAt=-100;
            PublishCue(.08f,point);View?.Hit();
            Actions.OnDamaged(source);
        }
        public void Face(Vector2 direction)
        { if(Mathf.Abs(direction.x)>.05f) Facing=Mathf.Sign(direction.x); }
        void Update()
        {
            if(!Running||!IsAlive||Time.deltaTime<=0)return;
            Perception.Tick(Position+Vector2.up*.35f);
            if(!Actions.Committed)
            {
                var facts=Perception.Facts;
                if(facts.Confidence>.1f)Face(facts.Position-Position);
                else if(Mathf.Abs(Velocity.x)>.1f)Face(Velocity);
            }
            Tactics.Tick();
            bool grounded=Motor.Grounded;
            if(grounded&&!wasGrounded)
            {View?.Landing();Sounds?.Play(Sfx.Land,ActorId);PublishCue(.12f);}
            if(!grounded&&wasGrounded&&IsCamo)PublishCue(.10f);
            if(!IsCamo&&grounded&&Mathf.Abs(Velocity.x)>.7f&&Time.time>=previousFootstep)
            {previousFootstep=Time.time+.28f;Sounds?.Play(Sfx.Footstep,ActorId);}
            wasGrounded=grounded;
            // A body-contact event is a clue, not an every-frame proximity radar.
            bool close=IsCamo&&Player!=null&&Body.bounds.SqrDistance(Player.transform.position)<.16f;
            if(close&&!wasClose)PublishCue(.08f);
            wasClose=close;
        }
        void PublishContact(bool visible)
        {
            lastContact=new BossContact {ActorId=ActorId,Bounds=Body.bounds,Velocity=visible?Velocity:Vector2.zero,
                VelocityKnown=visible,Visible=visible,Dynamic=true,Gravity=Mathf.Abs(Physics2D.gravity.y*profile.Motion.GravityScale),
                GroundedKnown=visible,Grounded=visible&&Motor.Grounded,
                Preparing=visible&&Actions.PublicThreats.Count>0&&(Actions.Phase==BossActionPhase.Prepare||Actions.Phase==BossActionPhase.Locked),
                PreparationId=visible?Actions.PreparationId:0,
                Recovering=visible&&Actions.Phase==BossActionPhase.Recovery&&!Actions.SwordResidualWaiting,Reloading=visible&&Actions.Reloading,
                HealthFraction=visible?(float)Health.CurrentHealth/Health.MaxHealth:0,
                ObservedAt=Time.time,ExpiresAt=Time.time+(IsCamo?profile.CamoContactMemory:profile.SightMemory)};
        }
        public void BeginCamo()
        {
            // Retire the old visible velocity/memory even if departure is behind terrain.
            lastContact.Visible=false;lastContact.VelocityKnown=false;lastContact.Velocity=Vector2.zero;
            lastContact.ExpiresAt=Mathf.Min(lastContact.ExpiresAt,Time.time+.10f);
            PublishCue(.10f);
        }
        public void PublishCue(float duration) => PublishCue(duration,null);
        void PublishCue(float duration,Vector2? point)
        {
            View?.CamoCue(duration,point);
            if(Player!=null&&BossPerception.CanSee(Player.transform.position,Body.bounds))
            {
                PublishContact(false);
                // A concealed cue exposes only a stationary snapshot for its visible lifetime.
                if(IsCamo)lastContact.ExpiresAt=Time.time+Mathf.Min(duration,profile.CamoContactMemory);
            }
        }
        public bool TryGetContact(out BossContact contact)
        {
            if(!IsAlive){contact=default;return false;}
            bool visible=!IsCamo&&Player!=null&&BossPerception.CanSee(Player.transform.position,Body.bounds);
            if(visible)PublishContact(true);
            else lastContact.Visible=false;
            contact=lastContact;
            return Time.time<=contact.ExpiresAt;
        }
        public bool GuardBlocksRay(Vector2 origin,Vector2 point)=>Actions.GuardBlocksRay(origin,point);
        public bool ReceiveProjectile(Projectile shot,Collider2D contact)
        {
            if(Actions.ReceiveProjectile(shot,contact))return true;
            if(contact==Body){projectileContact=contact.ClosestPoint(shot.transform.position);projectileContactAt=Time.time;}
            return false;
        }
        public void Trace(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(Diagnostics)Debug.Log($"[Boss {ActorId} {Time.time:0.00}] {message}");
#endif
        }
    }
}
