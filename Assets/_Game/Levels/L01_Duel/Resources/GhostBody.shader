Shader "GO GHOST/Ghost Body"
{
    // GHOST's body in the arena, the same hologram as on the title: rows that climb, one bright band travelling
    // up him, and now and then a short run of rows across the chest displaced sideways. Heights are in world
    // units above his soles; the rows are whole screen pixels so the camera's zoom does not change them.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Light ("Band colour", Color) = (.86, .91, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        fixed4 _Light;
        // _Clock counts rows climbed; _Window is the displaced run (low, high) and _Shift how far it moves.
        float _Feet, _Clock, _Band, _BandHalf, _Shift;
        float4 _Window;
        struct Varyings
        {
            float4 clip : SV_POSITION;
            float2 uv : TEXCOORD0;
            float height : TEXCOORD1;
            fixed4 colour : COLOR;
        };
        Varyings place(appdata_full v, float shift)
        {
            Varyings o;
            float4 world = mul(unity_ObjectToWorld, v.vertex);
            o.height = world.y - _Feet;
            world.x += shift;
            o.clip = mul(UNITY_MATRIX_VP, world);
            o.uv = v.texcoord.xy;
            o.colour = v.color;
            return o;
        }
        fixed4 shade(Varyings i, float displaced)
        {
            float inside = step(_Window.x, i.height) * step(i.height, _Window.y);
            float shown = lerp(1 - inside, inside, displaced);
            float period = max(3, round(4 * _ScreenParams.y / 1440));
            float row = frac(floor(i.clip.y) / period + _Clock) < .62 ? 1 : .26;
            float band = saturate(1 - abs(i.height - _Band) / _BandHalf);
            fixed4 colour = i.colour;
            colour.rgb = lerp(colour.rgb, _Light.rgb, saturate(band * .85 + displaced * .7));
            colour.a *= tex2D(_MainTex, i.uv).a * row * shown;
            return colour;
        }
        ENDCG

        // Everything outside the displaced run.
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(appdata_full v) { return place(v, 0); }
            fixed4 frag(Varyings i) : SV_Target { return shade(i, 0); }
            ENDCG
        }
        // The displaced run alone, moved sideways.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            Varyings vert(appdata_full v) { return place(v, _Shift); }
            fixed4 frag(Varyings i) : SV_Target { return shade(i, 1); }
            ENDCG
        }
    }
}
