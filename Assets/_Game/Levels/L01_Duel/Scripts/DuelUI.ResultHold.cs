using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // A result lands on the held fight: the HUD stays where it was and dims with the arena.
    public sealed partial class DuelUI
    {
        // Seconds after the lethal frame. Trial tuning.
        const float ResultSlabAt = .42f, ResultWordAt = .48f, ResultBandAt = .56f, ResultButtonAt = .76f, ResultSecondAt = .81f;

        // Result entrances count from the lethal frame, so a rebuilt page carries on instead of replaying.
        void BeginHeldResult()
        {
            if (!game.ResultHeld) return;
            OvMotion.EntranceStart = game.ResultHeldAt; OvMotion.Continue = true;
            if (battleUi == null) return;
            BuildOverrideHud(battleUi);
            var dim = battleUi.gameObject.AddComponent<ResultDim>();
            dim.HeldAt = game.ResultHeldAt; dim.Apply();
            // A beaten Boss's empty bar leaves: the mission name lands where it was.
            if (game.Phase != DuelPhase.Failed)
                foreach (var bar in bossHealthGroups)
                {
                    if (bar == null) continue;
                    var leave = bar.gameObject.AddComponent<ResultDim>();
                    leave.HeldAt = game.ResultHeldAt; leave.To = 0; leave.Apply();
                }
            // On a cleared mission the player's bar leaves too, so Happy's box and the button have the bottom of the
            // screen to themselves (Thomas, 2026-11-15).
            if (game.Phase == DuelPhase.Between)
                foreach (var part in battleUi.GetComponentsInChildren<RectTransform>(true))
                {
                    if (part.name != "Player health") continue;
                    var leave = part.gameObject.AddComponent<ResultDim>();
                    leave.HeldAt = game.ResultHeldAt; leave.To = 0; leave.Apply();
                }
            if (ruleSidebar != null)
            {
                var cover = Image(ruleSidebar, Vector2.zero, Vector2.zero, OverridePalette.Ink).rectTransform;
                cover.name = "Result dim";
                cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one; cover.sizeDelta = Vector2.zero;
                var shade = cover.gameObject.AddComponent<ResultDim>();
                shade.HeldAt = game.ResultHeldAt; shade.From = 0; shade.To = 1 - ResultHoldView.Dim; shade.Apply();
            }
            UpdateRuleHud();
            UpdateBattleStatus();
        }

        // Happy has a line pending or her box is still on screen.
        internal bool DialogueBoxBusy => game.PendingDialogue != null || (ambientPanel != null && ambientPanel.gameObject.activeSelf);

        // The hazard band is drawn across the screen from the left edge.
        void OvDrawAcross(RectTransform band, float delay)
        {
            float width = canvas.rect.width + 40;
            var mask = Rect("Band draw", band.parent, Vector2.zero, new Vector2(0, 240));
            mask.pivot = new Vector2(0, .5f);
            mask.anchoredPosition = new Vector2(-width / 2, band.anchoredPosition.y);
            mask.SetSiblingIndex(band.GetSiblingIndex());
            mask.gameObject.AddComponent<RectMask2D>();
            band.SetParent(mask, false);
            band.anchorMin = band.anchorMax = new Vector2(0, .5f);
            band.anchoredPosition = new Vector2(width / 2, 0);
            var draw = mask.gameObject.AddComponent<DrawAcross>();
            draw.Width = width; draw.Delay = delay;
        }
    }

    // Keeps a button away until Happy's box has closed, then lets it arrive in that place.
    public sealed class AfterDialogue : MonoBehaviour
    {
        internal DuelUI Owner;
        public SlabButton Target;
        public double NotBefore;

        void Update()
        {
            if (Target == null || Target.gameObject.activeSelf || Owner == null) return;
            if (Time.unscaledTimeAsDouble < NotBefore || Owner.DialogueBoxBusy) return;
            Target.EnterDelay = (float)(Time.unscaledTimeAsDouble - OvMotion.Born) + .04f;
            Target.gameObject.SetActive(true);
        }
    }
}
