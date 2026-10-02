#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Air Dash focused smoke: virtual input, isolated Archive. One scenario, eight rules.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator Tap(Key key, params Key[] held)
    {
        Keys(held.Append(key).ToArray()); yield return Frames(2);
        Keys(held); yield return Frames(1);
    }

    private IEnumerator AirDash()
    {
        Record("Air Dash smoke: virtual keyboard, isolated Archive. TEST values (dash 3 units / .18 s, shared .8 s cooldown).");
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        try
        {
            // 1. Base Human: no dash in the air, ground dash unchanged.
            yield return WeaponsPracticeStart(flow);
            var f = flow.ActiveFight;
            var dodge = f.Player.GetComponent<PlayerDodge>();
            var move = f.Player.GetComponent<PlayerMovement>();
            yield return Tap(Key.Space);
            yield return Seconds(.15f);
            float last = dodge.LastDodgeStartedAt;
            bool airborne = !move.IsGrounded;
            yield return Tap(Key.LeftShift);
            yield return Frames(2);
            Check("1 Human cannot dash in the air", airborne && !dodge.HasAirDash && dodge.LastDodgeStartedAt == last && !dodge.IsDodging, "Shift in air ignored");
            yield return WeaponsWait(() => move.IsGrounded, 2);
            yield return Frames(2);
            yield return Tap(Key.LeftShift);
            Check("  Human ground dash still works", dodge.LastDodgeStartedAt > last, "ground Shift dashes");
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "first door", 20);
            Keys(); yield return Frames(3);
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return DefeatToMenu(flow);

            // 7 / 8. Catalogue, +10%, no duplicate.
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_LEFT_LEG");
            var leftLeg = ChoiceLabels();
            yield return ClickSessionButton("Choice_" + LoadoutRules.LegAirDash);
            float withAirDash = flow.Draft.BodyModificationPercent;
            yield return ClickSessionButton("Region_RIGHT_LEG");
            var rightLeg = ChoiceLabels();
            bool duplicateRejected = !flow.TrySetDraftSlot(LoadoutSlot.RightLeg, LoadoutRules.LegAirDash);
            Check("7 Air Dash adds 10% Cyberization", leftLeg.SequenceEqual(new[] { "Human", "Double Jump", "Hover", "Air Dash" }) && withAirDash == 10
                && flow.Draft.GetSlot(LoadoutSlot.LeftLeg).ItemId == LoadoutRules.LegAirDash, $"{string.Join(",", leftLeg)} -> {withAirDash}%");
            Check("8 Air Dash cannot be installed on both legs", duplicateRejected && rightLeg.SequenceEqual(new[] { "Human", "Double Jump", "Hover" })
                && string.IsNullOrEmpty(flow.Draft.GetSlot(LoadoutSlot.RightLeg).ItemId), string.Join(",", rightLeg));
            flow.TrySetDraftSlot(LoadoutSlot.RightLeg, LoadoutRules.LegDoubleJump); // only to make one airtime longer than the cooldown
            yield return ClickSessionButton("BeginOperation");
            yield return WeaponsPracticeStart(flow);
            f = flow.ActiveFight;
            dodge = f.Player.GetComponent<PlayerDodge>();
            move = f.Player.GetComponent<PlayerMovement>();
            string controls = GameObject.Find("Controls").GetComponent<TMP_Text>().text;
            Check("  Controls show both dash lines", controls.Contains("LEFT SHIFT — DODGE") && controls.Contains("LEFT SHIFT IN AIR — AIR DASH"), controls.Replace("\n", " | "));

            // 2 / 3. One dash per airtime, even after the cooldown has passed.
            yield return Tap(Key.Space);
            yield return Seconds(.2f);
            last = dodge.LastDodgeStartedAt;
            float x0 = f.Player.transform.position.x;
            airborne = !move.IsGrounded;
            yield return Tap(Key.LeftShift);
            bool dashed = dodge.LastDodgeStartedAt > last && dodge.IsDodging;
            float firstAirDash = dodge.LastDodgeStartedAt;
            yield return Seconds(.25f);
            Check("2 Air Dash build dashes in the air", airborne && dashed && !dodge.AirDashAvailable && Mathf.Abs(f.Player.transform.position.x - x0) > 2f,
                $"moved {f.Player.transform.position.x - x0:0.00} while airborne");
            yield return Tap(Key.Space); // double jump keeps the officer up past the cooldown
            yield return WeaponsWait(() => Time.time - firstAirDash > .9f, 2);
            airborne = !move.IsGrounded;
            yield return Tap(Key.LeftShift);
            yield return Frames(2);
            Check("3 Second Shift in the same airtime does nothing", airborne && Time.time - firstAirDash > .8f && dodge.LastDodgeStartedAt == firstAirDash,
                $"still airborne {Time.time - firstAirDash:0.00}s after the dash (cooldown .8s over)");

            // 4. Landing restores the air use.
            yield return WeaponsWait(() => move.IsGrounded, 3);
            yield return Seconds(.15f);
            bool restored = dodge.AirDashAvailable;
            yield return Tap(Key.Space);
            yield return Seconds(.2f);
            last = dodge.LastDodgeStartedAt;
            yield return Tap(Key.LeftShift);
            Check("4 Landing and jumping again restores Air Dash", restored && dodge.LastDodgeStartedAt > last, "second airtime dashes");

            // 5. Landing does not reset the shared cooldown.
            float secondAirDash = dodge.LastDodgeStartedAt;
            yield return WeaponsWait(() => move.IsGrounded && !dodge.IsDodging, 2);
            float sinceDash = Time.time - secondAirDash;
            yield return Tap(Key.LeftShift);
            yield return Frames(2);
            Check("5 Landed inside the cooldown: no immediate dash", sinceDash < .8f && dodge.AirDashAvailable && dodge.LastDodgeStartedAt == secondAirDash,
                $"landed {sinceDash:0.00}s after the dash");

            // 6. Practice -> Boss restores the air use and the cooldown.
            Keys(Key.D);
            yield return WeaponsWait(() => f.Player.transform.position.x > -30.5f, 6);
            yield return Tap(Key.Space, Key.D);
            yield return Seconds(.12f);
            yield return Tap(Key.LeftShift, Key.D);
            float doorDash = dodge.LastDodgeStartedAt;
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "air dash door", 10);
            Keys(); yield return Frames(2);
            yield return Tap(Key.Space);
            yield return Seconds(.12f);
            yield return Tap(Key.LeftShift);
            Check("6 Boss room starts with Air Dash and cooldown restored", doorDash > secondAirDash && dodge.LastDodgeStartedAt > doorDash
                && dodge.LastDodgeStartedAt - doorDash < .8f, $"dashed again {dodge.LastDodgeStartedAt - doorDash:0.00}s after the door dash");
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
