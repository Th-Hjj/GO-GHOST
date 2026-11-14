using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>
    /// What the player's own body shows: lost health and the Dodge immunity window.
    /// Colours and alpha only; PlayerHealth and PlayerDodge remain the gameplay authority.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class PlayerFeedbackView : MonoBehaviour
    {
        // One step of pure white on the 12-per-second effect clock, then Danger fading back.
        const float WhiteSeconds = 1f / 12f, HurtSeconds = .15f, HurtTint = .9f;
        const float GhostAlpha = .72f, EchoAlpha = .16f, EchoSeconds = .14f, EchoInterval = .07f;
        PlayerHealth health;
        PlayerDodge dodge;
        AttemptPresentationVfx effects;
        readonly List<SpriteRenderer> bodyParts = new List<SpriteRenderer>(12), weaponParts = new List<SpriteRenderer>(3),
            allParts = new List<SpriteRenderer>(15);
        float hurtAt = float.NegativeInfinity, nextEcho;
        bool shown;

        public void Initialize(PlayerHealth player)
        {
            health = player;
            dodge = player.GetComponent<PlayerDodge>();
            effects = player.GetComponentInParent<AttemptPresentationVfx>();
            var visual = player.GetComponent<OfficerVisual>();
            if (visual != null)
            {
                foreach (var part in visual.BodySegments()) if (part != null) bodyParts.Add(part);
                foreach (var part in visual.WeaponSegments()) if (part != null) weaponParts.Add(part);
            }
            allParts.AddRange(bodyParts);
            allParts.AddRange(weaponParts);
            health.DamageTaken += OnDamageTaken;
        }

        void OnDamageTaken(int damage, GameObject source)
        {
            if (damage <= 0) return;
            hurtAt = Time.time;
        }

        void LateUpdate()
        {
            if (health == null) return;
            float sinceHit = Time.time - hurtAt;
            bool white = sinceHit < WhiteSeconds && !GameSettings.ReduceFlashing;
            float hurt = Mathf.Clamp01(1 - (sinceHit - WhiteSeconds) / HurtSeconds);
            bool ghost = dodge != null && dodge.ImmunityActive && !health.IsDead;
            if (ghost && effects != null && Time.time >= nextEcho)
            {
                nextEcho = Time.time + EchoInterval;
                effects.Echo(allParts, NeuralPalette.Alpha(NeuralPalette.Primary, EchoAlpha), EchoSeconds);
            }
            if (!shown && hurt <= 0 && !ghost) return;
            shown = hurt > 0 || ghost;
            // The neutral body is Primary everywhere; rule colours are added by its shader, on top of this tint.
            Color color = white ? Color.white : Color.Lerp(NeuralPalette.Primary, NeuralPalette.Danger, hurt * HurtTint);
            color.a = ghost ? GhostAlpha : 1;
            foreach (var part in bodyParts) if (part != null) part.color = color;
            foreach (var part in weaponParts)
            {
                if (part == null) continue;
                Color weapon = part.color;
                weapon.a = color.a;
                part.color = weapon;
            }
        }

        void OnDestroy()
        {
            if (health != null) health.DamageTaken -= OnDamageTaken;
        }
    }
}
