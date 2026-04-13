Shader "Fluid/WaterThreshold"
{
    Properties
    {
        _MainTex ("Metaball Texture", 2D) = "black" {}
        _Threshold ("Threshold", Range(0.01, 1.0)) = 0.4
        _EdgeSmooth ("Edge Smoothness", Range(0.001, 0.2)) = 0.05
        _ShallowColor ("Shallow Color", Color) = (0.3, 0.85, 1.0, 0.7)
        _DeepColor ("Deep Color", Color) = (0.05, 0.3, 0.8, 0.9)
        _EdgeColor ("Edge Color", Color) = (0.6, 0.95, 1.0, 1.0)
        _EdgeWidth ("Edge Width", Range(0.0, 0.2)) = 0.06
        _DepthContrast ("Depth Contrast", Range(0.5, 5.0)) = 2.0
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
            half4 _ShallowColor;
            half4 _DeepColor;
            half4 _EdgeColor;
            float _EdgeWidth;
            float _DepthContrast;

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

                // Hard-ish edge via smoothstep at the threshold
                float alpha = smoothstep(_Threshold - _EdgeSmooth, _Threshold + _EdgeSmooth, value);

                if (alpha < 0.001)
                    discard;

                // How far above threshold — drives shallow vs deep color
                float depth = saturate((value - _Threshold) * _DepthContrast);
                half4 bodyColor = lerp(_ShallowColor, _DeepColor, depth);

                // Bright edge highlight where alpha transitions
                float edgeFactor = 1.0 - smoothstep(0.0, _EdgeWidth, value - _Threshold);
                half4 color = lerp(bodyColor, _EdgeColor, edgeFactor * 0.6);
                color.a *= alpha;

                return color;
            }
            ENDHLSL
        }
    }
}
