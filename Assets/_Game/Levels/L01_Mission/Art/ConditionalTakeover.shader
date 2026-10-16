Shader "GO GHOST/Conditional Takeover"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white" {}
        _Color("Tint",Color)=(1,1,1,1)
        [HDR] _AIColor("AI highlight",Color)=(0.25,1.6,2.4,1)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _AIColor;
            half _AIStrength;
            CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            Varyings vert(Attributes v)
            {Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS);o.uv=v.uv;o.color=v.color*_Color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
                // Multiplication keeps the existing reload/health tint rather than replacing it.
                c.rgb*=lerp(half3(1,1,1),_AIColor.rgb,min(_AIStrength,1));
                c.rgb+=c.rgb*_AIStrength*.65;
                return c;
            }
            ENDHLSL
        }
    }
}
