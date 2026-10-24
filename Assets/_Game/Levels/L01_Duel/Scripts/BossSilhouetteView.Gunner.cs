using System.Collections.Generic;
using UnityEngine;

namespace GoGhost.Duel
{
    public sealed partial class BossSilhouetteView
    {
        static readonly Color GunCoreColor = new Color(1f, .94f, .84f, 1f);
        const int MaxGunBulletVisuals = 32;
        const float GunTrailLength = .55f;
        readonly List<GunBulletVisual> gunBullets = new List<GunBulletVisual>(MaxGunBulletVisuals);
        sealed class GunBulletVisual
        {
            public Projectile Shot;
            public SpriteRenderer Sprite;
            public Color OriginalColor;
            public LineRenderer Halo, Tail;
            public Vector2 Direction, Previous;
            public float Travel, Radius;
        }

        // View-owned, bounded renderer cache. Never changes the shared prefab, collider or projectile clock.
        public void Bullet(Projectile shot, Vector2 direction)
        {
            if (visualKind != BossKind.Refraction || shot == null) return;
            GunBulletVisual visual = null;
            foreach (var item in gunBullets)
                if (item.Shot == null) { visual = item; break; }
            if (visual == null)
            {
                if (gunBullets.Count >= MaxGunBulletVisuals) return;
                visual = new GunBulletVisual { Halo = NewLine("GunBulletHalo", true, 2), Tail = NewLine("GunBulletTail", true, 2) };
                gunBullets.Add(visual);
            }
            visual.Shot = shot;
            visual.Sprite = shot.GetComponent<SpriteRenderer>();
            if (visual.Sprite != null)
            {
                visual.OriginalColor = visual.Sprite.color;
                visual.Sprite.color = GunCoreColor;
                visual.Halo.sortingLayerID = visual.Tail.sortingLayerID = visual.Sprite.sortingLayerID;
                visual.Halo.sortingOrder = visual.Tail.sortingOrder = visual.Sprite.sortingOrder - 1;
            }
            visual.Direction = direction.normalized;
            visual.Previous = shot.transform.position;
            visual.Travel = 0;
            visual.Radius = shot.Radius;
            if (visual.Halo != null) visual.Halo.enabled = false;
            if (visual.Tail != null) visual.Tail.enabled = false;
        }

        static void ReleaseGunBullet(GunBulletVisual visual)
        {
            if (visual.Sprite != null && visual.Shot != null && !visual.Shot.Deflected)
                visual.Sprite.color = visual.OriginalColor;
            visual.Shot = null; visual.Sprite = null;
            if (visual.Halo != null) visual.Halo.enabled = false;
            if (visual.Tail != null) visual.Tail.enabled = false;
        }
        void ClearGunBullets()
        {
            foreach (var visual in gunBullets) ReleaseGunBullet(visual);
        }
        void UpdateGunBullets()
        {
            foreach (var visual in gunBullets)
            {
                if (visual.Shot == null || visual.Shot.Deflected || visual.Sprite != null && !visual.Sprite.enabled)
                { ReleaseGunBullet(visual); continue; }
                Vector2 at = visual.Shot.transform.position;
                visual.Travel = Mathf.Min(GunTrailLength, visual.Travel + Vector2.Distance(at, visual.Previous));
                visual.Previous = at;
                // Original sprite remains the collision-sized core; this narrow halo is cosmetic only.
                SetLine(visual.Halo, at - visual.Direction * .001f, at + visual.Direction * .001f,
                    visual.Radius * 2.4f, NeuralPalette.Alpha(bodyColor, .30f));
                SetLine(visual.Tail, at, at - visual.Direction * visual.Travel, visual.Radius * 1.1f,
                    NeuralPalette.Alpha(bodyColor, .65f));
                visual.Tail.endWidth = 0;
                visual.Tail.endColor = NeuralPalette.Alpha(bodyColor, 0);
                visual.Tail.enabled &= visual.Travel > .01f;
            }
        }

        void GunTell(int slot, string label, Vector2 from, Vector2 to, float width, Color color)
        {
            if (tells[slot] == null) tells[slot] = NewLine(label, false, 2);
            SetLine(tells[slot], from, to, width, color);
        }
        void GunTellStyle(bool locked, float progress, float lockedAge, out float width, out Color color)
        {
            // One short accent at commitment, then a stable locked line; never periodic flashing.
            float accent = locked ? Mathf.Clamp01(1 - lockedAge / .055f) : 0;
            width = locked ? .032f + .012f * accent : Mathf.Lerp(.018f, .023f, progress);
            color = Color.Lerp(bodyColor, GunCoreColor, locked ? .28f + .24f * accent : 0);
            color.a = locked ? .72f + .20f * accent : Mathf.Lerp(.24f, .43f, progress);
        }
        void GunnerTells(BossActionExecutor action)
        {
            if (action.Ability == BossAbility.Sweep)
            {
                // Once firing begins, real bullets replace the area preview, including future-ray tells.
                if (action.Phase != BossActionPhase.Prepare && action.Phase != BossActionPhase.Locked) return;
                bool locked = action.Phase == BossActionPhase.Locked;
                GunTellStyle(locked, action.PhaseProgress, action.PhaseProgress * actor.Profile.Sweep.Lock,
                    out float width, out Color color);
                GunTell(0, "SweepEdge", action.Origin, action.SweepFrom, width, color);
                GunTell(1, "SweepEdge", action.Origin, action.SweepTo, width, color);
                GunTell(2, "SweepBand", action.SweepFrom, action.SweepTo, .018f, NeuralPalette.Alpha(bodyColor, locked ? .32f : .22f));
                // A single restrained pass along the existing band communicates the authored sweep order.
                float t = locked ? 1 : action.PhaseProgress;
                GunTell(3, "SweepDirection", Vector2.Lerp(action.SweepFrom, action.SweepTo, Mathf.Max(0, t - .16f)),
                    Vector2.Lerp(action.SweepFrom, action.SweepTo, t), .032f, NeuralPalette.Alpha(bodyColor, .52f));
                return;
            }
            int slot = 0;
            foreach (var threat in action.PublicThreats)
            {
                if (!threat.Valid || (threat.Phase != BossThreatPhase.Preparation && threat.Phase != BossThreatPhase.Locked) || slot >= 6) continue;
                bool locked = threat.Phase == BossThreatPhase.Locked;
                bool burst = action.Ability == BossAbility.Burst || action.Ability == BossAbility.TacticalJumpShot;
                float lockDuration = burst ? actor.Profile.BurstShotLock : actor.Profile.Piercing.Lock;
                float lockedAge = Mathf.Max(0, lockDuration - (threat.StartsAt - Time.time));
                float progress = action.Phase == BossActionPhase.Prepare ? action.PhaseProgress
                    : 1 - Mathf.Clamp01((threat.StartsAt - Time.time) / actor.Profile.BurstStaggerPause);
                GunTellStyle(locked, progress, lockedAge, out float width, out Color color);
                GunTell(slot++, "GunAimTell", threat.Origin, threat.End, width, color);
            }
        }
    }
}
