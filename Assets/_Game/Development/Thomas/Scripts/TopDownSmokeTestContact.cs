#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    private IEnumerator BossHeadJump()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(S01Focus(flow));
        Record("Boss-head regression: real player physics and Space input; controlled stationary Boss and isolated Archive.");
        try
        {
            yield return S01Fresh(flow, 0);
            var fight = flow.ActiveFight;
            fight.Boss.StopCombat();
            var player = fight.Player.GetComponent<Rigidbody2D>();
            var boss = fight.Boss.GetComponent<Rigidbody2D>();
            var playerCollider = player.GetComponent<Collider2D>();
            var bossCollider = boss.GetComponent<Collider2D>();
            var movement = player.GetComponent<PlayerMovement>();
            boss.bodyType = RigidbodyType2D.Kinematic;
            boss.position = new Vector2(6f, 2f);
            boss.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            player.position = new Vector2(6f, bossCollider.bounds.max.y + playerCollider.bounds.extents.y + .2f);
            player.linearVelocity = Vector2.zero;
            yield return Seconds(.4f);
            Check("standing on Boss is grounded", movement.IsGrounded && playerCollider.IsTouching(bossCollider),
                $"grounded={movement.IsGrounded}, touching={playerCollider.IsTouching(bossCollider)}");
            float startY = player.position.y;
            Keys(Key.Space); yield return Seconds(.15f);
            Check("Space jumps off Boss head", player.position.y > startY + .8f && player.linearVelocity.y > 0f,
                $"rise={player.position.y - startY:0.000}, velocity={player.linearVelocity}");
            Keys(); yield return Frames(3);
            float velocity = player.linearVelocity.y;
            Keys(Key.Space); yield return Seconds(.05f);
            Check("human cannot jump again in midair", !movement.HasDoubleJump && !movement.IsGrounded && player.linearVelocity.y <= velocity,
                $"vy={velocity:0.000}->{player.linearVelocity.y:0.000}");
            Keys();

            float gravity = player.gravityScale;
            player.gravityScale = 0f;
            foreach (bool underside in new[] { false, true })
            {
                player.position = underside
                    ? new Vector2(6f, bossCollider.bounds.min.y - playerCollider.bounds.extents.y + .005f)
                    : new Vector2(bossCollider.bounds.max.x + playerCollider.bounds.extents.x - .005f, 2f);
                player.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms(); yield return Seconds(.1f);
                bool touching = playerCollider.IsTouching(bossCollider);
                Keys(Key.Space); yield return Seconds(.05f);
                Check(underside ? "Boss underside is not ground" : "Boss side is not ground",
                    touching && !movement.IsGrounded && player.linearVelocity.y < .1f,
                    $"touching={touching}, grounded={movement.IsGrounded}, vy={player.linearVelocity.y:0.000}");
                Keys(); yield return Frames(3);
            }

            player.position = new Vector2(6f, bossCollider.bounds.max.y + playerCollider.bounds.extents.y + .01f);
            player.linearVelocity = Vector2.zero;
            Physics2D.IgnoreCollision(playerCollider, bossCollider, true);
            Physics2D.SyncTransforms(); yield return Seconds(.1f);
            Check("ignored Boss collision does not support player", !movement.IsGrounded, $"grounded={movement.IsGrounded}");
            Physics2D.IgnoreCollision(playerCollider, bossCollider, false);
            player.gravityScale = gravity;
            player.position = new Vector2(-5f, -1.5f); player.linearVelocity = Vector2.zero;
            yield return Seconds(.3f);
            bool grounded = movement.IsGrounded;
            startY = player.position.y;
            Keys(Key.Space); yield return Seconds(.15f);
            Check("ordinary floor jump still works", grounded && player.position.y > startY + .8f, $"rise={player.position.y - startY:0.000}");
            Keys();
            yield return S01Fresh(flow, 0);
            yield return Seconds(.2f);
            Check("death Continue and redeploy remain valid", flow.State == GameSessionFlow.FlowState.Combat
                && flow.ActiveFight.Player.GetComponent<PlayerMovement>().IsGrounded && Time.timeScale == 1f, flow.State.ToString());
        }
        finally
        {
            StopCoroutine(focus);
            Keys();
            flow.ActiveFight?.CancelCombat();
        }
    }

    // One-time editor repair; the normal boss-contact scenario only reads the saved prefab.
    public static void ApplyPlayerContactFixAndRunBatch()
    {
        const string materialPath = "Assets/_Game/Levels/L01_Mission/Prefabs/PlayerBody_NoFriction.physicsMaterial2D";
        const string prefabPath = "Assets/_Game/Levels/L01_Mission/Prefabs/CombatAttempt.prefab";
        var material = UnityEditor.AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
        if (material == null)
        {
            material = new PhysicsMaterial2D("PlayerBody_NoFriction");
            UnityEditor.AssetDatabase.CreateAsset(material, materialPath);
        }
        material.friction = 0f;
        material.bounciness = 0f;
        UnityEditor.EditorUtility.SetDirty(material);
        var prefab = UnityEditor.PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var body = prefab.GetComponentInChildren<PlayerHealth>(true).GetComponent<Collider2D>();
            body.sharedMaterial = material;
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            UnityEditor.PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { UnityEditor.PrefabUtility.UnloadPrefabContents(prefab); }
        UnityEditor.AssetDatabase.SaveAssets();
        RunBossContactBatch();
    }

    private IEnumerator BossContact()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        FocusV2(flow);
        var fight = flow.ActiveFight;
        v2ProtectedPlayer = fight.Player;
        var playerCollider = fight.Player.GetComponent<Collider2D>();
        var bossCollider = fight.Boss.GetComponent<Collider2D>();
        Record("CONTACT FIXTURE: real input/physics; forced charge state and test-only invulnerability isolate escape from body contact.");
        Check("saved non-sticking player body", playerCollider.sharedMaterial != null && playerCollider.friction == 0f
            && !playerCollider.isTrigger && !Physics2D.GetIgnoreCollision(playerCollider, bossCollider),
            "zero friction; solid player/Boss contact retained");
        foreach (float side in new[] { -1f, 1f })
        {
            yield return ContactJump(flow, side, false, "saved settings");
            yield return ContactJump(flow, side, true, "saved settings");
        }
        yield return ContactMovementAndDamage(flow);
        Keys();
        fight.Boss.StopCombat();
        v2ProtectedPlayer = null;
        fight.Player.IsInvulnerable = false;
    }

    private IEnumerator ContactMovementAndDamage(GameSessionFlow flow)
    {
        var fight = flow.ActiveFight;
        v2ProtectedPlayer = null;
        fight.Player.IsInvulnerable = false;
        var player = fight.Player.GetComponent<Rigidbody2D>();
        var boss = fight.Boss.GetComponent<Rigidbody2D>();
        fight.Boss.StopCombat();
        FocusV2(flow);
        player.position = new Vector2(-5f, -1.71f);
        boss.position = new Vector2(9f, -1.74f);
        player.linearVelocity = boss.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return Seconds(.25f);
        Keys(Key.D);
        yield return Seconds(.2f);
        Check("normal movement speed", Mathf.Abs(player.linearVelocity.x - flow.Deployment.MoveSpeed) < .05f,
            $"speed={player.linearVelocity.x:0.00}");
        Keys();
        yield return Seconds(.15f);
        Check("no idle sliding", Mathf.Abs(player.linearVelocity.x) < .01f, $"velocity={player.linearVelocity}");
        yield return MeasureDodgeInterval(flow, .8f, "contact fix");

        foreach (float side in new[] { -1f, 1f })
        {
            Keys();
            player.position = new Vector2(side * 14.3f, -1.71f);
            player.linearVelocity = Vector2.zero;
            yield return Seconds(.2f);
            Keys(side < 0 ? Key.A : Key.D);
            yield return Seconds(.35f);
            var bounds = fight.Player.GetComponent<Collider2D>().bounds;
            Check("wall remains solid " + side, bounds.min.x >= -15.03f && bounds.max.x <= 15.03f,
                $"player bounds={bounds.min.x:0.000}..{bounds.max.x:0.000}");
            Keys();
        }

        foreach (bool invulnerable in new[] { false, true })
        {
            FocusV2(flow);
            fight.Boss.StopCombat();
            foreach (var bullet in FindObjectsByType<Projectile>()) bullet.Retire();
            player.position = new Vector2(0f, -1.71f);
            boss.position = new Vector2(1.15f, -1.74f);
            player.linearVelocity = boss.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            yield return Seconds(.2f);
            v2ProtectedPlayer = invulnerable ? fight.Player : null;
            fight.Player.IsInvulnerable = invulnerable;
            int hp = fight.Player.CurrentHealth;
            fight.Boss.BeginCombat();
            typeof(CyberpsychoBoss).GetField("chargeDirection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fight.Boss, -1f);
            typeof(CyberpsychoBoss).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fight.Boss, new object[] { CyberpsychoBoss.AttackState.Charge, .5f });
            yield return Seconds(.4f);
            int damage = hp - fight.Player.CurrentHealth;
            Check("charge contact damage / invulnerable=" + invulnerable, damage == (invulnerable ? 0 : 22),
                $"damage={damage}; repeated contact still consumes only one hit");
            fight.Boss.StopCombat();
            fight.Player.Heal(fight.Player.MaxHealth);
        }
    }

    private IEnumerator ContactJump(GameSessionFlow flow, float side, bool corner, string settings)
    {
        var fight = flow.ActiveFight;
        var player = fight.Player.GetComponent<Rigidbody2D>();
        var boss = fight.Boss.GetComponent<Rigidbody2D>();
        var playerCollider = fight.Player.GetComponent<Collider2D>();
        var bossCollider = fight.Boss.GetComponent<Collider2D>();
        var movement = fight.Player.GetComponent<PlayerMovement>();
        fight.Boss.StopCombat();
        foreach (var bullet in FindObjectsByType<Projectile>()) bullet.Retire();
        FocusV2(flow);
        float playerX = corner ? side * (15f - playerCollider.bounds.extents.x - .015f) : side * 1.1f;
        float gap = playerCollider.bounds.extents.x + bossCollider.bounds.extents.x + .01f;
        player.position = new Vector2(playerX, -2.5f + playerCollider.bounds.extents.y + .015f);
        boss.position = new Vector2(playerX - side * gap, -2.5f + bossCollider.bounds.extents.y + .015f);
        player.linearVelocity = boss.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        yield return Seconds(.2f);
        var towardBoss = side < 0 ? Key.D : Key.A;
        Keys(towardBoss);
        if (corner)
        {
            fight.Boss.BeginCombat();
            typeof(CyberpsychoBoss).GetField("chargeDirection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(fight.Boss, side);
            typeof(CyberpsychoBoss).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fight.Boss, new object[] { CyberpsychoBoss.AttackState.Charge, .5f });
        }
        yield return Seconds(.1f);
        float startY = player.position.y, peakY = startY;
        bool grounded = movement.IsGrounded;
        Keys(towardBoss, Key.Space);
        float end = Time.time + .65f;
        while (Time.time < end)
        {
            peakY = Mathf.Max(peakY, player.position.y);
            yield return null;
        }
        string label = settings + (corner ? " corner charge " : " body contact ") + side;
        Check("jump escapes " + label, grounded && peakY - startY > 1.55f,
            $"grounded={grounded}, rise={peakY-startY:0.000}, position={player.position}, velocity={player.linearVelocity}");
        fight.Boss.StopCombat();
        Keys(side < 0 ? Key.A : Key.D);
        float before = player.position.x;
        yield return Seconds(.2f);
        if (!corner) Check("can retreat " + label, (player.position.x - before) * side > .7f, $"moved={player.position.x-before:0.000}");
        Keys();
        yield return Seconds(.6f);
    }
}
#endif
