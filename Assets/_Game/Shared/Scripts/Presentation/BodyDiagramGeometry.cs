using UnityEngine;

// Shared authored contours; both current and historical screens use this geometry.
internal static class BodyDiagramGeometry
{
    internal static Vector2[] BodyContour(int index)
    {
        switch (index)
        {
            case 1: return new[] { new Vector2(0.3000000f, 0.5000000f), new Vector2(-0.3000000f, 0.5000000f), new Vector2(-0.5000000f, 0.3800000f), new Vector2(-0.3400000f, -0.2500000f), new Vector2(-0.2000000f, -0.5000000f), new Vector2(0.2000000f, -0.5000000f), new Vector2(0.3400000f, -0.2500000f), new Vector2(0.5000000f, 0.3800000f) };
            case 2: return new[] { new Vector2(0.5000000f, -0.2863248f), new Vector2(0.4621212f, -0.0299145f), new Vector2(-0.4356061f, 0.5000000f), new Vector2(-0.5000000f, 0.3888889f), new Vector2(-0.4280303f, 0.2179487f), new Vector2(0.3825758f, -0.5000000f) };
            case 3: return new[] { new Vector2(-0.5000000f, -0.2863248f), new Vector2(-0.4621212f, -0.0299145f), new Vector2(0.4356061f, 0.5000000f), new Vector2(0.5000000f, 0.3888889f), new Vector2(0.4280303f, 0.2179487f), new Vector2(-0.3825758f, -0.5000000f) };
            case 4: return new[] { new Vector2(0.2281553f, 0.5000000f), new Vector2(0.5000000f, 0.4850299f), new Vector2(0.0436893f, -0.4491018f), new Vector2(-0.3252427f, -0.5000000f), new Vector2(-0.5000000f, -0.4880240f), new Vector2(-0.1213592f, 0.0359281f) };
            case 5: return new[] { new Vector2(-0.2281553f, 0.5000000f), new Vector2(-0.5000000f, 0.4850299f), new Vector2(-0.0436893f, -0.4491018f), new Vector2(0.3252427f, -0.5000000f), new Vector2(0.5000000f, -0.4880240f), new Vector2(0.1213592f, 0.0359281f) };
            default: return null;
        }
    }
}
