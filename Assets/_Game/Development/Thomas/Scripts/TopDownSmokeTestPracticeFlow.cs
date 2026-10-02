#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Practice room -> door -> Boss smoke. Virtual input, controlled outcomes, isolated Archive; flow evidence only.</summary>
public partial class TopDownSmokeTest
{
    private IEnumerator PracticeFlow()
    {
        Record("Practice flow smoke: virtual keyboard/mouse, controlled defeat/victory, isolated Archive. Not balance evidence.");
        string officialBefore = File.Exists(PoliceArchive.SavePath) ? File.ReadAllText(PoliceArchive.SavePath) : null;
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var store = (PoliceArchiveStore)typeof(PoliceArchive).GetField("store", ArchiveFields).GetValue(null);
        var states = new List<GameSessionFlow.FlowState>();
        System.Action<GameSessionFlow.FlowState> watch = s => states.Add(s);
        flow.OnStateChanged += watch;
        var focus = StartCoroutine(KeepVirtualFocus(flow));
        var uiSfx = flow.GetComponent<SfxSet>();
        try
        {
            // Cyberization % is the existing BodyModificationPercent: six current body slots, 0-100, Weapon never counted.
            var probe = new LoadoutDraft(true);
            float emptyPercent = probe.BodyModificationPercent;
            probe.TrySetSlot(LoadoutSlot.LeftArm, LoadoutRules.ArmFireRate); probe.TrySetSlot(LoadoutSlot.RightArm, LoadoutRules.ArmFireRate);
            probe.TrySetSlot(LoadoutSlot.LeftLeg, LoadoutRules.LegMoveSpeed); probe.TrySetSlot(LoadoutSlot.RightLeg, LoadoutRules.LegHover);
            probe.TrySetSlot(LoadoutSlot.Torso, LoadoutRules.TorsoSlow); probe.TrySetSlot(LoadoutSlot.Brain, LoadoutRules.PredictiveCortex);
            float fullPercent = probe.BodyModificationPercent;
            probe.TrySetSlot(LoadoutSlot.Brain, LoadoutRules.BrainAI);
            float replacedPercent = probe.BodyModificationPercent;
            probe.TrySetSlot(LoadoutSlot.Brain, "");
            var probeSnapshot = probe.Deploy(1);
            Check("Cyberization is 0-100 from current body slots only", emptyPercent == 0 && fullPercent == 100 && replacedPercent == 100
                && probeSnapshot.BodyModificationPercent == 60 && probeSnapshot.WeaponId == LoadoutRules.AssaultRifle
                && new VictoryRecord(probeSnapshot).BodyModificationPercent == 60
                && PoliceArchive.EndingFor(49.9f) == PoliceArchive.HumanEnding && PoliceArchive.EndingFor(50f) == PoliceArchive.AiEnding
                && PoliceArchive.EndingFor(100f) == PoliceArchive.AiEnding && PoliceArchive.EndingFor(0f) == PoliceArchive.HumanEnding,
                $"empty={emptyPercent} full={fullPercent} replaced={replacedPercent} brainRemoved={probeSnapshot.BodyModificationPercent}");

            // 1. Launch goes straight to practice.
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Practice, "launch practice", 5);
            yield return Frames(3);
            var f = flow.ActiveFight;
            var officer = flow.Officer;
            var deployment = flow.Deployment;
            string baseControls = "A / D — MOVE\nMOUSE — AIM\nLEFT MOUSE — SHOOT\nSPACE — JUMP\nLEFT SHIFT — DODGE";
            Check("Launch enters practice without menus", GameObject.Find("MainMenu") == null && GameObject.Find("Upgrade") == null
                && GameObject.Find("CombatHUD") == null, "no menu or HUD");
            Check("Base controls text is exact; only it and the ammo readout are shown", StrictTexts().OrderBy(x => x).SequenceEqual(new[] { baseControls, "24 / 24" }.OrderBy(x => x)), string.Join(" / ", StrictTexts()).Replace("\n", " | "));
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/practice-flow-base.png");
            Check("Launch uses default human build and one officer", officer != null && deployment.BodyModificationPercent == 0
                && deployment.GetSlot(LoadoutSlot.Weapon).ItemId == LoadoutRules.AssaultRifle && PoliceArchive.Current.RunRecords.Count == 0,
                $"officer #{officer?.OfficerNumber} body={deployment.BodyModificationPercent}%");
            Check("Practice has no live Boss", !f.Boss.gameObject.activeInHierarchy && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth
                && f.Result == BossFight.Outcome.Fighting, "boss inactive, full HP");
            Check("Practice camera and officer are in the practice room", Mathf.Abs(Camera.main.transform.position.x + 40f) < .01f
                && Mathf.Abs(f.Player.transform.position.x + 49f) < .5f, $"cam={Camera.main.transform.position.x:0.0} player={f.Player.transform.position.x:0.0}");

            // 2. Real controls in practice.
            var reader = f.Player.GetComponent<PlayerInputReader>();
            var shooting = f.Player.GetComponent<PlayerShooting>();
            var dodge = f.Player.GetComponent<PlayerDodge>();
            reader.SendMessage("OnApplicationFocus", true);
            Keys(); MouseTo(.5f, .9f);
            yield return Frames(3);
            float x0 = f.Player.transform.position.x;
            Keys(Key.D); yield return Seconds(.5f);
            Check("Practice move", f.Player.transform.position.x > x0 + .5f, $"dx={f.Player.transform.position.x - x0:0.00}");
            float y0 = f.Player.transform.position.y;
            Keys(Key.D, Key.Space); yield return Seconds(.2f);
            Check("Practice jump", f.Player.transform.position.y > y0 + .2f, $"dy={f.Player.transform.position.y - y0:0.00}");
            Keys(Key.D); yield return Seconds(.8f);
            float dodgeBefore = dodge.LastDodgeStartedAt;
            Keys(Key.D, Key.LeftShift); yield return Frames(3);
            Keys(Key.D); yield return Seconds(.4f);
            Check("Practice dodge", dodge.LastDodgeStartedAt > dodgeBefore, "dodge started");
            Mouse(.2f, .8f, true); yield return Seconds(.8f);
            int practiceShots = shooting.ShotsFired;
            Check("Practice fire", practiceShots >= 2 && FindObjectsByType<Projectile>().Length > 0, $"shots={practiceShots}");
            Check("Practice cannot hurt the Boss", f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth && shooting.ShotsHit == 0, "boss HP full");
            var sfx = f.GetComponent<SfxSet>();
            yield return Phase8Until(() => f.GetComponent<AudioSource>().isPlaying, "shot voice playing", 2);
            Check("Shot cue once per real shot and audible source", sfx != null && sfx.Requested(Sfx.Shot) == shooting.ShotsFired
                && sfx.Requested(Sfx.BulletHit) == 0 && sfx.Requested(Sfx.PlayerHurt) == 0,
                $"shot cues={sfx?.Requested(Sfx.Shot)} shots={shooting.ShotsFired}");

            Check("Practice muzzle VFX drawn with a supported material", f.transform.Find("AttemptVfx_Transient") != null
                && f.GetComponentInChildren<LineRenderer>() != null && f.GetComponentInChildren<LineRenderer>().sharedMaterial.shader.isSupported, "line effects alive");

            // 3. Walk through the door while still firing.
            float practiceStarted = Time.unscaledTime;
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "walk through door", 15);
            int shotsAtEntry = shooting.ShotsFired;
            int practiceBullets = FindObjectsByType<Projectile>().Count(p => p.transform.position.x < -20f && p.GetComponent<Collider2D>().enabled);
            float doorAt = Time.unscaledTime;
            Keys(); Mouse(.2f, .8f, false);
            yield return Frames(3);
            Check("Door starts the Boss fight once", f == flow.ActiveFight && FindObjectsByType<BossFight>().Length == 1
                && f.Boss.gameObject.activeInHierarchy && ReferenceEquals(flow.Officer, officer) && ReferenceEquals(flow.Deployment, deployment)
                && ReferenceEquals(f.Player.GetComponent<OfficerIdentity>().Deployment, deployment) && !flow.RequestEnterBossRoom(),
                "same fight, officer and deployment; second request rejected");
            Check("Door moves officer and camera to the Boss room", Mathf.Abs(Camera.main.transform.position.x) < .01f
                && Mathf.Abs(f.Player.transform.position.x + 9f) < 3f, $"cam={Camera.main.transform.position.x:0.0} player={f.Player.transform.position.x:0.0}");
            Check("Practice shots are cleared and not counted", shotsAtEntry <= 1 && practiceBullets == 0 && shotsAtEntry < practiceShots
                && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth, $"practice={practiceShots} atEntry={shotsAtEntry} leftover={practiceBullets}");
            Check("Boss HUD only", GameObject.Find("CombatHUD") != null && StrictTexts().Length == 1 && PoliceArchive.Current.RunRecords.Count == 0, "two bars, ammo readout only, no record");

