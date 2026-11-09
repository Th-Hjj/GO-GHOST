Shader "GO GHOST/Final Boss Rainbow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _AxeCharge ("Axe charge", Float) = -1
        _AxeFlowPhase ("Phase two axe flow", Float) = -1
        _AxeShadow ("Axe deep violet", Color) = (0.400, 0.278, 0.647, 1)
        _AxeHighlight ("Axe pale violet", Color) = (0.773, 0.655, 0.973, 1)
        _RainbowBottom ("World bottom", Float) = 0
        _RainbowHeight ("Body height", Float) = 1.6
        _RainbowPhase ("Rainbow flow phase", Float) = 0
        _RainbowAmount ("Rainbow amount", Range(0, 1)) = 1
        _BaseColor ("Drained colour", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _RainbowBottom;
                float _RainbowHeight;
                float _RainbowAmount;
                float _RainbowPhase;
                float _AxeCharge;
                float _AxeFlowPhase;
                half4 _BaseColor;
                half4 _AxeShadow;
                half4 _AxeHighlight;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float worldY : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(world);
                o.worldY = world.y;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                if (_AxeCharge >= 0)
                {
                    half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                    if (_AxeFlowPhase >= 0)
                    {
                        // The shaft runs along local V. Bow the bands as they cross the wider blade.
                        float across = i.uv.x - .33;
                        float path = i.uv.y * 1.25 + across * (.12 + .34 * across);
                        float wave = sin((path - _AxeFlowPhase) * 6.2831853);
                        half3 violet = lerp(_BaseColor.rgb, _AxeShadow.rgb, smoothstep(0, 1, -wave));
                        violet = lerp(violet, _AxeHighlight.rgb, smoothstep(.10, 1, wave));
                        half relief = lerp(.82, 1, max(tex.r, max(tex.g, tex.b)));
                        return half4(violet * relief, tex.a * i.color.a);
                    }
                    float front = 1 - smoothstep(_AxeCharge - .055, _AxeCharge + .055, i.uv.y);
                    half shade = lerp(.62, 1, max(tex.r, max(tex.g, tex.b)));
                    return half4(lerp(tex.rgb, _BaseColor.rgb * shade, front), tex.a * i.color.a);
                }
                float hue = (1 - saturate((i.worldY - _RainbowBottom) / max(.01, _RainbowHeight))) * .78 + _RainbowPhase;
                half3 rgb = saturate(abs(frac(hue + float3(0, 2.0/3.0, 1.0/3.0)) * 6 - 3) - 1);
                rgb = lerp(half3(1,1,1), rgb, .75);
                // Non-rainbow dossier silhouettes use their ordinary body colour.
                rgb = lerp(_BaseColor.rgb, rgb, _RainbowAmount);
                return half4(rgb, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a);
            }
            ENDHLSL
        }
    }
}
