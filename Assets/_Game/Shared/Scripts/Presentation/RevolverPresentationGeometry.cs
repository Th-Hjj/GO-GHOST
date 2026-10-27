using System.Collections.Generic;
using UnityEngine;

// Immutable, shared sprite masks for the approved flat revolver silhouette.
// Generated once per Play session; gameplay never reads these visual contours.
public static class RevolverPresentationGeometry
{
    const int Width = 600, Height = 340;
    const float PixelsPerUnit = 1000f;
    public static readonly Color BodyColor = new Color32(111, 129, 145, 255);
    public static readonly Color CoreColor = new Color32(197, 237, 255, 255);
    public static readonly Vector2 CorePosition = new Vector2(-.334f, -.039f);
    public static readonly Vector2 GripPosition = new Vector2(-.495f, -.164f);
    public static readonly Vector2 SupportPosition = new Vector2(-.476f, -.188f);
    static Sprite body, core, halo, armSegment;
    public static Sprite Body { get { Ensure(); return body; } }
    public static Sprite Core { get { Ensure(); return core; } }
    public static Sprite Halo { get { Ensure(); return halo; } }
    public static Sprite ArmSegment { get { Ensure(); return armSegment; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        Release(ref body); Release(ref core); Release(ref halo); Release(ref armSegment);
    }

    static void Release(ref Sprite sprite)
    {
        if (sprite != null) { Object.Destroy(sprite.texture); Object.Destroy(sprite); }
        sprite = null;
    }

    static void Ensure()
    {
        if (body != null && core != null && halo != null && armSegment != null) return;
        // The drawing faces left, like the accepted concept; the sprite faces +X.
        var outline = new List<Vector2> {
            new Vector2(16, 36), new Vector2(21, 36), new Vector2(21, 22),
            new Vector2(49, 22), new Vector2(57, 30), new Vector2(265, 30),
            new Vector2(274, 26), new Vector2(389, 26), new Vector2(396, 40)
        };
        Curve(outline, new Vector2(417, 40), new Vector2(413, 61), new Vector2(432, 58));
        Curve(outline, new Vector2(452, 43), new Vector2(478, 35), new Vector2(485, 40));
        Curve(outline, new Vector2(467, 44), new Vector2(452, 58), new Vector2(453, 69));
        Curve(outline, new Vector2(455, 81), new Vector2(475, 86), new Vector2(493, 92));
        Curve(outline, new Vector2(521, 139), new Vector2(555, 222), new Vector2(581, 302));
        Curve(outline, new Vector2(586, 316), new Vector2(577, 318), new Vector2(559, 318));
        outline.Add(new Vector2(465, 318));
        Curve(outline, new Vector2(450, 318), new Vector2(450, 312), new Vector2(454, 296));
        Curve(outline, new Vector2(467, 237), new Vector2(465, 204), new Vector2(445, 193));
        Curve(outline, new Vector2(430, 185), new Vector2(419, 186), new Vector2(410, 202));
        Curve(outline, new Vector2(402, 225), new Vector2(377, 234), new Vector2(348, 233));
        Curve(outline, new Vector2(310, 234), new Vector2(297, 216), new Vector2(296, 187));
        Curve(outline, new Vector2(295, 167), new Vector2(286, 157), new Vector2(267, 156));
        outline.Add(new Vector2(246, 156));
        Curve(outline, new Vector2(237, 155), new Vector2(242, 133), new Vector2(234, 120));
        outline.Add(new Vector2(224, 107)); outline.Add(new Vector2(224, 81));
        outline.Add(new Vector2(20, 81)); outline.Add(new Vector2(16, 76));

        var opening = new List<Vector2> { new Vector2(328, 160) };
        Curve(opening, new Vector2(347, 155), new Vector2(378, 157), new Vector2(392, 167));
        Curve(opening, new Vector2(413, 184), new Vector2(399, 211), new Vector2(379, 218));
        Curve(opening, new Vector2(350, 228), new Vector2(315, 219), new Vector2(309, 198));
        Curve(opening, new Vector2(303, 182), new Vector2(309, 167), new Vector2(328, 160));

        var trigger = new List<Vector2> { new Vector2(355, 157) };
        Curve(trigger, new Vector2(351, 174), new Vector2(369, 184), new Vector2(338, 213));
        Curve(trigger, new Vector2(366, 210), new Vector2(381, 185), new Vector2(374, 160));

        var pixels = new Color32[Width * Height];
        // Four fixed coverage samples keep the thin guard readable without a hard pixel stair.
        var offsets = new[] { new Vector2(.25f,.25f), new Vector2(.75f,.25f),
            new Vector2(.25f,.75f), new Vector2(.75f,.75f) };
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
        {
            int coverage = 0;
            foreach (var offset in offsets)
            {
                var p = new Vector2(Width - x - offset.x, Height - y - offset.y);
                if (Inside(outline, p) && (!Inside(opening, p) || Inside(trigger, p))) coverage++;
            }
            pixels[y * Width + x] = new Color32(255,255,255,(byte)(coverage * 255 / 4));
        }
        body = MakeSprite("ElectromagneticRevolverSilhouette", pixels, Width, Height,
            new Vector2((Width - 16f) / Width, (Height - 58f) / Height));

        const int w = 136, h = 116;
        var corePixels = new Color32[w * h];
        var haloPixels = new Color32[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            Vector2 q = new Vector2(Mathf.Abs(x + .5f - w * .5f) - 38,
                Mathf.Abs(y + .5f - h * .5f) - 29);
            float distance = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x,q.y),0) - 9;
            float alpha = Mathf.Clamp01(.5f - distance);
            float glow = Mathf.Exp(-Mathf.Pow(Mathf.Max(0, distance) / 7f, 2)) * .46f;
            corePixels[y*w+x] = new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));
            haloPixels[y*w+x] = new Color32(255,255,255,(byte)Mathf.RoundToInt(glow*255));
        }
        core = MakeSprite("ElectromagneticRevolverCore",corePixels,w,h,Vector2.one*.5f);
        halo = MakeSprite("ElectromagneticRevolverHalo",haloPixels,w,h,Vector2.one*.5f);
        armSegment = MakeArmSegment();
    }

    static Sprite MakeArmSegment()
    {
        // Keep the original .16 thickness and flat shoulder/wrist ends. Only the elbow
        // gets a semicircle; the lower segment mirrors this same mask toward its elbow.
        const float extent = .30f + .0125f + .08f;
        const int size = 256;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
            float along = (x + .5f) / size * extent - .1625f;
            float across = (y + .5f) / size * .16f - .08f;
            float distance = new Vector2(Mathf.Max(0, along - .15f), across).magnitude - .08f;
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - distance / (extent / size)) * 255);
            pixels[y * size + x] = new Color32(255,255,255,alpha);
        }
        var texture = new Texture2D(size,size,TextureFormat.RGBA32,false) {
            name="RevolverRoundedElbowSegment", filterMode=FilterMode.Bilinear,
            wrapMode=TextureWrapMode.Clamp, hideFlags=HideFlags.DontSave
        };
        texture.SetPixels32(pixels); texture.Apply(false,true);
        var sprite = Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.1625f/extent,.5f),size,0,SpriteMeshType.FullRect);
        sprite.name = "RevolverRoundedElbowSegment"; sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    static Sprite MakeSprite(string name, Color32[] pixels, int width, int height, Vector2 pivot)
    {
        var texture = new Texture2D(width,height,TextureFormat.RGBA32,false) {
            name=name, filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp, hideFlags=HideFlags.DontSave
        };
        texture.SetPixels32(pixels); texture.Apply(false,true);
        var sprite = Sprite.Create(texture,new Rect(0,0,width,height),pivot,PixelsPerUnit,0,SpriteMeshType.FullRect);
        sprite.name=name; sprite.hideFlags=HideFlags.DontSave;
        return sprite;
    }

    static void Curve(List<Vector2> points, Vector2 a, Vector2 b, Vector2 end)
    {
        Vector2 start=points[points.Count-1];
        for(int i=1;i<=8;i++) {
            float t=i/8f,u=1-t;
            points.Add(u*u*u*start+3*u*u*t*a+3*u*t*t*b+t*t*t*end);
        }
    }

    static bool Inside(List<Vector2> path, Vector2 p)
    {
        bool inside=false;
        for(int i=0,j=path.Count-1;i<path.Count;j=i++) {
            Vector2 a=path[i],b=path[j];
            if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
        }
        return inside;
    }
}
