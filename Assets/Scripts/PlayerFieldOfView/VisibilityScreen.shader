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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Pass the world position of the quad pixel to the fragment shader
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 rayOrigin;
                float3 rayDir;

                // Check if in Orthographic mode (w component is 1)
                if (unity_OrthoParams.w > 0.5)
                {
                    // Orthographic
                    // Origin is the pixel on the quad itself
                    rayOrigin = i.worldPos;
                    // Direction is constant: the camera's forward vector
                    rayDir = -UNITY_MATRIX_V[2].xyz; 
                }
                else
                {
                    // Perspective
                    // Origin is the camera itself
                    rayOrigin = _WorldSpaceCameraPos;
                    // Direction is the vector from camera to the pixel on the quad
                    rayDir = normalize(i.worldPos - _WorldSpaceCameraPos);
                }

                // Ray-plane Intersection
                // Formula: Pos = Origin + Dir * t
                // Pos.y must be 0.
                // 0 = Origin.y + Dir.y * t
                // t = -Origin.y / Dir.y

                // Prevent division by zero if looking perfectly horizontal
                if (abs(rayDir.y) < 0.00001) return fixed4(0,0,0,1);

                float t = -rayOrigin.y / rayDir.y;

                // If t < 0, the intersection is behind the camera (or looking up at the sky)
                if (t < 0) return fixed4(0,0,0,1);

                // Calculate the actual point on the ground
                float3 groundPos = rayOrigin + rayDir * t;

                // Map to texture
                float2 visibilityUV;
                visibilityUV.x = (groundPos.x - _GridBottomLeft.x) / _GridWorldSize.x;
                visibilityUV.y = (groundPos.z - _GridBottomLeft.y) / _GridWorldSize.y;
                
                // Sample texture
                float visibility = tex2D(_VisibilityTex, visibilityUV).a;

                // Return black with alpha based on visibility
                return fixed4(0, 0, 0, 1.0 - visibility);
            }
            ENDCG
        }
    }
}