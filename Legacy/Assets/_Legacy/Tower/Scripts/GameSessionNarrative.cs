using System;
using System.Collections;
using UnityEngine;

public sealed partial class GameSessionFlow
{
    private int storyToken;
    public bool PuppeteerIntroduced { get; private set; }
    private Coroutine storyMotion;
    private TowerCharacter focusedCharacter;
    private bool replyWindow;
    private int replyShotsAtOpen;
    private bool replyFireRequested;
    private int maintenancePhase;
    private int securityEntryHealth;
    private TowerRoom revealRoom;
    [Header("Greybox management area (same objects across office/P3/final)")]
    [SerializeField] private Transform managementRoot;
    [SerializeField] private TowerCharacter president;
    [SerializeField] private TowerCharacter continuingCore;
    [SerializeField] private GameObject managementBarrier;
    public TowerCharacter President => president;
    public TowerCharacter ContinuingCore => continuingCore;
    private Action attackReply;
    private MissionUI StoryUI => GetComponent<MissionUI>();
    private PlayerInputReader StoryInput => ActiveFight != null ? ActiveFight.Player.GetComponent<PlayerInputReader>() : null;
    public bool HasInnerAI => Deployment != null && Deployment.BrainPermissionTier >= 1;
    public bool HighAI => Deployment != null && Deployment.BrainPermissionTier >= 2;
    public bool ReplyOrAttackActive => replyWindow;
    public TowerCharacter FocusedCharacter => focusedCharacter;

    private void CancelStory()
    {
        storyToken++;
        replyWindow = false; attackReply = null;
        if (storyMotion != null) StopCoroutine(storyMotion);
        storyMotion = null;
        if (revealRoom != null && revealRoom != CurrentRoom) revealRoom.gameObject.SetActive(false);
        revealRoom = null;
        focusedCharacter = null;
        StoryUI?.HideDialogue();
        StoryUI?.ShowInteractionKey(null);
        if (ActiveFight != null) ActiveFight.SetNarrativePaused(false);
    }

    private void Say(string sequence, Action completed, bool voluntary = false)
    {
        int token = storyToken;
        ActiveFight?.SetNarrativePaused(true);
        if (!StoryUI.ShowNarrative(sequence, (result, _) =>
        {
            if (token != storyToken) return;
            ActiveFight?.SetNarrativePaused(false);
            if (result == MissionUI.DialogueResult.Completed) completed?.Invoke();
        }, voluntary))
        {
            ActiveFight?.SetNarrativePaused(false);
            Debug.LogError("Required narrative could not open: " + sequence, this);
        }
    }

    private void Choose(string[] ids, Action<string> committed, bool response = false, Func<string,string> cue = null)
    {
        int token = storyToken;
        ActiveFight.SetNarrativePaused(!response);
        if (!StoryUI.ShowChoices(ids, (result, id) =>
        {
            if (token != storyToken) return;
            ActiveFight?.SetNarrativePaused(false);
            if (result != MissionUI.DialogueResult.ChoiceCommitted) return;
            replyWindow = false; attackReply = null;
            committed(id);
        }, response, cue))
        {
            ActiveFight.SetNarrativePaused(false);
            Debug.LogError("Required narrative choices could not open.", this);
        }
    }

    private void ReplyOrAttack(string[] ids, Action<string> reply, Action attack)
    {
        // The response callback already presents its authored line. With one response there is no decision UI.
        if (ids.Length == 1) { reply(ids[0]); return; }
        replyWindow = true;
        replyShotsAtOpen = ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls;
        replyFireRequested = false;
        attackReply = attack;
        Choose(ids, reply, true);
    }

