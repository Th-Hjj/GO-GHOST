#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Arm-Blade, real deflection and this round's upgrade catalogue. Controlled positions/outcomes, isolated Archive.</summary>
public partial class TopDownSmokeTest
{
    private static string[] ChoiceLabels() => GameObject.Find("UpgradeChoices").GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();

    private IEnumerator ArmBlade()
    {
        Record("Arm-Blade smoke: virtual input, fixture positions and bullets, controlled outcomes, isolated Archive. TEST values.");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            // Launch run: straight through the door, controlled defeat, to reach Upgrade.
            yield return WeaponsPracticeStart(flow);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "first door", 20);
            Keys(); yield return Frames(3);
            flow.ActiveFight.Player.IsInvulnerable = false;
            flow.ActiveFight.Player.TakeDamage(10000, flow.ActiveFight.Boss.gameObject);
            yield return DefeatToMenu(flow);

            // Catalogue: Human + this round's upgrades only; legs never duplicate.
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_LEFT_ARM");
            var leftArm = ChoiceLabels();
            yield return ClickSessionButton("Choice_" + LoadoutRules.ArmBlade);
            yield return ClickSessionButton("Region_RIGHT_ARM");
            var rightArm = ChoiceLabels();
            yield return ClickSessionButton("Region_LEFT_LEG");
            var leftLeg = ChoiceLabels();
            yield return ClickSessionButton("Choice_" + LoadoutRules.LegDoubleJump);
            yield return ClickSessionButton("Region_RIGHT_LEG");
            var rightLeg = ChoiceLabels();
            bool duplicateRejected = !flow.TrySetDraftSlot(LoadoutSlot.RightLeg, LoadoutRules.LegDoubleJump);
            yield return ClickSessionButton("Choice_" + LoadoutRules.LegHover);
            yield return ClickSessionButton("Region_TORSO");
            var torso = ChoiceLabels();
            yield return ClickSessionButton("Region_HEAD");
            var head = ChoiceLabels();
            Check("Catalogue lists Human plus this round's upgrades only", leftArm.SequenceEqual(new[] { "Human", "Arm-Blade" })
                && rightArm.SequenceEqual(new[] { "Human", "Rapid Fire" }) && leftLeg.SequenceEqual(new[] { "Human", "Double Jump", "Hover" })
                && rightLeg.SequenceEqual(new[] { "Human", "Hover" }) && torso.SequenceEqual(new[] { "Human", "Time Slow" })
                && head.SequenceEqual(new[] { "Human", "Targeting AI" }),
                string.Join(" / ", new[] { leftArm, rightArm, leftLeg, rightLeg, torso, head }.Select(x => string.Join(",", x))));
            float thirty = flow.Draft.BodyModificationPercent;
            Check("Leg duplicate rejected, other leg untouched", duplicateRejected && flow.Draft.GetSlot(LoadoutSlot.LeftLeg).ItemId == LoadoutRules.LegDoubleJump
                && flow.Draft.GetSlot(LoadoutSlot.RightLeg).ItemId == LoadoutRules.LegHover && thirty == 30, $"body={thirty}%");
            yield return ClickSessionButton("Region_LEFT_LEG");
            yield return ClickSessionButton("Choice_Human");
            float afterHuman = flow.Draft.BodyModificationPercent;
            flow.TrySetDraftSlot(LoadoutSlot.LeftLeg, LoadoutRules.LegDoubleJump);
            flow.TrySetDraftSlot(LoadoutSlot.RightArm, LoadoutRules.ArmFireRate);
            flow.TrySetDraftSlot(LoadoutSlot.Torso, LoadoutRules.TorsoSlow);
            flow.TrySetDraftSlot(LoadoutSlot.Brain, LoadoutRules.BrainAI);
            float full = flow.Draft.BodyModificationPercent;
            flow.TrySetDraftSlot(LoadoutSlot.Brain, ""); // manual aim for the blade checks; 60% still routes to the AI Ending
            Check("Human removes; 0-100% reachable from this catalogue", afterHuman == 20 && full == 100 && flow.Draft.BodyModificationPercent == 60
                && new LoadoutDraft(true).BodyModificationPercent == 0, $"human->{afterHuman}% full={full}% run={flow.Draft.BodyModificationPercent}%");

            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            var f = flow.ActiveFight;
            var arm = f.Player.GetComponent<PlayerCounterArm>();
            var gun = f.Player.GetComponent<PlayerShooting>();
            var sfx = f.GetComponent<SfxSet>();
            string controls = GameObject.Find("Controls").GetComponent<TMP_Text>().text;
            Check("Controls show the blade line and no hidden or reload lines", controls.Contains("E — SLASH / DEFLECT") && !controls.Contains("COUNTER")
                && !controls.Contains("RELOAD") && controls.Contains("SPACE AGAIN — DOUBLE JUMP") && controls.Contains("HOLD SPACE — HOVER")
                && controls.Contains("Q — TIME SLOW") && controls.Contains("MOUSE — AIM"), controls.Replace("\n", " | "));
            Keys(Key.E); yield return Frames(3); Keys(); yield return Frames(2);
            Keys(Key.E); yield return Frames(3); Keys(); yield return Seconds(.4f);
            Check("Practice swing whiffs once per cooldown and cannot reach the Boss", arm.SlashesStarted == 1 && arm.BladeHits == 0 && arm.Deflections == 0
                && sfx.Requested(Sfx.BladeSwing) == 1 && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth && arm.CooldownRemaining > 0, "one swing, no hit");
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "blade door", 20);
            Keys(); yield return Frames(3);
            Check("Door resets the blade", arm.IsBlade && arm.SlashesStarted == 0 && arm.CooldownRemaining == 0 && !arm.IsWindowOpen, "cooldown cleared");

            // Deflection: a real hostile bullet (fixture launch point) is turned around inside the swing and then hurts the Boss.
            f.Player.IsInvulnerable = false;
            var bulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
            Vector2 player = f.Player.transform.position;
            AimAtWorld(player + Vector2.right * 5f, false);
            yield return Frames(2);
            int playerHp = f.Player.CurrentHealth, bossHp = f.BossHealth.CurrentHealth;
            var hostile = Instantiate(bulletPrefab, player + Vector2.right * 6f, Quaternion.identity);
            hostile.Launch(Vector2.left, 12f, 9, f.Boss.gameObject);
            yield return Seconds(.15f);
            Keys(Key.E); yield return Frames(3); Keys();
            yield return Seconds(.45f);
            bool turned = hostile != null && hostile.Deflected && hostile.OwnerLayer == f.Player.gameObject.layer
                && hostile.GetComponent<Rigidbody2D>().linearVelocity.x > 0;
            Check("Swing really deflects the hostile bullet", turned && arm.Deflections == 1 && f.Player.CurrentHealth == playerHp
                && sfx.Requested(Sfx.Counter) >= 1, $"deflected={turned} playerHP={f.Player.CurrentHealth}");
            yield return WeaponsWait(() => hostile == null, 3);
            Check("Deflected bullet damages the Boss without touching gun accuracy", f.BossHealth.CurrentHealth == bossHp - 9
                && gun.ShotsFired == 0 && gun.ShotsHit == 0, $"boss HP {bossHp}->{f.BossHealth.CurrentHealth}");

            // Melee: fixture position next to the Boss; one swing = one hit on the same target.
            f.Player.IsInvulnerable = true; // observation protection only
            yield return Seconds(.6f); // blade cooldown
            Vector2 bossCentre = f.BossHealth.GetComponent<Collider2D>().bounds.center;
            f.PlacePlayer(new Vector2(bossCentre.x - 1.7f - (-9f), 0f));
            yield return Frames(3);
            AimAtWorld(f.BossHealth.GetComponent<Collider2D>().bounds.center, false);
            yield return Frames(2);
            bossHp = f.BossHealth.CurrentHealth;
            Keys(Key.E); yield return Frames(3); Keys();
            yield return Seconds(.5f);
            Check("Blade damages the Boss once per swing, not as a gun hit", arm.BladeHits == 1 && f.BossHealth.CurrentHealth == bossHp - PlayerCounterArm.BladeDamage
                && gun.ShotsFired == 0 && gun.ShotsHit == 0 && gun.AmmoInMagazine == gun.MagazineSize, $"boss HP {bossHp}->{f.BossHealth.CurrentHealth}");

            // Controlled victory at 60% -> AI Ending; Case Closure lists formal endings only.
            yield return ControlledVictory(f);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "victory", 3);
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            bool aiEnding = flow.State == GameSessionFlow.FlowState.Ending && StrictTexts().Contains("AI ENDING");
            yield return ClickSessionButton("EndingContinue");
            yield return Frames(3);
            var run = PoliceArchive.Current.LatestRun;
            Check("60% blade victory: AI Ending, frozen build, zero-shot accuracy", aiEnding && run.Success && run.BodyModificationPercent == 60
                && run.SlotItemId(LoadoutSlot.LeftArm) == LoadoutRules.ArmBlade && run.AccuracyPercent == 0
                && StrictText("Value_LEFT ARM") == "Arm-Blade", $"body={run.BodyModificationPercent}% accuracy={run.AccuracyPercent}%");
            PoliceArchive.Current.UnlockEnding("TEMP_ENDING_UNDER_50"); // an older save entry: kept, never listed
            yield return ClickSessionButton("PoliceArchive");
            yield return ClickSessionButton("ArchiveTab2");
            Check("Case Closure lists formal endings only", StrictTexts().Contains("AI ENDING") && StrictTexts().Length == 5
                && PoliceArchive.Current.UnlockedEndings.Count == 2, string.Join(" | ", StrictTexts()));
            yield return ClickSessionButton("ArchiveBack");
        }
        finally
        {
            StopCoroutine(focus);
            Keys(); Mouse(.5f, .5f, false);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
