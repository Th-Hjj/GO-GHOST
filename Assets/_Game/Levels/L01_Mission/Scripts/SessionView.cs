using TMPro;
using UnityEngine;

/// <summary>Greybox presentation. Buttons request transitions; only GameSessionFlow changes the world.</summary>
public sealed class SessionView : MonoBehaviour
{
    [SerializeField] private GameSessionFlow flow;
    [SerializeField] private TMP_Text hud;
    [SerializeField] private TMP_Text combatCue;
    [SerializeField] private TMP_Text controls;
    [SerializeField] private TMP_Text weaponLabel;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text heading;
    [SerializeField] private TMP_Text details;
    [SerializeField] private UnityEngine.UI.Button deployButton;
    [SerializeField] private UnityEngine.UI.Button newGameButton;
    [SerializeField] private GameObject slotPanel;
    [SerializeField] private UnityEngine.UI.Button leftArmButton;
    [SerializeField] private UnityEngine.UI.Button rightLegButton;
    [SerializeField] private UnityEngine.UI.Button brainButton;
    [SerializeField] private UnityEngine.UI.Button rightArmButton;
    [SerializeField] private UnityEngine.UI.Button leftLegButton;
    [SerializeField] private UnityEngine.UI.Button spineButton;
    private TMP_Text armLabel, legLabel, brainLabel;
    private TMP_Text rightArmLabel, leftLegLabel, spineLabel;
    private TMP_Text deployLabel, newGameLabel;
    private UnityEngine.UI.LayoutElement detailsLayout;

    private void OnEnable()
    {
        deployButton.onClick.AddListener(Deploy);
        newGameButton.onClick.AddListener(NewGame);
        leftArmButton.onClick.AddListener(ToggleArm);
        rightLegButton.onClick.AddListener(ToggleLeg);
        brainButton.onClick.AddListener(ToggleBrain);
        rightArmButton.onClick.AddListener(ToggleRightArm);
        leftLegButton.onClick.AddListener(ToggleLeftLeg);
        spineButton.onClick.AddListener(ToggleSpine);
        armLabel = leftArmButton.GetComponentInChildren<TMP_Text>();
        legLabel = rightLegButton.GetComponentInChildren<TMP_Text>();
        brainLabel = brainButton.GetComponentInChildren<TMP_Text>();
        rightArmLabel = rightArmButton.GetComponentInChildren<TMP_Text>();
        leftLegLabel = leftLegButton.GetComponentInChildren<TMP_Text>();
        spineLabel = spineButton.GetComponentInChildren<TMP_Text>();
        detailsLayout = details.GetComponent<UnityEngine.UI.LayoutElement>();
        deployLabel = deployButton.GetComponentInChildren<TMP_Text>();
        newGameLabel = newGameButton.GetComponentInChildren<TMP_Text>();
    }

    private void OnDisable()
    {
        deployButton.onClick.RemoveListener(Deploy);
        newGameButton.onClick.RemoveListener(NewGame);
        leftArmButton.onClick.RemoveListener(ToggleArm);
        rightLegButton.onClick.RemoveListener(ToggleLeg);
        brainButton.onClick.RemoveListener(ToggleBrain);
        rightArmButton.onClick.RemoveListener(ToggleRightArm);
        leftLegButton.onClick.RemoveListener(ToggleLeftLeg);
        spineButton.onClick.RemoveListener(ToggleSpine);
    }

    private void Deploy() => flow.RequestDeploy();
    private void NewGame() => flow.RequestNewGame();
    private void ToggleArm() => Toggle(LoadoutSlot.LeftArm);
    private void ToggleLeg() => Toggle(LoadoutSlot.RightLeg);
    private void ToggleBrain() => Toggle(LoadoutSlot.Brain);
    private void ToggleRightArm() => Toggle(LoadoutSlot.RightArm);
    private void ToggleLeftLeg() => Toggle(LoadoutSlot.LeftLeg);
    private void ToggleSpine() => Toggle(LoadoutSlot.Spine);
    private void Toggle(LoadoutSlot slot)
    {
        if (flow.State != GameSessionFlow.FlowState.Loadout) return;
        flow.TrySetDraftSlot(slot, string.IsNullOrEmpty(flow.Draft.GetSlot(slot).ItemId) ? LoadoutRules.ModuleForSlot(slot) : "");
    }

