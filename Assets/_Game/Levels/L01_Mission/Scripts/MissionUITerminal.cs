using TMPro;
using UnityEngine;

public sealed partial class MissionUI
{
    internal static readonly Color TerminalBlue = new Color(.24f, .61f, 1f);
    internal static readonly Color TerminalWhite = new Color(.84f, .94f, 1f);
    private static readonly Color TerminalMuted = new Color(.48f, .65f, .78f);
    private static readonly Color TerminalPanel = new Color(.014f, .027f, .043f);
    private static readonly Color TerminalButton = new Color(.025f, .065f, .11f);

    // Creation-time lookup only. Combat, practice and ending keep their existing appearance.
    private static bool TerminalContext(Transform node)
    {
        while (node != null && node.GetComponent<Canvas>() == null)
        {
            if (node.name == "MainMenu" || node.name == "Archive" || node.name == "MissionResult") return true;
            node = node.parent;
        }
        return false;
    }

    private static void TerminalPanelStyle(RectTransform rect)
    {
        rect.GetComponent<UnityEngine.UI.Image>().color = TerminalPanel;
        var edge = Rect(rect, "TerminalFrame", Vector2.zero, Vector2.zero);
        Stretch(edge);
        edge.gameObject.AddComponent<MissionTerminalFrame>().raycastTarget = false;
    }

    private static void TerminalButtonStyle(UnityEngine.UI.Button button, bool active)
    {
        // Built-in ColorTint supplies a short hover fade; no per-button Update or tween package.
        button.targetGraphic.color = Color.white;
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = active ? new Color(.075f, .26f, .43f) : TerminalButton;
        colors.highlightedColor = active ? new Color(.10f, .32f, .51f) : new Color(.045f, .14f, .23f);
        colors.pressedColor = new Color(.11f, .36f, .56f);
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(.025f, .04f, .055f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = .12f;
        button.colors = colors;
        var frame = button.GetComponentInChildren<MissionTerminalFrame>();
        if (frame != null) { frame.Accent = active; frame.SetVerticesDirty(); }
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.color = active ? Color.white : TerminalWhite;
    }

    private static void TerminalTextStyle(TMP_Text text, string name)
    {
        text.color = name.StartsWith("Field_") || name.StartsWith("StatisticLabel") ? TerminalMuted : TerminalWhite;
        if (name == "Title") { text.fontSize = 80; text.characterSpacing = 10; }
        else if (name == "ResultHeading") text.characterSpacing = 5;
        else if (name == "Text") { text.fontSize = 25; text.characterSpacing = 1.2f; }
        text.enableAutoSizing = true;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = Mathf.Min(16, text.fontSize);
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void TerminalRule(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var line = Rect(parent, name, position, size);
        var image = line.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = new Color(TerminalBlue.r, TerminalBlue.g, TerminalBlue.b, .32f);
        image.raycastTarget = false;
    }
}
