Shader "GO GHOST/Ghost Hologram"
{
    // GHOST's body: a flat colour drawn as rising scan rows, with one bright band travelling up it and,
    // now and then, a short run of rows displaced. Heights are measured from his feet in the page's own
    // units, so the rows keep their size on every screen. No glow and no outline.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            // _Mode 0 draws everything outside _Window; 1 draws only what is inside it (the displaced rows).
            float _Feet, _Scale, _Clock, _Period, _Band, _BandHalf, _Mode, _Fade;
            float4 _Window;
            fixed4 _Light;
            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
                fixed4 colour : COLOR;
            };
            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.height = (mul(unity_ObjectToWorld, v.vertex).y - _Feet) / _Scale;
                o.colour = v.color;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                float inside = step(_Window.x, i.height) * step(i.height, _Window.y);
                float shown = lerp(1 - inside, inside, _Mode);
                float row = frac((i.height - _Clock) / _Period) < .62 ? 1 : .26;
                float band = saturate(1 - abs(i.height - _Band) / _BandHalf);
                fixed4 colour = i.colour;
                colour.rgb = lerp(colour.rgb, _Light.rgb, saturate(band * .85 + _Mode * .7));
                colour.a *= tex2D(_MainTex, i.uv).a * row * shown * _Fade;
                return colour;
            }
            ENDCG
        }
    }
}