    private void UpdateSlot(LoadoutSlot slot, UnityEngine.UI.Button button, TMP_Text label)
    {
        bool equipped = !string.IsNullOrEmpty(flow.Draft.GetSlot(slot).ItemId);
        string module = LoadoutRules.ItemLabel(LoadoutRules.ModuleForSlot(slot));
        string contribution = $"+{LoadoutRules.MaximumContribution(slot):0}%";
        label.text = Tint(slot, LoadoutRules.SlotLabel(slot).ToUpperInvariant()) + "   |   " + (equipped
            ? $"<b>{module}</b> {contribution}   [REMOVE]" : $"HUMAN   [INSTALL {module} {contribution}]");
        button.interactable = flow.AugmentationsUnlocked && flow.CanDeploy;
        button.image.color = equipped ? new Color(.1f, .1f, .1f) : Color.black;
        var outline = button.GetComponent<UnityEngine.UI.Outline>();
        if (outline != null) outline.effectColor = equipped ? OfficerVisual.SlotColor(slot) : new Color(.4f, .4f, .4f);
    }

    private static string Tint(LoadoutSlot slot, string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(OfficerVisual.SlotColor(slot))}>{text}</color>";
    private static string AimLabel(AimControl aim) => aim == AimControl.AI ? Tint(LoadoutSlot.Brain, "AIM: AI") : "AIM: MANUAL";
    private static string Identity(DeploymentSnapshot snapshot) => $"<size=32>OFFICER #{snapshot.OfficerIndex}   |   BODY MODIFICATION: {snapshot.BodyModificationPercent:0.##}%</size>\n"
        + AimLabel(snapshot.Aim) + "   |   " + (snapshot.HasTimeSlow ? Tint(LoadoutSlot.Spine, "SHIFT: TIME SLOW") : "SHIFT: GROUND DODGE");
    private static string EquipmentSummary(DeploymentSnapshot snapshot)
    {
        string text = "";
        for (int i = 0; i < 6; i++)
        {
            var slot = (LoadoutSlot)i;
            text += Tint(slot, LoadoutRules.SlotLabel(slot)) + ": " + LoadoutRules.ItemLabel(snapshot.GetSlot(slot).ItemId)
                + (i % 2 == 0 ? "      |      " : "\n");
        }
        return text + Tint(LoadoutSlot.Weapon, "Weapon") + ": Basic Gun (0%)";
    }