            // 4. Controlled defeat -> Main Menu directly.
            yield return Seconds(.6f);
            f.Player.IsInvulnerable = false;
            f.Player.TakeDamage(10000, f.Boss.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.MainMenu, "defeat to Main Menu", 3);
            yield return Frames(3);
            var defeat = PoliceArchive.Current.LatestRun;
            Check("Defeat skips Result and saves one record", !states.Contains(GameSessionFlow.FlowState.Result) && PoliceArchive.Current.RunRecords.Count == 1
                && !defeat.Success && defeat.OfficerNumber == officer.OfficerNumber && flow.ActiveFight == null, string.Join(">", states));
            Check("Defeat TIME excludes practice", defeat.CombatDuration > 0 && defeat.CombatDuration < Time.unscaledTime - doorAt + .1f
                && defeat.CombatDuration < doorAt - practiceStarted, $"time={defeat.CombatDuration:0.00}s practiceWalk={doorAt - practiceStarted:0.00}s");
            Check("Defeat unlocks no ending", PoliceArchive.Current.UnlockedEndings.Count == 0 && flow.EndingId == null, "no ending on defeat");
            Check("Defeat cues: one hurt, one defeat, combat voice stopped", sfx.Requested(Sfx.PlayerHurt) == 1 && uiSfx.Requested(Sfx.Defeat) == 1
                && uiSfx.Requested(Sfx.Victory) == 0, $"hurt={sfx.Requested(Sfx.PlayerHurt)} defeat={uiSfx.Requested(Sfx.Defeat)}");
            Check("Main Menu shows the saved defeat", StrictText("Value_OFFICER #") == officer.OfficerNumber.ToString()
                && StrictText("Value_STATUS") == "MISSION FAILED" && StrictText("Value_WEAPON") == "Assault Rifle", "latest real record");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/practice-flow-menu.png");

