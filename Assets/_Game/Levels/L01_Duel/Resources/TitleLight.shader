Shader "GO GHOST/Title Light"
{
    // Light and lit mist on the title: adds the vertex colour, scaled by the texture's red channel.
    // Flow above zero lets slow noise drift through it, so mist moves inside its shape instead of sliding.
    Properties
    {
        [PerRendererData] _MainTex ("Strength in red", 2D) = "black" {}
        _Flow ("Flow", Range(0, 1)) = 0
        _Clock ("Clock", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Flow, _Clock;
            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 colour : COLOR;
            };
            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.colour = v.color;
                return o;
            }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x),
                    lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                half strength = tex2D(_MainTex, i.uv).r;
                float2 p = i.uv * float2(7, 2.5);
                float drift = noise(p + float2(_Clock * .035, 0)) * .65 + noise(p * 2.3 - float2(_Clock * .05, .3)) * .35;
                strength *= lerp(1, drift * 1.7, _Flow);
                return half4(i.colour.rgb * i.colour.a * strength, 0);
            }
            ENDCG
        }
    }
}
