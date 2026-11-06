using System.Collections;
using UnityEngine;

namespace GoGhost.Duel
{
    /// <summary>
    /// FINAL BOSS SPECIAL MOVES (a component on the boss).
    /// While one runs it switches the normal AI off (Tactics.Enabled=false) and switches it back on afterwards.
    /// Tells are simple flashing boxes and lines, so the player can read where each hit will land.
    ///
    ///  Move 0, Global slash:  full-height slash, then another, then a double slash with a safe gap to step into.
    ///  Move 1, Sword throw:   thrown sword, three bullets, teleport next to the sword, forward slash.
    ///  Move 2, Platform beam: tells a platform, then destroys it for the rest of the fight (keeps at least 2 standing).
    ///  Phase 2:               when the first health pool ends, stops everything, strips the player's AI rules,
    ///                         refills health and runs only the normal AI. No special moves any more.
    /// Damage uses PlayerHealth.TakeDamage, so a dodge's invulnerability avoids it, like any other hit.
    /// </summary>
    public sealed class FinalBossSpecials : MonoBehaviour
    {
        BossAgent boss;
        PlayerHealth player;
        Collider2D playerBody;
        ArenaNavigation2D nav;
        Collider2D[] platforms;
        System.Action onPhaseTwo;
        Transform fx;
        Coroutine running;
        float nextSpecialAt;
        int lastMove = -1;
        bool started, transitioning, phaseTwo;
        static Sprite pixel;

        public void Begin(BossAgent boss, PlayerHealth player, ArenaNavigation2D nav, Collider2D[] platforms, System.Action onPhaseTwo)
        {
            this.boss = boss; this.player = player; this.nav = nav; this.platforms = platforms; this.onPhaseTwo = onPhaseTwo;
            playerBody = player.GetComponent<Collider2D>();
            fx = new GameObject("FinalBossFx").transform;
            fx.SetParent(transform.parent, false);
        }

        bool Aborted => boss == null || !boss.IsAlive || !boss.Running || player == null || player.IsDead
            || boss.Health.AwaitingSecondPhase;

        void Update()
        {
            if (boss == null || !boss.Running || !boss.IsAlive || Time.deltaTime <= 0) return;
            if (!transitioning && !phaseTwo && boss.Health.AwaitingSecondPhase)
            {
                StartCoroutine(PhaseTwo());
                return;
            }
            if (phaseTwo || transitioning || running != null || player == null || player.IsDead) return;
            if (!started) { started = true; nextSpecialAt = Time.time + 4f; }
            if (Time.time < nextSpecialAt || boss.Actions.Busy || !boss.Motor.Grounded) return;
            running = StartCoroutine(Run(PickMove()));
        }

        int PickMove()
        {
            for (int i = 1; i <= 3; i++)
            {
                int move = (lastMove + i) % 3;
                if (move == 2 && PickPlatform() == null) continue;
                return move;
            }
            return 0;
        }

        IEnumerator Run(int move)
        {
            boss.Tactics.Enabled = false;
            boss.Motor.HoldHorizontal();
            if (move == 0) yield return StartCoroutine(GlobalSlash());
            else if (move == 1) yield return StartCoroutine(SwordThrow());
            else yield return StartCoroutine(PlatformBeam());
            ClearFx();
            lastMove = move;
            running = null;
            nextSpecialAt = Time.time + Random.Range(3.5f, 5.5f);
            if (boss != null && boss.IsAlive && !phaseTwo && !boss.Health.AwaitingSecondPhase) boss.Tactics.Enabled = true;
        }

        // ---------------- Move 0: global slash ----------------

        IEnumerator GlobalSlash()
        {
            var arena = nav.ArenaBounds;
            for (int i = 0; i < 2; i++)
            {
                float x = Mathf.Clamp(PlayerX(), arena.min.x + 1.5f, arena.max.x - 1.5f);
                yield return StartCoroutine(Slash(new[] { x }, 2.6f, i == 0 ? .85f : .70f, 28));
                if (Aborted) yield break;
                yield return StartCoroutine(Pause(.25f));
                if (Aborted) yield break;
            }
            // Double slash: the safe gap is placed away from the player, so they must move into it.
            float side = Random.value < .5f ? -1f : 1f;
            float gapCentre = Mathf.Clamp(PlayerX() + side * 3.2f, arena.min.x + 3.2f, arena.max.x - 3.2f);
            float width = 2.6f, offset = 1.5f + width * .5f;
            yield return StartCoroutine(Slash(new[] { gapCentre - offset, gapCentre + offset }, width, 1.1f, 32));
        }

