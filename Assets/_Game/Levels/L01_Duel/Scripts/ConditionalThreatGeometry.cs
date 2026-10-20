using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>Current conditional threat queries from public Boss facts and the player's physical path.</summary>
    public sealed class ConditionalThreatGeometry
    {
        public const float SpineThreatWindow = .20f;
        private const float Step = .02f;
        private readonly PlayerHealth player;
        private readonly PlayerMovement movement;
        private readonly PlayerDodge dodge;
        private readonly Rigidbody2D body;
        private readonly Collider2D shape;
        private readonly IReadOnlyList<BossAgent> targets;
        private readonly ArenaNavigation2D navigation;
        private readonly List<ObservedTarget> contacts = new List<ObservedTarget>(2);
        private readonly List<BossThreat> threats = new List<BossThreat>(4);
        private readonly Transform combatRoot;
        private readonly List<Collider2D> supports = new List<Collider2D>();
        private readonly Vector2[] path = new Vector2[64];
        private Bounds selfBounds;
        private float gravity, leftEdge = -15f, rightEdge = 15f;
        private int sensedFrame = -1;

        private struct ObservedTarget
        {
            public BossAgent Actor;
            public BossContact Contact;
            public Bounds Bounds;
        }

        private struct Motion
        {
            public float Move, DodgeDirection, LandingX;
            public bool SteerLanding => !float.IsNaN(LandingX);
            public bool Jump;
            public Motion(float move, bool jump = false, float dodgeDirection = 0, float landingX = float.NaN)
            { Move = move; Jump = jump; DodgeDirection = dodgeDirection; LandingX = landingX; }
        }

        public ConditionalThreatGeometry(PlayerHealth player, IReadOnlyList<BossAgent> targets, Collider2D[] platforms)
        {
            this.player = player;
            movement = player.GetComponent<PlayerMovement>(); dodge = player.GetComponent<PlayerDodge>();
            body = player.GetComponent<Rigidbody2D>(); shape = player.GetComponent<Collider2D>();
            combatRoot = player.transform.parent;
            foreach (var collider in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (!collider.enabled || collider.isTrigger || collider.gameObject.layer != LayerMask.NameToLayer("Ground")) continue;
                var bounds = collider.bounds;
                if (bounds.size.x > bounds.size.y && bounds.size.x > 1f) supports.Add(collider);
                if (bounds.size.y > 6f && bounds.size.x < 2f)
                {
                    if (bounds.center.x < 0) leftEdge = Mathf.Max(leftEdge, bounds.max.x);
                    else rightEdge = Mathf.Min(rightEdge, bounds.min.x);
                }
            }
            if (platforms != null)
                foreach (var platform in platforms)
                    if (platform != null && !supports.Contains(platform)) supports.Add(platform);
            this.targets = targets;
            navigation = targets.Count > 0 ? targets[0].Navigation : null;
            // Threat prediction follows the active room, including rooms away from world origin.
            // The formal arena retains its existing -15 / 15 physical boundaries.
            if (navigation != null)
            { leftEdge = navigation.ArenaBounds.min.x; rightEdge = navigation.ArenaBounds.max.x; }
        }

        private void Sense()
        {
            if (sensedFrame == Time.frameCount) return;
            sensedFrame = Time.frameCount;
            selfBounds = shape.bounds;
            gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
            threats.Clear(); contacts.Clear();
            if (targets != null)
            {
                foreach (var actor in targets)
                {
                    if (actor == null || !actor.IsAlive) continue;
                    foreach (var expressed in actor.PublicThreats)
                        if (expressed.Valid && expressed.EndsAt >= Time.time) threats.Add(expressed);
                    if (!actor.TryGetContact(out var contact) || !contact.IsFresh) continue;
                    var observed = contact.Bounds;
                    // Cues expose their own position only; they do not imply exact hidden body velocity.
                    if (contact.VelocityKnown)
                        observed.center += (Vector3)(contact.Velocity * Mathf.Min(.35f, Mathf.Max(0, Time.time - contact.ObservedAt)));
                    contacts.Add(new ObservedTarget { Actor = actor, Contact = contact, Bounds = observed });
                }
            }
        }

        public bool ConditionalThreat(int kind,float move,bool jump,out Vector2 source,out int actorId,int requiredActorId=0)
        {
            source=default;actorId=0;
            // 41 retains the old Bullet storage key but now means Ranged. The retired Low
            // key remains invalid instead of silently selecting a different attack form.
            if(kind!=0&&kind!=41&&kind!=42)return false;
            Sense();
            int count=PredictPath(new Motion(move,jump,dodge.IsDodging?Mathf.Sign(body.linearVelocity.x):0),SpineThreatWindow);
            Vector2 half=selfBounds.extents;
            for(int i=1;i<count;i++)
            {
                float t=i*Step;
                if(kind!=42)
                    foreach(var shot in Projectile.Active)
                    {
                        if(shot==null||shot.OwnerLayer==player.gameObject.layer||!shot.transform.IsChildOf(combatRoot))continue;
                        var rb=shot.GetComponent<Rigidbody2D>();
                        if(rb==null||!rb.simulated)continue;
                        Vector2 a=rb.position+rb.linearVelocity*(t-Step),b=rb.position+rb.linearVelocity*t;
                        if(!SegmentBox(a-path[i-1],b-path[i],half+Vector2.one*shot.Radius))continue;
                        var owner=shot.Owner!=null?shot.Owner.GetComponentInParent<BossAgent>():null;
                        int candidateId=owner!=null?owner.ActorId:0;
                        if(requiredActorId!=0&&candidateId!=requiredActorId)continue;
                        source=rb.position;actorId=candidateId;return true;
                    }
                foreach(var expressed in threats)
                {
                    if(requiredActorId!=0&&expressed.ActorId!=requiredActorId)continue;
                    if(kind==41&&!expressed.IsRanged||kind==42&&expressed.IsRanged)continue;
                    if(!expressed.IntersectsMotion(path[i-1],path[i],half,Time.time+t-Step,Time.time+t))continue;
                    source=expressed.Origin;actorId=expressed.ActorId;return true;
                }
            }
            return false;
        }

        private float LandingSteer(float x,float from) => Mathf.Clamp((x-from)/(movement.MoveSpeed*Step),-1,1);

        private int PredictPath(Motion motion,float horizon)
        {
            int count=Mathf.Min(path.Length,Mathf.CeilToInt(horizon/Step)+1);
            Vector2 position=selfBounds.center;
            float vy=motion.Jump ? movement.JumpSpeed : body.linearVelocity.y;
            bool grounded=movement.IsGrounded && !motion.Jump;
            path[0]=position;
            for(int i=1;i<count;i++)
            {
                float t=i*Step;
                bool dashing=motion.DodgeDirection!=0 && t<=(dodge.IsDodging?dodge.MovementRemaining:dodge.Duration);
                float vx=dashing
                    ? (dodge.IsDodging?dodge.CommittedHorizontalSpeed:motion.DodgeDirection*dodge.Distance/dodge.Duration)
                    : (motion.SteerLanding?LandingSteer(motion.LandingX,position.x):motion.Move)*movement.MoveSpeed;
                Vector2 previous=position;
                position.x=Mathf.Clamp(position.x+vx*Step,leftEdge+selfBounds.extents.x+.01f,rightEdge-selfBounds.extents.x-.01f);
                if (targets != null)
                    foreach (var observed in contacts)
                    {
                        var opponent = observed.Bounds;
                        if (observed.Contact.VelocityKnown)
                            opponent.center += (Vector3)(observed.Contact.Velocity * Mathf.Min(t, SpineThreatWindow));
                        ClampAgainstBody(ref position, previous, opponent, observed.Actor.Body, dashing);
                    }
                if(grounded && !SupportedAt(position)) grounded=false;
                if(!grounded)
                {
                    vy-=gravity*Step; position.y+=vy*Step;
                    if(vy<=0)
                        foreach(var support in supports)
                        {
                            if(support==null || !support.enabled) continue;
                            var b=support.bounds;
                            if(position.x+selfBounds.extents.x*.5f<b.min.x || position.x-selfBounds.extents.x*.5f>b.max.x) continue;
                            if(previous.y-selfBounds.extents.y>=b.max.y-.03f && position.y-selfBounds.extents.y<=b.max.y)
                            { position.y=b.max.y+selfBounds.extents.y; vy=0; grounded=true; }
                        }
                }
                if(navigation!=null && !navigation.SweepTraversal(previous,position,selfBounds.size,out var terrain,out var safe))
                {
                    if(Mathf.Abs(terrain.normal.y)>.5f)
                    { position=safe; vy=0; grounded=terrain.normal.y>.5f; }
                    else position.x=safe.x;
                }
                path[i]=position;
            }
            return count;
        }

        private bool SupportedAt(Vector2 centre)
        {
            foreach(var support in supports)
                if(support!=null && support.enabled && Mathf.Abs(centre.y-selfBounds.extents.y-support.bounds.max.y)<.12f
                    && centre.x>support.bounds.min.x-selfBounds.extents.x*.5f && centre.x<support.bounds.max.x+selfBounds.extents.x*.5f) return true;
            return false;
        }

        private void ClampAgainstBody(ref Vector2 position, Vector2 previous, Bounds opponent, Collider2D other, bool dashing)
        {
            if (other == null || dashing && dodge.CanPassThroughBody(other) || Physics2D.GetIgnoreCollision(shape, other)
                || position.y - selfBounds.extents.y >= opponent.max.y || position.y + selfBounds.extents.y <= opponent.min.y) return;
            if (previous.x + selfBounds.extents.x <= opponent.min.x && position.x + selfBounds.extents.x > opponent.min.x)
                position.x = opponent.min.x - selfBounds.extents.x - .01f;
            if (previous.x - selfBounds.extents.x >= opponent.max.x && position.x - selfBounds.extents.x < opponent.max.x)
                position.x = opponent.max.x + selfBounds.extents.x + .01f;
        }

        private static bool SegmentBox(Vector2 from,Vector2 to,Vector2 half)
        {
            Vector2 delta=to-from;
            float first=0,last=1;
            for(int axis=0;axis<2;axis++)
            {
                if(Mathf.Abs(delta[axis])<.0001f) { if(Mathf.Abs(from[axis])>half[axis]) return false; continue; }
                float a=(-half[axis]-from[axis])/delta[axis],b=(half[axis]-from[axis])/delta[axis];
                if(a>b) { float swap=a; a=b; b=swap; }
                first=Mathf.Max(first,a); last=Mathf.Min(last,b);
                if(first>last) return false;
            }
            return true;
        }
    }
}
