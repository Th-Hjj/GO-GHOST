Shader "GO GHOST/Arena Lit Haze"
{
    // Haze that fills the arena and stays dark until a light reaches it. Trial, 2026-11-14.
    // IndustrialArenaBackdrop supplies the lights each frame; the result is added to what is behind.
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
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
            #define MAX_LIGHTS 16
            float4 _Lights[MAX_LIGHTS];        // xy world position, z radius
            float4 _LightColours[MAX_LIGHTS];  // linear colour times strength
            float _LightCount, _Steps;
            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 world : TEXCOORD0;
                half fade : COLOR;
            };
            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.fade = v.color.a;
                return o;
            }
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float valueNoise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(cell), hash(cell + float2(1, 0)), f.x),
                    lerp(hash(cell + float2(0, 1)), hash(cell + float2(1, 1)), f.x), f.y);
            }
            half4 frag(Varyings i) : SV_Target
            {
                // Two slow layers of drifting density, so a light shows the haze moving through it.
                float density = valueNoise(i.world * .55 + float2(_Time.y * .11, _Time.y * .03)) * .6
                    + valueNoise(i.world * 1.4 - float2(_Time.y * .17, _Time.y * -.05)) * .4;
                density = lerp(.45, 1, density);
                float3 lit = 0;
                for (int n = 0; n < MAX_LIGHTS; n++)
                {
                    if (n >= _LightCount) break;
                    float reach = saturate(1 - distance(i.world, _Lights[n].xy) / max(_Lights[n].z, .001));
                    reach *= reach;
                    // Above zero the falloff is cut into flat bands.
                    if (_Steps > 0) reach = ceil(reach * _Steps - .08) / _Steps;
                    lit += _LightColours[n].rgb * reach;
                }
                return half4(lit * density * i.fade, 0);
            }
            ENDCG
        }
    }
}
