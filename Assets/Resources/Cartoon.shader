Shader "Custom/Cartoon"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.55, 0.62, 0.78, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSmoothness ("Shadow Smoothness", Range(0.001, 0.25)) = 0.05
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.2

        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _SpecularThreshold ("Specular Threshold", Range(0, 1)) = 0.7
        _SpecularSmoothness ("Specular Smoothness", Range(0.001, 0.25)) = 0.05
        _SpecularPower ("Specular Power", Range(1, 128)) = 32

        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.25, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 1)) = 0.35

        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.05, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _ShadowColor;
                half4 _HighlightColor;
                half4 _RimColor;
                half _ShadowThreshold;
                half _ShadowSmoothness;
                half _AmbientStrength;
                half _SpecularThreshold;
                half _SpecularSmoothness;
                half _SpecularPower;
                half _RimPower;
                half _RimIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                half3 vertexLighting : TEXCOORD5;
                half fogFactor : TEXCOORD6;
            };

            half ToonStep(half value, half threshold, half smoothness)
            {
                half edge0 = threshold - smoothness * 0.5h;
                half edge1 = threshold + smoothness * 0.5h;
                return smoothstep(edge0, edge1, value);
            }

            half3 EvaluateToonLight(Light light, half3 normalWS, half3 viewDirWS)
            {
                half ndl = saturate(dot(normalWS, light.direction));
                half shadowBand = ToonStep(ndl, _ShadowThreshold, _ShadowSmoothness);

                half3 baseLight = lerp(_ShadowColor.rgb, half3(1.0h, 1.0h, 1.0h), shadowBand);
                half attenuation = light.distanceAttenuation * light.shadowAttenuation;

                half3 halfDir = normalize(light.direction + viewDirWS);
                half ndh = saturate(dot(normalWS, halfDir));
                half specular = pow(ndh, _SpecularPower);
                specular = ToonStep(specular, _SpecularThreshold, _SpecularSmoothness);

                half3 litColor = baseLight * light.color * attenuation;
                litColor += _HighlightColor.rgb * specular * light.color * attenuation;
                return litColor;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                output.shadowCoord = GetShadowCoord(positionInputs);
                output.vertexLighting = VertexLighting(positionInputs.positionWS, output.normalWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _Color.rgb;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 lighting = EvaluateToonLight(mainLight, normalWS, viewDirWS);

                #ifdef _ADDITIONAL_LIGHTS
                uint additionalLightsCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightsCount; ++lightIndex)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                    lighting += EvaluateToonLight(additionalLight, normalWS, viewDirWS);
                }
                #endif

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                lighting += input.vertexLighting;
                #endif

                half rim = 1.0h - saturate(dot(viewDirWS, normalWS));
                rim = pow(rim, _RimPower) * _RimIntensity;

                half3 finalColor = albedo * (_AmbientStrength + lighting);
                finalColor += _RimColor.rgb * rim;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, _Color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _ShadowColor;
                half4 _HighlightColor;
                half4 _RimColor;
                half _ShadowThreshold;
                half _ShadowSmoothness;
                half _AmbientStrength;
                half _SpecularThreshold;
                half _SpecularSmoothness;
                half _SpecularPower;
                half _RimPower;
                half _RimIntensity;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalize(normalWS) * _OutlineWidth;

                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