        IEnumerator Slash(float[] xs, float width, float tell, int damage)
        {
            var arena = nav.ArenaBounds;
            float height = arena.size.y + 2f, centreY = arena.center.y;
            var fills = new SpriteRenderer[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                fills[i] = Box("SlashFill", new Vector2(xs[i], centreY), new Vector2(width, height), new Color(1, .15f, .15f, .1f));
                Box("SlashEdgeL", new Vector2(xs[i] - width * .5f, centreY), new Vector2(.07f, height), new Color(1, .35f, .35f, .95f));
                Box("SlashEdgeR", new Vector2(xs[i] + width * .5f, centreY), new Vector2(.07f, height), new Color(1, .35f, .35f, .95f));
            }
            boss.Sounds?.Play(Sfx.DrawPrepare, boss.ActorId);
            for (float t = 0; t < tell; t += Time.deltaTime)
            {
                if (Aborted) { ClearFx(); yield break; }
                float pulse = .85f + .15f * Mathf.Sin(t * 28f);
                foreach (var fill in fills) fill.color = new Color(1, .15f, .15f, Mathf.Lerp(.10f, .50f, t / tell) * pulse);
                yield return null;
            }
            foreach (var fill in fills) fill.color = new Color(1, 1, 1, .95f);
            boss.Sounds?.Play(Sfx.BladeHeavy, boss.ActorId);
            for (int i = 0; i < xs.Length; i++)
                Strike(new Bounds(new Vector3(xs[i], centreY, 0), new Vector3(width, height, 100f)), damage);
            yield return StartCoroutine(Pause(.14f));
            ClearFx();
        }

        // ---------------- Move 1: sword throw, three bullets, teleport slash ----------------

        IEnumerator SwordThrow()
        {
            Vector2 from = boss.Position + Vector2.up * .6f;
            Vector2 dir = ((Vector2)playerBody.bounds.center - from).normalized;
            if (dir.sqrMagnitude < .01f) dir = Vector2.right * boss.Facing;
            float reach = ArenaBoundary2D.ExitDistance(nav.ArenaBounds, from, dir, .2f);
            var wall = Physics2D.Raycast(from, dir, reach, LayerMask.GetMask("Ground"));
            float distance = Mathf.Max(1.5f, wall.collider != null ? wall.distance : reach);
            Vector2 end = from + dir * distance;

            // Tell: a line along the throw.
            var tellLine = Line("ThrowTell", from, end, .22f, new Color(1, .3f, .3f, .2f));
            boss.Sounds?.Play(Sfx.DrawPrepare, boss.ActorId);
            for (float t = 0; t < .55f; t += Time.deltaTime)
            {
                if (Aborted) yield break;
                tellLine.color = new Color(1, .3f, .3f, Mathf.Lerp(.15f, .7f, t / .55f));
                yield return null;
            }
            Destroy(tellLine.gameObject);

            // The sword flies and plants where the line ends.
            var sword = Line("Sword", from, from + dir * 2.4f, .45f, new Color(.9f, .95f, 1f, 1f));
            bool hit = false;
            for (float travelled = 0; travelled < distance; travelled += 30f * Time.deltaTime)
            {
                if (Aborted) yield break;
                Vector2 p = from + dir * travelled;
                sword.transform.position = new Vector3(p.x, p.y, 0);
                if (!hit && player != null && !player.IsDead
                    && new Bounds(new Vector3(p.x, p.y, 0), new Vector3(1.4f, 1.4f, 100f)).Intersects(playerBody.bounds))
                { hit = true; player.TakeDamage(22, boss.gameObject); }
                yield return null;
            }
            sword.transform.position = new Vector3(end.x - dir.x * .8f, end.y - dir.y * .8f, 0);
            sword.color = new Color(.65f, .7f, .8f, 1f);

            // Three bullets, each aimed where the player is when it leaves.
            var aimLine = Line("ShotTell", from, playerBody.bounds.center, .12f, new Color(1, .3f, .3f, .5f));
            for (float t = 0; t < .4f; t += Time.deltaTime)
            {
                if (Aborted) yield break;
                SetLine(aimLine, boss.Position + Vector2.up * .6f, playerBody.bounds.center);
                yield return null;
            }
            for (int i = 0; i < 3; i++)
            {
                if (Aborted) yield break;
                Vector2 origin = boss.Position + Vector2.up * .6f;
                Fire(((Vector2)playerBody.bounds.center - origin).normalized);
                for (float t = 0; t < .22f; t += Time.deltaTime)
                {
                    SetLine(aimLine, boss.Position + Vector2.up * .6f, playerBody.bounds.center);
                    yield return null;
                }
            }
            Destroy(aimLine.gameObject);
            yield return StartCoroutine(Pause(.3f));
            if (Aborted) yield break;

            // Teleport to the standing spot nearest the sword, then slash toward the player.
            if (TryTeleportSpot(end, out var spot))
            {
                var flashOut = Box("FlashOut", boss.Position, new Vector2(1.8f, 3.4f), new Color(1, 1, 1, .85f));
                yield return StartCoroutine(Pause(.12f));
                Destroy(flashOut.gameObject);
                Teleport(spot);
                Box("FlashIn", boss.Position, new Vector2(1.8f, 3.4f), new Color(1, 1, 1, .85f));
                boss.Sounds?.Play(Sfx.DashCut, boss.ActorId);
                yield return StartCoroutine(Pause(.12f));
            }
            if (Aborted) yield break;
            float face = Mathf.Sign(playerBody.bounds.center.x - boss.Position.x);
            if (face == 0) face = 1;
            boss.Face(Vector2.right * face);
            Vector2 centre = boss.Position + new Vector2(face * 3f, .3f);
            var zone = Box("SlashZone", centre, new Vector2(5.2f, 3.4f), new Color(1, .15f, .15f, .1f));
            for (float t = 0; t < .5f; t += Time.deltaTime)
            {
                if (Aborted) yield break;
                zone.color = new Color(1, .15f, .15f, Mathf.Lerp(.10f, .55f, t / .5f) * (.85f + .15f * Mathf.Sin(t * 30f)));
                yield return null;
            }
            zone.color = new Color(1, 1, 1, .95f);
            boss.Sounds?.Play(Sfx.BladeHeavy, boss.ActorId);
            Strike(new Bounds(new Vector3(centre.x, centre.y, 0), new Vector3(5.2f, 3.4f, 100f)), 30);
            yield return StartCoroutine(Pause(.15f));
        }

