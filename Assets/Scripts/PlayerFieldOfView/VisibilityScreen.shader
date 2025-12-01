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
            #pragma require 2darray // Required for Texture Arrays
            #include "UnityCG.cginc"

            // Updated to use Texture Array instead of single texture
            UNITY_DECLARE_TEX2DARRAY(_VisibilityTexArray);
            
            sampler2D _CameraDepthTexture; // Used for Perspective Depth reconstruction
            
            float4 _GridWorldSize;
            float4 _GridBottomLeft;
            
            // 0 = Player Layer, 1 = Enemy Layer
            float _FowIndex; 

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0; // For Depth
                float3 worldPos : TEXCOORD1;  // For Ortho / Fallback
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                // Pass the actual world position of the quad pixel
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 worldPosToSample;
                
                // Orthographic mode
                if (unity_OrthoParams.w > 0.5)
                {
                    float3 rayOrigin = i.worldPos;
                    float3 rayDir = -UNITY_MATRIX_V[2].xyz; // Camera Forward

                    // Prevent divide by zero
                    if (abs(rayDir.y) < 0.00001) return fixed4(0,0,0,1);

                    // Ray-Plane Intersection with Y=0
                    float t = -rayOrigin.y / rayDir.y;
                    
                    // If t < 0, looking up away from ground
                    if (t < 0) return fixed4(0,0,0,1); 

                    worldPosToSample = rayOrigin + rayDir * t;
                }
                // Perspective mode
                else 
                {
                    float2 uv = i.screenPos.xy / i.screenPos.w;
                    float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                    float linearDepth = Linear01Depth(rawDepth);

                    // Check for Skybox / Far Plane
                    if (linearDepth > 0.999) 
                    {
                        // Fallback to Ray-Plane Intersection if sky is hit
                        float3 rayOrigin = _WorldSpaceCameraPos;
                        float3 rayDir = normalize(i.worldPos - _WorldSpaceCameraPos);

                        if (abs(rayDir.y) < 0.00001) return fixed4(0,0,0,1);
                        float t = -rayOrigin.y / rayDir.y;
                        if (t < 0) return fixed4(0,0,0,1);
                        
                        worldPosToSample = rayOrigin + rayDir * t;
                    }
                    else
                    {
                        // Reconstruct geometry from Depth Buffer
                        float3 rayVector = normalize(i.worldPos - _WorldSpaceCameraPos);
                        float zDepth = LinearEyeDepth(rawDepth);
                        float3 camFwd = -UNITY_MATRIX_V[2].xyz; 
                        float cosAngle = dot(rayVector, camFwd);
                        float dist = zDepth / cosAngle;
                        
                        worldPosToSample = _WorldSpaceCameraPos + rayVector * dist;
                    }
                }

                // Texture mapping
                float2 visibilityUV;
                visibilityUV.x = (worldPosToSample.x - _GridBottomLeft.x) / _GridWorldSize.x;
                visibilityUV.y = (worldPosToSample.z - _GridBottomLeft.y) / _GridWorldSize.y;
                
                // Sample texture array at the index
                float visibility = UNITY_SAMPLE_TEX2DARRAY(_VisibilityTexArray, float3(visibilityUV, _FowIndex)).a;

                // Return black with alpha based on visibility
                return fixed4(0, 0, 0, 1.0 - visibility);
            }
            ENDCG
        }
    }
}