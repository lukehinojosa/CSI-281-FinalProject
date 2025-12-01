Shader "Unlit/UnitFog"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        // Render in the Transparent queue after the Fog Quad
        Tags { "RenderType"="Transparent" "Queue"="Transparent+2" }
        
        // Transparency Blending
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            // A Texture2DArray to look up
            UNITY_DECLARE_TEX2DARRAY(_VisibilityTexArray);
            
            float4 _GridWorldSize;
            float4 _GridBottomLeft;
            
            // This float determines which layer (Player=0, Enemy=1) this camera sees
            float _FowIndex; 

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // Calculate UV
                float2 uv;
                uv.x = (i.worldPos.x - _GridBottomLeft.x) / _GridWorldSize.x;
                uv.y = (i.worldPos.z - _GridBottomLeft.y) / _GridWorldSize.y;

                // Sample the specific Slice of the Array
                float visibility = UNITY_SAMPLE_TEX2DARRAY(_VisibilityTexArray, float3(uv, _FowIndex)).a;

                // Apply
                col.a *= visibility;
                
                col.rgb *= visibility;

                return col;
            }
            ENDCG
        }
    }
}