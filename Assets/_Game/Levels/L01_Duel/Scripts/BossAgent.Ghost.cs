using UnityEngine;

namespace GoGhost.Duel
{
    // GHOST's body (Thomas, 2026-11-14): the player's own operations and numbers on a Boss actor.
    // Move 7, jump 12, gravity 3; a six-round revolver, 20 a shot, .45 s apart, 1.8 s reload, bullets at 18;
    // Dodge 3 units in .18 s with .20 s of immunity and a 1.7 s cooldown. No attack tells: the player has none.
    // Stage 3 gives him the body and plain behaviour. His real AI is stage 4.
    public sealed partial class BossAgent
    {
        public bool IsGhost { get; private set; }
        public const int GhostHealth = 150, GhostMagazine = 6, GhostShotDamage = 20;
        const float GhostShotInterval = .45f, GhostReload = 1.8f, GhostBulletSpeed = 18;
        const float GhostDodgeDistance = 3, GhostDodgeSeconds = .18f, GhostImmunity = .20f, GhostDodgeCooldown = 1.7f;

        int ghostAmmo;
        float ghostShotAt, ghostReloadUntil, ghostDodgeReadyAt, ghostImmuneUntil, ghostJumpAt, ghostLookAt;
        public bool GhostReloading => IsGhost && Time.time < ghostReloadUntil;
        public bool GhostImmune => IsGhost && Time.time < ghostImmuneUntil;

        OfficerVisual.Puppet ghostPuppet;
        GameObject ghostFigure;
        Material ghostMaterial;
        int ghostShots;
        Vector2 ghostAim = Vector2.left;

        // GHOST looks like the player because he is drawn by the player's own body view: a copy of the player's
        // visual with every gameplay component removed, written to each frame from this actor.
        void BuildGhostFigure(OfficerVisual bodyPrefab)
        {
            if (bodyPrefab == null) return;
            var box = bodyPrefab.GetComponent<BoxCollider2D>();
            float soles = box != null ? box.offset.y - box.size.y * .5f : -.75f;
            var weaponPart = bodyPrefab.Part(LoadoutSlot.Weapon);
            float reach = weaponPart != null && weaponPart.transform.parent != null
                ? ((Vector2)weaponPart.transform.parent.localPosition).magnitude : .51f;
            // Built inactive, so nothing on the copy wakes before it is stripped.
            var holder = new GameObject("GHOST figure holder");
            holder.SetActive(false);
            ghostFigure = Instantiate(bodyPrefab.gameObject, holder.transform);
            ghostFigure.name = "GHOST figure";
            StripToVisual(ghostFigure);
            ghostFigure.transform.SetParent(transform, false);
            // The copy's soles on this body's soles.
            ghostFigure.transform.localPosition = new Vector3(0, Body.bounds.min.y - transform.position.y - soles, 0);
            ghostFigure.transform.localRotation = Quaternion.identity;
            Destroy(holder);
            ghostMaterial = new Material(Resources.Load<Shader>("GhostProjection")) { hideFlags = HideFlags.HideAndDontSave };
            // The rows alone on the body; no travelling band.
            ghostMaterial.SetFloat("_Low", -1000); ghostMaterial.SetFloat("_High", -1000);
            ghostPuppet = new OfficerVisual.Puppet { Floor = soles, Reach = reach + .16f, Facing = -1, Aim = Vector2.left };
            ghostFigure.GetComponent<OfficerVisual>().ConfigurePuppet(ghostPuppet, GhostHologram.Body, GhostHologram.Gun,
                GhostHologram.Core, NeuralPalette.Alpha(GhostHologram.Light, .5f), ghostMaterial);
        }

        // Removes everything but transforms, renderers, masks and the body view, in an order RequireComponent allows.
        static void StripToVisual(GameObject root)
        {
            for (int pass = 0; pass < 8; pass++)
            {
                bool removed = false;
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null || component is Transform || component is SpriteRenderer || component is SpriteMask || component is OfficerVisual) continue;
                    bool needed = false;
                    foreach (var other in component.GetComponents<Component>())
                    {
                        if (other == null || other == component) continue;
                        foreach (RequireComponent rule in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                            foreach (var type in new[] { rule.m_Type0, rule.m_Type1, rule.m_Type2 })
                                if (type != null && type.IsInstanceOfType(component)) needed = true;
                    }
                    if (needed) continue;
                    DestroyImmediate(component);
                    removed = true;
                }
                if (!removed) break;
            }
        }

        void WriteGhostPuppet()
        {
            if (ghostPuppet == null) return;
            ghostPuppet.Grounded = Motor.Grounded; ghostPuppet.Dodging = Motor.DashActive; ghostPuppet.Velocity = Motor.Velocity;
            ghostPuppet.Facing = Facing < 0 ? -1 : 1; ghostPuppet.Aim = ghostAim; ghostPuppet.ShotsFired = ghostShots;
            ghostPuppet.Reloading = Time.time < ghostReloadUntil;
            ghostPuppet.ReloadProgress = 1 - Mathf.Clamp01((ghostReloadUntil - Time.time) / GhostReload);
            ghostPuppet.Dead = !IsAlive;
            if (ghostFigure != null && ghostFigure.activeSelf != IsAlive) ghostFigure.SetActive(IsAlive);
        }

