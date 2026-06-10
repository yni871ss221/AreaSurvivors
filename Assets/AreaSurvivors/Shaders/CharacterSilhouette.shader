Shader "AreaSurvivors/CharacterSilhouette"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.3,0.95,1,0.72)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Stencil
        {
            Ref 1
            Comp Equal
            Pass Keep
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, i.uv).a;
                float2 p = _MainTex_TexelSize.xy * 2.0;
                fixed nearAlpha = max(max(tex2D(_MainTex, i.uv + float2(p.x, 0)).a,
                                          tex2D(_MainTex, i.uv - float2(p.x, 0)).a),
                                      max(tex2D(_MainTex, i.uv + float2(0, p.y)).a,
                                          tex2D(_MainTex, i.uv - float2(0, p.y)).a));
                clip(max(alpha, nearAlpha) - 0.05);
                return alpha > 0.05
                    ? fixed4(_Color.rgb, alpha * _Color.a)
                    : fixed4(_OutlineColor.rgb, nearAlpha * _OutlineColor.a);
            }
            ENDCG
        }
    }
}
