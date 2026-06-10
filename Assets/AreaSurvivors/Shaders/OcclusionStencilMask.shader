Shader "AreaSurvivors/OcclusionStencilMask"
{
    Properties
    {
        _OccluderTex ("Occluder Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="TransparentCutout" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        ColorMask 0
        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };
            sampler2D _OccluderTex;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                clip(tex2D(_OccluderTex, i.uv).a - 0.05);
                return 0;
            }
            ENDCG
        }
    }
}
