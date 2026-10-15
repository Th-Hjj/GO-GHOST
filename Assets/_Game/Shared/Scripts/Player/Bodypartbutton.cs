using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// BODY PART BUTTON (one per clickable region on the blueprint).
/// Put on an Image placed over the matching part of the body art, then set 'slot'
/// (Brain for head, Torso for chest, etc.) and drag in the UpgradeScreen.
/// It only reports clicks; UpgradeScreen decides what happens.
/// It also tints itself using your OfficerVisual.SlotColor palette:
///   idle = nearly invisible, selected = medium glow, has a committed ability = soft glow.
/// For irregular shapes set image.alphaHitTestMinimumThreshold (sprite needs Read/Write enabled).
/// </summary>
[RequireComponent(typeof(Image))]
public class BodyPartButton : MonoBehaviour, IPointerClickHandler
{
    public LoadoutSlot slot;
    public UpgradeScreen screen;

    Image image;

    void Awake()
    {
        image = GetComponent<Image>();
        SetState(false, false);
    }

    public void OnPointerClick(PointerEventData eventData) => screen.SelectPart(slot);

    /// <summary>Called by UpgradeScreen whenever selection or commitments change.</summary>
    public void SetState(bool selected, bool hasCommitted)
    {
        Color c = OfficerVisual.SlotColor(slot);
        c.a = selected ? 0.55f : hasCommitted ? 0.30f : 0.02f;   // never fully 0 so it still receives clicks
        image.color = c;
    }
}