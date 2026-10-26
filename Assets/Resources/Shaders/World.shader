Shader "Zinc Whiskers/World"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Color", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Color) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_LIGHTS 16

            // Set every frame by WorldLook.
            float4 _WorldLightPositions[MAX_LIGHTS]; // xyz position, w range
            float4 _WorldLightColors[MAX_LIGHTS];
            float _WorldLightCount;
            float4 _WorldAmbient;
            float4 _WorldFogColor;
            float4 _WorldFogRange; // x start, y end
            float _WorldVertexSnap;
            float _WorldAffineWarp;

            TEXTURE2D(_MainTex);
            SamplerState sampler_point_repeat;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _Emission;
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
                float3 affineUV : TEXCOORD1;
                float3 light : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                if (_WorldVertexSnap > 0)
                {
                    float2 grid = float2(_WorldVertexSnap * _ScreenParams.x / _ScreenParams.y, _WorldVertexSnap) * 0.5;
                    positionCS.xy = floor(positionCS.xy / positionCS.w * grid + 0.5) / grid * positionCS.w;
                }

                float3 light = _WorldAmbient.rgb;
                [loop] for (int i = 0; i < (int)_WorldLightCount; i++)
                {
                    float3 toLight = _WorldLightPositions[i].xyz - positionWS;
                    float lightDistance = max(length(toLight), 0.0001);
                    float falloff = saturate(1 - lightDistance / _WorldLightPositions[i].w);
                    float facing = saturate(dot(normalWS, toLight / lightDistance) * 0.7 + 0.3);
                    light += _WorldLightColors[i].rgb * falloff * falloff * facing;
                }

                float viewDistance = length(positionWS - _WorldSpaceCameraPos);

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.affineUV = float3(output.uv * positionCS.w, positionCS.w);
                output.light = light;
                output.fog = saturate((viewDistance - _WorldFogRange.x) / max(_WorldFogRange.y - _WorldFogRange.x, 0.001));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = lerp(input.uv, input.affineUV.xy / input.affineUV.z, _WorldAffineWarp);
                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_point_repeat, uv).rgb * _Color.rgb;
                float3 color = albedo * input.light + _Emission.rgb;
                color = lerp(color, _WorldFogColor.rgb, input.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