        void LateUpdate() { if (IsGhost) WriteGhostPuppet(); }

        public void ConfigureGhost(PlayerHealth player, ArenaNavigation2D navigation, OfficerVisual bodyPrefab)
        {
            IsGhost = true;
            // Instance-only profile, as for the Test Room robot. The Gunner's asset is untouched.
            profile = ScriptableObject.CreateInstance<BossProfile>();
            profile.name = "GHOST (runtime)";
            profile.hideFlags = HideFlags.DontSave;
            profile.Health = GhostHealth;
            profile.Magazine = GhostMagazine;
            profile.BulletSpeed = GhostBulletSpeed;
            profile.ReloadSeconds = GhostReload;
            profile.BurstShotLock = .25f;
            profile.Burst = new BossAbilitySettings(1.1f, .25f, .1f, .5f, 3.4f, 30, 4, 1);
            profile.Motion = new BossMotionProfile { Speed = 7, Acceleration = 90, Braking = 90, JumpSpeed = 12, GravityScale = 3, AirControl = 1 };
            Configure(player, navigation, null, 1, false);
            BuildGhostFigure(bodyPrefab);
            ghostAmmo = GhostMagazine;
            ghostShotAt = Time.time + 1f;
            Face(Vector2.left);
        }

        void OnDisable() { if (IsGhost && Health != null) Health.Invulnerable = false; }
        void DestroyGhostLook() { if (ghostMaterial != null) Destroy(ghostMaterial); }

        void TickGhost()
        {
            if (Health.Invulnerable && Time.time >= ghostImmuneUntil) Health.Invulnerable = false;
            if (Player == null || Player.IsDead) { Motor.Brake(); return; }
            Vector2 chest = Position + Vector2.up * .28f;
            Vector2 target = (Vector2)Player.transform.position + Vector2.up * .1f;
            Vector2 toPlayer = target - chest;
            Face(toPlayer);
            ghostAim = toPlayer.normalized;

            if (!Motor.DashActive)
            {
                // Plain footwork for now: hold a middle distance.
                float gap = Mathf.Abs(toPlayer.x), side = Mathf.Sign(toPlayer.x == 0 ? 1 : toPlayer.x);
                if (gap > 11) Motor.SetMove(side);
                else if (gap < 5) Motor.SetMove(-side);
                else Motor.Brake();
                // Climb toward a player who is a tier or more above.
                if (toPlayer.y > 1.6f && Motor.Grounded && Time.time >= ghostJumpAt && Motor.TryJump())
                    ghostJumpAt = Time.time + 1.2f;
                TryGhostDodge(chest);
            }

            if (Time.time < ghostReloadUntil || Time.time < ghostShotAt || Motor.DashActive) return;
            if (ghostAmmo <= 0) { ghostAmmo = GhostMagazine; }
            FireGhostShot(chest, toPlayer.normalized);
            ghostAmmo--; ghostShots++;
            ghostShotAt = Time.time + GhostShotInterval;
            if (ghostAmmo <= 0) { ghostReloadUntil = Time.time + GhostReload; ghostAmmo = GhostMagazine; }
        }

        void FireGhostShot(Vector2 chest, Vector2 direction)
        {
            Vector2 muzzle = chest + direction * .52f;
            var shot = Instantiate(ProjectilePrefab, muzzle, Quaternion.identity);
            shot.Launch(direction, GhostBulletSpeed, GhostShotDamage, gameObject, friendlyFire: true);
            var sprite = shot.GetComponent<SpriteRenderer>();
            // His own colour, without the Gunner's warm core, halo and tail.
            if (sprite != null) sprite.color = GhostHologram.Light;
            View?.Muzzle(muzzle, direction);
            Sounds?.Play(Sfx.BossBurst, ActorId);
        }

        // A player's bullet about to arrive: step out of its line, as the player would.
        void TryGhostDodge(Vector2 chest)
        {
            if (Time.time < ghostDodgeReadyAt || Time.time < ghostLookAt) return;
            ghostLookAt = Time.time + .05f;
            foreach (var shot in Projectile.Active)
            {
                if (shot == null || shot.Owner != Player.gameObject) continue;
                Vector2 from = shot.transform.position, gap = chest - from;
                if (gap.sqrMagnitude > 3.2f * 3.2f) continue;
                var body = shot.GetComponent<Rigidbody2D>();
                Vector2 travel = body != null ? body.linearVelocity : Vector2.zero;
                if (travel.sqrMagnitude < .01f || Vector2.Dot(travel.normalized, gap.normalized) < .93f) continue;
                // Sideways along the ground, away from the shooter unless a wall is close behind.
                float away = Mathf.Sign(Position.x - Player.transform.position.x);
                if (away == 0) away = 1;
                var arena = Navigation.ArenaBounds;
                if (away > 0 && Position.x > arena.max.x - GhostDodgeDistance - .6f) away = -1;
                else if (away < 0 && Position.x < arena.min.x + GhostDodgeDistance + .6f) away = 1;
                Motor.StartDash(Vector2.right * away, GhostDodgeDistance, GhostDodgeDistance / GhostDodgeSeconds);
                ghostDodgeReadyAt = Time.time + GhostDodgeCooldown;
                ghostImmuneUntil = Time.time + GhostImmunity;
                Health.Invulnerable = true;
                return;
            }
        }
    }
}
