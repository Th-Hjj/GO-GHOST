using UnityEngine;

namespace GoGhost.Duel
{
    // A delayed copy of publicly expressed body/weapon facts. Tactics never receive player controls.
    public struct BossObservation
    {
        public Bounds Body;
        public Vector2 Position, Velocity, Aim;
        public bool Visible, Grounded, ReloadVisible, Dodging;
        public float ObservedAt, Confidence, LastShotAt;
        public Vector2 Predict(float seconds) => Position + Velocity * Mathf.Min(seconds, .65f) * Confidence;
    }
    public sealed class BossPerception
    {
        readonly PlayerHealth player;
        readonly Collider2D shape;
        readonly Rigidbody2D body;
        readonly PlayerShooting rifle;
        readonly BossProfile profile;
        BossObservation current, pending;
        float readyAt, nextSample, lastShotAt=-100;
        Vector2 lastAim;
        bool hasPending;
        Vector2 pressureStart;
        float reactionAt;
        int comparable, retreatSign, missed;
        float pressureDistance, pressureHeight;
        public float LastDodgeObservedAt {get;private set;}=-100;
        public float DodgeStartedAt {get;private set;}=-100;
        public float BeforeDodgeVelocityX {get;private set;}
        public int LearnedRetreat => comparable>=2 && missed<2 ? retreatSign : 0;
        public BossObservation Facts => current;
        public BossPerception(PlayerHealth player, BossProfile profile)
        {
            this.player=player; this.profile=profile;
            shape=player.GetComponent<Collider2D>(); body=player.GetComponent<Rigidbody2D>();
            rifle=player.GetComponent<PlayerShooting>(); rifle.ShotFired+=OnShot;
        }
        public void Dispose() { if(rifle!=null) rifle.ShotFired-=OnShot; }
        void OnShot(Vector2 origin,Vector2 direction)
        { if(current.Visible) { lastShotAt=Time.time;lastAim=direction; } }
        public static bool CanSee(Vector2 observer, Bounds bounds)
        {
            for(int i=0;i<3;i++)
            {
                Vector2 p=(Vector2)bounds.center + Vector2.up*bounds.extents.y*(i==0?0:i==1?.7f:-.7f);
                if(!Projectile.TerrainHit(observer,p,.015f,out _)) return true;
            }
            return false;
        }
        public void Tick(Vector2 observer)
        {
            if(player==null || player.IsDead) {current.Confidence=0;return;}
            if(hasPending && Time.time>=readyAt)
            {
                // Learn only from the delayed public motion sample, never the player's action clock.
                if(pending.Dodging)
                {
                    if(!current.Dodging||pending.ObservedAt-current.ObservedAt>profile.SightMemory)
                        DodgeStartedAt=pending.ObservedAt;
                    LastDodgeObservedAt=pending.ObservedAt;
                }
                else BeforeDodgeVelocityX=pending.Velocity.x;
                current=pending;hasPending=false;
            }
            current.Confidence=Mathf.Clamp01(1-(Time.time-current.ObservedAt)/profile.SightMemory);
            if(Time.time>=nextSample && !hasPending)
            {
                nextSample=Time.time+profile.ReactionSeconds;
                bool visible=CanSee(observer,shape.bounds);
                current.Visible=visible && current.Confidence>0;
                if(visible)
                {
                    var hit=Physics2D.Raycast(shape.bounds.center,Vector2.down,shape.bounds.extents.y+.1f,LayerMask.GetMask("Ground"));
                    pending=new BossObservation {Body=shape.bounds,Position=shape.bounds.center,Velocity=body.linearVelocity,
                        Visible=true,Grounded=hit.collider!=null && Mathf.Abs(body.linearVelocity.y)<.25f,
                        ReloadVisible=rifle.ReloadVisible,Dodging=Mathf.Abs(body.linearVelocity.x)>12,
                        ObservedAt=Time.time,Confidence=1,LastShotAt=lastShotAt,Aim=lastAim};
                    readyAt=Time.time+profile.ReactionSeconds;hasPending=true;
                }
            }
            if(reactionAt>0 && Time.time>=reactionAt)
            {
                reactionAt=0;
                int sign=current.Visible && Mathf.Abs(current.Position.x-pressureStart.x)>.45f ? (int)Mathf.Sign(current.Position.x-pressureStart.x):0;
                if(sign!=0 && (retreatSign==0 || retreatSign==sign)) {retreatSign=sign;comparable++;missed=0;}
                else {missed++;if(missed>=2) comparable=0;}
            }
        }
        public void ExpressedPressure(Vector2 self)
        {
            if(!current.Visible || reactionAt>0)return;
            float distance=Mathf.Abs(current.Position.x-self.x), height=current.Position.y-self.y;
            if(Mathf.Abs(distance-pressureDistance)>3 || Mathf.Abs(height-pressureHeight)>1.2f) comparable=0;
            pressureDistance=distance;pressureHeight=height;pressureStart=current.Position;reactionAt=Time.time+.45f;
        }
    }
}