    private void BeginNarrativeRoom()
    {
        if (managementRoot != null)
        {
            bool office = TowerStageId == "office_intro" || TowerStageId == "boss_p3" || TowerStageId == "final_disposition";
            managementRoot.gameObject.SetActive(office);
            if (office) managementRoot.position = CurrentRoom.transform.position;
            managementBarrier.SetActive(TowerStageId != "final_disposition");
        }
        switch (TowerStageId)
        {
            case "floor01": Narrative.ConnectMaintenance(CurrentRoom.Character.IsAlive); break;
            case "boss_p3":
                if (towerEvents.Contains("office.summon.delivered")) storyMotion = StartCoroutine(RevealFinalCarrier());
                else Say("office.summon", () => { towerEvents.Add("office.summon.delivered"); storyMotion = StartCoroutine(RevealFinalCarrier()); });
                break;
            case "final_disposition": Narrative.ConnectDispositionWorld(president.IsAlive, continuingCore.IsAlive); break;
            case "boss_p1":
                Say("boss.p1.intro", () => ReplyOrAttack(new[]{"boss.p1.choice.reply"},
                    _ => Say("boss.p1.reply", () => Say("boss.p1.silence", StartP1Opening)), StartP1Opening));
                break;
            case "boss_p2": storyMotion = StartCoroutine(P2Reveal()); break;
            case "floor99":
                securityEntryHealth = ActiveFight.BossHealth.CurrentHealth;
                break;
            case "office_intro": break; // Let the player see the room and approach the President first.
        }
    }

    // Speaker IDs bind to actual actors, never to player-facing internal names.
    public Transform ResolveNarrativeSpeaker(string id)
    {
        if (ActiveFight == null) return null;
        switch (id)
        {
            case "player": case "inner_ai": return ActiveFight.Player.transform;
            case "puppeteer": case "puppeteer_unknown": return TowerStageId == "final_disposition"
                ? continuingCore?.InteractionAnchor : ActiveFight.BossHealth?.transform;
            case "president": return president?.InteractionAnchor;
            case "entrance_worker": case "maintenance_ai": return CurrentRoom?.Character?.InteractionAnchor;
            case "floor50_npc": return CurrentRoom?.Receptionist?.InteractionAnchor;
            case "security99": return ActiveFight.BossHealth?.transform;
            default: return null; // Deliberately unnamed ending captions remain unanchored.
        }
    }

    private void StartP1Opening()
    {
        ActiveFight.StartPreparedEncounter();
        ActiveFight.Boss.BeginOpeningLaser();
        opportunityReadyAt = Time.time + .35f;
    }

    private void StartPreparedFight()
    {
        ActiveFight.StartPreparedEncounter();
        opportunityReadyAt = Time.time + 1f;
    }
    private void OfficeWarning()
    {
        if (!HighAI) { SummonPuppeteer(); return; }
        // Local cinematic pointing only. No autonomous request or transfer of execution permission.
        if (Deployment.BrainPermissionTier == 3) StoryInput.BeginAimOverride(president.InteractionAnchor, 3f, false);
        Say("office.ai_warning", () => storyMotion = StartCoroutine(OfficeAimBeat()));
    }
    private IEnumerator OfficeAimBeat()
    {
        float until = Time.time + .7f;
        while (Time.time < until) yield return null;
        SummonPuppeteer();
    }
    private void SummonPuppeteer()
    {
        StoryInput.EndAimOverride();
        storyMotion = StartCoroutine(SummonAfterShot());
    }
    private IEnumerator SummonAfterShot()
    {
        yield return null; // PlayerShooting consumes this frame's live Fire before the next modal sequence.
        Say("office.summon", () => { towerEvents.Add("office.summon.delivered"); EnterTowerRoom(towerRoomIndex + 1); });
    }
    private IEnumerator P2Reveal()
    {
        int token = storyToken;
        ActiveFight.SetNarrativePaused(true);
        var actor = ActiveFight.Boss.transform;
        var room = CurrentRoom;
        var oldRoom = System.Array.Find(towerRooms, r => r.StageId == "boss_p1");
        revealRoom = oldRoom; oldRoom.gameObject.SetActive(true); SetView(oldRoom.Offset);
        actor.position = oldRoom.BossSpawn; actor.localEulerAngles = new Vector3(0,0,90);
        foreach (var part in ActiveFight.Boss.RoomPresentationParts()) if (part != null) part.color = Color.gray;
        yield return new WaitForSecondsRealtime(.35f); // stopped old carrier / eyes return
        foreach (var part in ActiveFight.Boss.RoomPresentationParts()) if (part != null) part.color = Color.white;
        actor.localEulerAngles = Vector3.zero;
        yield return MovePresentation(actor, oldRoom.transform.position + new Vector3(15,2,0), .4f);
        oldRoom.gameObject.SetActive(false); revealRoom = null; SetView(room.Offset);
        actor.position = room.transform.position + new Vector3(-14,-1.7f,0);
        yield return MovePresentation(actor, room.transform.position + new Vector3(-14,6,0), .45f);
        yield return MovePresentation(actor, room.BossSpawn, .45f);
        if (token != storyToken) yield break;
        ActiveFight.SetNarrativePaused(false);
        Say("boss.p2.introduction", () =>
        {
            PuppeteerIntroduced = true; // Presentation knowledge only, after actual line completion.
            Say(HasInnerAI ? "boss.p2.intro" : "boss.p2.intro.no_ai", () => ReplyOrAttack(
            new[]{"boss.p2.choice.why"}, _ => Say("boss.p2.reply.why", () =>
                { if (HighAI) Say("boss.p2.ai_warning", StartPreparedFight); else StartPreparedFight(); }), StartPreparedFight));
        });
    }
    private IEnumerator MovePresentation(Transform target, Vector3 to, float seconds)
    {
        Vector3 from = target.position;
        float elapsed = 0;
        while (elapsed < seconds && target != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (GetComponent<DeveloperRouteJump>()?.MenuVisible == true) { yield return null; continue; }
#endif
            elapsed += Time.unscaledDeltaTime; target.position = Vector3.Lerp(from,to,Mathf.Clamp01(elapsed/seconds)); yield return null;
        }
    }

