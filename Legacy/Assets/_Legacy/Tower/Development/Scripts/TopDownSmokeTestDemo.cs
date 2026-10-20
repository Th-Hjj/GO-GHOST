#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class TopDownSmokeTest
{
    private IEnumerator ClassDemo()
    {
        var flow=FindAnyObjectByType<GameSessionFlow>();
        Record("Class demo smoke: actual Play Mode virtual input/uGUI; controlled victory is only flow evidence.");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);FocusV2(flow);yield return Frames(3);
        var f=flow.ActiveFight;f.Boss.StopCombat();
        CheckPolishArena(flow);CheckPolishColors(flow,"demo Human");yield return PolishScreen("demo-start");
        yield return RateAndMovementV2(flow,"demo Human");yield return VerifyManualAim(flow);
        var rb=f.Player.GetComponent<Rigidbody2D>();
        Keys(Key.Space);yield return Seconds(.12f);Keys();
        Check("demo Space jump",rb.linearVelocity.y>5,"actual upward velocity="+rb.linearVelocity.y);
        yield return Seconds(.85f);yield return MeasureDodgeInterval(flow,.8f,"demo grounded Shift");
        yield return VerifyOneBullet(flow,12,"demo LMB");
        yield return PressureV2(flow);
        Check("demo actors remain inside enlarged arena",Mathf.Abs(f.Player.transform.position.x)<15 && Mathf.Abs(f.Boss.transform.position.x)<15,"after four-skill cycles and physical collisions");
        FocusV2(flow);f.Boss.BeginCombat();int outcomes=0;f.OnResolved+=_=>outcomes++;
        float deadline=Time.time+45;
        while(flow.State==GameSessionFlow.FlowState.Combat && Time.time<deadline)yield return null;
        Check("demo real Boss kill",flow.State==GameSessionFlow.FlowState.DefeatTransition && f.Player.IsDead && outcomes==1,"no forced Player damage; one outcome");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Loadout);
        Check("demo Officer 2 clean Loadout",flow.OfficerIndex==2 && flow.ActiveFight==null && FindObjectsByType<Projectile>().Length==0,"old actors/projectiles disposed");
        while(!flow.CanDeploy)yield return null;
        foreach(var button in new[]{"LeftArmButton","RightLegButton","TorsoButton","BrainButton"})yield return ClickSessionButton(button);
        Check("demo one Arm one Leg Torso Brain",flow.Draft.BodyModificationPercent==80,"10+10+20+40=80%");
        yield return PolishScreen("demo-loadout");yield return DeployAndFocus(flow);
        f=flow.ActiveFight;f.Boss.StopCombat();CheckPolishColors(flow,"demo 80%");
        Check("demo fresh full-health deployment",f.Player.CurrentHealth==f.Player.MaxHealth && f.BossHealth.CurrentHealth==800 && flow.Deployment.ShotsPerSecond==7 && flow.Deployment.MoveSpeed==8,"fresh combat; snapshot 7 shots/s, move 8");
        yield return RateAndMovementV2(flow,"demo one Arm one Leg");yield return BrainV2(flow);
        rb=f.Player.GetComponent<Rigidbody2D>();rb.position=new Vector2(-9,-1.69f);f.Boss.GetComponent<Rigidbody2D>().position=new Vector2(9,-1.7f);
        FocusV2(flow);yield return Frames(3);yield return TorsoV2(flow);
        Record("CONTROLLED VICTORY: existing health API to validate result/New Game only; not a human win.");
        f.BossHealth.TakeDamage(f.BossHealth.MaxHealth,f.Player.gameObject);
        yield return WaitForState(flow,GameSessionFlow.FlowState.Result);yield return PolishScreen("demo-result");
        Check("demo victory record",flow.Victory.OfficerIndex==2 && flow.Victory.BodyModificationPercent==80,"winning deployment copied");
        while(!flow.CanNewGame)yield return null;yield return ClickSessionButton("NewGameButton");
        yield return WaitForState(flow,GameSessionFlow.FlowState.Combat);FocusV2(flow);flow.ActiveFight.Boss.StopCombat();
        CheckPolishColors(flow,"demo New Game");
        Check("demo New Game reset",flow.OfficerIndex==1 && flow.Deployment.BodyModificationPercent==0 && flow.Deployment.Aim==AimControl.Manual
            && !flow.AugmentationsUnlocked && flow.Victory==null && flow.ActiveFight.Player.CurrentHealth==100 && flow.ActiveFight.BossHealth.CurrentHealth==800
            && !flow.ActiveFight.Player.GetComponent<PlayerTimeSlow>().IsActive,"Human/manual/full health, no old session or ability state");
        yield return PolishScreen("demo-reset");
    }

    private IEnumerator ContinueFlow()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>();
        Record("Continue flow: real uGUI pointer clicks; controlled outcomes test navigation, not combat balance.");
        yield return CheckContinuePage(flow, GameSessionFlow.FlowState.NewGameInitialization, GameSessionFlow.FlowState.Deployment);
        yield return CheckContinuePage(flow, GameSessionFlow.FlowState.Deployment, GameSessionFlow.FlowState.Combat);
        var fight = flow.ActiveFight;
        fight.Boss.StopCombat();
        Check("combat rejects Continue", !flow.RequestContinue(), "Continue cannot change a running fight");
        Check("Continue click does not fire", FindObjectsByType<Projectile>().Length == 0, "no click-through projectile");

        fight.Player.TakeDamage(fight.Player.MaxHealth, fight.Boss.gameObject);
        yield return Frames(3);
        yield return CheckContinuePage(flow, GameSessionFlow.FlowState.DefeatTransition, GameSessionFlow.FlowState.NextOfficerTransition);
        yield return CheckContinuePage(flow, GameSessionFlow.FlowState.NextOfficerTransition, GameSessionFlow.FlowState.Loadout);
        Check("Loadout immediately ready", flow.StateAge < 8f && flow.CanDeploy && flow.CanNewGame && !flow.CanContinue,
            "no minimum read window");
        Check("previous attempt cleared", flow.ActiveFight == null && flow.OfficerIndex == 2 && FindObjectsByType<Projectile>().Length == 0,
            "one handoff, clean loadout");
        yield return ClickSessionButton("LeftArmButton");
        Check("Loadout immediately editable", flow.Draft.BodyModificationPercent == 10f, "real install click before eight seconds");
        yield return PolishScreen("continue-loadout");
        yield return ClickSessionButton("DeployButton");
        Check("Deploy immediately accepted", flow.State == GameSessionFlow.FlowState.Deployment, "no eight-second gate");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        Check("deployment can continue early", flow.State == GameSessionFlow.FlowState.Combat, "no countdown before combat");
        fight = flow.ActiveFight;
        fight.Boss.StopCombat();
        fight.BossHealth.TakeDamage(fight.BossHealth.MaxHealth, fight.Player.gameObject);
        yield return Frames(3);
        yield return CheckContinuePage(flow, GameSessionFlow.FlowState.VictoryTransition, GameSessionFlow.FlowState.Result);
        Check("Result immediately ready", flow.StateAge < 8f && flow.CanNewGame && !flow.CanContinue,
            "NEW GAME available without a countdown");
        Check("winning record preserved", flow.Victory.OfficerIndex == 2 && flow.Victory.BodyModificationPercent == 10f,
            "record matches the winning deployment");
        yield return PolishScreen("continue-result");
        yield return ClickSessionButton("NewGameButton");
        Check("New Game immediately accepted", flow.State == GameSessionFlow.FlowState.NewGameInitialization
            && flow.OfficerIndex == 1 && !flow.AugmentationsUnlocked && flow.Victory == null, "fresh session before eight seconds");
        Check("Continue consumes one request", flow.RequestContinue() && !flow.RequestContinue(), "duplicate request rejected on the same page");
        yield return Frames(3);
        Check("duplicate did not skip deployment", flow.State == GameSessionFlow.FlowState.Deployment, "next page needs its own click");
        Check("early deployment Continue accepted", flow.RequestContinue() && !flow.RequestContinue(), "click is latched during physics settling");
        yield return WaitForState(flow, GameSessionFlow.FlowState.Combat);
        flow.ActiveFight.Boss.StopCombat();
        Check("fresh combat ready", flow.Deployment.BodyModificationPercent == 0f && flow.Deployment.Aim == AimControl.Manual
            && flow.ActiveFight.Player.CurrentHealth == 100 && flow.ActiveFight.BossHealth.CurrentHealth == 800,
            "Human, manual aim, full health after New Game");
    }

    private IEnumerator CheckContinuePage(GameSessionFlow flow, GameSessionFlow.FlowState page, GameSessionFlow.FlowState next)
    {
        yield return Frames(3);
        var button = GameObject.Find("DeployButton").GetComponent<UnityEngine.UI.Button>();
        Check("Continue available " + page, flow.State == page && flow.CanContinue && button.interactable
            && button.GetComponentInChildren<TMPro.TMP_Text>().text == "CONTINUE", "visible and ready immediately");
        yield return PolishScreen("continue-" + page);
        yield return new WaitForSecondsRealtime(8.2f);
        Check("no automatic advance " + page, flow.State == page && flow.CanContinue, "still waiting after 8.2 seconds");
        if (flow.ActiveFight != null)
            Check("gameplay blocked " + page, !flow.ActiveFight.Player.GetComponent<PlayerInputReader>().GameplayInputEnabled,
                "combat input stays disabled throughout the page");
        yield return ClickSessionButton("DeployButton");
        Check("Continue advances " + page, flow.State == next, "one real pointer click -> " + flow.State);
    }

    private static double continueBatchDeadline, continueBatchStart;
    private static bool continueBatchStarted;

    // Invoke with Unity -batchmode -executeMethod TopDownSmokeTest.RunContinueFlowBatch (without -quit).
    public static void RunContinueFlowBatch()
    {
        RunSmokeBatch("continue-flow");
    }

    public static void RunBossContactBatch() => RunSmokeBatch("boss-contact");

    private static void RunSmokeBatch(string scenario)
    {
        UnityEditor.SessionState.SetString("GO_GHOST.BatchScenario", scenario);
        UnityEditor.SessionState.SetBool("GO_GHOST.ContinueBatch", true);
        ResumeContinueFlowBatch();
    }

    [UnityEditor.InitializeOnLoadMethod]
    private static void ResumeContinueFlowBatch()
    {
        if (!UnityEditor.SessionState.GetBool("GO_GHOST.ContinueBatch", false)) return;
        // Batch startup may reload assemblies after executeMethod returns; start after that refresh.
        continueBatchStarted = false;
        continueBatchStart = UnityEditor.EditorApplication.timeSinceStartup + 5;
        continueBatchDeadline = UnityEditor.EditorApplication.timeSinceStartup + 150;
        UnityEditor.EditorApplication.update -= FinishContinueFlowBatch;
        UnityEditor.EditorApplication.update += FinishContinueFlowBatch;
    }

    private static void FinishContinueFlowBatch()
    {
        if (!continueBatchStarted)
        {
            if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating
                || UnityEditor.EditorApplication.timeSinceStartup < continueBatchStart) return;
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.EditorApplication.isPlaying = false;
                return;
            }
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Legacy/Tower/Scenes/Cyberpunk World.unity");
            Done = false;
            continueBatchStarted = true;
            EnterPlayModeAndLaunch(UnityEditor.SessionState.GetString("GO_GHOST.BatchScenario", "continue-flow"));
            return;
        }
        bool timeout = UnityEditor.EditorApplication.timeSinceStartup > continueBatchDeadline;
        if (!Done && !timeout) return;
        UnityEditor.EditorApplication.update -= FinishContinueFlowBatch;
        UnityEditor.SessionState.EraseBool("GO_GHOST.ContinueBatch");
        UnityEditor.SessionState.EraseString("GO_GHOST.BatchScenario");
        bool failed = timeout || Results.Exists(line => line.StartsWith("FAIL")) || Results.Count == 0;
        UnityEditor.EditorApplication.Exit(failed ? 1 : 0);
    }
}
#endif