    private void Update()
    {
        var state = flow.State;
        var fight = flow.ActiveFight;
        bool combat = state == GameSessionFlow.FlowState.Combat;
        bool loadout = state == GameSessionFlow.FlowState.Loadout;
        bool result = state == GameSessionFlow.FlowState.Result;
        bool victory = state == GameSessionFlow.FlowState.VictoryTransition;
        panel.SetActive(!combat);
        deployButton.gameObject.SetActive(loadout);
        newGameButton.gameObject.SetActive(result);
        slotPanel.SetActive(loadout);
        deployButton.interactable = flow.CanDeploy;
        newGameButton.interactable = flow.CanNewGame;
        deployLabel.text = flow.CanDeploy ? "DEPLOY" : $"READY IN {Mathf.CeilToInt(flow.HoldRemaining)}";
        newGameLabel.text = flow.CanNewGame ? "NEW GAME" : $"NEW GAME IN {Mathf.CeilToInt(flow.HoldRemaining)}";
        detailsLayout.minHeight = detailsLayout.preferredHeight = loadout ? 104 : 420;
        combatCue.gameObject.SetActive(combat);
        controls.gameObject.SetActive(combat);

        if (combat && fight != null)
        {
            var snapshot = fight.Deployment;
            hud.text = $"<size=30><b>OFFICER #{snapshot.OfficerIndex}   |   BODY MODIFICATION: {snapshot.BodyModificationPercent:0.##}%</b></size>\n"
                + AimLabel(snapshot.Aim) + $"   |   HP {fight.Player.CurrentHealth}/{fight.Player.MaxHealth}   |   CYBERPSYCHO {fight.BossHealth.CurrentHealth}/{fight.BossHealth.MaxHealth}\n"
                + (snapshot.HasTimeSlow ? Tint(LoadoutSlot.Spine, fight.Player.GetComponent<PlayerTimeSlow>().Cue) : "GROUND DODGE [SHIFT]")
                + $"   |   FIRE {snapshot.ShotsPerSecond:0}/s   MOVE {snapshot.MoveSpeed:0}";
            combatCue.text = fight.Boss.Cue;
            controls.text = "A / D  MOVE     SPACE  JUMP     LEFT SHIFT  ABILITY     LEFT MOUSE  FIRE"
                + (snapshot.Aim == AimControl.AI ? "     AI OWNS AIM" : "     MOUSE  AIM");
        }
        else hud.text = "";

        if (loadout)
        {
            heading.text = $"OFFICER #{flow.OfficerIndex}   /   LOADOUT";
            var aim = flow.Draft.GetSlot(LoadoutSlot.Brain).ItemId == LoadoutRules.BrainAI ? AimControl.AI : AimControl.Manual;
            details.text = $"<size=30><b>BODY MODIFICATION: {flow.Draft.BodyModificationPercent:0.##}%</b></size>   |   " + AimLabel(aim)
                + "\nPrevious loadout shown. Install or remove freely; 0% is always available.\n"
                + (flow.Draft.GetSlot(LoadoutSlot.Spine).ItemId == LoadoutRules.SpineSlow
                    ? Tint(LoadoutSlot.Spine, "SHIFT: TIME SLOW — 3s active / 10s between starts") : "SHIFT: GROUND DODGE — 0.8s between starts");
            weaponLabel.text = Tint(LoadoutSlot.Weapon, "WEAPON") + "   |   BASIC GUN (0%)   /   STANDARD";
            UpdateSlot(LoadoutSlot.LeftArm, leftArmButton, armLabel);
            UpdateSlot(LoadoutSlot.RightArm, rightArmButton, rightArmLabel);
            UpdateSlot(LoadoutSlot.LeftLeg, leftLegButton, leftLegLabel);
            UpdateSlot(LoadoutSlot.RightLeg, rightLegButton, legLabel);
            UpdateSlot(LoadoutSlot.Spine, spineButton, spineLabel);
            UpdateSlot(LoadoutSlot.Brain, brainButton, brainLabel);
        }
        else if (result || victory)
        {
            var record = flow.Victory;
            heading.text = victory ? "VICTORY" : "MISSION COMPLETE";
            details.text = Identity(record.WinningDeployment) + (victory
                ? "\n\nCYBERPSYCHO NEUTRALIZED\n\nThis officer completed the mission.\nPreparing the deployment record..."
                : "\n\nWINNING DEPLOYMENT\n" + EquipmentSummary(record.WinningDeployment)
                    + "\n\nStart a new mission as Officer #1, 0% Human.\nContinue when you are ready.");
        }
        else if (state == GameSessionFlow.FlowState.DefeatTransition)
        {
            heading.text = "OFFICER DOWN";
            details.text = Identity(flow.Deployment) + $"\n\nOFFICER #{flow.OfficerIndex} HAS FALLEN\n\nThe mission continues.\nOfficer #{flow.OfficerIndex + 1} will take over.";
        }
        else if (state == GameSessionFlow.FlowState.NextOfficerTransition)
        {
            heading.text = $"OFFICER #{flow.OfficerIndex}   /   TAKING OVER";
            details.text = "SAME MISSION. A NEW OFFICER.\n\nYour predecessor's loadout is ready to edit.\nYou may deploy with any configuration, including 0% Human.\n\nOPENING LOADOUT...";
        }
        else
        {
            bool newGame = state == GameSessionFlow.FlowState.NewGameInitialization;
            heading.text = newGame ? "GO GHOST   /   NEW MISSION" : "DEPLOYING";
            details.text = newGame ? "<size=32>OFFICER #1   |   BODY MODIFICATION: 0%</size>\nAIM: MANUAL   |   BASIC GUN\n\nONE MISSION. ONE CYBERPSYCHO.\n\nYour first officer deploys fully human."
                : Identity(flow.Deployment) + "\n\n" + EquipmentSummary(flow.Deployment)
                    + $"\n\nCOMBAT IN {flow.HoldRemaining:0.0}s\nRelease your controls, then get ready.";
        }
    }
}
