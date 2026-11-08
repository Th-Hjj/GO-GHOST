Shader "GO GHOST/Final Boss Rainbow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _AxeCharge ("Axe charge", Float) = -1
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
                half4 _BaseColor;
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
