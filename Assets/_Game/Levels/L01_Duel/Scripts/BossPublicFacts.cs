using UnityEngine;

namespace GoGhost.Duel
{
    // A copied, public observation. A concealed actor republishes only when a visible cue occurs.
    public struct BossContact
    {
        public int ActorId;
        public int PreparationId;
        public Bounds Bounds;
        public Vector2 Velocity;
        public bool VelocityKnown, Visible, Dynamic;
        public bool GroundedKnown, Grounded, Preparing, Recovering, Reloading;
        public float HealthFraction;
        public float Gravity, ObservedAt, ExpiresAt;
        public bool IsFresh => ExpiresAt > Time.time;
    }

    public enum BossThreatPhase { Preparation, Locked, Active, Recovery }
    public enum BossThreatShape { Ray, SweptCapsule, Box, Projectile, BladeArc, FallingBlade, MovingBlade, StationaryBlade }

    // Scaled absolute times and the same expressed geometry used by the attack and tell.
    // Ray End is already clipped by the emitting skill against the nearest blocking ally body.
    public struct BossThreat
    {
        public int ActorId, AttackId;
        public BossThreatPhase Phase;
        public BossThreatShape Shape;
        public Vector2 Origin, Direction, End;
        public Bounds Area;
        public float Radius, StartsAt, EndsAt, ProjectileSpeed;
        public float ArcFrom, ArcTo, Facing, Reach, Gravity;
        public bool TerrainBlocked, Valid;
        public bool HasLandingX;
        public float LandingX;
        // Classification follows the expressed attack form, never target distance.
        public bool IsRanged=>Shape==BossThreatShape.Projectile||Shape==BossThreatShape.Ray||Shape==BossThreatShape.MovingBlade;
        // Copied executable jump samples. They never read a live target during prediction.
        public Vector2[] OriginPath;
        public float PathStartsAt, PathStep;
        public Vector2 OriginAt(float time)
        {
            if(OriginPath==null||OriginPath.Length==0)return Origin;
            float frame=Mathf.Clamp((time-PathStartsAt)/Mathf.Max(.001f,PathStep),0,OriginPath.Length-1);
            int i=Mathf.FloorToInt(frame);
            return Vector2.Lerp(OriginPath[i],OriginPath[Mathf.Min(i+1,OriginPath.Length-1)],frame-i);
        }

