using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        TMP_FontAsset characterControlFont, characterNumericFont, characterFallback;
        bool characterFontsInitialized;

        TMP_FontAsset CharacterNameFont(bool numeric = false)
        {
            if (!characterFontsInitialized)
            {
                characterFontsInitialized = true;
                var installed = Font.GetOSInstalledFontNames();
                foreach (string family in new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC" })
                {
                    if (System.Array.IndexOf(installed, family) < 0) continue;
                    characterFallback = TMP_FontAsset.CreateFontAsset(family, "Regular", 64);
                    if (characterFallback != null) break;
                }
                // Keep IBM Plex for all existing glyphs. Only missing name glyphs use the OS font.
                // Runtime copies keep shared font assets and other UI untouched.
                if (characterFallback != null)
                {
                    characterControlFont = WithNameFallback(controlFont);
                    characterNumericFont = WithNameFallback(numericFont);
                }
            }
            return numeric ? characterNumericFont != null ? characterNumericFont : numericFont
                : characterControlFont != null ? characterControlFont : controlFont;
        }
        TMP_FontAsset WithNameFallback(TMP_FontAsset source)
        {
            var copy = Instantiate(source);
            copy.name = source.name + " Character Name";
            copy.fallbackFontAssetTable = source.fallbackFontAssetTable != null
                ? new System.Collections.Generic.List<TMP_FontAsset>(source.fallbackFontAssetTable)
                : new System.Collections.Generic.List<TMP_FontAsset>();
            copy.fallbackFontAssetTable.Add(characterFallback);
            return copy;
        }
        void DisposeCharacterFonts()
        {
            if (characterControlFont != null) Destroy(characterControlFont);
            if (characterNumericFont != null) Destroy(characterNumericFont);
            if (characterFallback == null) return;
            foreach (var atlas in characterFallback.atlasTextures) if (atlas != null) Destroy(atlas);
            Destroy(characterFallback.material); Destroy(characterFallback);
        }

        void BuildCharacterCreation()
        {
            Text("Create Character", page, new Vector2(0, 350), new Vector2(640, 60), 30, face:headingFont);
            var face = Rect("CharacterFace", page, new Vector2(0, 65), new Vector2(390, 390));
            var image = face.gameObject.AddComponent<Image>();
            image.sprite = game.Appearance.Sprite;
            image.color = CharacterAppearance.Tint;
            image.raycastTarget = true;
            face.gameObject.AddComponent<CharacterFaceCanvas>().Initialize(game.Appearance);
            ConfigButton("ClearFace", "Clear", page, new Vector2(300, 65), new Vector2(140, 52), game.Appearance.Clear, style:DuelControlStyle.Tool);

            var box = Rect("CharacterName", page, new Vector2(0, -230), new Vector2(440, 60));
            var background = box.gameObject.AddComponent<Image>(); background.color = NeuralPalette.Panel;
            for (int i = 0; i < 4; i++)
            {
                var edge = Image(box, Vector2.zero, Vector2.zero, NeuralPalette.ControlBorder).rectTransform;
                edge.anchorMin = i < 2 ? new Vector2(0, i) : new Vector2(i - 2, 0);
                edge.anchorMax = i < 2 ? new Vector2(1, i) : new Vector2(i - 2, 1);
                edge.pivot = i < 2 ? new Vector2(.5f, i) : new Vector2(i - 2, .5f);
                edge.sizeDelta = i < 2 ? new Vector2(0, 2) : new Vector2(2, 0);
            }
            var input = box.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = CharacterAppearance.NameLimit;
            input.richText = false;
            input.customCaretColor = true; input.caretColor = NeuralPalette.Primary;
            input.selectionColor = new Color(.2f, .3f, .4f, .3f);
            var viewport = Rect("TextArea", box, Vector2.zero, new Vector2(404, 54));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text("", viewport, Vector2.zero, new Vector2(404, 54), 26, TextAlignmentOptions.Left, CharacterNameFont());
            text.color = NeuralPalette.Primary; text.textWrappingMode = TextWrappingModes.NoWrap;
            var placeholder = Text("Enter Your Name", viewport, Vector2.zero, new Vector2(404, 54), 26, TextAlignmentOptions.Left, controlFont);
            placeholder.color = NeuralPalette.Secondary;
            input.textViewport = viewport;
            input.textComponent = (TextMeshProUGUI)text; input.placeholder = placeholder;
            input.text = game.Appearance.Name;
            input.onValueChanged.AddListener(value => game.Appearance.Name = value);
            ConfigButton("CreateCharacterContinue", "Continue", page, new Vector2(0, -340), new Vector2(240, 60), game.CompleteCharacterCreation, style:DuelControlStyle.Primary);
            ConfigButton("CreateCharacterBack", "Back", page, new Vector2(-680, 370), new Vector2(120, 48), game.MainMenu, style:DuelControlStyle.Navigation);
        }
    }
}
