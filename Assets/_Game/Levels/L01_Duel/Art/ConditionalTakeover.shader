Shader "GO GHOST/Conditional Takeover"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        _AIColor("Body system signal",Color)=(1,1,1,1)
        _AIStrength("Takeover",Range(0,2))=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags {"LightMode"="Universal2D"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _AIColor;
            half _AIStrength;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                o.positionCS=TransformObjectToHClip(UnityFlipSprite(v.positionOS,unity_SpriteProps.xy));
                o.uv=v.uv;
                // Sprite tint lives in vertex color or per-draw data depending on batching.
                o.color=v.color*_Color*unity_SpriteColor;
                return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
                // Tint the complete sprite, retaining its alpha and the existing signal/fade curve.
                half signal=saturate(_AIStrength);
                half weight=smoothstep(0,.15,signal);
                half3 signalColor=_AIColor.rgb*(.55+.65*signal);
                c.rgb=lerp(c.rgb,c.rgb*signalColor,weight);
                c.rgb+=_AIColor.rgb*signal*.15;
                return c;
            }
            ENDHLSL
        }
    }
}