        public bool IntersectsMotion(Vector2 previous, Vector2 current, Vector2 half,
            float fromTime, float toTime)
        {
            if (!Valid || Phase == BossThreatPhase.Recovery || toTime < StartsAt || fromTime > EndsAt) return false;
            float first = Mathf.Max(StartsAt, fromTime), last = Mathf.Min(EndsAt, toTime);
            float interval = Mathf.Max(.00001f, toTime - fromTime);
            Vector2 aPlayer = Vector2.Lerp(previous, current, (first - fromTime) / interval);
            Vector2 bPlayer = Vector2.Lerp(previous, current, (last - fromTime) / interval);
            Vector2 expanded = half + Vector2.one * Mathf.Max(0, Radius);
            if(Shape==BossThreatShape.MovingBlade)
            {
                Vector2 v=Direction.normalized*ProjectileSpeed;
                Vector2 a=Origin+v*Mathf.Max(0,first-StartsAt)-aPlayer;
                Vector2 b=End+v*Mathf.Max(0,first-StartsAt)-aPlayer;
                Vector2 travel=v*(last-first)-(bPlayer-aPlayer);
                // Swept finite blade against the expanded player box (four-axis SAT).
                return OverlapAxis(a,b,travel,expanded,Vector2.right)
                    &&OverlapAxis(a,b,travel,expanded,Vector2.up)
                    &&OverlapAxis(a,b,travel,expanded,new Vector2(-(b-a).y,(b-a).x))
                    &&OverlapAxis(a,b,travel,expanded,new Vector2(-travel.y,travel.x));
            }
            if (Shape == BossThreatShape.BladeArc || Shape == BossThreatShape.FallingBlade)
            {
                // Bounded samples within this .02 s prediction step use the executable arc/descent.
                for (int sample=0;sample<=4;sample++)
                {
                    float t=Mathf.Lerp(first,last,sample/4f), elapsed=Mathf.Max(0,t-StartsAt);
                    Vector2 centre=Vector2.Lerp(aPlayer,bPlayer,sample/4f), root=Origin, tip;
                    if(Shape==BossThreatShape.BladeArc)
                    {
                        root=OriginAt(t);
                        float angle=Mathf.Lerp(ArcFrom,ArcTo,elapsed/Mathf.Max(.001f,EndsAt-StartsAt))*Mathf.Deg2Rad;
                        tip=root+new Vector2(Facing*Mathf.Cos(angle),Mathf.Sin(angle))*Reach;
                    }
                    else
                    {
                        root+=Direction*elapsed+Vector2.up*(Gravity*elapsed*elapsed*.5f);
                        if(HasLandingX)root.x=Mathf.Clamp(root.x,Mathf.Min(Origin.x,LandingX),Mathf.Max(Origin.x,LandingX));
                        tip=root+Vector2.down*Reach;
                    }
                    if(TerrainBlocked&&Projectile.TerrainHit(root,tip,Radius,out var hit))tip=root+(tip-root).normalized*hit.distance;
                    if(SegmentBox(root-centre,tip-centre,expanded))return true;
                }
                return false;
            }
            if (Shape == BossThreatShape.Box)
            {
                if (!SegmentBox(aPlayer - (Vector2)Area.center, bPlayer - (Vector2)Area.center,
                    half + (Vector2)Area.extents)) return false;
                return !TerrainBlocked || !Projectile.TerrainHit(Origin, aPlayer, 0, out _)
                    || !Projectile.TerrainHit(Origin, bPlayer, 0, out _);
            }
            if (Shape == BossThreatShape.SweptCapsule || Shape == BossThreatShape.Projectile)
            {
                Vector2 a, b;
                if (Shape == BossThreatShape.Projectile)
                {
                    a = Origin + Direction.normalized * ProjectileSpeed * Mathf.Max(0, first - StartsAt);
                    b = Origin + Direction.normalized * ProjectileSpeed * Mathf.Max(0, last - StartsAt);
                    if ((b - Origin).sqrMagnitude > (End - Origin).sqrMagnitude) b = End;
                    if ((a - Origin).sqrMagnitude > (End - Origin).sqrMagnitude) return false;
                }
                else
                {
                    float duration = Mathf.Max(.00001f, EndsAt - StartsAt);
                    a = Vector2.Lerp(Origin, End, (first - StartsAt) / duration);
                    b = Vector2.Lerp(Origin, End, (last - StartsAt) / duration);
                }
                if (TerrainBlocked && Projectile.TerrainHit(Origin, b, Radius, out var wall))
                {
                    if (wall.distance <= Vector2.Distance(Origin, a)) return false;
                    b = Origin + (b - Origin).normalized * wall.distance;
                }
                return SegmentBox(a - aPlayer, b - bPlayer, expanded);
            }
            Vector2 end = End;
            if (TerrainBlocked && Projectile.TerrainHit(Origin, end, Radius, out var blocker))
                end = Origin + (end - Origin).normalized * blocker.distance;
            // A locked beam occupies its line throughout its actual active window.
            // Relative motion is sampled at both ends and its midpoint over one .02 s predictor step.
            return SegmentBox(Origin - aPlayer, end - aPlayer, expanded)
                || SegmentBox(Origin - bPlayer, end - bPlayer, expanded)
                || SegmentBox(Origin - (aPlayer + bPlayer) * .5f, end - (aPlayer + bPlayer) * .5f, expanded);
        }

        static bool OverlapAxis(Vector2 a,Vector2 b,Vector2 travel,Vector2 half,Vector2 axis)
        {
            if(axis.sqrMagnitude<.000001f)return true;
            float x=Vector2.Dot(a,axis),y=Vector2.Dot(b,axis),d=Vector2.Dot(travel,axis);
            float lo=Mathf.Min(x,y)+Mathf.Min(0,d),hi=Mathf.Max(x,y)+Mathf.Max(0,d);
            float extent=Mathf.Abs(axis.x)*half.x+Mathf.Abs(axis.y)*half.y;
            return lo<=extent&&hi>=-extent;
        }

        static bool SegmentBox(Vector2 from, Vector2 to, Vector2 half)
        {
            Vector2 delta = to - from;
            float first = 0, last = 1;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(delta[axis]) < .00001f)
                { if (Mathf.Abs(from[axis]) > half[axis]) return false; continue; }
                float a = (-half[axis] - from[axis]) / delta[axis], b = (half[axis] - from[axis]) / delta[axis];
                if (a > b) { float swap = a; a = b; b = swap; }
                first = Mathf.Max(first, a); last = Mathf.Min(last, b);
                if (first > last) return false;
            }
            return true;
        }
    }
}