            // 5. Upgrade -> practice with another build -> door.
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("Region_TORSO");
            yield return ClickSessionButton("Choice_" + LoadoutRules.TorsoSlow);
            yield return ClickSessionButton("Choice_" + LoadoutRules.TorsoSlow);
            Check("UI cues: click on buttons, equip only on a real change", flow.Draft.GetSlot(LoadoutSlot.Torso).ItemId == LoadoutRules.TorsoSlow
                && uiSfx.Requested(Sfx.Click) == 2 && uiSfx.Requested(Sfx.Equip) == 1, $"click={uiSfx.Requested(Sfx.Click)} equip={uiSfx.Requested(Sfx.Equip)}");
            flow.TrySetDraftSlot(LoadoutSlot.Brain, LoadoutRules.BrainAI);
            flow.TrySetDraftSlot(LoadoutSlot.LeftLeg, LoadoutRules.LegDoubleJump);
            yield return ClickSessionButton("BeginOperation");
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Practice, "second practice", 5);
            yield return Frames(3);
            f = flow.ActiveFight;
            reader = f.Player.GetComponent<PlayerInputReader>();
            shooting = f.Player.GetComponent<PlayerShooting>();
            Check("Second deployment reaches practice with the new build", flow.Officer.OfficerNumber == officer.OfficerNumber + 1
                && flow.Deployment.GetSlot(LoadoutSlot.Torso).ItemId == LoadoutRules.TorsoSlow && flow.Deployment.Aim == AimControl.AI
                && flow.Deployment.BodyModificationPercent > 0 && !f.Boss.gameObject.activeInHierarchy && PoliceArchive.Current.RunRecords.Count == 1,
                $"officer #{flow.Officer.OfficerNumber} body={flow.Deployment.BodyModificationPercent}%");
            Check("Upgraded controls text matches the build", StrictTexts().Intersect(new[] {
                "A / D — MOVE\nAIM — AUTOMATIC\nLEFT MOUSE — SHOOT\nSPACE — JUMP\nLEFT SHIFT — DODGE\nSPACE AGAIN — DOUBLE JUMP\nQ — TIME SLOW" }).Count() == 1 && StrictTexts().Length == 2,
                string.Join(" / ", StrictTexts()).Replace("\n", " | "));
            reader.SendMessage("OnApplicationFocus", true);
            Keys(); MouseTo(.5f, .9f);
            yield return Frames(3);
            Keys(Key.Q); yield return Frames(3);
            Keys();
            var slow = f.Player.GetComponent<PlayerTimeSlow>();
            Check("Equipped ability works in practice", slow.IsActive && slow.CooldownRemaining > 0, "Q Time Slow active");
            var brackets = f.transform.Find("AttemptVfx_Transient/TimeSlowBrackets");
            Check("Time Slow brackets shown", brackets != null && brackets.gameObject.activeInHierarchy, "VFX-F");
            Vector2 aimBefore = reader.AimDirection;
            Mouse(.2f, .8f, true); yield return Seconds(.5f);
            Check("AI aim has no cross-room target", reader.AimMode == AimControl.AI && reader.AimDirection == aimBefore
                && f.BossHealth.CurrentHealth == f.BossHealth.MaxHealth, $"aim held {reader.AimDirection}");
            Mouse(.2f, .8f, false);
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/practice-flow-room.png");
            yield return Frames(2);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "second door", 20);
            Keys();
            yield return Frames(3);
            Check("Second door starts combat with the same locked build", f == flow.ActiveFight && f.Boss.gameObject.activeInHierarchy
                && ReferenceEquals(f.Player.GetComponent<OfficerIdentity>().Deployment, flow.Deployment) && shooting.ShotsFired == 0,
                "same snapshot, counters reset");
            Check("Door refills resources of the same build", !slow.IsActive && slow.IsEquipped && slow.CooldownRemaining == 0
                && f.Player.CurrentHealth == f.Player.MaxHealth && f.Player.GetComponent<PlayerMovement>().AirJumpsRemaining == 1
                && StrictTexts().Length == 1, $"slow cd={slow.CooldownRemaining:0.0} hp={f.Player.CurrentHealth} airJumps={f.Player.GetComponent<PlayerMovement>().AirJumpsRemaining}");
            sfx = f.GetComponent<SfxSet>();
            Check("Door clears practice VFX", f.transform.Find("AttemptVfx_Transient") == null, "no leftover effects");

            // Bullets pass through platforms: real Projectile physics, fixture launch points behind Low A / Low B.
            var bulletPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
            int bossBefore = f.BossHealth.CurrentHealth;
            Vector2 from = new Vector2(-8f, -.3f);
            var shot = Instantiate(bulletPrefab, from, Quaternion.identity);
            shot.Launch((Vector2)f.BossHealth.GetComponent<Collider2D>().bounds.center - from, 30f, 5, f.Player.gameObject);
            yield return Phase8Until(() => shot == null, "player bullet resolves", 5);
            Check("Player bullet passes Low A and hits the Boss", f.BossHealth.CurrentHealth == bossBefore - 5 && sfx.Requested(Sfx.BulletHit) == 1,
                $"boss HP {bossBefore}->{f.BossHealth.CurrentHealth}");
            f.Player.IsInvulnerable = false;
            int playerBefore = f.Player.CurrentHealth;
            Vector2 hostileFrom = new Vector2(7f, -.25f);
            var hostile = Instantiate(bulletPrefab, hostileFrom, Quaternion.identity);
            hostile.Launch((Vector2)f.Player.transform.position - hostileFrom, 30f, 7, f.Boss.gameObject);
            yield return Phase8Until(() => hostile == null, "boss bullet resolves", 5);
            Check("Boss bullet passes Low B and hits the player", f.Player.CurrentHealth <= playerBefore - 7 && sfx.Requested(Sfx.PlayerHurt) >= 1,
                $"player HP {playerBefore}->{f.Player.CurrentHealth}");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/practice-flow-boss.png");

            // The real Boss keeps running after the door: one P1 shot, then P2 and one blade strike. Observation protection only.
            f.Player.IsInvulnerable = true;
            yield return Phase8Until(() => f.Boss.ShotsFired > 0, "Boss P1 shot", 10);
            Check("Boss shot cue", sfx.Requested(Sfx.Shot) >= f.Boss.ShotsFired && sfx.Requested(Sfx.Glitch) >= 1, $"boss shots={f.Boss.ShotsFired} cues={sfx.Requested(Sfx.Shot)}");
            f.BossHealth.TakeDamage(f.BossHealth.CurrentHealth - f.BossHealth.MaxHealth * 45 / 100, f.Player.gameObject);
            yield return Phase8Until(() => f.Boss.SlashesStarted > 0 && f.Boss.State == CyberpsychoBoss.AttackState.SlashExecute, "P2 blade strike", 15);
            Check("P2 blade cue", f.Boss.Phase == CyberpsychoBoss.BossPhase.Phase2 && sfx.Requested(Sfx.BladeSwing) >= 1, $"blade cues={sfx.Requested(Sfx.BladeSwing)}");
            yield return Frames(2);

            // 6. Controlled victory keeps the existing Result path.
            float body = flow.Deployment.BodyModificationPercent;
            f.BossHealth.TakeDamage(10000, f.Player.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "MISSION SUCCESS", 3);
            yield return Frames(3);
            var victory = PoliceArchive.Current.LatestRun;
            Check("Victory keeps Result, record and TEMP rewards", StrictTexts().Contains("MISSION SUCCESS") && PoliceArchive.Current.RunRecords.Count == 2
                && victory.Success && PoliceArchive.Current.HasAchievement(PoliceArchive.TempFirstVictory)
                && PoliceArchive.Current.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm)
                && PoliceArchive.Current.UnlockedEndings.SequenceEqual(new[] { PoliceArchive.EndingFor(body) }), $"body={body}% ending={PoliceArchive.EndingFor(body)}");
            Check("Combat VFX removed at resolution", f.transform.Find("AttemptVfx_Transient") == null, "EndAttempt");
            Check("Victory cue once, combat voices stopped", uiSfx.Requested(Sfx.Victory) == 1 && uiSfx.Requested(Sfx.Defeat) == 1
                && !f.GetComponent<AudioSource>().isPlaying, $"victory={uiSfx.Requested(Sfx.Victory)}");
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            string aiText = "Cyber Psycho\u2026 and now, AI requires a new savior! Humanity is doomed, and it\u2019s all thanks to us!";
            Check("70% victory shows the AI Ending screen only", flow.State == GameSessionFlow.FlowState.Ending
                && StrictTexts().OrderBy(x => x).SequenceEqual(new[] { "AI ENDING", aiText, "CONTINUE" }.OrderBy(x => x)), string.Join(" | ", StrictTexts()));
            var endingBody = GameObject.Find("EndingText").GetComponent<TMPro.TMP_Text>();
            endingBody.ForceMeshUpdate();
            Check("Ending text renders every character on screen", endingBody.font.HasCharacters(aiText) && !endingBody.isTextOverflowing
                && endingBody.textInfo.lineCount >= 1, $"lines={endingBody.textInfo.lineCount}");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/practice-flow-ending-ai.png");
            yield return Frames(2);
            yield return ClickSessionButton("EndingContinue");
            yield return Frames(3);
            Check("Ending CONTINUE returns to Main Menu", flow.State == GameSessionFlow.FlowState.MainMenu && StrictText("Value_STATUS") == "MISSION SUCCESS"
                && flow.ActiveFight == null, "victory record shown");

            // Third run: default human build, controlled 0% victory -> Human Ending.
            yield return ClickSessionButton("StartMission");
            yield return ClickSessionButton("BeginOperation");
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Practice, "third practice", 5);
            yield return Frames(3);
            f = flow.ActiveFight;
            f.Player.GetComponent<PlayerInputReader>().SendMessage("OnApplicationFocus", true);
            Keys(); yield return Frames(3);
            Keys(Key.D);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Combat, "third door", 20);
            Keys(); yield return Frames(3);
            f.BossHealth.TakeDamage(10000, f.Player.gameObject);
            yield return Phase8Until(() => flow.State == GameSessionFlow.FlowState.Result, "second victory", 3);
            yield return ClickSessionButton("ResultContinue");
            yield return Frames(3);
            Check("0% victory shows the Human Ending screen only", flow.State == GameSessionFlow.FlowState.Ending
                && StrictTexts().OrderBy(x => x).SequenceEqual(new[] { "HUMAN ENDING", "CONTINUE",
                    "Cyber Psycho is defeated, Humanity lives to see another day! AI shall no longer threaten our existence!" }.OrderBy(x => x)),
                string.Join(" | ", StrictTexts()));
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/practice-flow-ending-human.png");
            yield return Frames(2);
            yield return ClickSessionButton("EndingContinue");
            yield return Frames(3);
            Check("Endings are idempotent and both recorded", flow.State == GameSessionFlow.FlowState.MainMenu
                && PoliceArchive.Current.UnlockedEndings.SequenceEqual(new[] { PoliceArchive.AiEnding, PoliceArchive.HumanEnding })
                && !PoliceArchive.Current.UnlockEnding(PoliceArchive.AiEnding) && PoliceArchive.Current.UnlockedEndings.Count == 2
                && PoliceArchive.Current.RunRecords.Count == 3, string.Join(",", PoliceArchive.Current.UnlockedEndings));
            yield return ClickSessionButton("PoliceArchive");
            yield return ClickSessionButton("ArchiveTab2");
            Check("Case Closure lists ending names only", StrictTexts().Contains("AI ENDING") && StrictTexts().Contains("HUMAN ENDING") && StrictTexts().Length == 6,
                string.Join(" | ", StrictTexts()));
            yield return ClickSessionButton("ArchiveBack");

            var reloaded = new PoliceArchiveStore(store.FilePath).Load();
            Check("Archive reload keeps records and endings", reloaded.RunRecords.Count == 3 && !reloaded.RunRecords[0].Success && reloaded.RunRecords[1].Success
                && reloaded.UnlockedEndings.SequenceEqual(new[] { PoliceArchive.AiEnding, PoliceArchive.HumanEnding })
                && reloaded.HasAchievement(PoliceArchive.TempFirstVictory) && reloaded.HasHiddenAugmentation(LoadoutRules.DetachedDroneArm), store.FilePath);
            Check("Normal profile untouched", (File.Exists(PoliceArchive.SavePath) ? File.ReadAllText(PoliceArchive.SavePath) : null) == officialBefore
                && store.FilePath != PoliceArchive.SavePath, "isolated store");
        }
        finally
        {
            flow.OnStateChanged -= watch;
            StopCoroutine(focus);
            Keys(); Mouse(.5f, .5f, false);
            if (flow != null && flow.ActiveFight != null) flow.ActiveFight.CancelCombat();
        }
    }
}
#endif
