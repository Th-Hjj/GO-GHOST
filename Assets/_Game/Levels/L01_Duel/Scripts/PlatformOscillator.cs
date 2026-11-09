using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase two (Thomas, 2026-11-06): the shockwave sets each platform bobbing half a tier
    // (1.05) above and below its home height. A kinematic body driven by velocity pushes riders
    // physically and lets their grounding read the carrier's speed. Trial cycle: 4 s, adjacent
    // platforms in opposite phase. FixedUpdate stops while paused; the stage layout owns cleanup.
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlatformOscillator : MonoBehaviour
    {
        public const float Amplitude = DuelGame.PlatformTierRise * .5f, Period = 4f;
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

        float Target(float time) => home.y + Amplitude * Mathf.Sin(time / Period * Mathf.PI * 2 + phase);

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
