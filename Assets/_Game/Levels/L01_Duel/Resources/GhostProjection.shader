Shader "GO GHOST/Ghost Projection"
{
    // Projected light in the GHOST arena: the vertical strokes of the letters. Flat colour in rising rows,
    // the same rows GHOST's body is made of. Bodies and bullets pass through it.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Period ("Row height in pixels at 1440", Float) = 4
        _Speed ("Rows per second", Float) = 0
        _Low ("Band start height", Float) = -1
        _High ("Band end height", Float) = 4.5
        _BandSeconds ("Seconds per pass", Float) = 5
        _Light ("Band colour", Color) = (.86, .91, 1, 1)
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
            float _Period, _Speed, _Low, _High, _BandSeconds;
            fixed4 _Light;
            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
                fixed4 colour : COLOR;
            };
            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.colour = v.color;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                // Screen rows, so the camera pulling back never changes their size or makes them crawl.
                // They stand still and the faint rows are only a little darker: over strokes this large,
                // climbing high-contrast rows read as flicker (Thomas, 2026-11-14).
                float period = max(3, round(_Period * _ScreenParams.y / 1440));
                float row = frac(floor(i.clip.y) / period + _Time.y * _Speed) < .62 ? 1 : .55;
                // The life in it: one soft band of light climbs each stroke, a little later on each letter to the right.
                // It is wide and slow, so it reads as light passing and never as flicker.
                float along = frac(_Time.y / _BandSeconds - i.world.x * .02);
                float band = lerp(_Low - 1.2, _High + 1.2, along);
                float glow = 1 - smoothstep(0, 1.1, abs(i.world.y - band));
                fixed4 colour = i.colour;
                colour.rgb = lerp(colour.rgb, _Light.rgb, glow * .55);
                colour.a *= tex2D(_MainTex, i.uv).a * lerp(row, 1, glow * .6);
                return colour;
            }
            ENDCG
        }
    }
}
