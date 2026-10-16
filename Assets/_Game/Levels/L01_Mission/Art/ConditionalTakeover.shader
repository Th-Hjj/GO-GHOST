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
                // A narrow signal core remains readable without Bloom. The tint never reaches the head.
                half signal=saturate(_AIStrength);
                half core=1-smoothstep(lerp(.08,.18,signal),lerp(.22,.46,signal),abs(i.uv.y-.5));
                half weight=smoothstep(0,.15,signal)*core;
                half3 signalColor=_AIColor.rgb*(.55+.65*signal);
                c.rgb=lerp(c.rgb,c.rgb*signalColor,weight);
                c.rgb+=_AIColor.rgb*core*signal*.15;
                return c;
            }
            ENDHLSL
        }
    }
}
