using UnityEngine;

namespace GoGhost.Duel
{
    // Final Boss phase two (Thomas, 2026-11-06): the shockwave sets each platform bobbing one tier
    // (2.1) above and below its home height. A kinematic body driven by velocity pushes riders
    // physically and lets their grounding read the carrier's speed. Trial cycle: 4 s, adjacent
    // platforms in opposite phase. FixedUpdate stops while paused; the stage layout owns cleanup.
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlatformOscillator : MonoBehaviour
    {
        public const float Amplitude = DuelGame.PlatformTierRise, Period = 4f;
        Rigidbody2D body;
        Vector2 home;
        float phase, clock, rampRemaining;
        const float RampDuration = .4f;
        const float EdgeRestAlpha = .34f;
        SpriteRenderer empEdge;
        float contactAt;
        bool edgeSettled;

        public Vector2 Home => home;
        public float Offset => body != null ? body.position.y - home.y : 0;
        public float VerticalSpeed => body != null ? body.linearVelocity.y : 0;

        internal void PrimeEmp(float arrival)
        {
            var surface = GetComponent<SpriteRenderer>();
            empEdge = EmpOutline.Create(surface, surface.sharedMaterial, surface.sortingOrder - 1, .065f);
            contactAt = arrival;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (edgeSettled || empEdge == null) return;
            float age = Time.time - contactAt;
            float alpha = age < 0 ? 0 : Mathf.Lerp(.86f, EdgeRestAlpha, Mathf.SmoothStep(0, 1, age / .48f));
            empEdge.color = NeuralPalette.Alpha(NeuralPalette.Emp, alpha);
            edgeSettled = age >= .48f;
        }

        public void Begin(float phaseOffset, float startClock = 0, bool easeIn = false)
        {
            // Direct phase-two entry starts at the residual intensity without replaying the wave.
            if (empEdge == null) PrimeEmp(Time.time - 1f);
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