        void Fire(Vector2 direction)
        {
            var prefab = boss.ProjectilePrefab;
            if (prefab == null) return;
            Vector2 muzzle = boss.Position + Vector2.up * .6f + direction * 1.4f;
            var shot = Instantiate(prefab, muzzle, Quaternion.identity);
            shot.Launch(direction, 18f, 12, boss.gameObject, friendlyFire: true);
            boss.Sounds?.Play(Sfx.BossBurst, boss.ActorId);
        }

        bool TryTeleportSpot(Vector2 near, out Vector2 spot)
        {
            spot = default;
            float best = float.PositiveInfinity;
            Vector2 size = boss.Motor.Size;
            Vector2 target = playerBody.bounds.center;
            foreach (var node in nav.Nodes)
            {
                Vector2 c = node.Center(size);
                float d = Vector2.Distance(c, near);
                if (d >= best) continue;
                if (Mathf.Abs(c.x - target.x) <= size.x + .5f && Mathf.Abs(c.y - target.y) <= size.y) continue;
                if (!nav.CanStand(c, size)) continue;
                best = d; spot = c;
            }
            return best < float.PositiveInfinity;
        }

        void Teleport(Vector2 spot)
        {
            var body = boss.GetComponent<Rigidbody2D>();
            Vector2 delta = spot - boss.Motor.Position;
            var interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation2D.None;
            boss.Motor.Stop();
            body.position += delta;
            boss.transform.position += (Vector3)delta;
            Physics2D.SyncTransforms();
            body.interpolation = interpolation;
        }

        // ---------------- Move 2: platform beam ----------------

