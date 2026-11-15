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

        public void ConfigureGhost(PlayerHealth player, ArenaNavigation2D navigation)
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
            ghostAmmo = GhostMagazine;
            ghostShotAt = Time.time + 1f;
            Face(Vector2.left);
        }

        void OnDisable() { if (IsGhost && Health != null) Health.Invulnerable = false; }

        void TickGhost()
        {
            if (Health.Invulnerable && Time.time >= ghostImmuneUntil) Health.Invulnerable = false;
            if (Player == null || Player.IsDead) { Motor.Brake(); return; }
            Vector2 chest = Position + Vector2.up * .28f;
            Vector2 target = (Vector2)Player.transform.position + Vector2.up * .1f;
            Vector2 toPlayer = target - chest;
            Face(toPlayer);

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
            ghostAmmo--;
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
