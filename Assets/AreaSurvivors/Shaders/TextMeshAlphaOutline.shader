Shader "AreaSurvivors/TextMeshAlphaOutline"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _FaceColor ("Face Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineTexel ("Outline Texel", Vector) = (0.002,0.002,0,0)
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
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
            fixed4 _FaceColor;
            fixed4 _OutlineColor;
            float4 _OutlineTexel;
            float _AlphaThreshold;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed center = SampleAlpha(i.uv);
                float2 o = _OutlineTexel.xy;
                fixed outline = center;

                outline = max(outline, SampleAlpha(i.uv + float2(-o.x, 0)));
                outline = max(outline, SampleAlpha(i.uv + float2( o.x, 0)));
                outline = max(outline, SampleAlpha(i.uv + float2(0, -o.y)));
                outline = max(outline, SampleAlpha(i.uv + float2(0,  o.y)));
                outline = max(outline, SampleAlpha(i.uv + float2(-o.x, -o.y)));
                outline = max(outline, SampleAlpha(i.uv + float2(-o.x,  o.y)));
                outline = max(outline, SampleAlpha(i.uv + float2( o.x, -o.y)));
                outline = max(outline, SampleAlpha(i.uv + float2( o.x,  o.y)));

                fixed4 color = center > _AlphaThreshold ? _FaceColor : _OutlineColor;
                fixed alpha = center > _AlphaThreshold ? center * _FaceColor.a : outline * _OutlineColor.a;
                clip(alpha - _AlphaThreshold);
                return fixed4(color.rgb, alpha);
            }
            ENDCG
        }
    }
}
