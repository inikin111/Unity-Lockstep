Shader "Custom/ObraDinnDither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold ("Threshold", Range(0,1)) = 0.5
        _LightIntensity ("Light Intensity", Range(0,5)) = 2.0
        _AmbientLight ("Ambient Light", Range(0,1)) = 0.1
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.8
        _Contrast ("Contrast", Range(0.5,3.0)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Threshold;
                float _LightIntensity;
                float _AmbientLight;
                float _ShadowStrength;
                float _Contrast;
            CBUFFER_END

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                float3 vertexLighting : TEXCOORD5;
            };

            v2f vert(appdata v)
            {
                v2f o;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.vertex.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normal);

                o.vertex = positionInputs.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.worldPos = positionInputs.positionWS;
                o.worldNormal = normalize(normalInputs.normalWS);
                o.shadowCoord = GetShadowCoord(positionInputs);
                o.vertexLighting = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);

                return o;
            }

            float Bayer4x4(int2 pixel)
            {
                static const float bayer[16] =
                {
                     0,  8,  2, 10,
                    12,  4, 14,  6,
                     3, 11,  1,  9,
                    15,  7, 13,  5
                };

                int x = pixel.x & 3;
                int y = pixel.y & 3;

                return bayer[y * 4 + x] / 16.0;
            }

            float ComputeGray(float3 color)
            {
                return dot(color, float3(0.299, 0.587, 0.114));
            }

            float EvaluateLightContribution(Light light, float3 normalWS)
            {
                float NdotL = saturate(dot(normalWS, light.direction));
                float attenuation = light.distanceAttenuation * light.shadowAttenuation;
                float intensity = ComputeGray(light.color);
                return NdotL * attenuation * intensity;
            }

            half4 frag(v2f i) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                // 获取纹理的灰度值
                float gray =
                    dot(col.rgb,
                        float3(0.299, 0.587, 0.114));

                // 归一化法线
                float3 normal = normalize(i.worldNormal);

                Light mainLight = GetMainLight(i.shadowCoord);
                float totalDiffuse = EvaluateLightContribution(mainLight, normal);

                #ifdef _ADDITIONAL_LIGHTS
                uint additionalLightsCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0; lightIndex < additionalLightsCount; ++lightIndex)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, i.worldPos);
                    totalDiffuse += EvaluateLightContribution(additionalLight, normal);
                }
                #endif

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                totalDiffuse += ComputeGray(i.vertexLighting);
                #endif

                // 添加环境光
                float totalLight = totalDiffuse * _LightIntensity + _AmbientLight;

                // 应用对比度增强光照效果
                totalLight = (totalLight - 0.5) * _Contrast + 0.5;
                totalLight = saturate(totalLight);

                // 将光照应用到纹理灰度上
                float litGray = gray * totalLight;

                // 计算屏幕坐标用于抖动
                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                int2 pixel =
                    int2(screenUV * _ScreenParams.xy);

                float threshold =
                    Bayer4x4(pixel);

                // 应用抖动，但根据光照调整阈值范围
                float adjustedThreshold = threshold * (1.0 - _ShadowStrength * 0.5) + _ShadowStrength * 0.25;
                
                float result =
                    litGray > adjustedThreshold ? 1.0 : 0.0;

                return float4(result, result, result, 1);
            }

            ENDHLSL
        }
    }
}
