Shader "Unlit/VisibilityScreen"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _VisibilityTex;
            float4 _GridWorldSize;
            float4 _GridBottomLeft;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 worldIntersectPos = i.worldPos;
                
                float2 visibilityUV;
                visibilityUV.x = (worldIntersectPos.x - _GridBottomLeft.x) / _GridWorldSize.x;
                visibilityUV.y = (worldIntersectPos.z - _GridBottomLeft.y) / _GridWorldSize.y;
                
                float visibility = tex2D(_VisibilityTex, visibilityUV).a;
                
                return fixed4(0, 0, 0, 1.0 - visibility);
            }
            ENDCG
        }
    }
}