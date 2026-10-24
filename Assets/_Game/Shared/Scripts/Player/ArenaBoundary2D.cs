using UnityEngine;

// Attempt-local geometry supplied by the level. Shared projectiles never depend on level types.
public sealed class ArenaBoundary2D : MonoBehaviour
{
    public Bounds Bounds { get; private set; }
    public void Initialize(Bounds bounds) => Bounds = bounds;

    public static bool Contains(Bounds bounds, Vector2 point, float inset = 0)
        => point.x >= bounds.min.x + inset && point.x <= bounds.max.x - inset
        && point.y >= bounds.min.y + inset && point.y <= bounds.max.y - inset;

    public static Vector2 ClampPoint(Bounds bounds, Vector2 point, float inset = 0)
        => new Vector2(Mathf.Clamp(point.x, bounds.min.x + inset, bounds.max.x - inset),
            Mathf.Clamp(point.y, bounds.min.y + inset, bounds.max.y - inset));

    // Distance along the original ray to first contact, including the body's radius.
    public static float ExitDistance(Bounds bounds, Vector2 origin, Vector2 direction, float inset = 0)
    {
        if (!Contains(bounds, origin, inset)) return 0;
        float distance = float.PositiveInfinity;
        if (Mathf.Abs(direction.x) > .000001f)
            distance = Mathf.Min(distance, ((direction.x > 0 ? bounds.max.x - inset : bounds.min.x + inset) - origin.x) / direction.x);
        if (Mathf.Abs(direction.y) > .000001f)
            distance = Mathf.Min(distance, ((direction.y > 0 ? bounds.max.y - inset : bounds.min.y + inset) - origin.y) / direction.y);
        return Mathf.Max(0, distance);
    }

    // Clip without rotating the ray or drawing an off-arena segment along the wall.
    public static bool ClipSegment(Bounds bounds, ref Vector2 from, ref Vector2 to, float inset = 0)
    {
        Vector2 delta = to - from;
        float enter = 0, exit = 1;
        if (!ClipAxis(from.x, delta.x, bounds.min.x + inset, bounds.max.x - inset, ref enter, ref exit)
            || !ClipAxis(from.y, delta.y, bounds.min.y + inset, bounds.max.y - inset, ref enter, ref exit)) return false;
        to = from + delta * exit;
        from += delta * enter;
        return true;
    }

    static bool ClipAxis(float origin, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < .000001f) return origin >= min && origin <= max;
        float a = (min - origin) / delta, b = (max - origin) / delta;
        enter = Mathf.Max(enter, Mathf.Min(a, b));
        exit = Mathf.Min(exit, Mathf.Max(a, b));
        return enter <= exit;
    }
}
