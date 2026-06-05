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
        Tags { "RenderType"="Opaque" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Threshold;
            float _LightIntensity;
            float _AmbientLight;
            float _ShadowStrength;
            float _Contrast;

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
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.screenPos = ComputeScreenPos(o.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

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

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // 获取纹理的灰度值
                float gray =
                    dot(col.rgb,
                        float3(0.299, 0.587, 0.114));

                // 归一化法线
                float3 normal = normalize(i.worldNormal);

                // 计算主光源光照
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float NdotL = max(0, dot(normal, lightDir));

                // 计算漫反射光照
                float diffuse = NdotL * _LightIntensity;

                // 添加环境光
                float totalLight = diffuse + _AmbientLight;

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