        IEnumerator PlatformBeam()
        {
            var target = PickPlatform();
            if (target == null) yield break;
            Vector2 from = boss.Position + Vector2.up * .6f;
            Vector2 aim = target.bounds.center;
            Vector2 dir = (aim - from).normalized;
            Vector2 end = from + dir * (Vector2.Distance(from, aim) + 4f);
            var beam = Line("BeamTell", from, end, .4f, new Color(1, .2f, .2f, .15f));
            var mark = Box("PlatformMark", aim, (Vector2)target.bounds.size + Vector2.one * .3f, new Color(1, .2f, .2f, .15f));
            boss.Sounds?.Play(Sfx.BeamCharge, boss.ActorId);
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                if (Aborted) yield break;
                float alpha = Mathf.Lerp(.12f, .65f, t / 1.5f) * (.85f + .15f * Mathf.Sin(t * 26f));
                beam.color = new Color(1, .2f, .2f, alpha);
                mark.color = new Color(1, .2f, .2f, alpha * .8f);
                yield return null;
            }
            boss.Sounds?.Play(Sfx.BeamFire, boss.ActorId);
            boss.View?.Beam(from, end);
            beam.color = new Color(1, 1, 1, 1f);
            if (BeamHitsPlayer(from, end)) player.TakeDamage(35, boss.gameObject);
            if (target != null)
            {
                target.gameObject.SetActive(false);   // gone for the rest of the fight
                Physics2D.SyncTransforms();
                nav.Refresh();                        // routes replan around the missing platform
            }
            yield return StartCoroutine(Pause(.45f));
        }

        bool BeamHitsPlayer(Vector2 from, Vector2 to)
        {
            if (player == null || player.IsDead) return false;
            var box = playerBody.bounds;
            box.Expand(.7f);
            for (int i = 0; i <= 40; i++)
                if (box.Contains(new Vector3(Mathf.Lerp(from.x, to.x, i / 40f), Mathf.Lerp(from.y, to.y, i / 40f), box.center.z))) return true;
            return false;
        }

        Collider2D PickPlatform()
        {
            int standing = 0;
            Collider2D best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 1; i < platforms.Length; i++)   // index 0 is the floor
            {
                var p = platforms[i];
                if (p == null || !p.gameObject.activeInHierarchy || !p.enabled) continue;
                standing++;
                float d = Vector2.Distance(p.bounds.center, playerBody.bounds.center);
                if (d < bestDistance) { bestDistance = d; best = p; }
            }
            return standing > 2 ? best : null;   // always leave at least two platforms
        }

        // ---------------- Phase 2 ----------------

        IEnumerator PhaseTwo()
        {
            transitioning = true;
            if (running != null) { StopCoroutine(running); running = null; }
            ClearFx();
            boss.Tactics.Enabled = false;
            boss.Actions.StopCombat();
            boss.Motor.HoldHorizontal();
            onPhaseTwo?.Invoke();   // the player loses every AI rule
            for (float t = 0; t < 2.2f; t += Time.deltaTime)
            {
                if (boss == null) yield break;
                if (Mathf.Repeat(t, .45f) < Time.deltaTime) boss.View?.Impact(boss.Position);
                yield return null;
            }
            boss.Health.BeginSecondPhase();
            phaseTwo = true;
            transitioning = false;
            boss.Tactics.Enabled = true;
        }

        // ---------------- Helpers ----------------

        float PlayerX() => playerBody.bounds.center.x;

        IEnumerator Pause(float seconds)
        {
            for (float t = 0; t < seconds && !Aborted; t += Time.deltaTime) yield return null;
        }

        void Strike(Bounds area, int damage)
        {
            if (player == null || player.IsDead) return;
            if (area.Intersects(playerBody.bounds)) player.TakeDamage(damage, boss.gameObject);
        }

        static Sprite Pixel()
        {
            if (pixel == null)
            {
                var texture = new Texture2D(1, 1);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
            }
            return pixel;
        }

        SpriteRenderer Box(string name, Vector2 centre, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(fx, false);
            go.transform.position = new Vector3(centre.x, centre.y, 0);
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Pixel(); sr.color = color; sr.sortingOrder = 60;
            return sr;
        }

        SpriteRenderer Line(string name, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var sr = Box(name, (a + b) * .5f, new Vector2(1, thickness), color);
            SetLine(sr, a, b);
            return sr;
        }

        static void SetLine(SpriteRenderer sr, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            var scale = sr.transform.localScale;
            sr.transform.position = new Vector3((a.x + b.x) * .5f, (a.y + b.y) * .5f, 0);
            sr.transform.localScale = new Vector3(Mathf.Max(.01f, delta.magnitude), scale.y, 1);
            sr.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        void ClearFx()
        {
            if (fx == null) return;
            for (int i = fx.childCount - 1; i >= 0; i--) Destroy(fx.GetChild(i).gameObject);
        }
    }
}
