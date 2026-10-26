Shader "Hidden/Zinc Whiskers/Screen"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }

    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _ColorSteps;
            float _Dither;
            float _Grain;
            float _Vignette;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float bayer4(int2 p)
            {
                static const float m[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                return m[(p.y & 3) * 4 + (p.x & 3)] / 16.0 - 0.5;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pixel = floor(i.uv * _MainTex_TexelSize.zw);
                float3 color = tex2D(_MainTex, i.uv).rgb;
            #if !defined(UNITY_COLORSPACE_GAMMA)
                color = LinearToGammaSpace(color);
            #endif

                float frame = floor(_Time.y * 12);
                color += (hash(pixel + frame * 17.31) - 0.5) * _Grain;

                float2 fromCenter = i.uv - 0.5;
                color *= saturate(1 - dot(fromCenter, fromCenter) * _Vignette);

                color = floor(color * _ColorSteps + 0.5 + bayer4(int2(pixel)) * _Dither) / _ColorSteps;
                color = saturate(color);

            #if !defined(UNITY_COLORSPACE_GAMMA)
                color = GammaToLinearSpace(color);
            #endif
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
}
