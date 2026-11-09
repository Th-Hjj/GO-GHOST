Shader "GO GHOST/Final Boss EMP Sweep"
{
    Properties
    {
        [PerRendererData] _MainTex ("Silhouette", 2D) = "white" {}
        _Tint ("Settled violet", Color) = (0.5, 0.3, 0.7, 0.7)
        _FrontColor ("Wave edge", Color) = (0.7, 0.55, 1, 0.85)
        _Radius ("Wave radius", Float) = -1
        _Origin ("Wave origin", Vector) = (0,0,0,0)
        _ScreenScale ("World units per HUD pixel", Float) = 0
        _Trail ("Transient contact width", Float) = 0
        _EdgeFadeWidth ("Distance from peak to settled violet", Float) = 0.22
        _CityContours ("Use architectural contours", Float) = 0
        _CityCrops ("Far and near parallax crops", Vector) = (0,1,0,1)
        [HideInInspector] _ClipRect ("UI clip", Vector) = (-32767,-32767,32767,32767)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            float4 _Origin, _ClipRect;
            float _Radius, _ScreenScale, _Trail, _EdgeFadeWidth, _CityContours;
            float4 _CityCrops;
            half4 _Tint, _FrontColor;
            struct Varyings
            {
                float4 clip : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
                float4 screen : TEXCOORD2;
                float2 local : TEXCOORD3;
                half alpha : COLOR;
            };
            Varyings vert(appdata_full v)
            {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.screen = ComputeScreenPos(o.clip);
                o.world = mul(unity_ObjectToWorld, v.vertex).xy;
                o.local = v.vertex.xy;
                o.uv = v.texcoord.xy;
                o.alpha = v.color.a;
                return o;
            }
            half outerRim(float distance, float width)
            {
                // Paint only the sky side, like the expanded sprite behind each platform.
                float aa = max(0.18, 0.5 * fwidth(distance));
                return smoothstep(-aa, aa, distance) - smoothstep(width - aa, width + aa, distance);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 delta = i.world - _Origin.xy;
                if (_ScreenScale > 0)
                    delta = (i.screen.xy / i.screen.w * _ScreenParams.xy - _Origin.xy) * _ScreenScale;
                float behind = _Radius - length(delta);
                half coverage = smoothstep(-0.045, 0.045, behind);
                half4 ink = lerp(_Tint, _FrontColor, 1 - smoothstep(0, _EdgeFadeWidth, behind));
                if (_Trail > 0) coverage *= 1 - smoothstep(0, _Trail, behind);
                half mask;
                if (_CityContours > 0.5)
                {
                    // R/G are signed distances in the far/near plates (±4 source texels).
                    float2 farUV = float2(_CityCrops.x + i.uv.x * _CityCrops.y, i.uv.y);
                    float2 nearUV = float2(_CityCrops.z + i.uv.x * _CityCrops.w, i.uv.y);
                    float farDistance = (tex2D(_MainTex, farUV).r - 0.5) * 8;
                    float3 nearShape = tex2D(_MainTex, nearUV).rgb;
                    float nearDistance = (nearShape.g - 0.5) * 8;
                    half farRim = outerRim(farDistance, 0.9) * (1 - nearShape.b) * 0.52;
                    mask = max(outerRim(nearDistance, 1.15), farRim);
                }
                else mask = tex2D(_MainTex, i.uv).a;
                ink.a *= coverage * mask * i.alpha;
                #ifdef UNITY_UI_CLIP_RECT
                    ink.a *= UnityGet2DClipping(i.local, _ClipRect);
                #endif
                return ink;
            }
            ENDCG
        }
    }
}
