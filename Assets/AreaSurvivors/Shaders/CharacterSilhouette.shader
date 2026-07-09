Shader "AreaSurvivors/CharacterSilhouette"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.3,0.95,1,0.72)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _SpriteRect ("Sprite Rect", Vector) = (0,0,1,1)
        _OutlineUv ("Outline UV", Vector) = (0.01,0.01,0,0)
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.05
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
            float4 _SpriteRect;
            float4 _OutlineUv;
            float _AlphaThreshold;

            fixed AlphaInSpriteRect(float2 uv)
            {
                if (uv.x < _SpriteRect.x || uv.x > _SpriteRect.z || uv.y < _SpriteRect.y || uv.y > _SpriteRect.w)
                {
                    return 0;
                }
                return tex2D(_MainTex, uv).a;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed center = AlphaInSpriteRect(i.uv);
                float2 o = _OutlineUv.xy;
                fixed outlineAlpha = 0;

                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(-o.x, 0)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2( o.x, 0)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(0, -o.y)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(0,  o.y)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(-o.x, -o.y)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(-o.x,  o.y)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2( o.x, -o.y)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2( o.x,  o.y)));

                float2 halfO = o * 0.5;
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(-halfO.x, 0)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2( halfO.x, 0)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(0, -halfO.y)));
                outlineAlpha = max(outlineAlpha, AlphaInSpriteRect(i.uv + float2(0,  halfO.y)));

                fixed filledAlpha = max(center, outlineAlpha);
                clip(filledAlpha - _AlphaThreshold);
                return center > _AlphaThreshold
                    ? fixed4(_Color.rgb, center * _Color.a)
                    : fixed4(_OutlineColor.rgb, outlineAlpha * _OutlineColor.a);
            }
            ENDCG
        }
    }
}
