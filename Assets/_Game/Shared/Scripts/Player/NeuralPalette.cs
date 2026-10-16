using UnityEngine;

public enum NeuralBodySystem { None, Arms, Legs, Spine }

// Shared sRGB identities. Alpha and local signal intensity are controlled by each view.
public static class NeuralPalette
{
    public static readonly Color Background = new Color32(0, 0, 0, 255);
    public static readonly Color Panel = new Color32(8, 14, 20, 255);
    public static readonly Color Primary = new Color32(220, 231, 237, 255);
    public static readonly Color Secondary = new Color32(155, 171, 181, 255);
    public static readonly Color Arms = new Color32(53, 207, 255, 255);
    public static readonly Color Legs = new Color32(92, 247, 162, 255);
    public static readonly Color Spine = new Color32(178, 140, 255, 255);
    // Gold contour only. No current head/Ultimate gameplay system.
    public static readonly Color HeadReserved = new Color32(248, 200, 92, 255);
    public static readonly Color Refraction = new Color32(255, 154, 98, 255);
    public static readonly Color Severance = new Color32(255, 99, 117, 255);
    public static Color Danger => Severance;
    public static Color Border => Color.Lerp(Panel, Secondary, .28f);
    public static Color ControlBorder => Color.Lerp(Panel, Secondary, .62f);
    public static Color DisabledText => Color.Lerp(Panel, Secondary, .72f);
    public static NeuralBodySystem System(ControlOperation operation) => operation switch
    {
        ControlOperation.Aim or ControlOperation.Fire => NeuralBodySystem.Arms,
        ControlOperation.Movement or ControlOperation.Jump => NeuralBodySystem.Legs,
        _ => NeuralBodySystem.Spine
    };
    public static Color Body(NeuralBodySystem system) => system switch
    {
        NeuralBodySystem.Arms => Arms, NeuralBodySystem.Legs => Legs,
        NeuralBodySystem.Spine => Spine, _ => Primary
    };
    public static Color Operation(ControlOperation operation) => Body(System(operation));
    public static Color Region(int region) => region switch
    {
        0 => HeadReserved, 1 => Spine, 2 or 3 => Arms, 4 or 5 => Legs, _ => Primary
    };
    public static Color Alpha(Color color, float alpha) { color.a = alpha; return color; }
}

// Read-only view data; no rule evaluation, occurrence state or level types cross into Shared.
public struct ConditionalVisualFeedback
{
    public bool Matched, Executing, AcceptedThisFrame;
    public float Pulse, Afterglow;
    public float Signal => Executing ? .72f : Matched ? .20f : 0;
}
