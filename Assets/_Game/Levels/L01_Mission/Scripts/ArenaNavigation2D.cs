using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>Small geometry-derived graph for this arena. Nodes are support points, not actor centres.</summary>
    public sealed class ArenaNavigation2D : MonoBehaviour
    {
        public enum TravelKind { Walk, Jump, Drop }
        public sealed class Node
        {
            public int Id, SurfaceId, Version;
            public Collider2D Collider;
            public Vector2 Position;
            public Vector2 Center(Vector2 size) => Position + Vector2.up * (size.y * .5f + .025f);
        }
        public sealed class RouteStep
        {
            public TravelKind Kind;
            public Vector2 Start, Launch, End;
            public Collider2D Support;
            public float Duration;
            internal int FromId, ToId;
        }
        public sealed class Route
        {
            public int Version;
            public readonly List<RouteStep> Steps = new List<RouteStep>();
            public float Duration;
        }
        sealed class Surface
        {
            public int Id;
            public Collider2D Collider;
            public Vector2 A, B;
            public readonly List<Node> Nodes = new List<Node>();
        }
        sealed class Geometry
        {
            public Collider2D Collider;
            public int Fingerprint;
            public Bounds Bounds;
            public readonly List<Surface> Surfaces = new List<Surface>();
        }
        sealed class Link
        {
            public RouteStep Step;
            public Bounds Corridor;
        }
        struct Key : IEquatable<Key>
        {
            public int From, To, Motion;
            public bool Equals(Key other) => From == other.From && To == other.To && Motion == other.Motion;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => From * 397 ^ To * 71 ^ Motion;
        }
        readonly Dictionary<int, Geometry> geometry = new Dictionary<int, Geometry>();
        readonly Dictionary<Key, Link> links = new Dictionary<Key, Link>();
        readonly Dictionary<long, float> blocked = new Dictionary<long, float>();
        readonly List<Node> nodes = new List<Node>();
        readonly List<Surface> surfaces = new List<Surface>();
        readonly HashSet<Type> warnedTypes = new HashSet<Type>();
        Bounds arena;
        float nextScan;
        int nextId = 1, groundMask;
        bool initialized;
        public int Version { get; private set; }
        public IReadOnlyList<Node> Nodes => nodes;
        public Bounds Bounds => arena;
        public Bounds ArenaBounds => arena;
        public float PlatformWidth { get; private set; }
        public float SameLevelSpacing { get; private set; }

        public void Initialize(Bounds legalBounds)
        {
            arena = legalBounds; groundMask = LayerMask.GetMask("Ground"); initialized = true;
            geometry.Clear(); links.Clear(); blocked.Clear(); nodes.Clear(); surfaces.Clear();
            Version = 0; nextId = 1; Refresh();
        }
        void Update()
        {
            if (initialized && Time.time >= nextScan) Refresh();
        }
        public void Refresh()
        {
            nextScan = Time.time + .5f;
            Physics2D.SyncTransforms(); // Static edits may precede the next FixedUpdate; sample their actual new shapes.
            var present = new HashSet<int>();
            var dirty = new List<Bounds>();
            var all = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include);
            foreach (var collider in all)
            {
                if (collider.gameObject.scene != gameObject.scene || collider.gameObject.layer != LayerMask.NameToLayer("Ground")
                    || collider.isTrigger || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                int id = collider.GetEntityId().GetHashCode(); present.Add(id);
                int fingerprint = Fingerprint(collider);
                if (geometry.TryGetValue(id, out var previous) && previous.Fingerprint == fingerprint) continue;
                if (previous != null) dirty.Add(previous.Bounds);
                var item = new Geometry { Collider = collider, Fingerprint = fingerprint, Bounds = collider.bounds };
                ReadSurfaces(item);
                geometry[id] = item; dirty.Add(item.Bounds);
            }
            var removed = new List<int>();
            foreach (var pair in geometry) if (!present.Contains(pair.Key)) { removed.Add(pair.Key); dirty.Add(pair.Value.Bounds); }
            foreach (int id in removed) geometry.Remove(id);
            if (dirty.Count == 0) return;
            Version++;
            nodes.Clear(); surfaces.Clear();
            var validIds = new HashSet<int>();
            foreach (var item in geometry.Values)
                foreach (var surface in item.Surfaces)
                {
                    surfaces.Add(surface);
                    foreach (var node in surface.Nodes) { node.Version = Version; nodes.Add(node); validIds.Add(node.Id); }
                }
            nodes.Sort((a, b) => a.Id.CompareTo(b.Id));
            ReadCombatScale();
            // Unchanged geometry keeps its nodes and link cache. Only corridors near changed geometry are rejected.
            var stale = new List<Key>();
            foreach (var pair in links)
            {
                if (!validIds.Contains(pair.Key.From) || !validIds.Contains(pair.Key.To)) { stale.Add(pair.Key); continue; }
                foreach (var bounds in dirty)
                    if (pair.Value.Corridor.Intersects(bounds)) { stale.Add(pair.Key); break; }
            }
            foreach (var key in stale) links.Remove(key);
            blocked.Clear();
        }
        void ReadCombatScale()
        {
            // Floating supports define the scale; floor/walls are not platform-spacing samples.
            PlatformWidth = SameLevelSpacing = float.PositiveInfinity;
            foreach (var a in surfaces)
            {
                if (!OneWay(a.Collider, out _) || Mathf.Abs(a.A.y-a.B.y) > .05f) continue;
                PlatformWidth = Mathf.Min(PlatformWidth, a.B.x-a.A.x);
                foreach (var b in surfaces)
                {
                    if (a == b || !OneWay(b.Collider, out _) || Mathf.Abs(a.A.y-b.A.y) > .05f) continue;
                    float spacing = Mathf.Abs((a.A.x+a.B.x-b.A.x-b.B.x)*.5f);
                    if (spacing > (a.B.x-a.A.x+b.B.x-b.A.x)*.5f)
                        SameLevelSpacing = Mathf.Min(SameLevelSpacing, spacing);
                }
            }
            if (float.IsInfinity(PlatformWidth)) PlatformWidth = 0;
            if (float.IsInfinity(SameLevelSpacing)) SameLevelSpacing = PlatformWidth;
        }
        public bool TryHorizontalAttack(Vector2 from, Vector2 target, Vector2 size, out Vector2 end)
        {
            end = from;
            if (SameLevelSpacing <= 0 || !TrySupport(from,size,out var source)) return false;
            float side = Mathf.Sign(target.x-from.x);
            float distance = Mathf.Min(SameLevelSpacing, Mathf.Abs(target.x-from.x)+size.x);
            Vector2 desired = from+Vector2.right*side*distance;
            // Cross-support dashes preserve the source's local offset on the corresponding platform.
            if (!TrySupport(desired,size,out var support) || support != source)
            {
                Vector2 full = from+Vector2.right*side*SameLevelSpacing;
                if (CanStand(full,size)) desired = full;
            }
            if (!TryDash(from,desired,size,out end)) return Vector2.Distance(from,end)>size.x;
            return CanStand(end,size); // An unblocked full dash must have a real landing support.
        }
        int Fingerprint(Collider2D collider)
        {
            unchecked
            {
                int value = collider.transform.localToWorldMatrix.GetHashCode() * 31 ^ collider.offset.GetHashCode();
                value = value * 31 ^ collider.usedByEffector.GetHashCode();
                var effector = collider.GetComponent<PlatformEffector2D>();
                if (effector != null) value ^= effector.useOneWay.GetHashCode() ^ effector.surfaceArc.GetHashCode() ^ effector.rotationalOffset.GetHashCode();
                if (collider is BoxCollider2D box) value = value * 31 ^ box.size.GetHashCode() ^ box.edgeRadius.GetHashCode();
                else if (collider is PolygonCollider2D polygon)
                    for (int i = 0; i < polygon.pathCount; i++) foreach (var point in polygon.GetPath(i)) value = value * 31 ^ point.GetHashCode();
                else if (collider is EdgeCollider2D edge)
                    foreach (var point in edge.points) value = value * 31 ^ point.GetHashCode();
                else if (collider is CompositeCollider2D composite)
                    for (int i = 0; i < composite.pathCount; i++)
                    { var points = new Vector2[composite.GetPathPointCount(i)]; composite.GetPath(i, points); foreach (var point in points) value = value * 31 ^ point.GetHashCode(); }
                return value;
            }
        }
        void ReadSurfaces(Geometry item)
        {
            var collider = item.Collider;
            if (collider is BoxCollider2D box)
            {
                var h = box.size * .5f;
                ReadPath(item, new[] { new Vector2(-h.x,-h.y), new Vector2(h.x,-h.y), new Vector2(h.x,h.y), new Vector2(-h.x,h.y) }, true);
            }
            else if (collider is PolygonCollider2D polygon)
                for (int i = 0; i < polygon.pathCount; i++) ReadPath(item, polygon.GetPath(i), true);
            else if (collider is EdgeCollider2D edge) ReadPath(item, edge.points, false);
            else if (collider is CompositeCollider2D composite)
                for (int i = 0; i < composite.pathCount; i++)
                {
                    var points = new Vector2[composite.GetPathPointCount(i)]; composite.GetPath(i, points);
                    ReadPath(item, points, composite.geometryType == CompositeCollider2D.GeometryType.Polygons);
                }
            else if (warnedTypes.Add(collider.GetType()))
                Debug.LogWarning($"Boss navigation cannot extract {collider.GetType().Name}; it remains a real obstacle, never a bounds-top platform.", collider);
        }
        void ReadPath(Geometry item, Vector2[] points, bool closed)
        {
            var collider = item.Collider;
            int count = closed ? points.Length : points.Length - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = collider.transform.TransformPoint(points[i] + collider.offset);
                Vector2 b = collider.transform.TransformPoint(points[(i + 1) % points.Length] + collider.offset);
                if (a.x > b.x) { var swap = a; a = b; b = swap; }
                Vector2 tangent = b - a;
                if (tangent.x < .25f || Mathf.Abs(tangent.y) > tangent.x * .7f) continue;
                Vector2 middle = (a + b) * .5f;
                if (middle.y < arena.min.y - .1f || middle.y >= arena.max.y - .1f) continue;
                // A real ray, above the actual edge, rejects undersides, holes and hidden interior edges.
                bool upward = false;
                foreach (var hit in Physics2D.RaycastAll(middle + Vector2.up * .06f, Vector2.down, .12f, groundMask))
                    if (hit.collider == collider && hit.distance > .01f && hit.normal.y > .7f) { upward = true; break; }
                if (!upward) continue;
                var surface = new Surface { Id = nextId++, Collider = collider, A = a, B = b };
                int samples = Mathf.Clamp(Mathf.CeilToInt(tangent.x / .65f), 1, 48);
                for (int j = 0; j <= samples; j++)
                {
                    Vector2 point = Vector2.Lerp(a, b, (j + .5f) / (samples + 1));
                    if (point.x < arena.min.x || point.x > arena.max.x) continue;
                    surface.Nodes.Add(new Node { Id = nextId++, SurfaceId = surface.Id, Collider = collider, Position = point });
                }
                if (surface.Nodes.Count > 0) item.Surfaces.Add(surface);
            }
        }
        static bool OneWay(Collider2D collider, out PlatformEffector2D effector)
        {
            effector = collider.usedByEffector ? collider.GetComponent<PlatformEffector2D>() : null;
            return effector != null && effector.enabled && effector.useOneWay;
        }
        public bool CanStand(Vector2 center, Vector2 size)
        {
            if (center.x - size.x * .5f < arena.min.x + .02f || center.x + size.x * .5f > arena.max.x - .02f
                || center.y + size.y * .5f > arena.max.y - .02f) return false;
            if (!TrySupport(center, size, out _)) return false;
            foreach (var collider in Physics2D.OverlapBoxAll(center + Vector2.up * .02f, size - Vector2.one * .055f, 0, groundMask))
                if (collider != null && !collider.isTrigger) return false;
            return true;
        }
        public bool TrySupport(Vector2 center, Vector2 size, out Collider2D support)
        {
            support = null;
            Vector2 foot = center - Vector2.up * (size.y * .5f);
            float best = float.PositiveInfinity;
            foreach (var hit in Physics2D.BoxCastAll(foot + Vector2.up * .055f,
                new Vector2(Mathf.Max(.04f, size.x - .10f), .035f), 0, Vector2.down, .115f, groundMask))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.normal.y < .7f || hit.distance >= best) continue;
                if (OneWay(hit.collider, out var effector) && Vector2.Dot(hit.normal, effector.transform.up) < Mathf.Cos(effector.surfaceArc * .5f * Mathf.Deg2Rad)) continue;
                support = hit.collider; best = hit.distance;
            }
            return support != null;
        }
        public bool SweepTraversal(Vector2 from, Vector2 to, Vector2 size, out RaycastHit2D blocker, out Vector2 safeEnd)
        {
            Vector2 delta = to - from; blocker = default; safeEnd = to;
            if (delta.sqrMagnitude < .0000001f) return true;
            float distance = delta.magnitude;
            foreach (var hit in Physics2D.BoxCastAll(from, size - Vector2.one * .035f, 0, delta / distance, distance, groundMask))
            {
                if (hit.collider == null || hit.collider.isTrigger) continue;
                if (OneWay(hit.collider, out var effector))
                {
                    if (delta.y >= -.0001f || hit.normal.y < .5f || from.y - size.y * .5f < hit.point.y - .045f) continue;
                    if (Vector2.Dot(hit.normal, effector.transform.up) < Mathf.Cos(effector.surfaceArc * .5f * Mathf.Deg2Rad)) continue;
                }
                // Resting floor contact must not stop a horizontal walk or a jump leaving it.
                if (hit.distance < .003f && hit.normal.y > .5f && delta.y >= -.001f) continue;
                if (hit.distance < .003f && Vector2.Dot(delta, hit.normal) >= 0) continue;
                blocker = hit;
                safeEnd = from + delta.normalized * Mathf.Max(0, hit.distance - .008f);
                return false;
            }
            return true;
        }
        public bool TryDash(Vector2 start, Vector2 end, Vector2 size, out Vector2 safeEnd)
        {
            var clipped = end;
            clipped.x = Mathf.Clamp(clipped.x, arena.min.x + size.x * .5f + .025f, arena.max.x - size.x * .5f - .025f);
            clipped.y = Mathf.Min(clipped.y, arena.max.y - size.y * .5f - .025f);
            bool clear = SweepTraversal(start, clipped, size, out _, out safeEnd);
            return clear && Vector2.Distance(clipped, end) < .005f;
        }
        public bool TryJumpRoute(Vector2 from, Vector2 destination, BossMotor2D motor, out Route route)
        {
            route = null;
            if (!CanStand(destination,motor.Size) || !TrySupport(destination,motor.Size,out var support)
                || !Simulate(from,destination,motor.Profile,motor.Size,true,support,out float flight)) return false;
            route = new Route { Version=Version, Duration=flight+.3f };
            route.Steps.Add(new RouteStep { Kind=TravelKind.Jump, Start=from, Launch=from,
                End=destination, Support=support, Duration=route.Duration });
            return true;
        }
        Surface SurfaceAt(Vector2 center, Vector2 size)
        {
            if (!TrySupport(center, size, out var support)) return null;
            float foot = center.y - size.y * .5f;
            Surface best = null; float error = .18f;
            foreach (var surface in surfaces)
            {
                if (surface.Collider != support || center.x < surface.A.x - .1f || center.x > surface.B.x + .1f) continue;
                float y = Mathf.Lerp(surface.A.y, surface.B.y, Mathf.InverseLerp(surface.A.x, surface.B.x, center.x));
                if (Mathf.Abs(y - foot) < error) { error = Mathf.Abs(y - foot); best = surface; }
            }
            return best;
        }
        public bool TryRoute(Vector2 from, Vector2 destination, BossMotor2D motor, out Route route)
        {
            route = null;
            if (!initialized || motor == null || motor.Profile == null) return false;
            Vector2 size = motor.Size;
            Surface startSurface = SurfaceAt(from, size), endSurface = SurfaceAt(destination, size);
            if (startSurface == null || endSurface == null || !CanStand(destination, size)) return false;
            var usable = new List<Node>();
            foreach (var node in nodes)
            {
                var surface = SurfaceById(node.SurfaceId);
                if (node.Position.x < surface.A.x + size.x * .5f + .055f || node.Position.x > surface.B.x - size.x * .5f - .055f) continue;
                if (CanStand(node.Center(size), size)) usable.Add(node);
            }
            if (usable.Count == 0) return false;
            int count = usable.Count;
            var cost = new float[count]; var previous = new int[count]; var incoming = new RouteStep[count]; var visited = new bool[count];
            for (int i = 0; i < count; i++)
            {
                cost[i] = float.PositiveInfinity; previous[i] = -1;
                if (usable[i].SurfaceId == startSurface.Id && WalkClear(from, usable[i].Center(size), size))
                    cost[i] = Vector2.Distance(from, usable[i].Center(size)) / motor.Profile.Speed;
            }
            int goal = -1;
            for (int iteration = 0; iteration < count; iteration++)
            {
                int current = -1; float cheapest = float.PositiveInfinity;
                for (int i = 0; i < count; i++) if (!visited[i] && cost[i] < cheapest) { current = i; cheapest = cost[i]; }
                if (current < 0) break;
                var source = usable[current]; visited[current] = true;
                if (source.SurfaceId == endSurface.Id && WalkClear(source.Center(size), destination, size)) { goal = current; break; }
                for (int j = 0; j < count; j++)
                {
                    if (visited[j] || j == current) continue;
                    var link = GetLink(source, usable[j], motor.Profile, size);
                    if (link == null || IsBlocked(source.Id, usable[j].Id)) continue;
                    float candidate = cost[current] + Mathf.Max(.02f, link.Duration);
                    if (candidate >= cost[j]) continue;
                    cost[j] = candidate; previous[j] = current; incoming[j] = link;
                }
            }
            if (goal < 0) return false;
            var reversed = new List<RouteStep>(); int cursor = goal;
            while (previous[cursor] >= 0) { reversed.Add(incoming[cursor]); cursor = previous[cursor]; }
            route = new Route { Version = Version };
            AddWalk(route, from, usable[cursor].Center(size), startSurface.Collider, motor.Profile.Speed);
            for (int i = reversed.Count - 1; i >= 0; i--) { route.Steps.Add(reversed[i]); route.Duration += reversed[i].Duration; }
            AddWalk(route, usable[goal].Center(size), destination, endSurface.Collider, motor.Profile.Speed);
            return true;
        }
        Surface SurfaceById(int id) { foreach (var surface in surfaces) if (surface.Id == id) return surface; return null; }
        void AddWalk(Route route, Vector2 from, Vector2 to, Collider2D support, float speed)
        {
            if (Vector2.Distance(from, to) < .05f) return;
            var step = new RouteStep { Kind = TravelKind.Walk, Start = from, Launch = from, End = to, Support = support,
                Duration = Vector2.Distance(from, to) / speed + .15f };
            route.Steps.Add(step); route.Duration += step.Duration;
        }
        bool WalkClear(Vector2 from, Vector2 to, Vector2 size)
        {
            if (!SweepTraversal(from, to, size, out _, out _)) return false;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / .3f));
            for (int i = 1; i <= steps; i++) if (!CanStand(Vector2.Lerp(from, to, i / (float)steps), size)) return false;
            return true;
        }
        RouteStep GetLink(Node from, Node to, BossMotionProfile profile, Vector2 size)
        {
            var key = new Key { From = from.Id, To = to.Id, Motion = profile.Fingerprint ^ size.GetHashCode() };
            if (links.TryGetValue(key, out var cached)) return cached.Step;
            var a = from.Center(size); var b = to.Center(size);
            float gravity = Mathf.Abs(Physics2D.gravity.y * profile.GravityScale);
            // Rigidbody2D applies gravity before displacement each fixed step. The analytic
            // parabola overestimates a jump by ~0.12 units at 12 / 29.43 / .02, enough to
            // invent a landing on a raised one-way platform that the full body never clears.
            float apex = ExecutableJumpHeight(profile);
            var corridor = new Bounds((a + b) * .5f, new Vector3(Mathf.Abs(a.x-b.x)+size.x+1,
                Mathf.Abs(a.y-b.y)+size.y+2*apex+1, 1));
            RouteStep step = null;
            if (from.SurfaceId == to.SurfaceId)
            {
                if (WalkClear(a, b, size)) step = new RouteStep { Kind = TravelKind.Walk, Start = a, Launch = a, End = b,
                    Support = to.Collider, Duration = Vector2.Distance(a,b) / profile.Speed + .1f };
            }
            else if (b.y-a.y <= apex-.04f)
            {
                float discriminant = profile.JumpSpeed * profile.JumpSpeed - 2*gravity*(b.y-a.y);
                float duration = discriminant >= 0 ? (profile.JumpSpeed + Mathf.Sqrt(discriminant))/gravity : 0;
                if (Mathf.Abs(b.x-a.x) < profile.Speed*duration && Simulate(a,b,profile,size, true, to.Collider, out float flight))
                    step = new RouteStep { Kind = TravelKind.Jump, Start = a, Launch = a, End = b, Support = to.Collider, Duration = flight+.3f };
                else if (b.y < a.y-.2f)
                {
                    var surface = SurfaceById(from.SurfaceId);
                    float direction = b.x >= a.x ? 1 : -1;
                    float edge = direction > 0 ? surface.B.x : surface.A.x;
                    var launch = new Vector2(edge+direction*(size.x*.5f+.06f), a.y);
                    if (Mathf.Abs(launch.x-a.x) < 1.6f && SimulateDrop(a,launch,b,profile,size,from.Collider,to.Collider,out flight))
                        step = new RouteStep { Kind = TravelKind.Drop, Start = a, Launch = launch, End = b,
                            Support = to.Collider, Duration = flight+.35f };
                }
            }
            if (step != null) { step.FromId = from.Id; step.ToId = to.Id; }
            links[key] = new Link { Step = step, Corridor = corridor };
            return step;
        }
        public static float ExecutableJumpHeight(BossMotionProfile profile)
        {
            float dt = Time.fixedDeltaTime, gravity = Mathf.Abs(Physics2D.gravity.y * profile.GravityScale);
            if (dt <= 0 || gravity <= 0) return 0;
            float velocity = profile.JumpSpeed, height = 0, peak = 0;
            while (velocity > 0)
            {
                velocity -= gravity * dt;
                height += velocity * dt;
                peak = Mathf.Max(peak, height);
            }
            return peak;
        }
        public bool TryJumpTrajectory(Vector2 from, Vector2 destination, BossMotor2D motor, out Route route, out Vector2[] trajectory)
            => TryJumpTrajectory(from,destination,motor.Profile,motor.Size,out route,out trajectory);
        public bool TryJumpTrajectory(Vector2 from, Vector2 destination, BossMotionProfile motion, Vector2 size, out Route route, out Vector2[] trajectory)
        {
            route = null; trajectory = null;
            var samples = new List<Vector2>();
            if (!CanStand(destination,size) || !TrySupport(destination,size,out var support)
                || !Simulate(from,destination,motion,size,true,support,out float flight,samples)) return false;
            route = new Route { Version=Version, Duration=flight+.3f };
            route.Steps.Add(new RouteStep { Kind=TravelKind.Jump, Start=from, Launch=from,
                End=destination, Support=support, Duration=flight+.3f });
            trajectory = samples.ToArray(); return true;
        }
        public Vector2[] PredictAirPath(Vector2 point, Vector2 velocity, Vector2 destination,
            BossMotionProfile profile, Vector2 size, float seconds)
        {
            float dt=Time.fixedDeltaTime;
            var points=new Vector2[Mathf.CeilToInt(seconds/dt)+1]; points[0]=point;
            bool stopped=false;
            for(int i=1;i<points.Length;i++)
            {
                if(!stopped)
                {
                    velocity.x=BossMotor2D.StepHorizontal(velocity.x,BossMotor2D.JumpHorizontalTarget(destination.x-point.x,profile,dt),false,profile,dt);
                    velocity.y+=Physics2D.gravity.y*profile.GravityScale*dt;
                    Vector2 next=point+velocity*dt;
                    if(!SweepTraversal(point,next,size,out var hit,out var safe))
                    {
                        point=safe;
                        if(hit.normal.y>.5f&&velocity.y<=0)stopped=true;
                        else if(Mathf.Abs(hit.normal.x)>.5f)velocity.x=0;
                        else velocity.y=0;
                    }
                    else point=next;
                }
                points[i]=point;
            }
            return points;
        }
        bool Simulate(Vector2 start, Vector2 end, BossMotionProfile profile, Vector2 size, bool jump, Collider2D destination, out float duration, List<Vector2> samples=null)
        {
            duration = 0;
            float dt = Time.fixedDeltaTime, gravity = Physics2D.gravity.y*profile.GravityScale;
            Vector2 point = start;
            samples?.Add(point);
            float vx = 0, vy = jump ? profile.JumpSpeed : 0;
            int max = Mathf.CeilToInt(2.5f/dt);
            for (int i = 0; i < max; i++)
            {
                float desired = BossMotor2D.JumpHorizontalTarget(end.x-point.x,profile,dt);
                vx = BossMotor2D.StepHorizontal(vx,desired,false,profile,dt); vy += gravity*dt;
                Vector2 next = point + new Vector2(vx,vy)*dt;
                if (!SweepTraversal(point,next,size,out var hit,out var safe))
                {
                    if (vy <= 0 && hit.normal.y > .7f)
                    {
                        duration = (i+1)*dt;
                        samples?.Add(safe);
                        return hit.collider == destination && Mathf.Abs(safe.x-end.x) < .30f
                            && Mathf.Abs(safe.y-end.y) < .12f && CanStand(end,size);
                    }
                    return false;
                }
                if (next.x-size.x*.5f < arena.min.x || next.x+size.x*.5f > arena.max.x || next.y-size.y*.5f < arena.min.y-.1f) return false;
                point = next;
                samples?.Add(point);
            }
            return false;
        }
        bool SimulateDrop(Vector2 start, Vector2 launch, Vector2 end, BossMotionProfile profile, Vector2 size,
            Collider2D source, Collider2D destination, out float duration)
        {
            duration = 0;
            float dt = Time.fixedDeltaTime, gravity = Physics2D.gravity.y*profile.GravityScale;
            Vector2 point = start;
            float vx = 0, vy = 0;
            bool airborne = false;
            for (int i = 0; i < Mathf.CeilToInt(3f/dt); i++)
            {
                Vector2 target = airborne ? end : launch;
                float desired = Mathf.Clamp((target.x-point.x)/dt,-profile.Speed,profile.Speed);
                vx = BossMotor2D.StepHorizontal(vx,desired,!airborne,profile,dt);
                Vector2 next = point + Vector2.right*(vx*dt);
                if (!airborne && (!TrySupport(next,size,out var standing) || standing != source)) airborne = true;
                if (airborne) { vy += gravity*dt; next.y += vy*dt; }
                if (!SweepTraversal(point,next,size,out var hit,out var safe))
                {
                    if (airborne && vy < 0 && hit.normal.y > .7f)
                    {
                        duration = (i+1)*dt;
                        return hit.collider == destination && Mathf.Abs(safe.x-end.x)<.30f
                            && Mathf.Abs(safe.y-end.y)<.12f && CanStand(end,size);
                    }
                    return false;
                }
                if (next.x-size.x*.5f<arena.min.x || next.x+size.x*.5f>arena.max.x || next.y-size.y*.5f<arena.min.y-.1f) return false;
                point = next;
            }
            return false;
        }
        public bool ValidateStep(RouteStep step, BossMotor2D motor)
        {
            if (step == null || step.Support == null || !step.Support.enabled || !step.Support.gameObject.activeInHierarchy) return false;
            if (step.Kind == TravelKind.Walk) return WalkClear(motor.Position,step.End,motor.Size);
            if (step.Kind == TravelKind.Jump) return Simulate(motor.Position,step.End,motor.Profile,motor.Size,true,step.Support,out _);
            if (!TrySupport(motor.Position,motor.Size,out var source)) return false;
            return SimulateDrop(motor.Position,step.Launch,step.End,motor.Profile,motor.Size,source,step.Support,out _);
        }
        static long Pair(int from, int to) => ((long)from<<32) | (uint)to;
        bool IsBlocked(int from,int to) => blocked.TryGetValue(Pair(from,to),out float until) && Time.time < until;
        public void BlockTemporarily(RouteStep step)
        {
            if (step != null && step.FromId != 0) blocked[Pair(step.FromId,step.ToId)] = Time.time+1.2f;
        }
    }
}
