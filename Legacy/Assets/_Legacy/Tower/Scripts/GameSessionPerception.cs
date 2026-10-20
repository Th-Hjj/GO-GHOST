using System;
using UnityEngine;

public sealed partial class GameSessionFlow
{
    private void BindPracticePerception()
    {
        foreach (var door in practiceRoom.GetComponentsInChildren<PracticeDoor>(true))
        {
            var outline = door.GetComponent<InteractionOutline>();
            if (outline == null) outline = door.gameObject.AddComponent<InteractionOutline>();
            outline.Bind(ActiveFight.Player, () => State == FlowState.Practice);
        }
    }

    private void BindRoomPerception()
    {
        var room = CurrentRoom;
        if (room == null || ActiveFight == null) return;
        var player = ActiveFight.Player;
        void Mark(Component target, Func<bool> relevant)
        {
            if (target == null) return;
            var outline = target.GetComponent<InteractionOutline>();
            if (outline == null) outline = target.gameObject.AddComponent<InteractionOutline>();
            outline.Bind(player, () => CurrentRoom == room && State == FlowState.Combat && relevant(), target is ChandelierEncounter);
        }
        // Living NPCs remain meaningful bullet targets even when their voluntary conversation is busy.
        Mark(room.Character, () => room.Character.IsAlive);
        Mark(room.Receptionist, () => room.Receptionist.IsAlive);
        Mark(room.Prop, () => room.Prop.CanRelease || !room.Prop.IsHanging);
        Mark(room.ResetUnit, () => room.Character != null && room.Character.IsAlive && maintenancePhase == 2
            && Narrative.MaintenanceResetDecision == TowerNarrativeState.ResetDecision.Unresolved);
        foreach (var door in room.GetComponentsInChildren<TowerDoor>(true)) Mark(door, () => room.ExitOpen);
        if (TowerStageId == "floor50" || TowerStageId == "floor99")
        {
            var guard = ActiveFight.BossHealth;
            Mark(guard, () => guard != null && !guard.IsDead);
        }
        if (TowerStageId == "office_intro" || TowerStageId == "final_disposition")
        {
            Mark(president, () => president.IsAlive);
            Mark(continuingCore, () => continuingCore.IsAlive);
        }
        if (TowerStageId == "office_intro" || TowerStageId == "boss_p3" || TowerStageId == "final_disposition")
            Mark(managementBarrier != null ? managementBarrier.transform : null, () => managementBarrier.activeInHierarchy);
    }
}
