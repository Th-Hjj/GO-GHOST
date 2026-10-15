using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ABILITY OPTION BUTTON (goes on the list-entry prefab).
/// Prefab needs: a Button, a name text, a description text, and an "Equipped" badge object.
/// Setup() takes plain strings so it works for both AI abilities and body abilities:
///  - owned:     shows the badge (already committed).
///  - canChoose: enables the button (slot open, legal, not owned).
///  - onClick:   what to run when pressed (UpgradeScreen opens the confirm popup).
/// </summary>
public class AbilityOptionButton : MonoBehaviour
{
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text descText;
    [SerializeField] Button button;
    [SerializeField] GameObject ownedBadge;

    public void Setup(string label, string description, bool owned, bool canChoose, Action onClick)
    {
        nameText.text = label;
        descText.text = description;
        ownedBadge.SetActive(owned);
        button.interactable = canChoose;
        button.onClick.AddListener(() => onClick());
    }
}