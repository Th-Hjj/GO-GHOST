using UnityEngine;

// Visual timing only. The movement executors report the actual launch and contact.
internal struct JumpLandingPose
{
    private bool tookOff, landed, heavy;
    private float startedAt;

    public void TakeOff(float now)
    {
        tookOff = true;
        landed = false;
        startedAt = now;
    }

    public void Land(float now, float impactSpeed)
    {
        tookOff = false;
        landed = true;
        heavy = impactSpeed >= 14f;
        startedAt = now;
    }

    public float Compression(float now)
    {
        if (!landed) return 0;
        float t = Mathf.Max(0, now - startedAt);
        float down = heavy ? .080f : .055f;
        float recovery = heavy ? .235f : .135f;
        float weight = t < down ? Mathf.SmoothStep(0, 1, t / down)
            : Mathf.SmoothStep(1, 0, (t - down) / recovery);
        return weight * (heavy ? .31f : .16f);
    }

    public float Extension(float now) =>
        tookOff ? Mathf.SmoothStep(1, 0, (now - startedAt) / .15f) : 0;

    public static void ExtendFeet(ref Vector2 back, ref Vector2 front, float sole, float facing, float extension)
    {
        back = Vector2.Lerp(back, new Vector2(-.145f * facing, sole), extension);
        front.y = Mathf.Lerp(front.y, sole + .03f, extension);
    }

    public static Vector2 Knee(Vector2 hip, Vector2 foot, Vector2 restingBend, float facing,
        bool rear, float compression, float extension)
    {
        Vector2 delta = foot - hip;
        float length = rear ? .354f : .366f;
        float bend = Mathf.Sqrt(Mathf.Max(0, length * length - delta.sqrMagnitude * .25f));
        if (rear) bend *= 1 - .62f * extension;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * facing;
        float weight = Mathf.Max(extension, Mathf.Clamp01(compression / .16f));
        return (hip + foot) * .5f + Vector2.Lerp(restingBend, normal * bend, weight);
    }
}
