using UnityEditor;
using UnityEngine;

// Bake the amber entrance lights on the near plate. The far artwork is already lit.
sealed class CasinoLightImporter : AssetPostprocessor
{
    const string Arena = "Assets/_Game/Levels/L01_Duel/Resources/Arena/";

    public override uint GetVersion() => 2;

    void OnPostprocessTexture(Texture2D texture)
    {
        if (assetPath != Arena + "CasinoNear.png") return;

        int width = texture.width, height = texture.height;
        Color32[] pixels = texture.GetPixels32();
        var core = new float[pixels.Length];
        var halo = new float[pixels.Length];
        const int radius = 5;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            float amber = Amber(pixels[i]);
            if (amber <= 0) continue;

            // Bulbs and lettering have dark gaps. The continuous bronze trim
            // shares their palette but should pick up much less light.
            int left = y * width + Mathf.Max(x - 4, 0);
            int right = y * width + Mathf.Min(x + 4, width - 1);
            float gap = Mathf.Clamp01(amber - Mathf.Min(Amber(pixels[left]), Amber(pixels[right])));
            amber *= Mathf.Lerp(.18f, 1f, gap);
            core[i] = amber;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int px = x + dx, py = y + dy;
                if (px < 0 || px >= width || py < 0 || py >= height) continue;
                float falloff = 1f - (dx * dx + dy * dy) / (float)(radius * radius);
                if (falloff <= 0) continue;
                int j = py * width + px;
                halo[j] = Mathf.Max(halo[j], amber * falloff * falloff);
            }
        }

        Color32 orange = new Color32(242, 123, 43, 255);
        Color32 filament = new Color32(255, 204, 125, 255);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (halo[i] <= 0) continue;
            Color32 lit = LightOver(pixels[i], orange, halo[i] * .42f);
            pixels[i] = LightOver(lit, filament, core[i]);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false);
    }

    static float Amber(Color32 pixel)
    {
        if (pixel.r <= pixel.g || pixel.g <= pixel.b) return 0;
        return Mathf.InverseLerp(8f, 34f, pixel.r - pixel.b) * (pixel.a / 255f);
    }

    static Color32 LightOver(Color32 surface, Color32 light, float strength)
    {
        if (strength <= 0f) return surface;
        float alpha = strength + surface.a / 255f * (1f - strength);
        Color32 color = Color32.Lerp(surface, light, strength / alpha);
        color.a = (byte)Mathf.RoundToInt(alpha * 255f);
        return color;
    }
}
