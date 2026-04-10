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
                float4 pos   : SV_POSITION;
                float  depth : TEXCOORD0;   // true eye-space depth in meters
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // View-space position: UNITY_MATRIX_MV transforms object→view.
                // In Unity view space Z is negative forward; negate to get positive depth.
                float4 viewPos = mul(UNITY_MATRIX_MV, v.vertex);
                o.depth = -viewPos.z;   // positive metric distance from camera
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float near = _ProjectionParams.y;
                float far  = _ProjectionParams.z;
                // Normalize to [0,1] so it fits in an RFloat texture read by C#
                float normalized = (i.depth - near) / (far - near);
                return fixed4(normalized, normalized, normalized, 1);
            }
            ENDCG
        }
    }
}
