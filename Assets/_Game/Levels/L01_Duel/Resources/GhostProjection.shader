Shader "GO GHOST/Ghost Projection"
{
    // Projected light in the GHOST arena: the vertical strokes of the letters. Flat colour in rising rows,
    // the same rows GHOST's body is made of. Bodies and bullets pass through it.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Period ("Row height", Float) = .0556
        _Speed ("Rise per second", Float) = .1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Period, _Speed;
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
                o.height = mul(unity_ObjectToWorld, v.vertex).y;
                o.colour = v.color;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                float row = frac((i.height - _Time.y * _Speed) / _Period) < .62 ? 1 : .24;
                fixed4 colour = i.colour;
                colour.a *= tex2D(_MainTex, i.uv).a * row;
                return colour;
            }
            ENDCG
        }
    }
}
