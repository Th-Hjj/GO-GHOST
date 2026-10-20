#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    private IEnumerator Phase2Walk(GameSessionFlow flow, float x)
    {
        var rb = flow.ActiveFight.Player.GetComponent<Rigidbody2D>();
        float end = Time.time + 6;
        while (Mathf.Abs(rb.position.x - x) > .08f && Time.time < end)
        {
            Keys(rb.position.x < x ? Key.D : Key.A);
            yield return null;
        }
        Keys(); yield return Frames(3);
        Check("walk reaches x=" + x, Mathf.Abs(rb.position.x - x) < .2f, "actual x=" + rb.position.x);
    }

    private IEnumerator Phase2Land(GameSessionFlow flow, float top, string label)
    {
        var player = flow.ActiveFight.Player;
        var movement = player.GetComponent<PlayerMovement>();
        float end = Time.time + 4;
        while (!movement.IsGrounded && Time.time < end) yield return null;
        yield return Seconds(.08f);
        float feet = player.GetComponent<Collider2D>().bounds.min.y;
        Check(label, movement.IsGrounded && Mathf.Abs(feet - top) < .13f,
            "feet=" + feet.ToString("0.000") + "; expected surface=" + top + "; position=" + player.transform.position);
    }

    private IEnumerator Phase2JumpTo(GameSessionFlow flow, float x, float top, string label)
    {
        var rb = flow.ActiveFight.Player.GetComponent<Rigidbody2D>();
        Keys(Key.Space); yield return Seconds(.06f);
        Check(label + " ordinary jump", rb.linearVelocity.y > 8 && !flow.Deployment.HasDoubleJump, "unchanged jump impulse");
        yield return Phase2Walk(flow, x);
        yield return Phase2Land(flow, top, label);
    }

    private IEnumerator Phase2Arena()
    {
        Record("Arena V1: real keyboard/physics traversal. Boss stopped and collider disabled only for traversal; no player movement/stat changes.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow); yield return Frames(3);
        var f = flow.ActiveFight; f.Boss.StopCombat(); var bossBody = f.Boss.GetComponent<Rigidbody2D>(); bossBody.simulated = false;
        Check("Arena Human starts 0%", flow.Deployment.BodyModificationPercent == 0 && !f.Player.GetComponent<PlayerMovement>().HasDoubleJump
            && !f.Player.GetComponent<PlayerMovement>().HasHover, "no leg abilities");
        foreach (string name in new[] { "LowPlatformA", "LowPlatformB", "HighPlatformC" })
        {
            var platform = GameObject.Find(name); var effect = platform.GetComponent<PlatformEffector2D>();
            var box = platform.GetComponent<BoxCollider2D>();
            Check("native platform " + name, effect.useOneWay && box.usedByEffector && !box.isTrigger && platform.layer == LayerMask.NameToLayer("Ground"),
                "center=" + platform.transform.position + "; bounds=" + box.bounds);
        }
        yield return PolishScreen("phase2-arena");
        yield return Phase2Walk(flow, -4.5f);
        yield return Phase2JumpTo(flow, -4.5f, -.4f, "0% Ground -> Low A");
        yield return Phase2Walk(flow, -3.5f);
        yield return Phase2JumpTo(flow, -1f, 1.3f, "0% Low A -> High C");
        yield return PolishScreen("phase2-human-high-c");
        yield return Phase2Walk(flow, 4.5f); yield return Phase2Land(flow, -.4f, "High C -> Low B");
        yield return Phase2Walk(flow, 8f); yield return Phase2Land(flow, -2.5f, "Low B -> Ground");
        yield return Phase2Walk(flow, 4.5f);
        yield return Phase2JumpTo(flow, 4.5f, -.4f, "0% Ground -> Low B");
        yield return Phase2Walk(flow, 3.5f);
        yield return Phase2JumpTo(flow, 1f, 1.3f, "0% Low B -> High C");
        yield return Phase2Walk(flow, -8f); yield return Phase2Land(flow, -2.5f, "High C -> Ground without softlock");
        yield return Phase2Walk(flow, -4.5f); yield return Phase2JumpTo(flow, -4.5f, -.4f, "platform Dodge setup");
        yield return Phase2Walk(flow, -5.5f);
        Keys(Key.A, Key.LeftShift); yield return Seconds(.08f);
        Check("Dodge starts on platform", f.Player.GetComponent<PlayerDodge>().IsDodging, "same grounded Shift action");
        Keys(); yield return Seconds(.4f); yield return Phase2Land(flow, -2.5f, "Dodge off platform lands normally");

        Record("FIXTURE positions for isolated real projectile contact probes, then restore normal Boss physics.");
        f.Player.GetComponent<Rigidbody2D>().position = new Vector2(-10, -1.69f);
        bossBody.position = new Vector2(10, -1.7f); bossBody.simulated = true; yield return Seconds(.2f);
        foreach (float x in new[] { -4.5f, 4.5f })
            Check("upward Boss muzzle clears low platform x=" + x,
                Physics2D.OverlapCircle(new Vector2(x, bossBody.position.y + .9f), .125f, LayerMask.GetMask("Ground")) == null,
                "real bullet radius=.125, existing Boss muzzle offset=.9; collider starts above muzzle");
        foreach (var owner in new[] { f.Player.gameObject, f.Boss.gameObject })
        foreach (int side in new[] { -1, 1 })
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
            var bullet = Instantiate(prefab, new Vector3(-4.5f, -.4f + side, 0), Quaternion.identity).GetComponent<Projectile>();
            bullet.Launch(Vector2.down * side, 10, 12, owner);
            yield return Seconds(.35f);
            bool blocked = bullet == null;
            bool crossed = bullet != null && (bullet.transform.position.y + .4f) * side < -.5f;
            Check("platform projectile " + owner.name + " side=" + side, blocked || crossed,
                blocked ? "retired on platform well before 3s lifetime" : "passed native effector; no stuck bullet");
        }
        yield return Seconds(3.2f);
        Check("platform probes leave no bullets", FindObjectsByType<Projectile>().Length == 0, "no permanent projectile accumulation");
        yield return VerifyOneBullet(flow, 12, "Phase2 open ground lane");
        yield return PressureV2(flow);
        Check("Boss ground navigation remains inside arena", Mathf.Abs(bossBody.position.x) < 15 && bossBody.position.y < -.5f,
            "after four-skill cycles: " + bossBody.position);
        yield return Phase2HighCombat(flow);
    }

    private IEnumerator Phase2HighCombat(GameSessionFlow flow)
    {
        Record("FIXTURE High C firing/pressure observation: initial actor positions only, normal damage and unmodified Boss AI.");
        var f = flow.ActiveFight; var rb = f.Player.GetComponent<Rigidbody2D>(); var bossBody = f.Boss.GetComponent<Rigidbody2D>();
        yield return Phase2Place(flow, new Vector2(0, 2.11f)); bossBody.position = new Vector2(9, -1.7f);
        yield return Seconds(.2f); int bossHP = f.BossHealth.CurrentHealth;
        AimAtWorld(f.Boss.transform.position, true); yield return Frames(3);
        bool liveShot = false; foreach (var p in FindObjectsByType<Projectile>()) if (p.OwnerLayer == f.Player.gameObject.layer) liveShot = true;
        Check("High C player muzzle spawns outside platform", liveShot, "real LMB projectile exists after launch");
        yield return Seconds(.05f); MouseTo(.5f, .5f); yield return Seconds(1);
        Record("OBSERVATION High C center -> Boss at x9: damage=" + (bossHP - f.BossHealth.CurrentHealth) + "; bullets remaining=" + FindObjectsByType<Projectile>().Length);
        f.Boss.BeginCombat(); int hp = f.Player.CurrentHealth, peak = 0; float began = Time.time;
        while (flow.State == GameSessionFlow.FlowState.Combat && Time.time - began < 25)
        {
            peak = Mathf.Max(peak, FindObjectsByType<Projectile>().Length); yield return null;
        }
        Check("high-target Boss simulation stays bounded", peak <= 12 && Mathf.Abs(bossBody.position.x) < 15 && bossBody.position.y < -.5f,
            "peak bullets=" + peak + "; Boss=" + bossBody.position);
        Record("OBSERVATION stationary High C without invulnerability: " + (Time.time - began).ToString("0.00")
            + "s, Player HP " + hp + " -> " + f.Player.CurrentHealth + "; Boss shots=" + f.Boss.ShotsFired + "; state=" + flow.State);
        f.Boss.StopCombat();
    }

    private void Phase2Models()
    {
        string[] options = { "", LoadoutRules.LegMoveSpeed, LoadoutRules.LegDoubleJump, LoadoutRules.LegHover };
        bool correct = true;
        foreach (string left in options) foreach (string right in options)
        {
            var draft = new LoadoutDraft(true);
            correct &= draft.TrySetSlot(LoadoutSlot.LeftLeg, left) && draft.TrySetSlot(LoadoutSlot.RightLeg, right);
            var s = draft.Deploy(1);
            correct &= s.BodyModificationPercent == (left == "" ? 0 : 10) + (right == "" ? 0 : 10)
                && s.MoveSpeed == 6 + (left == LoadoutRules.LegMoveSpeed ? 2 : 0) + (right == LoadoutRules.LegMoveSpeed ? 2 : 0)
                && s.HasDoubleJump == (left == LoadoutRules.LegDoubleJump || right == LoadoutRules.LegDoubleJump)
                && s.HasHover == (left == LoadoutRules.LegHover || right == LoadoutRules.LegHover)
                && s.WeaponCompatibility == WeaponCompatibility.Normal && !draft.TrySetSlot(LoadoutSlot.LeftLeg, "");
        }
        Check("all 16 leg combinations preserve replacement and percentage", correct, "0/10/20%; only Move Speed adds speed; OR semantics for Double Jump/Hover; sealed draft");
    }

    private IEnumerator Phase2Select(GameSessionFlow flow, LoadoutSlot slot, string item)
    {
        for (int i = 0; i < 4 && flow.Draft.GetSlot(slot).ItemId != item; i++) yield return ClickSessionButton(slot + "Button");
        Check("leg UI selects " + slot + "=" + LoadoutRules.ItemLabel(item), flow.Draft.GetSlot(slot).ItemId == item,
            "real replacement-button clicks; contribution=" + flow.Draft.GetSlot(slot).BodyContributionPercent);
    }

    private IEnumerator Phase2Death(GameSessionFlow flow)
    {
        Record("FIXTURE direct death to inspect attempt cleanup, not natural combat evidence.");
        var f = flow.ActiveFight; var movement = f.Player.GetComponent<PlayerMovement>();
        v2ProtectedPlayer = null; f.Player.IsInvulnerable = false;
        f.Player.TakeDamage(f.Player.MaxHealth, f.Boss.gameObject); Keys(); yield return Frames(3);
        Check("leg attempt cleanup", !movement.IsHovering && movement.HoverRemaining == 0 && movement.AirJumpsRemaining == 0
            && f.Player.GetComponent<Rigidbody2D>().gravityScale == 3 && f.Player.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero,
            "no fuel, air jump, hover or velocity residue; original gravity=3");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Loadout);
        Check("previous leg actor disposed", movement == null, "next officer uses new actor");
    }

    private IEnumerator Phase2Equip(GameSessionFlow flow, string left, string right)
    {
        yield return Phase2Death(flow);
        yield return Phase2Select(flow, LoadoutSlot.LeftLeg, left); yield return Phase2Select(flow, LoadoutSlot.RightLeg, right);
        yield return DeployAndFocus(flow); flow.ActiveFight.Boss.StopCombat();
        Check("leg redeploy full health", flow.ActiveFight.Player.CurrentHealth == 100 && flow.ActiveFight.BossHealth.CurrentHealth == 800, "fresh attempt HP and FSM");
    }

    private IEnumerator Phase2Place(GameSessionFlow flow, Vector2 position)
    {
        Record("FIXTURE start position=" + position + "; resource reset only through real landing, not direct counter writes.");
        FocusV2(flow); var rb = flow.ActiveFight.Player.GetComponent<Rigidbody2D>(); rb.position = position; rb.linearVelocity = Vector2.zero;
        yield return Seconds(.2f);
    }

    private IEnumerator Phase2AirJumps(GameSessionFlow flow, string label)
    {
        yield return Phase2Place(flow, new Vector2(-10, -1.69f));
        var movement = flow.ActiveFight.Player.GetComponent<PlayerMovement>(); var rb = movement.GetComponent<Rigidbody2D>();
        Keys(Key.Space); yield return Seconds(.32f);
        Check(label + " held first Space does not consume extra jump", movement.AirJumpsRemaining == 1, "one press, no automatic air jump");
        Keys(); yield return Seconds(.05f); Keys(Key.Space); yield return Seconds(.06f);
        Check(label + " second press jumps", rb.linearVelocity.y > 8 && movement.AirJumpsRemaining == 0, "actual upward velocity=" + rb.linearVelocity.y);
        Keys(); yield return Seconds(.05f); float before = rb.linearVelocity.y; Keys(Key.Space); yield return Seconds(.06f); Keys();
        Check(label + " third press cannot jump", movement.AirJumpsRemaining == 0 && rb.linearVelocity.y < before, "no third upward impulse");
        yield return Phase2Land(flow, -2.5f, label + " lands");
        Check(label + " landing restores one", movement.AirJumpsRemaining == 1, "one shared air jump regardless of number of equipped legs");
    }

    private IEnumerator Phase2HoverProbe(GameSessionFlow flow, string label, bool mixed)
    {
        yield return Phase2Place(flow, new Vector2(0, 2.11f));
        var movement = flow.ActiveFight.Player.GetComponent<PlayerMovement>(); var rb = movement.GetComponent<Rigidbody2D>();
        Check(label + " one full resource", Mathf.Abs(movement.HoverRemaining - PlayerMovement.HoverDuration) < .001f, "max=" + movement.HoverRemaining);
        Keys(Key.Space); yield return Seconds(.2f);
        if (mixed)
        {
            Keys(); yield return Seconds(.05f); Keys(Key.Space); yield return Seconds(.06f);
            Check(label + " extra jump before hover", rb.linearVelocity.y > 8 && movement.AirJumpsRemaining == 0, "one second-press impulse");
        }
        float deadline = Time.time + 2;
        while (!movement.IsHovering && Time.time < deadline) yield return null;
        Check(label + " hold activates descent hover", movement.IsHovering && rb.gravityScale == 3, "gravity unchanged; velocity=" + rb.linearVelocity);
        yield return Seconds(.24f); Keys(); yield return Seconds(.06f); float remaining = movement.HoverRemaining;
        yield return Seconds(.12f);
        Check(label + " release pauses resource and restores free fall", !movement.IsHovering && Mathf.Abs(movement.HoverRemaining - remaining) < .001f
            && rb.linearVelocity.y < -3, "remaining=" + remaining + "; velocity=" + rb.linearVelocity.y);
        Keys(Key.Space); yield return Seconds(.05f);
        Check(label + " resume has no repeated jump or resource refill", movement.IsHovering && movement.HoverRemaining < remaining
            && movement.AirJumpsRemaining == 0 && rb.linearVelocity.y <= 0, "shared Space state stays bounded");
        // Release testing may legitimately land before exhaustion. Refill via landing, then test a separate uninterrupted hold.
        Keys(); yield return Phase2Land(flow, 1.3f, label + " release test lands");
        Keys(Key.Space); deadline = Time.time + 2;
        while (!movement.IsHovering && Time.time < deadline) yield return null;
        float began = Time.time; deadline = Time.time + 2;
        while (movement.HoverRemaining > 0 && !movement.IsGrounded && Time.time < deadline) yield return null;
        yield return Seconds(.04f);
        Check(label + " duration bounded while still holding", movement.HoverRemaining == 0 && !movement.IsHovering && !movement.IsGrounded
            && rb.linearVelocity.y < -1.5f && Time.time - began >= PlayerMovement.HoverDuration - .04f
            && Time.time - began < PlayerMovement.HoverDuration + .15f,
            "no double duration/permanent flight; remaining=" + movement.HoverRemaining + "; y velocity=" + rb.linearVelocity.y);
        Keys(); yield return Phase2Land(flow, 1.3f, label + " returns to High C");
        Check(label + " landing refills both resources", Mathf.Abs(movement.HoverRemaining - PlayerMovement.HoverDuration) < .001f
            && movement.AirJumpsRemaining == (mixed ? 1 : 0), "fuel=" + movement.HoverRemaining + "; air jumps=" + movement.AirJumpsRemaining);
    }

    private IEnumerator Phase2Legs()
    {
        Record("Phase2 legs: virtual input, real physics and uGUI; forced branch outcomes and initial positions explicitly labelled.");
        Phase2Models(); var flow = FindAnyObjectByType<GameSessionFlow>();
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); FocusV2(flow); flow.ActiveFight.Boss.StopCombat();
        var official = PoliceArchive.Current; string officialBefore = File.ReadAllText(PoliceArchive.SavePath);
        var archive = new PoliceArchiveStore(Path.GetFullPath("Logs/GO_GHOST_VS/Phase2ArchiveTests/" + Guid.NewGuid().ToString("N") + "/archive.json"));
        var profile = archive.Load(); profile.CompleteAchievement("TEST_PHASE2_ACHIEVEMENT");
        Check("Phase2 isolated archive saved", archive.TrySave(profile, out string error), error ?? "isolated test ID");

        yield return Phase2Equip(flow, LoadoutRules.LegMoveSpeed, "");
        yield return RateAndMovementV2(flow, "Phase2 Move Speed");
        yield return Phase2Equip(flow, LoadoutRules.LegDoubleJump, "");
        yield return Phase2AirJumps(flow, "single Double Jump");
        yield return Phase2Place(flow, new Vector2(-4.5f, .39f)); yield return Phase2Walk(flow, -7.5f);
        var movement = flow.ActiveFight.Player.GetComponent<PlayerMovement>();
        Check("walk off edge keeps one air jump", !movement.IsGrounded && movement.AirJumpsRemaining == 1, "landing resource survives walking/falling");
        Keys(Key.Space); yield return Seconds(.06f); Keys();
        Check("walk off edge can spend exactly one", movement.AirJumpsRemaining == 0 && movement.GetComponent<Rigidbody2D>().linearVelocity.y > 8, "no infinite reset at edge");

        yield return Phase2Equip(flow, LoadoutRules.LegHover, ""); // death while Double Jump is spent
        Check("replacement Double Jump -> Hover", !flow.Deployment.HasDoubleJump && flow.Deployment.HasHover && flow.Deployment.MoveSpeed == 6, "no old jump or speed effect");
        yield return Phase2HoverProbe(flow, "single Hover", false);
        Keys(Key.Space); float deadline = Time.time + 2;
        movement = flow.ActiveFight.Player.GetComponent<PlayerMovement>();
        while (!movement.IsHovering && Time.time < deadline) yield return null;
        Check("hover active before death", movement.IsHovering, "held Space before death/removal");
        yield return Phase2Equip(flow, "", "");
        movement = flow.ActiveFight.Player.GetComponent<PlayerMovement>(); var rb = movement.GetComponent<Rigidbody2D>();
        Check("Hover -> None restores complete Human", !movement.HasHover && !movement.HasDoubleJump && movement.HoverRemaining == 0
            && rb.gravityScale == 3 && flow.Deployment.BodyModificationPercent == 0 && flow.Deployment.MoveSpeed == 6, "no stale resources or modifiers");
        Keys(Key.Space); yield return Seconds(.28f); Keys(); yield return Seconds(.06f); float vy = rb.linearVelocity.y;
        Keys(Key.Space); yield return Seconds(.06f);
        Check("removed extra jump cannot trigger", rb.linearVelocity.y < vy && movement.AirJumpsRemaining == 0, "Space cannot renew upward impulse");
        bool hovered = false; deadline = Time.time + 1;
        while (Time.time < deadline) { hovered |= movement.IsHovering; yield return null; }
        Keys(); Check("removed Hover cannot trigger", !hovered && movement.HoverRemaining == 0 && movement.IsGrounded, "held Space still falls/lands normally");

        yield return Phase2Equip(flow, LoadoutRules.LegDoubleJump, LoadoutRules.LegHover);
        yield return PolishScreen("phase2-mixed-combat");
        yield return Phase2HoverProbe(flow, "Double Jump + Hover", true);
        yield return Phase2Equip(flow, LoadoutRules.LegHover, LoadoutRules.LegDoubleJump);
        yield return Phase2HoverProbe(flow, "Hover + Double Jump", true);
        yield return Phase2Equip(flow, LoadoutRules.LegDoubleJump, LoadoutRules.LegDoubleJump);
        Check("duplicate Double Jump still costs 20%", flow.Deployment.BodyModificationPercent == 20, "two modified body parts");
        yield return Phase2AirJumps(flow, "duplicate Double Jump");
        yield return Phase2Equip(flow, LoadoutRules.LegHover, LoadoutRules.LegHover);
        Check("duplicate Hover still costs 20%", flow.Deployment.BodyModificationPercent == 20, "single movement component, two modified body parts");
        yield return Phase2HoverProbe(flow, "duplicate Hover", false);
        yield return Phase2Equip(flow, LoadoutRules.LegMoveSpeed, LoadoutRules.LegDoubleJump);
        yield return RateAndMovementV2(flow, "Move Speed + Double Jump"); yield return Phase2AirJumps(flow, "speed mixed Double Jump");
        yield return Phase2Equip(flow, LoadoutRules.LegMoveSpeed, LoadoutRules.LegHover);
        yield return RateAndMovementV2(flow, "Move Speed + Hover"); yield return Phase2HoverProbe(flow, "speed mixed Hover", false);

        yield return Phase2Death(flow);
        yield return Phase2Select(flow, LoadoutSlot.LeftLeg, LoadoutRules.LegDoubleJump);
        yield return Phase2Select(flow, LoadoutSlot.RightLeg, LoadoutRules.LegHover);
        yield return ClickSessionButton("TorsoButton"); yield return ClickSessionButton("BrainButton");
        yield return PolishScreen("phase2-mixed-loadout");
        yield return DeployAndFocus(flow); var f = flow.ActiveFight; f.Boss.StopCombat();
        Check("leg Snapshot locks at 80%", flow.Deployment.BodyModificationPercent == 80 && flow.Deployment.HasDoubleJump && flow.Deployment.HasHover
            && !flow.TrySetDraftSlot(LoadoutSlot.LeftLeg, ""), "legs20 + Torso20 + Brain40");
        yield return PolishScreen("phase2-full-mixed-combat");
        yield return BrainV2(flow);
        f.Boss.GetComponent<Rigidbody2D>().position = new Vector2(9, -1.7f);
        yield return Phase2Place(flow, new Vector2(-9, -1.69f));
        Keys(Key.E); yield return Frames(3); Keys(Key.LeftShift); yield return Frames(3); Keys();
        Check("Phase2 E slow and Shift Dodge independent", f.Player.GetComponent<PlayerTimeSlow>().IsActive && f.Player.GetComponent<PlayerDodge>().IsDodging, "both actions work with new legs and AI aim");
        f.BossHealth.TakeDamage(f.BossHealth.MaxHealth, f.Player.gameObject);
        yield return WaitForState(flow, GameSessionFlow.FlowState.Result);
        Check("Phase2 Result reads winning leg snapshot", flow.Victory.BodyModificationPercent == 80 && flow.Victory.WinningDeployment.HasDoubleJump
            && flow.Victory.WinningDeployment.HasHover && flow.Victory.WinningDeployment.GetSlot(LoadoutSlot.Weapon).BodyContributionPercent == 0,
            "controlled victory; no ending routing");
        yield return PolishScreen("phase2-result"); yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat); flow.ActiveFight.Boss.StopCombat();
        Check("Phase2 New Game resets session only", flow.OfficerIndex == 1 && flow.Victory == null && !flow.AugmentationsUnlocked
            && flow.Deployment.BodyModificationPercent == 0 && !flow.Deployment.HasDoubleJump && !flow.Deployment.HasHover
            && flow.Deployment.Aim == AimControl.Manual && flow.ActiveFight.Player.CurrentHealth == 100 && flow.ActiveFight.BossHealth.CurrentHealth == 800,
            "fresh Human officer and full health");
        Check("Phase2 Archive survives New Game without user pollution", ReferenceEquals(official, PoliceArchive.Current)
            && new PoliceArchiveStore(archive.FilePath).Load().CompletedAchievements[0] == "TEST_PHASE2_ACHIEVEMENT"
            && File.ReadAllText(PoliceArchive.SavePath) == officialBefore, "isolated disk round trip survives; official profile untouched");
    }
}
#endif
