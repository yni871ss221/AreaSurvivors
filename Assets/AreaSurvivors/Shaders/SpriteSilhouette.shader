Shader "AreaSurvivors/SpriteAlphaOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _SpriteRect ("Sprite Rect", Vector) = (0,0,1,1)
        _OutlineUv ("Outline UV", Vector) = (0.01,0.01,0,0)
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local __ AREA_OUTLINE_CROWD_OPTIMIZED
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
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
                fixed alpha = 0;

                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(-o.x, 0)));
                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2( o.x, 0)));
                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(0, -o.y)));
                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(0,  o.y)));
                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(-o.x, -o.y)));
                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(-o.x,  o.y)));
                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2( o.x, -o.y)));
                alpha = max(alpha, AlphaInSpriteRect(i.uv + float2( o.x,  o.y)));

                #if !defined(AREA_OUTLINE_CROWD_OPTIMIZED)
                    float2 halfO = o * 0.5;
                    alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(-halfO.x, 0)));
                    alpha = max(alpha, AlphaInSpriteRect(i.uv + float2( halfO.x, 0)));
                    alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(0, -halfO.y)));
                    alpha = max(alpha, AlphaInSpriteRect(i.uv + float2(0,  halfO.y)));
                #endif

                fixed outlineAlpha = max(center, alpha);
                clip(outlineAlpha - _AlphaThreshold);
                return fixed4(_Color.rgb, outlineAlpha * _Color.a);
            }
            ENDCG
        }
    }
}
