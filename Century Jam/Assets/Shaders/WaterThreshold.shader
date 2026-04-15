Shader "Fluid/WaterThreshold"
{
    Properties
    {
        _MainTex ("Metaball Texture", 2D) = "black" {}
        _Threshold ("Threshold", Range(0.01, 1.0)) = 0.4
        _EdgeSmooth ("Edge Smoothness", Range(0.001, 0.2)) = 0.05
        _WaterColor ("Water Color", Color) = (0.15, 0.5, 0.95, 0.9)
        _EdgeColor ("Edge Color", Color) = (0.4, 0.75, 1.0, 1.0)
        _EdgeWidth ("Edge Width", Range(0.0, 0.2)) = 0.06
        [HDR] _EmissionColor ("Emission Color", Color) = (0.3, 0.8, 2.0, 1.0)
        _EmissionIntensity ("Emission Intensity", Range(0.0, 5.0)) = 1.5
        _GlowColor ("Glow Color", Color) = (0.2, 0.6, 1.0, 0.5)
        _GlowWidth ("Glow Width", Range(0.0, 0.3)) = 0.1
        _GlowIntensity ("Glow Intensity", Range(0.0, 3.0)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _Threshold;
            float _EdgeSmooth;
            half4 _WaterColor;
            half4 _EdgeColor;
            float _EdgeWidth;
            half4 _EmissionColor;
            float _EmissionIntensity;
            half4 _GlowColor;
            float _GlowWidth;
            float _GlowIntensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float value = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).r;

                // Water body
                float alpha = smoothstep(_Threshold - _EdgeSmooth, _Threshold + _EdgeSmooth, value);

                // Outer glow halo — visible below the threshold
                float glowAlpha = smoothstep(_Threshold - _GlowWidth - _EdgeSmooth, _Threshold - _EdgeSmooth, value);
                float glowOnly = glowAlpha * (1.0 - alpha); // Only in the halo region

                if (alpha < 0.001 && glowOnly < 0.001)
                    discard;

                // Water body color with bright edge
                float edgeFactor = 1.0 - smoothstep(0.0, _EdgeWidth, value - _Threshold);
                half4 bodyColor = lerp(_WaterColor, _EdgeColor, edgeFactor * 0.5);

                // Add HDR emission so bloom picks it up
                bodyColor.rgb += _EmissionColor.rgb * _EmissionIntensity * alpha;

                // Brighter emission at the edge for extra glow
                bodyColor.rgb += _EmissionColor.rgb * edgeFactor * _EmissionIntensity * 0.5;

                // Combine body + outer glow
                half4 color;
                color.rgb = lerp(_GlowColor.rgb * _GlowIntensity, bodyColor.rgb, alpha);
                color.a = saturate(alpha + glowOnly * _GlowColor.a);

                return color;
            }
            ENDHLSL
        }
    }
}
