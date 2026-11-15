using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase two: the shockwave sets the platforms bobbing. Since 2026-11-15 they all sink
    // together, .6 below home and back, every 3 s. They never rise above home and keep their distance
    // from each other, so every jump that works on the still tower works at any moment. Before that each
    // swung 1.05 above and below home over 4 s, opposite to its neighbours, which closed every link
    // about half the time. A kinematic body driven by velocity pushes riders physically and lets their
    // grounding read the carrier's speed. FixedUpdate stops while paused; the stage layout owns cleanup.
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlatformOscillator : MonoBehaviour
    {
        public const float Amplitude = .3f, Period = 3f;
        Rigidbody2D body;
        Vector2 home;
        float phase, clock, rampRemaining;
        const float RampDuration = .4f;

        public Vector2 Home => home;
        public float Offset => body != null ? body.position.y - home.y : 0;
        public float VerticalSpeed => body != null ? body.linearVelocity.y : 0;

        public void Begin(float phaseOffset, float startClock = 0, bool easeIn = false)
        {
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            home = body.position; phase = phaseOffset; clock = startClock;
            rampRemaining = easeIn ? RampDuration : 0;
            body.position = new Vector2(home.x, Target(clock));
        }

        // Starts at home with no speed and only ever goes below it.
        float Target(float time) => home.y - Amplitude * (1 - Mathf.Cos(time / Period * Mathf.PI * 2 + phase));

        // A combatant can see the flight of a moving support, including its EMP start-up ramp.
        internal float DisplacementAfter(float seconds)
        {
            float remaining = Mathf.Max(0, rampRemaining - seconds);
            float advance = seconds - (rampRemaining*rampRemaining - remaining*remaining) / (2*RampDuration);
            return Target(clock + advance) - body.position.y;
        }

        void FixedUpdate()
        {
            if (body == null) return;
            float dt = Time.fixedDeltaTime;
            // Integrate the speed ramp, including a fixed step crossing its end.
            float nextRemaining = Mathf.Max(0, rampRemaining-dt);
            clock += dt - (rampRemaining*rampRemaining-nextRemaining*nextRemaining)/(2*RampDuration);
            rampRemaining = nextRemaining;
            body.linearVelocity = new Vector2(0, (Target(clock) - body.position.y) / dt);
        }
    }
}
