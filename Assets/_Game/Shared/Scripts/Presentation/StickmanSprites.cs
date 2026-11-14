using UnityEngine;

// Shared, generated sprites for every stickman's body. They replace low-resolution and hard-edged
// primitives without changing any proportion. Generated once per Play session.
public static class StickmanSprites
{
    // A limb quad is drawn this much larger than the limb, leaving one soft texel all round.
    public const float LimbPadding = 14f / 12f;
    // Visible diameter over sprite size, matching the built-in Knob the heads used.
    public const float DiscFill = .9f;

    static Sprite limb, disc, pool, block;

    // Square limb with a one-texel transparent border, so rotated edges are filtered instead of stepped.
    public static Sprite Limb => limb != null ? limb : limb = Make("Stickman limb", 14, 14,
        (x, y) => x == 0 || y == 0 || x == 13 || y == 13 ? 0 : 1);

    // Round head and joint.
    public static Sprite Disc => disc != null ? disc : disc = Make("Stickman disc", 128, 128, (x, y) =>
    {
        float dx = x + .5f - 64, dy = y + .5f - 64;
        return Mathf.Clamp01(64 * DiscFill + .5f - Mathf.Sqrt(dx * dx + dy * dy));
    });

    // Soft pool of light under a grounded fighter.
    public static Sprite Pool => pool != null ? pool : pool = Make("Stickman foot light", 64, 32, (x, y) =>
    {
        float dx = (x + .5f - 32) / 32, dy = (y + .5f - 16) / 16;
        float reach = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
        return reach * reach;
    });

    // Opaque square for the mask that cuts a standing foot flat.
    public static Sprite Block => block != null ? block : block = Make("Stickman block", 4, 4, (x, y) => 1);

    // An afterimage of a standing calf is longer than the leg looks: the live body hides the excess with a mask
    // that moves on with it. The afterimage therefore gets its own copy of that mask, left where the foot stood.
    public static void CarryFootCut(SpriteRenderer source, SpriteRenderer copy, Transform group)
    {
        if (source.maskInteraction == SpriteMaskInteraction.None) return;
        copy.maskInteraction = source.maskInteraction;
        if (group.Find("FootCut") != null) return;
        SpriteMask mask = null;
        for (var owner = source.transform.parent; owner != null && mask == null; owner = owner.parent)
            mask = owner.GetComponentInChildren<SpriteMask>();
        if (mask == null || !mask.enabled) return;
        var cut = new GameObject("FootCut").AddComponent<SpriteMask>();
        cut.sprite = mask.sprite;
        cut.transform.SetPositionAndRotation(mask.transform.position, mask.transform.rotation);
        cut.transform.localScale = mask.transform.lossyScale;
        cut.transform.SetParent(group, true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Release(ref limb); Release(ref disc); Release(ref pool); Release(ref block);
    }

    static void Release(ref Sprite sprite)
    {
        if (sprite != null) { Object.Destroy(sprite.texture); Object.Destroy(sprite); }
        sprite = null;
    }

    // The larger side is one world unit.
    static Sprite Make(string name, int width, int height, System.Func<int, int, float> alpha)
    {
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(alpha(x, y))));
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f),
            Mathf.Max(width, height), 0, SpriteMeshType.FullRect);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
