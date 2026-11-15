Shader "GO GHOST/Arena Line"
{
    // A thin outline that stays even while the camera moves and zooms. The quad is drawn one pixel larger than
    // the line on every side and each pixel takes the share of it the line actually covers, so a line between two
    // pixel rows is spread over both instead of jumping from one thickness to another.
    Properties
    {
        [PerRendererData] _MainTex ("Strength in alpha", 2D) = "white" {}
        [PerRendererData] _Tint ("Line colour", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            // Set per line by ArenaSilhouetteDressing; the renderer's own colour does not reach an unbatched sprite's vertices.
            fixed4 _Tint;
            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                // The quad's size in pixels, and how far this pixel is from its centre lines.
                float2 size = 1 / max(fwidth(i.uv), 1e-6);
                float2 away = abs(i.uv - .5) * size;
                float2 cover = saturate(size * .5 - 1 - away + .5);
                fixed4 colour = _Tint;
                colour.a *= tex2D(_MainTex, i.uv).a * cover.x * cover.y;
                return colour;
            }
            ENDCG
        }
    }
}
