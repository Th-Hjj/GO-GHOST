#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

// Focused presenter/input checks on the existing virtual-input driver, not a route regression suite.
public partial class TopDownSmokeTest
{
    private Transform DialoguePart(MissionUI ui, string name) => ui.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    private void DialoguePointer(MissionUI ui, int row, bool down = false)
    {
        var rect = (RectTransform)DialoguePart(ui, "NarrativeChoiceRow" + row);
        var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        Mouse(point.x / Screen.width, point.y / Screen.height, down);
    }
    private IEnumerator DialogueClick(float x, float y)
    {
        MouseTo(x,y); yield return Frames(2); Mouse(x,y,true); yield return Frames(2); MouseTo(x,y); yield return Frames(2);
    }
    private IEnumerator DialogueUx()
    {
        var flow = FindAnyObjectByType<GameSessionFlow>(); var ui = flow.GetComponent<MissionUI>();
        var focus = StartCoroutine(S01Focus(flow));
        Record("Dialogue UX only: real virtual F/W/S/mouse events on DEV room setups; isolated Archive and focus assist. No balance/build claim.");
        try
        {
            yield return FlowJump(flow,"tower_entry",0); yield return FlowNear(flow,flow.CurrentRoom.Character); yield return FlowKey(Key.F);
            Check("F opens worker with data speaker",ui.DialogueVisible && ui.DisplayedSpeaker=="Worker" && ui.DisplayedNarrativeText=="Hi",ui.DisplayedSpeaker);
            yield return FlowKey(Key.F);
            Check("F closes normal dialogue",!ui.DialogueVisible && !flow.ActiveFight.NarrativePaused,"keyboard");

            yield return FlowJump(flow,"floor50",0); yield return FlowNear(flow,flow.CurrentRoom.Receptionist); yield return FlowKey(Key.F);
            var panel = DialoguePart(ui,"DialoguePanel").GetComponent<UnityEngine.UI.Image>();
            var labels = panel.GetComponentsInChildren<TMP_Text>(true);
            Check("One plain opaque text style",labels.All(t=>t.fontSize==38 && !t.enableAutoSizing && !t.richText && t.fontStyle==FontStyles.Normal && t.color==Color.white),"body/speaker/choices/consequence");
            Check("Translucent panel, transparent screen",Mathf.Approximately(panel.color.a,.6f) && panel.transform.parent.GetComponent<UnityEngine.UI.Image>().color.a==0,"background only");
            var speaker = (RectTransform)DialoguePart(ui,"SpeakerName");
            Check("Speaker is upper-left",speaker.anchoredPosition.y>0 && speaker.GetComponent<TMP_Text>().alignment==TextAlignmentOptions.Left,"layout");
            ScreenCapture.CaptureScreenshot("Logs/DialogueUX/dialogue.png"); yield return Frames(3);
            int shots = flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls;
            float elapsed = flow.ActiveFight.CombatElapsed;
            string[] names={"Receptionist","Officer","Officer","Receptionist","Receptionist"};
            for(int i=0;i<names.Length;i++)
            {
                Check("50F line speaker "+i,ui.DisplayedSpeaker==names[i],ui.DisplayedSpeaker);
                Mouse(.1f,.8f,true); yield return Frames(3);
                string after = ui.DisplayedNarrativeText;
                yield return Frames(4);
                if(i<names.Length-1) Check("Held click advances only once "+i,ui.DialogueVisible && ui.DisplayedNarrativeText==after,"held");
                MouseTo(.1f,.8f); yield return Frames(3);
            }
            Check("Mouse finishes sequence, resumes once without shooting",!ui.DialogueVisible && !flow.ActiveFight.NarrativePaused && flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls==shots,"mouse close gate");
            // Return to a peaceful room; test finite choices directly without assigning any story outcome.
            yield return FlowJump(flow,"tower_entry",0);
            string[] ids={"npc.floor01.choice.allow_reset","npc.floor01.choice.cancel_reset","npc.floor01.choice.attack"};
            int commits=0; string picked=null;
            System.Action<MissionUI.DialogueResult,string> finish=(r,id)=>{if(r==MissionUI.DialogueResult.ChoiceCommitted){commits++;picked=id;}flow.ActiveFight.SetNarrativePaused(false);};
            flow.ActiveFight.SetNarrativePaused(true); ui.ShowChoices(ids,finish);
            yield return Frames(3); yield return FlowKey(Key.S);
            Check("S selects next",ui.SelectedChoiceId==ids[1],ui.SelectedChoiceId);
            yield return FlowKey(Key.W);
            Check("W selects previous",ui.SelectedChoiceId==ids[0],ui.SelectedChoiceId);
            yield return FlowKey(Key.DownArrow); yield return FlowKey(Key.UpArrow);
            Check("Arrow keys have no dialogue action",ui.SelectedChoiceId==ids[0],ui.SelectedChoiceId);
            DialoguePointer(ui,2); yield return Frames(3);
            Check("Mouse hover selects and highlights",ui.SelectedChoiceId==ids[2] && DialoguePart(ui,"NarrativeChoiceRow2").GetComponent<UnityEngine.UI.Image>().color.a>0,"row highlight");
            yield return FlowKey(Key.W);
            Check("Stationary mouse does not undo W",ui.SelectedChoiceId==ids[1],ui.SelectedChoiceId);
            ScreenCapture.CaptureScreenshot("Logs/DialogueUX/choices.png");yield return Frames(3);
            yield return FlowKey(Key.F);
            Check("F confirms selected choice once",commits==1 && picked==ids[1] && !ui.DialogueVisible,"keyboard commit");

            MouseTo(.1f,.8f);yield return Frames(2);
            flow.ActiveFight.SetNarrativePaused(true);ui.ShowChoices(ids,finish);yield return Frames(3);
            DialoguePointer(ui,2,true);yield return Frames(6);
            Check("Single mouse click selects and confirms intended row",commits==2 && picked==ids[2] && !ui.DialogueVisible,"no extra confirm");
            Check("Held choice click cannot fire after close",flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls==0,"no click-through");
            // Opening while held never treats that held press as approval of a new page.
            flow.ActiveFight.SetNarrativePaused(true);ui.ShowNarrative("npc.entry.intro",()=>flow.ActiveFight.SetNarrativePaused(false));yield return Frames(4);
            Check("Opening-held click cannot advance",ui.DialogueVisible,"fresh press required");
            yield return DialogueClick(.1f,.8f);
            Check("Fresh click after release advances",!ui.DialogueVisible,"released");

            flow.ActiveFight.SetNarrativePaused(true);ui.ShowNarrative("ending.A",()=>flow.ActiveFight.SetNarrativePaused(false));yield return Frames(3);
            Check("Caption has no invented speaker",ui.DisplayedSpeaker=="",ui.DisplayedSpeaker);
            ui.HideDialogue();flow.ActiveFight.SetNarrativePaused(false);

            // Real unpaused ReplyOrAttack window: option clicks must not become attack intent.
            yield return FlowJump(flow,"boss_p2",2);yield return WeaponsWait(()=>ui.DialogueVisible,4);yield return FlowClose(ui);
            Check("Real P2 reply window reached",ui.ChoicesVisible && flow.ReplyOrAttackActive,"existing sequencing");
            shots=flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls;
            DialoguePointer(ui,1,true);yield return Frames(6);
            Check("Unpaused choice click speaks rather than shoots",ui.CurrentNarrativeId=="boss.p2.reply.deny_ai_consciousness" && ui.DisplayedSpeaker=="Officer" && flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls==shots,ui.CurrentNarrativeId);
            MouseTo(.1f,.8f);yield return Frames(3);
            yield return FlowJump(flow,"boss_p1",0);yield return WeaponsWait(()=>ui.DialogueVisible,3);yield return FlowClose(ui);
            shots=flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls;
            yield return DialogueClick(.1f,.8f);
            Check("Outside reply panel retains real shooting",flow.ActiveFight.Player.GetComponent<PlayerShooting>().TriggerPulls>shots && !flow.ReplyOrAttackActive,"existing attack route");
            yield return FlowJump(flow,"tower_entry",0);
            Check("Jump clears UI pointer ownership",flow.ActiveFight.Player.GetComponent<PlayerInputReader>().UiBlocksManualFire==null && !ui.DialogueVisible,"cleanup");
            flow.GetComponent<DeveloperRouteJump>().RestartFull();yield return Frames(3);
        }
        finally {StopCoroutine(focus);Keys();MouseTo(.2f,.8f);}
    }
}
#endif
