Shader "Custom/ObraDinnDither"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Threshold ("Threshold", Range(0,1)) = 0.5
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Threshold;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.screenPos = ComputeScreenPos(o.vertex);

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

                float gray =
                    dot(col.rgb,
                        float3(0.299, 0.587, 0.114));

                float2 screenUV =
                    i.screenPos.xy / i.screenPos.w;

                int2 pixel =
                    int2(screenUV * _ScreenParams.xy);

                float threshold =
                    Bayer4x4(pixel);

                float result =
                    gray > threshold ? 1.0 : 0.0;

                return float4(result, result, result, 1);
            }

            ENDHLSL
        }
    }
}