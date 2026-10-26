Shader "Zinc Whiskers/Glow"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (1, 0.75, 0.4, 1)
        _Intensity ("Intensity", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            // Leave alpha alone; the screen pass reads it.
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 toCamera : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.toCamera = _WorldSpaceCameraPos - positionWS;
                return output;
            }

            // Bright in the middle of the sphere, fading to nothing at its edges, so it reads as a soft halo.
            half4 frag(Varyings input) : SV_Target
            {
                float facing = saturate(dot(normalize(input.normalWS), normalize(input.toCamera)));
                return half4(_Color.rgb * pow(facing, 3) * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
