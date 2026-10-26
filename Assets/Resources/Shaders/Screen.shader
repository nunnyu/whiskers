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
            float4 _OutlineColor;
            float _Outlines;
            float4 _GradeTint;
            float _GradeSaturation;
            float _BlurRadius;

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
                float2 uv = (pixel + 0.5) * _MainTex_TexelSize.xy;
                float4 source = tex2D(_MainTex, uv);
                float3 color = source.rgb;
                float2 t = _MainTex_TexelSize.xy;
                float2 r = t * _BlurRadius;

                // Distant world pixels get blurred; characters (alpha above 0.5) stay sharp.
                float blur = source.a < 0.5 ? saturate(source.a / 0.45) : 0;
                if (blur > 0.01)
                {
                    float3 sum = 0;
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            sum += tex2D(_MainTex, uv + r * float2(x, y)).rgb;
                        }
                    }
                    sum += tex2D(_MainTex, uv + r * float2(2, 0)).rgb + tex2D(_MainTex, uv - r * float2(2, 0)).rgb;
                    sum += tex2D(_MainTex, uv + r * float2(0, 2)).rgb + tex2D(_MainTex, uv - r * float2(0, 2)).rgb;
                    color = lerp(color, sum / 13, blur);
                }
            #if !defined(UNITY_COLORSPACE_GAMMA)
                color = LinearToGammaSpace(color);
            #endif

                // One-pixel outline around anything that marked itself as a character.
                if (_Outlines > 0.5 && source.a < 0.5)
                {
                    float neighbor = max(max(tex2D(_MainTex, uv + float2(t.x, 0)).a, tex2D(_MainTex, uv - float2(t.x, 0)).a),
                                         max(tex2D(_MainTex, uv + float2(0, t.y)).a, tex2D(_MainTex, uv - float2(0, t.y)).a));
                    if (neighbor > 0.5)
                    {
                        color = _OutlineColor.rgb;
                    }
                }

                // Dark, washed-out grade over everything.
                color = lerp(dot(color, float3(0.299, 0.587, 0.114)).xxx, color, _GradeSaturation) * _GradeTint.rgb;

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
