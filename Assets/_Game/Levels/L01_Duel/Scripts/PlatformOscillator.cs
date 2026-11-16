using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase two: the shockwave sets the platforms swinging half a tier (1.05) above and below
    // home over 4 s. They move as a wave from left to right (Thomas, 2026-11-15): each starts upward when
    // the one on its left is a quarter of the way up its first rise, .25 s later, and then keeps swinging.
    // Starting at the top of that rise (1 s) was tried first and did not read as one wave.
    // A platform waits at home until its turn. Sinking together by .6 was tried the same day and
    // rejected. A kinematic body driven by velocity pushes riders physically and lets their grounding read
    // the carrier's speed. FixedUpdate stops while paused; the stage layout owns cleanup.
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlatformOscillator : MonoBehaviour
    {
        public const float Amplitude = DuelGame.PlatformTierRise * .5f, Period = 4f;
        // How long after its left neighbour a platform starts.
        public const float WaveLag = Period / 16f;
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

        // A negative clock is the wait before this platform's turn in the wave.
        // The first moments are eased so a waiting platform does not jump to full speed.
        float Target(float time) => time < 0 ? home.y
            : home.y + Amplitude * Mathf.Sin(time / Period * Mathf.PI * 2 + phase) * Mathf.SmoothStep(0, 1, time / RampDuration);

        // A combatant can see the flight of a moving support, including its EMP start-up ramp.
        internal float DisplacementAfter(float seconds)
        {
            float c = clock, ramp = rampRemaining;
            Advance(ref c, ref ramp, seconds);
            return Target(c) - body.position.y;
        }

        // A waiting platform's clock runs at full speed until its turn; its own speed ramp starts then,
        // so every platform starts exactly WaveLag after its left neighbour and keeps that lag.
        static void Advance(ref float c, ref float ramp, float seconds)
        {
            if (ramp > 0 && c < 0) { float wait = Mathf.Min(seconds, -c); c += wait; seconds -= wait; }
            // Integrate the speed ramp, including a step crossing its end.
            float next = Mathf.Max(0, ramp - seconds);
            c += seconds - (ramp*ramp - next*next) / (2*RampDuration);
            ramp = next;
        }

        void FixedUpdate()
        {
            if (body == null) return;
            float dt = Time.fixedDeltaTime;
            Advance(ref clock, ref rampRemaining, dt);
            body.linearVelocity = new Vector2(0, (Target(clock) - body.position.y) / dt);
        }
    }
}
