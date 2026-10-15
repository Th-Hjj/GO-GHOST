using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CONFIRM POPUP (Yes/No panel).
/// Since every choice is permanent, we ask before committing.
/// Put on a panel with a message text and Yes/No buttons (add a full-screen dimmer behind it
/// so clicks don't leak through). Start it anywhere; it hides itself in Awake.
/// Usage: confirmPopup.Ask("Are you sure?", () => { do the thing });
/// </summary>
public class ConfirmPopup : MonoBehaviour
{
    [SerializeField] TMP_Text messageText;
    [SerializeField] Button yesButton, noButton;

    void Awake()
    {
        noButton.onClick.AddListener(() => gameObject.SetActive(false));
        gameObject.SetActive(false);
    }

    public void Ask(string message, Action onYes)
    {
        messageText.text = message;
        yesButton.onClick.RemoveAllListeners();
        yesButton.onClick.AddListener(() => { gameObject.SetActive(false); onYes(); });
        gameObject.SetActive(true);
    }
}