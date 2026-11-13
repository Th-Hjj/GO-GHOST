using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoGhost.Duel
{
    public sealed partial class DuelUI
    {
        TMP_FontAsset characterControlFont, characterNumericFont, characterFallback, dialogueFont;
        bool characterFontsInitialized;
        Button characterStart;
        RectTransform startupFill;
        TMP_Text startupLabel, startupError;
        int displayedPercent;

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
        TMP_FontAsset DialogueFont()
        {
            var control = CharacterNameFont();
            if (font == controlFont) return control;
            if (characterFallback == null) return font;
            if (dialogueFont == null) dialogueFont = WithNameFallback(font);
            return dialogueFont;
        }
        void DisposeCharacterFonts()
        {
            DestroyNameFontCopy(dialogueFont);
            DestroyNameFontCopy(monoDialogue);
            DestroyNameFontCopy(characterControlFont);
            DestroyNameFontCopy(characterNumericFont);
            if (characterFallback == null) return;
            foreach (var atlas in characterFallback.atlasTextures) if (atlas != null) Destroy(atlas);
            Destroy(characterFallback.material); Destroy(characterFallback);
        }
        static void DestroyNameFontCopy(TMP_FontAsset copy)
        {
            if (copy == null) return;
            // The copy shares the source asset's atlas and material. Detach them so TMP's
            // OnDestroy releases only the copy, never the shared IBM Plex resources.
            copy.atlasTextures = null;
            copy.material = null;
            Destroy(copy);
        }

        void BuildCharacterCreation() => BuildOverrideCreation();

        internal void ShowStartupProgress(float progress) => ShowOverrideStartupProgress(progress);
    }
}
