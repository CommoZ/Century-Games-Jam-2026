Shader "Fluid/MetaballParticle"
{
    Properties
    {
        _Softness ("Softness", Range(0.01, 2.0)) = 1.0
        _Intensity ("Intensity", Range(0.1, 5.0)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Blend One One // Additive blending
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

            float _Softness;
            float _Intensity;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Distance from center of quad (0,0) to edge is 0.5
                float2 center = input.uv - 0.5;
                float dist = length(center) * 2.0; // 0 at center, 1 at edge

                // Smooth radial falloff
                float falloff = saturate(1.0 - dist / _Softness);
                falloff = falloff * falloff; // Quadratic falloff for smoother blobs

                float value = falloff * _Intensity;
                return half4(value, value, value, value);
            }
            ENDHLSL
        }
    }
}