    private void TickNarrative()
    {
        if (CurrentRoom == null || ActiveFight == null || ActiveFight.Player.IsDead) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<DeveloperRouteJump>()?.MenuVisible == true) { StoryUI.ShowInteractionKey(null); return; }
#endif
        if (replyWindow && StoryInput.AttackPressedThisFrame) replyFireRequested = true;
        // Observe the existing weapon's successful shot. A press during reload/cadence is not an invisible shot.
        if (replyWindow && replyFireRequested && ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls > replyShotsAtOpen)
        {
            var attack = attackReply;
            replyWindow = false; attackReply = null;
            StoryUI.HideDialogue();
            attack?.Invoke();
            return;
        }
        if (TowerStageId == "floor99" && !towerEvents.Contains("floor99.security.started"))
        {
            var security = ActiveFight.BossHealth;
            // A shot elsewhere on the floor is not an attack on Security. Its real damage endpoint owns this gate.
            if (security == null || security.CurrentHealth < securityEntryHealth)
            { StartFloor99Security(true); return; }
            bool focus = !StoryUI.DialogueVisible && !ActiveFight.NarrativePaused && !security.IsDead
                && Vector2.Distance(security.transform.position, ActiveFight.Player.transform.position) <= 2.4f
                && Vector2.Dot(StoryInput.AimDirection, (Vector2)(security.transform.position - ActiveFight.Player.transform.position)) > 0;
            StoryUI.ShowInteractionKey(focus ? security.transform : null, Vector3.up * 1.4f);
            if (focus && StoryUI.ConsumeInteractionPress()) StartFloor99Security();
            return;
        }
        focusedCharacter = null;
        if (!StoryUI.DialogueVisible && !ActiveFight.NarrativePaused)
        {
            var npc = TowerStageId == "office_intro" ? president : TowerStageId == "floor50" ? CurrentRoom.Receptionist : CurrentRoom.Character;
            bool available = npc != null && npc.IsAlive && (TowerStageId == "office_intro" || TowerStageId == "tower_entry"
                || (TowerStageId == "floor01" && maintenancePhase != 1 && maintenancePhase != 3)
                || (TowerStageId == "floor50" && (!towerEvents.Contains("floor50.security.started") || CurrentRoom.ExitOpen)));
            if (available && Vector2.Distance(npc.transform.position, ActiveFight.Player.transform.position) <= (TowerStageId == "office_intro" ? 3.25f : 2.4f)) focusedCharacter = npc;
        }
        if (TowerStageId == "final_disposition" && !StoryUI.DialogueVisible && !ActiveFight.NarrativePaused) focusedCharacter = FinalInteractionFocus();
        StoryUI.ShowInteractionKey(focusedCharacter?.InteractionAnchor);
        if (focusedCharacter == null || !StoryUI.ConsumeInteractionPress()) return;
        if (TowerStageId == "final_disposition") InteractFinal(focusedCharacter);
        else if (TowerStageId == "tower_entry") Say("npc.entry.intro", null, true);
        else if (TowerStageId == "office_intro")
            Say("office.intro", () => ReplyOrAttack(new[]{"office.choice.surrender"},
                _ => Say("office.reply.surrender", () => Say("office.dialogue", OfficeWarning)), SummonPuppeteer));
        else if (TowerStageId == "floor01") InteractMaintenance();
        else if (TowerStageId == "floor50")
        {
            if (CurrentRoom.ExitOpen) Say("npc.floor50.after.survived", null, true);
            else Say("npc.floor50.context", StartFloor50Security, true);
        }
    }

    private void InteractMaintenance()
    {
        if (maintenancePhase == 0)
        {
            Say("npc.floor01.request", () => { maintenancePhase = 1; storyMotion = StartCoroutine(MaintenanceWalk(false)); }, true);
            return;
        }
        if (Narrative.MaintenanceResetDecision != TowerNarrativeState.ResetDecision.Unresolved)
        {
            Say(Narrative.MaintenanceResetDecision == TowerNarrativeState.ResetDecision.Allowed ? "npc.floor01.restored" : "npc.floor01.preserved", null, true);
            return;
        }
        Choose(new[]{"npc.floor01.choice.allow_reset", "npc.floor01.choice.cancel_reset", "npc.floor01.choice.attack"}, id =>
        {
            if (!CurrentRoom.Character.IsAlive) return;
            if (id == "npc.floor01.choice.allow_reset") { maintenancePhase = 3; storyMotion = StartCoroutine(MaintenanceWalk(true)); }
            else if (id == "npc.floor01.choice.cancel_reset") { Narrative.CompleteMaintenanceReset(TowerNarrativeState.ResetDecision.Cancelled); maintenancePhase = 4; }
            else ActiveFight.Player.GetComponent<PlayerShooting>().RequestPlayerAttackAt(CurrentRoom.Character.transform.position + Vector3.up);
        });
    }
    private IEnumerator MaintenanceWalk(bool reset)
    {
        var npc = CurrentRoom.Character;
        var unit = CurrentRoom.ResetUnit;
        Vector3 target = unit.position + (reset ? Vector3.zero : Vector3.left * 1.2f);
        while (npc.IsAlive && Vector3.Distance(npc.transform.position,target)>.02f)
        { npc.transform.position = Vector3.MoveTowards(npc.transform.position,target,2.5f*Time.deltaTime); yield return null; }
        if (!npc.IsAlive) yield break;
        if (!reset) { maintenancePhase = 2; yield break; }
        yield return new WaitForSeconds(.8f);
        target = unit.position + Vector3.left * 1.2f;
        while (npc.IsAlive && Vector3.Distance(npc.transform.position,target)>.02f)
        { npc.transform.position = Vector3.MoveTowards(npc.transform.position,target,2.5f*Time.deltaTime); yield return null; }
        if (!npc.IsAlive) yield break;
        Narrative.CompleteMaintenanceReset(TowerNarrativeState.ResetDecision.Allowed); maintenancePhase = 4;
    }
    private void RecordCharacterDeath(TowerCharacter npc)
    {
        if (CurrentRoom == null || ActiveFight == null || ActiveFight.Player.IsDead) return;
        if (TowerStageId == "floor01" && npc == CurrentRoom.Character) { CancelStory(); Narrative.MaintenanceDied(); }
        if (TowerStageId == "tower_entry") CancelStory();
    }
    private void StartFloor99Security(bool attacked = false)
    {
        if (TowerStageId != "floor99" || ActiveFight.Player.IsDead || !towerEvents.Add("floor99.security.started")) return;
        if (attacked || ActiveFight.BossHealth == null || ActiveFight.BossHealth.IsDead) { StartPreparedFight(); return; }
        Say("npc.floor99.security", () => { if (HighAI) Say("npc.floor99.ai_warning", StartPreparedFight); else StartPreparedFight(); });
    }

    private void StartFloor50Security()
    {
        if (TowerStageId != "floor50" || ActiveFight.Player.IsDead || !towerEvents.Add("floor50.security.started")) return;
        ActiveFight.BeginSecurityEncounter(CurrentRoom, true);
        BindRoomPerception();
        Narrative.ObserveExternalOutcome("floor50.security", "activated");
        interventionOffered = true;
        towerEvents.Add(CurrentRoom.PropEventInstanceId + ".offered");
        OfferChandelierIntervention(StartPreparedFight);
    }
}
