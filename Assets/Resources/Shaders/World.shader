Shader "Zinc Whiskers/World"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Color", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Color) = (0, 0, 0, 0)
        _SelfLight ("Self Light", Range(0, 1)) = 0
        _Rim ("Rim Light", Range(0, 1)) = 0
        _Outline ("Outline", Float) = 0
        _Saturation ("Saturation", Range(0, 1)) = 1
        _FogAmount ("Fog Amount", Range(0, 1)) = 1
        _VertexSnap ("Vertex Snap", Range(0, 1)) = 1
        _Smooth ("Smooth Texture", Range(0, 1)) = 0
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
            float4 _WorldFogRange; // x start, y end, z minimum
            float4 _WorldBlurRange; // x start, y end
            float _WorldVertexSnap;
            float _WorldAffineWarp;

            TEXTURE2D(_MainTex);
            SamplerState sampler_point_repeat;
            SamplerState sampler_linear_repeat;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _Emission;
                float _SelfLight;
                float _Rim;
                float _Outline;
                float _Saturation;
                float _FogAmount;
                float _VertexSnap;
                float _Smooth;
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
                float2 fogAndBlur : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                if (_WorldVertexSnap > 0 && _VertexSnap > 0.5)
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

                // Characters never drop below their self light and get a rim so they read against the dark.
                float3 toCamera = _WorldSpaceCameraPos - positionWS;
                float viewDistance = length(toCamera);
                float rim = pow(1 - saturate(dot(normalWS, toCamera / max(viewDistance, 0.0001))), 2) * _Rim;
                light = max(light, _SelfLight) + rim;

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.affineUV = float3(output.uv * positionCS.w, positionCS.w);
                output.light = light;
                float fog = lerp(_WorldFogRange.z, 1, saturate((viewDistance - _WorldFogRange.x) / max(_WorldFogRange.y - _WorldFogRange.x, 0.001)));
                float blur = saturate((viewDistance - _WorldBlurRange.x) / max(_WorldBlurRange.y - _WorldBlurRange.x, 0.001));
                output.fogAndBlur = float2(fog * _FogAmount, blur);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = lerp(input.uv, input.affineUV.xy / input.affineUV.z, _WorldAffineWarp);
                float3 albedo = (_Smooth > 0.5 ? SAMPLE_TEXTURE2D(_MainTex, sampler_linear_repeat, uv).rgb
                                               : SAMPLE_TEXTURE2D(_MainTex, sampler_point_repeat, uv).rgb) * _Color.rgb;
                // Only the surface color is desaturated, so warm lantern light still tints it.
                albedo = lerp(dot(albedo, float3(0.299, 0.587, 0.114)).xxx, albedo, _Saturation);
                float3 color = albedo * input.light + _Emission.rgb;
                color = lerp(color, _WorldFogColor.rgb, input.fogAndBlur.x);
                // Alpha tells the screen pass what this pixel is: 1 for characters (outlined, kept sharp),
                // 0 to 0.45 for the world (how much distance blur it gets).
                return half4(color, _Outline > 0.5 ? 1 : input.fogAndBlur.y * 0.45);
            }
            ENDHLSL
        }
    }
}
