Shader "Hidden/DepthCapture"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float depth : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = COMPUTE_DEPTH_01;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = i.depth;
                return fixed4(d, d, d, 1);
            }
            ENDCG
        }
    }
}
