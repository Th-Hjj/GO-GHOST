#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Development entry points reuse deployment and room entry. No additional progression or saved presets.
public sealed partial class GameSessionFlow
{
    private bool developmentJumpRun;
    private bool developmentSettingsSaved, originalPermissionEnabled;
    private int originalPermissionTier;
    private string developmentLastNode;
    public bool IsDevelopmentJumpRun => developmentJumpRun;
    public string DevelopmentCurrentNode => TowerStageId ?? developmentLastNode ?? "practice";
    public IEnumerable<string> DevelopmentRouteNodes
    {
        get
        {
            yield return "practice";
            foreach (var room in towerRooms) if (room != null) yield return room.StageId;
        }
    }

    public void DevelopmentPauseMenu(bool paused)
    {
        // The menu takes ownership after resolving any dialogue/aim override, never nests a second pause.
        if (paused) CancelIntervention();
        if (ActiveFight != null) ActiveFight.SetNarrativePaused(paused);
    }

    public bool DevelopmentJump(string stageId, int tier, LoadoutDraft choices)
    {
        if (!Application.isPlaying || !towerPrototypeEnabled || tier < 0 || tier > 3) return false;
        int index = System.Array.FindIndex(towerRooms, room => room != null && room.StageId == stageId);
        if (stageId != "practice" && index < 0) return false;
        // Validate a fresh copy before disposing the current attempt. Never mutate its immutable snapshot.
        var validated = new LoadoutDraft(true) { DevelopmentAllowLockedItems = true };
        if (choices != null)
            for (int i = 0; i < LoadoutRules.SlotCount; i++)
                if (!validated.TrySetSlot((LoadoutSlot)i, choices.GetSlot((LoadoutSlot)i).ItemId)) return false;
        if (!developmentSettingsSaved)
        {
            originalPermissionEnabled = permissionPrototypeEnabled;
            originalPermissionTier = developmentPermissionTier;
            developmentSettingsSaved = true;
        }
        ReturnToMainMenu(); // Cancels pause, overrides, projectiles, encounter and previous run facts.
        if (!RequestNewGame()) return false;
        Draft = validated;
        permissionPrototypeEnabled = true;
        developmentPermissionTier = tier;
        developmentJumpRun = true;
        developmentLastNode = stageId;
        if (!RequestDeploy()) return false;
        StartCoroutine(CompleteDevelopmentJump(generation, index));
        return true;
    }

    private IEnumerator CompleteDevelopmentJump(int token, int index)
    {
        while (token == generation && State == FlowState.Deployment) yield return null;
        if (token != generation || State != FlowState.Practice || index < 0) yield break;
        // Enter only this node. Skipped stages produce no events, decisions, deaths or relationship facts.
        BeginTowerRoute(index);
    }

    public bool DevelopmentRestartFullRun()
    {
        if (!Application.isPlaying) return false;
        ReturnToMainMenu();
        if (developmentSettingsSaved)
        {
            permissionPrototypeEnabled = originalPermissionEnabled;
            developmentPermissionTier = originalPermissionTier;
            developmentSettingsSaved = false;
        }
        developmentLastNode = null;
        return RequestNewGame() && RequestDeploy(); // Normal default loadout, practice door, route and Archive policy.
    }
}
#endif
