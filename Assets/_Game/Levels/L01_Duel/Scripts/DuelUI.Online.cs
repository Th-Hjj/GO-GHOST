using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    // Online (Thomas, 2026-11-18), stage 1: the title's Online button and the page behind it.
    // Creating and joining a room are wired in the networking stage. Docs/Game/UIDesign.md, Online.
    public sealed partial class DuelUI
    {
        // New Game's share of the title's second row; Online takes the rest.
        const float TitleNewGameWidth = 218;
        const int RoomCodeLimit = 6;
        string roomCode = "";
        Button joinRoom;

        // The title's landscape stays behind the page under a shade; the player on the roof stays lit in front of it,
        // as the character does when the menu dims a preparation page.
        void BuildShadedTitle()
        {
            BuildTitleBackdrop();
            var roof = titleLayers[titleLayers.Length - 1];
            var shade = Image(roof, Vector2.zero, new Vector2(ScreenSpan, ScreenSpan), NeuralPalette.Alpha(OverridePalette.Ink, .7f));
            // Under the figure, over the roof and everything behind it.
            shade.transform.SetAsFirstSibling();
            OvArrive(shade, Vector2.zero, 0);
            OvScanlines(page, .28f);
        }

        void BuildOnlinePage()
        {
            var frame = OvOverlayFrame("Online", overTitle: true);
            OvButton("Create Room", frame, At(56, 300, 480, 96), new Vector2(480, 96), CreateRoom, OverrideButtonKind.Primary, 58, true).name = "CreateRoom";

            // The code a friend reads out, typed into the same field the name uses. The row stands 12 under Create Room,
            // as the title's rows do. The field's box runs 4 into Join's: two slabs with this slant already stand one
            // cut (16) apart, and the gap between them should read as the 12 above them.
            const float rowTop = 408, fieldWidth = 316;
            var box = Rect("RoomCode", frame, At(56, rowTop, fieldWidth, 76), new Vector2(fieldWidth, 76));
            var field = Rect("Slab", box, Vector2.zero, new Vector2(fieldWidth, 76)).gameObject.AddComponent<SlabGraphic>();
            field.color = OverridePalette.Surface; field.raycastTarget = false;
            var background = box.gameObject.AddComponent<Image>(); background.color = Color.clear;
            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = RoomCodeLimit;
            input.richText = false;
            input.customCaretColor = true; input.caretColor = OverridePalette.Signal; input.caretWidth = 3;
            input.selectionColor = NeuralPalette.Alpha(OverridePalette.Signal, .35f);
            input.onValidateInput = (text, index, added) => added < 128 && char.IsLetterOrDigit(added) ? char.ToUpperInvariant(added) : '\0';
            // The typed code starts under Create Room's first letter: the same 44 in from the box's left.
            const float textWidth = fieldWidth - 88;
            var viewport = Rect("TextArea", box, Vector2.zero, new Vector2(textWidth, 60));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text("", viewport, Vector2.zero, new Vector2(textWidth, 60), 36, TextAlignmentOptions.Left, monoBold != null ? monoBold : OvDialogueFont());
            text.color = OverridePalette.Text; text.textWrappingMode = TextWrappingModes.NoWrap; text.characterSpacing = 14;
            var placeholder = Text("Room Code", viewport, Vector2.zero, new Vector2(textWidth, 60), 24, TextAlignmentOptions.Left, OvDialogueFont());
            placeholder.color = OverridePalette.TextDim;
            input.textViewport = viewport;
            input.textComponent = (TextMeshProUGUI)text; input.placeholder = placeholder;
            input.text = roomCode;
            // The field built its caret before it had a text component; enabling it again creates the caret.
            input.enabled = false; input.enabled = true;

            joinRoom = OvButton("Join", frame, At(368, rowTop, 168, 76), new Vector2(168, 76), JoinRoom, OverrideButtonKind.Secondary, 44,
                align: TextAlignmentOptions.Capline);
            joinRoom.name = "JoinRoom";
            joinRoom.interactable = roomCode.Length > 0;
            input.onValueChanged.AddListener(value =>
            {
                roomCode = value;
                if (joinRoom != null) joinRoom.interactable = value.Length > 0;
            });
        }

        void CreateRoom() { }
        void JoinRoom() { }
    }
}
