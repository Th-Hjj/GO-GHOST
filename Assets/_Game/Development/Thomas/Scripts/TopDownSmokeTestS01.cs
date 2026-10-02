#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Only S01's eight direct checks. Reuses virtual input, reporting and isolated Archive from the existing smoke driver.</summary>
public partial class TopDownSmokeTest
{
    private static void S01Field(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private IEnumerator S01Focus(GameSessionFlow flow)
    {
        while (true)
        {
            if (flow.ActiveFight != null) ReaderFocus.SetValue(flow.ActiveFight.Player.GetComponent<PlayerInputReader>(), true);
            // Do NOT reuse KeepVirtualFocus: it clears release gates, which are part of this test.
            ResendVirtualState();
            yield return null;
        }
    }

    private IEnumerator S01Fresh(GameSessionFlow flow, int tier, bool slow = false)
    {
        Keys(); MouseTo(.8f, .5f);
        if (flow.State == GameSessionFlow.FlowState.Deployment)
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Practice, 3f);
        if (flow.State == GameSessionFlow.FlowState.Practice) flow.RequestEnterBossRoom();
        if (flow.State == GameSessionFlow.FlowState.Combat)
        {
            flow.ActiveFight.Player.IsInvulnerable = false;
            flow.ActiveFight.Player.TakeDamage(100000, flow.ActiveFight.Boss.gameObject);
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Result, 2f);
        }
        if (flow.State == GameSessionFlow.FlowState.Result) flow.RequestContinue();
        S01Field(flow, "permissionPrototypeEnabled", true);
        S01Field(flow, "developmentPermissionTier", tier);
        flow.RequestNewGame();
        if (slow) flow.TrySetDraftSlot(LoadoutSlot.Torso, LoadoutRules.TorsoSlow);
        flow.RequestDeploy();
        yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Practice, 3f);
        flow.RequestEnterBossRoom();
        yield return Frames(3);
    }

    private IEnumerator S01ManualHit(ChandelierEncounter prop)
    {
        AimAtWorld(prop.AimTarget.position, false); yield return Frames(3);
        AimAtWorld(prop.AimTarget.position, true);
        yield return WeaponsWait(() => !prop.IsHanging, 2f);
        MouseTo(.8f, .5f);
        yield return WeaponsWait(() => prop.HasLanded, 2f);
    }

    private IEnumerator S01Permission()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        var ui = flow.GetComponent<MissionUI>();
        var prop = FindAnyObjectByType<ChandelierEncounter>();
        var focus = StartCoroutine(S01Focus(flow));
        Record("S01 only: real virtual-input gun shots and uGUI clicks; controlled death/reposition/projectile probes; isolated Archive. Not a balance test.");
        try
        {
            yield return S01Fresh(flow, 0);
            var reader = flow.ActiveFight.Player.GetComponent<PlayerInputReader>();
            yield return Seconds(1.15f);
            AimAtWorld(reader.transform.position + Vector3.right * 5f, false); yield return Frames(3);
            Check("0 manual/no dialogue", !ui.DialogueVisible && !reader.HasTemporaryAimOverride && reader.AimDirection.x > .99f
                && flow.Deployment.BrainPermissionTier == 0, $"aim={reader.AimDirection}");
            yield return S01ManualHit(prop);
            Check("0 real manual hit and landing", prop.HasLanded && prop.OutcomeCount == 1 && prop.LastActor == ProjectileActor.Player
                && prop.LastPermissionTier == 0, $"landed={prop.HasLanded}, facts={prop.OutcomeCount}");

            yield return S01Fresh(flow, 1, true);
            var fight = flow.ActiveFight;
            reader = fight.Player.GetComponent<PlayerInputReader>();
            var shooting = reader.GetComponent<PlayerShooting>();
            var slow = reader.GetComponent<PlayerTimeSlow>();
            slow.TryActivate();
            Keys(Key.D); AimAtWorld(reader.transform.position + Vector3.right * 5f, true);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<Projectile>("Assets/_Game/Shared/Prefabs/Bullet.prefab");
            var probe = Instantiate(prefab, new Vector3(0, 7, 0), Quaternion.identity);
            probe.Launch(Vector2.right, 2, 0, fight.Boss.gameObject);
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            yield return Frames(2);
            float active = slow.ActiveRemaining, cooldown = slow.CooldownRemaining, elapsed = fight.CombatElapsed;
            var playerPosition = reader.transform.position;
            var bossPosition = fight.Boss.transform.position;
            var bulletPosition = probe.transform.position;
            int shots = shooting.TriggerPulls, hp = fight.Player.CurrentHealth;
            yield return new WaitForSecondsRealtime(1.25f);
            Check("dialogue freezes physics, firing, HP and clocks", Time.timeScale == 0 && slow.ActiveRemaining == active
                && slow.CooldownRemaining == cooldown && fight.CombatElapsed == elapsed && shooting.TriggerPulls == shots
                && reader.transform.position == playerPosition && fight.Boss.transform.position == bossPosition
                && probe != null && probe.transform.position == bulletPosition && fight.Player.CurrentHealth == hp,
                $"active={active:0.000}/{slow.ActiveRemaining:0.000}, cooldown={cooldown:0.000}/{slow.CooldownRemaining:0.000}, elapsed={elapsed:0.000}/{fight.CombatElapsed:0.000}");
            Keys(); MouseTo(.8f, .5f);
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/s01-dialogue.png"); yield return Frames(3);
            yield return ClickSessionButton("DialoguePanel");
            AimAtWorld(reader.transform.position + Vector3.right * 5f, false); yield return Frames(3);
            Check("T1 restores manual control only", !ui.DialogueVisible && !reader.HasTemporaryAimOverride && !reader.AutomaticFireRequested
                && reader.AimDirection.x > .99f && Time.timeScale == 1f && shooting.TriggerPulls == shots,
                $"aim={reader.AimDirection}, pulls={shooting.TriggerPulls}");
            yield return S01ManualHit(prop);
            Check("T1 manual projectile releases prop", prop.HasLanded && prop.LastActor == ProjectileActor.Player && prop.LastPermissionTier == 1, $"landed={prop.HasLanded}");

            yield return S01Fresh(flow, 2);
            fight = flow.ActiveFight; reader = fight.Player.GetComponent<PlayerInputReader>(); shooting = reader.GetComponent<PlayerShooting>();
            AimAtWorld(reader.transform.position + Vector3.right * 5f, true);
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            shots = shooting.TriggerPulls;
            // Complete the UI callback while old Fire is still down, then separately test a real pointer-close below.
            GameObject.Find("DialoguePanel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            float began = Time.time;
            yield return Frames(3);
            Check("T2 holds aim and rejects pre-dialogue held fire", reader.HasTemporaryAimOverride && reader.OverrideRemaining > 2.7f
                && Vector2.Dot(reader.AimDirection, ((Vector2)prop.AimTarget.position - (Vector2)reader.transform.position).normalized) > .99f
                && shooting.TriggerPulls == shots, $"remaining={reader.OverrideRemaining:0.000}, pulls={shooting.TriggerPulls}/{shots}");
            float startX = reader.transform.position.x;
            Keys(Key.D); yield return Seconds(.2f); Keys();
            Check("T2 retains movement and fire veto", reader.transform.position.x > startX + .7f && shooting.TriggerPulls == shots && prop.IsHanging,
                $"dx={reader.transform.position.x - startX:0.00}, pulls={shooting.TriggerPulls}/{shots}");
            yield return WeaponsWait(() => !reader.HasTemporaryAimOverride, 4f);
            float window = Time.time - began;
            Check("T2 refusal expires at three running seconds", window >= 2.98f && window < 3.2f && prop.IsHanging && shooting.TriggerPulls == shots,
                $"window={window:0.000}, pulls={shooting.TriggerPulls}/{shots}");
            MouseTo(.8f, .5f); yield return Frames(3);
            AimAtWorld(reader.transform.position + Vector3.right * 5f, false); yield return Frames(3);
            Check("T2 restores normal aim", reader.AimDirection.x > .99f && !reader.HasTemporaryAimOverride, $"aim={reader.AimDirection}");

            yield return S01Fresh(flow, 2);
            fight = flow.ActiveFight; reader = fight.Player.GetComponent<PlayerInputReader>(); shooting = reader.GetComponent<PlayerShooting>();
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            shots = shooting.TriggerPulls;
            yield return ClickSessionButton("DialoguePanel");
            Check("T2 closing click does not fire", shooting.TriggerPulls == shots && prop.IsHanging, $"pulls={shooting.TriggerPulls}");
            // Mouse points away; a new real trigger is resolved along the single temporary aim output.
            AimAtWorld(reader.transform.position + Vector3.right * 5f, true);
            yield return WeaponsWait(() => !prop.IsHanging, 2f);
            MouseTo(.8f, .5f);
            yield return WeaponsWait(() => prop.HasLanded, 2f);
            Check("T2 fresh press really hits locked target", prop.HasLanded && prop.LastActor == ProjectileActor.Player
                && prop.LastPermissionTier == 2 && prop.OutcomeCount == 1 && shooting.TriggerPulls > shots,
                $"landed={prop.HasLanded}, actor={prop.LastActor}, pulls={shooting.TriggerPulls}");

            yield return S01Fresh(flow, 3);
            fight = flow.ActiveFight; reader = fight.Player.GetComponent<PlayerInputReader>(); shooting = reader.GetComponent<PlayerShooting>();
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            int ammo = shooting.AmmoInMagazine;
            yield return ClickSessionButton("DialoguePanel");
            Check("T3 second approved line still pauses", ui.DialogueVisible && fight.NarrativePaused && shooting.TriggerPulls == 0,
                "sequential dialogue; no fire before final close");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/s01-t3-dialogue.png"); yield return Frames(3);
            yield return ClickSessionButton("DialoguePanel");
            yield return WeaponsWait(() => prop.HasLanded, 3f);
            Check("T3 autonomous real projectile/ammo/result", prop.HasLanded && prop.LastActor == ProjectileActor.InnerAI && prop.LastPermissionTier == 3
                && prop.OutcomeCount == 1 && shooting.TriggerPulls > 0 && shooting.AmmoInMagazine == ammo - shooting.TriggerPulls,
                $"landed={prop.HasLanded}, actor={prop.LastActor}, ammo={ammo}->{shooting.AmmoInMagazine}, pulls={shooting.TriggerPulls}");

            // Controlled landing and projectile probes establish collision/blocking, not natural combat balance.
            fight.Boss.enabled = false;
            foreach (var bullet in fight.GetComponentsInChildren<Projectile>()) bullet.Retire();
            var body = prop.GetComponent<BoxCollider2D>();
            var playerBody = reader.GetComponent<Rigidbody2D>();
            playerBody.position = new Vector2(prop.transform.position.x, body.bounds.max.y + 1.8f);
            playerBody.linearVelocity = Vector2.zero;
            yield return Seconds(.8f);
            Check("landed prop supports player", reader.GetComponent<PlayerMovement>().IsGrounded
                && Mathf.Abs(reader.GetComponent<Collider2D>().bounds.min.y - body.bounds.max.y) < .15f,
                $"feet={reader.GetComponent<Collider2D>().bounds.min.y:0.00}, surface={body.bounds.max.y:0.00}");
            ScreenCapture.CaptureScreenshot("Logs/GO_GHOST_VS/s01-standing.png"); yield return Frames(3);
            playerBody.position = new Vector2(prop.transform.position.x - 2f, -1.69f); playerBody.linearVelocity = Vector2.zero;
            hp = fight.Player.CurrentHealth;
            int blocked = prop.BlockedProjectiles;
            probe = Instantiate(prefab, new Vector3(prop.transform.position.x + 3f, prop.transform.position.y, 0), Quaternion.identity);
            probe.Launch(Vector2.left, 12, 9, fight.Boss.gameObject);
            yield return Seconds(.45f);
            Check("explicit cover consumes hostile projectile", probe == null && prop.BlockedProjectiles == blocked + 1 && fight.Player.CurrentHealth == hp,
                $"blocked={prop.BlockedProjectiles}, HP={hp}/{fight.Player.CurrentHealth}");
            var terrain = GameObject.Find("Arena/Wall_Bottom").GetComponent<Collider2D>();
            probe = Instantiate(prefab, new Vector3(10f, terrain.bounds.max.y + 1f, 0), Quaternion.identity);
            probe.Launch(Vector2.down, 8, 0, fight.Boss.gameObject);
            yield return Seconds(.3f);
            Check("unmarked terrain still passes bullets", probe != null && probe.transform.position.y < terrain.bounds.min.y,
                $"bulletY={(probe != null ? probe.transform.position.y : 999):0.00}, terrainBottom={terrain.bounds.min.y:0.00}");

            yield return S01Fresh(flow, 2);
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            reader = flow.ActiveFight.Player.GetComponent<PlayerInputReader>();
            flow.ActiveFight.Player.TakeDamage(100000, flow.ActiveFight.Boss.gameObject);
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Result, 2f);
            Check("death during dialogue restores pause", Time.timeScale == 1f && !ui.DialogueVisible && !reader.HasTemporaryAimOverride
                && !flow.ActiveFight.NarrativePaused && flow.State == GameSessionFlow.FlowState.Result, $"scale={Time.timeScale}, state={flow.State}");
            yield return S01Fresh(flow, 2);
            yield return WeaponsWait(() => ui.DialogueVisible, 2f);
            yield return ClickSessionButton("DialoguePanel");
            reader = flow.ActiveFight.Player.GetComponent<PlayerInputReader>();
            flow.ActiveFight.Player.TakeDamage(100000, flow.ActiveFight.Boss.gameObject);
            yield return WeaponsWait(() => flow.State == GameSessionFlow.FlowState.Result, 2f);
            Check("death during aim override clears ownership", !reader.HasTemporaryAimOverride && !reader.AutomaticFireRequested && Time.timeScale == 1f,
                $"override={reader.HasTemporaryAimOverride}, scale={Time.timeScale}");
            yield return S01Fresh(flow, 0);
            yield return Seconds(1.1f);
            reader = flow.ActiveFight.Player.GetComponent<PlayerInputReader>();
            Check("redeploy resets prop, dialogue and aim", prop.IsHanging && prop.OutcomeCount == 0 && !reader.HasTemporaryAimOverride
                && !reader.NarrativePaused && !ui.DialogueVisible && Time.timeScale == 1f, "new immutable tier 0 deployment, no old intervention");
        }
        finally
        {
            StopCoroutine(focus);
            Keys(); MouseTo(.8f, .5f);
            flow.ActiveFight?.CancelCombat();
        }
    }
}
#endif
