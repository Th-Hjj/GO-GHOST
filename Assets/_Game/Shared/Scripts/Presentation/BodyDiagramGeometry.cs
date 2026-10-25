using UnityEngine;

// Shared authored contours; both current and historical screens use this geometry.
internal static class BodyDiagramGeometry
{
    // Configure Your Body uses a narrower, relaxed pose. Historical callers keep BodyContour.
    internal static Vector2[] ConfigurationContour(int index)
    {
        Vector2[] points;
        if(index==0)return null;
        if(index==1)
            return new[] { new Vector2(.30f,.50f),new Vector2(-.30f,.50f),new Vector2(-.50f,.38f),new Vector2(-.34f,-.25f),new Vector2(-.26f,-.50f),new Vector2(.26f,-.50f),new Vector2(.34f,-.25f),new Vector2(.50f,.38f) };
        if(index<4)
            // The root shares the torso's upper side edge; the outer cap continues its shoulder slope.
            points=new[] { new Vector2(.53125f,.4230769f),new Vector2(.203125f,.3538462f),new Vector2(-.03125f,.2692308f),new Vector2(-.50f,-.44f),new Vector2(-.34f,-.50f),new Vector2(-.13f,-.48f),new Vector2(.11f,0),new Vector2(.61875f,.3019231f) };
        else
            points=new[] { new Vector2(-.10f,.50f),new Vector2(.50f,.485f),new Vector2(.044f,-.449f),new Vector2(-.325f,-.50f),new Vector2(-.50f,-.488f),new Vector2(-.32f,.036f) };
        if(index==3||index==5)
            for(int i=0;i<points.Length;i++)points[i].x=-points[i].x;
        return points;
    }

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
